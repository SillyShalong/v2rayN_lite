using System.IO.Compression;
using System.Net.Sockets;
using AwesomeAssertions;
using ServiceLib.Common;
using ServiceLib.Services;
using Xunit;

namespace ServiceLib.Tests.Services;

public class DownloadServiceTests
{
    [Fact]
    public async Task TryDownloadString_SendsAcceptAndDecompressesGzipSubscriptionResponse()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var endpoint = (IPEndPoint)listener.LocalEndpoint;
        var cancellationToken = TestContext.Current.CancellationToken;
        var requestHeadersTask = Task.Run(async () =>
        {
            using var connection = await listener.AcceptTcpClientAsync(cancellationToken).AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
            await using var stream = connection.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var headers = new List<string>();
            string? line;
            while ((line = await reader.ReadLineAsync()) is { Length: > 0 })
            {
                headers.Add(line);
            }

            var content = Encoding.UTF8.GetBytes("subscription payload");
            await using var compressed = new MemoryStream();
            await using (var gzip = new GZipStream(compressed, CompressionMode.Compress, leaveOpen: true))
            {
                await gzip.WriteAsync(content);
            }

            var responseHeader = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Encoding: gzip\r\nContent-Length: {compressed.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(responseHeader);
            await stream.WriteAsync(compressed.ToArray());
            await stream.FlushAsync();
            return headers;
        });

        var service = new DownloadService
        {
            ExternalUrlValidator = _ => true,
            AcceptHeader = "*/*"
        };
        var url = $"http://127.0.0.1:{endpoint.Port}/subscription";

        var result = await service.TryDownloadString(url, (IWebProxy?)null, "ServiceLib.Tests/1.0");
        var requestHeaders = await requestHeadersTask.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        result.Should().Be("subscription payload");
        requestHeaders.Should().Contain(header => header.Equals("Accept: */*", StringComparison.OrdinalIgnoreCase));
        requestHeaders.Should().Contain(header => header.StartsWith("Accept-Encoding:", StringComparison.OrdinalIgnoreCase)
                                                  && header.Contains("gzip", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task TryDownloadString_DoesNotRequestUrlRejectedBySubscriptionValidator()
    {
        var service = new DownloadService { ExternalUrlValidator = _ => false };
        var errors = new List<string>();
        service.Error += (_, args) => errors.Add(args.GetException().Message);

        var result = await service.TryDownloadString("http://203.0.113.7:18080/subscription", (IWebProxy?)null,
            "ServiceLib.Tests/1.0");

        result.Should().BeNull();
        errors.Should().ContainSingle().Which.Should().Contain(ExternalConnectionGuard.BlockedMessage);
    }
}
