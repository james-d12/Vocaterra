using System.Collections.Concurrent;
using System.Text;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Fuzzing.Tests.Generation;

/// <summary>The part of speech a generated sentence token was realised as.</summary>
public enum TokenRole
{
    /// <summary>A literal word of the grammar (or the implicit <c>por favor</c>).</summary>
    Literal,

    /// <summary>An article.</summary>
    Article,

    /// <summary>A determiner adjective such as <c>mi</c>, <c>este</c> or <c>otros</c>.</summary>
    Determiner,

    /// <summary>A pronoun, either a subject pronoun or a clitic.</summary>
    Pronoun,

    /// <summary>A numeral (its normalized form is a digit string).</summary>
    Numeral,

    /// <summary>A noun.</summary>
    Noun,

    /// <summary>A descriptive adjective.</summary>
    Adjective,

    /// <summary>A finite verb form or an infinitive.</summary>
    Verb,

    /// <summary>An adverb.</summary>
    Adverb,

    /// <summary>A preposition.</summary>
    Preposition,

    /// <summary>A conjunction.</summary>
    Conjunction,

    /// <summary>An interjection.</summary>
    Interjection,

    /// <summary>A name: a word of letters that the lexicon need not know. It has no part of speech for the checks, so no agreement applies to it (M17b).</summary>
    Name,
}

/// <summary>One word of a generated sentence with the features it was generated with; indices refer to <see cref="GeneratedSentence.Tokens"/>.</summary>
/// <param name="Written">The surface as written in the sentence (a number may be several words).</param>
/// <param name="Norm">The surface after normalization (a number is a digit string); it is one normalized token.</param>
/// <param name="Role">The part of speech it was generated as.</param>
/// <param name="Generated">False for a literal word of the grammar, true for a word the generator chose.</param>
/// <param name="Lemma">The lemma of the intended reading, or the digit string of a numeral.</param>
/// <param name="Person">The grammatical person (<c>1</c>, <c>2</c>, <c>3</c>) when it has one.</param>
/// <param name="Number">The grammatical number (<c>sg</c> or <c>pl</c>) when it has one.</param>
/// <param name="Gender">The grammatical gender (<c>m</c> or <c>f</c>) when it has one.</param>
/// <param name="Tense">The tense of a finite verb.</param>
/// <param name="Head">For a determiner, numeral or adjunct of a noun phrase the index of its noun head; for a noun head its own index; otherwise -1.</param>
/// <param name="Slot">The grammar slot the token fills, or null.</param>
/// <param name="Infinitive">True for an infinitive verb.</param>
/// <param name="SubjectPronoun">True for a pronoun generated as the subject of a verb.</param>
/// <param name="Coordination">For a subject pronoun that ends a subject of members joined by <c>y</c> or <c>e</c> (<c>luz y yo comemos</c>, M64), that subject; otherwise null.</param>
public sealed record SentenceToken(
    string Written,
    string Norm,
    TokenRole Role,
    bool Generated,
    string? Lemma,
    string? Person,
    string? Number,
    string? Gender,
    string? Tense,
    int Head,
    string? Slot,
    bool Infinitive,
    bool SubjectPronoun,
    SubjectCoordination? Coordination = null);

/// <summary>
/// A subject of members joined by <c>y</c> or <c>e</c> that a subject pronoun ends (M64; mirror of <c>SpanishAgreementChecker.CoordinationOf</c>): the person its verb takes,
/// always plural (<c>1</c> when a member is of the first person; else <c>2</c> when one is of the second, whose verb may be of the second or the third person plural; else
/// <c>3</c>), and its gender, known only when every member has one.
/// </summary>
/// <param name="Person">The dominant person of the members.</param>
/// <param name="Gender">The gender of the subject, or null.</param>
public sealed record SubjectCoordination(string Person, string? Gender);

/// <summary>A sentence realised from a grammar, correct by construction, with the structure the mutation generator needs.</summary>
/// <param name="PatternSource">Where the grammar came from: <c>catalog</c>, <c>skeleton</c> or <c>random</c>.</param>
/// <param name="PatternText">The canonical pattern text, which is also the grammar id.</param>
/// <param name="Tokens">The words in order.</param>
/// <param name="ExpectedSlots">The slot values a correct analysis must extract (first occurrence of a name wins).</param>
public sealed record GeneratedSentence(
    string PatternSource,
    string PatternText,
    IReadOnlyList<SentenceToken> Tokens,
    IReadOnlyDictionary<string, string> ExpectedSlots)
{
    /// <summary>The sentence as written, words separated by single spaces.</summary>
    public string Text => string.Join(' ', Tokens.Select(static t => t.Written));

    /// <summary>The normalized text the pipeline sees: the token <c>Norm</c> forms separated by single spaces.</summary>
    public string NormalizedText => string.Join(' ', Tokens.Select(static t => t.Norm));

    /// <summary>The span of tokens <paramref name="first"/> to <paramref name="last"/> (inclusive) in <see cref="NormalizedText"/>.</summary>
    public TextSpan SpanOf(int first, int last)
    {
        var start = 0;
        for (var i = 0; i < first; i++)
        {
            start += Tokens[i].Norm.Length + 1;
        }

        var length = 0;
        for (var i = first; i <= last; i++)
        {
            length += Tokens[i].Norm.Length + (i > first ? 1 : 0);
        }

        return new TextSpan(start, length);
    }

    /// <summary>A compact one-line description for failure messages.</summary>
    public string Describe()
    {
        var builder = new StringBuilder();
        builder.Append("pattern=[").Append(PatternText).Append("] text=\"").Append(Text).Append("\" normalized=\"").Append(NormalizedText).Append('"');
        return builder.ToString();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return Describe();
    }
}

/// <summary>Knobs of the sentence generator; the defaults are what the language oracles use.</summary>
public sealed record SentenceOptions
{
    /// <summary>The default options.</summary>
    public static SentenceOptions Default { get; } = new();

    /// <summary>Chance that an optional grammar element is included.</summary>
    public double OptionalProbability { get; init; } = 0.6;

    /// <summary>Chance that a subject pronoun is added in front of a leading verb (the grammar matcher allows one implicitly).</summary>
    public double SubjectProbability { get; init; } = 0.3;

    /// <summary>Chance that the implicit trailing <c>por favor</c> is added.</summary>
    public double PoliteProbability { get; init; } = 0.1;

    /// <summary>Chance that a sentence is realised in the preterite instead of the present when the lemma allows it.</summary>
    public double PreteriteProbability { get; init; } = 0.2;

    /// <summary>Chance that a numeral of 1 is allowed (a numeral of 1 makes its noun phrase singular).</summary>
    public double OneProbability { get; init; } = 0.15;

    /// <summary>The options that include every optional element and always add a subject pronoun, used to reach structure for mutations.</summary>
    public static SentenceOptions Full { get; } = new() { OptionalProbability = 1.0, SubjectProbability = 1.0, PoliteProbability = 0.0, OneProbability = 0.0 };
}

/// <summary>Thread-safe counters a fuzz test keeps to report how its generators performed (skips by reason, applications by kind).</summary>
public sealed class FuzzDiagnostics
{
    private readonly ConcurrentDictionary<string, int> _counts = new(StringComparer.Ordinal);

    /// <summary>Adds one to the named counter.</summary>
    public void Count(string key)
    {
        _counts.AddOrUpdate(key, 1, static (_, value) => value + 1);
    }

    /// <summary>Adds an amount to the named counter.</summary>
    public void Add(string key, int amount)
    {
        _counts.AddOrUpdate(key, amount, (_, value) => value + amount);
    }

    /// <summary>Returns the value of a counter (0 when it was never counted).</summary>
    public int Get(string key)
    {
        return _counts.TryGetValue(key, out var value) ? value : 0;
    }

    /// <summary>Returns the sum of the counters whose names start with the prefix.</summary>
    public int Sum(string prefix)
    {
        return _counts.Where(pair => pair.Key.StartsWith(prefix, StringComparison.Ordinal)).Sum(static pair => pair.Value);
    }

    /// <summary>
    /// How many recipes the sentence generator gave up on because of a homograph it could not spell correctly (a verb form that is a copula by homograph or right behind
    /// a determiner, a function word that is also a verb form or a noun) after every attempt. Since the analyzer resolves the part of speech of each word (M18) these are
    /// rare, and the oracles assert that they stay so.
    /// </summary>
    public int HomographSkips => Get("skip:verb-form-homograph") + Get("skip:function-word-homograph") + Get("skip:accent-variant-homograph");

    /// <summary>How many recipes the sentence generator was given: the realised sentences and every skip.</summary>
    public int Recipes => Sum("sentence:") + Sum("skip:");

    /// <summary>True when the homograph skips are at most one recipe in two hundred.</summary>
    public bool HomographSkipsAreRare => HomographSkips * 200 <= Recipes;

    /// <summary>
    /// The prefix of the counters of rejected draws (M65): one per attempt of the realiser that failed and was drawn again, and one per realised sentence that
    /// <see cref="SentenceGen.Realize"/> rejected afterwards, by the same reason as the <c>skip:</c> counters but without their detail.
    /// </summary>
    public const string RejectedPrefix = "rejected:";

    /// <summary>How many sentences the realiser drew (M65): every attempt, rejected or kept, so a re-roll that a later attempt made good is still a draw.</summary>
    public int Draws => Sum(RejectedPrefix) + Sum("sentence:");

    /// <summary>Renders every counter, one per line, in ordinal key order.</summary>
    public string Report()
    {
        var builder = new StringBuilder();
        foreach (var pair in _counts.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            builder.Append("  ").Append(pair.Key).Append(" = ").Append(pair.Value).Append('\n');
        }

        return builder.ToString();
    }
}
