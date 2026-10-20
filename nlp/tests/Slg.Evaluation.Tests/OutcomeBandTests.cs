using Slg.Core.Determinism;
using Slg.Core.Prompts;

namespace Slg.Evaluation.Tests;

/// <summary>Tests for <see cref="OutcomeBand"/>.</summary>
public class OutcomeBandTests
{
    private static readonly DifficultyEvaluationConfig Beginner = EvaluationConfig.Default.Beginner;

    [Fact]
    public void NotCommunicatedIsNotUnderstoodWhateverTheCorrectness()
    {
        var band = OutcomeBand.Classify(Fixtures.MakeVerdict(communicated: false, correctness: 1.0, confidence: 1.0), Beginner);

        Assert.Equal(OutcomeBandKind.NotUnderstood, band.Kind);
        Assert.Equal(MarginDecision.Reject, band.Communicated);
        Assert.Equal(MarginDecision.Accept, band.Correctness);
    }

    [Fact]
    public void ConfidentCommunicationWithHighCorrectnessIsCorrect()
    {
        var band = OutcomeBand.Classify(Fixtures.MakeVerdict(correctness: 1.0, confidence: 0.9), Beginner);

        Assert.Equal(OutcomeBandKind.Correct, band.Kind);
        Assert.Equal(MarginDecision.Accept, band.Communicated);
        Assert.Equal(MarginDecision.Accept, band.Correctness);
    }

    [Fact]
    public void CorrectnessInsideTheBandIsPartiallyCorrect()
    {
        var band = OutcomeBand.Classify(Fixtures.MakeVerdict(correctness: Beginner.CorrectnessThreshold, confidence: 0.9), Beginner);

        Assert.Equal(OutcomeBandKind.PartiallyCorrect, band.Kind);
        Assert.Equal(MarginDecision.Uncertain, band.Correctness);
    }

    [Fact]
    public void LowCorrectnessIsCommunicated()
    {
        var band = OutcomeBand.Classify(Fixtures.MakeVerdict(correctness: 0.6, confidence: 0.9), Beginner);

        Assert.Equal(OutcomeBandKind.Communicated, band.Kind);
        Assert.Equal(MarginDecision.Accept, band.Communicated);
        Assert.Equal(MarginDecision.Reject, band.Correctness);
    }

    [Fact]
    public void UncertainUnderstandingCapsAPerfectUtteranceAtPartiallyCorrect()
    {
        var band = OutcomeBand.Classify(Fixtures.MakeVerdict(correctness: 1.0, confidence: Beginner.CommunicatedConfidenceThreshold), Beginner);

        Assert.Equal(OutcomeBandKind.PartiallyCorrect, band.Kind);
        Assert.Equal(MarginDecision.Uncertain, band.Communicated);
        Assert.Equal(MarginDecision.Accept, band.Correctness);
    }

    [Fact]
    public void UncertainUnderstandingWithLowCorrectnessStaysCommunicated()
    {
        var band = OutcomeBand.Classify(Fixtures.MakeVerdict(correctness: 0.2, confidence: Beginner.CommunicatedConfidenceThreshold), Beginner);

        Assert.Equal(OutcomeBandKind.Communicated, band.Kind);
    }

    [Fact]
    public void CommunicatedVerdictWithLowConfidenceIsNeverRejectedOnUnderstanding()
    {
        var band = OutcomeBand.Classify(Fixtures.MakeVerdict(correctness: 1.0, confidence: 0.1), Beginner);

        Assert.Equal(MarginDecision.Uncertain, band.Communicated);
        Assert.Equal(OutcomeBandKind.PartiallyCorrect, band.Kind);
    }

    [Fact]
    public void CorrectnessBoundariesFollowTheMargin()
    {
        var accept = Beginner.CorrectnessThreshold + Beginner.Epsilon;
        var reject = Beginner.CorrectnessThreshold - Beginner.Epsilon;

        Assert.Equal(OutcomeBandKind.Correct, OutcomeBand.Classify(Fixtures.MakeVerdict(correctness: accept), Beginner).Kind);
        Assert.Equal(OutcomeBandKind.Communicated, OutcomeBand.Classify(Fixtures.MakeVerdict(correctness: reject), Beginner).Kind);
    }

    [Fact]
    public void DifficultyOverloadUsesThatTiersThresholds()
    {
        var verdict = Fixtures.MakeVerdict(correctness: 0.8);

        Assert.Equal(OutcomeBandKind.Correct, OutcomeBand.Classify(verdict, EvaluationConfig.Default, Difficulty.Beginner).Kind);
        Assert.Equal(OutcomeBandKind.PartiallyCorrect, OutcomeBand.Classify(verdict, EvaluationConfig.Default, Difficulty.Intermediate).Kind);
        Assert.Equal(OutcomeBandKind.Communicated, OutcomeBand.Classify(verdict, EvaluationConfig.Default, Difficulty.Advanced).Kind);
    }

    [Fact]
    public void PronunciationDecisionUsesTheMeanPhoneScoreAndDoesNotChangeTheBand()
    {
        Assert.Null(OutcomeBand.Classify(Fixtures.MakeVerdict(), Beginner).Pronunciation);

        var good = OutcomeBand.Classify(Fixtures.MakeVerdict(phones: [Fixtures.Phone(1.0), Fixtures.Phone(0.8)]), Beginner);
        var middling = OutcomeBand.Classify(Fixtures.MakeVerdict(phones: [Fixtures.Phone(Beginner.PronunciationThreshold)]), Beginner);
        var poor = OutcomeBand.Classify(Fixtures.MakeVerdict(phones: [Fixtures.Phone(0.0), Fixtures.Phone(0.2)]), Beginner);

        Assert.Equal(MarginDecision.Accept, good.Pronunciation);
        Assert.Equal(MarginDecision.Uncertain, middling.Pronunciation);
        Assert.Equal(MarginDecision.Reject, poor.Pronunciation);
        Assert.Equal(good.Kind, poor.Kind);
    }

    [Fact]
    public void WorkedExampleIsCommunicatedBand()
    {
        var verdict = VerdictAssembler.Assemble(Fixtures.WorkedExample());

        var band = OutcomeBand.Classify(verdict, EvaluationConfig.Default, Difficulty.Beginner);

        Assert.Equal(new OutcomeBand(OutcomeBandKind.Communicated, MarginDecision.Accept, MarginDecision.Reject, null), band);
    }

    [Fact]
    public void ClassifyValidatesArguments()
    {
        Assert.Throws<ArgumentNullException>(() => OutcomeBand.Classify(null!, Beginner));
        Assert.Throws<ArgumentNullException>(() => OutcomeBand.Classify(Fixtures.MakeVerdict(), (DifficultyEvaluationConfig)null!));
        Assert.Throws<ArgumentNullException>(() => OutcomeBand.Classify(Fixtures.MakeVerdict(), null!, Difficulty.Beginner));
    }
}
