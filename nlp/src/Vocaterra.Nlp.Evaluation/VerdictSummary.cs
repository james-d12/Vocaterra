using System.Globalization;
using System.Text;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Evaluation;

/// <summary>Renders a verdict as one stable, non-localized line for logs and test failure messages.</summary>
public static class VerdictSummary
{
    /// <summary>
    /// Returns a single line such as
    /// <c>intent=offer_item communicated=true correctness=0.6 confidence=0.9 resolvedBy=Grammar slots={item=espada, qty=2} errors=[conj.person@3+5 tengo/tiene] phones=0</c>.
    /// Numbers use the invariant culture; errors print as <c>tag@start+length expected/actual</c>.
    /// </summary>
    public static string Describe(Verdict verdict)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        var text = new StringBuilder();
        text.Append("intent=").Append(verdict.Intent);
        text.Append(" communicated=").Append(verdict.Communicated ? "true" : "false");
        text.Append(" correctness=").Append(Format(verdict.Correctness));
        text.Append(" confidence=").Append(Format(verdict.Confidence));
        text.Append(" resolvedBy=").Append(verdict.ResolvedBy);
        text.Append(" slots=").Append(verdict.Slots);
        text.Append(" errors=[");
        for (var i = 0; i < verdict.Errors.Count; i++)
        {
            var error = verdict.Errors[i];
            if (i > 0)
            {
                text.Append("; ");
            }

            text.Append(error.Tag)
                .Append('@').Append(error.Span.Start.ToString(CultureInfo.InvariantCulture))
                .Append('+').Append(error.Span.Length.ToString(CultureInfo.InvariantCulture))
                .Append(' ').Append(error.Expected).Append('/').Append(error.Actual);
        }

        text.Append("] phones=").Append(verdict.Pronunciation.Count.ToString(CultureInfo.InvariantCulture));
        return text.ToString();
    }

    private static string Format(double value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }
}
