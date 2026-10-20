using System.Globalization;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Host;

/// <summary>Writes one utterance's outcome as text: the verdict summary, the agent's lines, the effects and the repair step.</summary>
public static class VerdictReport
{
    /// <summary>Writes the outcome of evaluating <paramref name="transcript"/> on <paramref name="promptId"/>.</summary>
    public static void Write(TextWriter output, string promptId, string transcript, PipelineResult result)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(result);
        var verdict = result.Verdict;
        output.WriteLine($"{promptId}: \"{transcript}\"");
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"  intent={verdict.Intent.Value} communicated={Lower(verdict.Communicated)} correctness={Format(verdict.Correctness)}"));
        foreach (var error in verdict.Errors)
        {
            output.WriteLine($"  error {error.Tag}: expected '{error.Expected}', heard '{error.Actual}'");
        }

        foreach (var effect in result.Effects)
        {
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"  effect {effect.EffectId} {effect.Kind} {Format(effect.Value)}"));
        }

        foreach (var line in result.Response.Lines)
        {
            output.WriteLine($"  agent: {line.Text}");
        }

        if (result.Repair is { } repair)
        {
            output.WriteLine($"  repair: {repair.Kind} {repair.AgentLine}");
        }
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string Lower(bool value)
    {
        return value ? "true" : "false";
    }
}
