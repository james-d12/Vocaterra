namespace Vocaterra.Nlp.Pronunciation.Tests;

/// <summary>Golden tests for the phone aligner and its tie-breaking.</summary>
public class PhoneAlignerTests
{
    private static string Ops(string[] hyp, string[] reference)
    {
        var alignment = PhoneAligner.Align(hyp, reference);
        return string.Join(' ', alignment.Steps.Select(static s => s.Op switch
        {
            AlignmentOp.Match => "M",
            AlignmentOp.Substitute => "S",
            AlignmentOp.Delete => "D",
            _ => "I",
        }));
    }

    [Fact]
    public void IdenticalSequencesAreAllMatches()
    {
        var alignment = PhoneAligner.Align(["a", "b", "c"], ["a", "b", "c"]);

        Assert.Equal(0, alignment.Distance);
        Assert.Equal(
            [new AlignmentStep(AlignmentOp.Match, 0, 0), new AlignmentStep(AlignmentOp.Match, 1, 1), new AlignmentStep(AlignmentOp.Match, 2, 2)],
            alignment.Steps);
    }

    [Fact]
    public void SubstitutionDeletionAndInsertion()
    {
        Assert.Equal("M S M", Ops(["a", "x", "c"], ["a", "b", "c"]));
        Assert.Equal("M D M", Ops(["a", "c"], ["a", "b", "c"]));
        Assert.Equal("M M I M", Ops(["a", "b", "x", "c"], ["a", "b", "c"]));
    }

    [Fact]
    public void StepsCarryTheIndicesOfBothSides()
    {
        var alignment = PhoneAligner.Align(["a", "c"], ["a", "b", "c"]);

        Assert.Equal(1, alignment.Distance);
        Assert.Equal(new AlignmentStep(AlignmentOp.Delete, -1, 1), alignment.Steps[1]);
        Assert.Equal(new AlignmentStep(AlignmentOp.Match, 1, 2), alignment.Steps[2]);

        var insertion = PhoneAligner.Align(["h", "o"], ["o"]);
        Assert.Equal(new AlignmentStep(AlignmentOp.Insert, 0, -1), insertion.Steps[0]);
        Assert.Equal(new AlignmentStep(AlignmentOp.Match, 1, 0), insertion.Steps[1]);
    }

    [Fact]
    public void EmptySides()
    {
        Assert.Equal("D D", Ops([], ["a", "b"]));
        Assert.Equal("I I", Ops(["a", "b"], []));
        Assert.Equal(string.Empty, Ops([], []));
        Assert.Equal(2, PhoneAligner.Align([], ["a", "b"]).Distance);
    }

    [Fact]
    public void SubstituteIsPreferredOverDeleteAndInsert()
    {
        // "x a" against "a y": substituting twice and inserting/deleting cost the same; Substitute wins.
        Assert.Equal("S S", Ops(["x", "a"], ["a", "y"]));
        Assert.Equal("S", Ops(["a"], ["b"]));
    }

    [Fact]
    public void MatchIsTakenEarliestInReadingOrder()
    {
        Assert.Equal("M I", Ops(["a", "a"], ["a"]));
        Assert.Equal("M D", Ops(["a"], ["a", "a"]));
    }

    [Fact]
    public void RotationUsesInsertAndDelete()
    {
        Assert.Equal("I M M D", Ops(["a", "b", "c"], ["b", "c", "a"]));
    }

    [Fact]
    public void AlignmentIsDeterministicAndValueEqual()
    {
        string[] hyp = ["p", "e", "ɹ", "o"];
        string[] reference = ["p", "e", "r", "o"];

        Assert.Equal(PhoneAligner.Align(hyp, reference), PhoneAligner.Align(hyp, reference));
        Assert.Equal(PhoneAligner.Align(hyp, reference).GetHashCode(), PhoneAligner.Align(hyp, reference).GetHashCode());
        Assert.NotEqual(PhoneAligner.Align(hyp, reference), PhoneAligner.Align(reference, reference));
    }

    [Fact]
    public void ComparisonIsOrdinalOverWholeTokens()
    {
        // The two-code-point token "tʃ" is one phone and does not match "t".
        Assert.Equal("S", Ops(["tʃ"], ["t"]));
        Assert.Equal("M", Ops(["tʃ"], ["tʃ"]));
    }
}
