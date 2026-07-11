using Downloader;

namespace ServiceLib.Helper;

public class DownloaderHelper
{
    private static readonly Lazy<DownloaderHelper> _instance = new(() => new());
    public static DownloaderHelper Instance => _instance.Value;

    public async Task<string?> DownloadStringAsync(IWebProxy? webProxy, string url, string? userAgent, int timeout)
    {
        if (url.IsNullOrEmpty())
        {
            return null;
        }

        Uri uri = new(url);
        //Authorization Header
        var headers = new WebHeaderCollection();
        if (uri.UserInfo.IsNotEmpty())
        {
            headers.Add(HttpRequestHeader.Authorization, "Basic " + Utils.Base64Encode(uri.UserInfo));
        }

        var downloadOpt = new DownloadConfiguration()
        {
            BlockTimeout = timeout * 1000,
            MaxTryAgainOnFailure = 2,
            RequestConfiguration =
                {
                    Headers = headers,
                    UserAgent = userAgent,
                    ConnectTimeout = timeout * 1000,
                    Proxy = webProxy
                }
        };

        await using var downloader = new Downloader.DownloadService(downloadOpt);
        downloader.DownloadFileCompleted += (sender, value) =>
        {
            if (value.Error != null)
            {
                throw value.Error;
            }
        };

        using var cts = new CancellationTokenSource();
        await using var stream = await downloader.DownloadFileTaskAsync(address: url, cts.Token).WaitAsync(TimeSpan.FromSeconds(timeout), cts.Token);
        using StreamReader reader = new(stream);

        downloadOpt = null;

        return await reader.ReadToEndAsync(cts.Token);
    }

    public async Task<double> DownloadDataAsync4Speed(IWebProxy? webProxy, string url, IProgress<string>? progress, int timeout)
    {
        if (url.IsNullOrEmpty())
        {
            throw new ArgumentNullException(nameof(url));
        }

        var timeoutSeconds = Math.Max(1, timeout);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            ConnectTimeout = TimeSpan.FromSeconds(timeoutSeconds),
            Proxy = webProxy,
            UseProxy = webProxy != null
        };
        using var client = new HttpClient(handler)
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);

        var buffer = new byte[128 * 1024];
        long totalBytes = 0;
        var timer = Stopwatch.StartNew();
        var lastReportSeconds = 0d;
        try
        {
            while (true)
            {
                var read = await stream.ReadAsync(buffer, cts.Token).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                totalBytes += read;
                var elapsedSeconds = timer.Elapsed.TotalSeconds;
                if (elapsedSeconds - lastReportSeconds >= 1)
                {
                    lastReportSeconds = elapsedSeconds;
                    var currentSpeed = totalBytes / 1_000_000d / Math.Max(elapsedSeconds, 0.001d);
                    progress?.Report(currentSpeed.ToString("0.0"));
                }
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && totalBytes > 0)
        {
            // Reaching the configured duration is a successful bounded speed test.
        }
        finally
        {
            timer.Stop();
        }

        if (totalBytes < 64 * 1024)
        {
            return 0;
        }

        return totalBytes / 1_000_000d / Math.Max(timer.Elapsed.TotalSeconds, 0.001d);
    }
    public async Task DownloadFileAsync(IWebProxy? webProxy, string url, string fileName, IProgress<double> progress, int timeout)
    {
        if (url.IsNullOrEmpty())
        {
            throw new ArgumentNullException(nameof(url));
        }
        if (fileName.IsNullOrEmpty())
        {
            throw new ArgumentNullException(nameof(fileName));
        }
        if (File.Exists(fileName))
        {
            File.Delete(fileName);
        }

        var downloadOpt = new DownloadConfiguration()
        {
            BlockTimeout = timeout * 1000,
            MaxTryAgainOnFailure = 2,
            RequestConfiguration =
                {
                    ConnectTimeout= timeout * 1000,
                    Proxy = webProxy
                }
        };

        var progressPercentage = 0;
        var hasValue = false;
        await using var downloader = new Downloader.DownloadService(downloadOpt);
        downloader.DownloadStarted += (sender, value) => progress?.Report(0);
        downloader.DownloadProgressChanged += (sender, value) =>
        {
            hasValue = true;
            var percent = (int)value.ProgressPercentage;//   Convert.ToInt32((totalRead * 1d) / (total * 1d) * 100);
            if (progressPercentage != percent && percent % 10 == 0)
            {
                progressPercentage = percent;
                progress.Report(percent);
            }
        };
        downloader.DownloadFileCompleted += (sender, value) =>
        {
            if (progress != null)
            {
                if (hasValue && value.Error == null)
                {
                    progress.Report(101);
                }
                else if (value.Error != null)
                {
                    throw value.Error;
                }
            }
        };

        using var cts = new CancellationTokenSource();
        await downloader.DownloadFileTaskAsync(url, fileName, cts.Token);

        downloadOpt = null;
    }
}
