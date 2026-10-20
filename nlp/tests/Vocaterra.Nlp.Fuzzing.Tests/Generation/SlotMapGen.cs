using System.Globalization;
using System.Text.RegularExpressions;
using CsCheck;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Nlp.Normalization;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Fuzzing.Tests.Generation;

/// <summary>What a template does with a slot, which decides what kind of value the slot can hold; the later members are the stricter ones.</summary>
public enum SlotUse
{
    /// <summary>Inserted as it is (or capitalized): any text.</summary>
    Text,

    /// <summary>The target of <c>article(...)</c> or <c>plural-if(...)</c>, or the noun a <c>words(slot)</c> agrees with: a single noun.</summary>
    Noun,

    /// <summary>The target of <c>conj(...)</c>: a verb lemma that conjugates in the requested cell.</summary>
    VerbLemma,

    /// <summary>The target of <c>words</c> or the argument of <c>plural-if(slot)</c>: a number from 0 to 999 999 as digits or words.</summary>
    Quantity,
}

/// <summary>
/// Slot maps for dialogue templates, derived from the templates themselves: the placeholders and filters of each template say which slots
/// it reads and what each must hold (a noun, a quantity, a verb lemma, free text), so new content widens the coverage with no test edits.
/// </summary>
public static partial class SlotMapGen
{
    private static readonly Regex Placeholder = PlaceholderPattern();

    /// <summary>Returns every slot a template reads and the strictest use it puts the slot to.</summary>
    public static IReadOnlyDictionary<string, SlotUse> UsesOf(DialogueTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var uses = new SortedDictionary<string, SlotUse>(StringComparer.Ordinal);
        var text = template.Text.Replace("{{", string.Empty, StringComparison.Ordinal).Replace("}}", string.Empty, StringComparison.Ordinal);
        foreach (Match match in Placeholder.Matches(text))
        {
            var segments = SplitTopLevel(match.Groups[1].Value);
            var name = segments[0].Trim();
            Upgrade(uses, name, SlotUse.Text);
            foreach (var segment in segments.Skip(1))
            {
                var open = segment.IndexOf('(', StringComparison.Ordinal);
                var filter = (open < 0 ? segment : segment[..open]).Trim();
                var args = open < 0 ? [] : segment[(open + 1)..segment.LastIndexOf(')')].Split(',').Select(static a => a.Trim()).ToArray();
                switch (filter)
                {
                    case "plural-if":
                        Upgrade(uses, name, SlotUse.Noun);
                        Upgrade(uses, args[0], SlotUse.Quantity);
                        break;
                    case "article":
                        Upgrade(uses, name, SlotUse.Noun);
                        break;
                    case "conj":
                        Upgrade(uses, name, SlotUse.VerbLemma);
                        break;
                    case "words":
                        Upgrade(uses, name, SlotUse.Quantity);
                        if (args.Length == 1 && args[0] is not ("m" or "f"))
                        {
                            Upgrade(uses, args[0], SlotUse.Noun);
                        }

                        break;
                    default:
                        break;
                }
            }
        }

        return uses;
    }

    /// <summary>
    /// Builds a slot map that holds every slot the template reads, each with a value of the kind its use needs. With <paramref name="messy"/>
    /// the free-text slots carry casing, accent, punctuation and spacing noise (never braces), and unreferenced extra slots may be present.
    /// </summary>
    public static SlotMap For(DialogueTemplate template, LexiconCatalog words, SeededRng rng, bool messy = false)
    {
        var map = SlotMap.Empty;
        foreach (var (name, use) in UsesOf(template))
        {
            map = map.With(name, Value(use, words, rng, messy));
        }

        if (messy)
        {
            for (var i = rng.NextInt(3); i > 0; i--)
            {
                map = map.With($"extra{i}", rng.Pick(AdversarialCorpus.Strings.Where(static s => s.Length < 200).ToArray()));
            }
        }

        return map;
    }

    /// <summary>Draws a value for a slot of the given use.</summary>
    public static string Value(SlotUse use, LexiconCatalog words, SeededRng rng, bool messy)
    {
        switch (use)
        {
            case SlotUse.Noun:
                var noun = rng.Pick(words.Nouns);
                return rng.NextInt(3) == 0 ? noun.Plural : noun.Singular;
            case SlotUse.VerbLemma:
                return rng.Pick(words.Verbs).Lemma;
            case SlotUse.Quantity:
                return Quantity(rng);
            default:
                var phrase = string.Join(' ', Enumerable.Range(0, 1 + rng.NextInt(4)).Select(_ => rng.Pick(words.AllForms)));
                return messy ? new TextNoise(rng.NextInt(5), rng.NextInt(4), rng.NextInt(2) == 0, rng.NextInt(5), rng.NextInt(4), 0, -1, rng.NextInt(1000)).Apply(phrase) : phrase;
        }
    }

    /// <summary>A quantity the template engine accepts: digits (also with leading zeros or up to six digits) or the Spanish number words.</summary>
    public static string Quantity(SeededRng rng)
    {
        switch (rng.NextInt(10))
        {
            case 0:
            case 1:
            case 2:
                return rng.NextInt(31).ToString(CultureInfo.InvariantCulture);
            case 3:
            case 4:
                return rng.Pick(["1", "01", "uno"]);
            case 5:
                return rng.NextInt(SpanishNumbers.MaxValue + 1).ToString(CultureInfo.InvariantCulture);
            case 6:
                return "00" + rng.NextInt(10).ToString(CultureInfo.InvariantCulture);
            default:
                return SpanishNumbers.ToWords(rng.NextInt(31));
        }
    }

    /// <summary>Draws a template of the bundle's dialogue catalog and a complete slot map for it.</summary>
    public static Gen<(DialogueTemplate Template, SlotMap Slots, bool Messy)> Templates(PipelineHarness harness)
    {
        var templates = harness.Bundle.Dialogue.Pools.Values.SelectMany(static p => p.Variants).ToArray();
        return Gen.Select(
            Gen.OneOfConst(templates),
            Gen.ULong,
            Gen.Frequency((3, (IGen<bool>)Gen.Const(false)), (1, (IGen<bool>)Gen.Const(true))),
            (template, seed, messy) => (template, For(template, harness.Words, new SeededRng(seed), messy), messy));
    }

    private static void Upgrade(SortedDictionary<string, SlotUse> uses, string name, SlotUse use)
    {
        uses[name] = uses.TryGetValue(name, out var current) && current > use ? current : use;
    }

    private static List<string> SplitTopLevel(string body)
    {
        var segments = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < body.Length; i++)
        {
            switch (body[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ':' when depth == 0:
                    segments.Add(body[start..i]);
                    start = i + 1;
                    break;
                default:
                    break;
            }
        }

        segments.Add(body[start..]);
        return segments;
    }

    [GeneratedRegex(@"\{([^{}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();
}
