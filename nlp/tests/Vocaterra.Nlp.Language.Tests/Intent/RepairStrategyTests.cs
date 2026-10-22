namespace Vocaterra.Nlp.Language.Tests.Intent;

using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Matching;

public class RepairStrategyTests
{
    private static readonly string[] Phrases =
    [
        "quiero comprar una espada",
        "tengo dos espadas",
        "hola buenos días",
        "adiós",
    ];

    private static PromptSpec PromptOf(RecognitionMode mode)
    {
        return new PromptSpec("p1", mode, Difficulty.Beginner, "answers", [], null, []);
    }

    [Fact]
    public void FirstAttemptAsksAgainInTheSameModeForAnOpenPrompt()
    {
        var step = RepairStrategy.Next(1, PromptOf(RecognitionMode.Open), Phrases);

        Assert.Equal(RepairKind.AskAgain, step.Kind);
        Assert.Equal("¿Cómo?", step.AgentLine);
        Assert.Equal(RecognitionRequest.Open(), step.RetryRequest);
    }

    [Fact]
    public void FirstAttemptKeepsTheFullPhraseListForAConstrainedPrompt()
    {
        var step = RepairStrategy.Next(1, PromptOf(RecognitionMode.Constrained), Phrases);

        Assert.Equal(RepairKind.AskAgain, step.Kind);
        Assert.Equal(RecognitionRequest.Constrained(Phrases), step.RetryRequest);
    }

    [Fact]
    public void SecondAttemptRepeatsSlowlyInTheSameMode()
    {
        var open = RepairStrategy.Next(2, PromptOf(RecognitionMode.Open), Phrases);
        var constrained = RepairStrategy.Next(2, PromptOf(RecognitionMode.Constrained), Phrases);

        Assert.Equal(RepairKind.SlowRepeat, open.Kind);
        Assert.Equal(RepairStrategy.SlowRepeatLine, open.AgentLine);
        Assert.Equal(RecognitionMode.Open, open.RetryRequest!.Mode);
        Assert.Equal(RepairKind.SlowRepeat, constrained.Kind);
        Assert.Equal(RecognitionMode.Constrained, constrained.RetryRequest!.Mode);
    }

    [Theory]
    [InlineData(RecognitionMode.Open)]
    [InlineData(RecognitionMode.Constrained)]
    public void ThirdAttemptOffersTheFirstThreeChoicesAndSwitchesToConstrained(RecognitionMode mode)
    {
        var step = RepairStrategy.Next(3, PromptOf(mode), Phrases);

        Assert.Equal(RepairKind.OfferChoices, step.Kind);
        string[] firstThree = ["quiero comprar una espada", "tengo dos espadas", "hola buenos días"];
        Assert.Equal(RecognitionRequest.Constrained(firstThree), step.RetryRequest);
        Assert.Equal("¿Quieres decir «quiero comprar una espada», «tengo dos espadas» o «hola buenos días»?", step.AgentLine);
    }

    [Fact]
    public void ChoicesSkipBlanksAndDuplicatesAndMayBeFewerThanThree()
    {
        var step = RepairStrategy.Next(3, PromptOf(RecognitionMode.Open), ["", "hola", "hola", "  ", "adiós"]);

        Assert.Equal(RepairKind.OfferChoices, step.Kind);
        Assert.Equal(["hola", "adiós"], step.RetryRequest!.GrammarPhrases);
        Assert.Equal("¿Quieres decir «hola» o «adiós»?", step.AgentLine);

        var single = RepairStrategy.Next(3, PromptOf(RecognitionMode.Open), ["hola"]);
        Assert.Equal("¿Quieres decir «hola»?", single.AgentLine);
    }

    [Fact]
    public void ThirdAttemptGivesUpWhenThereAreNoChoicesToOffer()
    {
        var step = RepairStrategy.Next(3, PromptOf(RecognitionMode.Open), [" "]);

        Assert.Equal(RepairKind.GiveUp, step.Kind);
        Assert.Null(step.RetryRequest);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(100)]
    public void FourthAttemptOnwardsGivesUp(int attempt)
    {
        var step = RepairStrategy.Next(attempt, PromptOf(RecognitionMode.Constrained), Phrases);

        Assert.Equal(RepairKind.GiveUp, step.Kind);
        Assert.Equal(RepairStrategy.GiveUpLine, step.AgentLine);
        Assert.Null(step.RetryRequest);
    }

    [Fact]
    public void FullSequenceIsAskSlowChoicesGiveUp()
    {
        var prompt = PromptOf(RecognitionMode.Open);

        var kinds = Enumerable.Range(1, 5).Select(attempt => RepairStrategy.Next(attempt, prompt, Phrases).Kind).ToArray();

        Assert.Equal([RepairKind.AskAgain, RepairKind.SlowRepeat, RepairKind.OfferChoices, RepairKind.GiveUp, RepairKind.GiveUp], kinds);
    }

    [Fact]
    public void SameInputsGiveEqualSteps()
    {
        var prompt = PromptOf(RecognitionMode.Constrained);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            Assert.Equal(RepairStrategy.Next(attempt, prompt, Phrases), RepairStrategy.Next(attempt, prompt, Phrases.ToList()));
        }
    }

    [Fact]
    public void InvalidArgumentsAreRejected()
    {
        var prompt = PromptOf(RecognitionMode.Open);

        Assert.Throws<ArgumentOutOfRangeException>(() => RepairStrategy.Next(0, prompt, Phrases));
        Assert.Throws<ArgumentNullException>(() => RepairStrategy.Next(1, null!, Phrases));
        Assert.Throws<ArgumentNullException>(() => RepairStrategy.Next(1, prompt, null!));
    }

    // Fixture answer sets shaped like the game's `supplies` and `yes-no` sets (declared order: the answers, help and don't-know last).
    private static AnswerSet SuppliesFixture()
    {
        return new AnswerSet(
            "supplies",
            [
                new Answer("water", "quiero agua por favor", ["un agua por favor", "quiero agua", "agua por favor", "necesito agua"], []),
                new Answer("bread", "quiero pan por favor", ["quiero pan", "pan por favor", "necesito pan"], []),
                new Answer("fruit", "quiero fruta por favor", ["quiero fruta", "fruta por favor", "necesito fruta"], []),
                new Answer("help", "ayuda", ["ayuda por favor", "necesito ayuda", "me ayudas"], []),
                new Answer("dont-know", "no sé", ["no lo sé", "no entiendo"], []),
            ]);
    }

    private static AnswerSet YesNoFixture()
    {
        return new AnswerSet(
            "yes-no",
            [
                new Answer("yes", "sí por favor", ["sí", "si", "claro", "claro que sí"], []),
                new Answer("no", "no gracias", ["no", "gracias no", "no muchas gracias"], []),
                new Answer("help", "ayuda", ["ayuda por favor", "necesito ayuda"], []),
                new Answer("dont-know", "no sé", ["no lo sé"], []),
            ]);
    }

    [Fact]
    public void TheStallNeedOffersOneCanonicalPerItemNotTheSortedAliases()
    {
        var set = SuppliesFixture();
        var step = RepairStrategy.Next(3, PromptOf(RecognitionMode.Constrained), set.GrammarPhrases(), set.CanonicalPhrases());

        // The old choices were the first three of GrammarPhrases(): «agua por favor», «ayuda», «ayuda por favor».
        Assert.Equal(RepairKind.OfferChoices, step.Kind);
        Assert.Equal(["quiero agua por favor", "quiero pan por favor", "quiero fruta por favor"], step.RetryRequest!.GrammarPhrases);
        Assert.Equal("¿Quieres decir «quiero agua por favor», «quiero pan por favor» o «quiero fruta por favor»?", step.AgentLine);
    }

    [Fact]
    public void TheStallOfferAlwaysOffersTheConfirmAndTheDenyCanonicals()
    {
        var set = YesNoFixture();
        var step = RepairStrategy.Next(3, PromptOf(RecognitionMode.Constrained), set.GrammarPhrases(), set.CanonicalPhrases());

        // The old choices were «ayuda», «ayuda por favor», «claro»; «no gracias» was never offered.
        Assert.Equal(["sí por favor", "no gracias", "ayuda"], step.RetryRequest!.GrammarPhrases);
        Assert.Equal("¿Quieres decir «sí por favor», «no gracias» o «ayuda»?", step.AgentLine);
    }

    [Fact]
    public void ARetryAtAttemptOneStillRecognizesEveryAcceptedPhrase()
    {
        var set = YesNoFixture();
        var step = RepairStrategy.Next(1, PromptOf(RecognitionMode.Constrained), set.GrammarPhrases(), set.CanonicalPhrases());

        Assert.Equal(RecognitionRequest.Constrained(set.GrammarPhrases().ToArray()), step.RetryRequest);
        Assert.Contains("claro", step.RetryRequest!.GrammarPhrases);
    }

    [Fact]
    public void AnAnswerSetWithFewerThanThreeAnswersOffersEveryCanonical()
    {
        var set = new AnswerSet("two", [new Answer("yes", "sí", ["si"], []), new Answer("no", "no", [], [])]);
        var step = RepairStrategy.Next(3, PromptOf(RecognitionMode.Constrained), set.GrammarPhrases(), set.CanonicalPhrases());

        Assert.Equal(["sí", "no"], step.RetryRequest!.GrammarPhrases);
        Assert.Equal("¿Quieres decir «sí» o «no»?", step.AgentLine);
    }

    [Fact]
    public void TheChoicesFollowTheGivenOrderAndAreTheSameForEqualInputs()
    {
        var set = SuppliesFixture();
        var first = RepairStrategy.Next(3, PromptOf(RecognitionMode.Constrained), set.GrammarPhrases(), set.CanonicalPhrases());
        var second = RepairStrategy.Next(3, PromptOf(RecognitionMode.Constrained), set.GrammarPhrases(), set.CanonicalPhrases());

        Assert.Equal(first, second);
        Assert.Equal(["quiero agua por favor", "quiero pan por favor", "quiero fruta por favor"], first.RetryRequest!.GrammarPhrases);
    }

    [Fact]
    public void TheFourArgumentFormGivesTheSameStepsAsTheThreeArgumentFormForOneList()
    {
        var prompt = PromptOf(RecognitionMode.Constrained);

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            Assert.Equal(RepairStrategy.Next(attempt, prompt, Phrases), RepairStrategy.Next(attempt, prompt, Phrases, Phrases));
        }
    }
}
