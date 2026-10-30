using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Language.Checks;

/// <summary>
/// The order of a check battery (S11): seeded by the world seed and the check kind, the same for the same pair, and the same content in every order; since
/// VILLAGE_PLAN V07 the items of the lessons the play session has unlocked, the pilot's 31 kept whole.
/// </summary>
public sealed class CheckPlanTests
{
    private static IReadOnlyList<CheckItem> Bank => RouteScene.DataOf(TestGame.Bundle).Checks;

    [Fact]
    public void TheSameSeedAndKindGiveTheSamePlan()
    {
        var first = CheckPlan.For(Bank, CheckKind.Pre, TestGame.Seed);
        var second = CheckPlan.For(Bank, CheckKind.Pre, TestGame.Seed);

        Assert.Equal(Describe(first), Describe(second));
    }

    [Fact]
    public void APreCheckAndAPostCheckHaveTheSameItemsInOtherOrdersAndOptionsInOtherOrders()
    {
        var pre = CheckPlan.For(Bank, CheckKind.Pre, TestGame.Seed);
        var post = CheckPlan.For(Bank, CheckKind.Post, TestGame.Seed);

        Assert.Equal(pre.Select(static e => e.Item.Id).OrderBy(static id => id, StringComparer.Ordinal), post.Select(static e => e.Item.Id).OrderBy(static id => id, StringComparer.Ordinal));
        Assert.NotEqual(pre.Select(static e => e.Item.Id), post.Select(static e => e.Item.Id));
        Assert.NotEqual(Describe(pre), Describe(post));
    }

    [Fact]
    public void ADifferentWorldSeedGivesADifferentOrder()
    {
        var seeds = new ulong[] { 1, 2, 3, 4, 5 };
        var orders = seeds.Select(seed => string.Join(",", CheckPlan.For(Bank, CheckKind.Pre, seed).Select(static e => e.Item.Id))).Distinct().Count();

        Assert.True(orders > 1, "Five seeds gave one order.");
    }

    [Fact]
    public void EveryOptionIsShownOnceAndTheRightOneStaysRight()
    {
        foreach (var entry in CheckPlan.For(Bank, CheckKind.Recall, TestGame.Seed))
        {
            if (entry.Item.Kind == CheckItemKind.Production)
            {
                Assert.Empty(entry.OptionOrder);
                Assert.Equal(-1, entry.ShownCorrect);
                continue;
            }

            Assert.Equal([0, 1, 2], entry.OptionOrder.OrderBy(static i => i).ToList());
            Assert.Equal(entry.Item.Options[entry.Item.Correct].Label, entry.ShownOptions[entry.ShownCorrect].Label);
            for (var shown = 0; shown < 3; shown++)
            {
                Assert.Equal(shown == entry.ShownCorrect, entry.IsCorrect(shown));
            }
        }
    }

    [Fact]
    public void ThePositionsAreTheOrderOfTheBattery()
    {
        var plan = CheckPlan.For(Bank, CheckKind.Pre, TestGame.Seed);

        Assert.Equal(Enumerable.Range(0, plan.Count), plan.Select(static e => e.Position));
        Assert.Equal(Bank.Count, plan.Count);
    }

    // The pilot's battery (docs/game/PLAYTEST.md, sections 3, 4 and 8): these 31 items in this order, the pre- and post-check in the first play session, the recall
    // in the second.
    private static readonly string[] PilotBattery =
    [
        "hear.que-necesitas", "hear.quieres-pan", "hear.pedir-agua", "hear.si-por-favor", "hear.no-gracias", "hear.donde-esta-el-rio", "hear.quieres-fruta",
        "hear.necesitas-madera", "hear.me-llamo", "hear.es-madera", "hear.de-nada", "hear.que-quieres", "hear.tienes-fruta", "hear.que-quieren-ustedes",
        "say.pedir-agua", "say.si-por-favor", "say.no-gracias", "say.donde-esta-el-rio", "say.si", "say.no", "say.que-es-esto", "say.otra-vez", "say.mas-despacio",
        "say.quiero-madera", "say.tengo-fruta", "say.ella-quiere-fruta", "say.necesitamos-agua", "hear.que-vas-a-comer", "hear.que-comiste-ayer",
        "say.voy-a-comer-pan", "say.ayer-comi-fruta",
    ];

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ThePilotsSessionsAskTheWholeBatteryOfThirtyOneExactlyAsBefore(int playSession)
    {
        // VILLAGE_PLAN V07: a battery asks the items of the lessons its play session has unlocked, and the lessons of the first three sessions are the pilot's,
        // asked from the first session on (coreSessions). So the pilot's batteries are the 31 items, in the same order with the same options, as before.
        var data = RouteScene.DataOf(TestGame.Bundle);
        Assert.Equal(PilotBattery, data.Checks.AskedIn(playSession, data.Lessons).Select(static i => i.Id));
        foreach (var kind in new[] { CheckKind.Pre, CheckKind.Post, CheckKind.Recall })
        {
            var plan = CheckPlan.For(data.Checks, data.Lessons, playSession, kind, TestGame.Seed);
            Assert.Equal(31, plan.Count);
            Assert.Equal(Describe(CheckPlan.For(Bank, kind, TestGame.Seed)), Describe(plan));
        }
    }

    [Fact]
    public void ALaterLessonsItemsJoinTheBatteryFromTheSessionItOpensIn()
    {
        // Three lessons opening in sessions 1, 4 and 6, an item of each, one of no lesson and one of a lesson the bundle does not have.
        var lessons = TestGame.Lessons("""
            { "schemaVersion": 1, "lessons": [
              { "id": "a", "npc": "villager", "title": "A", "english": "A.", "steps": [ { "id": "s", "type": "explain", "english": "A." } ] },
              { "id": "b", "npc": "villager", "title": "B", "english": "B.", "objective": "Talk to the villager.", "session": 4, "steps": [ { "id": "s", "type": "explain", "english": "B." } ] },
              { "id": "c", "npc": "villager", "title": "C", "english": "C.", "objective": "Talk to the villager.", "session": 6, "steps": [ { "id": "s", "type": "explain", "english": "C." } ] } ] }
            """);
        static string Item(string id, string lesson) =>
            $$"""{"id":"{{id}}","kind":"production","phrase":"pedir-agua","lesson":{{lesson}},"situation":"You want water.","prompt":"stall-need","intent":"request_item"}""";
        var json = """{"schemaVersion":2,"coreSessions":3,"items":[""" + string.Join(",", Item("say.a", "\"a\""), Item("say.b", "\"b\""), Item("say.c", "\"c\""), Item("say.none", "null"), Item("say.lost", "\"lost\"")) + "]}";
        var (file, strings) = LocaleFixture.SplitChecks(json);
        var bank = CheckBank.Parse(file, TestGame.Locale.With(strings));

        string Asked(CheckBank battery, int session) => string.Join(" ", battery.AskedIn(session, lessons).Select(static i => i.Id));
        Assert.Equal("say.a say.none say.lost", Asked(bank, 1)); // b opens in session 4, past the three core sessions
        Assert.Equal("say.a say.none say.lost", Asked(bank, 3));
        Assert.Equal("say.a say.b say.none say.lost", Asked(bank, 4));
        Assert.Equal("say.a say.b say.c say.none say.lost", Asked(bank, 6));
        Assert.Equal("say.a say.b say.none say.lost", Asked(new CheckBank(bank, coreSessions: 4), 1)); // four core sessions take b from the first
        Assert.Equal(["say.a", "say.lost", "say.none"], CheckPlan.For(bank, lessons, 2, CheckKind.Pre, TestGame.Seed).Select(static e => e.Item.Id).Order(StringComparer.Ordinal)); // the plan of what is asked
    }

    // The items in order with their option orders, as one comparable string.
    private static string Describe(IEnumerable<CheckPlanEntry> plan) =>
        string.Join("|", plan.Select(static e => e.Item.Id + ":" + string.Join(".", e.OptionOrder)));
}
