using AwesomeAssertions;
using Xunit;

namespace ServiceLib.Tests.Fmt;

public class SocksFmtTests
{
    // The legacy form base64-encodes the whole "user:pass@host:port".
    [Theory]
    [InlineData("socks://YWRtaW46YWRtaW5AMS4yLjMuNDoxMDgw#demo", "admin", "admin", "1.2.3.4", 1080)]
    [InlineData("socks://dXNlcjpwYXNzQDEuMi4zLjQ6MTA4MA==#demo", "user", "pass", "1.2.3.4", 1080)]
    [InlineData("socks://dXNlcjpxd2VydHk/QDEuMi4zLjQ6MTA4MA==#demo", "user", "qwerty?", "1.2.3.4", 1080)]
    [InlineData("socks://dXNlcjpxd2VydHk_QDEuMi4zLjQ6MTA4MA#demo", "user", "qwerty?", "1.2.3.4", 1080)]
    public void ResolveConfig_LegacyBase64_ShouldDecodeServerAndCredentials(
        string shareUri, string username, string password, string address, int port)
    {
        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        resolved.Should().NotBeNull(because: $"uri: {shareUri}, msg: {msg}");
        resolved!.ConfigType.Should().Be(EConfigType.SOCKS);
        resolved.Address.Should().Be(address);
        resolved.Port.Should().Be(port);
        resolved.Username.Should().Be(username);
        resolved.Password.Should().Be(password);
        resolved.Remarks.Should().Be("demo");
    }

    [Theory]
    [InlineData("socks://YWRtaW46YWRtaW4@1.2.3.4:1080#demo", "admin", "admin")]
    [InlineData("socks://admin:admin@1.2.3.4:1080#demo", "admin", "admin")]
    [InlineData("socks://1.2.3.4:1080#demo", "", "")]
    public void ResolveConfig_HostAndPort_ShouldKeepParsingIt(string shareUri, string username, string password)
    {
        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        resolved.Should().NotBeNull(because: $"uri: {shareUri}, msg: {msg}");
        resolved!.Address.Should().Be("1.2.3.4");
        resolved.Port.Should().Be(1080);
        resolved.Username.Should().Be(username);
        resolved.Password.Should().Be(password);
        resolved.Remarks.Should().Be("demo");
    }
}
