using NJsonSchema;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Damage;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Stats;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Tests.Damage;

/// <summary>The damage pipeline (I05a): armour's formula, which causes it applies to (the data's), the checks of game/damage.json and drowning through the real session.</summary>
public sealed class DamagePipelineTests
{
    private static readonly DamageCause Fall = new("fall");

    private sealed class Record : IPlayerHealth
    {
        public List<(int Amount, DamageCause Cause)> Hurts { get; } = [];

        public bool IsDead => false;

        public void Damage(int amount, DamageCause cause) => Hurts.Add((amount, cause));
    }

    private static StatSheet Armour(double rating)
    {
        var builder = new StatBuilder();
        StatRules.Contribute(builder, StatKeys.Armour, rating);
        return builder.Build();
    }

    private static (ModifiedHealth Health, Record Inner) Rig(string json, double armour)
    {
        var inner = new Record();
        return (new ModifiedHealth(inner, DamageData.Parse(json), DamageModifiers.Standard(), () => Armour(armour)), inner);
    }

    private const string ArmourOnFall = """{ "schemaVersion": 1, "causes": [ { "id": "drowning", "modifiers": [] }, { "id": "fall", "modifiers": ["armour"] } ] }""";

    [Theory]
    [InlineData(0, 10, 10)]
    [InlineData(5, 10, 8)] // 20% off
    [InlineData(10, 10, 6)] // 40% off
    [InlineData(15, 10, 4)] // 60% off
    [InlineData(20, 10, 2)] // the cap of 80%
    [InlineData(100, 10, 2)] // never more than the cap
    [InlineData(100, 1, 1)] // a hit is never reduced to nothing
    [InlineData(7, 25, 18)] // 28% off 25 is 18
    public void Given_ArmourWorn_When_TheModifierRuns_Then_TheFormulaTakesItsShareOff(double armour, int amount, int expected) =>
        Assert.Equal(expected, new ArmourDamageModifier().Modify(amount, Fall, Armour(armour)));

    [Fact]
    public void Given_ACauseWithArmourInItsRow_When_ItHurts_Then_TheDamageIsReduced()
    {
        var (health, inner) = Rig(ArmourOnFall, 10);

        health.Damage(10, Fall);

        Assert.Equal([(6, Fall)], inner.Hurts);
    }

    [Fact]
    public void Given_ACauseWithNoModifier_When_ItHurtsWithArmourWorn_Then_TheDamageIsTakenAsItIs()
    {
        var (health, inner) = Rig(ArmourOnFall, 20);

        health.Damage(10, DamageCause.Drowning);

        Assert.Equal([(10, DamageCause.Drowning)], inner.Hurts);
    }

    [Fact]
    public void Given_ACauseTheFileDoesNotList_When_ItHurts_Then_TheDamageIsTakenAsItIs()
    {
        var (health, inner) = Rig(ArmourOnFall, 20);

        health.Damage(10, new DamageCause("lava"));

        Assert.Equal(10, Assert.Single(inner.Hurts).Amount);
    }

    [Fact]
    public void Given_ADeadInnerHealth_When_AskedIfDead_Then_TheDecoratorSaysSo()
    {
        var health = new ModifiedHealth(new NoPlayerHealth(), DamageData.Parse(ArmourOnFall), DamageModifiers.Standard(), () => StatSheet.Empty);

        Assert.False(health.IsDead);
    }

    [Fact]
    public void Given_TwoModifiersInARow_When_ItHurts_Then_TheyRunInTheOrderTheRowNames()
    {
        var modifiers = DamageModifiers.Standard();
        modifiers.Add(new Halve());
        var inner = new Record();
        var data = DamageData.Parse("""{ "schemaVersion": 1, "causes": [ { "id": "fall", "modifiers": ["halve", "armour"] } ] }""");
        var health = new ModifiedHealth(inner, data, modifiers, () => Armour(10));

        health.Damage(10, Fall); // 10 halved is 5, then 40% off is 3

        Assert.Equal(3, inner.Hurts[0].Amount);
    }

    private sealed class Halve : IDamageModifier
    {
        public string Id => "halve";

        public int Modify(int amount, DamageCause cause, StatSheet stats) => amount / 2;
    }

    [Fact]
    public void Given_TheShippedFile_When_ItIsChecked_Then_ArmourAppliesToNoCauseAndTheFileHoldsTogether()
    {
        var data = TestGame.Data.Damage;

        Assert.Empty(DamageValidator.Validate(data, DamageModifiers.Standard()));
        Assert.Equal(["drowning"], data.Causes.Select(static c => c.Id));
        Assert.All(data.Causes, static c => Assert.Empty(c.Modifiers));
    }

    [Fact]
    public void Given_ATestCauseAndFile_When_ItIsChecked_Then_ItHoldsTogetherAndDrowningIsStillThere()
    {
        var data = DamageData.Parse(ArmourOnFall);

        Assert.Empty(DamageValidator.Validate(data, DamageModifiers.Standard(), [DamageCause.Drowning, Fall]));
    }

    [Fact]
    public void Given_AnUnknownModifierId_When_TheFileIsChecked_Then_ItIsRefused()
    {
        var data = DamageData.Parse("""{ "schemaVersion": 1, "causes": [ { "id": "drowning", "modifiers": ["magic"] } ] }""");

        Assert.Contains("game/damage.json: the cause 'drowning' names the modifier 'magic', which is not one of armour, wear.", DamageValidator.Validate(data, DamageModifiers.Standard()));
    }

    [Fact]
    public void Given_AnUnknownCause_When_TheFileIsChecked_Then_ItIsRefused()
    {
        var data = DamageData.Parse("""{ "schemaVersion": 1, "causes": [ { "id": "drowning", "modifiers": [] }, { "id": "lava", "modifiers": [] } ] }""");

        Assert.Contains("game/damage.json: the cause 'lava' is not one the game raises (drowning).", DamageValidator.Validate(data, DamageModifiers.Standard()));
    }

    [Fact]
    public void Given_ACauseTheCodeRaisesWithNoRow_When_TheFileIsChecked_Then_ItIsRefused()
    {
        var data = DamageData.Parse("""{ "schemaVersion": 1, "causes": [] }""");

        Assert.Contains("game/damage.json: the game raises the cause 'drowning' and the file has no row for it.", DamageValidator.Validate(data, DamageModifiers.Standard()));
    }

    [Fact]
    public void Given_ADuplicateCauseOrModifier_When_TheFileIsChecked_Then_BothAreReported()
    {
        var data = DamageData.Parse("""{ "schemaVersion": 1, "causes": [ { "id": "drowning", "modifiers": ["armour", "armour"] }, { "id": "drowning", "modifiers": [] } ] }""");

        var problems = DamageValidator.Validate(data, DamageModifiers.Standard());

        Assert.Contains("game/damage.json: the cause id 'drowning' is used twice.", problems);
        Assert.Contains("game/damage.json: the cause 'drowning' names the modifier 'armour' twice.", problems);
    }

    [Fact]
    public void Given_ABundleWhoseDamageFileNamesAnUnknownModifier_When_TheWholeContentIsChecked_Then_TheGameDataValidatorReportsIt()
    {
        var data = TestGame.Data.WithDamage(DamageData.Parse("""{ "schemaVersion": 1, "causes": [ { "id": "drowning", "modifiers": ["magic"] } ] }"""));

        Assert.Contains(GameDataValidator.Validate(data), static p => p.Contains("the modifier 'magic'", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2, "causes": [] }""")]
    [InlineData("""{ "schemaVersion": 1 }""")]
    [InlineData("""{ "schemaVersion": 1, "causes": [ { "id": "Drowning", "modifiers": [] } ] }""")]
    [InlineData("""{ "schemaVersion": 1, "causes": [ { "id": "drowning" } ] }""")]
    [InlineData("""{ "schemaVersion": 1, "causes": [ { "id": "drowning", "modifiers": ["armour", "armour"] } ] }""")]
    [InlineData("""{ "schemaVersion": 1, "causes": [ { "id": "drowning", "modifiers": [], "scale": 2 } ] }""")]
    public void Given_AMalformedFile_When_ItIsCheckedAgainstTheSchema_Then_ItIsRefused(string json) =>
        Assert.NotEmpty(Schema().Validate(json));

    [Fact]
    public void Given_TheShippedFile_When_ItIsCheckedAgainstTheSchema_Then_ItPasses() =>
        Assert.Empty(Schema().Validate(File.ReadAllText(Repo.Path("content", "game-v1", "game", "damage.json"))));

    private static JsonSchema Schema() => JsonSchema.FromFileAsync(Repo.Path("content", "schema", "damage.schema.json")).GetAwaiter().GetResult();

    [Fact]
    public void Given_ArmourWornInTheSession_When_PlayerDrowns_Then_TheDamageIsExactlyTheDrowningRuleWithNoReduction()
    {
        var withArmour = GearItems.Session();
        var plain = GearItems.Session();
        _ = withArmour.Simulation.Equip(GearItems.Bag(withArmour.Simulation, GearItems.Plate), GearItems.Slot("armour", "body"));
        _ = withArmour.Simulation.Equip(GearItems.Bag(withArmour.Simulation, GearItems.Helm, 13), GearItems.Slot("armour", "head"));
        withArmour.Simulation.RefreshStats();

        withArmour.PlayerHealth.Damage(DrowningDamage.DrowningDamagePerSecond, DamageCause.Drowning);
        plain.PlayerHealth.Damage(DrowningDamage.DrowningDamagePerSecond, DamageCause.Drowning);

        var health = NeedMeter.Health;
        Assert.Equal(7, withArmour.Simulation.Stats.Get(StatKeys.Armour));
        Assert.Equal(plain.Needs.Value(health), withArmour.Needs.Value(health));
        Assert.Equal(90, withArmour.Needs.Value(health));
    }

    [Fact]
    public void Given_ASessionWithADamageFileWhereArmourAppliesToDrowning_When_PlayerDrowns_Then_ArmourReducesIt()
    {
        var data = TestGame.Data.WithItems(GearItems.Catalogue).WithDamage(DamageData.Parse("""{ "schemaVersion": 1, "causes": [ { "id": "drowning", "modifiers": ["armour"] } ] }"""));
        var session = new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), CreditRules.FullTyped, TestGame.NoLessons);
        _ = session.Simulation.Equip(GearItems.Bag(session.Simulation, GearItems.Plate), GearItems.Slot("armour", "body"));
        _ = session.Simulation.Equip(GearItems.Bag(session.Simulation, GearItems.Helm, 13), GearItems.Slot("armour", "head"));

        session.PlayerHealth.Damage(DrowningDamage.DrowningDamagePerSecond, DamageCause.Drowning); // 7 armour: 28% off 10 is 7

        Assert.Equal(93, session.Needs.Value(NeedMeter.Health));
    }
}
