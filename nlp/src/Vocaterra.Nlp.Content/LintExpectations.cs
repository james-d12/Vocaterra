using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Language.Intent;

namespace Vocaterra.Nlp.Content;

/// <summary>
/// Check 6: an example written as <c>{ "text": ..., "errors": [...] }</c> is graded with <see cref="TextGrader"/> and the verdict's error tags must equal the
/// expected set. The prompt is the example's own <c>prompt</c> when it names one, otherwise the first open-mode prompt (ordinal id) that allows the example's intent, or the first prompt of any mode when
/// none is open (an open prompt grades a sentence; a constrained prompt expects one of its answer set's phrases, so it would add <c>vocab.wrong</c> to a sentence);
/// the tags then depend on that prompt's answer set and difficulty, exactly as when a player answers it.
/// </summary>
internal static class LintExpectations
{
    public static void Run(LintContext context)
    {
        TextGrader? grader = null;
        foreach (var intent in context.Bundle.Intents.Intents)
        {
            foreach (var example in intent.ExampleSpecs.Where(static example => example.HasExpectation))
            {
                var prompt = Choose(context, intent.Id.Value, example);
                if (prompt is null)
                {
                    continue;
                }

                grader ??= new TextGrader(context.Bundle);
                var actual = grader.Grade(example.Text, prompt).Errors.Select(static error => error.Tag).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                var expected = example.ExpectedErrors!.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
                if (!actual.SequenceEqual(expected))
                {
                    context.Add(
                        LintSeverity.Error,
                        "expect.mismatch",
                        ContentPaths.Intents,
                        [intent.Id.Value, prompt.PromptId],
                        $"example \"{example.Text}\" expects error tags [{string.Join(", ", expected)}] but prompt '{prompt.PromptId}' gives [{string.Join(", ", actual)}].");
                }
            }
        }
    }

    private static PromptSpec? Choose(LintContext context, string intentId, IntentExample example)
    {
        if (example.Prompt is { } promptId)
        {
            if (!context.Bundle.Prompts.TryGetValue(promptId, out var named))
            {
                context.Add(LintSeverity.Error, "expect.unknown-prompt", ContentPaths.Intents, [intentId, promptId], $"example \"{example.Text}\" names prompt '{promptId}', which the bundle does not have.");
                return null;
            }

            if (!named.AllowedIntentIds.Contains(intentId, StringComparer.Ordinal))
            {
                context.Add(LintSeverity.Error, "expect.prompt-intent", ContentPaths.Intents, [intentId, promptId], $"example \"{example.Text}\" names prompt '{promptId}', which does not allow intent '{intentId}'.");
                return null;
            }

            return named;
        }

        var chosen = context.PromptsAllowing(intentId).OrderBy(static prompt => prompt.Mode == RecognitionMode.Open ? 0 : 1).FirstOrDefault();
        if (chosen is null)
        {
            context.Add(
                LintSeverity.Warning,
                "expect.no-prompt",
                ContentPaths.Intents,
                [intentId],
                $"example \"{example.Text}\" has expected errors, but no prompt allows intent '{intentId}' to grade it against (name a prompt in the example, or allow the intent in a prompt).");
        }

        return chosen;
    }
}
