namespace ServiceLib.Common;

public static class ExternalConnectionGuard
{
    public const string BlockedMessage = "Lite build disables non-core external communication.";
    public const string XrayCoreUpdateOnlyMessage = "Lite build only allows one-click Xray core update.";

    public static bool IsUrlAllowed(string? url)
    {
        if (!TryCreateAbsoluteUri(url, out var uri))
        {
            return false;
        }

        return IsHostAllowed(uri.Host)
               || IsConfiguredSpeedTestUrl(uri)
               || IsXrayUpdateUrl(uri);
    }

    public static bool IsSubscriptionUrlAllowed(string? url)
    {
        if (!TryCreateAbsoluteUri(url, out var uri))
        {
            return false;
        }

        return IsConfiguredSubscriptionUrl(uri)
               || IsConfiguredSubscriptionConvertUrl(uri);
    }

    public static bool IsGeoUpdateUrlAllowed(string? url)
    {
        if (!TryCreateAbsoluteUri(url, out var uri))
        {
            return false;
        }

        return IsGeoTemplateUrl(uri)
               || IsConfiguredGeoTemplateUrl(uri)
               || IsOtherGeoUrl(uri)
               || IsSrsTemplateUrl(uri)
               || IsConfiguredSrsTemplateUrl(uri);
    }

    public static IReadOnlyList<string> GetAllowedSpeedTestUrls()
    {
        return GetOrderedUrlList(
            AppManager.Instance.Config?.SpeedTestItem?.SpeedTestUrl,
            Global.SpeedTestUrls);
    }

    public static IReadOnlyList<string> GetAllowedSpeedPingTestUrls()
    {
        return GetOrderedUrlList(
            AppManager.Instance.Config?.SpeedTestItem?.SpeedPingTestUrl,
            Global.SpeedPingTestUrls);
    }

    public static bool IsSpeedTestUrlAllowed(string? url)
    {
        if (!TryCreateAbsoluteUri(url, out var uri))
        {
            return false;
        }

        try
        {
            return GetAllowedSpeedTestUrls().Any(candidate => IsSameAbsoluteUrl(uri, candidate))
                   || GetAllowedSpeedPingTestUrls().Any(candidate => IsSameAbsoluteUrl(uri, candidate));
        }
        catch
        {
            return false;
        }
    }

    public static bool IsHostAllowed(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        host = host.Trim().Trim('[', ']');

        if (IPAddress.TryParse(host, out var ipAddress))
        {
            return IPAddress.IsLoopback(ipAddress);
        }

        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
               || host.Equals(Global.Loopback, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConfiguredSpeedTestUrl(Uri uri)
    {
        return IsSpeedTestUrlAllowed(uri.AbsoluteUri);
    }

    private static bool TryCreateAbsoluteUri(string? url, out Uri uri)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out uri!);
    }

    private static bool IsSameAbsoluteUrl(Uri uri, string? configuredUrl)
    {
        if (string.IsNullOrWhiteSpace(configuredUrl))
        {
            return false;
        }

        if (!Uri.TryCreate(configuredUrl, UriKind.Absolute, out var configuredUri))
        {
            return false;
        }

        return Uri.Compare(uri, configuredUri, UriComponents.AbsoluteUri, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;
    }

    private static IReadOnlyList<string> GetOrderedUrlList(string? configuredUrl, IEnumerable<string> fallbackUrls)
    {
        List<string> results = [];
        AddDistinctAbsoluteUrl(results, configuredUrl);
        foreach (var fallbackUrl in fallbackUrls)
        {
            AddDistinctAbsoluteUrl(results, fallbackUrl);
        }

        return results;
    }

    private static void AddDistinctAbsoluteUrl(List<string> results, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return;
        }

        var absoluteUrl = uri.AbsoluteUri;
        if (results.Any(existing => string.Equals(existing, absoluteUrl, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        results.Add(absoluteUrl);
    }

    private static bool IsConfiguredSubscriptionUrl(Uri uri)
    {
        try
        {
            var subItems = SQLiteHelper.Instance.TableAsync<SubItem>().ToListAsync().GetAwaiter().GetResult();
            foreach (var item in subItems)
            {
                if (item.Enabled == false)
                {
                    continue;
                }

                if (IsSameAbsoluteUrl(uri, Utils.GetPunycode(item.Url.TrimEx())))
                {
                    return true;
                }

                var extraUrls = item.MoreUrl.TrimEx().Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var extraUrl in extraUrls)
                {
                    if (IsSameAbsoluteUrl(uri, Utils.GetPunycode(extraUrl)))
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool IsConfiguredSubscriptionConvertUrl(Uri uri)
    {
        try
        {
            var subItems = SQLiteHelper.Instance.TableAsync<SubItem>().ToListAsync().GetAwaiter().GetResult();
            if (!subItems.Any(t => t.Enabled && t.ConvertTarget.IsNotEmpty()))
            {
                return false;
            }

            var template = AppManager.Instance.Config?.ConstItem.SubConvertUrl;
            if (template.IsNullOrEmpty())
            {
                template = Global.SubConvertUrls.FirstOrDefault();
            }

            return IsUrlFromTemplate(uri, template, true);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsGeoTemplateUrl(Uri uri)
    {
        return IsUrlFromTemplate(uri, Global.GeoUrl);
    }

    private static bool IsConfiguredGeoTemplateUrl(Uri uri)
    {
        try
        {
            return IsUrlFromTemplate(uri, AppManager.Instance.Config?.ConstItem.GeoSourceUrl);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsOtherGeoUrl(Uri uri)
    {
        return Global.OtherGeoUrls.Any(url => IsSameAbsoluteUrl(uri, url));
    }

    private static bool IsSrsTemplateUrl(Uri uri)
    {
        return IsUrlFromTemplate(uri, Global.SingboxRulesetUrl);
    }

    private static bool IsConfiguredSrsTemplateUrl(Uri uri)
    {
        try
        {
            return IsUrlFromTemplate(uri, AppManager.Instance.Config?.ConstItem.SrsSourceUrl);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsUrlFromTemplate(Uri uri, string? template, bool allowExtraQuery = false)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return false;
        }

        var pattern = Regex.Escape(template);
        pattern = Regex.Replace(pattern, @"\\\{\d+\\\}", "[^?#]+");
        if (allowExtraQuery)
        {
            pattern += "(?:&.*)?";
        }

        return Regex.IsMatch(uri.AbsoluteUri, $"^{pattern}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool IsXrayUpdateUrl(Uri uri)
    {
        try
        {
            var coreInfo = CoreInfoManager.Instance.GetCoreInfo(ECoreType.Xray);
            if (coreInfo == null)
            {
                return false;
            }

            return IsSameAbsoluteUrl(uri, coreInfo.ReleaseApiUrl)
                   || IsGithubLatestReleaseUrl(uri, coreInfo.Url)
                   || IsGithubReleaseDownloadUrl(uri, coreInfo.Url);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsGithubLatestReleaseUrl(Uri uri, string? releasesUrl)
    {
        if (string.IsNullOrWhiteSpace(releasesUrl) || !Uri.TryCreate(releasesUrl, UriKind.Absolute, out var releasesUri))
        {
            return false;
        }

        var latestUri = new Uri(releasesUri.AbsoluteUri.TrimEnd('/') + "/latest");
        return Uri.Compare(uri, latestUri, UriComponents.AbsoluteUri, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0;
    }

    private static bool IsGithubReleaseDownloadUrl(Uri uri, string? releasesUrl)
    {
        if (string.IsNullOrWhiteSpace(releasesUrl) || !Uri.TryCreate(releasesUrl, UriKind.Absolute, out var releasesUri))
        {
            return false;
        }

        if (!uri.Host.Equals(releasesUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var releasePath = releasesUri.AbsolutePath.TrimEnd('/');
        return uri.AbsolutePath.StartsWith(releasePath + "/download/", StringComparison.OrdinalIgnoreCase);
    }
}