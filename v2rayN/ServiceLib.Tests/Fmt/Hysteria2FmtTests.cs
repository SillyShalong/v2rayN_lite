using AwesomeAssertions;
using Xunit;

namespace ServiceLib.Tests.Fmt;

public class Hysteria2FmtTests
{
    // "The hostname and optional port of the server. If the port is omitted, it defaults to 443."
    // -- https://v2.hysteria.network/docs/developers/URI-Scheme/
    // A ':' with no digits after it is an omitted port too, per RFC 3986 'port = *DIGIT'.
    [Theory]
    [InlineData("hysteria2://password@hy2.example/")]
    [InlineData("hysteria2://password@hy2.example")]
    [InlineData("hysteria2://password@hy2.example:/")]
    [InlineData("hy2://password@hy2.example/?sni=real.example")]
    public void ResolveConfig_WithoutPort_ShouldDefaultTo443(string shareUri)
    {
        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        resolved.Should().NotBeNull(because: $"uri: {shareUri}, msg: {msg}");
        resolved!.ConfigType.Should().Be(EConfigType.Hysteria2);
        resolved.Address.Should().Be("hy2.example");
        resolved.Port.Should().Be(443);
    }

    [Fact]
    public void ResolveConfig_WithoutPort_ShouldProduceAValidProfile()
    {
        // Uri.Port is -1 for an unregistered scheme with no port, and ProfileItem.IsValid rejects
        // any port outside 1..65535 - so the default is what keeps such a link usable at all.
        var resolved = FmtHandler.ResolveConfig("hysteria2://password@hy2.example/", out _);

        resolved.Should().NotBeNull();
        resolved!.IsValid().Should().BeTrue();
    }

    [Fact]
    public void ResolveConfig_WithExplicitPort_ShouldKeepIt()
    {
        var resolved = FmtHandler.ResolveConfig("hysteria2://password@hy2.example:8443/", out _);

        resolved.Should().NotBeNull();
        resolved!.Port.Should().Be(8443);
    }

    [Fact]
    public void ResolveConfig_WithExplicitZeroPort_ShouldNotApplyTheDefault()
    {
        // Uri.Port is 0 here, not -1: the port is present, it is just not a usable one. Treating
        // it as "omitted" would silently move the endpoint to :443, so it stays invalid instead.
        var resolved = FmtHandler.ResolveConfig("hysteria2://password@hy2.example:0/", out _);

        resolved.Should().NotBeNull();
        resolved!.Port.Should().Be(0);
        resolved.IsValid().Should().BeFalse();
    }
}
