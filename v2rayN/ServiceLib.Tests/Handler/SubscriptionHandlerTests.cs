using AwesomeAssertions;
using Xunit;

namespace ServiceLib.Tests.Handler;

public class SubscriptionHandlerTests
{
    [Fact]
    public void BuildUserAgent_ReturnsCustomUserAgentWhenSet()
    {
        var item = new SubItem { UserAgent = "clash.meta", CustomClientVersion = "6.55", CustomCoreVersion = "24.11.30" };

        SubscriptionHandler.BuildUserAgent(item).Should().Be("clash.meta");
    }

    [Fact]
    public void BuildUserAgent_TrimsCustomUserAgent()
    {
        var item = new SubItem { UserAgent = "  sing-box  " };

        SubscriptionHandler.BuildUserAgent(item).Should().Be("sing-box");
    }

    [Fact]
    public void BuildUserAgent_CombinesBothCustomVersions()
    {
        var item = new SubItem { CustomClientVersion = "6.55", CustomCoreVersion = "24.11.30" };

        SubscriptionHandler.BuildUserAgent(item).Should().Be("v2rayN/6.55 (xray/24.11.30)");
    }

    [Fact]
    public void BuildUserAgent_UsesOnlyCustomClientVersion()
    {
        var item = new SubItem { CustomClientVersion = "6.55" };

        SubscriptionHandler.BuildUserAgent(item).Should().Be("v2rayN/6.55");
    }

    [Fact]
    public void BuildUserAgent_FallsBackToRealVersionWhenOnlyCoreVersionSet()
    {
        var item = new SubItem { CustomCoreVersion = "24.11.30" };

        SubscriptionHandler.BuildUserAgent(item).Should().Be($"v2rayN/{Utils.GetVersionInfo()} (xray/24.11.30)");
    }

    [Fact]
    public void BuildUserAgent_TrimsCustomVersions()
    {
        var item = new SubItem { CustomClientVersion = " 6.55 ", CustomCoreVersion = " 24.11.30 " };

        SubscriptionHandler.BuildUserAgent(item).Should().Be("v2rayN/6.55 (xray/24.11.30)");
    }

    [Fact]
    public void BuildUserAgent_ReturnsEmptyWhenNothingSet()
    {
        var item = new SubItem();

        SubscriptionHandler.BuildUserAgent(item).Should().BeEmpty();
    }

    [Fact]
    public void BuildUserAgent_HandlesNullFields()
    {
        var item = new SubItem { UserAgent = null!, CustomClientVersion = null!, CustomCoreVersion = null! };

        SubscriptionHandler.BuildUserAgent(item).Should().BeEmpty();
    }
}
