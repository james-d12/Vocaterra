using Vocaterra.Game.Core.Stats;

namespace Vocaterra.Game.Core.Tests.Stats;

/// <summary>The equipment panel's stats line (I04): only a stat that is not at its default, and the Spanish noun only where the game has one.</summary>
public sealed class StatDisplayTests
{
    private static StatSheet Sheet(Action<StatBuilder> fill)
    {
        var builder = new StatBuilder();
        fill(builder);
        return builder.Build();
    }

    [Fact]
    public void Given_AnEmptySheet_When_TheChipsAreMade_Then_ThereAreNone()
    {
        Assert.Empty(StatDisplay.Chips(StatSheet.Empty));
        Assert.Empty(StatDisplay.Chips(Sheet(static b => { b.Add(StatKeys.Armour, 0); b.Multiply(StatKeys.WalkSpeed, 1.0); b.Multiply(StatKeys.ThirstDrain, 1.004); })));
    }

    [Fact]
    public void Given_ArmourAndWarmth_When_TheChipsAreMade_Then_TheyComeFirstWithTheirNouns()
    {
        var sheet = Sheet(static b => { b.Add(StatKeys.Warmth, 15); b.Add(StatKeys.Armour, 7); });

        var chips = StatDisplay.Chips(sheet);

        Assert.Equal([new StatChip(StatKeys.Armour, "7", "armadura"), new StatChip(StatKeys.Warmth, "+1.5", "calor")], chips);
    }

    [Fact]
    public void Given_TheAccessoriesEffects_When_TheChipsAreMade_Then_ThePercentsAreSignedAndHaveNoNoun()
    {
        var sheet = Sheet(static b =>
        {
            b.Multiply(StatKeys.WalkSpeed, 1.1);
            b.Multiply(StatKeys.HungerDrain, 0.8);
            b.Add(StatKeys.MaxStamina, 10);
        });

        var chips = StatDisplay.Chips(sheet);

        Assert.Equal(
            [new StatChip(StatKeys.WalkSpeed, "+10%", null), new StatChip(StatKeys.HungerDrain, "-20%", null), new StatChip(StatKeys.MaxStamina, "+10", null)],
            chips);
    }

    [Fact]
    public void Given_ANounTheGameLacks_When_TheChipsAreMade_Then_TheChipHasNoLemma()
    {
        var sheet = Sheet(static b => b.Add(StatKeys.Armour, 3));

        var chips = StatDisplay.Chips(sheet, hasNoun: static _ => false);

        Assert.Equal([new StatChip(StatKeys.Armour, "3", null)], chips);
    }

    [Fact]
    public void Given_ColdWarmth_When_TheChipsAreMade_Then_TheNegativeNumberKeepsItsSign()
    {
        var sheet = Sheet(static b => b.Add(StatKeys.Warmth, -5));

        Assert.Equal("-0.5", StatDisplay.Chips(sheet).Single().Value);
    }

    [Fact]
    public void Given_ANewRow_When_ATableWithItIsUsed_Then_TheLineShowsItWithNoEditToTheCode()
    {
        IReadOnlyList<StatDisplay> rows = [.. StatDisplay.Defaults, new StatDisplay("stealth", StatDisplayFormat.Whole, null)];
        var sheet = Sheet(static b => b.Add("stealth", 4));

        Assert.Equal([new StatChip("stealth", "+4", null)], StatDisplay.Chips(sheet, rows));
    }

    [Fact]
    public void Given_ALightAboveZero_When_TheChipsAreMade_Then_ALightChipShowsItsLevel()
    {
        var chips = StatDisplay.Chips(Sheet(static b => b.Add(StatKeys.Light, 12)));

        Assert.Equal([new StatChip(StatKeys.Light, "+12", null)], chips);
        Assert.Empty(StatDisplay.Chips(Sheet(static b => b.Add(StatKeys.Light, 0))));
    }
}
