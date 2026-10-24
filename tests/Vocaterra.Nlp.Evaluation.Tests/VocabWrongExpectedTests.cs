using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Evaluation.Tests;

/// <summary>M17c: a derived vocab.wrong names the word of the answer only when the answer set has exactly one answer.</summary>
public class VocabWrongExpectedTests
{
    [Fact]
    public void AOneAnswerSetNamesItsCanonicalAnswerAsTheExpectedWord()
    {
        var error = Assert.Single(VerdictAssembler.Assemble(Inputs("gato", ["perro"])).Errors);

        Assert.Equal(new ErrorTag(ErrorTags.VocabWrong, new TextSpan(0, 4), "perro", "gato"), error);
    }

    [Fact]
    public void AnAnswerSetWithSeveralAnswersHasNoExpectedWord()
    {
        var error = Assert.Single(VerdictAssembler.Assemble(Inputs("gato", ["perro", "gato negro"])).Errors);

        Assert.Equal(new ErrorTag(ErrorTags.VocabWrong, new TextSpan(0, 4), string.Empty, "gato"), error);
    }

    [Fact]
    public void WithoutTheCanonicalAnswersTheExpectedWordIsEmpty()
    {
        var error = Assert.Single(VerdictAssembler.Assemble(Inputs("gato", null)).Errors);

        Assert.Equal(string.Empty, error.Expected);
        Assert.Equal(ErrorTags.VocabWrong, error.Tag);
    }

    // A quiz prompt on the animals set: the player says a known word that is not in the set.
    private static VerdictInputs Inputs(string spoken, IReadOnlyList<string>? canonicalAnswers)
    {
        return new VerdictInputs(
            Fixtures.Prompt(RecognitionMode.Constrained, answerSetId: "animals"),
            Fixtures.Asr(mode: RecognitionMode.Constrained),
            Fixtures.NoMatch(),
            Fixtures.Analysis([Fixtures.Token(spoken, 0, "noun")]),
            Fixtures.UnknownIntent(),
            null,
            [],
            [spoken, "perro"],
            EvaluationConfig.Default,
            Fixtures.Versions(),
            canonicalAnswers);
    }
}
