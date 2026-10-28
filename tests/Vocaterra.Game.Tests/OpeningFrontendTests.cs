using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Startup;
using Vocaterra.Game.UI.Dialogue;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Tests;

/// <summary>
/// The front-end side of S17 (D-game-15): the guide who greets the player who walks up to her, the closing moment of the session, the English on the speech buttons
/// and on the hold-to-talk hint (rule 8: the buttons work the same with or without it), the launch flag of the lesson run, and the panel that shows nothing of the
/// guide's ordinary reply for a turn the lesson took. No window is opened.
/// </summary>
public sealed class OpeningFrontendTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));

    private static VoxelSession Session() => RouteScene.NewSessionWithShippedLessons(Bundle.Value, Nouns.Value, CreditRules.FullTyped);

    private static float Measure(string text) => text.Length * 8f;

    private static void TeleportToNpc(VoxelSession session, string npc) =>
        session.Simulation.TeleportTo(NpcStandOff.Choose(session.Scene.Grid, session.Scene.Npc(npc).Feet, session.Simulation.Player.FeetBlock)!.Value);

    // A cell on the flat ground of the guide's camp this many blocks east of her.
    private static void TeleportNear(VoxelSession session, int blocks)
    {
        var feet = session.Scene.Npc("guide").Feet;
        session.Simulation.TeleportTo(new BlockPos(feet.X + blocks, feet.Y, feet.Z));
    }

    private static void CompleteTheChecklist(VoxelSession session)
    {
        _ = session.Objective.RecordPickup(ItemId.Wood, 5);
        _ = session.Objective.RecordPickup(ItemId.Berry, 3);
        _ = session.Objective.RecordTorchPlaced(atFireRing: true);
        _ = session.HostEvent(Objective.WaterEvent);
    }

    private static void LightThePlayersFire(VoxelSession session)
    {
        var ring = session.Scene.Point("firering.player");
        var stone = session.Scene.Grid.BlocksOfKind(BlockId.FireRing).First(p => Math.Abs(p.X - ring.X) <= 1 && Math.Abs(p.Z - ring.Z) <= 1);
        var rim = new BlockPos(stone.X, stone.Y + 1, stone.Z);
        session.Simulation.TeleportTo(new BlockPos(rim.X, rim.Y - 1, rim.Z + 3));
        Assert.True(session.Simulation.Inventory.Count(ItemId.Torch) > 0); // the spare torch the complete checklist gave
        session.Step([VoxelCommand.Look(0, 0), VoxelCommand.Place(ItemId.Torch, rim.X, rim.Y, rim.Z)]);
    }

    // ---- The guide greets the player who walks up to her.

    [Fact]
    public void TheGuideGreetsThePlayerWhoWalksUpToHerOnceAndGreetsAgainAfterTheyHaveGoneAway()
    {
        var session = Session();
        var armed = true;
        Assert.False(GuideApproach.ShouldGreet(session, ref armed, free: true)); // the player's camp is far from hers
        Assert.True(armed);

        TeleportNear(session, GuideApproach.Blocks - 1);
        Assert.True(GuideApproach.Reached(session));
        Assert.False(GuideApproach.ShouldGreet(session, ref armed, free: false)); // something else has the player's attention: she waits
        Assert.True(armed);
        Assert.True(GuideApproach.ShouldGreet(session, ref armed, free: true));
        Assert.False(armed);
        Assert.False(GuideApproach.ShouldGreet(session, ref armed, free: true)); // once per approach

        session.Simulation.TeleportTo(session.Scene.Spawn.Feet); // far away: armed again
        Assert.False(GuideApproach.ShouldGreet(session, ref armed, free: true));
        Assert.True(armed);
        TeleportNear(session, 2);
        Assert.True(GuideApproach.ShouldGreet(session, ref armed, free: true));
    }

    [Fact]
    public void TheGuideDoesNotGreetWhenHerLessonIsDoneAndTheCampIsNotYetComplete()
    {
        var session = Session();
        TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        while (session.Lessons.Active is not null)
        {
            _ = session.Lessons.Skip();
        }

        session.EndTalk();
        var armed = true;

        Assert.False(GuideApproach.LessonWaiting(session));
        Assert.False(GuideApproach.TransferWaiting(session));
        Assert.False(GuideApproach.ShouldGreet(session, ref armed, free: true));
    }

    [Fact]
    public void TheGuideGreetsWithHerTransferOfferOnceTheCampIsDoneAndOnlyUntilItHasBeenMade()
    {
        var session = Session();
        TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        while (session.Lessons.Active is not null)
        {
            _ = session.Lessons.Skip(); // the lesson has the conversation first
        }

        session.EndTalk();
        CompleteTheChecklist(session);
        var armed = true;

        Assert.True(GuideApproach.TransferWaiting(session));
        Assert.True(GuideApproach.ShouldGreet(session, ref armed, free: true));

        _ = session.OpenTalk("guide");
        Assert.True(PlaytestEnd.TransferOffered(session.Events));
        Assert.False(GuideApproach.TransferWaiting(session));
    }

    // ---- The closing moment.

    [Fact]
    public void TheClosingMomentBeginsOnceWhenTheSessionIsCompleteStaysTwelveSecondsAndEscTakesItDown()
    {
        var session = Session();
        var ending = new SessionEnding();
        Assert.False(ending.Update(session, 100));
        Assert.False(ending.Visible(100));

        CompleteTheChecklist(session);
        Assert.False(ending.Update(session, 200)); // the checklist is done but the fire is not lit

        LightThePlayersFire(session);
        Assert.True(session.SessionComplete);

        Assert.True(ending.Update(session, 300));
        Assert.False(ending.Update(session, 301)); // once
        Assert.Equal("¡Muy bien! Ya tienes fuego.", ending.Spanish);
        Assert.Equal("Very good! Now you have a fire.", ending.English);
        Assert.True(ending.Visible(300 + SessionEnding.ShowTicks - 1));
        Assert.False(ending.Visible(300 + SessionEnding.ShowTicks));
        var dismissed = new SessionEnding();
        Assert.True(dismissed.Update(session, 10));
        dismissed.Dismiss();
        Assert.False(dismissed.Visible(11));
    }

    [Fact]
    public void AGameLoadedAfterItsEndingDoesNotPlayTheEndingAgain()
    {
        var session = Session();
        CompleteTheChecklist(session);
        LightThePlayersFire(session);
        Assert.True(session.SessionComplete);

        var seen = new SessionEnding();
        seen.MarkSeen(session);

        Assert.True(seen.Begun);
        Assert.False(seen.Update(session, 500));
        Assert.False(seen.Visible(500));
    }

    // ---- The English on the speech buttons.

    [Fact]
    public void TheSpeechButtonsAndTheHoldHintShowTheirEnglishWhileTheLearnerIsEarly()
    {
        var session = Session();

        Assert.Equal(
            ["[R] Repetir (repeat)", "[S] Más despacio (slower)", "[H] Ayuda (help)", "[N] No sé (I don't know)", "[W] Escribir (type)"],
            SpeechButtons.All.Select(button => SpeechButtons.TextFor(button, session)));
        Assert.Equal(["[R] Repetir", "[S] Más despacio", "[H] Ayuda", "[N] No sé", "[W] Escribir"], SpeechButtons.All.Select(button => SpeechButtons.TextFor(button, null))); // no game: Spanish alone
    }

    [Fact]
    public void AButtonLosesItsEnglishAfterThreeUsesOfItAndTheOthersKeepTheirs()
    {
        var session = Session();
        TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");

        session.LogVoice(VoiceEventKind.Replay);
        session.LogVoice(VoiceEventKind.Replay);
        Assert.Equal("[R] Repetir (repeat)", SpeechButtons.TextFor(SpeechButtons.All[0], session));
        session.LogVoice(VoiceEventKind.Replay);

        Assert.Equal("[R] Repetir", SpeechButtons.TextFor(SpeechButtons.All[0], session));
        Assert.Equal("[S] Más despacio (slower)", SpeechButtons.TextFor(SpeechButtons.All[1], session));
        Assert.True(session.ButtonEnglishUp(SpeechButtonKeys.Hold));
    }

    [Fact]
    public void TypedLinesCountTowardsTheTypeButtonAndNothingSpokenKeepsTheMicrophonesHintInEnglish()
    {
        var session = Session();
        TeleportToNpc(session, "guide");
        _ = session.OpenTalk("guide");
        while (session.Lessons.Active is not null)
        {
            _ = session.Lessons.Skip();
        }

        _ = session.Say("¿dónde está el árbol?");
        _ = session.Say("¿dónde está la piedra?");
        Assert.True(session.ButtonEnglishUp(SpeechButtonKeys.Type)); // two typed lines
        _ = session.Say("¿qué es esto?");

        Assert.False(session.ButtonEnglishUp(SpeechButtonKeys.Type));
        Assert.True(session.ButtonEnglishUp(SpeechButtonKeys.Hold)); // nothing was spoken
    }

    [Fact]
    public void TheEnglishOnTheButtonsGatesNothingEveryButtonIsInTheLayoutAndInsideThePanelWithAndWithoutIt()
    {
        var session = Session();
        foreach (var width in new[] { 1280, 800 })
        {
            var withEnglish = new DialogueLayout(width, 720, Measure, [], typing: false, button => SpeechButtons.TextFor(button, session));
            var without = new DialogueLayout(width, 720, Measure, [], typing: false);

            Assert.Equal(without.Buttons.Select(static b => b.Action), withEnglish.Buttons.Select(static b => b.Action)); // the same five actions
            Assert.All(withEnglish.Buttons, b =>
            {
                Assert.True(b.Box.X >= withEnglish.Panel.X && b.Box.Right <= withEnglish.Panel.Right, $"{b.Action} is off the panel at {width}");
                Assert.Equal(b.Action, withEnglish.ActionAt(b.Box.X + 1, b.Box.Y + 1)); // a click on it presses it
            });
        }
    }

    [Fact]
    public void ARowOfButtonsTooWideForThePanelWrapsToASecondRowAboveInsteadOfRunningOffIt()
    {
        var wide = new DialogueLayout(800, 720, static t => t.Length * 14f, [], typing: false, static b => b.Text + " (a long English gloss)");

        Assert.True(wide.Buttons.Select(static b => b.Box.Y).Distinct().Count() >= 2);
        Assert.All(wide.Buttons, b => Assert.True(b.Box.Right <= wide.Panel.Right));
        Assert.True(wide.HeardY < wide.Buttons.Min(static b => b.Box.Y)); // the heard line and the conversation stay above the buttons
    }

    // ---- The panel shows nothing of the guide's ordinary reply for a turn the lesson took.

    [Fact]
    public void ATurnTheLessonTookAddsNoGuideReplyToThePanelAndAnOrdinaryTurnDoes()
    {
        var session = Session();
        TeleportToNpc(session, "guide");
        var dialogue = new DialogueModel(new InputFocus(), session.OpeningLine, session.Phrases, session.English);
        dialogue.Open(session.OpenTalk("guide"), "Guía");
        _ = session.Lessons.Continue();
        _ = session.Lessons.Continue();
        Assert.Equal("u1-say-hola", session.Lessons.Current!.Id);
        var before = dialogue.Lines.Count(static l => l.Speaker == DialogueSpeaker.Guide);
        _ = dialogue.Entry.Insert("hola");

        var turn = dialogue.Submit(line => session.Say(line))!;

        Assert.True(turn.LessonHandled);
        Assert.NotEmpty(turn.Replies);
        Assert.Equal(before, dialogue.Lines.Count(static l => l.Speaker == DialogueSpeaker.Guide)); // her reply is not on the panel

        var ordinary = RouteScene.NewSession(Bundle.Value, Nouns.Value, CreditRules.FullTyped); // no lesson: the same words get her reply
        TeleportToNpc(ordinary, "guide");
        var panel = new DialogueModel(new InputFocus(), ordinary.OpeningLine, ordinary.Phrases, ordinary.English);
        panel.Open(ordinary.OpenTalk("guide"), "Guía");
        var ordinaryBefore = panel.Lines.Count(static l => l.Speaker == DialogueSpeaker.Guide);
        _ = panel.Entry.Insert("hola");
        _ = panel.Submit(line => ordinary.Say(line));
        Assert.True(panel.Lines.Count(static l => l.Speaker == DialogueSpeaker.Guide) > ordinaryBefore);
    }

    // ---- The launch flag of the lesson run.

    [Fact]
    public void TheLessonRunFlagIsAScriptStepAndNeedsALessonId()
    {
        Assert.True(LaunchOptions.TryParse(["--seed", "42", "--lesson-run", "first-words", "--screenshot", "/tmp/x.png", "--frames", "5"], out var options, out var error), error);

        var step = Assert.Single(options.Script);
        Assert.Equal((ScriptStepKind.LessonRun, "first-words"), (step.Kind, step.Value));
        Assert.False(LaunchOptions.TryParse(["--lesson-run", "  ", "--screenshot", "/tmp/x.png"], out _, out var missing));
        Assert.Contains("--lesson-run needs the id of a lesson", missing, StringComparison.Ordinal);
        Assert.Contains("--lesson-run", LaunchOptions.Usage, StringComparison.Ordinal);
    }

    // ---- The recap ends on the journal.

    [Fact]
    public void WhenTheRecapEndsTheLessonFrontendAsksTheGameToOpenTheJournalOnThePhrasesPage()
    {
        var session = Session();
        TeleportToNpc(session, "guide");
        var dialogue = new DialogueModel(new InputFocus(), session.OpeningLine, session.Phrases, session.English);
        dialogue.Open(session.OpenTalk("guide"), "Guía");
        var opened = 0;
        var lesson = new LessonFrontend(session, dialogue, static _ => { }) { OnRecapDone = () => opened++ };
        while (session.Lessons.Current is { Type: not LessonStepType.Recap })
        {
            _ = session.Lessons.Skip();
        }

        Assert.Equal(0, opened);
        Assert.True(lesson.Perform(LessonAction.Continue));

        Assert.Equal(1, opened);
        Assert.Equal(LessonStatus.Completed, session.Lessons.StatusOf("first-words"));
    }
}
