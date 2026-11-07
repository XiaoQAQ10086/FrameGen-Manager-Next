using System.Net;
using System.Security.Cryptography;
using System.Text;
using FrameGenManagerNext.Infrastructure.Updates;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class UpdateFeedClientTests
{
    private const string ManifestUrl = "https://github.com/test/update.json";
    private const string InstallerUrl = "https://github.com/test/FrameGenManagerNext-9.9.9-win-Setup.exe";

    [Fact]
    public async Task CheckParsesManifestAndInstallerMetadata()
    {
        var bytes = Encoding.UTF8.GetBytes("installer");
        using var handler = new StubHandler(request =>
            request.RequestUri!.AbsoluteUri == ManifestUrl
                ? Json(ManifestJson(bytes))
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        var result = await client.CheckAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        result.Value.Version.ShouldBe("9.9.9");
        result.Value.InstallerName.ShouldBe("FrameGenManagerNext-9.9.9-win-Setup.exe");
        result.Value.InstallerSize.ShouldBe(bytes.Length);
        result.Value.InstallerSha256.ShouldBe(Sha256(bytes));
    }

    [Fact]
    public async Task DownloadVerifiesHashAndWritesInstaller()
    {
        var bytes = Encoding.UTF8.GetBytes("verified installer bytes");
        using var handler = new StubHandler(request =>
            request.RequestUri!.AbsoluteUri == ManifestUrl
                ? Json(ManifestJson(bytes))
                : Bytes(bytes));
        var client = CreateClient(handler);
        var manifest = (await client.CheckAsync(TestContext.Current.CancellationToken)).Value;

        var result = await client.DownloadInstallerAsync(
            manifest,
            progress: null,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        File.ReadAllBytes(result.Value).ShouldBe(bytes);
        result.Value.ShouldEndWith("FrameGenManagerNext-9.9.9-win-Setup.exe");
    }

    [Fact]
    public async Task DownloadFallsBackWhenFirstMirrorHasWrongHash()
    {
        var bytes = Encoding.UTF8.GetBytes("verified installer bytes");
        var bad = Encoding.UTF8.GetBytes("tampered installer bytes");
        using var handler = new StubHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri == ManifestUrl)
            {
                return Json(ManifestJson(bytes));
            }

            return request.RequestUri.Host.Equals("ghfast.top", StringComparison.OrdinalIgnoreCase)
                ? Bytes(bad)
                : Bytes(bytes);
        });
        var client = CreateClient(handler);
        var manifest = (await client.CheckAsync(TestContext.Current.CancellationToken)).Value;

        var result = await client.DownloadInstallerAsync(
            manifest,
            progress: null,
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue(result.Error?.ToString());
        File.ReadAllBytes(result.Value).ShouldBe(bytes);
    }

    [Fact]
    public async Task DownloadFailsWhenEverySourceFailsVerification()
    {
        var expected = Encoding.UTF8.GetBytes("expected");
        var actual = Encoding.UTF8.GetBytes("tampered");
        using var handler = new StubHandler(request =>
            request.RequestUri!.AbsoluteUri == ManifestUrl
                ? Json(ManifestJson(expected))
                : Bytes(actual));
        var client = CreateClient(handler);
        var manifest = (await client.CheckAsync(TestContext.Current.CancellationToken)).Value;

        var result = await client.DownloadInstallerAsync(
            manifest,
            progress: null,
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Code.ShouldBe(FrameGenManagerNext.Domain.Errors.ErrorCode.DownloadFailed);
    }

    [Fact]
    public async Task CheckRejectsNonHttpsInstallerUrl()
    {
        var bytes = Encoding.UTF8.GetBytes("installer");
        var json = ManifestJson(bytes).Replace(
            InstallerUrl,
            "http://github.com/test/insecure.exe",
            StringComparison.Ordinal);
        using var handler = new StubHandler(request => Json(json));
        var client = CreateClient(handler);

        var result = await client.CheckAsync(TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
    }

    private static UpdateFeedClient CreateClient(HttpMessageHandler handler)
    {
        var staging = Path.Combine(Path.GetTempPath(), $"fgm-update-{Guid.NewGuid():N}");
        return new UpdateFeedClient(
            NullLogger<UpdateFeedClient>.Instance,
            new HttpClient(handler),
            [ManifestUrl],
            staging);
    }

    private static string ManifestJson(byte[] installer)
    {
        return $$"""
            {
              "version": "9.9.9",
              "notes": "test notes",
              "pageUrl": "https://github.com/test/releases",
              "installer": {
                "name": "FrameGenManagerNext-9.9.9-win-Setup.exe",
                "size": {{installer.Length}},
                "sha256": "{{Sha256(installer)}}",
                "urls": ["{{InstallerUrl}}"]
              }
            }
            """;
    }

    private static string Sha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static HttpResponseMessage Json(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private static HttpResponseMessage Bytes(byte[] bytes)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes),
        };
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(respond(request));
        }
    }
}
