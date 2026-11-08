namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>
/// Grades one line at one prompt of <c>content/game-v1</c> through the whole pipeline, as the CLI's eval does, and compares the intent, whether it was communicated and
/// each error tag with its expected word. The line and the prompt are in the failure message, so it names the line.
/// </summary>
internal static class LineGrading
{
    /// <summary>The errors of a verdict as <c>tag=expected</c> (just <c>tag</c> when it names no word).</summary>
    public static string[] ErrorsOf(Core.Verdicts.Verdict verdict) =>
        [.. verdict.Errors.Select(static e => e.Expected.Length == 0 ? e.Tag : e.Tag + "=" + e.Expected)];

    public static void Check(string prompt, string line, string intent, bool communicated, string[] errors)
    {
        var spec = TestSupport.Bundle.Prompts[prompt];
        var verdict = TestSupport.Pipeline(line, spec.Mode).Evaluate(TestSupport.Clip(), spec, TestSupport.Context(agentId: spec.AgentId ?? "player")).Verdict;

        Assert.Equal(Describe(prompt, line, intent, communicated, errors), Describe(prompt, line, verdict.Intent.Value, verdict.Communicated, ErrorsOf(verdict)));
    }

    /// <summary>The same check, with the intent the line has off the prompt (M72; <c>none</c> when there is none) beside the others (M74).</summary>
    public static void Check(string prompt, string line, string intent, bool communicated, string[] errors, string offPrompt)
    {
        var spec = TestSupport.Bundle.Prompts[prompt];
        var verdict = TestSupport.Pipeline(line, spec.Mode).Evaluate(TestSupport.Clip(), spec, TestSupport.Context(agentId: spec.AgentId ?? "player")).Verdict;

        Assert.Equal(
            Describe(prompt, line, intent, communicated, errors) + " off-prompt=" + offPrompt,
            Describe(prompt, line, verdict.Intent.Value, verdict.Communicated, ErrorsOf(verdict)) + " off-prompt=" + (verdict.OffPromptIntent?.Value ?? "none"));
    }

    private static string Describe(string prompt, string line, string intent, bool communicated, string[] errors) =>
        $"{prompt} | {line} => {intent} communicated={communicated} errors=[{string.Join(' ', errors)}]";
}
