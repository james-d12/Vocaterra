using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>
/// The test aid <c>--skip-to-session</c> (V12c): a new game stands where a learner who did sessions 1 to n-1 would, built from the data. The lesson of session n is unlocked,
/// named by the objective and pointed at; the earlier lessons are completed in their own sessions; session 1's ending is reached; the earlier orders are delivered and paid
/// and the lessons' purchases made, so the coins and the items follow the plan's path (VILLAGE_PLAN 3.4); and no practice is faked.
/// </summary>
public sealed class SessionSkipTests
{
    private static string Hud(VoxelSession session) => string.Concat(session.ObjectiveHud().Select(static p => p.Text));

    private static VoxelSession SkippedTo(int session)
    {
        var game = TestGame.NewSessionWithShippedLessons();
        SessionSkip.Apply(game, session);
        return game;
    }

    public static TheoryData<int, string, string> Sessions => new()
    {
        { 2, "personas", "villager" },
        { 3, "ayer-y-manana", "villager" },
        { 4, "el-pueblo", "fisher" },
        { 5, "numeros-y-pan", "baker" },
        { 6, "gente-del-pueblo", "villager" },
        { 7, "como-es", "farmer" },
    };

    [Theory]
    [MemberData(nameof(Sessions))]
    public void TheSessionsLessonIsUnlockedNamedAndPointedAtAndTheEarlierOnesAreCompletedInTheirOwnSessions(int target, string lesson, string npc)
    {
        var session = SkippedTo(target);

        Assert.Equal(target, session.PlaySession);
        Assert.Equal(target, session.SkippedTo);
        Assert.Equal(lesson, session.Lessons.NewLesson?.Id);
        Assert.Equal(session.Lessons.Book.Find(lesson)!.Objective, Hud(session)); // its own line, as after a load (S26)
        Assert.Equal(npc, session.Pointer?.Point);
        foreach (var earlier in session.Lessons.Book.Lessons)
        {
            var row = session.Lessons.Record.Single(r => r.LessonId == earlier.Id);
            if (earlier.Session < target)
            {
                Assert.Equal(LessonStatus.Completed, row.Status);
                Assert.Equal(earlier.Session, row.CompletedSession);
                Assert.Equal(earlier.Steps.Count, row.Units.Count); // the book ticks its units
                Assert.All(row.Units, static unit => Assert.Equal(StepOutcome.Skipped, unit.Outcome)); // none was taken by the learner
                Assert.Equal(0, row.Turns);
            }
            else
            {
                Assert.Equal(LessonStatus.NotStarted, row.Status);
            }
        }

        // Session 1's ending: the camp done, the player's own fire lit (a torch on its rim), so the sleep works from there.
        Assert.True(session.SessionComplete);
        Assert.True(session.FireLit);
        Assert.True(session.GuideMet);
        Assert.All(session.Objectives.Where(static o => o.Spec.EndsSession), static o => Assert.True(o.Complete));
        Assert.All(session.Objectives.Where(o => o.Spec.IsOrder), o => Assert.Equal(o.Spec.Session < target, o.Complete)); // the earlier orders delivered, the later ones open
    }

    // The coins and the items in hand at the start of the session, on the plan's path (VILLAGE_PLAN 3.4; the axe order's file says it: two coins from fruit-for-mar, four from
    // oven-wood, four spent on two loaves, and V13: two on the axe, so none at session 7): what the scripted play of the sessions before reaches in what only an NPC gives.
    // The gathered goods were used in the play; the bottle the shopkeeper gave in session 1 stays full of the river's water (nothing empties a bottle).
    public static TheoryData<int, int, int, int> Economy => new()
    {
        { 2, 0, 0, 0 },
        { 3, 0, 0, 0 },
        { 4, 0, 0, 0 },
        { 5, 2, 0, 0 },
        { 6, 2, 2, 0 },
        { 7, 0, 2, 1 },
    };

    [Theory]
    [MemberData(nameof(Economy))]
    public void TheCoinsAndTheItemsFollowThePlansPathAndTheSessionsOrderCanBePaid(int target, int coins, int bread, int axes)
    {
        var session = SkippedTo(target);
        var bag = session.Simulation.Inventory;

        Assert.Equal(coins, bag.Count(ItemId.Coin));
        Assert.Equal(bread, bag.Count(ItemId.Bread));
        Assert.Equal(axes, bag.Count(ItemId.Axe));
        Assert.Equal(1, bag.Count(ItemId.Book));
        Assert.Equal(0, bag.Count(ItemId.Wood) + bag.Count(ItemId.Berry) + bag.Count(ItemId.Torch) + bag.Count(ItemId.Bottle));
        Assert.Equal(1, bag.Count(ItemId.WaterBottle)); // session 1's camp filled the shopkeeper's bottle at the river (V13: the farmer's order takes it)

        // What the session's order needs that only an NPC gives (coins, and the bottle of water) is in hand; what the world gives (wood, fruit) the lesson points at.
        foreach (var order in session.Objectives.Where(o => o.Spec.IsOrder && o.Spec.Session == target))
        {
            foreach (var goods in order.Spec.Goods.Where(static g => g.Item is ItemId.Coin or ItemId.WaterBottle))
            {
                Assert.True(bag.Count(goods.Item) >= goods.Count, $"the order {order.Id} needs {goods.Count} of {goods.Item}");
            }
        }
    }

    [Fact]
    public void TheOrdersAreDeliveredByTheNpcsOwnThanksAndTheBreadIsBoughtAtItsPrice()
    {
        var session = SkippedTo(6);
        var world = session.Events.OfType<WorldEvent>().ToList();

        Assert.Equal(["fruit-for-mar", "oven-wood"], world.Where(static e => e.Name == WorldEvents.OrderDelivered).Select(static e => e.Detail));
        Assert.Contains(world, static e => e.Name == WorldEvents.ItemGiven && e.Npc == "fisher" && e.Detail!.StartsWith("moneda", StringComparison.Ordinal));
        Assert.Contains(world, static e => e.Name == WorldEvents.ItemGiven && e.Npc == "baker" && e.Detail!.StartsWith("pan", StringComparison.Ordinal));
        Assert.Equal(WorldEvents.SessionSkipped, world.Last(static e => e.Name != WorldEvents.BeaconSet).Name);
        Assert.Equal("skipped to play session 6 (sessions 1 to 5 counted as done; test aid)", world.Single(static e => e.Name == WorldEvents.SessionSkipped).Detail);

        var purchases = session.Lessons.Book.Lessons.SelectMany(l => SessionSkip.Purchases(l, session.Data).Select(p => (l.Id, p))).ToList();
        var (lesson, purchase) = Assert.Single(purchases); // the two loaves of Números y pan, from its do step and its request "Quiero dos panes, por favor."
        Assert.Equal("numeros-y-pan", lesson);
        Assert.Equal(new SessionSkip.Purchase("baker", ItemId.Bread, 2, 2), purchase);
    }

    [Fact]
    public void NoPracticeIsFakedTheWordAndPhraseRecordsStayANewGames()
    {
        var fresh = TestGame.NewSessionWithShippedLessons();
        var session = SkippedTo(6);

        Assert.Equal(fresh.Words.Records, session.Words.Records);
        Assert.Equal(fresh.PhraseProgress.Records, session.PhraseProgress.Records);
        Assert.Equal(fresh.CardedLemmas(), session.CardedLemmas());
        Assert.Empty(session.TakeWordCards());
        Assert.Empty(session.Learner.History);
        Assert.Equal(fresh.StoredTime, session.StoredTime); // no time passes
    }

    [Fact]
    public void TheSaveCarriesTheMarkAndAnOrdinarySaveDoesNot()
    {
        var session = SkippedTo(4);
        var text = SaveGame.Save(session, TestGame.Route());
        Assert.Contains("\"skippedTo\":4", text, StringComparison.Ordinal);

        var loaded = SaveGame.Load(text, TestGame.Data, static seed => TestGame.Route(seed));
        Assert.Equal(4, loaded.SkippedTo);
        Assert.Equal(5, loaded.PlaySession);

        Assert.DoesNotContain("skippedTo", SaveGame.Save(TestGame.NewSessionWithShippedLessons(), TestGame.Route()), StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyANewGameIsSkippedAndOnlyToASessionThatHasALesson()
    {
        Assert.Equal(7, SessionSkip.LastSession(TestGame.Data.Lessons));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkippedTo(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => SkippedTo(8));

        var played = TestGame.NewSessionWithShippedLessons();
        played.Step([]);
        Assert.Throws<InvalidOperationException>(() => SessionSkip.Apply(played, 4));
        var twice = SkippedTo(3);
        Assert.Throws<InvalidOperationException>(() => SessionSkip.Apply(twice, 4));
    }
}
