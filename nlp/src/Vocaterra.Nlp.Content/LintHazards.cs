using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Lexicon;

namespace Vocaterra.Nlp.Content;

/// <summary>Check 3: grammar constructs with a known reading problem, and words the lexicon lacks.</summary>
internal static class LintHazards
{
    private const int MaxFormsListed = 6;

    public static void Run(LintContext context)
    {
        var lexicon = context.Bundle.Lexicon;
        foreach (var intent in context.Bundle.Intents.Intents)
        {
            var homographs = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var grammar in intent.Grammars)
            {
                foreach (var hazard in grammar.OptionalWordNeighbours().Distinct(StringComparer.Ordinal))
                {
                    context.Add(
                        LintSeverity.Warning,
                        "hazard.optional-word",
                        ContentPaths.Intents,
                        [intent.Id.Value],
                        $"grammar \"{grammar.Pattern}\" has an optional [WORD] beside an optional element ({hazard}); the name can absorb a word meant for the other element or for none.");
                }

                foreach (var word in grammar.LiteralWords().Where(word => !context.Knows(word)))
                {
                    context.Add(
                        LintSeverity.Error,
                        "hazard.lexicon-gap",
                        ContentPaths.Intents,
                        [intent.Id.Value],
                        $"grammar \"{grammar.Pattern}\" has the word '{word}', which the lexicon does not know.");
                }

                foreach (var lemma in grammar.VerbLemmas())
                {
                    if (!lexicon.EntriesForLemma(lemma).Any(static entry => string.Equals(entry.Pos, LexiconSchema.Verb, StringComparison.Ordinal)))
                    {
                        context.Add(
                            LintSeverity.Error,
                            "hazard.lexicon-gap",
                            ContentPaths.Intents,
                            [intent.Id.Value],
                            $"grammar \"{grammar.Pattern}\" has [VERB:{lemma}], but the lexicon has no verb with that lemma.");
                    }
                    else if (!homographs.ContainsKey(lemma))
                    {
                        homographs.Add(lemma, grammar.Pattern);
                    }
                }
            }

            foreach (var rule in intent.KeywordRules)
            {
                foreach (var lemma in rule.Lemmas.Where(lemma => !context.Knows(lemma)))
                {
                    context.Add(
                        LintSeverity.Error,
                        "hazard.lexicon-gap",
                        ContentPaths.Intents,
                        [intent.Id.Value],
                        $"a keyword rule has the lemma '{lemma}', which the lexicon does not know.");
                }
            }

            foreach (var (lemma, pattern) in homographs)
            {
                var nounForms = lexicon.EntriesForLemma(lemma)
                    .Where(static entry => string.Equals(entry.Pos, LexiconSchema.Verb, StringComparison.Ordinal))
                    .Select(static entry => entry.Form)
                    .Distinct(StringComparer.Ordinal)
                    .Where(form => lexicon.Lookup(form).Any(static reading => string.Equals(reading.Pos, LexiconSchema.Noun, StringComparison.Ordinal)))
                    .Order(StringComparer.Ordinal)
                    .ToList();
                if (nounForms.Count == 0)
                {
                    continue;
                }

                var listed = string.Join(", ", nounForms.Take(MaxFormsListed)) + (nounForms.Count > MaxFormsListed ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $" and {nounForms.Count - MaxFormsListed} more") : string.Empty);
                var waived = intent.Lint.AllowHomograph?.Contains(lemma, StringComparer.Ordinal) == true
                    ? $"{intent.Id.Value} lint.allowHomograph {lemma}"
                    : null;
                // Info, not a warning: inside the grammar the typed element binds the verb reading, so a sentence that fits is read as a verb; the noun
                // reading only matters outside it (a rule, another grammar, a [WORD] neighbour), which the coverage and confusion checks test directly.
                context.Add(
                    LintSeverity.Info,
                    "hazard.homograph",
                    ContentPaths.Intents,
                    [intent.Id.Value, lemma],
                    $"[VERB:{lemma}] (first in grammar \"{pattern}\"): the verb forms {listed} are also nouns; the grammar binds the verb reading, but outside it the noun reading exists.",
                    waived);
            }
        }
    }
}
