using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Startup;
using Vocaterra.Game.UI.Checks;
using Vocaterra.Game.UI.Dialogue;
using Vocaterra.Game.UI.Hud;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Intent;

namespace Vocaterra.Game.Tests.Component;

/// <summary>
/// The front-end logic of S7 that needs no GPU, window or microphone: the cancel and watchdog state machine, the heard-versus-expected line, the
/// button-to-action map with its voice events, the choices from the repair ladder to a turn marked as a choice, the uncertainty offer of typing,
/// the mic check and the command line.
/// </summary>
public sealed class SpeechUxFrontendTests
{
    private static readonly Lazy<PhraseSheet> Sheet = new(() => TestGame.Data.Phrases);

    private sealed class RecordingPlayer : ILinePlayer
    {
        public List<(string Npc, string Line, bool Slow)> Played { get; } = [];

        public void Play(string npc, string line, bool slow) => Played.Add((npc, line, slow));
    }

    private static LaunchOptions Parse(params string[] args)
    {
        Assert.True(LaunchOptions.TryParse(args, out var options, out var error), error);
        return options;
    }

    private static string Refuse(params string[] args)
    {
        Assert.False(LaunchOptions.TryParse(args, out _, out var error));
        return error;
    }

    // A spoken turn at the stall as the worker would give it (Vosk's words: the transcript, and what the NPC understood).
    private static GuideTurn Spoken(string line, string intent = "request_item", bool communicated = true, bool uncertain = false, string prompt = "stall-need", double? confidence = 0.95, IReadOnlyList<string>? choices = null, RepairKind? repair = null) => new(
        line,
        intent,
        communicated,
        1.0,
        [],
        null,
        communicated ? ["Toma la botella."] : ["No te entendí. ¿Me lo dices otra vez?"],
        null,
        "asking",
        [],
        null,
        null,
        null,
        "digest",
        SlotMap.Empty,
        Input: InputKind.Spoken,
        Confidence: confidence,
        Npc: "shopkeeper",
        Prompt: prompt,
        RepairKind: repair,
        Choices: choices,
        Uncertain: uncertain);

    private static AppliedTurn Applied(GuideTurn turn, bool cancelled = false)
    {
        var scene = new GuideScene(new GuideViewpoint(new BlockPos(0, 0, 0), 0, 0, 0));
        var request = new TurnRequest(1, TurnSource.Clip, null, null, null, scene, new Vocaterra.Nlp.Core.Determinism.StoredTimestamp(1), "digest", null, 0, "shopkeeper");
        return new AppliedTurn(new TurnOutcome(request, cancelled ? null : turn, null, new TurnTimings(0, 0, 5, false), cancelled), null);
    }

    private static DialogueModel OpenPanel(out InputFocus focus)
    {
        focus = new InputFocus();
        var dialogue = new DialogueModel(focus, "¡Hola! Soy la guía.", RouteScene.Shipped, Sheet.Value);
        dialogue.Open(new NpcOpening("shopkeeper", "stall-need", ["¿Qué necesitas?"], true, [], false), "Tendero");
        return dialogue;
    }

    [Fact]
    public void TheWatchdogFiresOnceWhenATurnHasThoughtForFiveSecondsAndStartsOverWhenItEnds()
    {
        var watchdog = new TurnWatchdog();

        Assert.False(watchdog.Update(10, thinking: false)); // nothing is thinking: no wait
        Assert.Equal(0, watchdog.Waited);
        for (var frame = 0; frame < 9; frame++)
        {
            Assert.False(watchdog.Update(0.5, thinking: true)); // 4.5 s
        }

        Assert.True(watchdog.Update(0.5, thinking: true)); // 5.0 s: the limit
        Assert.True(watchdog.Fired);
        Assert.False(watchdog.Update(1.0, thinking: true)); // once per wait
        Assert.False(watchdog.Update(1.0, thinking: true));

        Assert.False(watchdog.Update(0.5, thinking: false)); // the turn came back
        Assert.False(watchdog.Fired);
        Assert.False(watchdog.Update(4.9, thinking: true));
        Assert.True(watchdog.Update(0.2, thinking: true)); // a new wait fires again
        Assert.Equal(5.0, TurnWatchdog.LimitSeconds);
    }

    [Fact]
    public void WhenTheWatchdogFiresThePanelOffersTypingWithTheEscribirButtonFocusedAndTheTimeoutIsLogged()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var dialogue = OpenPanel(out _);
        dialogue.BeginSpoken(); // a turn is out

        // What the game does when the watchdog fires: log it and offer typing.
        session.LogVoice(VoiceEventKind.Timeout, "5 s");
        dialogue.OfferTyping();

        Assert.True(dialogue.IsThinking);
        Assert.True(dialogue.TypingOffered);
        Assert.Equal(new DialogueLine(DialogueSpeaker.Hint, "¿Escribes?"), dialogue.Lines[^1]);
        Assert.Equal(["voice timeout at guide (\"5 s\")"], session.Events.OfType<VoiceEvent>().Select(static e => e.Text));
        dialogue.OfferTyping(); // once
        Assert.Single(dialogue.Lines, static l => l.Text == "¿Escribes?");

        // The turn comes back: the offer is gone.
        _ = dialogue.Complete(Applied(Spoken("quiero agua")));
        Assert.False(dialogue.TypingOffered);
        Assert.False(dialogue.IsThinking);
    }

    [Fact]
    public void EscCancelsATurnThatIsThinkingThePanelStopsThinkingAtOnceAndTheCancelledOutcomeShowsNothing()
    {
        var dialogue = OpenPanel(out _);
        dialogue.BeginSpoken();
        var before = dialogue.Lines.Count;

        Assert.True(dialogue.IsThinking);
        Assert.True(dialogue.CancelPending());

        Assert.False(dialogue.IsThinking); // "Thinking…" is gone at once
        Assert.Equal(1, dialogue.Pending); // the worker has not handed the outcome back yet
        Assert.False(dialogue.CancelPending()); // nothing left to cancel

        var shown = dialogue.Complete(Applied(Spoken("quiero agua"), cancelled: true));

        Assert.Null(shown);
        Assert.Equal(0, dialogue.Pending);
        Assert.Equal(before, dialogue.Lines.Count); // a dropped turn adds no line and counts no turn
        Assert.Equal(0, dialogue.TurnCount);
        Assert.Null(dialogue.Heard);
    }

    [Fact]
    public void ATurnSentAfterACancelIsShownNormally()
    {
        var dialogue = OpenPanel(out _);
        dialogue.BeginSpoken();
        _ = dialogue.CancelPending();
        dialogue.BeginSpoken(); // the player tries again while the cancelled one is still on the worker

        Assert.True(dialogue.IsThinking);
        Assert.Equal(2, dialogue.Pending);
        _ = dialogue.Complete(Applied(Spoken("x"), cancelled: true));
        Assert.True(dialogue.IsThinking); // the new one is still out
        _ = dialogue.Complete(Applied(Spoken("quiero agua")));
        Assert.False(dialogue.IsThinking);
        Assert.Equal(1, dialogue.TurnCount);
    }

    [Fact]
    public void ThePersistentLineShowsWhatWasHeardAndTheSheetsPhraseTheNpcExpected()
    {
        var dialogue = OpenPanel(out _);

        _ = dialogue.Complete(Applied(Spoken("quiero agua")));

        Assert.Equal("Heard: «quiero agua» · Expected: «Quiero agua, por favor.»", dialogue.Heard!.Text(RouteScene.Shipped.Locale));
        Assert.Equal("Heard: «quiero agua» · Expected: «Quiero agua, por favor.»", dialogue.HeardText); // the console's "panel:" line (C04: it once printed the method)
        Assert.Equal("pedir-agua", dialogue.Heard.ExpectedId);

        // It stays through the next turn until a spoken one replaces it; a typed turn clears it (nothing was heard).
        _ = dialogue.Complete(Applied(Spoken("no gracias", intent: "deny", prompt: "stall-offer")));
        Assert.Equal("Heard: «no gracias» · Expected: «No, gracias.»", dialogue.Heard!.Text(RouteScene.Shipped.Locale));
        dialogue.BeginLine("Ayuda");
        _ = dialogue.Complete(Applied(Spoken("ayuda") with { Input = InputKind.Button }));
        Assert.Null(dialogue.Heard);
    }

    [Fact]
    public void ForALineTheNpcDidNotUnderstandTheExpectedPhraseIsTheClosestRowOfThePromptAndSilenceAndUnknownSpeechAreToldApart()
    {
        var dialogue = OpenPanel(out _);

        _ = dialogue.Complete(Applied(Spoken("no gracia", intent: "unknown", communicated: false, uncertain: true, prompt: "stall-offer", repair: RepairKind.AskAgain)));
        Assert.Equal("Heard: «no gracia» · Expected: «No, gracias.»", dialogue.Heard!.Text(RouteScene.Shipped.Locale));

        _ = dialogue.Complete(Applied(Spoken(string.Empty, intent: "unknown", communicated: false, uncertain: true, confidence: 0.0, repair: RepairKind.AskAgain))); // only [unk]: speech that was no words
        Assert.Equal("Heard: «…» · Expected: «Quiero agua, por favor.»", dialogue.Heard!.Text(RouteScene.Shipped.Locale));

        _ = dialogue.Complete(Applied(Spoken(string.Empty, intent: "unknown", communicated: false, uncertain: true, confidence: 1.0, repair: RepairKind.AskAgain))); // silence
        Assert.Equal("Heard: (nothing) · Expected: «Quiero agua, por favor.»", dialogue.Heard!.Text(RouteScene.Shipped.Locale));
    }

    [Fact]
    public void AnOpenPromptWithSeveralPhrasesExpectsTheOneOfTheIntentThatMatched()
    {
        var focus = new InputFocus();
        var dialogue = new DialogueModel(focus, "Hola.", RouteScene.Shipped, Sheet.Value);
        dialogue.Open(new NpcOpening("villager", "villager-open", ["Hola."], true, [], false), "Aldeano");

        _ = dialogue.Complete(Applied(Spoken("dónde está el río", intent: "ask_directions", prompt: "villager-open")));
        Assert.Equal("Heard: «dónde está el río» · Expected: «¿Dónde está el río?»", dialogue.Heard!.Text(RouteScene.Shipped.Locale));

        _ = dialogue.Complete(Applied(Spoken("buenos días", intent: "greet", prompt: "villager-open")));
        Assert.Equal("Heard: «buenos días» · Expected: «Hola.»", dialogue.Heard!.Text(RouteScene.Shipped.Locale));

        _ = dialogue.Complete(Applied(Spoken("gola", intent: "unknown", communicated: false, uncertain: true, prompt: "villager-open", repair: RepairKind.AskAgain)));
        Assert.Equal("Heard: «gola» · Expected: «Hola.»", dialogue.Heard!.Text(RouteScene.Shipped.Locale));
    }

    [Fact]
    public void EveryKeyOfTheButtonsMapsToItsActionAndEveryActionToItsVoiceEvent()
    {
        Assert.Equal(
            [(PanelKey.R, SpeechAction.Repeat), (PanelKey.S, SpeechAction.Slower), (PanelKey.H, SpeechAction.Help), (PanelKey.N, SpeechAction.DontKnow), (PanelKey.W, SpeechAction.Type), (PanelKey.Tab, SpeechAction.Type),
                (PanelKey.D1, SpeechAction.Choice1), (PanelKey.D2, SpeechAction.Choice2), (PanelKey.D3, SpeechAction.Choice3)],
            Enum.GetValues<PanelKey>().Select(static k => (k, SpeechButtons.ForKey(k)!.Value)));
        Assert.Equal(["Repetir", "Más despacio", "Ayuda", "No sé", "Escribir"], SpeechButtons.For(RouteScene.Shipped.Words.Buttons).Select(static b => b.Label));
        Assert.Equal(["[R] Repetir", "[S] Más despacio", "[H] Ayuda", "[N] No sé", "[W] Escribir"], SpeechButtons.For(RouteScene.Shipped.Words.Buttons).Select(static b => b.Text));
        Assert.Equal(
            [VoiceEventKind.Replay, VoiceEventKind.Slower, VoiceEventKind.Hint, VoiceEventKind.NoSe, VoiceEventKind.TypeFallback],
            SpeechButtons.For(RouteScene.Shipped.Words.Buttons).Select(static b => SpeechButtons.EventOf(b.Action)));
        Assert.Equal(VoiceEventKind.Choice, SpeechButtons.EventOf(SpeechAction.Choice2));
        Assert.Equal([0, 1, 2], new[] { SpeechAction.Choice1, SpeechAction.Choice2, SpeechAction.Choice3 }.Select(static a => SpeechButtons.ChoiceIndex(a)!.Value));
        Assert.Null(SpeechButtons.ChoiceIndex(SpeechAction.Help));
        Assert.Null(SpeechButtons.ChoiceAt(3));
    }

    // Runs the panel's buttons against a real session at the stall with a typed worker: what a press does, and the event it logs.
    private sealed class AtTheStall : IDisposable
    {
        public AtTheStall()
        {
            Session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns, CreditRules.FullTyped);
            Assert.True(Session.StartWorker(loadSpeech: false).WaitUntilReady(30000));
            Session.Simulation.TeleportTo(NpcStandOff.Choose(Session.Scene.Grid, Session.Scene.Npc("shopkeeper").Feet, Session.Simulation.Player.FeetBlock)!.Value);
            var opening = Session.OpenTalk("shopkeeper");
            Dialogue = new DialogueModel(new InputFocus(), Session.OpeningLine, Session.Data, Session.Phrases);
            Dialogue.Open(opening, "Tendero");
            Player = new RecordingPlayer();
            Actions = new SpeechActionRunner(Session, Dialogue, Player, () => null);
            Pump = new TurnPump(Session, Dialogue, () => null);
        }

        public VoxelSession Session { get; }

        public DialogueModel Dialogue { get; }

        public RecordingPlayer Player { get; }

        public SpeechActionRunner Actions { get; }

        public TurnPump Pump { get; }

        public IReadOnlyList<string> Voice => Session.Events.OfType<VoiceEvent>().Select(static e => e.Text).ToList();

        public IReadOnlyList<AppliedTurn> Await(int count)
        {
            var all = new List<AppliedTurn>();
            var deadline = Environment.TickCount64 + 30000;
            while (all.Count < count && Environment.TickCount64 < deadline)
            {
                all.AddRange(Pump.Apply());
                Thread.Sleep(2);
            }

            Assert.Equal(count, all.Count);
            return all;
        }

        public void Dispose() => Session.StopWorker();
    }

    [Fact]
    public void RepetirAndMasDespacioShowTheNpcsLastLineAgainHighlightedCallTheAudioHookAndRaiseTheirVoiceEvents()
    {
        using var stall = new AtTheStall();

        Assert.True(stall.Actions.Perform(SpeechAction.Repeat));
        Assert.True(stall.Actions.Perform(SpeechAction.Slower));

        // The question again, and (S8: the card of the request is up) the model answer after it.
        Assert.Equal(
            [
                ("shopkeeper", "¿Qué necesitas?", false),
                ("shopkeeper", "Puedes decir: «Quiero agua, por favor».", false),
                ("shopkeeper", "¿Qué necesitas?", true),
                ("shopkeeper", "Puedes decir: «Quiero agua, por favor».", true),
            ],
            stall.Player.Played);
        Assert.Equal(["voice replay at shopkeeper (\"¿Qué necesitas?\")", "voice slower at shopkeeper (\"¿Qué necesitas?\")"], stall.Voice);
        Assert.Equal(0, stall.Dialogue.HighlightedLine);
        Assert.Equal(stall.Session.CurrentTick + SpeechActionRunner.HighlightTicks, stall.Dialogue.HighlightUntilTick);
        Assert.Equal(0, stall.Dialogue.Pending); // a replay is no turn
    }

    [Fact]
    public void AyudaAndNoSeSendTheirLinesAsTurnsTheNpcAnswersAndEachPressIsAVoiceEvent()
    {
        using var stall = new AtTheStall();

        Assert.True(stall.Actions.Perform(SpeechAction.Help));
        Assert.True(stall.Dialogue.IsThinking);
        var help = stall.Await(1).Single().Turn!;
        Assert.True(stall.Actions.Perform(SpeechAction.DontKnow));
        var unknown = stall.Await(1).Single().Turn!;

        Assert.Equal(("ayuda", "ask_help", InputKind.Button), (help.Line, help.Intent, help.Input));
        Assert.Equal(("no sé", "dont_know", InputKind.Button), (unknown.Line, unknown.Intent, unknown.Input));
        Assert.Equal(["voice hint at shopkeeper", "voice no_se at shopkeeper"], stall.Voice);
        Assert.Equal(["Ayuda", "No sé"], stall.Dialogue.Lines.Where(static l => l.Speaker == DialogueSpeaker.Player).Select(static l => l.Text)); // what the player picked
    }

    [Fact]
    public void EscribirOpensTheTextBoxLogsTheFallbackAndDropsATurnThatWasStillThinking()
    {
        using var stall = new AtTheStall();
        Assert.False(stall.Dialogue.IsTyping);
        stall.Dialogue.BeginSpoken(); // a turn is out (the watchdog is about to fire)

        Assert.True(stall.Actions.Perform(SpeechAction.Type));

        Assert.True(stall.Dialogue.IsTyping);
        Assert.False(stall.Dialogue.IsThinking); // the pending turn was cancelled: the player chose to type
        Assert.Contains("voice typed_fallback at shopkeeper", stall.Voice);

        stall.Dialogue.EndTyping();
        Assert.False(stall.Dialogue.IsTyping);
        Assert.True(stall.Dialogue.IsOpen);
    }

    [Fact]
    public void ThreeLinesTheNpcDidNotUnderstandOfferChoicesThatBecomeButtonsAndAPickedOneIsATurnMarkedChoice()
    {
        using var stall = new AtTheStall();
        for (var i = 0; i < 3; i++)
        {
            stall.Dialogue.Entry.Insert("el gato duerme");
            stall.Dialogue.RequestSubmit();
            stall.Pump.Submit();
            _ = stall.Await(1);
        }

        // The core offers one canonical per answer in declared order (M29f): the water row has a sheet row, the bread and the fruit do not and get the core's text, capitalized.
        Assert.Equal(["Quiero agua, por favor.", "Quiero pan por favor.", "Quiero fruta por favor."], stall.Dialogue.Choices.Select(static c => c.Label));

        Assert.True(stall.Actions.Perform(SpeechAction.Choice1));

        Assert.Empty(stall.Dialogue.Choices); // answered: the buttons go
        Assert.Equal(new DialogueLine(DialogueSpeaker.Player, "Quiero agua, por favor."), stall.Dialogue.Lines[^1]); // the label of the choice, shown as the player's line
        var picked = stall.Await(1).Single().Turn!;
        Assert.True(picked.Choice);
        Assert.Equal("request_item", picked.Intent);
        Assert.Contains("stall.give_water", picked.HostEvents);
        Assert.Contains(stall.Session.Events.OfType<PhraseUse>(), static u => u is { PhraseId: "pedir-agua", Kind: PhraseUseKind.Choice });
        Assert.Equal(["voice choice at shopkeeper (\"quiero agua por favor\")"], stall.Voice); // the line the choice sends is the offered canonical
        Assert.Equal(0, stall.Session.Words.RecordOf("agua").Spoken); // a recognition signal, never counted as said
        Assert.Empty(stall.Dialogue.Choices);
    }

    [Fact]
    public void TheChoiceButtonsAreLaidOutAboveTheFixedButtonsAndAClickFindsEachOfThem()
    {
        var dialogue = OpenPanel(out _);
        _ = dialogue.Complete(Applied(Spoken("x", intent: "unknown", communicated: false, uncertain: true, repair: RepairKind.OfferChoices, choices: ["agua por favor", "ayuda", "ayuda por favor"])));
        Assert.Equal(2, dialogue.Choices.Count);

        var layout = new DialogueLayout(1280, 720, static t => t.Length * 8f, dialogue.Choices.Select(static c => c.Label).ToList(), SpeechButtons.For(RouteScene.Shipped.Words.Buttons), typing: false);

        foreach (var (action, box) in layout.Buttons.Concat(layout.Choices))
        {
            Assert.Equal(action, layout.ActionAt(box.X + 1, box.Y + 1));
            Assert.Equal(action, layout.ActionAt(box.Right - 1, box.Bottom - 1));
            Assert.True(box.Y >= layout.Panel.Y && box.Bottom <= layout.Panel.Bottom, $"{action} is outside the panel.");
        }

        Assert.Equal([SpeechAction.Repeat, SpeechAction.Slower, SpeechAction.Help, SpeechAction.DontKnow, SpeechAction.Type], layout.Buttons.Select(static b => b.Action));
        Assert.Equal([SpeechAction.Choice1, SpeechAction.Choice2], layout.Choices.Select(static b => b.Action));
        Assert.True(layout.Choices[0].Box.Bottom <= layout.Buttons[0].Box.Y);
        Assert.Null(layout.ActionAt(layout.Panel.X + 2, layout.Panel.Y + 2)); // the title bar
        Assert.Null(layout.ActionAt(layout.Buttons[0].Box.Right + 1, layout.Buttons[0].Box.Y + 1)); // the gap between two buttons
        Assert.Null(layout.InputBox);
        Assert.True(layout.HeardY > layout.LinesTop && layout.HeardY < layout.Choices[0].Box.Y);
        Assert.True(layout.LinesBottom > layout.LinesTop);
    }

    [Fact]
    public void TheTextBoxTakesRoomBelowTheButtonsOnlyWhileTyping()
    {
        var typing = new DialogueLayout(1280, 720, static t => t.Length * 8f, [], SpeechButtons.For(RouteScene.Shipped.Words.Buttons), typing: true);
        var buttons = new DialogueLayout(1280, 720, static t => t.Length * 8f, [], SpeechButtons.For(RouteScene.Shipped.Words.Buttons), typing: false);

        Assert.NotNull(typing.InputBox);
        Assert.True(typing.InputBox!.Value.Y > typing.Buttons[0].Box.Bottom);
        Assert.True(typing.Buttons[0].Box.Y < buttons.Buttons[0].Box.Y); // the buttons moved up to make room
        Assert.True(typing.InputBox!.Value.Bottom <= typing.HintsY);

        var small = new DialogueLayout(640, 480, static t => t.Length * 8f, ["a", "b", "c"], SpeechButtons.For(RouteScene.Shipped.Words.Buttons), typing: true);
        Assert.True(small.Panel.Y >= 0 && small.Panel.Bottom <= 480);
        Assert.True(small.LinesBottom > small.LinesTop); // still room for the conversation
    }

    [Fact]
    public void TheWordCardStandsAboveTheDialoguePanelNotUnderIt()
    {
        var panel = DialogueLayout.PanelBox(1280, 720);
        var head = ConversationCards.HeadRect(640, 180, 290);
        var layout = ConversationLayout.Compute(1280, 720, head, null, panel, [ConversationCards.NewWord]);
        var toast = Assert.Single(layout.Cards);
        var free = WordCardLayer.BoxFor(1280, 720);

        Assert.Equal(CardPresence.Full, toast.State);
        Assert.False(ConversationLayout.Overlaps(toast.Box, panel), "the toast must not overlap the panel"); // S6 left it hidden behind the panel
        Assert.False(ConversationLayout.Overlaps(toast.Box, head), "nor the speaker (S22)");
        Assert.True(toast.Box.Y >= 0);
        Assert.True(free.Y > toast.Box.Y); // without the panel it sits above the hotbar as before
    }

    [Fact]
    public void TwoUncertainSpokenLinesInARowOfferTypingAndASureLineStartsTheCountOver()
    {
        var dialogue = OpenPanel(out _);
        GuideTurn Lost() => Spoken("gola", intent: "unknown", communicated: false, uncertain: true, repair: RepairKind.AskAgain);

        _ = dialogue.Complete(Applied(Lost()));
        Assert.False(dialogue.TypingOffered);
        Assert.Equal(1, dialogue.UncertainStreak);

        _ = dialogue.Complete(Applied(Lost()));
        Assert.True(dialogue.TypingOffered); // "¿Escribes?" with the Escribir button focused
        Assert.Equal("¿Escribes?", dialogue.Lines[^1].Text);

        _ = dialogue.Complete(Applied(Spoken("quiero agua")));
        Assert.Equal(0, dialogue.UncertainStreak);
        Assert.False(dialogue.TypingOffered);
    }

    [Fact]
    public void AnUncertainLineShowsAsNotUnderstoodInTheNoteAndNeverAsAVerdict()
    {
        var dialogue = OpenPanel(out _);

        _ = dialogue.Complete(Applied(Spoken("quiero agua por favor", intent: "unknown", communicated: false, uncertain: true, confidence: 0.4)));

        var note = dialogue.Lines.Last(static l => l.Speaker == DialogueSpeaker.Note).Text;
        Assert.StartsWith("heard at 40 %, not understood", note, StringComparison.Ordinal);
        Assert.DoesNotContain("request_item", note, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDialoguePanelKeepsTheSpeechButtonsModeUntilTheTextBoxIsAskedFor()
    {
        var dialogue = OpenPanel(out var focus);

        Assert.Equal(PanelMode.Buttons, dialogue.Mode);
        Assert.False(dialogue.IsTyping);
        Assert.True(focus.TextEntryActive); // the panel has the keyboard either way (the player does not walk)

        dialogue.BeginTyping();
        Assert.True(dialogue.IsTyping);
        dialogue.Close();
        Assert.False(dialogue.IsTyping);
        Assert.Equal(PanelMode.Buttons, dialogue.Mode); // reopening starts on the buttons
    }

    [Fact]
    public void TheMicCheckHearsHolaAndPassesAndLogsItAndANewTryAfterAFailedOneCanPassToo()
    {
        var log = new List<(VoiceEventKind Kind, string? Heard)>();
        var check = new MicCheckModel((kind, heard) => log.Add((kind, heard)), RouteScene.Shipped.Locale, "hola");
        Assert.Equal(MicCheckPhase.Ready, check.Phase);
        Assert.Equal("Say \"hola\"", check.Instruction);
        Assert.Equal([MicCheckButton.Skip], check.Buttons);

        check.BeginListening();
        Assert.Equal(MicCheckPhase.Listening, check.Phase);
        check.Feed(0.1, 1.0 / 60);
        check.Feed(0.3, 1.0 / 60);
        check.Feed(0.05, 1.0 / 60);
        Assert.Equal(0.3, check.Peak);
        Assert.True(check.Level > 0.2 && check.Level < 0.3); // the meter fell a little from its peak
        check.EndListening();
        Assert.Equal(MicCheckPhase.Thinking, check.Phase);
        check.Complete(new MicCheckResult("gola", false, 0.3));

        Assert.Equal(MicCheckPhase.Failed, check.Phase);
        Assert.Equal("Heard: «gola»", check.HeardText);
        Assert.Equal("I didn't catch that. Try again?", check.StatusText);
        Assert.Equal([MicCheckButton.Retry, MicCheckButton.Skip], check.Buttons);

        check.Press(MicCheckButton.Retry);
        Assert.Equal(MicCheckPhase.Ready, check.Phase);
        Assert.Null(check.HeardText);
        check.BeginListening();
        check.EndListening();
        check.Complete(new MicCheckResult("hola", true, 0.4));

        Assert.Equal(MicCheckPhase.Passed, check.Phase);
        Assert.Equal("Heard: «hola»", check.HeardText);
        Assert.Equal("Got it, I can hear you.", check.StatusText);
        Assert.Equal([MicCheckButton.Continue], check.Buttons);
        check.Press(MicCheckButton.Continue);
        Assert.False(check.IsActive);
        Assert.Equal([(VoiceEventKind.MicCheckFailed, "gola"), (VoiceEventKind.MicCheckPassed, "hola")], log);
        Assert.Equal(2, check.Tries);
    }

    [Fact]
    public void TheMicCheckCanBeSkippedFromAnyPhaseAndNeverBlocksAndSilenceIsAFailedTryNotAnError()
    {
        foreach (var phase in new[] { "ready", "listening", "thinking", "failed" })
        {
            var log = new List<VoiceEventKind>();
            var check = new MicCheckModel((kind, _) => log.Add(kind), RouteScene.Shipped.Locale, "hola");
            if (phase != "ready")
            {
                check.BeginListening();
            }

            if (phase is "thinking" or "failed")
            {
                check.EndListening();
            }

            if (phase == "failed")
            {
                check.Complete(new MicCheckResult(string.Empty, false, 0));
            }

            check.Skip();

            Assert.False(check.IsActive, phase);
            Assert.Equal([VoiceEventKind.MicCheckSkipped], log.Where(static k => k == VoiceEventKind.MicCheckSkipped));
            check.Skip(); // again: nothing more is logged
            Assert.Equal(1, log.Count(static k => k == VoiceEventKind.MicCheckSkipped));
        }

        var silent = new MicCheckModel((_, _) => { }, RouteScene.Shipped.Locale, "hola");
        silent.BeginListening();
        silent.EndListening();
        silent.HeardNothing();
        Assert.Equal(MicCheckPhase.Failed, silent.Phase);
        Assert.Equal("Heard: (nothing)", silent.HeardText);
    }

    [Fact]
    public void TheMicCheckScreenFindsItsButtonsByClickAndItsMeterFollowsTheSquareRootOfTheLevel()
    {
        var check = new MicCheckModel((_, _) => { }, RouteScene.Shipped.Locale, "hola");
        check.BeginListening();
        check.EndListening();
        check.Complete(new MicCheckResult("gola", false, 0.0625));
        var layout = new MicCheckLayout(1280, 720, check.Buttons, static t => t.Length * 8f, RouteScene.Shipped.Locale);

        Assert.Equal(MicCheckButton.Retry, layout.ButtonAt(layout.Buttons[0].Box.X + 2, layout.Buttons[0].Box.Y + 2));
        Assert.Equal(MicCheckButton.Skip, layout.ButtonAt(layout.Buttons[1].Box.X + 2, layout.Buttons[1].Box.Y + 2));
        Assert.Null(layout.ButtonAt(layout.Panel.X + 1, layout.Panel.Y + 1));
        Assert.Equal(0.4, check.MeterFill, 6); // sqrt(0.0625) * 1.6
        Assert.True(layout.Panel.Contains(layout.Meter.X, layout.Meter.Y));
    }

    [Fact]
    public void ThePushToTalkReportsTheLevelOfWhatItCollectedForTheMeter()
    {
        var quiet = new short[1600];
        var loud = new short[1600];
        for (var i = 0; i < loud.Length; i++)
        {
            loud[i] = (short)(i % 2 == 0 ? 8000 : -8000);
        }

        var talk = new PushToTalkSpeechCapture(new FakeMic(16000, quiet, loud));
        Assert.True(talk.Begin());
        talk.Pump();
        Assert.Equal(0, talk.LastLevel);
        talk.Pump();
        Assert.InRange(talk.LastLevel, 0.24, 0.25); // 8000 / 32767
        _ = talk.End();
    }

    private sealed class FakeMic(int sampleRate, params short[][] chunks) : ISpeechCapture
    {
        private int _next;

        public int SampleRate => sampleRate;

        public void Start()
        {
        }

        public short[] ReadAvailable() => _next < chunks.Length ? chunks[_next++] : [];

        public void Stop()
        {
        }
    }

    [Fact]
    public void TheMicCheckAndTheSpokenStepsHaveTheirOwnLaunchOptions()
    {
        var forced = Parse("--screenshot", "a.png", "--mic-check");
        Assert.Equal(MicCheckMode.Force, forced.MicCheck);
        Assert.True(forced.ShowsMicCheck);
        Assert.True(forced.NeedsSpeech); // a scripted mic check hears a synthesized "hola": it loads the recognizer
        Assert.False(forced.UsesMicrophone); // but never opens a real microphone in a scripted run

        var scripted = Parse("--screenshot", "a.png", "--talk", "shopkeeper", "--say", "hola");
        Assert.Equal(MicCheckMode.Auto, scripted.MicCheck);
        Assert.False(scripted.ShowsMicCheck); // a scripted run skips it unless asked
        Assert.False(scripted.NeedsSpeech);

        var interactive = Parse();
        Assert.True(interactive.ShowsMicCheck); // an interactive run shows it at the start
        Assert.True(interactive.UsesMicrophone);
        Assert.False(Parse("--no-mic-check").ShowsMicCheck);
        Assert.False(Parse("--no-speech").ShowsMicCheck); // no recognizer: nothing to check
        Assert.True(Parse("--no-mic-check", "--mic-check").ShowsMicCheck); // the last flag wins
        Assert.False(Parse("--mic-check", "--no-mic-check").ShowsMicCheck);
        Assert.False(Parse("--screenshot", "a.png", "--mic-check", "--no-speech").ShowsMicCheck);
    }

    [Fact]
    public void SpeakAndWavAreScriptStepsInTheOrderGivenAndMakeTheScriptedRunLoadTheSpeechModels()
    {
        var options = Parse("--screenshot", "a.png", "--talk", "shopkeeper", "--speak", "quiero agua", "--shot", "s.png", "--wav", "x.wav", "--say", "hola");

        Assert.Equal(
            [ScriptStepKind.Talk, ScriptStepKind.Speak, ScriptStepKind.Shot, ScriptStepKind.Wav, ScriptStepKind.Say],
            options.Script.Select(static s => s.Kind));
        Assert.Equal(["shopkeeper", "quiero agua", "s.png", "x.wav", "hola"], options.Script.Select(static s => s.Value));
        Assert.True(options.HasSpokenSteps);
        Assert.True(options.NeedsSpeech);
        Assert.False(options.UsesMicrophone);
        Assert.False(options.ShowsMicCheck);

        Assert.Contains("need --screenshot", Refuse("--speak", "hola"), StringComparison.Ordinal);
        Assert.Contains("--speak needs", Refuse("--screenshot", "a.png", "--speak", " "), StringComparison.Ordinal);
        Assert.Contains("--wav needs", Refuse("--screenshot", "a.png", "--wav", string.Empty), StringComparison.Ordinal);
        Assert.False(Parse("--screenshot", "a.png", "--speak", "hola", "--no-speech").NeedsSpeech);
    }

    [Fact]
    public void TheScriptedSpeakStepAsksThePanelToSendASpokenLineAndWaitsForTheTurn()
    {
        using var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns, CreditRules.FullTyped);
        Assert.True(session.StartWorker(loadSpeech: false).WaitUntilReady(30000));
        var look = new LookControl();
        var dialogue = new DialogueModel(new InputFocus(), session.OpeningLine, session.Data, session.Phrases);
        var hooks = new ScenarioHooks(
            npc => dialogue.Open(session.OpenTalk(npc), session.Data.Npcs.LabelOf(npc)),
            () =>
            {
                dialogue.Close();
                session.EndTalk();
            },
            _ => { },
            () => false);
        var script = new ScenarioScript(session, look, dialogue, [new ScriptStep(ScriptStepKind.Talk, Value: "shopkeeper"), new ScriptStep(ScriptStepKind.Speak, Value: "quiero agua")], static _ => null, hooks, static _ => { });
        var commands = new List<VoxelCommand>();
        SpeechRequest? asked = null;
        for (var tick = 0; tick < 20000 && asked is null; tick++)
        {
            commands.Clear();
            script.Fill(commands);
            session.Step(commands);
            asked = dialogue.PendingSpeech;
        }

        Assert.NotNull(asked);
        Assert.Equal(new SpeechRequest(TurnSource.Speak, "quiero agua"), asked);
        Assert.False(script.IsDone); // it waits for the turn before it is done
        Assert.Equal("shopkeeper", dialogue.Npc);
    }

    [Fact]
    public void TheOldTabHoldToTalkHintIsReplacedByTheButtonKeysAndTheSpanishStringsAreListedForTheReview()
    {
        Assert.DoesNotContain("Tab (mantener)", DialoguePanel.KeyHints(RouteScene.Shipped.Locale, RouteScene.Shipped.Words.Typing), StringComparison.Ordinal);
        Assert.Contains("Hold V or Space", DialoguePanel.ButtonHints(RouteScene.Shipped.Locale), StringComparison.Ordinal);
        Assert.Equal("¿Escribes?", new DialogueModel(new InputFocus(), "Hola.", RouteScene.Shipped).TypeOfferText);
        Assert.Equal("Heard: (nothing) · Expected: «Hola.»", new HeardLine(string.Empty, "Hola.", "hola").Text(RouteScene.Shipped.Locale));
        Assert.Equal(["Continue", "Try again", "Skip"], new[] { MicCheckButton.Continue, MicCheckButton.Retry, MicCheckButton.Skip }.Select(static b => MicCheckModel.LabelOf(RouteScene.Shipped.Locale, b)));
    }
}
