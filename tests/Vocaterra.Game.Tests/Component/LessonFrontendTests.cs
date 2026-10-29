using System.Text.Json;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Book;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Startup;
using Vocaterra.Game.Tests.TestSupport;
using Vocaterra.Game.UI.Dialogue;
using Vocaterra.Game.UI.Lesson;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Tests.Component;

/// <summary>
/// The front-end side of S16 (D-game-13): the lesson strip's layout and keys, the lesson steps of the scripted run (which prints each step and its outcome), the
/// help and "no sé" buttons in a lesson, the English feedback after a turn on the panel, the new launch options, the lesson lines of the session log, and the
/// font's coverage of every word of the English. No window is opened: the layers only draw what these classes lay out.
/// </summary>
public sealed class LessonFrontendTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());

    private static readonly Lazy<NounCatalog> Nouns = new(() => NounCatalog.Load(Bundle.Value));

    private static readonly DateTimeOffset Stamp = DateTimeOffset.Parse("2026-10-22T09:30:00+01:00", System.Globalization.CultureInfo.InvariantCulture);

    private sealed class RecordingPlayer : ILinePlayer
    {
        public List<(string Npc, string Line, bool Slow)> Played { get; } = [];

        public void Play(string npc, string line, bool slow) => Played.Add((npc, line, slow));
    }

    // The scripted run's loop with the lesson front-end, as the game builds it (the panel opens on the lesson's NPC; the lesson steps press the strip's buttons).
    private sealed class Harness : IDisposable
    {
        private readonly List<VoxelCommand> _commands = [];
        private readonly TurnPump _pump;
        private readonly SupportFrontend _support;

        public Harness(IReadOnlyList<ScriptStep> steps, LessonBook? lessons = null)
        {
            Session = new VoxelSession(RouteScene.DataOf(Bundle.Value), 42, GuideSession.DefaultStartedAt, RouteScene.Build(), CreditRules.FullTyped, lessons ?? RouteScene.Sample(Bundle.Value, Nouns.Value));
            Session.StartWorker(loadSpeech: false).WaitUntilReady(30000);
            Look = new LookControl();
            Look.Set(Session.Scene.Spawn.Yaw, 0);
            Dialogue = new DialogueModel(new InputFocus(), Session.OpeningLine, Session.Phrases, Session.Locale);
            _pump = new TurnPump(Session, Dialogue, () => Session.AimedCell);
            var actions = new SpeechActionRunner(Session, Dialogue, Player, () => Session.AimedCell);
            _support = new SupportFrontend(Session, Dialogue, actions, Log.Add);
            Lesson = new LessonFrontend(Session, Dialogue, Log.Add);
            var hooks = new ScenarioHooks(OpenTalk, CloseTalk, path => Shots.Add(path), () => false, RunLessonStep, BookPractise: id => Assert.True(Session.Lessons.RequestReplay(id)));
            Script = new ScenarioScript(Session, Look, Dialogue, steps, static _ => null, hooks, Log.Add);
        }

        public VoxelSession Session { get; }

        public LookControl Look { get; }

        public DialogueModel Dialogue { get; }

        public ScenarioScript Script { get; }

        public LessonFrontend Lesson { get; }

        public RecordingPlayer Player { get; } = new();

        public List<string> Log { get; } = [];

        public List<string> Shots { get; } = [];

        public void Go(int limit = 20000)
        {
            var printed = 0;
            for (var tick = 0; tick < limit && !Script.IsDone; tick++)
            {
                _commands.Clear();
                Script.Fill(_commands);
                _commands.Insert(0, VoxelCommand.Look(Look.Yaw, Look.Pitch));
                Session.Step(_commands);
                _ = _support.Pump();
                Lesson.Pump();
                _pump.Submit();
                var deadline = Environment.TickCount64 + 30000;
                while (Session.PendingTurns > 0 && Environment.TickCount64 < deadline)
                {
                    _ = _pump.Apply();
                    Thread.Sleep(1);
                }

                _ = _pump.Apply();
                for (; printed < Session.Events.Count; printed++)
                {
                    Log.Add($"  event: {Session.Events[printed].Text}"); // the game prints each event as one console line
                }
            }

            Assert.True(Script.IsDone, "the script did not finish");
        }

        public void Dispose() => Session.StopWorker();

        private void RunLessonStep(ScriptStepKind kind, string value)
        {
            switch (kind)
            {
                case ScriptStepKind.LessonStart:
                    Assert.True(Session.Lessons.Start(value));
                    Lesson.Pump();
                    break;
                case ScriptStepKind.LessonContinue:
                    _ = Lesson.Perform(LessonAction.Continue);
                    break;
                case ScriptStepKind.LessonSkip:
                    _ = Lesson.Perform(LessonAction.Skip);
                    break;
                case ScriptStepKind.LessonHelp:
                    _ = Lesson.Perform(LessonAction.Help);
                    break;
                default:
                    _ = Lesson.Perform(LessonAction.Option1 + (int.Parse(value, System.Globalization.CultureInfo.InvariantCulture) - 1));
                    break;
            }
        }

        private void OpenTalk(string npc)
        {
            var opening = Session.OpenTalk(npc, teachKeyWords: true);
            Dialogue.Open(opening, NpcNames.LabelOf(npc), Session.ReviewLine);
        }

        private void CloseTalk()
        {
            Dialogue.Close();
            Session.EndTalk();
        }
    }

    private static LaunchOptions Parse(params string[] args)
    {
        Assert.True(LaunchOptions.TryParse(args, out var options, out var error), error);
        return options;
    }

    private static LocaleText English => RouteScene.DataOf(Bundle.Value).Locale;

    private static float Measure(string text) => text.Length * 8f;

    private static LessonView Step(LessonStepType type, string english = "Say hello.", string? spanish = "Hola.", string? hint = null, bool canContinue = false, IReadOnlyList<string>? options = null) =>
        new("sample", "A first hello", 1, 2, type, "Say it", english, spanish, null, hint, options ?? [], -1, canContinue, type is LessonStepType.Repeat or LessonStepType.Answer, false, []);

    // ---- The scripted run.

    [Fact]
    public void AScriptedRunGoesThroughTheSampleLessonAndPrintsEachStepAndItsOutcome()
    {
        using var run = new Harness([new(ScriptStepKind.LessonStart, Value: "sample"), new(ScriptStepKind.Say, Value: "hola"), new(ScriptStepKind.Say, Value: "gracias")]);

        run.Go();

        var lessonLines = run.Log.Where(static l => l.StartsWith("  event: lesson", StringComparison.Ordinal)).ToList();
        Assert.Equal(
            [
                "  event: lesson sample started",
                "  event: lesson sample step 1 repeat (say-hola) step_started (hola)",
                "  event: lesson sample step 1 repeat (say-hola) done",
                "  event: lesson sample step 2 answer (say-gracias) step_started (gracias)",
                "  event: lesson sample step 2 answer (say-gracias) done",
                "  event: lesson sample completed (done 2, skipped 0, moved on 0)",
            ],
            lessonLines);
        Assert.Equal(LessonStatus.Completed, run.Session.Lessons.StatusOf("sample"));
        Assert.Equal(["hola", "gracias"], run.Session.Events.OfType<PhraseUse>().Select(static u => u.PhraseId));
        Assert.Equal([PhraseUseKind.Supported, PhraseUseKind.Unaided], run.Session.Events.OfType<PhraseUse>().Select(static u => u.Kind));
        Assert.Contains(("guide", "Hola.", false), run.Player.Played); // the repeat step's card plays the model WAV
        Assert.Contains(("guide", "¡Claro! Aquí tienes.", false), run.Player.Played); // the answer step's line plays too
        Assert.Contains(run.Dialogue.Lines, static l => l is { Speaker: DialogueSpeaker.Guide, Text: "¡Claro! Aquí tienes." }); // and is on the panel, glossed in English
        Assert.Equal("Of course! Here you are.", run.Session.SupportForNpcLine("¡Claro! Aquí tienes."));
        Assert.Contains(run.Dialogue.Lines, static l => l is { Speaker: DialogueSpeaker.English, Text: "Good. The guide understood you." });
    }

    [Fact]
    public void ALessonRunStepDoesTheWholeFirstWordsLessonWithTypedAnswersAndWalksToPickTheFruit()
    {
        var shipped = RouteScene.DataOf(Bundle.Value).Lessons;
        using var run = new Harness([new(ScriptStepKind.LessonRun, Value: "first-words")], shipped);

        run.Go(60000);

        var done = run.Log.Where(static l => l.StartsWith("  event: lesson first-words step ", StringComparison.Ordinal) && (l.EndsWith(" done", StringComparison.Ordinal) || l.Contains(" done (", StringComparison.Ordinal))).ToList();
        Assert.Equal(37, done.Count); // every step, its outcome printed by the event line
        Assert.Contains("  event: lesson first-words step 21 do (u4-pick) done (fruta)", run.Log); // the fruit was picked up at the bush
        Assert.Contains("  event: lesson first-words completed (done 37, skipped 0, moved on 0)", run.Log);
        Assert.Equal(LessonStatus.Completed, run.Session.Lessons.StatusOf("first-words"));
        Assert.Equal(1, run.Session.Objective.Step(Objective.BerryKey)!.Count); // the lesson's fruit is a real one: the checklist moved
        Assert.True(run.Session.GuideMet);
        Assert.Equal(["hola", "si", "no", "que-es-esto", "que-es-esto", "que-es-esto", "si-por-favor", "gracias", "quiero-madera", "tengo-fruta", "necesito-agua", "no-se", "otra-vez", "mas-despacio"],
            run.Session.Events.OfType<PhraseUse>().Select(static u => u.PhraseId));
    }

    [Fact]
    public void ALessonRunStepDoesPersonasFromTheVillagerToTheStallAndTheShopkeeper()
    {
        var shipped = RouteScene.DataOf(Bundle.Value).Lessons;
        using var run = new Harness([new(ScriptStepKind.LessonRun, Value: "personas")], shipped);
        run.Session.PlaySession = 2; // the lesson of the second session

        run.Go(120000);

        var done = run.Log.Where(static l => l.StartsWith("  event: lesson personas step ", StringComparison.Ordinal) && (l.EndsWith(" done", StringComparison.Ordinal) || l.Contains(" done (", StringComparison.Ordinal))).ToList();
        Assert.Equal(20, done.Count);
        Assert.Contains("  event: lesson personas completed (done 20, skipped 0, moved on 0)", run.Log);
        Assert.Equal(LessonStatus.Completed, run.Session.Lessons.StatusOf("personas"));
        Assert.Equal(["quiero-madera", "ella-quiere-fruta", "necesitamos-agua", "necesito-pan", "tengo-botella", "queremos-pan"], run.Session.Events.OfType<PhraseUse>().Select(static u => u.PhraseId));
        var opened = run.Session.Events.OfType<WorldEvent>().Where(static e => e.Name == WorldEvents.TalkOpened).Select(static e => e.Npc).ToList();
        Assert.Equal("villager", opened[0]);
        Assert.Contains("shopkeeper", opened); // the run walked from the villager to the stall
        Assert.Equal(0, run.Session.Simulation.Inventory.Count(ItemId.Bottle)); // the stall handed nothing over: the statements are no requests
    }

    [Fact]
    public void APractiseAgainStepStartsACompletedLessonAtItsNpcAndTheRunRunsItAgain()
    {
        var shipped = RouteScene.DataOf(Bundle.Value).Lessons;
        using var run = new Harness(
        [
            new(ScriptStepKind.LessonRun, Value: "first-words"),
            new(ScriptStepKind.BookPractise, Value: "first-words"),
            new(ScriptStepKind.LessonRun, Value: "first-words"),
        ], shipped);

        run.Go(160000);

        Assert.Equal(2, run.Log.Count(static l => l == "  event: lesson first-words started"));
        Assert.Equal(2, run.Log.Count(static l => l.StartsWith("  event: lesson first-words completed", StringComparison.Ordinal)));
        Assert.Contains(run.Session.Events.OfType<LessonEvent>(), static e => e.Kind == LessonEventKind.ReplayRequested);
        Assert.Null(run.Session.Lessons.ReplayPending);
        Assert.True(run.Session.Lessons.IsDone("first-words"));
    }

    [Fact]
    public void TheLessonStepsOfTheScriptSkipHelpPickAndContinueLikeTheButtons()
    {
        var lessons = RouteScene.Lessons(LessonBookTestsRich, Bundle.Value);
        using var run = new Harness(
        [
            new(ScriptStepKind.LessonStart, Value: "rich"),
            new(ScriptStepKind.LessonContinue),
            new(ScriptStepKind.LessonContinue),
            new(ScriptStepKind.LessonPick, Value: "1"),
            new(ScriptStepKind.LessonPick, Value: "2"),
            new(ScriptStepKind.LessonContinue),
            new(ScriptStepKind.LessonHelp),
            new(ScriptStepKind.LessonSkip),
            new(ScriptStepKind.LessonSkip),
            new(ScriptStepKind.LessonSkip),
            new(ScriptStepKind.LessonContinue),
        ],
            lessons);

        run.Go();

        var done = run.Session.Events.OfType<LessonEvent>().Where(static e => e.Kind == LessonEventKind.StepDone).Select(static e => (e.StepId, e.Outcome)).ToList();
        Assert.Equal(
            [("read", StepOutcome.Done), ("hear", StepOutcome.Done), ("pick", StepOutcome.Done), ("say", StepOutcome.Skipped), ("reply", StepOutcome.Skipped), ("get", StepOutcome.Skipped), ("sum", StepOutcome.Done)],
            done);
        Assert.Contains(run.Session.Events.OfType<LessonEvent>(), static e => e.Kind == LessonEventKind.Help && e.StepId == "say");
        Assert.Contains(run.Session.Events.OfType<LessonEvent>(), static e => e.Kind == LessonEventKind.Retry && e.StepId == "pick"); // the wrong pick
    }

    private const string LessonBookTestsRich = """
        { "schemaVersion": 1, "lessons": [ { "id": "rich", "npc": "guide", "title": "Rich", "english": "Every step type.", "steps": [
          { "id": "read", "type": "explain", "english": "Read this." },
          { "id": "hear", "type": "model", "line": "phrase.hola", "translation": "Hello.", "english": "Listen." },
          { "id": "pick", "type": "listen", "line": "phrase.hola", "translation": "Hello.", "english": "What does she say?", "options": ["Goodbye", "Hello", "Thank you"], "correct": 1 },
          { "id": "say", "type": "repeat", "phrase": "hola", "prompt": "guide-open", "english": "Say it." },
          { "id": "reply", "type": "answer", "phrase": "gracias", "prompt": "guide-open", "line": "guide_offer_yes.1", "translation": "Of course! Here you are.", "english": "Thank her." },
          { "id": "get", "type": "do", "event": "item_picked_up", "detail": "fruta", "english": "Pick a fruit." },
          { "id": "sum", "type": "recap", "english": "Well done." } ] } ] }
        """;

    // ---- The strip.

    [Fact]
    public void TheStripStandsAboveTheLimitWithTheSpanishLargeAndTheEnglishUnderItMarkedAsEnglish()
    {
        var layout = LessonStripLayout.For(English, Step(LessonStepType.Repeat), null, 1280, 720, 284, Measure, Measure)!;

        Assert.True(layout.Box.Bottom <= 284 - LessonStripLayout.Gap);
        Assert.Equal(DialogueLayout.PanelBox(1280, 720).X, layout.Box.X);
        Assert.NotNull(layout.SpanishY);
        Assert.True(layout.SpanishY < layout.EnglishY); // the English is under the Spanish
        Assert.StartsWith(DialoguePanel.SupportPrefix, layout.EnglishRows[0], StringComparison.Ordinal);
        Assert.Contains("Say hello.", layout.EnglishRows[0], StringComparison.Ordinal);
        Assert.Empty(layout.HintRows);
        Assert.All(layout.Buttons, b => Assert.True(layout.Box.Contains(b.Box.X, b.Box.Y) && b.Box.Bottom <= layout.Box.Bottom));
        Assert.Equal([LessonAction.Help, LessonAction.Skip], layout.Buttons.Select(static b => b.Action)); // a step that waits on a turn has no Continue
        Assert.Equal(["[H] Help", "[K] Skip"], layout.Buttons.Select(static b => b.Text));
    }

    [Fact]
    public void AStepWithNoSpanishLineHasNoLargeLineAndAHintAddsRowsOfEnglish()
    {
        var plain = LessonStripLayout.For(English, Step(LessonStepType.Explain, "Read this.", null, canContinue: true), null, 1280, 720, 284, Measure, Measure)!;
        var hinted = LessonStripLayout.For(English, Step(LessonStepType.Explain, "Read this.", null, "Listen again.", true), null, 1280, 720, 284, Measure, Measure)!;

        Assert.Null(plain.SpanishY);
        Assert.Equal([LessonAction.Continue, LessonAction.Help, LessonAction.Skip], plain.Buttons.Select(static b => b.Action));
        Assert.Equal("English: Listen again.", Assert.Single(hinted.HintRows));
        Assert.True(hinted.Box.Height > plain.Box.Height);
    }

    [Fact]
    public void AListenStepHasAButtonPerOptionAndALongEnglishTextWrapsAndIsCapped()
    {
        var options = new[] { "Hello", "Goodbye", "Thank you" };
        var listen = LessonStripLayout.For(English, Step(LessonStepType.Listen, "What does she say?", "Hola.", options: options), null, 1280, 720, 284, Measure, Measure)!;
        var longText = string.Join(' ', Enumerable.Repeat("word", 80));
        var long_ = LessonStripLayout.For(English, Step(LessonStepType.Explain, longText, null, canContinue: true), null, 640, 480, 200, Measure, Measure)!;

        Assert.Equal([LessonAction.Option1, LessonAction.Option2, LessonAction.Option3, LessonAction.Help, LessonAction.Skip], listen.Buttons.Select(static b => b.Action));
        Assert.Equal("[2] Goodbye", listen.Buttons[1].Text);
        Assert.Equal(LessonAction.Option2, listen.ActionAt(listen.Buttons[1].Box.X + 2, listen.Buttons[1].Box.Y + 2));
        Assert.Null(listen.ActionAt(0, 0));
        Assert.Equal(LessonStripLayout.MaxEnglishRows, long_.EnglishRows.Count);
        Assert.True(long_.Box.Y >= 4);
    }

    [Fact]
    public void AnOfferedLessonShowsAOneButtonStripAndNothingShowsWhenThereIsNeither()
    {
        var offer = new LessonOffer("sample", "A first hello", "Say hello to the guide, then say thank you.", false);

        var layout = LessonStripLayout.For(English, null, offer, 1280, 720, 284, Measure, Measure)!;

        Assert.Equal([LessonAction.Start], layout.Buttons.Select(static b => b.Action));
        Assert.Equal("[L] Start the lesson", layout.Buttons[0].Text);
        Assert.Null(layout.SpanishY);
        Assert.Equal("English: Say hello to the guide, then say thank you.", layout.EnglishRows[0]);
        Assert.Equal("Lesson · A first hello", LessonStripLayout.Heading(English, offer));
        Assert.Equal("Lesson · A first hello · step 1 of 2 · Say it", LessonStripLayout.Heading(English, Step(LessonStepType.Repeat)));
        Assert.Null(LessonStripLayout.For(English, null, null, 1280, 720, 284, Measure, Measure));
    }

    [Fact]
    public void TheKeysOfTheStripMapToTheirActions()
    {
        Assert.Equal(LessonAction.Continue, LessonActions.ForKey(LessonKey.Enter));
        Assert.Equal(LessonAction.Skip, LessonActions.ForKey(LessonKey.K));
        Assert.Equal(LessonAction.Start, LessonActions.ForKey(LessonKey.L));
        Assert.Equal([0, 1, 2, 3], new[] { LessonKey.D1, LessonKey.D2, LessonKey.D3, LessonKey.D4 }.Select(static k => LessonActions.OptionIndex(LessonActions.ForKey(k)!.Value)!.Value));
        Assert.Null(LessonActions.OptionIndex(LessonAction.Skip));
        Assert.Equal(["L", "Enter", "K", "H", "1", "2", "3", "4", "M"], Enum.GetValues<LessonAction>().Select(LessonActions.KeyLabel));
    }

    // ---- The front-end.

    private static (VoxelSession Session, DialogueModel Dialogue, LessonFrontend Lesson, RecordingPlayer Player, SpeechActionRunner Actions) AtGuide(LessonBook? lessons = null)
    {
        var session = new VoxelSession(RouteScene.DataOf(Bundle.Value), 42, GuideSession.DefaultStartedAt, RouteScene.Build(), CreditRules.FullTyped, lessons ?? RouteScene.Sample(Bundle.Value, Nouns.Value));
        session.Simulation.TeleportTo(NpcStandOff.Choose(session.Scene.Grid, session.Scene.Npc("guide").Feet, session.Simulation.Player.FeetBlock)!.Value);
        var dialogue = new DialogueModel(new InputFocus(), session.OpeningLine, session.Phrases, session.Locale);
        var player = new RecordingPlayer();
        var actions = new SpeechActionRunner(session, dialogue, player, () => null);
        var lesson = new LessonFrontend(session, dialogue, _ => { });
        dialogue.Open(session.OpenTalk("guide"), "Guía", session.ReviewLine);
        return (session, dialogue, lesson, player, actions);
    }

    [Fact]
    public void TheOfferComesFirstLStartsTheLessonAndTheStripThenShowsTheStep()
    {
        var (session, dialogue, lesson, _, _) = AtGuide();

        var (view, offer) = lesson.State();
        Assert.Null(view);
        Assert.Equal("sample", offer!.LessonId);
        Assert.True(lesson.Handles(LessonKey.L));
        Assert.False(lesson.Handles(LessonKey.K)); // nothing to skip yet
        Assert.False(lesson.Handles(LessonKey.Enter));

        Assert.True(lesson.PressKey(LessonKey.L));

        (view, offer) = lesson.State();
        Assert.Null(offer);
        Assert.Equal(1, view!.StepNumber);
        Assert.False(lesson.Handles(LessonKey.L));
        Assert.True(lesson.Handles(LessonKey.K));
        Assert.False(lesson.Handles(LessonKey.Enter)); // a repeat step waits for the learner's turn, not for Continue
        Assert.False(lesson.Handles(LessonKey.D1)); // and has no options
        dialogue.Close();
        Assert.False(lesson.Handles(LessonKey.K)); // a closed panel has no strip
        Assert.Equal((null, null), lesson.State());
        Assert.Equal(LessonStatus.InProgress, session.Lessons.StatusOf("sample"));
    }

    [Fact]
    public void KSkipsEnterContinuesAndTheDigitsPickWhileTheTextBoxDoesNotTakeTheKeys()
    {
        var rich = TestBook(LessonBookTestsRich);
        var (_, dialogue, lesson, _, _) = AtGuide(rich);
        Assert.True(lesson.PressKey(LessonKey.L));

        Assert.True(lesson.PressKey(LessonKey.Enter)); // explain
        dialogue.BeginTyping();
        Assert.False(lesson.Handles(LessonKey.Enter)); // typing: the keys are the text box's
        dialogue.EndTyping();
        Assert.True(lesson.PressKey(LessonKey.Enter)); // model
        Assert.True(lesson.Handles(LessonKey.D2)); // the listen step
        Assert.False(lesson.Handles(LessonKey.Enter));
        Assert.True(lesson.PressKey(LessonKey.D1)); // wrong
        Assert.True(lesson.PressKey(LessonKey.D2)); // right
        Assert.False(lesson.Handles(LessonKey.D2)); // picked
        Assert.True(lesson.PressKey(LessonKey.Enter));
        Assert.True(lesson.PressKey(LessonKey.K)); // the repeat step
    }

    private static LessonBook TestBook(string json) =>
        RouteScene.Lessons(json, Bundle.Value);

    [Fact]
    public void ALessonThatStaysEngagedIsNotDrawnOverAnotherNpcsPanel()
    {
        var auto = TestBook(LessonBookTestsRich.Replace("\"title\": \"Rich\"", "\"title\": \"Rich\", \"autoStart\": true", StringComparison.Ordinal));
        var (session, dialogue, lesson, _, _) = AtGuide(auto);
        Assert.NotNull(lesson.State().View); // it started by itself at the guide
        dialogue.Close();
        session.EndTalk();
        Assert.True(session.Lessons.Engaged); // an autoStart lesson stays engaged when the panel closes
        session.Simulation.TeleportTo(NpcStandOff.Choose(session.Scene.Grid, session.Scene.Npc("villager").Feet, session.Simulation.Player.FeetBlock)!.Value);

        dialogue.Open(session.OpenTalk("villager"), "Aldeano");

        Assert.Equal((null, null), lesson.State());
        Assert.False(lesson.Handles(LessonKey.K));
        Assert.False(session.Lessons.HandlesHelp); // the help button is the villager's own
        var before = dialogue.Lines.Count;
        lesson.Pump();
        Assert.Equal(before, dialogue.Lines.Count); // the lesson adds nothing to the villager's page
    }

    [Fact]
    public void AStepWithALinePutsItOnThePanelAsTheNpcsOwnAndGlossedInEnglish()
    {
        var (session, dialogue, lesson, _, _) = AtGuide(TestBook(LessonBookTestsRich));
        _ = lesson.PressKey(LessonKey.L);
        _ = lesson.PressKey(LessonKey.Enter); // explain to model: the model's line
        var before = dialogue.Lines.Count;

        lesson.Pump();

        Assert.Equal((DialogueSpeaker.Guide, "Hola."), (dialogue.Lines[^1].Speaker, dialogue.Lines[^1].Text));
        Assert.Equal("Hello.", session.SupportForNpcLine("Hola."));
        lesson.Pump();
        Assert.Equal(before, dialogue.Lines.Count); // once per step
        Assert.Equal("Hola.", dialogue.LastNpcLine); // so Repetir plays it again
    }

    [Fact]
    public void TheAyudaAndNoSeButtonsBelongToTheLessonWhileOneIsEngagedAndSendNothingToTheNpc()
    {
        var (session, dialogue, lesson, _, actions) = AtGuide();
        _ = lesson.PressKey(LessonKey.L);

        Assert.True(actions.Perform(SpeechAction.Help));
        Assert.Equal(0, dialogue.Pending); // no turn was submitted
        Assert.Equal("Say: Hola (OH-lah). It means hello. The letter h is silent in Spanish.", session.Lessons.View!.Hint);
        Assert.Contains(session.Events.OfType<VoiceEvent>(), static e => e.Kind == VoiceEventKind.Hint);
        Assert.True(Assert.Single(session.Support.Cards).EnglishShown);

        Assert.True(actions.Perform(SpeechAction.DontKnow));
        Assert.Equal(0, dialogue.Pending);
        Assert.Equal(1, session.PhraseProgress.RecordOf("hola").DontKnow);
        Assert.Contains(session.Events.OfType<VoiceEvent>(), static e => e.Kind == VoiceEventKind.NoSe);
        Assert.Equal(1, session.Lessons.StepNumber); // a step is never ended by help
    }

    // ---- The panel's English.

    [Fact]
    public void ATurnOnThePanelGetsItsPlainEnglishFeedbackAfterTheNpcsLines()
    {
        var session = RouteScene.NewSession(Bundle.Value, Nouns.Value, CreditRules.FullTyped);
        session.Simulation.TeleportTo(NpcStandOff.Choose(session.Scene.Grid, session.Scene.Npc("shopkeeper").Feet, session.Simulation.Player.FeetBlock)!.Value);
        var dialogue = new DialogueModel(new InputFocus(), session.OpeningLine, session.Phrases, session.Locale);
        dialogue.Open(session.OpenTalk("shopkeeper"), "Tendero");
        _ = dialogue.Entry.Insert("yo quieres agua, por favor");

        _ = dialogue.Submit(line => session.Say(line));

        var english = dialogue.Lines.Where(static l => l.Speaker == DialogueSpeaker.English).Select(static l => l.Text).ToList();
        Assert.Equal("The shopkeeper understood you, but there was a small mistake. That is fine: keep going.", english[0]);
        Assert.Contains(english, static l => l.StartsWith("The verb ending must match who is speaking", StringComparison.Ordinal));
        var last = dialogue.Lines[^1];
        Assert.Equal(DialogueSpeaker.English, last.Speaker); // after the NPC's lines
        Assert.Equal("Tendero", dialogue.NpcName);
        Assert.True(dialogue.Lines.Count(static l => l.Speaker == DialogueSpeaker.Guide) >= 1);
        Assert.NotNull(session.SupportForNpcLine("¿Qué necesitas?"));
    }

    [Fact]
    public void ThePanelAddsNoEnglishFeedbackWithoutTheEnglishTextAndEnglishIsNeverWhatRepetirPlays()
    {
        var session = RouteScene.NewSession(Bundle.Value, Nouns.Value, CreditRules.FullTyped);
        session.Simulation.TeleportTo(NpcStandOff.Choose(session.Scene.Grid, session.Scene.Npc("shopkeeper").Feet, session.Simulation.Player.FeetBlock)!.Value);
        var dialogue = new DialogueModel(new InputFocus(), session.OpeningLine, session.Phrases);
        dialogue.Open(session.OpenTalk("shopkeeper"), "Tendero");
        _ = dialogue.Entry.Insert("quiero agua, por favor");

        _ = dialogue.Submit(line => session.Say(line));

        Assert.DoesNotContain(dialogue.Lines, static l => l.Speaker == DialogueSpeaker.English);
        dialogue.AddEnglish("English text from the game"); // and when there is some, it is a line of its own
        Assert.Equal(DialogueSpeaker.English, dialogue.Lines[^1].Speaker);
        Assert.NotEqual("English text from the game", dialogue.LastNpcLine);
        Assert.False(string.IsNullOrEmpty(dialogue.LastNpcLine));
    }

    [Fact]
    public void EnglishIsOnlyDrawnNeverSpokenSoNoCueOrPlayedLineIsAnEnglishText()
    {
        var (session, _, lesson, player, _) = AtGuide();
        _ = lesson.PressKey(LessonKey.L);
        _ = session.Say("hola");
        var cues = session.Support.TakeCues();
        var index = TestSpeechIndex();
        var spoken = cues.Select(static c => c.Text).Concat(player.Played.Select(static p => p.Line)).ToList();

        Assert.NotEmpty(cues);
        Assert.All(spoken, text => Assert.True(index.Find("guide", text) is not null, $"\"{text}\" is a Spanish line with a pre-rendered voice"));
        using var document = JsonDocument.Parse(File.ReadAllText(Repo.Path("content", "game-v1", "game", "english.json")));
        foreach (var line in document.RootElement.GetProperty("lines").EnumerateArray())
        {
            Assert.Null(index.Find("guide", line.GetProperty("english").GetString()!)); // no English text has a voice: only Spanish is spoken
        }
    }

    private static SpeechIndex TestSpeechIndex() => new(SpeechLines.Enumerate(Bundle.Value, Nouns.Value, RouteScene.DataOf(Bundle.Value).Phrases));

    // ---- Options, the log and the font.

    [Fact]
    public void TheLessonStepsAndTheTipsFlagAreParsedInOrderAndRefuseNonsense()
    {
        var options = Parse("--screenshot", "a.png", "--lesson", "sample", "--say", "hola", "--lesson-help", "--lesson-pick", "2", "--lesson-next", "--lesson-skip", "--tips");

        Assert.Equal(
            [ScriptStepKind.LessonStart, ScriptStepKind.Say, ScriptStepKind.LessonHelp, ScriptStepKind.LessonPick, ScriptStepKind.LessonContinue, ScriptStepKind.LessonSkip],
            options.Script.Select(static s => s.Kind));
        Assert.Equal(["sample", "hola", string.Empty, "2", string.Empty, string.Empty], options.Script.Select(static s => s.Value));
        Assert.True(options.Tips);
        Assert.True(options.ShowsTips);
        Assert.False(Parse("--screenshot", "a.png", "--talk", "guide").ShowsTips); // a scripted run draws no tips unless it asks
        Assert.True(Parse().ShowsTips); // an interactive run always does
        Assert.False(LaunchOptions.TryParse(["--screenshot", "a.png", "--lesson-pick", "5"], out _, out var error));
        Assert.Contains("--lesson-pick", error, StringComparison.Ordinal);
        Assert.False(LaunchOptions.TryParse(["--lesson", "sample"], out _, out error)); // a step needs --screenshot
        Assert.Contains("--lesson", error, StringComparison.Ordinal);
        Assert.False(LaunchOptions.TryParse(["--screenshot", "a.png", "--lesson", " "], out _, out _));
        Assert.Contains("--lesson <id>", LaunchOptions.Usage, StringComparison.Ordinal);
        Assert.Contains("F1 shows the controls in English", LaunchOptions.Usage, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSessionLogWritesEachLessonEventWithItsStepAndOutcome()
    {
        var sessionWriter = new StringWriter();
        var log = new SessionLog(sessionWriter, new StringWriter(), () => Stamp);
        var (session, _, lesson, _, _) = AtGuide();
        _ = lesson.PressKey(LessonKey.L);
        _ = session.Say("hola");
        _ = lesson.PressKey(LessonKey.K);

        log.Pump(session);
        log.Dispose();

        var lines = sessionWriter.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(static l => JsonDocument.Parse(l).RootElement.Clone()).Where(static l => l.GetProperty("type").GetString() == "lesson").ToList();
        Assert.Equal(
            ["lesson_started", "step_started", "step_done", "step_started", "step_done", "lesson_completed"],
            lines.Select(static l => l.GetProperty("kind").GetString()));
        var firstDone = lines[2];
        Assert.Equal(("sample", 1, "say-hola", "repeat", "done"), (firstDone.GetProperty("lesson").GetString(), firstDone.GetProperty("step").GetInt32(), firstDone.GetProperty("step_id").GetString(), firstDone.GetProperty("step_type").GetString(), firstDone.GetProperty("outcome").GetString()));
        var skipped = lines[4];
        Assert.Equal("skipped", skipped.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, lines[0].GetProperty("step_id").ValueKind);
        Assert.Equal(JsonValueKind.Null, lines[1].GetProperty("outcome").ValueKind);
        Assert.Equal(Stamp.ToString("O", System.Globalization.CultureInfo.InvariantCulture), lines[0].GetProperty("t").GetString());
    }

    [Fact]
    public void TheFontCoversEveryCharacterOfTheEnglishTheLessonsAndTheNewLayers()
    {
        var cmap = TrueTypeCmap.Load(Repo.Path("assets", "fonts", "pixel-operator", "PixelOperator.ttf"));
        var strings = new List<string>
        {
            DialoguePanel.SupportPrefix, SupportLayer.EnglishOnHelp, PhraseJournalRows.EnglishAndCardText, "Lesson · A first hello · step 1 of 2 · Say it", "[Enter] [K] [L] [H] [1] [2] [3] [4]",
        };
        foreach (var file in new[] { "english.json", "lessons.json" })
        {
            using var document = JsonDocument.Parse(File.ReadAllText(Repo.Path("content", "game-v1", "game", file)));
            CollectStrings(document.RootElement, strings);
        }

        var missing = strings.SelectMany(static s => s).Where(c => !cmap.Covers(c)).Distinct().Select(static c => $"U+{(int)c:X4} {c}").ToList();

        Assert.True(missing.Count == 0, "The font has no glyph for: " + string.Join(", ", missing));
    }

    private static void CollectStrings(JsonElement element, List<string> into)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                into.Add(element.GetString()!);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectStrings(item, into);
                }

                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    CollectStrings(property.Value, into);
                }

                break;
            default:
                break;
        }
    }
}
