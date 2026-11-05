using FluentAssertions;
using FocusCatAI.Domain.Progression;
using Xunit;

namespace FocusCatAI.Application.Tests.Progression;

public class LevelCurveTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 50)]
    [InlineData(3, 150)]
    [InlineData(4, 300)]
    [InlineData(50, 61250)]
    public void XpForLevelFollowsFormula(int level, long expected) =>
        LevelCurve.XpForLevel(level).Should().Be(expected);

    [Theory]
    [InlineData(-5, 1)]
    [InlineData(0, 1)]
    [InlineData(49, 1)]
    [InlineData(50, 2)]
    [InlineData(149, 2)]
    [InlineData(150, 3)]
    [InlineData(61249, 49)]
    [InlineData(61250, 50)]
    [InlineData(long.MaxValue, 50)]
    public void LevelForXpFindsLevel(long xp, int expected) =>
        LevelCurve.LevelForXp(xp).Should().Be(expected);

    [Fact]
    public void DescribeAtZeroIsLevelOneEmptyBar()
    {
        var p = LevelCurve.Describe(0);
        (p.Level, p.CurrentLevelXp, p.NextLevelXp, p.ProgressFraction, p.IsMax).Should().Be((1, 0L, 50L, 0.0, false));
    }

    [Fact]
    public void DescribeMidLevelReportsProgress()
    {
        var p = LevelCurve.Describe(100);
        (p.Level, p.CurrentLevelXp, p.NextLevelXp, p.ProgressFraction).Should().Be((2, 50L, 100L, 0.5));
    }

    [Fact]
    public void DescribeAtMaxLevelIsFullAndMax()
    {
        var p = LevelCurve.Describe(1_000_000);
        (p.Level, p.ProgressFraction, p.IsMax).Should().Be((50, 1.0, true));
    }

    [Fact]
    public void LevelNeverDecreasesAsXpGrows()
    {
        var previous = 0;
        for (long xp = 0; xp <= 62000; xp += 7)
        {
            var level = LevelCurve.LevelForXp(xp);
            level.Should().BeGreaterThanOrEqualTo(previous);
            previous = level;
        }
    }
}
