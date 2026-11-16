using Vocaterra.Game.Core.Stats;

namespace Vocaterra.Game.Core.Tests.Stats;

/// <summary>The sheet, its builder and the pipeline (I03a): sums and factors by open key, and a key nobody touched.</summary>
public sealed class StatSheetTests
{
    [Fact]
    public void Given_NothingAdded_When_AKeyIsRead_Then_ItHasNoSumAndNoFactor()
    {
        var sheet = new StatBuilder().Build();

        Assert.Equal(0, sheet.Sum("warmth"));
        Assert.Equal(1, sheet.Factor("warmth"));
        Assert.Equal(0, sheet.Get("warmth"));
        Assert.Empty(sheet.Keys);
        Assert.Equal(0, StatSheet.Empty.GetWhole("anything"));
    }

    [Fact]
    public void Given_SeveralAdditionsAndFactors_When_TheSheetIsBuilt_Then_TheSumsAddAndTheFactorsMultiply()
    {
        var builder = new StatBuilder();
        builder.Add("warmth", 30);
        builder.Add("warmth", 20);
        builder.Add("warmth", -5);
        builder.Multiply("walkSpeed", 1.1);
        builder.Multiply("walkSpeed", 0.5);
        builder.Add("walkSpeed", 2);

        var sheet = builder.Build();

        Assert.Equal(45, sheet.Sum("warmth"));
        Assert.Equal(1, sheet.Factor("warmth"));
        Assert.Equal(0.55, sheet.Factor("walkSpeed"), 10);
        Assert.Equal(1.1, sheet.Get("walkSpeed"), 10);
        Assert.Equal(["walkSpeed", "warmth"], sheet.Keys);
    }

    [Theory]
    [InlineData(2.5, 3)]
    [InlineData(-2.5, -3)]
    [InlineData(2.4, 2)]
    public void Given_AFractionalStat_When_ItIsReadAsAWholeNumber_Then_HalvesGoAwayFromZero(double value, int whole)
    {
        var builder = new StatBuilder();
        builder.Add("warmth", value);

        Assert.Equal(whole, builder.Build().GetWhole("warmth"));
    }

    [Theory]
    [InlineData("Warmth")]
    [InlineData("1warmth")]
    [InlineData("warm-th")]
    [InlineData("")]
    public void Given_AKeyThatIsNotWellFormed_When_ItIsAdded_Then_TheBuilderRefusesIt(string key)
    {
        _ = Assert.Throws<ArgumentException>(() => new StatBuilder().Add(key, 1));
        _ = Assert.Throws<ArgumentException>(() => new StatBuilder().Multiply(key, 1));
        Assert.False(StatKeys.IsWellFormed(key));
    }

    [Fact]
    public void Given_ANumberThatIsNotFinite_When_ItIsAdded_Then_TheBuilderRefusesIt()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new StatBuilder().Add("warmth", double.NaN));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new StatBuilder().Multiply("warmth", double.PositiveInfinity));
    }

    [Fact]
    public void Given_TwoContributorsRegistered_When_ThePipelineBuilds_Then_TheSheetSumsBothInOrder()
    {
        var pipeline = new StatPipeline();
        pipeline.Add(new FixedStat("warmth", 30));
        pipeline.Add(new FixedStat("warmth", 12));
        pipeline.Add(new FixedStat("light", 4));

        var sheet = pipeline.Build();

        Assert.Equal(42, sheet.Get("warmth"));
        Assert.Equal(4, sheet.Get("light"));
        Assert.Equal(3, pipeline.Contributors.Count);
    }

    internal sealed class FixedStat(string key, double amount) : IStatContributor
    {
        public void Contribute(StatBuilder builder) => builder.Add(key, amount);
    }
}
