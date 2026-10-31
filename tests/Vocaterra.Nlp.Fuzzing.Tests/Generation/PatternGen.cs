using CsCheck;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Fuzzing.Tests.Generation;

/// <summary>A grammar pattern together with where it came from.</summary>
/// <param name="Source">The pattern source: <c>catalog</c> (the bundle <c>IntentCatalog</c>), <c>skeleton</c> or <c>random</c>.</param>
/// <param name="Pattern">The pattern tree.</param>
public sealed record SourcedPattern(string Source, GrammarPattern Pattern);

/// <summary>
/// Slot grammar patterns: every grammar of the bundle's intent catalog, a handful of structural skeletons that guarantee the shapes the
/// mutation oracle needs (subject plus verb, copula plus adjective, article plus noun plus adjective, numeral plus noun), and random DSL
/// strings built from the token kinds <c>[ART] [DET] [PRON] [NUM:x] [NOUN:x] [ADJ:x] [VERB] [VERB:lemma] [VERB:lemma:slot] [WORD:x]</c>, <c>?</c> and
/// <c>(a|b)</c>.
/// </summary>
public static class PatternGen
{
    /// <summary>Structural skeletons; each is a valid DSL string and each one makes at least one mutation kind applicable.</summary>
    public static IReadOnlyList<string> Skeletons { get; } =
    [
        "[PRON] [VERB] [NUM:qty] [NOUN:item]",
        "[PRON]? [VERB] [ART]? [NUM:qty]? [NOUN:item] [ADJ]?",
        "[PRON] [VERB:ser] [ADJ:quality]",
        "[PRON] [VERB:estar] [ADJ:quality]",
        "[PRON] [ADV]? [VERB:estar] [ADJ]",
        "[ART] [NOUN:thing] [ADJ]",
        "[ART] [ADJ] [NOUN:thing]",
        "[ART] [NUM:qty] [NOUN:item] [ADJ:quality]",
        "[PRON] [ADV]? [VERB] [DET]? [NOUN:item] [ADJ]?",
        "[VERB] [ART] [NOUN:first] [PREP] [ART] [NOUN:second]",
        "[DET] [NOUN:item] [VERB] [ADJ]",
        "[NUM:first] [NOUN:one] [CONJ] [NUM:second] [NOUN:two]",
        "[PRON] [VERB:querer] [VERB] [ART] [NOUN:item]",
        "[INTERJ]? [PRON]? [VERB] [NOUN:item]",
        "[PRON] [VERB:tener] [NUM:qty] [NOUN:item] [ADJ]?",
        "[PRON] [VERB:ir] [PREP] [ART] [NOUN:place]",
    ];

    private static readonly string[] SlotNames = ["item", "qty", "spell", "thing", "who", "what", "extra", "more", "other"];

    /// <summary>Draws a pattern: catalog grammars 40%, skeletons 25%, random strings 35%.</summary>
    public static Gen<SourcedPattern> Any(IGeneratorInputs harness)
    {
        return Gen.Frequency(
            (40, (IGen<SourcedPattern>)Catalog(harness)),
            (25, (IGen<SourcedPattern>)Skeleton()),
            (35, (IGen<SourcedPattern>)Random(harness)));
    }

    /// <summary>Draws one grammar of the bundle's intent catalog.</summary>
    public static Gen<SourcedPattern> Catalog(IGeneratorInputs harness)
    {
        return Gen.OneOfConst([.. harness.Grammars.Grammars.Select(static g => new SourcedPattern("catalog", g.Pattern))]);
    }

    /// <summary>Draws one of the structural skeletons.</summary>
    public static Gen<SourcedPattern> Skeleton()
    {
        return Gen.OneOfConst([.. Skeletons.Select(static s => new SourcedPattern("skeleton", GrammarPattern.Parse(s)))]);
    }

    /// <summary>Draws a random DSL pattern; the seed fully determines it.</summary>
    public static Gen<SourcedPattern> Random(IGeneratorInputs harness)
    {
        return Gen.ULong.Select(seed => new SourcedPattern("random", Build(harness.Words, new SeededRng(seed))));
    }

    /// <summary>Builds a random pattern of one to five elements with unique slot names and at least one mandatory element.</summary>
    public static GrammarPattern Build(LexiconCatalog words, SeededRng rng)
    {
        var count = 1 + rng.NextInt(5);
        var slots = 0;
        var nodes = new List<PatternNode>(count);
        for (var i = 0; i < count; i++)
        {
            nodes.Add(Element(words, rng, ref slots, allowGroup: true));
        }

        if (nodes.All(static n => n is OptionalPattern))
        {
            nodes[0] = ((OptionalPattern)nodes[0]).Inner;
        }

        return new GrammarPattern(nodes);
    }

    private static PatternNode Element(LexiconCatalog words, SeededRng rng, ref int slots, bool allowGroup)
    {
        PatternNode node;
        var roll = rng.NextDouble();
        if (roll < 0.14)
        {
            node = new TypedPattern("ART", null, null);
        }
        else if (roll < 0.19)
        {
            node = new TypedPattern("DET", null, null);
        }
        else if (roll < 0.30)
        {
            node = new TypedPattern("PRON", null, null);
        }
        else if (roll < 0.40)
        {
            node = new TypedPattern("NUM", null, NextSlot(rng, ref slots, 0.8));
        }
        else if (roll < 0.62)
        {
            node = new TypedPattern("NOUN", null, NextSlot(rng, ref slots, 0.8));
        }
        else if (roll < 0.72)
        {
            node = new TypedPattern("ADJ", null, NextSlot(rng, ref slots, 0.4));
        }
        else if (roll < 0.79)
        {
            node = new TypedPattern("VERB", null, null);
        }
        else if (roll < 0.89)
        {
            node = new TypedPattern("VERB", rng.Pick(words.Verbs).Lemma, null);
        }
        else if (roll < 0.93)
        {
            node = new TypedPattern("VERB", rng.Pick(words.Verbs).Lemma, NextSlot(rng, ref slots, 1.0));
        }
        else if (roll < 0.96 && allowGroup)
        {
            var alternatives = new List<IReadOnlyList<PatternNode>>();
            var branches = 2 + rng.NextInt(2);
            for (var i = 0; i < branches; i++)
            {
                var length = 1 + rng.NextInt(2);
                var branch = new List<PatternNode>(length);
                for (var j = 0; j < length; j++)
                {
                    var element = Element(words, rng, ref slots, allowGroup: false);
                    branch.Add(element is OptionalPattern optional ? optional.Inner : element);
                }

                alternatives.Add(branch);
            }

            node = new GroupPattern(alternatives);
        }
        else if (roll < 0.98)
        {
            node = new TypedPattern(rng.Pick(new[] { "ADV", "PREP", "CONJ", "INTERJ" }), null, null);
        }
        else if (roll < 0.99)
        {
            node = new WordPattern(NextSlot(rng, ref slots, 0.8));
        }
        else
        {
            node = new LiteralPattern(rng.NextDouble() < 0.7
                ? rng.Pick(words.Prepositions.Concat(words.Adverbs).Concat(words.Conjunctions).Where(IsLiteralSafe).ToArray())
                : rng.Pick(words.Nouns.Select(static n => n.Singular).Where(IsLiteralSafe).ToArray()));
        }

        return rng.NextDouble() < 0.25 ? new OptionalPattern(node) : node;
    }

    private static string? NextSlot(SeededRng rng, ref int slots, double probability)
    {
        if (rng.NextDouble() >= probability)
        {
            return null;
        }

        var name = slots < SlotNames.Length ? SlotNames[slots] : $"slot{slots}";
        slots++;
        return name;
    }

    private static bool IsLiteralSafe(string word)
    {
        return word.Length > 0 && word.All(static c => char.IsLetter(c));
    }
}
