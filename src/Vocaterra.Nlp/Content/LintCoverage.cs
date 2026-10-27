using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.Packs;

namespace Vocaterra.Nlp.Content;

/// <summary>Check 1: every intent example resolves to its own intent, and by which rung.</summary>
internal static class LintCoverage
{
    private static readonly NormalizationOptions KeepArticles = new(ConvertIndefiniteArticles: false);

    public static void Run(LintContext context)
    {
        var language = context.Bundle.Language;
        foreach (var intent in context.Bundle.Intents.Intents)
        {
            var own = context.Only(intent.Id.Value);
            foreach (var example in intent.ExampleSpecs)
            {
                var evaluated = context.Evaluate(example.Text, own);
                var resolution = evaluated.Resolution;
                if (!string.Equals(resolution.Intent.Value, intent.Id.Value, StringComparison.Ordinal))
                {
                    context.Add(
                        LintSeverity.Error,
                        "coverage.uncovered",
                        ContentPaths.Intents,
                        [intent.Id.Value],
                        $"example \"{example.Text}\" is not resolved by any grammar or keyword rule of the intent.",
                        suggestion: Skeleton(intent, evaluated, language));
                }
                else if (resolution.Rung == IntentRung.Rules)
                {
                    var waived = intent.Lint.AllowRuleOnly?.Contains(example.Text, StringComparer.Ordinal) == true
                        ? $"{intent.Id.Value} lint.allowRuleOnly \"{example.Text}\""
                        : null;
                    context.Add(
                        LintSeverity.Warning,
                        "coverage.rule-only",
                        ContentPaths.Intents,
                        [intent.Id.Value],
                        $"example \"{example.Text}\" is covered only by a keyword rule; no slot grammar matches it.",
                        waived);
                }

                if (example.ExpectedErrors?.Contains(ErrorTags.VocabUnknown, StringComparer.Ordinal) != true)
                {
                    foreach (var word in evaluated.Analysis.Errors.Where(static error => error.Tag == ErrorTags.VocabUnknown).Select(static error => error.Actual).Distinct(StringComparer.Ordinal))
                    {
                        context.Add(
                            LintSeverity.Warning,
                            "coverage.unknown-word",
                            ContentPaths.Intents,
                            [intent.Id.Value],
                            $"example \"{example.Text}\" has the word '{word}', which the lexicon does not know (add it to the lexicon, or bind it with a [WORD] element if it is a name).");
                    }
                }
            }
        }
    }

    // A hint for an uncovered example: closed-class words stay literal, open-class words become [POS] or [POS:lemma], nouns that vary between the
    // intent's examples take the intent's noun slot (the first required slot that is not the quantity), numerals the quantity slot.
    private static string Skeleton(IntentDefinition intent, Evaluated evaluated, ILanguagePack language)
    {
        var words = evaluated.Analysis.Words ?? [];
        var nounSlot = intent.RequiredSlots.FirstOrDefault(static slot => !string.Equals(slot, IntentCatalog.QuantitySlot, StringComparison.Ordinal));
        var quantity = intent.RequiredSlots.Contains(IntentCatalog.QuantitySlot, StringComparer.Ordinal);
        var otherTokens = intent.Examples
            .Select(example => language.Normalizer.Normalize(example, KeepArticles).Tokens.Select(static token => token.Text).ToHashSet(StringComparer.Ordinal))
            .ToList();
        var politePhrase = language.Lint.PolitePhrase;
        var elements = new List<string>();
        var slotUsed = false;
        for (var i = 0; i < words.Count; i++)
        {
            var word = words[i];
            var text = word.Token.Text;
            if (i == 0 && words.Count > 1 && language.Lint.SubjectPronouns.Contains(text, StringComparer.Ordinal))
            {
                continue;
            }

            if (IsPolitePhraseAt(words, i, politePhrase))
            {
                break;
            }

            if (word.Role != WordRole.Word || word.Reading is not { } reading)
            {
                elements.Add(WithSlot("WORD", ref slotUsed, nounSlot));
                continue;
            }

            switch (reading.Pos)
            {
                case LexiconSchema.Article:
                    elements.Add("[ART]");
                    break;
                case LexiconSchema.Verb:
                    elements.Add($"[VERB:{reading.Lemma}]");
                    break;
                case LexiconSchema.Adjective:
                    elements.Add("[ADJ]");
                    break;
                case LexiconSchema.Numeral when language.Lint.ArticleNumeralHomographs.Contains(text, StringComparer.Ordinal):
                    elements.Add("[ART]");
                    break;
                case LexiconSchema.Numeral:
                    elements.Add(quantity ? $"[NUM:{IntentCatalog.QuantitySlot}]" : "[NUM]");
                    break;
                case LexiconSchema.Noun:
                    var constant = otherTokens.Count > 1 && otherTokens.All(tokens => tokens.Contains(text));
                    elements.Add(constant ? text : WithSlot("NOUN", ref slotUsed, nounSlot));
                    break;
                default:
                    elements.Add(text);
                    break;
            }
        }

        return string.Join(' ', elements);
    }

    // True when the words from the index on are the polite phrase and end the example (the phrase is the last thing the example says).
    private static bool IsPolitePhraseAt(IReadOnlyList<ResolvedWord> words, int index, IReadOnlyList<string> phrase)
    {
        if (phrase.Count == 0 || index != words.Count - phrase.Count || words.Count <= phrase.Count)
        {
            return false;
        }

        for (var k = 0; k < phrase.Count; k++)
        {
            if (!string.Equals(words[index + k].Token.Text, phrase[k], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string WithSlot(string tag, ref bool slotUsed, string? slot)
    {
        if (slot is null || slotUsed)
        {
            return $"[{tag}]";
        }

        slotUsed = true;
        return $"[{tag}:{slot}]";
    }
}
