using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Core.Tests;

/// <summary>Tests for <see cref="Margin"/>.</summary>
public class MarginTests
{
    [Fact]
    public void EvaluateIsThreeWay()
    {
        var margin = new Margin(0.5, 0.25);
        Assert.Equal(MarginDecision.Accept, margin.Evaluate(0.9));
        Assert.Equal(MarginDecision.Reject, margin.Evaluate(0.1));
        Assert.Equal(MarginDecision.Uncertain, margin.Evaluate(0.5));
    }

    [Fact]
    public void BoundariesAreInclusiveForAcceptAndReject()
    {
        var margin = new Margin(0.5, 0.25);
        Assert.Equal(MarginDecision.Accept, margin.Evaluate(0.75));
        Assert.Equal(MarginDecision.Reject, margin.Evaluate(0.25));
        Assert.Equal(MarginDecision.Uncertain, margin.Evaluate(double.BitDecrement(0.75)));
        Assert.Equal(MarginDecision.Uncertain, margin.Evaluate(double.BitIncrement(0.25)));
    }

    [Fact]
    public void ZeroEpsilonNeverReturnsUncertainAwayFromThreshold()
    {
        var margin = new Margin(1.0, 0.0);
        Assert.Equal(MarginDecision.Accept, margin.Evaluate(1.0));
        Assert.Equal(MarginDecision.Accept, margin.Evaluate(2.0));
        Assert.Equal(MarginDecision.Reject, margin.Evaluate(0.0));
    }

    [Fact]
    public void InvalidArgumentsThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Margin(0.5, -0.01));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Margin(0.5, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Margin(double.NaN, 0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Margin(0.5, 0.1).Evaluate(double.NaN));
    }

    [Fact]
    public void ValueEqualityHolds()
    {
        Assert.Equal(new Margin(0.5, 0.1), new Margin(0.5, 0.1));
        Assert.NotEqual(new Margin(0.5, 0.1), new Margin(0.5, 0.2));
    }

    [Fact]
    public void StoredTimestampArithmeticAndComparison()
    {
        var start = new StoredTimestamp(1_000);
        var later = start.AddSeconds(60);
        Assert.Equal(1_060, later.UnixSeconds);
        Assert.Equal(60, start.SecondsUntil(later));
        Assert.Equal(-60, later.SecondsUntil(start));
        Assert.True(start < later);
        Assert.True(later > start);
        Assert.True(start <= new StoredTimestamp(1_000));
        Assert.True(start >= new StoredTimestamp(1_000));
        Assert.False(later <= start);
        Assert.Equal(-1, Math.Sign(start.CompareTo(later)));
        Assert.Equal(start, new StoredTimestamp(1_000));
        Assert.Throws<OverflowException>(() => new StoredTimestamp(long.MaxValue).AddSeconds(1));
    }
}
