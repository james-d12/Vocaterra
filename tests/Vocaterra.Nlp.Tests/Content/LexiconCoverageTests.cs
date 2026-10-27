using System.Text;
using Vocaterra.Nlp.Core.Taxonomy;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.Packs;

namespace Vocaterra.Nlp.Content.Tests;

/// <summary>
/// Permanent coverage gates for the merged lexicon of the real bundle: every word that the authored content uses must be known, and a hand-written list of
/// common beginner words must be known. They keep new content honest: a grammar, example, answer or dialogue line that uses a word the lexicon lacks fails
/// here with the word and the place, instead of surfacing later as a <c>vocab.unknown</c> on a correct sentence. When one fails, add the word to a
/// hand-authored lexicon file (or raise the lemma count of the import and regenerate), then re-hash the bundle.
/// </summary>
public class LexiconCoverageTests
{
    private static readonly ContentBundle Bundle = TestSupport.Bundle;

    // The pipeline normalizes with indefinite articles kept, so the tests do too.
    private static readonly NormalizationOptions PipelineOptions = new(ConvertIndefiniteArticles: false);

    private static readonly LinguisticAnalyzer Analyzer = new(Bundle.Lexicon, Bundle.SpanishMorphology(), SpanishLanguagePack.Instance);

    private static bool Knows(string word)
    {
        return Bundle.Lexicon.Lookup(word).Count > 0 || Bundle.Lexicon.ContainsLemma(word);
    }

    // The words of a text that the analyzer does not know; digits are numerals and are always known.
    private static IEnumerable<string> UnknownWords(string text)
    {
        var analysis = Analyzer.Analyze(SpanishTextNormalizer.Normalize(text, PipelineOptions));
        return analysis.Tokens.Where(static token => token.Readings.Count == 0).Select(static token => token.Token.Text);
    }

    private static void AssertNoneMissing(List<string> missing, string what)
    {
        Assert.True(missing.Count == 0, $"{missing.Count} {what}:\n  " + string.Join("\n  ", missing.Order(StringComparer.Ordinal)));
    }

    // ---- Intent grammars, keyword rules and examples ---------------------------------------------------------------------------------

    // The literal words and verb lemmas of a canonical grammar pattern: "[PRON]? [VERB:ofrecer:verb] (bola|lanza) de [NOUN:spell]".
    private static IEnumerable<(string Kind, string Word)> GrammarWords(string pattern)
    {
        foreach (var raw in pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var alternative in raw.Trim('(', ')', '?').Split('|', StringSplitOptions.RemoveEmptyEntries))
            {
                var token = alternative.Trim('(', ')', '?');
                if (token.StartsWith('['))
                {
                    var parts = token.Trim('[', ']').Split(':');
                    if (parts[0] == "VERB" && parts.Length >= 2 && parts[1].Length > 0)
                    {
                        yield return ("verb lemma", parts[1]);
                    }

                    continue;
                }

                if (token.Length > 0)
                {
                    yield return ("word", token);
                }
            }
        }
    }

    [Fact]
    public void EveryWordAndVerbLemmaOfTheSlotGrammarsIsInTheLexicon()
    {
        var missing = new List<string>();
        foreach (var intent in Bundle.Intents.Intents)
        {
            foreach (var grammar in intent.Grammars)
            {
                foreach (var (kind, word) in GrammarWords(grammar.Pattern))
                {
                    var known = kind == "verb lemma"
                        ? Bundle.Lexicon.EntriesForLemma(word).Any(static entry => entry.Pos == "verb")
                        : Knows(word);
                    if (!known)
                    {
                        missing.Add($"{kind} '{word}' in grammar '{grammar.Pattern}' of intent '{intent.Id.Value}'");
                    }
                }
            }
        }

        AssertNoneMissing(missing, "grammar words are not in the lexicon");
    }

    [Fact]
    public void EveryLemmaOfTheKeywordRulesIsInTheLexicon()
    {
        var missing = new List<string>();
        foreach (var intent in Bundle.Intents.Intents)
        {
            foreach (var rule in intent.KeywordRules)
            {
                missing.AddRange(rule.Lemmas.Where(lemma => !Knows(lemma)).Select(lemma => $"lemma '{lemma}' in a keyword rule of intent '{intent.Id.Value}'"));
            }
        }

        AssertNoneMissing(missing, "keyword rule lemmas are not in the lexicon");
    }

    [Fact]
    public void EveryIntentExampleAnalyzesWithoutVocabUnknown()
    {
        var missing = new List<string>();
        foreach (var intent in Bundle.Intents.Intents)
        {
            foreach (var spec in intent.ExampleSpecs)
            {
                // An example that expects vocab.unknown is a deliberate learner error (a misspelling the lesson tests for), not a lexicon gap.
                if (spec.HasExpectation && spec.ExpectedErrors!.Contains(ErrorTags.VocabUnknown))
                {
                    continue;
                }

                var example = spec.Text;
                var analysis = Analyzer.Analyze(SpanishTextNormalizer.Normalize(example, PipelineOptions));
                foreach (var error in analysis.Errors.Where(static error => error.Tag == ErrorTags.VocabUnknown))
                {
                    missing.Add($"word '{error.Actual}' in the example \"{example}\" of intent '{intent.Id.Value}' is {ErrorTags.VocabUnknown}");
                }
            }
        }

        AssertNoneMissing(missing, "intent example words are vocab.unknown");
    }

    // ---- Answer sets and dialogue templates ------------------------------------------------------------------------------------------

    [Fact]
    public void EveryWordOfTheAnswerSetPhrasesIsInTheLexicon()
    {
        var missing = new List<string>();
        foreach (var set in Bundle.AnswerSets.Values)
        {
            foreach (var answer in set.Answers)
            {
                foreach (var phrase in answer.Aliases.Prepend(answer.Canonical))
                {
                    missing.AddRange(UnknownWords(phrase).Select(word => $"word '{word}' in the phrase \"{phrase}\" of answer '{answer.AnswerId}' in answer set '{set.AnswerSetId}'"));
                }
            }
        }

        AssertNoneMissing(missing, "answer set words are not in the lexicon");
    }

    [Fact]
    public void EveryWordOfTheDialogueTemplatesIsInTheLexicon()
    {
        var missing = new List<string>();
        foreach (var pool in Bundle.Dialogue.Pools.Values)
        {
            foreach (var variant in pool.Variants)
            {
                missing.AddRange(UnknownWords(LiteralText(variant.Text)).Select(word => $"word '{word}' in dialogue line '{variant.LineId}' (\"{variant.Text}\")"));
            }
        }

        AssertNoneMissing(missing, "dialogue template words are not in the lexicon");
    }

    // The author's own words: placeholders such as {item:article(def)} are removed and the escapes {{ and }} are dropped.
    private static string LiteralText(string template)
    {
        var builder = new StringBuilder(template.Length);
        var i = 0;
        while (i < template.Length)
        {
            var c = template[i];
            if ((c == '{' || c == '}') && i + 1 < template.Length && template[i + 1] == c)
            {
                i += 2;
            }
            else if (c == '{')
            {
                var close = template.IndexOf('}', i + 1);
                i = close < 0 ? template.Length : close + 1;
                builder.Append(' ');
            }
            else
            {
                builder.Append(c);
                i++;
            }
        }

        return builder.ToString();
    }

    [Fact]
    public void TheGrammarWordExtractionUnderstandsTheDsl()
    {
        var words = GrammarWords("[PRON]? [VERB:ofrecer:verb] [NUM:qty] (bola|lanza) de [NOUN:spell] [VERB:querer] cuánto").ToArray();

        Assert.Equal(
            [("verb lemma", "ofrecer"), ("word", "bola"), ("word", "lanza"), ("word", "de"), ("verb lemma", "querer"), ("word", "cuánto")],
            words);
    }

    [Fact]
    public void ALiteralTextDropsPlaceholdersAndEscapes()
    {
        var words = LiteralText("Por {item:article(def)} pido {{ {qty} }} monedas.").Split(' ', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(["Por", "pido", "monedas."], words);
        Assert.DoesNotContain('{', LiteralText("{item:article(def):cap} cuesta {qty} monedas."));
    }

    // ---- Common beginner words ---------------------------------------------------------------------------------------------------------

    private static readonly string[] Verbs =
    [
        "ayudar", "buscar", "comer", "beber", "comprar", "vender", "hablar", "vivir", "trabajar", "querer", "poder", "saber", "abrir", "pagar", "dormir",
        "tener", "hacer", "venir", "decir", "estar",
    ];

    private static readonly string[] Nouns =
    [
        "casa", "ciudad", "familia", "amigo", "madre", "padre", "mujer", "hombre", "día", "noche", "agua", "comida", "dinero", "trabajo", "nombre", "cerveza",
    ];

    private static readonly string[] Adjectives =
    [
        "grande", "pequeño", "bueno", "malo", "nuevo", "viejo", "feliz", "cansado", "difícil", "rojo", "caro", "barato",
    ];

    private static readonly string[] Adverbs =
    [
        "aquí", "allí", "ahora", "hoy", "mañana", "ayer", "siempre", "nunca", "muy", "bien", "también", "después",
    ];

    [Fact]
    public void TheCommonVerbsAreVerbLemmas()
    {
        var missing = Verbs.Where(verb => !Bundle.Lexicon.EntriesForLemma(verb).Any(static entry => entry.Pos == "verb")).ToList();

        AssertNoneMissing(missing, "common verbs are not verb lemmas of the lexicon");
    }

    [Fact]
    public void TheCommonNounsHaveANounReading()
    {
        var missing = Nouns.Where(noun => !Bundle.Lexicon.Lookup(noun).Any(static reading => reading.Pos == "noun")).ToList();

        AssertNoneMissing(missing, "common nouns have no noun reading");
    }

    [Fact]
    public void TheCommonAdjectivesHaveAnAdjectiveReading()
    {
        var missing = Adjectives.Where(adjective => !Bundle.Lexicon.Lookup(adjective).Any(static reading => reading.Pos == "adj")).ToList();

        AssertNoneMissing(missing, "common adjectives have no adjective reading");
    }

    [Fact]
    public void TheCommonAdverbsAreKnown()
    {
        var missing = Adverbs.Where(adverb => !Knows(adverb)).ToList();

        AssertNoneMissing(missing, "common adverbs are not in the lexicon");
    }

    [Fact]
    public void TheListIsAboutSixtyWordsAndHasNoDuplicates()
    {
        var all = Verbs.Concat(Nouns).Concat(Adjectives).Concat(Adverbs).ToArray();

        Assert.InRange(all.Length, 50, 70);
        Assert.Equal(all.Length, all.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("hablamos", "hablar")]
    [InlineData("quiero", "querer")]
    [InlineData("tienen", "tener")]
    [InlineData("compré", "comprar")]
    [InlineData("vivimos", "vivir")]
    [InlineData("niñas", "niña")]
    [InlineData("grandes", "grande")]
    [InlineData("buenas", "bueno")]
    public void CommonInflectedFormsReadAsTheirLemma(string form, string lemma)
    {
        Assert.Contains(Bundle.Lexicon.Lookup(form), reading => reading.Lemma == lemma);
    }
}
