using Slg.Core.Verdicts;

namespace Slg.Decision.Tests;

/// <summary>Builders for verdicts used across the decision tests.</summary>
internal static class TestVerdicts
{
    public static ErrorTag Error(string tag, int start = 0, string expected = "esperado", string actual = "obtenido")
    {
        return new ErrorTag(tag, new TextSpan(start, 3), expected, actual);
    }

    public static Verdict Make(string intent, bool communicated, double correctness, params ErrorTag[] errors)
    {
        return new Verdict(
            new IntentId(intent),
            SlotMap.Empty,
            communicated,
            correctness,
            errors,
            [],
            1.0,
            ResolvedBy.Exact,
            VersionStamp.Empty);
    }

    public static Verdict Clean(string intent = "greet")
    {
        return Make(intent, true, 1.0);
    }

    public static Verdict WithErrors(params ErrorTag[] errors)
    {
        return Make("buy_item", true, 0.7, errors);
    }
}
