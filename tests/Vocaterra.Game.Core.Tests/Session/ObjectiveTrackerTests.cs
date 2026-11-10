using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Session;

/// <summary>The objective tracker (U01): one line per objective, three at most, the most recently advanced first, the rest as "+n".</summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class ObjectiveTrackerTests
{
    // Play session 7 on the shipped bundle with no lessons (a lesson would take a line): the three orders of sessions 4 to 6 are taken on with «sí» to their NPCs' offers (issue #54).
    private static VoxelSession FourObjectives()
    {
        var data = TestGame.Data;
        var noLessons = LessonBook.Parse("{ \"schemaVersion\": 1, \"lessons\": [] }", data.Phrases, data.Speech, data.Bundle.Prompts, data.Locale, data.Npcs.Ids);
        var session = new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), lessons: noLessons) { Day = 7 };
        foreach (var npc in new[] { "fisher", "baker", "smith" })
        {
            _ = session.OpenTalk(npc);
            Assert.NotNull(session.AcceptOrder());
            session.EndTalk();
        }

        return session;
    }

    [Fact]
    public void FourObjectivesGiveThreeLinesAndAPlusOneInTheOrderOfTheirLastAdvance()
    {
        var session = FourObjectives();
        var tracker = session.ObjectiveTracker(lessonStripShown: false);

        Assert.Equal(3, tracker.Lines.Count);
        Assert.Equal(1, tracker.More);
        Assert.Equal("+1", tracker.MoreText);
        Assert.Equal(session.Locale.ObjectiveHudLine("meet-guide"), tracker.Lines[0].Pieces.Single().Text); // none has advanced: the session's own order

        session.Objectives.Single(static o => o.Id == "axe-handle").AdvancedAt = 50; // the order that moved last goes first
        var after = session.ObjectiveTracker(lessonStripShown: false);
        Assert.Equal(session.Locale.ObjectiveHudLine("axe-handle"), after.Lines[0].Pieces.Single().Text);
        Assert.Equal(3, after.Lines.Count);
    }

    [Fact]
    public void AnObjectiveThatAdvancesGoesToTheTop()
    {
        var session = FourObjectives();
        _ = session.OpenTalk("guide"); // meeting the guide completes the first objective
        session.EndTalk();
        Assert.True(session.Objectives.Single(static o => o.Id == "meet-guide").Complete);
        _ = session.Simulation.GrantItems(ItemId.Wood, 1);
        _ = session.HostEvent(WorldEvents.ItemPickedUp, "madera"); // the camp's wood moves: the camp line, a checklist, is first
        var tracker = session.ObjectiveTracker(lessonStripShown: false);
        Assert.StartsWith("Campamento", tracker.Lines[0].Pieces[0].Text, StringComparison.Ordinal);
        Assert.Equal(3, tracker.Lines.Count);
        Assert.Equal(1, tracker.More); // the camp and the three orders are four
    }

    [Fact]
    public void OneObjectiveIsOneLineAndNothingIsLeftOut()
    {
        var data = TestGame.Data;
        var session = new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route());
        var tracker = session.ObjectiveTracker(lessonStripShown: false);
        _ = Assert.Single(tracker.Lines);
        Assert.Equal((0, string.Empty), (tracker.More, tracker.MoreText));
        Assert.Equal(session.ObjectiveHud().Select(static p => p.Text), tracker.Lines[0].Pieces.Select(static p => p.Text)); // the same line the checklist always had
    }

    [Fact]
    public void TheSpeakTipComesUpAtTheFirstConversationOnly()
    {
        var session = new VoxelSession(TestGame.Data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route());
        var seen = 0;
        for (var conversation = 0; conversation < 3; conversation++)
        {
            session.Tips.Offer(new WorldEvent(0, 0, WorldEvents.TalkOpened, "guide", null));
            for (long tick = 0; tick < 5000; tick += 30)
            {
                session.Tips.Step((conversation * 5000) + tick);
                if (session.Tips.Current is { Id: "speak" } && tick % 30 == 0)
                {
                    seen++;
                    break;
                }
            }

            session.Tips.Step((conversation * 5000) + 4900); // the tip's time is over
            session.Tips.Step((conversation * 5000) + 4990);
        }

        Assert.True(session.Tips.WasShown("speak"));
        Assert.Equal(1, seen);
    }
}
