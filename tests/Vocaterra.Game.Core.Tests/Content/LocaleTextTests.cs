using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Game.Core.Tests.Content;

/// <summary>
/// The support language of S16 (D-game-13), in the locale bundle since C03 (<c>content/locale/en</c>): it loads and is complete, every line an NPC can say has its
/// gloss by its speech-line id, the controls and tips are there, the objective line is bilingual and drops each gloss once its noun is known, and the feedback
/// after a turn is keyed by the core's error codes and by the outcome, with a fallback for a code the locale does not know. A missing string shows as its marker.
/// </summary>
public sealed class LocaleTextTests
{
    private static LocaleText English => TestGame.Locale;

    [Fact]
    public void TheFileLoadsAndHasTheControlsTheTipsAndEveryInterfaceWord()
    {
        var english = English;
        var data = TestGame.Data;

        Assert.True(data.Controls.Count >= 10);
        Assert.Contains(data.Controls, static c => c.Id == "look" && c.Text.StartsWith("Look around", StringComparison.Ordinal));
        Assert.Contains(data.Controls, static c => c.Id == "help" && c.Keys.Contains('H', StringComparison.Ordinal));
        Assert.Contains(data.Tips, static t => t is { When: "start", Text: "Move the mouse to look around. The valley below is where you are headed." });
        Assert.Equal(["look", "walk", "jump", "note", "gather"], data.Tips.Where(static t => t.When == "start").Select(static t => t.Id)); // the opening (S17)
        Assert.Equal([0, 9, 18, 28, 50], data.Tips.Where(static t => t.When == "start").Select(static t => t.DelaySeconds)); // paced so they do not stack up
        Assert.Contains(data.Tips, static t => t.When == "aimed_npc");
        Assert.All(LocaleText.UiKeys, key => Assert.False(english.Ui(key).StartsWith('\u27e6'), key));
        Assert.Equal("step 2 of 7", english.Ui("stepOf", ("n", "2"), ("total", "7")));
        Assert.All(data.Tips, static t => Assert.Contains(t.When, TipBook.TipTriggers));
    }

    [Fact]
    public void EveryLineAnNpcCanSayHasItsGlossByItsSpeechLineId()
    {
        var english = English;
        var missing = TestGame.Speech
            .Where(static l => l.Kind is SpeechKind.Dialogue or SpeechKind.Prompt)
            .Where(l => english.Line(l.LineId) is null)
            .Select(static l => $"{l.LineId}: {l.Text}")
            .ToList();

        Assert.Empty(missing);
        Assert.Equal("What do you need?", english.Line("prompt.stall-need"));
        Assert.Equal("What do you need?", TestGame.Data.SupportForLine("shopkeeper", "¿Qué necesitas?"));
        Assert.Equal("What do you need?", TestGame.Data.SupportForLine("shopkeeper", "que necesitas")); // the panel's text finds its line without accents, capitals or marks
        Assert.Null(TestGame.Data.SupportForLine("guide", "El río está allí, a la izquierda, a sesenta bloques.")); // a templated world line has no fixed gloss
    }

    [Fact]
    public void AStringIsLookedForInTheLocaleThenItsFallbackAndAMissingOneShowsItsMarker()
    {
        var english = English;
        var overlay = english.With(new Dictionary<string, string>(StringComparer.Ordinal) { ["ui.continue"] = "Go on", ["test.only"] = "only here" });

        Assert.Equal("Continue", english.Ui("continue"));
        Assert.Equal("Go on", overlay.Ui("continue"));
        Assert.Equal("only here", overlay.Text("test.only"));
        Assert.Equal("Skip", overlay.Ui("skip")); // the locale's own strings stay under the overlay
        Assert.Null(english.Find("no.such.id"));
        Assert.Equal("\u27e6no.such.id\u27e7", english.Text("no.such.id"));
        Assert.Equal("en", english.Code);
        Assert.Null(english.Fallback);
        Assert.Equal(new VerbForms("to have", "have", "has", "had"), english.Verb("tener"));
        Assert.Equal(new VerbForms("to want", "want", "wants", "wanted"), english.Verb("querer"));
    }

    [Fact]
    public void EveryPhraseTheGameTeachesHasAnEnglishExplanationAndTheOnesWithoutAScaffoldToo()
    {
        var english = English;
        var sheet = RouteScene.DataOf(TestGame.Bundle).Phrases;

        Assert.All(sheet.Rows, row => Assert.False(string.IsNullOrWhiteSpace(english.Explanation(row.Id)), row.Id));
        Assert.Null(english.Explanation("no-such-phrase"));
    }

    [Fact]
    public void TheObjectiveIsBilingualAtFirstAndEachEnglishWordGoesOnceItsNounIsKnown()
    {
        var english = English;
        var objective = Objective.Camp();

        Assert.Equal(
            "Campamento (camp): madera (wood) 0/5 · frutas (fruit) 0/3 · antorcha (torch) 0/1 · agua (water) 0/1",
            english.ObjectiveLine(objective, static _ => false));
        Assert.Equal(
            "Campamento (camp): madera 0/5 · frutas (fruit) 0/3 · antorcha (torch) 0/1 · agua (water) 0/1",
            english.ObjectiveLine(objective, static noun => noun == "madera"));
        Assert.Equal(
            "Campamento (camp): madera 0/5 · frutas 0/3 · antorcha 0/1 · agua (water) 0/1",
            english.ObjectiveLine(objective, static noun => noun != "agua"));
        Assert.Equal(
            "Campamento: madera 0/5 · frutas 0/3 · antorcha 0/1 · agua 0/1",
            english.ObjectiveLine(objective, static _ => true)); // the title's English goes with the last word's
        Assert.Equal(objective.Summary, english.ObjectiveLine(objective, static _ => true)); // and then it is the Spanish line the objective reports
    }

    [Fact]
    public void TheObjectivePiecesCarryTheKindForTheColoursAndTheDoneFlag()
    {
        var english = English;
        var objective = Objective.Camp();
        _ = objective.RecordPickup(ItemId.Wood, 5);

        var pieces = english.ObjectivePieces(objective, static _ => false);

        Assert.Contains(pieces, static p => p is { Text: " (wood)", Kind: ObjectivePieceKind.Support, Done: true });
        Assert.Contains(pieces, static p => p is { Text: " (fruit)", Kind: ObjectivePieceKind.Support, Done: false });
        Assert.Equal(ObjectivePieceKind.Title, pieces[0].Kind);
    }

    [Fact]
    public void TheObjectiveInASessionDropsAnEnglishWordWhenTheWordsRecordIsKnown()
    {
        var session = TestGame.NewSession();
        Assert.Contains("madera (wood)", session.Locale.ObjectiveLine(session.Objective, lemma => session.Words.RecordOf(lemma).Known), StringComparison.Ordinal);

        session.Words.Restore([new WordRecord("madera", CreditUnits: WordProgress.UnitsToKnow)]);

        var line = session.Locale.ObjectiveLine(session.Objective, lemma => session.Words.RecordOf(lemma).Known);
        Assert.StartsWith("Campamento (camp): madera 0/5 · frutas (fruit)", line, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryErrorCodeOfTheCoreHasItsOwnLineAndTheLineMentionsTheFormsItNeeds()
    {
        var english = English;

        Assert.Empty(english.MissingErrorTags());
        foreach (var tag in ErrorTags.All)
        {
            Assert.True(english.HasErrorLine(tag), tag);
            var line = english.ErrorLine(new GuideError(tag, "agua", "aguas", new TextSpan(0, 1)));
            Assert.DoesNotContain("{", line, StringComparison.Ordinal); // every placeholder was filled
            Assert.NotEqual(english.ErrorLine(new GuideError("zzz.unknown", "agua", "aguas", new TextSpan(0, 1))), line); // not the fallback
        }

        Assert.Equal(
            "One or more than one? The noun and the words next to it must match. Say “aguas” instead of “agua”.",
            english.ErrorLine(new GuideError(ErrorTags.NounNumber, "aguas", "agua", new TextSpan(0, 1))));
    }

    [Fact]
    public void AnUnknownErrorCodeAndALineThatNeedsAFormTheCoreDidNotGiveGetTheFallbackLine()
    {
        var english = English;

        var unknown = english.ErrorLine(new GuideError("zzz.new_code", "uno", "dos", new TextSpan(0, 1)));
        var noForm = english.ErrorLine(new GuideError(ErrorTags.VocabWrong, string.Empty, "xyz", new TextSpan(0, 1)));

        Assert.Equal("There was a small mistake in “dos”. Listen to the guide's version and try again.", unknown);
        Assert.Equal("There was a small mistake in “xyz”. Listen to the guide's version and try again.", noForm);
        Assert.False(english.HasErrorLine("zzz.new_code"));
    }

    private static (VoxelSession Session, LocaleText English) AtStall()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalk("shopkeeper");
        return (session, session.Locale);
    }

    [Fact]
    public void TheFeedbackFollowsTheOutcomeOfARealTurn()
    {
        var (slipSession, english) = AtStall();
        var slipLines = english.FeedbackFor(slipSession.Say("yo quieres agua, por favor"));
        Assert.Equal("The shopkeeper understood you, but there was a small mistake. That is fine: keep going.", slipLines[0]);
        Assert.Contains(slipLines, static l => l.StartsWith("The verb ending must match who is speaking", StringComparison.Ordinal));
        Assert.True(slipLines.Count <= 1 + LocaleText.MaxErrorLines);

        var (lostSession, _) = AtStall();
        Assert.Equal(["The shopkeeper did not understand that. Try again, press H for help, or press W to type."], english.FeedbackFor(lostSession.Say("zzz qqq")));

        var (helpSession, _) = AtStall();
        Assert.Equal(["Here is some help. Try the phrase on the card."], english.FeedbackFor(helpSession.Say("ayuda")));

        var (goodSession, _) = AtStall();
        Assert.Equal(["Good. The shopkeeper understood you."], english.FeedbackFor(goodSession.Say("quiero agua, por favor")));
    }

    [Fact]
    public void ADontKnowAnUncertainSpokenLineAChoiceAFailForwardAndAPointerEachHaveTheirOwnPlainLine()
    {
        var session = TestGame.NewSession();
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalk("villager");
        var english = session.Locale;
        var turn = session.Say("¿dónde está el río?");
        Assert.Equal(["Good. The villager understood you."], english.FeedbackFor(turn with { Pointer = null }));

        var dont = session.Say("no sé");
        Assert.Equal(["That is fine. Not knowing is part of learning. Here is the phrase to say."], english.FeedbackFor(dont));

        var uncertain = turn with { Communicated = false, Uncertain = true, Input = InputKind.Spoken, Intent = "unknown" };
        Assert.Equal(["I could not hear that clearly. That is the microphone, not your Spanish. Try again, or press W to type."], english.FeedbackFor(uncertain));
        Assert.DoesNotContain("mistake", string.Concat(english.FeedbackFor(uncertain)), StringComparison.Ordinal); // never the language's failure (D-game-9)

        Assert.Equal(["You picked one of the offered phrases. Next time, try saying it yourself."], english.FeedbackFor(turn with { Choice = true, Pointer = null }));
        var forward = english.FeedbackFor(turn with { FailForward = true, Pointer = null });
        Assert.Equal("That is fine. The game carries on: you do not need to say it perfectly to continue.", forward[^1]);
        var pointed = english.FeedbackFor(turn with { Pointer = new GuidePointer("río", new GuideCell(1, 2, 3), 42, "a la izquierda") });
        Assert.Equal("The villager points to the river, 42 blocks away. Follow the arrow.", pointed[^1]);
    }

    [Fact]
    public void ALocaleFileThatIsMalformedOrGivesAnIdTwiceIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => LocaleText.Parse("xx", "h", [("a.json", "{ not json")]));
        Assert.Throws<InvalidDataException>(() => LocaleText.Parse("xx", "h", [("a.json", """{ "strings": { "ui.x": "" } }""")]));
        Assert.Throws<InvalidDataException>(() => LocaleText.Parse("xx", "h", [("a.json", """{ "strings": { "ui.x": "A" } }"""), ("b.json", """{ "strings": { "ui.x": "B" } }""")]));
        Assert.Throws<InvalidDataException>(() => LocaleText.Parse("xx", "h", [("a.json", """{ "strings": { "verb.ir": { "gloss": "to go" } } }""")]));
        Assert.Equal("A", LocaleText.Parse("xx", "h", [("a.json", """{ "strings": { "ui.x": "A" } }""")]).Text("ui.x"));
    }

    [Fact]
    public void AnUnknownTipTriggerAndAMissingTipTextAreRefused()
    {
        var tips = File.ReadAllText(Repo.Path("content", "game-v1", "game", "tips.json"));

        Assert.Throws<InvalidDataException>(() => TipBook.ParseTips(tips.Replace("\"event\": \"start\"", "\"event\": \"never_happens\"", StringComparison.Ordinal), English));
        Assert.Throws<InvalidDataException>(() => TipBook.ParseTips(tips.Replace("\"id\": \"look\"", "\"id\": \"look-x\"", StringComparison.Ordinal), English));
        Assert.Throws<InvalidDataException>(() => TipBook.ParseTips(tips.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal), English));
        Assert.Equal(TestGame.Data.Tips, TipBook.ParseTips(tips, English));
    }

    [Fact]
    public void TheLocaleBundleRefusesAFileThatDiffersFromItsManifest()
    {
        var copy = Path.Combine(Path.GetTempPath(), "vocaterra-locale-" + Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + nameof(TheLocaleBundleRefusesAFileThatDiffersFromItsManifest));
        try
        {
            CopyDirectory(Repo.Path("content", "locale"), copy);
            Assert.Equal(TestGame.Locale.BundleHash, LocaleBundle.Load(copy).BundleHash);

            File.AppendAllText(Path.Combine(copy, "en", "ui.json"), " ");
            var changed = Assert.Throws<InvalidDataException>(() => LocaleBundle.Load(copy));
            Assert.Contains("en/ui.json: differs from the manifest", changed.Message, StringComparison.Ordinal);

            File.WriteAllText(Path.Combine(copy, "en", "extra.json"), """{ "strings": {} }""");
            var extra = Assert.Throws<InvalidDataException>(() => LocaleBundle.Load(copy));
            Assert.Contains("en/extra.json: on disk, not in the manifest", extra.Message, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(copy))
            {
                Directory.Delete(copy, recursive: true);
            }
        }
    }

    [Fact]
    public void TheShippedLocalesAreTheDefaultOneAndItIsGameV1sSupportLanguage()
    {
        var locales = LocaleBundle.Load(Repo.Path("content", "locale"));

        Assert.Equal(["en"], locales.Locales.Select(static l => l.Code));
        Assert.Equal("en", TestGame.Bundle.Manifest.SupportLanguage);
        Assert.Equal("en", LocaleBundle.ChooseCode(null, TestGame.Bundle));
        Assert.Equal("fr", LocaleBundle.ChooseCode("fr", TestGame.Bundle));
        Assert.Throws<ArgumentException>(() => locales.Text("fr"));
    }

    [Fact]
    public void AStringNoContentAsksForAndAStringTheContentNeedsAreProblems()
    {
        var withOrphan = TestGame.Locale.With(new Dictionary<string, string>(StringComparer.Ordinal) { ["lesson.first-words.no-such-step.text"] = "Orphan." });
        Assert.Contains("locale en: the string 'lesson.first-words.no-such-step.text' is asked for by no content or code (an orphan).", GameDataValidator.Validate(TestGame.Data.WithLocale(withOrphan)));

        var strings = Repo.Path("content", "locale", "en");
        var files = Directory.GetFiles(strings, "*.json").Order(StringComparer.Ordinal)
            .Select(static f => (Path.GetFileName(f), File.ReadAllText(f).Replace("\"line.prompt.stall-need\":", "\"line.prompt.stall-need-x\":", StringComparison.Ordinal)));
        var renamed = LocaleText.Parse("en", "h", files);
        var problems = GameDataValidator.Validate(TestGame.Data.WithLocale(renamed));
        Assert.Contains("locale en: the string 'line.prompt.stall-need' is missing.", problems);
        Assert.Contains("locale en: the string 'line.prompt.stall-need-x' is asked for by no content or code (an orphan).", problems);
    }

    private static void CopyDirectory(string from, string to)
    {
        foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
