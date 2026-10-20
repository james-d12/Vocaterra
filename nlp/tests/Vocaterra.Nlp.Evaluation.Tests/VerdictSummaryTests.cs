using System.Globalization;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Evaluation.Tests;

/// <summary>Tests for <see cref="VerdictSummary"/>.</summary>
public class VerdictSummaryTests
{
    [Fact]
    public void WorkedExampleHasAStableOneLineDescription()
    {
        var verdict = VerdictAssembler.Assemble(Fixtures.WorkedExample());

        Assert.Equal(
            "intent=offer_item communicated=true correctness=0.6 confidence=0.9 resolvedBy=Grammar slots={item=espada, qty=2} "
            + "errors=[conj.person@3+5 tengo/tiene; noun.number@13+6 espadas/espada] phones=0",
            VerdictSummary.Describe(verdict));
    }

    [Fact]
    public void UnresolvedVerdictDescribesWithoutErrors()
    {
        var verdict = Fixtures.MakeVerdict(communicated: false, correctness: 1.0, confidence: 0.25) with
        {
            Intent = IntentId.Unknown,
            ResolvedBy = ResolvedBy.Unresolved,
        };

        Assert.Equal(
            "intent=unknown communicated=false correctness=1 confidence=0.25 resolvedBy=Unresolved slots={} errors=[] phones=0",
            VerdictSummary.Describe(verdict));
    }

    [Fact]
    public void DescriptionIgnoresTheCurrentCulture()
    {
        var verdict = Fixtures.MakeVerdict(correctness: 0.5, confidence: 0.75, phones: [Fixtures.Phone(0.5), Fixtures.Phone(1.0)]);
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-ES");
            Assert.Contains("correctness=0.5 confidence=0.75", VerdictSummary.Describe(verdict), StringComparison.Ordinal);
            Assert.EndsWith("phones=2", VerdictSummary.Describe(verdict), StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void DescribeRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => VerdictSummary.Describe(null!));
    }
}
