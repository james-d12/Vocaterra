using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Morphology;

namespace Vocaterra.Nlp.Es.Text;

/// <summary>
/// The Spanish template functions (M34): the inflection behind <c>article()</c>, <c>words()</c>, <c>conj()</c> and <c>plural-if()</c>, moved out of the
/// template engine with the same behaviour. Created by <see cref="SpanishLanguagePack.CreateTemplateFunctions"/>.
/// </summary>
internal sealed class SpanishTemplateFunctions : ITemplateFunctions
{
    private static readonly string[] FeminineEndings = ["a", "ción", "sión", "dad", "tad", "tud", "umbre"];

    private readonly Lexicon _lexicon;
    private readonly IMorphology _morphology;

    public SpanishTemplateFunctions(Lexicon lexicon, IMorphology morphology)
    {
        ArgumentNullException.ThrowIfNull(lexicon);
        ArgumentNullException.ThrowIfNull(morphology);
        _lexicon = lexicon;
        _morphology = morphology;
    }

    public string ResolveVerbLemma(string value)
    {
        if (_lexicon.ContainsLemma(value))
        {
            return value;
        }

        foreach (var reading in _lexicon.Lookup(value))
        {
            if (reading.Pos == LexiconSchema.Verb)
            {
                return reading.Lemma;
            }
        }

        return value;
    }

    public string? Conjugate(string lemma, string person, string number, string tense) => _morphology.Conjugate(lemma, person, number, tense);

    public string InflectNoun(string word, string number) => _morphology.InflectNoun(word, number);

    public string InferNumber(string word)
    {
        foreach (var reading in _lexicon.Lookup(word))
        {
            if (reading.Pos == LexiconSchema.Noun && reading.Number is { } number)
            {
                return number;
            }
        }

        return LexiconSchema.Singular;
    }

    public string GenderOf(string singular)
    {
        foreach (var reading in _lexicon.Lookup(singular))
        {
            if (reading.Pos == LexiconSchema.Noun && reading.Gender is { } gender)
            {
                return gender;
            }
        }

        foreach (var entry in _lexicon.EntriesForLemma(singular))
        {
            if (entry.Pos == LexiconSchema.Noun && entry.Features.Gender is { } gender)
            {
                return gender;
            }
        }

        return HeuristicGender(singular);
    }

    public string Article(Definiteness definiteness, string gender, string number, string singular) => _morphology.Article(definiteness, gender, number, singular);

    private static string HeuristicGender(string noun)
    {
        foreach (var ending in FeminineEndings)
        {
            if (noun.EndsWith(ending, StringComparison.Ordinal))
            {
                return LexiconSchema.Feminine;
            }
        }

        return LexiconSchema.Masculine;
    }
}
