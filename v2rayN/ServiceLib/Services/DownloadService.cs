using System.Net.Http.Headers;

namespace ServiceLib.Services;

/// <summary>
/// Download
/// </summary>
public class DownloadService
{
    public Func<string, bool>? ExternalUrlValidator { get; set; }
    public event EventHandler<UpdateResult>? UpdateCompleted;

    public event ErrorEventHandler? Error;

    private static readonly string _tag = "DownloadService";

    /// <summary>
    /// Downloads data with the specified proxy and reports progress messages.
    /// </summary>
    public async Task<bool> DownloadDataAsync(string url, IWebProxy webProxy, int downloadTimeout, Func<bool, string, Task> updateFunc)
    {
        if (!await EnsureExternalUrlAllowed(url, updateFunc))
        {
            return false;
        }

        var hasPositiveSpeed = false;
        decimal maxMeasuredSpeed = 0;
        try
        {
            var progress = new InlineProgress<string>(value =>
            {
                if (decimal.TryParse(value, out var speed) && speed > 0)
                {
                    hasPositiveSpeed = true;
                    maxMeasuredSpeed = Math.Max(maxMeasuredSpeed, speed);
                }

                _ = updateFunc?.Invoke(false, $"{value}");
            });

            await DownloaderHelper.Instance.DownloadDataAsync4Speed(webProxy,
                  url,
                  progress,
                  downloadTimeout);
        }
        catch (Exception ex)
        {
            if (!hasPositiveSpeed)
            {
                await updateFunc?.Invoke(false, ex.Message);
                if (ex.InnerException != null)
                {
                    await updateFunc?.Invoke(false, ex.InnerException.Message);
                }
            }
        }

        if (hasPositiveSpeed)
        {
            await updateFunc?.Invoke(true, maxMeasuredSpeed.ToString());
        }
        return hasPositiveSpeed;
    }

    private sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }

    /// <summary>
    /// Downloads a file and reports progress through events.
    /// </summary>
    public async Task DownloadFileAsync(string url, string fileName, bool blProxy, int downloadTimeout)
    {
        if (!await EnsureExternalUrlAllowed(url))
        {
            return;
        }

        try
        {
            UpdateCompleted?.Invoke(this, new UpdateResult(false, $"{ResUI.Downloading}   {url}"));

            var progress = new Progress<double>();
            progress.ProgressChanged += (sender, value) => UpdateCompleted?.Invoke(this, new UpdateResult(value > 100, $"...{value}%"));

            var webProxy = await GetWebProxy(blProxy);
            await DownloaderHelper.Instance.DownloadFileAsync(webProxy,
                url,
                fileName,
                progress,
                downloadTimeout);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);

            Error?.Invoke(this, new ErrorEventArgs(ex));
            if (ex.InnerException != null)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
            }
        }
    }

    /// <summary>
    /// Gets redirect target URL without following redirects automatically.
    /// </summary>
    public async Task<string?> UrlRedirectAsync(string url, bool blProxy)
    {
        if (!await EnsureExternalUrlAllowed(url))
        {
            return null;
        }

        var webRequestHandler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            Proxy = await GetWebProxy(blProxy)
        };
        var client = new HttpClient(webRequestHandler);

        var response = await client.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location is not null)
        {
            return response.Headers.Location.ToString();
        }
        else
        {
            Error?.Invoke(this, new ErrorEventArgs(new Exception("StatusCode error: " + response.StatusCode)));
            Logging.SaveLog("StatusCode error: " + url);
            return null;
        }
    }

    /// <summary>
    /// Tries to download string content using proxy switch setting.
    /// </summary>
    public async Task<string?> TryDownloadString(string url, bool blProxy, string userAgent)
    {
        var webProxy = await GetWebProxy(blProxy);
        return await TryDownloadString(url, webProxy, userAgent);
    }

    /// <summary>
    /// Tries to download string content with a specified proxy.
    /// </summary>
    public async Task<string?> TryDownloadString(string url, IWebProxy? webProxy, string userAgent)
    {
        if (!await EnsureExternalUrlAllowed(url))
        {
            return null;
        }

        try
        {
            var result1 = await DownloadStringAsync(url, webProxy, userAgent, 15);
            if (result1.IsNotEmpty())
            {
                return result1;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            Error?.Invoke(this, new ErrorEventArgs(ex));
            if (ex.InnerException != null)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
            }
        }

        try
        {
            var result2 = await DownloadStringViaDownloader(url, webProxy, userAgent, 15);
            if (result2.IsNotEmpty())
            {
                return result2;
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            Error?.Invoke(this, new ErrorEventArgs(ex));
            if (ex.InnerException != null)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
            }
        }

        return null;
    }

    /// <summary>
    /// Downloads string content via HttpClient.
    /// </summary>
    private async Task<string?> DownloadStringAsync(string url, IWebProxy? webProxy, string userAgent, int timeout)
    {
        try
        {
            var client = new HttpClient(new SocketsHttpHandler()
            {
                Proxy = webProxy,
                UseProxy = webProxy != null
            });

            if (userAgent.IsNullOrEmpty())
            {
                userAgent = Utils.GetVersion(false);
            }
            client.DefaultRequestHeaders.UserAgent.TryParseAdd(userAgent);

            Uri uri = new(url);
            //Authorization Header
            if (uri.UserInfo.IsNotEmpty())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Utils.Base64Encode(uri.UserInfo));
            }

            using var cts = new CancellationTokenSource();
            var result = await client.GetStringAsync(url, cts.Token).WaitAsync(TimeSpan.FromSeconds(timeout), cts.Token);
            return result;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            Error?.Invoke(this, new ErrorEventArgs(ex));
            if (ex.InnerException != null)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
            }
        }
        return null;
    }

    /// <summary>
    /// Downloads string content via DownloaderHelper.
    /// </summary>
    private async Task<string?> DownloadStringViaDownloader(string url, IWebProxy? webProxy, string userAgent, int timeout)
    {
        try
        {
            if (userAgent.IsNullOrEmpty())
            {
                userAgent = Utils.GetVersion(false);
            }
            var result = await DownloaderHelper.Instance.DownloadStringAsync(webProxy, url, userAgent, timeout);
            return result;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            Error?.Invoke(this, new ErrorEventArgs(ex));
            if (ex.InnerException != null)
            {
                Error?.Invoke(this, new ErrorEventArgs(ex.InnerException));
            }
        }
        return null;
    }

    /// <summary>
    /// Creates local SOCKS proxy when proxy switch is enabled.
    /// </summary>
    private async Task<WebProxy?> GetWebProxy(bool blProxy)
    {
        if (!blProxy)
        {
            return null;
        }
        var port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
        if (await SocketCheck(Global.Loopback, port) == false)
        {
            return null;
        }

        return new WebProxy($"socks5://{Global.Loopback}:{port}");
    }

    /// <summary>
    /// Checks whether the specified TCP endpoint is reachable.
    /// </summary>
    private async Task<bool> SocketCheck(string ip, int port)
    {
        try
        {
            IPEndPoint point = new(IPAddress.Parse(ip), port);
            using Socket? sock = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await sock.ConnectAsync(point);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<bool> EnsureExternalUrlAllowed(string url, Func<bool, string, Task>? updateFunc = null)
    {
        if (ExternalConnectionGuard.IsUrlAllowed(url) || ExternalUrlValidator?.Invoke(url) == true)
        {
            return true;
        }

        var ex = new InvalidOperationException($"{ExternalConnectionGuard.BlockedMessage} {url}");
        Logging.SaveLog(_tag, ex);
        Error?.Invoke(this, new ErrorEventArgs(ex));
        if (updateFunc != null)
        {
            await updateFunc(false, ExternalConnectionGuard.BlockedMessage);
        }

        return false;
    }
}
