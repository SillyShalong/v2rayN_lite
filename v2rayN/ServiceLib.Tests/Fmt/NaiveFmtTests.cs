using AwesomeAssertions;
using Xunit;

namespace ServiceLib.Tests.Fmt;

public class NaiveFmtTests
{
    // "port can be omitted if it's 443" -- https://gist.github.com/DuckSoft/ca03913b0a26fc77a1da4d01cc6ab2f1
    // The first two cases are examples given there; none of its examples carries a port.
    [Theory]
    [InlineData("naive+https://what:happened@test.someone.cf?padding=false#Naive!", false)]
    [InlineData("naive+quic://manhole:114514@quic.test.me", true)]
    [InlineData("naive+https://what:happened@test.someone.cf/", false)]
    [InlineData("naive+https://what:happened@test.someone.cf:/", false)]
    public void ResolveConfig_WithoutPort_ShouldDefaultTo443(string shareUri, bool quic)
    {
        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        resolved.Should().NotBeNull(because: $"uri: {shareUri}, msg: {msg}");
        resolved!.ConfigType.Should().Be(EConfigType.Naive);
        resolved.Port.Should().Be(443);
        (resolved.GetProtocolExtra().NaiveQuic == true).Should().Be(quic);
        resolved.IsValid().Should().BeTrue();
    }

    [Fact]
    public void ResolveConfig_WithExplicitPort_ShouldKeepIt()
    {
        var resolved = FmtHandler.ResolveConfig("naive+https://what:happened@test.someone.cf:8443", out _);

        resolved.Should().NotBeNull();
        resolved!.Address.Should().Be("test.someone.cf");
        resolved.Port.Should().Be(8443);
        resolved.Username.Should().Be("what");
        resolved.Password.Should().Be("happened");
    }

    [Fact]
    public void ResolveConfig_WithExplicitZeroPort_ShouldNotApplyTheDefault()
    {
        var resolved = FmtHandler.ResolveConfig("naive+https://what:happened@test.someone.cf:0", out _);

        resolved.Should().NotBeNull();
        resolved!.Port.Should().Be(0);
        resolved.IsValid().Should().BeFalse();
    }
}
