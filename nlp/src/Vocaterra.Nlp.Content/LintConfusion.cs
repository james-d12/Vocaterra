using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.SlotGrammar;

namespace Vocaterra.Nlp.Content;

/// <summary>Check 2: an example of one intent that another intent's grammar also matches, or that the resolver would give to another intent.</summary>
internal static class LintConfusion
{
    public static void Run(LintContext context)
    {
        var intents = context.Bundle.Intents.Intents;
        var pairErrors = new HashSet<(string Owner, string Taken)>();
        foreach (var owner in intents)
        {
            foreach (var example in owner.ExampleSpecs)
            {
                var tokens = context.Evaluate(example.Text, context.Only(owner.Id.Value)).Analysis.Tokens;
                foreach (var other in intents)
                {
                    if (ReferenceEquals(owner, other))
                    {
                        continue;
                    }

                    CheckPair(context, owner, other, example.Text, tokens, pairErrors);
                }
            }
        }

        CheckPrompts(context, pairErrors);
    }

    private static void CheckPair(
        LintContext context,
        IntentDefinition owner,
        IntentDefinition other,
        string text,
        IReadOnlyList<AnalyzedToken> tokens,
        HashSet<(string Owner, string Taken)> pairErrors)
    {
        SlotGrammarMatch? best = null;
        foreach (var grammar in other.Grammars)
        {
            var match = SlotGrammarMatcher.Match(tokens, grammar);
            if (match is not null && (best is null || match.ExplicitTokenCount > best.ExplicitTokenCount))
            {
                best = match;
            }
        }

        var resolved = context.Evaluate(text, context.Only(owner.Id.Value, other.Id.Value)).Resolution;
        var resolvesToOwner = string.Equals(resolved.Intent.Value, owner.Id.Value, StringComparison.Ordinal);
        // An example that nothing resolves is reported once, by the coverage check; here only a rival intent matters.
        if (best is null && (resolvesToOwner || resolved.Intent.IsUnknown))
        {
            return;
        }

        var shared = context.PromptsAllowing(owner.Id.Value, other.Id.Value);
        var what = best is null
            ? $"the resolver gives it to '{resolved.Intent.Value}' (rung {resolved.Rung.ToString().ToLowerInvariant()})"
            : $"'{other.Id.Value}' grammar \"{best.Grammar.Pattern}\" also matches it"
                + (resolvesToOwner ? $"; '{owner.Id.Value}' wins only by the resolver's tie-break" : $", and the resolver gives it to '{resolved.Intent.Value}' (rung {resolved.Rung.ToString().ToLowerInvariant()})");
        string checkId;
        LintSeverity severity;
        string where;
        List<string> ids = [owner.Id.Value, other.Id.Value];
        if (shared.Count > 0)
        {
            checkId = "confusion.prompt";
            severity = resolvesToOwner ? LintSeverity.Warning : LintSeverity.Error;
            where = "prompt" + (shared.Count > 1 ? "s " : " ") + string.Join(", ", shared.Select(static prompt => prompt.PromptId)) + " allow" + (shared.Count > 1 ? " both" : "s both");
            ids.AddRange(shared.Select(static prompt => prompt.PromptId));
            if (!resolvesToOwner)
            {
                pairErrors.Add((owner.Id.Value, resolved.Intent.Value));
            }
        }
        else
        {
            checkId = "confusion.catalog";
            severity = LintSeverity.Info;
            where = "no prompt allows both intents";
        }

        context.Add(
            severity,
            checkId,
            ContentPaths.Intents,
            ids,
            $"example \"{text}\" of '{owner.Id.Value}': {what} ({where}).",
            severity == LintSeverity.Error ? null : LintContext.OverlapWaiver(owner, other));
    }

    // The resolver sees a prompt's whole allow-list, which can differ from the pair: an example that a prompt gives to a third intent is an error.
    private static void CheckPrompts(LintContext context, HashSet<(string Owner, string Taken)> pairErrors)
    {
        var grouped = new SortedDictionary<string, (IntentDefinition Owner, string Text, string Taken, SortedSet<string> Prompts)>(StringComparer.Ordinal);
        foreach (var prompt in context.Prompts.Where(static prompt => prompt.AllowedIntentIds.Count > 1))
        {
            foreach (var owner in context.Bundle.Intents.Intents.Where(intent => prompt.AllowedIntentIds.Contains(intent.Id.Value, StringComparer.Ordinal)))
            {
                foreach (var example in owner.ExampleSpecs)
                {
                    var taken = context.Evaluate(example.Text, prompt).Resolution.Intent.Value;
                    if (string.Equals(taken, owner.Id.Value, StringComparison.Ordinal) || pairErrors.Contains((owner.Id.Value, taken)))
                    {
                        continue;
                    }

                    var key = owner.Id.Value + "\u001f" + example.Text + "\u001f" + taken;
                    if (!grouped.TryGetValue(key, out var entry))
                    {
                        entry = (owner, example.Text, taken, new SortedSet<string>(StringComparer.Ordinal));
                        grouped.Add(key, entry);
                    }

                    entry.Prompts.Add(prompt.PromptId);
                }
            }
        }

        foreach (var (owner, text, taken, prompts) in grouped.Values)
        {
            var takenIntent = context.Bundle.Intents.Find(taken);
            context.Add(
                LintSeverity.Error,
                "confusion.prompt",
                ContentPaths.Intents,
                [owner.Id.Value, takenIntent is null ? taken : takenIntent.Id.Value, .. prompts],
                $"example \"{text}\" of '{owner.Id.Value}' resolves to '{taken}' in prompt{(prompts.Count > 1 ? "s" : string.Empty)} {string.Join(", ", prompts)}.");
        }
    }
}
