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

namespace Vocaterra.Game.Core.Tests.Tip;

/// <summary>
/// The English of S16 (D-game-13), which is game content in <c>game/english.json</c>: the file loads and is complete, every Spanish line an NPC can say has its
/// English, the controls and tips are there, the objective line is bilingual and drops each English word once its noun is known, and the feedback after a turn is
/// keyed by the core's error codes and by the outcome, with a fallback for a code the file does not know. The core is not changed.
/// </summary>
public sealed class EnglishTextTests
{
    private static LocaleText English => RouteScene.DataOf(TestGame.Bundle).Locale;

    [Fact]
    public void TheFileLoadsAndHasTheControlsTheTipsAndEveryInterfaceWord()
    {
        var english = English;

        Assert.True(english.Controls.Count >= 10);
        Assert.Contains(english.Controls, static c => c.Id == "look" && c.Text.StartsWith("Look around", StringComparison.Ordinal));
        Assert.Contains(english.Controls, static c => c.Id == "help" && c.Keys.Contains('H', StringComparison.Ordinal));
        Assert.Contains(english.Tips, static t => t is { When: "start", Text: "Move the mouse to look around. The valley below is where you are headed." });
        Assert.Equal(["look", "walk", "jump", "note", "gather"], english.Tips.Where(static t => t.When == "start").Select(static t => t.Id)); // the opening (S17)
        Assert.Equal([0, 9, 18, 28, 50], english.Tips.Where(static t => t.When == "start").Select(static t => t.DelaySeconds)); // paced so they do not stack up
        Assert.Contains(english.Tips, static t => t.When == "aimed_npc");
        Assert.All(LocaleText.UiKeys, key => Assert.False(string.IsNullOrWhiteSpace(english.Ui(key)), key));
        Assert.Equal("step 2 of 7", english.Ui("stepOf", ("n", "2"), ("total", "7")));
        Assert.All(english.Tips, static t => Assert.Contains(t.When, TipBook.TipTriggers));
    }

    [Fact]
    public void EveryLineAnNpcCanSayHasItsEnglishInTheFile()
    {
        var english = English;
        var missing = TestGame.Speech
            .Where(static l => l.Kind is SpeechKind.Dialogue or SpeechKind.Prompt)
            .Where(l => english.ForLine(l.Text) is null)
            .Select(static l => $"{l.LineId}: {l.Text}")
            .ToList();

        Assert.Empty(missing);
        Assert.Equal("What do you need?", english.ForLine("¿Qué necesitas?"));
        Assert.Equal("What do you need?", english.ForLine("que necesitas")); // without accents, capitals or marks
        Assert.Null(english.ForLine("El río está allí, a la izquierda, a sesenta bloques.")); // a templated world line has no fixed English
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
    public void AFileWithoutAnOutcomeOrWithAnUnknownTipTriggerIsRefused()
    {
        // A phrase the sheet lacks is the validator's to find (GameDataValidatorTests): the file alone cannot know the sheet.
        var json = File.ReadAllText(Path.Combine(GameBundle.ResolveDirectory(), "game", "english.json"));

        Assert.Throws<InvalidDataException>(() => LocaleText.Parse(json.Replace("\"uncertain\":", "\"uncertain_x\":", StringComparison.Ordinal)));
        Assert.Throws<InvalidDataException>(() => LocaleText.Parse(json.Replace("\"when\": \"start\"", "\"when\": \"never_happens\"", StringComparison.Ordinal)));
        Assert.Throws<InvalidDataException>(() => LocaleText.Parse(json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal)));
        Assert.Throws<InvalidDataException>(() => LocaleText.Parse("{ not json"));
        _ = LocaleText.Parse(json);
    }
}
