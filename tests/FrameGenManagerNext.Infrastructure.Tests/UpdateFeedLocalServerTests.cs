using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using FrameGenManagerNext.Infrastructure.Updates;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FrameGenManagerNext.Infrastructure.Tests;

public sealed class UpdateFeedLocalServerTests
{
    [Fact]
    public async Task CheckAndDownloadThroughLoopbackHttpServer()
    {
        var installer = Encoding.UTF8.GetBytes("local installer bytes");
        await using var server = new LocalHttpServer();
        server.Add("/installer.exe", installer);
        server.Add("/update.json", Encoding.UTF8.GetBytes(ManifestJson(server.BaseUrl, installer, "/installer.exe")));

        var staging = Path.Combine(Path.GetTempPath(), $"fgm-update-{Guid.NewGuid():N}");
        var client = new UpdateFeedClient(
            NullLogger<UpdateFeedClient>.Instance,
            manifestUrls: [server.BaseUrl + "update.json"],
            stagingDirectory: staging,
            allowLoopbackHttp: true);

        var manifest = await client.CheckAsync(TestContext.Current.CancellationToken);
        manifest.IsSuccess.ShouldBeTrue(manifest.Error?.ToString());
        manifest.Value.InstallerSize.ShouldBe(installer.Length);

        var download = await client.DownloadInstallerAsync(
            manifest.Value,
            progress: null,
            TestContext.Current.CancellationToken);

        download.IsSuccess.ShouldBeTrue(download.Error?.ToString());
        File.ReadAllBytes(download.Value).ShouldBe(installer);
    }

    [Fact]
    public async Task DownloadFallsBackToSecondLoopbackSource()
    {
        var installer = Encoding.UTF8.GetBytes("good installer bytes");
        var tampered = Encoding.UTF8.GetBytes("tampered installer bytes");
        await using var server = new LocalHttpServer();
        server.Add("/bad.exe", tampered);
        server.Add("/good.exe", installer);
        server.Add(
            "/update.json",
            Encoding.UTF8.GetBytes(ManifestJson(server.BaseUrl, installer, "/bad.exe", "/good.exe")));

        var staging = Path.Combine(Path.GetTempPath(), $"fgm-update-{Guid.NewGuid():N}");
        var client = new UpdateFeedClient(
            NullLogger<UpdateFeedClient>.Instance,
            manifestUrls: [server.BaseUrl + "update.json"],
            stagingDirectory: staging,
            allowLoopbackHttp: true);

        var manifest = await client.CheckAsync(TestContext.Current.CancellationToken);
        var download = await client.DownloadInstallerAsync(
            manifest.Value,
            progress: null,
            TestContext.Current.CancellationToken);

        download.IsSuccess.ShouldBeTrue(download.Error?.ToString());
        File.ReadAllBytes(download.Value).ShouldBe(installer);
    }

    [Fact]
    public async Task ProductionFeedRejectsLoopbackHttpInstaller()
    {
        var installer = Encoding.UTF8.GetBytes("local installer bytes");
        await using var server = new LocalHttpServer();
        server.Add("/installer.exe", installer);
        server.Add("/update.json", Encoding.UTF8.GetBytes(ManifestJson(server.BaseUrl, installer, "/installer.exe")));

        var client = new UpdateFeedClient(
            NullLogger<UpdateFeedClient>.Instance,
            manifestUrls: [server.BaseUrl + "update.json"],
            stagingDirectory: Path.Combine(Path.GetTempPath(), $"fgm-update-{Guid.NewGuid():N}"));

        var manifest = await client.CheckAsync(TestContext.Current.CancellationToken);

        manifest.IsFailure.ShouldBeTrue();
    }

    private static string ManifestJson(string baseUrl, byte[] bytes, params string[] paths)
    {
        var urls = string.Join(", ", paths.Select(path => $"\"{baseUrl.TrimEnd('/')}{path}\""));
        return $$"""
            {
              "version": "9.9.9",
              "installer": {
                "name": "FrameGenManagerNext-9.9.9-win-Setup.exe",
                "size": {{bytes.Length}},
                "sha256": "{{Sha256(bytes)}}",
                "urls": [{{urls}}]
              }
            }
            """;
    }

    private static string Sha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private sealed class LocalHttpServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cancellation = new();
        private readonly Dictionary<string, byte[]> _responses = new(StringComparer.OrdinalIgnoreCase);
        private readonly Task _acceptLoop;

        public LocalHttpServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            var endpoint = (IPEndPoint)_listener.LocalEndpoint;
            BaseUrl = $"http://127.0.0.1:{endpoint.Port}/";
            _acceptLoop = AcceptLoopAsync();
        }

        public string BaseUrl { get; }

        public void Add(string path, byte[] body)
        {
            _responses[path] = body;
        }

        public async ValueTask DisposeAsync()
        {
            _cancellation.Cancel();
            _listener.Stop();
            try
            {
                await _acceptLoop;
            }
            catch (Exception exception) when (exception is OperationCanceledException or SocketException)
            {
                // The listener is being torn down.
            }

            _cancellation.Dispose();
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cancellation.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_cancellation.Token);
                }
                catch (Exception exception) when (
                    exception is OperationCanceledException or SocketException or ObjectDisposedException)
                {
                    return;
                }

                _ = HandleAsync(client);
            }
        }

        private async Task HandleAsync(TcpClient client)
        {
            using (client)
            {
                try
                {
                    var stream = client.GetStream();
                    var header = new StringBuilder();
                    var buffer = new byte[1024];
                    while (!header.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                    {
                        var read = await stream.ReadAsync(buffer, _cancellation.Token);
                        if (read == 0)
                        {
                            return;
                        }

                        header.Append(Encoding.ASCII.GetString(buffer, 0, read));
                        if (header.Length > 16 * 1024)
                        {
                            return;
                        }
                    }

                    var requestLine = header.ToString().Split("\r\n", 2, StringSplitOptions.None)[0];
                    var parts = requestLine.Split(' ');
                    var path = parts.Length > 1 ? parts[1].Split('?')[0] : "/";
                    if (!_responses.TryGetValue(path, out var body))
                    {
                        await WriteAsync(stream, "404 Not Found", [], _cancellation.Token);
                        return;
                    }

                    await WriteAsync(stream, "200 OK", body, _cancellation.Token);
                }
                catch (Exception exception) when (
                    exception is IOException or OperationCanceledException or ObjectDisposedException)
                {
                    // The client disconnected or the test server is stopping.
                }
            }
        }

        private static async Task WriteAsync(
            NetworkStream stream,
            string status,
            byte[] body,
            CancellationToken cancellationToken)
        {
            var header = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {status}\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header, cancellationToken);
            if (body.Length > 0)
            {
                await stream.WriteAsync(body, cancellationToken);
            }
        }
    }
}
