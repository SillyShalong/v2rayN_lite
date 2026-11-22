using AwesomeAssertions;
using Xunit;

namespace ServiceLib.Tests.Fmt;

public class AnytlsFmtTests
{
    // "服务器的地址和可选端口。如果省略端口，则默认为 443。" (the port is optional and defaults to 443)
    // -- https://github.com/anytls/anytls-go/blob/main/docs/uri_scheme.md
    // The first two cases are the examples given there.
    [Theory]
    [InlineData("anytls://letmein@example.com/?sni=real.example.com")]
    [InlineData("anytls://letmein@example.com/?sni=127.0.0.1&insecure=1")]
    [InlineData("anytls://letmein@example.com")]
    [InlineData("anytls://letmein@example.com:/")]
    public void ResolveConfig_WithoutPort_ShouldDefaultTo443(string shareUri)
    {
        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        resolved.Should().NotBeNull(because: $"uri: {shareUri}, msg: {msg}");
        resolved!.ConfigType.Should().Be(EConfigType.Anytls);
        resolved.Address.Should().Be("example.com");
        resolved.Port.Should().Be(443);
        resolved.Password.Should().Be("letmein");
        resolved.IsValid().Should().BeTrue();
    }

    [Fact]
    public void ResolveConfig_WithExplicitPort_ShouldKeepIt()
    {
        // The third example from the URI scheme document.
        const string shareUri = "anytls://0fdf77d7-d4ba-455e-9ed9-a98dd6d5489a@[2409:8a71:6a00:1953::615]:8964/?insecure=1";

        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        resolved.Should().NotBeNull(because: $"uri: {shareUri}, msg: {msg}");
        resolved!.Address.Should().Be("2409:8a71:6a00:1953::615");
        resolved.Port.Should().Be(8964);
    }

    [Fact]
    public void ResolveConfig_WithExplicitZeroPort_ShouldNotApplyTheDefault()
    {
        var resolved = FmtHandler.ResolveConfig("anytls://letmein@example.com:0/", out _);

        resolved.Should().NotBeNull();
        resolved!.Port.Should().Be(0);
        resolved.IsValid().Should().BeFalse();
    }
}
