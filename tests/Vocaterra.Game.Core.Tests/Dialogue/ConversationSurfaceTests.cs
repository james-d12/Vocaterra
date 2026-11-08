using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Screens;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Game.Core.Tests.Dialogue;

/// <summary>U02, the conversation surface: the one coaching line, the collapsed older lines and their key, the heard line, the lesson heading in the header, and every D-game-9 route by key in each panel state.</summary>
public sealed class ConversationSurfaceTests : IDisposable
{
    // The sessions of Screen(): each started a turn worker (a thread that holds the session, its scene and a language engine), which only Dispose stops.
    private readonly List<VoxelSession> _sessions = [];

    public void Dispose()
    {
        foreach (var session in _sessions)
        {
            session.Dispose();
        }
    }

    private static GuideTurn Turn(string line, bool spoken = false, bool communicated = true, bool uncertain = false, string? feedback = null, string? offPrompt = null) => new(
        line,
        "request_item",
        communicated,
        1.0,
        [],
        feedback,
        communicated ? ["Toma la botella."] : [],
        null,
        "asking",
        [],
        null,
        null,
        null,
        "digest",
        SlotMap.Empty,
        Input: spoken ? InputKind.Spoken : InputKind.Typed,
        Confidence: spoken ? 0.9 : null,
        Npc: "shopkeeper",
        Prompt: "stall-need",
        Uncertain: uncertain)
    {
        OffPromptIntent = offPrompt,
    };

    private static AppliedTurn Applied(GuideTurn turn)
    {
        var scene = new GuideScene(new GuideViewpoint(new BlockPos(0, 0, 0), 0, 0, 0));
        var request = new TurnRequest(1, TurnSource.Clip, null, null, null, scene, new Vocaterra.Nlp.Core.Determinism.StoredTimestamp(1), "digest", null, 0, "shopkeeper");
        return new AppliedTurn(new TurnOutcome(request, turn, null, new TurnTimings(0, 0, 5, false), false), null);
    }

    private static (DialogueModel Dialogue, VoxelSession Session) Panel()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        var dialogue = new DialogueModel(new InputFocus(), session.OpeningLine, session.Data, session.Phrases, session.Locale);
        dialogue.Open(new NpcOpening("shopkeeper", "stall-need", ["¿Qué necesitas?"], true, [], false), "Tendero");
        return (dialogue, session);
    }

    private static void Say(DialogueModel dialogue, GuideTurn turn)
    {
        dialogue.BeginSpoken();
        _ = dialogue.Complete(Applied(turn));
    }

    // ---- The coaching line: one, by case.

    [Fact]
    public void AnUncertainSpokenTurnCoachesWithTheMicrophonesTroubleAndNothingElse()
    {
        var (dialogue, session) = Panel();
        var turn = Turn("quiero agua", spoken: true, communicated: false, uncertain: true);
        Say(dialogue, turn);

        Assert.Equal(session.Locale.FeedbackFor(turn)[0], dialogue.CoachingLine);
        Assert.Single(dialogue.PanelRows(), static r => r.Line.Speaker == DialogueSpeaker.Coaching);
    }

    [Fact]
    public void ATurnTheNpcDidNotUnderstandCoachesWithTheNotUnderstoodLine()
    {
        var (dialogue, session) = Panel();
        var turn = Turn("blorp", communicated: false);
        Say(dialogue, turn);

        Assert.Equal(session.Locale.FeedbackFor(turn)[0], dialogue.CoachingLine);
        Assert.Single(dialogue.PanelRows(), static r => r.Line.Speaker == DialogueSpeaker.Coaching);
    }

    [Fact]
    public void ATypedLineOffThePromptCoachesWithTheOffPromptNoteButASpokenOneDoesNot()
    {
        var (dialogue, session) = Panel();
        Say(dialogue, Turn("¿dónde está el río?", communicated: false, offPrompt: "ask_place"));
        Assert.Equal(session.Locale.Ui("coachOffPrompt"), dialogue.CoachingLine);

        Say(dialogue, Turn("¿dónde está el río?", spoken: true, communicated: false, offPrompt: "ask_place"));
        Assert.Null(dialogue.CoachingLine);
    }

    // U02b (QA-2026-11-06-ui-3): the English after a turn says what the NPC did: understood, understood off the prompt, or did not understand.
    [Fact]
    public void TheEnglishOfAnOffPromptTurnNeitherPraisesNorSaysNotUnderstood()
    {
        var (_, session) = Panel();
        var locale = session.Locale;
        var offPrompt = locale.FeedbackFor(Turn("gracias", communicated: false, offPrompt: "thank"))[0];
        Assert.Contains("not what was asked", offPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Good", offPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("did not understand", offPrompt, StringComparison.Ordinal);

        var notUnderstood = locale.FeedbackFor(Turn("blah", communicated: false))[0];
        Assert.Contains("did not understand", notUnderstood, StringComparison.Ordinal);

        var understood = locale.FeedbackFor(Turn("quiero agua", communicated: true))[0];
        Assert.StartsWith("Good.", understood, StringComparison.Ordinal);
        Assert.NotEqual(offPrompt, notUnderstood);
    }

    // U02b (ui-4): a typed turn the NPC did not take gets the not-understood line, never the off-prompt claim.
    [Fact]
    public void ATypedTurnNotTakenGetsTheNotUnderstoodCoachingNotTheOffPromptNote()
    {
        var (dialogue, session) = Panel();
        var turn = Turn("blah", communicated: false);
        Say(dialogue, turn);
        Assert.Equal(session.Locale.FeedbackFor(turn)[0], dialogue.CoachingLine);
        Assert.NotEqual(session.Locale.Ui("coachOffPrompt"), dialogue.CoachingLine);
    }

    [Fact]
    public void AnUnderstoodLineKeepsItsCoachingAndItsPraise()
    {
        var (dialogue, session) = Panel();
        var understood = Turn("quiero agua", communicated: true);
        Say(dialogue, understood);
        Assert.Contains(dialogue.Lines, line => line.Speaker == DialogueSpeaker.English && line.Text == session.Locale.FeedbackFor(understood)[0]);
        Assert.Null(dialogue.CoachingLine);
    }

    // U02b (QA-2026-11-06-ui-1): the expanded page scrolls back to its oldest lines and forward to its newest, and never past either end.
    [Fact]
    public void TheExpandedPageScrollsBackToItsOldestLinesAndForwardAgainWithinItsEnds()
    {
        var (dialogue, _) = Panel();
        for (var i = 0; i < 4; i++)
        {
            Say(dialogue, Turn("gracias"));
        }

        dialogue.ScrollOlder();
        Assert.Equal(0, dialogue.ScrollBack);
        dialogue.ToggleExpanded();
        Assert.Equal(0, dialogue.ScrollBack);
        dialogue.ScrollOlder();
        Assert.Equal(1, dialogue.ScrollBack);
        for (var i = 0; i < 200; i++)
        {
            dialogue.ScrollOlder();
        }

        Assert.Equal(dialogue.PanelRows().Count - 1, dialogue.ScrollBack);
        for (var i = 0; i < 200; i++)
        {
            dialogue.ScrollNewer();
        }

        Assert.Equal(0, dialogue.ScrollBack);
        dialogue.ScrollOlder();
        dialogue.ToggleExpanded();
        Assert.Equal(0, dialogue.ScrollBack);
    }

    [Fact]
    public void ATurnTheLessonTookIsNeverCoachedAsOffThePrompt()
    {
        var (dialogue, _) = Panel();
        Say(dialogue, Turn("sí", communicated: false, offPrompt: "agree") with { LessonHandled = true });
        Assert.Null(dialogue.CoachingLine);
    }

    [Fact]
    public void ASlipCoachesWithTheOneRecastAndTheCorrectionAndEnglishStackIsNotShownAlongsideIt()
    {
        var (dialogue, _) = Panel();
        Say(dialogue, Turn("quiero agua", spoken: true, feedback: "Se dice «quiero un agua»."));

        Assert.Equal("Se dice «quiero un agua».", dialogue.CoachingLine);
        var rows = dialogue.PanelRows();
        Assert.Single(rows, static r => r.Line.Speaker == DialogueSpeaker.Coaching);
        Assert.DoesNotContain(rows, static r => r.Line.Speaker is DialogueSpeaker.Correction or DialogueSpeaker.English or DialogueSpeaker.Accent);
        Assert.Contains(dialogue.Lines, static l => l.Speaker == DialogueSpeaker.Correction); // the history keeps it
    }

    [Fact]
    public void ATurnWithNothingToCoachShowsNoCoachingLineAndTheNextTurnReplacesTheLast()
    {
        var (dialogue, _) = Panel();
        Say(dialogue, Turn("quiero agua", feedback: "Se dice «quiero un agua»."));
        Assert.NotNull(dialogue.CoachingLine);

        Say(dialogue, Turn("quiero agua, por favor"));
        Assert.Null(dialogue.CoachingLine);
        Assert.DoesNotContain(dialogue.PanelRows(), static r => r.Line.Speaker == DialogueSpeaker.Coaching);
    }

    // ---- The older lines.

    [Fact]
    public void OnlyTheCurrentExchangeShowsByDefaultAndTheExpandKeyShowsTheRestAndTheHistoryIsUnchanged()
    {
        var (dialogue, _) = Panel();
        for (var i = 0; i < 3; i++)
        {
            Say(dialogue, Turn($"línea {i}", spoken: true));
        }

        var all = dialogue.Lines.ToList();
        var collapsed = dialogue.PanelRows();
        Assert.Equal("línea 2", collapsed[0].Line.Text); // the learner's last line starts the exchange
        Assert.Equal(dialogue.ExchangeStart, collapsed[0].Index);
        Assert.All(collapsed, r => Assert.True(r.Index < 0 || r.Index >= dialogue.ExchangeStart));
        Assert.Equal(dialogue.ExchangeStart, dialogue.HiddenLineCount);
        Assert.True(dialogue.HiddenLineCount > 0);
        Assert.Contains(collapsed, static r => r.Line.Speaker == DialogueSpeaker.Guide); // the NPC's current line is there

        dialogue.ToggleExpanded();
        var expanded = dialogue.PanelRows();
        Assert.Equal(all.Count, expanded.Count);
        Assert.Equal(0, dialogue.HiddenLineCount);
        Assert.Equal(all, dialogue.Lines); // the book's history is the lines and the log, not the panel's rows

        dialogue.ToggleExpanded();
        Assert.Equal(collapsed.Count, dialogue.PanelRows().Count);
    }

    [Fact]
    public void BeforeTheLearnerHasSpokenEveryLineShowsAndAReopenedPanelStartsCollapsed()
    {
        var (dialogue, _) = Panel();
        Assert.Equal(dialogue.Lines.Count, dialogue.PanelRows().Count);
        Assert.Equal(0, dialogue.HiddenLineCount);

        Say(dialogue, Turn("quiero agua", spoken: true));
        dialogue.ToggleExpanded();
        dialogue.Close();
        dialogue.Open(new NpcOpening("shopkeeper", "stall-need", ["¿Qué necesitas?"], true, [], false), "Tendero");
        Assert.False(dialogue.Expanded);
    }

    // ---- The heard / expected line.

    [Fact]
    public void TheHeardLineShowsAfterEverySpokenTurnAndIsGoneAtTheNextTypedOne()
    {
        var (dialogue, _) = Panel();
        for (var i = 0; i < 3; i++)
        {
            Say(dialogue, Turn("quiero agua", spoken: true));
            Assert.NotNull(dialogue.HeardText);
            Assert.Contains("quiero agua", dialogue.HeardText!, StringComparison.OrdinalIgnoreCase);
        }

        dialogue.Entry.Insert('s');
        dialogue.Entry.Insert('í');
        _ = dialogue.Submit(_ => Turn("sí"));
        Assert.Null(dialogue.HeardText);
    }

    [Fact]
    public void EllipsizeKeepsAShortTextCutsALongOneAtAWordEdgeAndGivesNothingWhenNothingFits()
    {
        float measure(string t) => t.Length * 10f;
        Assert.Equal("abc", TextWrap.Ellipsize("abc", 30, measure));
        Assert.Equal("abc…", TextWrap.Ellipsize("abc def ghi", 40, measure));
        Assert.Equal(string.Empty, TextWrap.Ellipsize("abc def ghi", 5, measure));
    }

    // ---- Every D-game-9 route by key, in each panel state.

    private (DialogueScreen Screen, DialogueModel Dialogue, VoxelSession Session) Screen()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        _sessions.Add(session);
        var dialogue = new DialogueModel(new InputFocus(), session.OpeningLine, session.Data, session.Phrases, session.Locale);
        var voice = new NpcVoice(Path.GetTempPath(), new SpeechIndex([]), new SilentOutput(), muted: true, _ => { });
        var sound = new SoundFrontend(new SoundMixer(new NullSoundOutput(), new SoundVolumes(), new PresentationRng(1)), new SoundEventMapper());
        var screen = new DialogueScreen(
            dialogue,
            session,
            voice,
            new SpeechActionRunner(session, dialogue, voice, () => null),
            new LessonFrontend(session, dialogue, _ => { }),
            new ListenSession(new PushToTalkSpeechCapture(null)),
            new OpenKeySwallow(),
            new TextEditKeys(),
            sound,
            static (_, _) => throw new InvalidOperationException("no view in this test"),
            static (_, _) => throw new InvalidOperationException("no font in this test"),
            static () => null,
            scriptedRun: true,
            _ => { });
        _ = session.StartWorker(loadSpeech: false); // the buttons that send a turn (Ayuda, No sé, a choice) need the worker
        screen.Open("shopkeeper", 'e');
        dialogue.AddNpcLine("¿Qué necesitas?"); // Repetir and Más despacio say her last line again
        return (screen, dialogue, session);
    }

    private static TestInputFrame Pressed(GameKey key) => TestInputFrame.Pressing(key);

    private static IEnumerable<VoiceEventKind> Voice(VoxelSession session) => session.Events.OfType<VoiceEvent>().Select(static e => e.Kind);

    public static TheoryData<string, GameKey, VoiceEventKind> Routes => new()
    {
        { "Repetir", GameKey.R, VoiceEventKind.Replay },
        { "Más despacio", GameKey.S, VoiceEventKind.Slower },
        { "Ayuda", GameKey.H, VoiceEventKind.Hint },
        { "No sé", GameKey.N, VoiceEventKind.NoSe },
        { "Escribir (W)", GameKey.W, VoiceEventKind.TypeFallback },
        { "Escribir (Tab)", GameKey.Tab, VoiceEventKind.TypeFallback },
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public void EveryButtonRouteWorksByKeyInTheIdleState(string route, GameKey key, VoiceEventKind kind)
    {
        var (screen, dialogue, session) = Screen();
        screen.HandleInput(Pressed(key));
        Assert.Contains(kind, Voice(session));
        Assert.Equal(key is GameKey.W or GameKey.Tab, dialogue.IsTyping);
        Assert.False(string.IsNullOrEmpty(route));
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void EveryButtonRouteWorksByKeyAfterATurnWithItsHeardLine(string route, GameKey key, VoiceEventKind kind)
    {
        var (screen, dialogue, session) = Screen();
        Say(dialogue, Turn("quiero agua", spoken: true));
        Assert.NotNull(dialogue.HeardText);
        screen.HandleInput(Pressed(key));
        Assert.Contains(kind, Voice(session));
        Assert.False(string.IsNullOrEmpty(route));
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void EveryButtonRouteWorksByKeyWhileATurnIsThinkingAndTypeDropsTheTurn(string route, GameKey key, VoiceEventKind kind)
    {
        var (screen, dialogue, session) = Screen();
        dialogue.BeginSpoken();
        Assert.True(dialogue.IsThinking);
        screen.HandleInput(Pressed(key));
        if (key is GameKey.W or GameKey.Tab)
        {
            Assert.True(dialogue.IsTyping);
            Assert.False(dialogue.IsThinking); // choosing to type drops the turn that was thinking
        }

        Assert.Contains(kind, Voice(session));
        Assert.False(string.IsNullOrEmpty(route));
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void EveryButtonRouteWorksByKeyAfterTheTimeoutOffersTyping(string route, GameKey key, VoiceEventKind kind)
    {
        var (screen, dialogue, session) = Screen();
        dialogue.BeginSpoken();
        dialogue.OfferTyping(); // what the watchdog does at five seconds
        Assert.True(dialogue.TypingOffered);
        screen.HandleInput(Pressed(key));
        Assert.Contains(kind, Voice(session));
        Assert.False(string.IsNullOrEmpty(route));
    }

    [Fact]
    public void EnterPressesEscribirWhenTypingIsOfferedAndEscCancelsTheThinkingTurnThenTheTextBoxThenThePanel()
    {
        var (screen, dialogue, session) = Screen();
        dialogue.BeginSpoken();
        dialogue.OfferTyping();
        screen.HandleInput(Pressed(GameKey.Enter));
        Assert.True(dialogue.IsTyping);
        Assert.Contains(VoiceEventKind.TypeFallback, Voice(session));

        screen.HandleInput(Pressed(GameKey.Escape)); // the turn that thinks is cancelled first
        Assert.True(dialogue.IsTyping || !dialogue.IsThinking);
        screen.HandleInput(Pressed(GameKey.Escape));
        screen.HandleInput(Pressed(GameKey.Escape));
        Assert.False(dialogue.IsOpen);
    }

    [Fact]
    public void EscCancelsAThinkingTurnAtOnceWithoutClosingThePanel()
    {
        var (screen, dialogue, _) = Screen();
        dialogue.BeginSpoken();
        screen.HandleInput(Pressed(GameKey.Escape));
        Assert.False(dialogue.IsThinking);
        Assert.True(dialogue.IsOpen);
    }

    [Fact]
    public void TheChoiceKeysOneToThreeAnswerTheOfferedChoicesAfterATurn()
    {
        var (screen, dialogue, _) = Screen();
        var offered = Turn("quiero", spoken: true, communicated: false) with { Choices = ["quiero agua"] };
        Say(dialogue, offered);
        Assert.NotEmpty(dialogue.Choices);

        screen.HandleInput(Pressed(GameKey.D1));
        Assert.True(dialogue.IsThinking);
        Assert.Contains(dialogue.Lines, l => l.Speaker == DialogueSpeaker.Player);
    }

    // ---- The expand key, in each state.

    [Theory]
    [InlineData(GameKey.PageUp)]
    [InlineData(GameKey.O)]
    public void TheExpandKeyTogglesTheOlderLinesInTheIdleAndThinkingStates(GameKey key)
    {
        var (screen, dialogue, _) = Screen();
        screen.HandleInput(Pressed(key));
        Assert.True(dialogue.Expanded);
        dialogue.BeginSpoken();
        screen.HandleInput(Pressed(key));
        Assert.False(dialogue.Expanded);
    }

    [Fact]
    public void PageUpExpandsInTheTextBoxTooButTheLetterOIsJustTyped()
    {
        var (screen, dialogue, _) = Screen();
        screen.HandleInput(Pressed(GameKey.W));
        Assert.True(dialogue.IsTyping);
        screen.HandleInput(Pressed(GameKey.O));
        Assert.False(dialogue.Expanded);
        screen.HandleInput(Pressed(GameKey.PageUp));
        Assert.True(dialogue.Expanded);
        Assert.True(dialogue.IsTyping); // the key leaves what was being typed alone
    }

    // ---- U06: the wheel, in each state.

    private static TestInputFrame Wheel(int units) => new() { Wheel = units };

    [Fact]
    public void TheWheelExpandsThePanelThenScrollsOlderAndNewerAndFoldsItBackAtTheNewestLine()
    {
        var (screen, dialogue, _) = Screen();
        for (var i = 0; i < 3; i++)
        {
            Say(dialogue, Turn("gracias"));
        }

        screen.HandleInput(Wheel(120)); // a notch up on the collapsed panel: as PageUp
        Assert.True(dialogue.Expanded);
        Assert.Equal(0, dialogue.ScrollBack);
        screen.HandleInput(Wheel(120));
        screen.HandleInput(Wheel(120));
        Assert.Equal(2, dialogue.ScrollBack); // one block a notch
        screen.HandleInput(Wheel(-120));
        Assert.Equal(1, dialogue.ScrollBack);
        screen.HandleInput(Wheel(-120));
        Assert.Equal(0, dialogue.ScrollBack);
        Assert.True(dialogue.Expanded);
        screen.HandleInput(Wheel(-120)); // down at the newest line: back to the current exchange
        Assert.False(dialogue.Expanded);
        screen.HandleInput(Wheel(-120)); // and nothing more on the collapsed panel
        Assert.False(dialogue.Expanded);
    }

    [Fact]
    public void TheWheelWorksWhileTypingAndTypesNothing()
    {
        var (screen, dialogue, _) = Screen();
        screen.HandleInput(Pressed(GameKey.W));
        Assert.True(dialogue.IsTyping);
        screen.HandleInput(Wheel(120));
        Assert.True(dialogue.Expanded);
        Assert.True(dialogue.IsTyping);
        Assert.Equal(string.Empty, dialogue.Entry.Text);
    }

    [Fact]
    public void TheWorldsHotbarDoesNotSeeTheWheelWhileThePanelIsOpen()
    {
        // VoxelGame reads the hotbar's wheel only when no screen frees the mouse (WorldInput.Read: uiOpen ? null : Slot(...)); the open dialogue is such a screen.
        var (screen, _, _) = Screen();
        var stack = new ScreenStack([screen], [screen]);
        Assert.True(screen.IsOpen);
        Assert.True(stack.FreesMouse);
        Assert.True(stack.HandleInput(Wheel(120))); // the screen takes the frame: the world's keys do not run
        screen.Close();
        Assert.False(stack.FreesMouse);
    }

    private sealed class SilentOutput : IAudioOutput
    {
        public bool Available => false;

        public bool IsPlaying => false;

        public bool Start(AudioBuffer clip) => false;

        public void Stop()
        {
        }

        public void Dispose()
        {
        }
    }
}
