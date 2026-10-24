using Vocaterra.Game.Language.Lessons;
using Vocaterra.Game.Language.Words;
using Vocaterra.Game.Save;

namespace Vocaterra.Game.Tests;

/// <summary>
/// The first-time tips of S16 (D-game-13): English text from the game's content, shown once each, one at a time for seven seconds of game time, when the start,
/// the first pickup, the first aim at an NPC or the first talk comes; saved with the game, and the same on every run (they count the game's ticks).
/// </summary>
public sealed class TipFeedTests
{
    private static void Run(VoxelSession session, long ticks)
    {
        for (long i = 0; i < ticks; i++)
        {
            session.Step([]);
        }
    }

    private static List<string> Shown(VoxelSession session) =>
        session.Events.OfType<WorldEvent>().Where(static e => e.Name == WorldEvents.TipShown).Select(static e => e.Detail!).ToList();

    // The opening tips (S17) end with "gather" at 50 s of game time (3,000 ticks), which is up for seven seconds.
    private const long OpeningTicks = (50 * 60) + TipFeed.ShowTicks + 2;

    [Fact]
    public void TheOpeningTipsTeachLookingWalkingJumpingTheNoteAndGatheringOneAtATimeWithGapsBetween()
    {
        var session = TestGame.NewSession();
        Assert.Null(session.Tips.Current);

        session.Step([]);
        Assert.Equal("look", session.Tips.Current!.Id);
        Assert.Equal("Move the mouse to look around. The valley below is where you are headed.", session.Tips.Current.Text);
        Run(session, TipFeed.ShowTicks - 1);
        Assert.Equal("look", session.Tips.Current!.Id); // still up
        Run(session, 1);
        Assert.Null(session.Tips.Current); // down, and the next tip waits for its delay: they do not stack up
        Run(session, (9 * 60) - TipFeed.ShowTicks - 2);
        Assert.Null(session.Tips.Current);
        Run(session, 2);
        Assert.Equal("walk", session.Tips.Current!.Id);

        // The whole opening, tick by tick: each tip at its delay, seven seconds up, a gap of at least a second between two.
        var fresh = TestGame.NewSession();
        var cameUp = new List<(string Id, long Tick)>();
        string? previous = null;
        for (long tick = 1; tick <= OpeningTicks + 600; tick++)
        {
            fresh.Step([]);
            if (fresh.Tips.Current is { } tip && !string.Equals(tip.Id, previous, StringComparison.Ordinal))
            {
                cameUp.Add((tip.Id, tick));
            }

            previous = fresh.Tips.Current?.Id;
        }

        Assert.Equal(["look", "walk", "jump", "note", "gather"], cameUp.Select(static c => c.Id));
        Assert.Equal([1L, 9 * 60, 18 * 60, 28 * 60, 50 * 60], cameUp.Select(static c => c.Tick)); // the file's delays: 0, 9, 18, 28 and 50 seconds
        for (var i = 1; i < cameUp.Count; i++)
        {
            Assert.True(cameUp[i].Tick - cameUp[i - 1].Tick >= TipFeed.ShowTicks + 60, $"{cameUp[i].Id} came up too soon after {cameUp[i - 1].Id}");
        }

        Assert.Null(fresh.Tips.Current); // the opening is over
        Assert.Equal(["look", "walk", "jump", "note", "gather"], Shown(fresh));
        Run(fresh, TipFeed.ShowTicks * 3);
        Assert.Equal(["look", "walk", "jump", "note", "gather"], Shown(fresh)); // and never again
        Assert.Equal(["gather", "jump", "look", "note", "walk"], fresh.Tips.Shown);
    }

    [Fact]
    public void TheNoteSaysTheGuidesCampIsDownTheTrailInEnglishWithOneSpanishWord()
    {
        var note = Assert.Single(TestGame.NewSession().English.Tips, static t => t.Id == "note");

        Assert.Contains("down the trail", note.Text, StringComparison.Ordinal);
        Assert.Contains("camino", note.Text, StringComparison.Ordinal); // the one Spanish word, glossed in brackets
        Assert.Contains("(the trail)", note.Text, StringComparison.Ordinal);
        Assert.Equal("start", note.When);
    }

    [Fact]
    public void ATipWaitsForItsTriggerAndIsShownOnceHoweverOftenTheTriggerRepeats()
    {
        var session = TestGame.NewSession();
        Run(session, OpeningTicks); // the start tips are done

        Assert.False(session.Tips.WasShown("pickup"));
        session.LogEvent(new WorldEvent(session.CurrentTick, session.StoredTime.UnixSeconds, WorldEvents.ItemPickedUp, null, "madera"));
        session.LogEvent(new WorldEvent(session.CurrentTick, session.StoredTime.UnixSeconds, WorldEvents.ItemPickedUp, null, "madera"));
        session.Step([]);

        Assert.Equal("pickup", session.Tips.Current!.Id);
        Run(session, TipFeed.ShowTicks + 1);
        Assert.Null(session.Tips.Current); // the second pickup queued nothing
        session.LogEvent(new WorldEvent(session.CurrentTick, session.StoredTime.UnixSeconds, WorldEvents.ItemPickedUp, null, "fruta"));
        Run(session, 5);
        Assert.Null(session.Tips.Current);
        Assert.Equal(1, Shown(session).Count(static id => id == "pickup"));
    }

    [Fact]
    public void TheTalkTipComesWhenThePanelOpensAndTheAimedTipWhenTheFrontEndOffersIt()
    {
        var session = TestGame.NewSession();
        Run(session, OpeningTicks);
        session.Tips.Offer("aimed_npc");
        session.Step([]);
        Assert.Equal("npc", session.Tips.Current!.Id);
        Run(session, TipFeed.ShowTicks);

        Stage.TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        session.Step([]);

        Assert.Equal("speak", session.Tips.Current!.Id);
        Assert.Contains("Press H for help any time", session.Tips.Current.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void DismissTakesTheTipDownAndTheNextOneComesUp()
    {
        var session = TestGame.NewSession();
        session.Step([]);
        Assert.Equal("look", session.Tips.Current!.Id);

        session.Tips.Dismiss();
        Assert.Null(session.Tips.Current);
        session.Step([]);

        Assert.Null(session.Tips.Current); // the next tip waits for its delay (nine seconds from the start)
        Run(session, (9 * 60) - 2);
        Assert.Equal("walk", session.Tips.Current!.Id);
    }

    [Fact]
    public void TheTipsShownAndWaitingAndTheOneOnScreenAreSavedAndALoadedGameContinuesAsTheOriginalWould()
    {
        var session = TestGame.NewSessionWithShippedLessons(); // (a load builds its session with the bundle's lessons)
        Run(session, (9 * 60) + 30); // "walk" is up; "jump", "note" and "gather" wait for their delays
        session.Tips.Offer("aimed_npc");
        var saved = SaveGame.Save(session, TestGame.Route());

        var loaded = SaveGame.Load(saved, TestGame.Bundle, TestGame.Route, TestGame.Nouns, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.Equal(saved, SaveGame.Save(loaded, TestGame.Route()));
        Assert.Equal(["look", "walk"], loaded.Tips.Shown.Where(static id => id is "look" or "walk"));
        Assert.Equal("walk", loaded.Tips.Current!.Id);
        Run(session, TipFeed.ShowTicks * 3);
        Run(loaded, TipFeed.ShowTicks * 3);
        Assert.Equal(session.Tips.Shown, loaded.Tips.Shown);
        Assert.Equal(session.Tips.Current?.Id, loaded.Tips.Current?.Id);
        Assert.Equal(SaveGame.Save(session, TestGame.Route()), SaveGame.Save(loaded, TestGame.Route()));
    }

    [Fact]
    public void ASaveWithATipTheGameDoesNotHaveIsRefused()
    {
        var session = TestGame.NewSession();
        var doc = SaveGame.Parse(SaveGame.Save(session, TestGame.Route()));
        var bad = doc with { Tips = new TipsSave(["no-such-tip"], [], null, 0) };

        Assert.Throws<SaveException>(() => SaveGame.Restore(bad, TestGame.Bundle, TestGame.Route(), TestGame.Nouns));
    }

    [Fact]
    public void ASaveFromBeforeTheTipsAndLessonsExistedStillLoads()
    {
        var session = TestGame.NewSession();
        var doc = SaveGame.Parse(SaveGame.Save(session, TestGame.Route())) with { Lessons = null, Tips = null };

        var loaded = SaveGame.Restore(doc, TestGame.Bundle, TestGame.Route(), TestGame.Nouns, CreditRules.FullTyped, bumpSession: false);

        Assert.Equal(LessonStatus.NotStarted, loaded.Lessons.StatusOf("sample"));
        Assert.Empty(loaded.Tips.Shown);
    }

    [Fact]
    public void TheSameTicksGiveTheSameTips()
    {
        static IReadOnlyList<string> Go()
        {
            var session = TestGame.NewSession();
            Run(session, TipFeed.ShowTicks * 4);
            return session.Events.Select(static e => $"{e.Tick}:{e.Text}").ToList();
        }

        Assert.Equal(Go(), Go());
    }
}
