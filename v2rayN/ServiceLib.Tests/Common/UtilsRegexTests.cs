using AwesomeAssertions;
using ServiceLib.Common;
using Xunit;

namespace ServiceLib.Tests.Common;

public class UtilsRegexTests
{
    [Fact]
    public void IsRegexMatch_ReturnsExpectedResultForValidPattern()
    {
        Utils.IsRegexMatch("node-us-01", "^node-").Should().BeTrue();
        Utils.IsRegexMatch("node-us-01", "^server-").Should().BeFalse();
    }

    [Fact]
    public void IsRegexMatch_FailsOpenForInvalidPattern()
    {
        Utils.IsRegexMatch("node", "[").Should().BeTrue();
    }

    [Fact]
    public void IsRegexMatch_FailsOpenWhenPatternTimesOut()
    {
        var input = new string('a', 20_000) + "!";

        Utils.IsRegexMatch(input, "(a+)+$", timeoutSeconds: 1).Should().BeTrue();
    }
}
