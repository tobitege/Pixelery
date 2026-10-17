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
            var path = await client.DownloadAsync(release, root, null, default);
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
            await Assert.ThrowsAsync<InvalidDataException>(() => client.DownloadAsync(release, TestData.NewDirectory(), null, default));
        }
    }

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
