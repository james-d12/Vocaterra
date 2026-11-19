using Vocaterra.Game.UI.Inventory;

namespace Vocaterra.Game.Tests.UI.Inventory;

/// <summary>I10b (QA-2026-11-19-ui-4): a cut bag label never reads the same as another's in the same bag.</summary>
public sealed class BagLabelFitterTests
{
    // A stand-in font: 8 pixels a letter at the normal size, one less for each step.
    private static float Measure(string text, int step) => text.Length * (8 - step);

    private static readonly string[] Bag = ["madera", "manzana", "escudo", "estaca", "pan", "hacha"];

    [Fact]
    public void Given_NamesThatShareTheirFirstLetters_When_TheyAreDistinguished_Then_EachGetsTheShortestPrefixThatTellsItApart()
    {
        Assert.Equal(3, BagLabelFitter.DistinguishingLength("madera", Bag)); // mad / man
        Assert.Equal(3, BagLabelFitter.DistinguishingLength("manzana", Bag));
        Assert.Equal(3, BagLabelFitter.DistinguishingLength("escudo", Bag)); // esc / est
        Assert.Equal(3, BagLabelFitter.DistinguishingLength("hacha", Bag)); // never fewer than three
    }

    [Theory]
    [InlineData(1280, 36)]
    [InlineData(800, 30)]
    public void Given_ANarrowSlot_When_EveryNameOfTheBagIsFitted_Then_NoTwoReadTheSame(int canvasWidth, int room)
    {
        var drawn = Bag.Select(name => BagLabelFitter.Fit(name, Bag, room, Measure).Text).ToList();

        Assert.Equal(drawn.Count, drawn.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        _ = canvasWidth;
    }

    [Fact]
    public void Given_ARoomThatHoldsTwoLettersAndTheDots_When_TwoNamesShareThem_Then_ASmallerFontShowsTheLettersThatTellThemApart()
    {
        var (text, step) = BagLabelFitter.Fit("madera", Bag, 30, Measure);

        Assert.StartsWith("mad", text, StringComparison.Ordinal);
        Assert.True(step > 0, "the normal font holds only two letters and the dots");
    }

    [Fact]
    public void Given_ARoomForTheWholeName_When_ItIsFitted_Then_ItIsUntouched() =>
        Assert.Equal(("madera", 0), BagLabelFitter.Fit("madera", Bag, 100, Measure));

    [Fact]
    public void Given_ALongNameAmongDifferentOnes_When_ItIsCut_Then_ItKeepsAsManyLettersAsFitAtTheNormalSize()
    {
        var (text, step) = BagLabelFitter.Fit("zanahoria", ["zanahoria", "pan"], 56, Measure);

        Assert.Equal(("zana...", 0), (text, step)); // 56 px is seven letters at the normal size: four and the three dots
    }
}
