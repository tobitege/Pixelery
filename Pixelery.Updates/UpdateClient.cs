using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using NetSparkleUpdater.Enums;
using NetSparkleUpdater.SignatureVerifiers;

namespace Pixelery.Updates;

public sealed record UpdateRelease(Version Version, Uri Download, long Length, string Signature);

/// <summary>Downloads the signed stable feed and NSIS payload without reflection or background update checks.</summary>
public sealed class UpdateClient(HttpClient http, string publicKey)
{
    public static string PublicKey { get; } = ReadPublicKey();
    public static readonly Uri Feed = new("https://github.com/tobitege/Pixelery/releases/latest/download/appcast.xml");
    private const long MaximumInstallerSize = 512L * 1024 * 1024;
    private static readonly XNamespace Sparkle = "http://www.andymatuschak.org/xml-namespaces/sparkle";

    private static string ReadPublicKey()
    {
        using var stream = typeof(UpdateClient).Assembly.GetManifestResourceStream("Pixelery.Updates.PublicKey")
            ?? throw new InvalidOperationException("Missing update public key.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }

    public async Task<UpdateRelease?> CheckAsync(Version current, CancellationToken cancellationToken)
    {
        var data = await ReadBoundedAsync(Feed, 1024 * 1024, cancellationToken).ConfigureAwait(false);
        var signature = Encoding.UTF8.GetString(await ReadBoundedAsync(new Uri(Feed + ".signature"), 1024, cancellationToken).ConfigureAwait(false)).Trim();
        Verify(data, signature);
        using var input = new MemoryStream(data);
        using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 });
        var document = XDocument.Load(reader);
        if (document.Root?.Name != "rss" || document.Root.Element("channel") is not { } channel)
            throw new InvalidDataException("Invalid update feed.");

        UpdateRelease? latest = null;
        foreach (var enclosure in channel.Elements("item").Elements("enclosure"))
        {
            if ((string?)enclosure.Attribute(Sparkle + "os") != "windows-x64")
                continue;
            var versionText = (string?)enclosure.Attribute(Sparkle + "version");
            if (!Version.TryParse(versionText, out var version) || version.Build < 0 || version.Revision > 0)
                throw new InvalidDataException("Invalid update version.");
            // Three-part release versions and four-part assembly versions compare equally.
            version = new Version(version.Major, version.Minor, version.Build);
            var installed = new Version(current.Major, current.Minor, Math.Max(0, current.Build));
            if (version <= installed || latest?.Version >= version)
                continue;
            var expected = new Uri($"https://github.com/tobitege/Pixelery/releases/download/v{version}/Pixelery-Setup-{version}-x64.exe");
            if (!Uri.TryCreate((string?)enclosure.Attribute("url"), UriKind.Absolute, out var url) || url != expected ||
                !long.TryParse((string?)enclosure.Attribute("length"), NumberStyles.None, CultureInfo.InvariantCulture, out var length) || length <= 0 || length > MaximumInstallerSize)
                throw new InvalidDataException("Invalid update download.");
            signature = (string?)enclosure.Attribute(Sparkle + "signature") ?? "";
            if (Convert.FromBase64String(signature).Length != 64)
                throw new InvalidDataException("Invalid update signature.");
            latest = new UpdateRelease(version, url, length, signature);
        }
        return latest;
    }

    public async Task<UpdateDownload> DownloadAsync(UpdateRelease release, string stateDirectory, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        UpdateDownload.CleanupAbandoned(stateDirectory);
        var download = UpdateDownload.Create(stateDirectory);
        try
        {
            var path = download.Path;
            using var response = await http.GetAsync(release.Download, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is { } length && length != release.Length)
                throw new InvalidDataException("Update size mismatch.");
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                var buffer = new byte[81920];
                long written = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
                {
                    written += read;
                    if (written > release.Length || written > MaximumInstallerSize)
                        throw new InvalidDataException("Update exceeds expected size.");
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    progress?.Report(100.0 * written / release.Length);
                }
                if (written != release.Length)
                    throw new InvalidDataException("Incomplete update download.");
                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Run(() => VerifyFile(path, release), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return download;
        }
        catch
        {
            download.Dispose();
            throw;
        }
    }

    public Process StartInstaller(string path, UpdateRelease release, int processId)
    {
        // Keep the verified executable read-only until CreateProcess has opened it.
        using var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        VerifyFile(path, release);
        return Process.Start(new ProcessStartInfo(path)
        {
            UseShellExecute = false,
            Arguments = $"/S /UPDATEPID={processId.ToString(CultureInfo.InvariantCulture)} /RELAUNCH=1"
        }) ?? throw new IOException("Cannot start the update installer.");
    }

    public void VerifyFile(string path, UpdateRelease release)
    {
        if (new FileInfo(path).Length != release.Length)
            throw new InvalidDataException("Update size mismatch.");
        var verifier = new Ed25519Checker(SecurityMode.Strict, publicKey, publicKeyFile: null, readFileBeingVerifiedInChunks: true);
        if (verifier.VerifySignatureOfFile(release.Signature, path) != ValidationResult.Valid)
            throw new InvalidDataException("Update signature verification failed.");
    }

    private void Verify(byte[] bytes, string signature)
    {
        var verifier = new Ed25519Checker(SecurityMode.Strict, publicKey, publicKeyFile: null);
        if (verifier.VerifySignature(signature, bytes) != ValidationResult.Valid)
            throw new InvalidDataException("Update feed signature verification failed.");
    }

    private async Task<byte[]> ReadBoundedAsync(Uri uri, int maximum, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        int read;
        while ((read = await source.ReadAsync(block, cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (buffer.Length + read > maximum)
                throw new InvalidDataException("Update metadata is too large.");
            buffer.Write(block, 0, read);
        }
        return buffer.ToArray();
    }
}
