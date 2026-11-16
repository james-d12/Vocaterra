using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Stats;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Stats;

/// <summary>The one rule per stat key (I05a): sum or factor, the range an item may give, the walking speed's clamp.</summary>
public sealed class StatRulesTests
{
    [Fact]
    public void Given_TwoFactorItems_When_TheyContribute_Then_TheFactorsMultiplyAndTheSumStaysEmpty()
    {
        var builder = new StatBuilder();

        StatRules.Contribute(builder, StatKeys.WalkSpeed, 1.1);
        StatRules.Contribute(builder, StatKeys.WalkSpeed, 1.2);
        var sheet = builder.Build();

        Assert.Equal(1.32, StatRules.Read(sheet, StatKeys.WalkSpeed), 9);
        Assert.Equal(0, sheet.Sum(StatKeys.WalkSpeed));
    }

    [Fact]
    public void Given_TwoSumItems_When_TheyContribute_Then_TheyAddAndAnUntouchedFactorReadsOne()
    {
        var builder = new StatBuilder();

        StatRules.Contribute(builder, StatKeys.Armour, 5);
        StatRules.Contribute(builder, StatKeys.Armour, 3);
        var sheet = builder.Build();

        Assert.Equal(8, StatRules.Read(sheet, StatKeys.Armour));
        Assert.Equal(1, StatRules.Read(sheet, StatKeys.HungerDrain));
        Assert.Equal(0, StatRules.Read(sheet, StatKeys.MaxStamina));
    }

    [Fact]
    public void Given_AnOpenKey_When_ItContributes_Then_ItIsASum()
    {
        var builder = new StatBuilder();

        StatRules.Contribute(builder, "light", 3);
        StatRules.Contribute(builder, "light", 2);

        Assert.Equal(5, StatRules.Read(builder.Build(), "light"));
    }

    [Theory]
    [InlineData(0.05, 100)] // far below: the player still moves
    [InlineData(0.4, 400)]
    [InlineData(1.0, 1000)]
    [InlineData(1.6, 1600)]
    [InlineData(9.0, 2500)] // far above: capped
    [InlineData(double.NaN, 1000)]
    public void Given_AProductOfFactors_When_ItBecomesPermille_Then_ItIsClampedInOnePlace(double product, int permille) => Assert.Equal(permille, WalkSpeed.Permille(product));

    private static IReadOnlyList<string> Problems(string key, double amount)
    {
        var item = new ItemDefinition((ItemId)230, "test-trinket", 1, "test-trinket", ItemHeld.Sprite, ["accessory"], Wear: "accessory", Stats: new Dictionary<string, double> { [key] = amount });
        return EquipmentValidator.Validate(TestGame.Data.Equipment, TestItems.With(item));
    }

    [Theory]
    [InlineData("walkSpeed", 0.49)]
    [InlineData("walkSpeed", 1.51)]
    [InlineData("walkSpeed", 0)]
    [InlineData("hungerDrain", 0.2)]
    [InlineData("hungerDrain", 2.5)]
    [InlineData("thirstDrain", -1)]
    [InlineData("thirstDrain", 3)]
    [InlineData("maxStamina", -1)]
    [InlineData("maxStamina", 101)]
    [InlineData("armour", -1)]
    [InlineData("armour", 101)]
    [InlineData("armour", 2.5)]
    public void Given_AStatOutsideItsRange_When_TheItemIsChecked_Then_ItIsRefused(string key, double amount) =>
        Assert.Contains(Problems(key, amount), p => p.Contains($"the stat '{key}' of 'test-trinket'", StringComparison.Ordinal));

    [Theory]
    [InlineData("walkSpeed", 0.5)]
    [InlineData("walkSpeed", 1.5)]
    [InlineData("hungerDrain", 0.25)]
    [InlineData("thirstDrain", 2)]
    [InlineData("maxStamina", 0)]
    [InlineData("maxStamina", 100)]
    [InlineData("armour", 7)]
    [InlineData("light", 7.5)] // an open key has no range here
    public void Given_AStatInsideItsRange_When_TheItemIsChecked_Then_NoProblemIsReported(string key, double amount) => Assert.Empty(Problems(key, amount));

    [Fact]
    public void Given_TheShippedKeys_When_TheRulesAreRead_Then_EachHasItsOwnRule()
    {
        Assert.Equal(StatCombine.Factor, StatRules.Find(StatKeys.WalkSpeed)!.Combine);
        Assert.Equal(StatCombine.Factor, StatRules.Find(StatKeys.HungerDrain)!.Combine);
        Assert.Equal(StatCombine.Factor, StatRules.Find(StatKeys.ThirstDrain)!.Combine);
        Assert.Equal(StatCombine.Sum, StatRules.Find(StatKeys.MaxStamina)!.Combine);
        Assert.Equal(StatCombine.Sum, StatRules.Find(StatKeys.Armour)!.Combine);
        Assert.Equal(StatCombine.Sum, StatRules.Find(StatKeys.Warmth)!.Combine);
        Assert.Null(StatRules.Find("light"));
    }
}
