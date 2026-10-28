using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Es.Normalization;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Game.Core.Tests.Language.Words;

/// <summary>The learner's word counts (D-game-6): from scripted turns and from the core's verdicts on typed lines.</summary>
public sealed class WordProgressTests
{
    // A turn built by hand carries the words the core's resolver reads the line as, so the counts see the same words as in play.
    private static GuideTurn Turn(
        string line,
        bool communicated = true,
        IReadOnlyList<GuideError>? errors = null,
        SlotMap? slots = null,
        string? worldLine = null) => new(
            line,
            "name_object",
            communicated,
            1.0,
            errors ?? [],
            null,
            [],
            null,
            "idle",
            [],
            worldLine,
            null,
            null,
            "digest",
            slots ?? SlotMap.Empty,
            Words: WordsOf(line));

    private static IReadOnlyList<GuideWord> WordsOf(string line)
    {
        var context = ConversationContext.Create(
            TestGame.Seed,
            NpcService.GuideAgent,
            TestGame.Bundle.Agents[NpcService.GuideAgent].InitialState(),
            new StoredTimestamp(GuideSession.DefaultStartedAt),
            LearnerModel.New(),
            new Dictionary<string, int>(StringComparer.Ordinal),
            SlotMap.Empty,
            []);
        var result = new TypedUtteranceEvaluator(TestGame.Bundle).Evaluate(line, TestGame.Bundle.Prompts[NpcService.GuidePrompt], context);
        return GuideWord.From(result);
    }

    // Stored time in the tests: the session's default start plus minutes and seconds.
    private static long At(int minutes, int seconds = 0) => GuideSession.DefaultStartedAt + (minutes * 60L) + seconds;

    private static SlotMap Object(string lemma) => SlotMap.From(new Dictionary<string, string> { ["object"] = lemma });

    // The span of a word in a normalized line, as the core reports it.
    private static TextSpan SpanOf(string line, string word)
    {
        foreach (var token in SpanishTextNormalizer.Normalize(line).Tokens)
        {
            if (token.Text == word)
            {
                return token.Span;
            }
        }

        throw new InvalidOperationException($"'{word}' is not a token of '{line}'.");
    }

    [Fact]
    public void ThreeCorrectUsesSpacedByTheGapsMakeTheNounKnownInThirds()
    {
        var words = new WordProgress(TestGame.Nouns);
        var rules = CreditRules.FullTyped;

        var first = words.RecordTurn(Turn("es un árbol", slots: Object("árbol")), InputKind.Typed, rules, At(0));
        Assert.Equal(["árbol"], first.Credited);
        Assert.Empty(first.NewlyKnown);
        Assert.Equal(1.0 / 3, words.SaturationOf("árbol"), 9);
        Assert.Equal(WordStage.Learning, words.RecordOf("árbol").Stage);

        words.RecordTurn(Turn("es un árbol", slots: Object("árbol")), InputKind.Typed, rules, At(10));
        Assert.Equal(2.0 / 3, words.SaturationOf("árbol"), 9);
        Assert.False(words.RecordOf("árbol").Known);

        var third = words.RecordTurn(Turn("es un árbol", slots: Object("árbol")), InputKind.Typed, rules, At(70));
        Assert.Equal(["árbol"], third.NewlyKnown);
        Assert.Equal(1.0, words.SaturationOf("árbol"));
        Assert.True(words.RecordOf("árbol").Known);
        Assert.Equal(WordStage.Known, words.RecordOf("árbol").Stage);
        Assert.Equal(3, words.RecordOf("árbol").CorrectTurns);
        Assert.Equal(["árbol"], words.KnownLemmas);
    }

    [Fact]
    public void ASecondTurnWithTheNounIsNotNewlyKnownAgain()
    {
        var words = new WordProgress(TestGame.Nouns);
        for (var i = 0; i < 3; i++)
        {
            words.RecordTurn(Turn("es un árbol", slots: Object("árbol")), InputKind.Typed, CreditRules.FullTyped, At(i * 70));
        }

        var fourth = words.RecordTurn(Turn("es un árbol", slots: Object("árbol")), InputKind.Typed, CreditRules.FullTyped, At(300));

        Assert.Empty(fourth.NewlyKnown);
        Assert.Equal(1.0, words.SaturationOf("árbol"));
    }

    [Fact]
    public void SeveralUsesInOneTurnEarnOneCredit()
    {
        var words = new WordProgress(TestGame.Nouns);

        var result = words.RecordTurn(Turn("es un árbol, el árbol", slots: Object("árbol")), InputKind.Typed, CreditRules.FullTyped, At(0));

        Assert.Equal(["árbol"], result.Credited);
        Assert.Equal(1, words.RecordOf("árbol").CorrectTurns);
        Assert.Equal(WordProgress.UnitsPerUse, words.RecordOf("árbol").CreditUnits);
        Assert.Equal(1, words.RecordOf("árbol").Spoken);
    }

    [Fact]
    public void AWrongArticleOnTheNounEarnsNoCredit()
    {
        var words = new WordProgress(TestGame.Nouns);
        const string Line = "la árbol";
        var error = new GuideError("gender.agree", "el", "la", SpanOf(Line, "la"));

        var result = words.RecordTurn(Turn(Line, errors: [error], slots: Object("árbol")), InputKind.Typed, CreditRules.FullTyped, At(0));

        Assert.Empty(result.Credited);
        Assert.Equal(0, words.RecordOf("árbol").CreditUnits);
        Assert.Equal(1, words.RecordOf("árbol").Spoken);
        Assert.Equal(0, words.RecordOf("árbol").SpokenCorrect);
    }

    [Fact]
    public void AnErrorOnTheNounItselfEarnsNoCredit()
    {
        var words = new WordProgress(TestGame.Nouns);
        const string Line = "es un árbol";
        var error = new GuideError("vocab.unknown", "árbol", "arbul", SpanOf(Line, "árbol"));

        var result = words.RecordTurn(Turn(Line, errors: [error], slots: Object("árbol")), InputKind.Typed, CreditRules.FullTyped, At(0));

        Assert.Empty(result.Credited);
        Assert.Equal(0, words.RecordOf("árbol").CreditUnits);
    }

    [Fact]
    public void AnErrorElsewhereInTheLineDoesNotTakeTheCreditAway()
    {
        var words = new WordProgress(TestGame.Nouns);
        const string Line = "es un árbol";
        var error = new GuideError("ser_estar", "es", "es", SpanOf(Line, "es"));

        var result = words.RecordTurn(Turn(Line, errors: [error], slots: Object("árbol")), InputKind.Typed, CreditRules.FullTyped, At(0));

        Assert.Equal(["árbol"], result.Credited);
        Assert.Equal(1, words.RecordOf("árbol").SpokenCorrect);
    }

    [Fact]
    public void ANotCommunicatedTurnCountsNothingForUse()
    {
        var words = new WordProgress(TestGame.Nouns);

        words.RecordTurn(Turn("es un árbol", communicated: false, slots: Object("árbol")), InputKind.Typed, CreditRules.FullTyped, At(0));

        Assert.Equal(0, words.RecordOf("árbol").Spoken);
        Assert.Equal(0, words.RecordOf("árbol").CreditUnits);
    }

    [Fact]
    public void WhatTheGuideSaysCountsAsHeardWithoutAnyUse()
    {
        var words = new WordProgress(TestGame.Nouns);

        words.RecordTurn(Turn("hola", communicated: false, worldLine: "El árbol está allí."), InputKind.Typed, CreditRules.FullTyped, At(0));

        var record = words.RecordOf("árbol");
        Assert.Equal(1, record.Heard);
        Assert.Equal(0, record.Spoken);
        Assert.Equal(WordStage.Encountered, record.Stage);
        Assert.Equal(0.0, words.SaturationOf("árbol"));
    }

    [Fact]
    public void ReadingTheLabelCountsAndOnlyCatalogueNounsAreRecorded()
    {
        var words = new WordProgress(TestGame.Nouns);

        words.MarkRead("árbol");
        words.MarkRead("correr");

        Assert.Equal(1, words.RecordOf("árbol").Read);
        Assert.False(words.Records.ContainsKey("correr"));
        Assert.Equal(WordStage.Encountered, words.RecordOf("árbol").Stage);
    }

    [Fact]
    public void ColourFollowsTheLemmaAndABlockTakesTheColourOfItsNoun()
    {
        var words = new WordProgress(TestGame.Nouns);
        words.RecordTurn(Turn("es un árbol", slots: Object("árbol")), InputKind.Typed, CreditRules.FullTyped, At(0));

        Assert.Equal(1.0 / 3, words.SaturationOf("árbol"), 9);
        Assert.Equal(0.0, words.SaturationOf("piedra"));
        Assert.Equal(0.0, words.SaturationOf(ItemId.Berry)); // the berry is named by fruta, which is not seen yet
    }

    [Fact]
    public void TheCreditPerKindOfInputIsOneSettingAndHalfCreditTakesTwiceAsManyTurns()
    {
        var half = new CreditRules(TypedUnits: WordProgress.UnitsPerUse, SpokenUnits: WordProgress.UnitsPerUse / 2);
        var words = new WordProgress(TestGame.Nouns);

        Assert.Equal(WordProgress.UnitsPerUse, CreditRules.FullTyped.For(InputKind.Typed));
        Assert.Equal(WordProgress.UnitsPerUse, CreditRules.FullTyped.For(InputKind.Spoken));
        Assert.Equal(WordProgress.UnitsPerUse / 2, half.For(InputKind.Spoken));

        for (var i = 0; i < WordProgress.UsesToKnow; i++)
        {
            words.RecordTurn(Turn("es un árbol", slots: Object("árbol")), InputKind.Spoken, half, At(i * 70));
        }

        Assert.False(words.RecordOf("árbol").Known);
        Assert.Equal(0.5, words.SaturationOf("árbol"), 9);
    }

    [Fact]
    public void TheDefaultRulesGiveTypedInputHalfCreditAndSpokenInputFullCredit()
    {
        // D-game-6, from P5: spoken input earns a full use, typed input half a use; the launch option --typed-credit full is FullTyped.
        Assert.Equal(WordProgress.UnitsPerUse / 2, CreditRules.Default.For(InputKind.Typed));
        Assert.Equal(WordProgress.UnitsPerUse, CreditRules.Default.For(InputKind.Spoken));
        Assert.Equal(WordProgress.UnitsPerUse, CreditRules.FullTyped.For(InputKind.Typed));
        Assert.Equal(WordProgress.UnitsPerUse, CreditRules.FullTyped.For(InputKind.Spoken));
    }

    [Fact]
    public void ThreeSpokenUsesKnowAWordAndSixTypedUsesAtHalfCreditDoTheSame()
    {
        var rules = CreditRules.Default;
        var spoken = new WordProgress(TestGame.Nouns);
        var typed = new WordProgress(TestGame.Nouns);

        // Spoken: 10 minutes after the first, 1 hour after the second (the gaps), three uses.
        long[] spokenAt = [0, 10 * 60, (10 * 60) + (60 * 60)];
        foreach (var second in spokenAt)
        {
            spoken.RecordTurn(Turn("es un árbol", slots: Object("árbol")), InputKind.Spoken, rules, GuideSession.DefaultStartedAt + second);
        }

        Assert.True(spoken.RecordOf("árbol").Known);
        Assert.Equal(3.0, spoken.RecordOf("árbol").Uses, 9);

        // Typed at half credit: the same gaps, the last one (1 hour) repeating, so a word needs six credited uses and 4 h 10 min.
        long time = 0;
        var gaps = new long[] { 0, 10 * 60, 60 * 60, 60 * 60, 60 * 60, 60 * 60 };
        for (var i = 0; i < gaps.Length; i++)
        {
            time += gaps[i];
            typed.RecordTurn(Turn("es un árbol", slots: Object("árbol")), InputKind.Typed, rules, GuideSession.DefaultStartedAt + time);
            Assert.Equal(i < gaps.Length - 1, !typed.RecordOf("árbol").Known);
            Assert.Equal((i + 1) / 2.0, typed.RecordOf("árbol").Uses, 9);
        }

        Assert.Equal(6, typed.RecordOf("árbol").CorrectTurns);
        Assert.Equal(1.0, typed.SaturationOf("árbol"), 9);
    }

    [Fact]
    public void TheProgressPipsLightAtLeastOneForAStartedWordAndAllThreeOnlyWhenKnown()
    {
        var rules = CreditRules.Default;
        var words = new WordProgress(TestGame.Nouns);
        Assert.Equal(0, words.RecordOf("árbol").Pips);

        words.RecordTurn(Turn("es un árbol", slots: Object("árbol")), InputKind.Typed, rules, At(0));
        Assert.Equal(1, words.RecordOf("árbol").Pips); // half a use still shows a pip
        words.RecordTurn(Turn("es un árbol", slots: Object("árbol")), InputKind.Spoken, rules, At(11));
        Assert.Equal(2, words.RecordOf("árbol").Pips); // 1.5 uses
        words.RecordTurn(Turn("es un árbol", slots: Object("árbol")), InputKind.Spoken, rules, At(80));
        Assert.False(words.RecordOf("árbol").Known); // 2.5 uses
        Assert.Equal(2, words.RecordOf("árbol").Pips); // never all three before it is known
        words.RecordTurn(Turn("es un árbol", slots: Object("árbol")), InputKind.Spoken, rules, At(150));
        Assert.True(words.RecordOf("árbol").Known);
        Assert.Equal(WordProgress.UsesToKnow, words.RecordOf("árbol").Pips);
    }

    [Fact]
    public void ATurnRecordsHowItArrivedAndTheSessionCreditsThatKind()
    {
        Assert.Equal(InputKind.Typed, Turn("es un árbol").Input);
        Assert.Null(Turn("es un árbol").Confidence);

        var typed = TestGame.NewSession(credit: CreditRules.Default);
        var player = typed.Simulation.Player.FeetBlock;
        var cursor = TestGame.NearestBlock(typed, BlockId.Trunk);
        var turn = typed.Say("es un árbol", cursor);

        Assert.Equal(InputKind.Typed, turn.Input);
        Assert.Equal(WordProgress.UnitsPerUse / 2, typed.Words.RecordOf("árbol").CreditUnits);
        Assert.Equal(0.5, typed.Words.RecordOf("árbol").Uses, 9);

        var full = TestGame.NewSession(credit: CreditRules.FullTyped);
        _ = full.Say("es un árbol", cursor);
        Assert.Equal(WordProgress.UnitsPerUse, full.Words.RecordOf("árbol").CreditUnits);
    }

    [Fact]
    public void TheSameTurnsGiveTheSameCounts()
    {
        var first = new WordProgress(TestGame.Nouns);
        var second = new WordProgress(TestGame.Nouns);
        var turns = new[]
        {
            Turn("es un árbol", slots: Object("árbol"), worldLine: "Es un árbol."),
            Turn("es una piedra", slots: Object("piedra")),
            Turn("es un árbol", slots: Object("árbol")),
        };
        foreach (var turn in turns)
        {
            first.RecordTurn(turn, InputKind.Typed, CreditRules.FullTyped, At(0));
            second.RecordTurn(turn, InputKind.Typed, CreditRules.FullTyped, At(0));
        }

        Assert.Equal(first.Records.Values.ToList(), second.Records.Values.ToList());
        Assert.Equal(["piedra", "árbol"], first.Records.Keys.ToList()); // ordinal: the code point of p is below that of á
    }

    [Fact]
    public void ThreeTypedUsesOfTheTreeInTheCoreSpacedByTheGapsMakeItKnown()
    {
        var session = TestGame.NewSession();
        var tree = TestGame.NearestOf(session, "árbol");

        for (var i = 1; i <= WordProgress.UsesToKnow; i++)
        {
            session.SkipTime(i == 1 ? 0 : i == 2 ? 11 * 60 : 61 * 60); // the owner's gaps: 10 minutes, then 1 hour
            var turn = session.Say("es un árbol", tree);

            Assert.True(turn.Communicated, $"turn {i}: not understood");
            Assert.Empty(turn.Errors);
            Assert.Equal(i * WordProgress.UnitsPerUse, session.Words.RecordOf("árbol").CreditUnits);
        }

        Assert.True(session.Words.RecordOf("árbol").Known);
        Assert.Equal(["árbol"], session.Words.KnownLemmas);
        Assert.Equal(1.0, session.Words.SaturationOf("árbol"));
    }

    [Fact]
    public void LaArbolInTheCoreEarnsNoCredit()
    {
        var session = TestGame.NewSession();

        var turn = session.Say("la árbol");

        Assert.Contains(turn.Errors, e => e.Tag == "gender.agree");
        Assert.Equal(0, session.Words.RecordOf("árbol").CreditUnits);
        Assert.Equal(0.0, session.Words.SaturationOf("árbol"));
    }

    [Fact]
    public void WhatTheCoreDoesWithAnUnaccentedNounIsCountedAsAUse()
    {
        // Measured on the core at db39999: `es un arbol` is understood as name_object with no error, so the unaccented word earns credit. The
        // journal shows the accented spelling (the lemma). A bare `arbol` is not a sentence and earns no credit as an intent.
        var session = TestGame.NewSession();
        var tree = TestGame.NearestOf(session, "árbol");

        var turn = session.Say("es un arbol", tree);

        Assert.Equal("name_object", turn.Intent);
        Assert.True(turn.Communicated);
        Assert.Empty(turn.Errors);
        Assert.Equal(WordProgress.UnitsPerUse, session.Words.RecordOf("árbol").CreditUnits);
    }

    [Fact]
    public void ANamedNounAtTheRightTileGetsTheRightReplyAndAWrongOneTheCorrection()
    {
        var session = TestGame.NewSession();
        var tree = TestGame.NearestOf(session, "árbol");

        var right = session.Say("es un árbol", tree);
        Assert.Contains("Es un árbol", right.WorldLine);

        var wrong = session.Say("es una piedra", tree);
        Assert.Contains("un árbol", wrong.WorldLine);
        Assert.DoesNotContain("piedra", wrong.WorldLine);

        // D-game-6 as amended: a correct word for the wrong object earns no colour for the word named (the owner's rule; it still counts as said).
        Assert.Equal(0, session.Words.RecordOf("piedra").CreditUnits);
        Assert.Equal(1, session.Words.RecordOf("piedra").Spoken);
        Assert.Equal(CreditOutcome.NamedWrong, session.LastWordTurn!.Outcomes.Single(o => o.Lemma == "piedra").Outcome);
    }

    [Fact]
    public void NamingWithoutACursorSaysSo()
    {
        var session = TestGame.NewSession();

        var turn = session.Say("es un árbol");

        Assert.NotNull(turn.WorldLine);
        Assert.DoesNotContain("árbol", turn.WorldLine);
    }

    [Fact]
    public void TheNameRepliesRenderWithTheTargetNoun()
    {
        var bundle = TestGame.Bundle;
        var target = SlotMap.From(new Dictionary<string, string> { ["target"] = "árbol" });
        foreach (var poolId in new[] { "guide.name.correct", "guide.name.wrong" })
        {
            Assert.True(bundle.Dialogue.TryGetPool(poolId, out var pool), $"No pool {poolId}.");
            foreach (var variant in pool.Variants)
            {
                Assert.Contains("un árbol", bundle.TemplateEngine.Render(variant, target));
            }
        }

        Assert.True(bundle.Dialogue.TryGetPool("guide.name.none", out var none));
        Assert.All(none.Variants, variant => Assert.False(string.IsNullOrWhiteSpace(bundle.TemplateEngine.Render(variant, SlotMap.Empty))));
    }
}
