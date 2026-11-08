using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Language.Book;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Startup;
using Vocaterra.Game.Tests.TestSupport;
using Vocaterra.Game.UI.Dialogue;

namespace Vocaterra.Game.Tests.Component;

/// <summary>
/// The front-end side of S8 without a window: the journal's phrases page (rows, counts, pages), the support layout above the panel, the line player
/// hook for the cards' sounds, the key-word cards before the stall's first question in the scripted run, the new script steps, and the font's
/// coverage of the new Spanish strings.
/// </summary>
public sealed class PhraseSupportFrontendTests
{
    private sealed class RecordingPlayer : ILinePlayer
    {
        public List<(string Npc, string Line, bool Slow)> Played { get; } = [];

        public void Play(string npc, string line, bool slow) => Played.Add((npc, line, slow));
    }

    private static PhraseProgress NewProgress() => new(TestGame.Data.Phrases, 1, TestGame.Data.Npcs.ScaffoldPhrases);

    private static PhraseUse Use(string phrase, PhraseUseKind kind, long time, InputKind input = InputKind.Spoken, IReadOnlyList<string>? inPlay = null) =>
        new(time * 60, time, phrase, kind, "shopkeeper", "stall-need", "line", null, input, true, SupportLevel.None, inPlay);

    private static LaunchOptions Parse(params string[] args)
    {
        Assert.True(LaunchOptions.TryParse(args, out var options, out var error), error);
        return options;
    }

    [Fact]
    public void ThePhrasesPageHasOneRowPerSheetRowWithTheSupportLevelAndTheCounts()
    {
        var progress = NewProgress();
        _ = progress.Apply(Use("pedir-agua", PhraseUseKind.Supported, 1000), CreditRules.Default);
        _ = progress.Apply(Use("hola", PhraseUseKind.Unaided, 1000, InputKind.Typed), CreditRules.Default);
        _ = progress.Apply(Use("no-se", PhraseUseKind.DontKnow, 1010, InputKind.Button, ["donde-esta-el-rio"]), CreditRules.Default);
        progress.NoteHeard("que-necesitas");
        progress.NoteAnswered("que-necesitas");

        var rows = PhraseJournalRows.Build(progress, 1000 + 1200, CreditRules.Default, RouteScene.Shipped.Locale);

        Assert.Equal(173, rows.Count);
        Assert.Equal(progress.Sheet.Rows.Select(static r => r.Id).Order(StringComparer.Ordinal), rows.Select(static r => r.Id).Order(StringComparer.Ordinal));
        Assert.Equal(["que-necesitas", "pedir-agua", "donde-esta-el-rio", "hola"], rows.Take(4).Select(static r => r.Id)); // the phrases met come first, each group in the sheet's order (S17)
        var water = rows.Single(static r => r.Id == "pedir-agua");
        Assert.Equal(("Quiero agua, por favor.", "I want water, please."), (water.Spanish, water.English));
        Assert.Equal("help: card and audio", water.LevelText); // the English rung went with the first correct use
        Assert.Equal("with help 1   without help 0   choices 0   don't know 0   other situation 0", water.CountsText);
        Assert.Equal(("due for review", true), (water.DueText, water.IsDue)); // twenty minutes after its credited use
        var hello = rows.Single(static r => r.Id == "hola");
        Assert.Equal("with help 0   without help 0.5   choices 0   don't know 0   other situation 0", hello.CountsText); // typed: half a use
        Assert.Equal("help: none", hello.LevelText);
        var river = rows.Single(static r => r.Id == "donde-esta-el-rio");
        Assert.Equal(("help: English, card and audio", "with help 0   without help 0   choices 0   don't know 1   other situation 0"), (river.LevelText, river.CountsText));
        var question = rows.Single(static r => r.Id == "que-necesitas");
        Assert.Equal(("heard", "heard 1   answered 1"), (question.LevelText, question.CountsText));
        Assert.True(question.Hears);
        Assert.Null(question.DueText);
    }

    [Fact]
    public void TheBookOpensOnTheNounsPageAndTurnsToThePhrasesWithoutLosingTheWordsRead()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        session.Words.MarkHeard("madera");
        var journal = new BookModel();

        journal.Open(session.Words);

        Assert.Equal(BookPage.Nouns, journal.Page);
        Assert.Equal(1, session.Words.RecordOf("madera").Read); // the label was shown
        journal.Turn(forward: true);
        Assert.Equal(BookPage.Verbs, journal.Page);
        journal.Turn(forward: false);
        Assert.Equal(BookPage.Nouns, journal.Page);
        Assert.Equal(1, session.Words.RecordOf("madera").Read); // once per opening, not per page turn
        journal.Close();
        journal.Show(BookPage.Phrases);
        Assert.Equal(BookPage.Nouns, journal.Page); // a closed book has no page to turn
    }

    [Fact]
    public void ABookOpenedOnThePhrasesPageDoesNotCountTheWordsAsReadUntilTheyAreShown()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns);
        session.Words.MarkHeard("madera");
        var journal = new BookModel();

        journal.Open(session.Words);
        journal.Close();
        var before = session.Words.RecordOf("madera").Read;
        var other = new BookModel();
        other.Open(session.Words); // reads once
        Assert.Equal(before + 1, session.Words.RecordOf("madera").Read);
        other.Show(BookPage.Phrases);
        other.Show(BookPage.Nouns);
        Assert.Equal(before + 1, session.Words.RecordOf("madera").Read);
    }

    [Fact]
    public void TheSupportCardsAndTheWordToastStandClearOfThePanelAndEachOther()
    {
        // The places are the conversation layout's (S22); the sizes are SupportLayout's.
        foreach (var (width, height) in new[] { (1280, 720), (800, 600) })
        {
            var panel = DialogueLayout.PanelBox(width, height);
            var head = ConversationCards.HeadRect(width / 2.0, height * 0.25, (height * 0.25) + 110);
            var requests = new[] { ConversationCards.NewWord, ConversationCards.Support, ConversationCards.Support, ConversationCards.KeyWord(false), ConversationCards.KeyWord(false), ConversationCards.Chip };

            var layout = ConversationLayout.Compute(width, height, head, null, panel, requests);

            var shown = layout.Cards.Where(static c => c.State != CardPresence.Hidden).ToList();
            Assert.NotEmpty(shown);
            Assert.All(shown, c => Assert.False(ConversationLayout.Overlaps(c.Box, panel)));
            Assert.All(shown, c => Assert.True(c.Box.X >= 0 && c.Box.Right <= width && c.Box.Y >= 0));
            Assert.InRange(ConversationCards.Support.Width, SupportLayout.CardMinWidth, SupportLayout.CardMaxWidth);
        }
    }

    [Fact]
    public void TheCardsSoundsGoThroughTheLinePlayerHook()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns, CreditRules.FullTyped);
        var dialogue = new DialogueModel(new InputFocus(), session.OpeningLine, session.Data, session.Phrases);
        var player = new RecordingPlayer();
        var actions = new SpeechActionRunner(session, dialogue, player, () => null);
        var lines = new List<string>();
        var frontend = new SupportFrontend(session, dialogue, actions, lines.Add);
        session.Simulation.TeleportTo(NpcStandOff.Choose(session.Scene.Grid, session.Scene.Npc("shopkeeper").Feet, session.Simulation.Player.FeetBlock)!.Value);

        var opening = session.OpenTalk("shopkeeper");
        dialogue.Open(opening, "Tendero");
        Assert.Null(frontend.Pump());

        Assert.Equal([("shopkeeper", "Puedes decir: «Quiero agua, por favor».", false)], player.Played); // the card appeared: its audio line plays once
        Assert.Null(frontend.Pump());
        Assert.Single(player.Played);
    }

    [Fact]
    public void AnAudioOnlyPromptPlaysItsSoundAndRepetirPlaysTheQuestionThenTheModel()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns, CreditRules.FullTyped);
        var dialogue = new DialogueModel(new InputFocus(), session.OpeningLine, session.Data, session.Phrases);
        var player = new RecordingPlayer();
        var actions = new SpeechActionRunner(session, dialogue, player, () => null);
        var frontend = new SupportFrontend(session, dialogue, actions, _ => { });
        session.Simulation.TeleportTo(NpcStandOff.Choose(session.Scene.Grid, session.Scene.Npc("villager").Feet, session.Simulation.Player.FeetBlock)!.Value);
        dialogue.Open(session.OpenTalk("villager"), "Aldeano");
        _ = frontend.Pump();
        _ = session.Say("¿dónde está el río?"); // english to card-and-audio
        session.EndTalk();
        dialogue.Close();
        session.SkipTime(11 * 60);
        dialogue.Open(session.OpenTalk("villager"), "Aldeano");
        _ = frontend.Pump();
        _ = session.Say("¿dónde está el río?"); // card-and-audio to audio-only, after the gap
        session.EndTalk();
        dialogue.Close();
        session.SkipTime(61 * 60);
        player.Played.Clear();

        dialogue.Open(session.OpenTalk("villager"), "Aldeano");
        _ = frontend.Pump();

        Assert.Equal([("villager", "Puedes preguntar: «¿Dónde está el río?».", false)], player.Played);
        Assert.Empty(session.Support.Cards);
        Assert.NotEmpty(session.Support.AudioOnly); // the audio-only chip is up: no card, an audio-only phrase
        player.Played.Clear();
        Assert.True(actions.Perform(SpeechAction.Repeat));
        Assert.Equal([("villager", "Hola.", false), ("villager", "Puedes preguntar: «¿Dónde está el río?».", false)], player.Played);
    }

    private sealed class FakeOutput : IAudioOutput
    {
        private bool _playing;

        public bool Available => true;

        public bool IsPlaying => _playing;

        public bool Start(Vocaterra.Nlp.Core.Audio.AudioBuffer clip)
        {
            _playing = true;
            return true;
        }

        public void Stop() => _playing = false;

        public void Finish() => _playing = false;

        public void Dispose()
        {
        }
    }

    [Fact]
    public void TheKeyWordAndSupportCardCuesPlayThePreRenderedFilesOfS9()
    {
        var bundle = TestGame.Bundle;
        var lines = SpeechLines.Enumerate(bundle, TestGame.Nouns, RouteScene.DataOf(bundle).Phrases, RouteScene.DataOf(bundle).Npcs);
        var logs = new List<string>();
        var output = new FakeOutput();
        using var voice = new NpcVoice(Path.Combine(GameBundle.ResolveDirectory(), SpeechLines.FolderName), new SpeechIndex(lines), output, muted: false, logs.Add);
        var session = RouteScene.NewSession(bundle, TestGame.Nouns, CreditRules.FullTyped);
        var dialogue = new DialogueModel(new InputFocus(), session.OpeningLine, session.Data, session.Phrases);
        var actions = new SpeechActionRunner(session, dialogue, voice, () => null);
        var frontend = new SupportFrontend(session, dialogue, actions, _ => { });
        session.Simulation.TeleportTo(NpcStandOff.Choose(session.Scene.Grid, session.Scene.Npc("shopkeeper").Feet, session.Simulation.Player.FeetBlock)!.Value);
        dialogue.Open(session.OpenTalk("shopkeeper", teachKeyWords: true), "Tendero");
        voice.Say("shopkeeper", []);

        _ = frontend.Pump(o => voice.Say(o.Npc, o.Lines));
        for (var i = 0; i < 4; i++)
        {
            output.Finish();
            voice.Update();
        }

        session.FinishKeyWords();
        _ = frontend.Pump(o => voice.Say(o.Npc, o.Lines));
        for (var i = 0; i < 4; i++)
        {
            output.Finish();
            voice.Update();
        }

        var played = logs.Where(static l => l.StartsWith("voice: ", StringComparison.Ordinal)).Select(static l => l["voice: ".Length..]).ToList();
        Assert.Equal(["card.agua", "card.pan", "shop_need.1", "shop_help_ask.1"], played); // the two key words, the question, then the model answer of the card
        Assert.Equal((0, 0), (voice.Missing, voice.TextOnly));
    }

    [Fact]
    public void ThePhraseProgressLinesFollowAPhraseUseAndTheSummaryListsWhatWasSeen()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns, CreditRules.FullTyped);
        var dialogue = new DialogueModel(new InputFocus(), session.OpeningLine, session.Data, session.Phrases);
        var actions = new SpeechActionRunner(session, dialogue, SilentLinePlayer.Instance, () => null);
        var frontend = new SupportFrontend(session, dialogue, actions, _ => { });
        session.Simulation.TeleportTo(NpcStandOff.Choose(session.Scene.Grid, session.Scene.Npc("shopkeeper").Feet, session.Simulation.Player.FeetBlock)!.Value);
        _ = session.OpenTalk("shopkeeper");

        _ = session.Say("quiero agua, por favor");

        var use = session.Events.OfType<PhraseUse>().Single();
        Assert.Equal(["phrase: pedir-agua, supported 1, unaided 0, choice 0, no sé 0, transfer 0, fail-forward 0, cards 1, support card-and-audio"], frontend.LinesFor(use));
        Assert.Empty(frontend.LinesFor(session.Events.OfType<WorldEvent>().First()));
        var summary = frontend.Summary();
        Assert.StartsWith("phrases: summary at exit", summary[0], StringComparison.Ordinal);
        Assert.Contains(summary, static l => l.StartsWith("  pedir-agua: supported 1,", StringComparison.Ordinal));
    }

    // The scripted run's loop with the support front-end: the panel opens with the key words as the game does.
    private sealed class Harness : IDisposable
    {
        private readonly List<VoxelCommand> _commands = [];
        private readonly TurnPump _pump;
        private readonly SupportFrontend _support;

        public Harness(IReadOnlyList<ScriptStep> steps)
        {
            Session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns, CreditRules.FullTyped);
            Session.StartWorker(loadSpeech: false).WaitUntilReady(30000);
            Look = new LookControl();
            Look.Set(Session.Scene.Spawn.Yaw, 0);
            Dialogue = new DialogueModel(new InputFocus(), Session.OpeningLine, Session.Data, Session.Phrases);
            _pump = new TurnPump(Session, Dialogue, () => Session.AimedCell);
            var actions = new SpeechActionRunner(Session, Dialogue, Player, () => Session.AimedCell);
            _support = new SupportFrontend(Session, Dialogue, actions, Log.Add);
            var hooks = new ScenarioHooks(OpenTalk, CloseTalk, path => Shots.Add($"{path}@{Session.Simulation.Tick}"), () => false);
            Script = new ScenarioScript(Session, Look, Dialogue, steps, static _ => null, hooks, Log.Add);
        }

        public VoxelSession Session { get; }

        public LookControl Look { get; }

        public DialogueModel Dialogue { get; }

        public ScenarioScript Script { get; }

        public RecordingPlayer Player { get; } = new();

        public List<string> Log { get; } = [];

        public List<string> Shots { get; } = [];

        public void Go(int limit = 20000)
        {
            for (var tick = 0; tick < limit && !Script.IsDone; tick++)
            {
                _commands.Clear();
                Script.Fill(_commands);
                _commands.Insert(0, VoxelCommand.Look(Look.Yaw, Look.Pitch));
                Session.Step(_commands);
                _ = _support.Pump();
                _pump.Submit();
                var deadline = Environment.TickCount64 + 30000;
                while (Session.PendingTurns > 0 && Environment.TickCount64 < deadline)
                {
                    _ = _pump.Apply();
                    Thread.Sleep(1);
                }

                _ = _pump.Apply();
            }

            Assert.True(Script.IsDone, "the script did not finish");
        }

        public void Dispose() => Session.StopWorker();

        private void OpenTalk(string npc)
        {
            var opening = Session.OpenTalk(npc, teachKeyWords: true);
            Dialogue.Open(opening, Session.Data.Npcs.LabelOf(npc), Session.ReviewLine);
        }

        private void CloseTalk()
        {
            Dialogue.Close();
            Session.EndTalk();
        }
    }

    [Fact]
    public void TheStallsFirstTalkShowsTheKeyWordsAloneThenTheQuestionAndThenTakesTheLine()
    {
        using var run = new Harness([new(ScriptStepKind.Talk, Value: "shopkeeper"), new(ScriptStepKind.Shot, Value: "intro.png"), new(ScriptStepKind.Wait, Value: "3"), new(ScriptStepKind.Shot, Value: "exchange.png"), new(ScriptStepKind.Say, Value: "quiero agua, por favor")]);

        run.Go();

        var intro = Assert.Single(run.Shots, static s => s.StartsWith("intro.png", StringComparison.Ordinal));
        var exchange = Assert.Single(run.Shots, static s => s.StartsWith("exchange.png", StringComparison.Ordinal));
        var introTick = long.Parse(intro[(intro.IndexOf('@', StringComparison.Ordinal) + 1)..], System.Globalization.CultureInfo.InvariantCulture);
        var exchangeTick = long.Parse(exchange[(exchange.IndexOf('@', StringComparison.Ordinal) + 1)..], System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(exchangeTick - introTick >= (3 * 60) - 2, $"the --wait step waited three seconds ({introTick} to {exchangeTick})");
        Assert.Equal("¿Qué necesitas?", run.Dialogue.Lines.First(static l => l.Speaker == DialogueSpeaker.Guide).Text); // the question came first
        Assert.Equal(["el agua", "el pan"], run.Session.KeyWordCards.Select(static c => c.Phrase));
        Assert.Contains(run.Dialogue.Lines, static l => l.Speaker == DialogueSpeaker.Player && l.Text == "quiero agua, por favor");
        Assert.Single(run.Player.Played, static p => p.Line == "Puedes decir: «Quiero agua, por favor».");
        Assert.Equal(["el agua", "el pan"], run.Player.Played.Where(static p => p.Line.StartsWith("el ", StringComparison.Ordinal)).Select(static p => p.Line));
        Assert.Equal(PhraseUseKind.Supported, run.Session.Events.OfType<PhraseUse>().Single().Kind);
        Assert.Contains(run.Log, static l => l.StartsWith("talk: shopkeeper (host turn, stall-need): ¿Qué necesitas?", StringComparison.Ordinal));
    }

    [Fact]
    public void TheSkipStepMovesTheStoredTimeOnAndBringsADuePhraseToTheGuide()
    {
        using var run = new Harness(
        [
            new(ScriptStepKind.Talk, Value: "shopkeeper"),
            new(ScriptStepKind.Say, Value: "quiero agua, por favor"),
            new(ScriptStepKind.Skip, Value: "660"),
            new(ScriptStepKind.Talk, Value: "guide"),
        ]);
        var before = run.Session.StoredTime.UnixSeconds;

        run.Go();

        Assert.True(run.Session.StoredTime.UnixSeconds - before >= 660);
        Assert.Contains(run.Log, static l => l.StartsWith("skip: the stored time moves on 660 s", StringComparison.Ordinal));
        Assert.Equal("pedir-agua", run.Session.PhraseReview?.PhraseId);
        Assert.Contains(run.Dialogue.Lines, static l => l.Text == "¿Qué necesitas?" && l.Speaker == DialogueSpeaker.Guide); // the bundle's review line (S11)
    }

    [Fact]
    public void TheWaitAndSkipStepsAndTheJournalPhrasesFlagAreParsedAndRefuseNonsense()
    {
        var options = Parse("--screenshot", "a.png", "--talk", "shopkeeper", "--wait", "3", "--skip", "660", "--journal-phrases");

        Assert.Equal([ScriptStepKind.Talk, ScriptStepKind.Wait, ScriptStepKind.Skip], options.Script.Select(static s => s.Kind));
        Assert.Equal(["shopkeeper", "3", "660"], options.Script.Select(static s => s.Value));
        Assert.True(options.Journal);
        Assert.True(options.JournalPhrases);
        Assert.False(Parse("--screenshot", "a.png", "--journal").JournalPhrases);
        Assert.False(LaunchOptions.TryParse(["--screenshot", "a.png", "--wait", "0"], out _, out var error));
        Assert.Contains("--wait", error, StringComparison.Ordinal);
        Assert.False(LaunchOptions.TryParse(["--screenshot", "a.png", "--skip", "soon"], out _, out _));
    }

    [Fact]
    public void TheFontCoversEveryNewSpanishStringOfThePhraseSupport()
    {
        var cmap = TrueTypeCmap.Load(Repo.Path("assets", "fonts", "pixel-operator", "PixelOperator.ttf"));
        var strings = new List<string>
        {
            RouteScene.Shipped.Words.Support.CardTag, SupportLayer.ChipText(RouteScene.Shipped.Words), RouteScene.Shipped.Words.Support.KeyWordsHeading, BookTabs.Title(RouteScene.Shipped.Locale), BookTabs.KeyHints(RouteScene.Shipped.Locale), BookTabs.LabelOf(RouteScene.Shipped.Locale, BookPage.Phrases),
            RouteScene.Shipped.Locale.Ui("phraseHeard"), RouteScene.Shipped.Locale.Ui("phraseHelpCardAndAudio"), RouteScene.Shipped.Locale.Ui("phraseHelpAudioOnly"), RouteScene.Shipped.Locale.Ui("phraseHelpNone"),
            "with help 1   without help 0.5   choices 0   don't know 0   other situation 0", "heard 1   answered 1", "Without help: 1 of 4", "and 3 more", "audio",
        };
        var sheet = TestGame.Data.Phrases;
        strings.AddRange(sheet.Rows.SelectMany(static r => new[] { r.Spanish, r.English, r.Card }));

        var missing = strings.SelectMany(static s => s).Where(c => !cmap.Covers(c)).Distinct().Select(static c => $"U+{(int)c:X4} {c}").ToList();

        Assert.True(missing.Count == 0, "The font has no glyph for: " + string.Join(", ", missing));
    }
}
