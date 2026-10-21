namespace Vocaterra.Nlp.Language.Tests.Intent;

using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Intent;

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
}
