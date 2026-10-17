using System.Net;
using System.Text;
using Chaos.NaCl;
using Pixelery.Updates;

namespace Pixelery.Core.Tests;

public sealed class UpdateTests
{
    private readonly byte[] _seed = Convert.FromBase64String(Ed25519.GeneratePrivateKeySeed());
    private static readonly byte[] Installer = Encoding.UTF8.GetBytes("A signed installer fixture");

    private string Sign(byte[] data) => Convert.ToBase64String(Ed25519.Sign(data, Ed25519.ExpandedPrivateKeyFromSeed(_seed)));

    private byte[] Feed(string version = "0.3.0", string? url = null, string os = "windows-x64", string? length = null) => Encoding.UTF8.GetBytes($"""
        <rss xmlns:sparkle="http://www.andymatuschak.org/xml-namespaces/sparkle"><channel><item>
        <enclosure sparkle:version="{version}" sparkle:os="{os}" url="{url ?? $"https://github.com/tobitege/Pixelery/releases/download/v{version}/Pixelery-Setup-{version}-x64.exe"}"
          length="{length ?? Installer.Length.ToString()}" sparkle:signature="{Sign(Installer)}" />
        </item></channel></rss>
        """);

    private (UpdateClient Client, HttpClient Http) Client(byte[] feed, byte[]? payload = null, string? signature = null)
    {
        var http = new HttpClient(new Handler(request =>
        {
            var url = request.RequestUri!.AbsoluteUri;
            var data = url.EndsWith(".signature", StringComparison.Ordinal) ? Encoding.UTF8.GetBytes(signature ?? Sign(feed))
                : url.EndsWith(".xml", StringComparison.Ordinal) ? feed : payload ?? Installer;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
        }));
        return (new UpdateClient(http, Convert.ToBase64String(Ed25519.PublicKeyFromSeed(_seed))), http);
    }

    [Fact]
    public async Task SignedFeedAndDownloadAreVerifiedBeforeUse()
    {
        var (client, http) = Client(Feed());
        using (http)
        {
            var release = Assert.IsType<UpdateRelease>(await client.CheckAsync(new Version(0, 2, 0, 0), default));
            var root = TestData.NewDirectory();
            using var download = await client.DownloadAsync(release, root, null, default);
            var path = download.Path;
            Assert.StartsWith(Path.Combine(Path.GetFullPath(root), "Updates") + Path.DirectorySeparatorChar, path, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(Installer, await File.ReadAllBytesAsync(path));
            client.VerifyFile(path, release);
            var tampered = (byte[])Installer.Clone();
            tampered[0] ^= 1;
            await File.WriteAllBytesAsync(path, tampered);
            Assert.Throws<InvalidDataException>(() => client.VerifyFile(path, release));
            Assert.Throws<InvalidDataException>(() => client.StartInstaller(path, release, Environment.ProcessId));
        }
    }

    [Fact]
    public async Task RejectsTamperedFeed()
    {
        var (client, http) = Client(Feed(), signature: Sign(Encoding.UTF8.GetBytes("different feed")));
        using (http)
            await Assert.ThrowsAsync<InvalidDataException>(() => client.CheckAsync(new Version(0, 2, 0), default));
    }

    [Theory]
    [InlineData("http://github.com/tobitege/Pixelery/releases/download/v0.3.0/Pixelery-Setup-0.3.0-x64.exe", "24")]
    [InlineData("https://example.org/installer.exe", "24")]
    [InlineData("https://github.com/another/repo/releases/download/v0.3.0/Pixelery-Setup-0.3.0-x64.exe", "24")]
    [InlineData(null, "0")]
    [InlineData(null, "536870913")]
    public async Task RejectsUnsafeSignedMetadata(string? url, string length)
    {
        var (client, http) = Client(Feed(url: url, length: length));
        using (http)
            await Assert.ThrowsAsync<InvalidDataException>(() => client.CheckAsync(new Version(0, 2, 0), default));
    }

    [Theory]
    [InlineData("0.2.0", "windows-x64")]
    [InlineData("0.1.0", "windows-x64")]
    [InlineData("0.3.0", "windows-arm64")]
    public async Task DoesNotOfferOldOrWrongArchitectureRelease(string version, string os)
    {
        var (client, http) = Client(Feed(version, os: os));
        using (http)
            Assert.Null(await client.CheckAsync(new Version(0, 2, 0, 0), default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectsChangedOrTruncatedInstaller(bool truncate)
    {
        var payload = truncate ? Installer[..^1] : Enumerable.Repeat((byte)0, Installer.Length).ToArray();
        var (client, http) = Client(Feed(), payload);
        using (http)
        {
            var release = (await client.CheckAsync(new Version(0, 2, 0), default))!;
            var root = TestData.NewDirectory();
            await Assert.ThrowsAsync<InvalidDataException>(() => client.DownloadAsync(release, root, null, default));
            Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "Updates")));
        }
    }

    [Fact]
    public async Task CancellationAfterWritingRemovesTheClosedPartialFile()
    {
        var (client, http) = Client(Feed());
        using (http)
        using (var cancellation = new CancellationTokenSource())
        {
            var release = (await client.CheckAsync(new Version(0, 2, 0), default))!;
            var root = TestData.NewDirectory();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.DownloadAsync(release, root,
                new CancelProgress(cancellation), cancellation.Token));
            Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, "Updates")));
        }
    }

    [Fact]
    public async Task CleanupKeepsActiveDownloadsAndDisposeRemovesSupersededDownload()
    {
        var (client, http) = Client(Feed());
        using (http)
        {
            var release = (await client.CheckAsync(new Version(0, 2, 0), default))!;
            var root = TestData.NewDirectory();
            var first = await client.DownloadAsync(release, root, null, default);
            using var second = await client.DownloadAsync(release, root, null, default);
            UpdateDownload.CleanupAbandoned(root);
            Assert.True(File.Exists(first.Path));
            Assert.True(File.Exists(second.Path));
            first.Dispose();
            first.Dispose();
            Assert.False(Directory.Exists(Path.GetDirectoryName(first.Path)));
            Assert.True(File.Exists(second.Path));
        }
    }

    [Fact]
    public async Task CleanupPreservesRunningInstallerThenRemovesAbandonedPayload()
    {
        var (client, http) = Client(Feed());
        using (http)
        {
            var release = (await client.CheckAsync(new Version(0, 2, 0), default))!;
            var root = TestData.NewDirectory();
            using var download = await client.DownloadAsync(release, root, null, default);
            download.HandOffToInstaller();
            using (var running = new FileStream(download.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                UpdateDownload.CleanupAbandoned(root);
                Assert.True(File.Exists(download.Path));
            }
            UpdateDownload.CleanupAbandoned(root);
            Assert.False(Directory.Exists(Path.GetDirectoryName(download.Path)));
        }
    }

    [Fact]
    public void CleanupCannotClaimDirectoryBeforeCreatorOpensLease()
    {
        var root = TestData.NewDirectory();
        var directory = Path.Combine(root, "Updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        // Reproduce the exact creation gap, without scheduling-dependent stress loops.
        UpdateDownload.CleanupAbandoned(root);
        Assert.True(Directory.Exists(directory));
        var leasePath = Path.Combine(directory, "download.lock");
        Assert.False(File.Exists(leasePath));
        using (var lease = new FileStream(leasePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
        {
            File.WriteAllBytes(Path.Combine(directory, "Pixelery-Setup.exe"), Installer);
            UpdateDownload.CleanupAbandoned(root);
            Assert.Equal(Installer, File.ReadAllBytes(Path.Combine(directory, "Pixelery-Setup.exe")));
        }
        UpdateDownload.CleanupAbandoned(root);
        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public void CleanupRemovesAbandonedDownloadsButPreservesUnknownFiles()
    {
        var root = TestData.NewDirectory();
        var updates = Path.Combine(root, "Updates");
        var abandoned = Path.Combine(updates, Guid.NewGuid().ToString("N"));
        var foreign = Path.Combine(updates, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(abandoned);
        Directory.CreateDirectory(foreign);
        File.WriteAllBytes(Path.Combine(abandoned, "Pixelery-Setup.exe"), Installer);
        File.WriteAllText(Path.Combine(abandoned, "download.lock"), "");
        File.WriteAllText(Path.Combine(foreign, "user-file.txt"), "preserve");
        UpdateDownload.CleanupAbandoned(root);
        Assert.False(Directory.Exists(abandoned));
        Assert.Equal("preserve", File.ReadAllText(Path.Combine(foreign, "user-file.txt")));
    }

    private sealed class CancelProgress(CancellationTokenSource cancellation) : IProgress<double>
    {
        public void Report(double value) => cancellation.Cancel();
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative")]
    [InlineData("../escape")]
    [InlineData("C:relative")]
    [InlineData("invalid\0path")]
    public void CacheRejectsInvalidOrRelativeStateDirectories(string directory) =>
        Assert.Throws<ArgumentException>(() => UpdateDownload.CleanupAbandoned(directory));

    [Fact]
    public async Task CancelledCheckDoesNotReturnRelease()
    {
        var (client, http) = Client(Feed());
        using (http)
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.CheckAsync(new Version(0, 2, 0), cancellation.Token));
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(respond(request));
        }
    }
}
