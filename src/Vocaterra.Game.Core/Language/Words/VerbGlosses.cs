using Vocaterra.Game.Core.Content;

namespace Vocaterra.Game.Core.Language.Words;

/// <summary>One verb's support-language forms (S20): the infinitive's gloss and every form the book's labels use, listed in the locale, none built by a rule.</summary>
/// <param name="Lemma">The infinitive (<c>querer</c>).</param>
/// <param name="English">The infinitive's gloss (<c>to want</c>).</param>
/// <param name="Base">The base form (<c>want</c>; <c>be</c> for the verbs that conjugate as to be).</param>
/// <param name="Third">The third person singular of the present (<c>wants</c>).</param>
/// <param name="Past">The past (<c>wanted</c>).</param>
public sealed record VerbGloss(string Lemma, string English, string Base, string Third, string Past);

/// <summary>
/// The verbs' support-language forms (S20, D-game-13: the support language is on-screen text), read from the locale's <c>verb.&lt;lemma&gt;</c> entries
/// (content-plan C03; until then <c>game/glosses.json</c>). The lexicon carries no gloss, so the locale gives one per verb the learner can meet; a verb not in
/// it is shown by its infinitive alone.
/// </summary>
public sealed class VerbGlosses
{
    private readonly Dictionary<string, VerbGloss> _verbs;

    private VerbGlosses(IEnumerable<VerbGloss> verbs)
    {
        _verbs = new Dictionary<string, VerbGloss>(StringComparer.Ordinal);
        foreach (var verb in verbs)
        {
            _verbs[verb.Lemma] = verb;
        }
    }

    /// <summary>No glosses at all.</summary>
    public static VerbGlosses Empty { get; } = new([]);

    /// <summary>Every gloss, by infinitive (ordinal).</summary>
    public IReadOnlyList<VerbGloss> All => _verbs.Values.OrderBy(static v => v.Lemma, StringComparer.Ordinal).ToList();

    /// <summary>The verbs the locale lists (with its fallback's for a verb it lacks).</summary>
    /// <param name="locale">The support language.</param>
    public static VerbGlosses From(LocaleText locale)
    {
        ArgumentNullException.ThrowIfNull(locale);
        var lemmas = new SortedSet<string>(StringComparer.Ordinal);
        for (var text = locale; text is not null; text = text.Fallback)
        {
            lemmas.UnionWith(text.Verbs);
        }

        return new VerbGlosses(lemmas.Select(lemma => locale.Verb(lemma) is { } forms ? new VerbGloss(lemma, forms.Gloss, forms.Base, forms.Third, forms.Past) : null).OfType<VerbGloss>());
    }

    /// <summary>The gloss of a verb, or <see langword="null"/> when the locale has none.</summary>
    /// <param name="lemma">The infinitive.</param>
    public VerbGloss? ForLemma(string lemma) => _verbs.TryGetValue(lemma, out var verb) ? verb : null;
}
