using AwesomeAssertions;
using Xunit;

namespace ServiceLib.Tests.Fmt;

public class ShareUriQueryTests
{
    [Theory]
    [InlineData("ob%41fs")]
    [InlineData("66%ff")]
    [InlineData("100%")]
    public void GetShareUriAndResolveConfig_QueryValueWithPercent_ShouldSurviveTheRoundTrip(string obfsPassword)
    {
        var source = new ProfileItem
        {
            ConfigType = EConfigType.Hysteria2,
            Remarks = "percent demo",
            Address = "hy2.example",
            Port = 8443,
            Password = "pw",
        };
        source.SetProtocolExtra(new ProtocolExtraItem { SalamanderPass = obfsPassword, });

        var uri = FmtHandler.GetShareUri(source);

        uri.Should().NotBeNull();

        var resolved = FmtHandler.ResolveConfig(uri!, out var msg);

        resolved.Should().NotBeNull(because: $"uri: {uri}, msg: {msg}");
        resolved!.GetProtocolExtra().SalamanderPass.Should().Be(obfsPassword);
    }

    // RFC 3986 lists '=' among the sub-delimiters a query value may carry, so only the first one
    // separates the key from the value. Splitting on every '=' discarded the pair outright.
    [Fact]
    public void ResolveConfig_QueryValueWithUnescapedEquals_ShouldNotBeDropped()
    {
        const string shareUri = "hysteria2://pw@hy2.example:8443/?ech=AAj+DQAEAAAAAA==&sni=real.example";

        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        resolved.Should().NotBeNull(because: $"uri: {shareUri}, msg: {msg}");
        resolved!.EchConfigList.Should().Be("AAj+DQAEAAAAAA==");
        resolved.Sni.Should().Be("real.example");
    }

    // Canonical SIP002 percent-encodes the plugin argument, and that is what this client's own
    // exporter emits. This covers the non-canonical spelling instead: the options are a ';'
    // separated list of 'key=value' pairs, so left unescaped the value always carries '='.
    [Fact]
    public void ResolveConfig_NonCanonicalSip002PluginWithLiteralEquals_ShouldConfigureObfs()
    {
        const string shareUri =
            "ss://YWVzLTEyOC1nY206cGFzczEyMw==@1.2.3.4:8388/?plugin=obfs-local;obfs=http;obfs-host=example.com#ss";

        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        resolved.Should().NotBeNull(because: $"uri: {shareUri}, msg: {msg}");
        resolved!.ConfigType.Should().Be(EConfigType.Shadowsocks);
        resolved.GetTransportExtra().Host.Should().Be("example.com");
        resolved.GetTransportExtra().RawHeaderType.Should().Be(Global.RawHeaderHttp);
    }

    [Fact]
    public void ResolveConfig_EscapedQueryValue_ShouldStillDecodeExactlyOnce()
    {
        const string shareUri = "hysteria2://pw@hy2.example:8443/?ech=AAj%2BDQAEAAAAAA%3D%3D&obfs=salamander&obfs-password=a%20b";

        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        resolved.Should().NotBeNull(because: $"uri: {shareUri}, msg: {msg}");
        resolved!.EchConfigList.Should().Be("AAj+DQAEAAAAAA==");
        resolved.GetProtocolExtra().SalamanderPass.Should().Be("a b");
    }
}
