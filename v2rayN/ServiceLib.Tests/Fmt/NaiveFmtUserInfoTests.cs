using AwesomeAssertions;
using Xunit;

namespace ServiceLib.Tests.Fmt;

public class NaiveFmtUserInfoTests
{
    [Theory]
    [InlineData("pa:ss")]
    [InlineData("a:b:c")]
    [InlineData(":leading")]
    [InlineData("trailing:")]
    public void GetShareUriAndResolveConfig_PasswordOnlyWithColon_ShouldKeepPassword(string password)
    {
        var source = CreateNaiveProfile(string.Empty, password);

        var uri = FmtHandler.GetShareUri(source);
        uri.Should().NotBeNull();

        var resolved = FmtHandler.ResolveConfig(uri!, out var msg);

        resolved.Should().NotBeNull(because: $"uri: {uri}, msg: {msg}");
        resolved!.Username.Should().Be(string.Empty);
        resolved.Password.Should().Be(password);
    }

    [Fact]
    public void GetShareUriAndResolveConfig_UsernameAndPasswordWithColons_ShouldKeepBoth()
    {
        var source = CreateNaiveProfile("us:er", "pa:ss");

        var uri = FmtHandler.GetShareUri(source);
        uri.Should().NotBeNull();

        var resolved = FmtHandler.ResolveConfig(uri!, out var msg);

        resolved.Should().NotBeNull(because: $"uri: {uri}, msg: {msg}");
        resolved!.Username.Should().Be("us:er");
        resolved.Password.Should().Be("pa:ss");
    }

    [Theory]
    [InlineData("naive+https://what:happened@test.someone.cf:443", "what", "happened")]
    [InlineData("naive+https://what:hap:pened@test.someone.cf:443", "what", "hap:pened")]
    [InlineData("naive+https://wh%40t:hap%3Apened@test.someone.cf:443", "wh@t", "hap:pened")]
    [InlineData("naive+https://pa%3Ass@test.someone.cf:443", "", "pa:ss")]
    [InlineData("naive+quic://secret@test.someone.cf:443", "", "secret")]
    public void ResolveConfig_ShouldSplitUserInfoBeforeDecoding(string shareUri, string username, string password)
    {
        var resolved = FmtHandler.ResolveConfig(shareUri, out var msg);

        resolved.Should().NotBeNull(because: $"uri: {shareUri}, msg: {msg}");
        resolved!.Username.Should().Be(username);
        resolved.Password.Should().Be(password);
    }

    private static ProfileItem CreateNaiveProfile(string username, string password)
    {
        var item = new ProfileItem
        {
            ConfigType = EConfigType.Naive,
            Remarks = "naive demo",
            Address = "naive.example",
            Port = 443,
            Username = username,
            Password = password,
            Network = nameof(ETransport.raw),
            StreamSecurity = Global.None,
        };
        item.SetTransportExtra(new TransportExtraItem { RawHeaderType = Global.None, });

        return item;
    }
}
