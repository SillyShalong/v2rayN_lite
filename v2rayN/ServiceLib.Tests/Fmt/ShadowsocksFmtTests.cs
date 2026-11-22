using AwesomeAssertions;
using Xunit;

namespace ServiceLib.Tests.Fmt;

public class ShadowsocksFmtTests
{
    [Theory]
    [InlineData("8388", true)]
    [InlineData("\"8388\"", true)]
    [InlineData("8388", false)]
    [InlineData("\"8388\"", false)]
    public void ResolveSip008_ObjectAndArrayDocuments_ShouldPreserveServerFields(string port, bool useObject)
    {
        var server = $$"""
            {
                "id": "27b8a625-4f4b-4428-9f0f-8a2317db7c79",
                "remarks": "SIP008 server",
                "server": "example.com",
                "server_port": {{port}},
                "password": "example",
                "method": "chacha20-ietf-poly1305"
            }
            """;
        var document = useObject
            ? $$"""{"version": 1, "servers": [{{server}}]}"""
            : $"[{server}]";

        var resolved = ShadowsocksFmt.ResolveSip008(document);

        resolved.Should().NotBeNull();
        resolved!.Should().HaveCount(1);
        resolved[0].Remarks.Should().Be("SIP008 server");
        resolved[0].Address.Should().Be("example.com");
        resolved[0].Port.Should().Be(8388);
        resolved[0].Password.Should().Be("example");
        resolved[0].GetProtocolExtra().SsMethod.Should().Be("chacha20-ietf-poly1305");
    }

    // SIP002 user info that is not base64 is "method:password", percent-encoded once.
    [Theory]
    [InlineData("ss://2022-blake3-aes-128-gcm:p%2541ss@1.2.3.4:8388#demo", "p%41ss")]
    [InlineData("ss://2022-blake3-aes-128-gcm:p%25ss@1.2.3.4:8388#demo", "p%ss")]
    [InlineData("ss://2022-blake3-aes-128-gcm:a%3Ab%2Fc%2B%3D@1.2.3.4:8388#demo", "a:b/c+=")]
    public void ResolveConfig_PlainUserInfo_ShouldDecodePasswordOnce(string shareUri, string password)
    {
        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        resolved.Should().NotBeNull(because: $"uri: {shareUri}, msg: {msg}");
        resolved!.ConfigType.Should().Be(EConfigType.Shadowsocks);
        resolved.GetProtocolExtra().SsMethod.Should().Be("2022-blake3-aes-128-gcm");
        resolved.Password.Should().Be(password);
    }
}
