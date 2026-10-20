using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Decision.Learning;

namespace Vocaterra.Nlp.Decision.Tests;

/// <summary>Tests for Bayesian Knowledge Tracing and the learner model.</summary>
public class LearnerModelTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void DefaultParametersMatchTheSpecification()
    {
        var p = BktParameters.Default;

        Assert.Equal(0.3, p.PriorKnown);
        Assert.Equal(0.1, p.Learn);
        Assert.Equal(0.1, p.Slip);
        Assert.Equal(0.2, p.Guess);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void ParametersOutsideZeroToOneAreRejected(double bad)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BktParameters(bad, 0.1, 0.1, 0.2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BktParameters(0.3, bad, 0.1, 0.2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BktParameters(0.3, 0.1, bad, 0.2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BktParameters(0.3, 0.1, 0.1, bad));
    }

    [Fact]
    public void FirstCorrectObservationMatchesHandComputedPosterior()
    {
        // posterior = 0.3*0.9 / (0.3*0.9 + 0.7*0.2) = 27/41; then 27/41 + (14/41)*0.1 = 28.4/41.
        var next = BktParameters.Default.Step(0.3, correct: true);

        Assert.Equal(28.4 / 41.0, next, Tolerance);
    }

    [Fact]
    public void SecondCorrectObservationMatchesHandComputedPosterior()
    {
        // From 28.4/41: posterior = 25.56/28.08 = 71/78; then 71/78 + (7/78)*0.1 = 71.7/78.
        var first = BktParameters.Default.Step(0.3, correct: true);
        var second = BktParameters.Default.Step(first, correct: true);

        Assert.Equal(71.7 / 78.0, second, Tolerance);
    }

    [Fact]
    public void FirstIncorrectObservationMatchesHandComputedPosterior()
    {
        // posterior = 0.3*0.1 / (0.3*0.1 + 0.7*0.8) = 3/59; then 3/59 + (56/59)*0.1 = 8.6/59.
        var next = BktParameters.Default.Step(0.3, correct: false);

        Assert.Equal(8.6 / 59.0, next, Tolerance);
    }

    [Fact]
    public void CorrectThenIncorrectMatchesHandComputedPosterior()
    {
        // From 28.4/41: numerator 2.84/41, denominator (2.84 + 12.6*0.8)/41 = 12.92/41, posterior 2.84/12.92.
        var afterCorrect = BktParameters.Default.Step(0.3, correct: true);
        var afterIncorrect = BktParameters.Default.Step(afterCorrect, correct: false);

        var posterior = 2.84 / 12.92;
        Assert.Equal(posterior + ((1.0 - posterior) * 0.1), afterIncorrect, Tolerance);
    }

    [Fact]
    public void DegenerateEvidenceKeepsTheModelFinite()
    {
        var p = new BktParameters(0.0, 0.0, 0.0, 0.0);

        // A correct answer with guess 0 and nothing known has zero evidence mass: the prior is kept.
        Assert.Equal(0.0, p.Step(0.0, correct: true));
        Assert.Equal(0.0, p.Step(0.0, correct: false));
    }

    [Fact]
    public void NewModelHasNoSkillsAndReportsThePrior()
    {
        var model = LearnerModel.New();

        Assert.Empty(model.Skills);
        Assert.Null(model.Find("conj"));
        Assert.Equal(0.3, model.PKnown("conj"));
    }

    [Fact]
    public void ExercisedSkillWithoutMatchingErrorCountsAsCorrect()
    {
        var model = LearnerModel.New().Update(TestVerdicts.Clean(), ["food.nouns"]);

        var skill = Assert.Single(model.Skills);
        Assert.Equal("food.nouns", skill.SkillId);
        Assert.Equal(28.4 / 41.0, skill.PKnown, Tolerance);
        Assert.Equal(1, skill.Attempts);
        Assert.Equal(0, skill.Errors);
    }

    [Fact]
    public void ExercisedSkillWithMatchingFamilyErrorCountsAsIncorrect()
    {
        var verdict = TestVerdicts.WithErrors(TestVerdicts.Error(ErrorTags.ConjPerson));

        var model = LearnerModel.New().Update(verdict, ["conj", "food.nouns"]);

        var conj = model.Find("conj")!;
        Assert.Equal(8.6 / 59.0, conj.PKnown, Tolerance);
        Assert.Equal(1, conj.Attempts);
        Assert.Equal(1, conj.Errors);
        var food = model.Find("food.nouns")!;
        Assert.Equal(28.4 / 41.0, food.PKnown, Tolerance);
        Assert.Equal(0, food.Errors);
    }

    [Fact]
    public void ErrorFamiliesAreTrackedEvenWhenNotListedAndNeverTwice()
    {
        var verdict = TestVerdicts.WithErrors(
            TestVerdicts.Error(ErrorTags.PronR),
            TestVerdicts.Error(ErrorTags.PronVowel),
            TestVerdicts.Error(ErrorTags.SerEstar));

        var model = LearnerModel.New().Update(verdict, ["pron"]);

        Assert.Equal(["pron", "ser_estar"], model.Skills.Select(s => s.SkillId));
        Assert.All(model.Skills, s =>
        {
            Assert.Equal(1, s.Attempts);
            Assert.Equal(1, s.Errors);
        });
    }

    [Fact]
    public void SkillsAreKeptInOrdinalOrderAndInputIsNotMutated()
    {
        var original = LearnerModel.New();

        var updated = original.Update(TestVerdicts.Clean(), ["b", "Z", "a", "B"]);

        Assert.Empty(original.Skills);
        Assert.Equal(["B", "Z", "a", "b"], updated.Skills.Select(s => s.SkillId));
    }

    [Fact]
    public void RepeatedUpdatesAccumulateAttemptsAndErrors()
    {
        var model = LearnerModel.New();
        var wrong = TestVerdicts.WithErrors(TestVerdicts.Error(ErrorTags.GenderAgree));

        model = model.Update(TestVerdicts.Clean(), ["gender"]);
        model = model.Update(wrong, ["gender"]);
        model = model.Update(TestVerdicts.Clean(), ["gender"]);

        var skill = model.Find("gender")!;
        Assert.Equal(3, skill.Attempts);
        Assert.Equal(1, skill.Errors);

        var expected = 0.3;
        expected = BktParameters.Default.Step(expected, true);
        expected = BktParameters.Default.Step(expected, false);
        expected = BktParameters.Default.Step(expected, true);
        Assert.Equal(expected, skill.PKnown, Tolerance);
    }

    [Fact]
    public void NothingExercisedReturnsTheSameModel()
    {
        var model = LearnerModel.New();

        Assert.Same(model, model.Update(TestVerdicts.Clean(), []));
    }

    [Fact]
    public void SameInputsGiveEqualModelsAndIdenticalJson()
    {
        var verdict = TestVerdicts.WithErrors(TestVerdicts.Error(ErrorTags.NounNumber));

        var a = LearnerModel.New().Update(verdict, ["noun", "food"]).Update(TestVerdicts.Clean(), ["food"]);
        var b = LearnerModel.New().Update(verdict, ["food", "noun"]).Update(TestVerdicts.Clean(), ["food"]);

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal(CanonicalJson.Serialize(a), CanonicalJson.Serialize(b));
    }

    [Fact]
    public void ModelRoundTripsThroughCanonicalJson()
    {
        var model = LearnerModel.New().Update(TestVerdicts.WithErrors(TestVerdicts.Error(ErrorTags.ConjTense)), ["conj", "food"]);

        var reloaded = CanonicalJson.Deserialize<LearnerModel>(CanonicalJson.Serialize(model));

        Assert.Equal(model, reloaded);
    }

    [Fact]
    public void DeserializedSkillsAreSortedAndDuplicatesAreRejected()
    {
        var unsorted = new LearnerModel(
            BktParameters.Default,
            [new SkillMastery("b", 0.5, 1, 0), new SkillMastery("a", 0.4, 1, 0)]);

        Assert.Equal(["a", "b"], unsorted.Skills.Select(s => s.SkillId));
        Assert.Throws<ArgumentException>(() => new LearnerModel(
            BktParameters.Default,
            [new SkillMastery("a", 0.5, 1, 0), new SkillMastery("a", 0.4, 1, 0)]));
    }

    [Fact]
    public void CustomParametersAreUsed()
    {
        var parameters = new BktParameters(0.5, 0.0, 0.0, 0.0);

        var model = LearnerModel.New(parameters).Update(TestVerdicts.Clean(), ["x"]);

        // slip 0: a correct answer proves the skill is known.
        Assert.Equal(1.0, model.PKnown("x"), Tolerance);
        Assert.Equal(0.5, model.PKnown("unseen"), Tolerance);
    }
}
