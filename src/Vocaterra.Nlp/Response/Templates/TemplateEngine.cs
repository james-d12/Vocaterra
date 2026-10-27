using System.Globalization;
using System.Text;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Lexicon;
using Vocaterra.Nlp.Language.Morphology;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.Packs;

namespace Vocaterra.Nlp.Response.Templates;

/// <summary>
/// Renders <see cref="DialogueTemplate"/> text. Placeholders are <c>{slot}</c> or <c>{slot:filter:filter}</c>; a literal brace is written
/// <c>{{</c> or <c>}}</c>. Filters: <c>plural-if(qty)</c> (noun in the number of slot <c>qty</c>), <c>article(def|indef)</c> (article and noun, agreeing),
/// <c>conj(person,number,tense)</c> (the slot holds a verb lemma), <c>words</c>, <c>words(m|f|slot)</c> (digits as Spanish words, optionally
/// with <c>un/una</c> agreement) and <c>cap</c> (capitalize the first letter). Output is deterministic and ends with <c>¿ ¡</c> balancing.
/// </summary>
public sealed class TemplateEngine
{
    private const string Plural = "pl";
    private const string Singular = "sg";

    private readonly Lexicon _lexicon;
    private readonly ITemplateFunctions _functions;
    private readonly ILanguagePack _language;

    /// <summary>
    /// Creates an engine that inflects with the pack's template functions over the morphology and the lexicon, and reads numbers, punctuation and number
    /// agreement through the language pack (the bundle's <c>Language</c>).
    /// </summary>
    public TemplateEngine(Lexicon lexicon, IMorphology morphology, ILanguagePack language)
        : this(lexicon, ValidMorphology(morphology), language, ValidPack(language).CreateTemplateFunctions(lexicon, morphology))
    {
    }

    /// <summary>Creates an engine with explicit template functions (M34, additive).</summary>
    /// <param name="lexicon">The lexicon.</param>
    /// <param name="morphology">The morphology the functions were created over.</param>
    /// <param name="language">The language pack.</param>
    /// <param name="functions">The functions behind the filters.</param>
    public TemplateEngine(Lexicon lexicon, IMorphology morphology, ILanguagePack language, ITemplateFunctions functions)
    {
        ArgumentNullException.ThrowIfNull(lexicon);
        ArgumentNullException.ThrowIfNull(morphology);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(functions);
        _lexicon = lexicon;
        _language = language;
        _functions = functions;
    }

    private static IMorphology ValidMorphology(IMorphology morphology)
    {
        ArgumentNullException.ThrowIfNull(morphology);
        return morphology;
    }

    private static ILanguagePack ValidPack(ILanguagePack language)
    {
        ArgumentNullException.ThrowIfNull(language);
        return language;
    }

    /// <summary>
    /// Creates an engine over the embedded seed lexicon, in Spanish (<see cref="SpanishLanguagePack.Instance"/>): the Spanish compatibility entry point
    /// for the seed. A bundle's engine is <c>bundle.TemplateEngine</c>.
    /// </summary>
    public static TemplateEngine CreateDefault()
    {
        return new TemplateEngine(Lexicon.Seed, SpanishLanguagePack.Instance.CreateMorphology(Lexicon.Seed), SpanishLanguagePack.Instance);
    }

    /// <summary>Renders the template with the slots; throws <see cref="TemplateException"/> for a malformed template or an unknown slot.</summary>
    public string Render(DialogueTemplate template, SlotMap slots)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(slots);
        var builder = new StringBuilder();
        foreach (var part in TemplateParser.Parse(template.Text))
        {
            switch (part)
            {
                case LiteralPart literal:
                    builder.Append(literal.Text);
                    break;
                case PlaceholderPart placeholder:
                    builder.Append(RenderPlaceholder(placeholder, slots));
                    break;
                default:
                    throw new InvalidOperationException("Unknown template part.");
            }
        }

        return _language.Punctuation.Fix(builder.ToString().Normalize(NormalizationForm.FormC));
    }

    /// <summary>Returns the distinct slot names a template reads (including slots named in filter arguments), in ordinal order.</summary>
    public static IReadOnlyList<string> ReferencedSlots(DialogueTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var part in TemplateParser.Parse(template.Text))
        {
            if (part is not PlaceholderPart placeholder)
            {
                continue;
            }

            names.Add(placeholder.SlotName);
            foreach (var filter in placeholder.Filters)
            {
                if (filter.Name == "plural-if" && filter.Args.Count == 1)
                {
                    names.Add(filter.Args[0]);
                }
                else if (filter.Name == "words" && filter.Args.Count == 1 && filter.Args[0] is not ("m" or "f"))
                {
                    names.Add(filter.Args[0]);
                }
            }
        }

        return [.. names];
    }

    /// <summary>
    /// The slots a template reads as numbers (the slot of <c>words</c> and <c>words(m|f)</c>, and the quantity slot named by <c>plural-if(qty)</c> or
    /// <c>words(qty)</c>) and as verb lemmas (the slot of <c>conj</c>), in ordinal order. Everything else the template reads is plain text or a noun.
    /// Content validation uses it to pick sample values, so a template that reads a host slot such as <c>{distance:words(m)}</c> can be proven to
    /// render without the validator knowing the host's slot names.
    /// </summary>
    public static IReadOnlyDictionary<string, TemplateSlotKind> SlotKinds(DialogueTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var kinds = new SortedDictionary<string, TemplateSlotKind>(StringComparer.Ordinal);
        foreach (var part in TemplateParser.Parse(template.Text))
        {
            if (part is not PlaceholderPart placeholder)
            {
                continue;
            }

            foreach (var filter in placeholder.Filters)
            {
                switch (filter.Name)
                {
                    case "words":
                        kinds[placeholder.SlotName] = TemplateSlotKind.Number;
                        break;
                    case "conj":
                        kinds.TryAdd(placeholder.SlotName, TemplateSlotKind.Verb);
                        break;
                }

                if (filter.Name == "plural-if" && filter.Args.Count == 1)
                {
                    kinds[filter.Args[0]] = TemplateSlotKind.Number;
                }
                else if (filter.Name == "words" && filter.Args.Count == 1 && filter.Args[0] is not ("m" or "f"))
                {
                    // The argument names the noun that decides the gender, not a number.
                    kinds.TryAdd(filter.Args[0], TemplateSlotKind.Text);
                }
            }
        }

        return kinds;
    }

    private static string Capitalize(string text)
    {
        return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static string GetSlot(SlotMap slots, string name, PlaceholderPart placeholder)
    {
        return slots.TryGetValue(name, out var value)
            ? value
            : throw new TemplateException(name, $"Unknown slot '{name}' in placeholder '{placeholder.Source}'.");
    }

    private static bool IsAsciiDigits(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (var c in text)
        {
            if (!char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private static void RejectDuplicates(PlaceholderPart placeholder)
    {
        for (var i = 0; i < placeholder.Filters.Count; i++)
        {
            for (var j = i + 1; j < placeholder.Filters.Count; j++)
            {
                if (string.Equals(placeholder.Filters[i].Name, placeholder.Filters[j].Name, StringComparison.Ordinal))
                {
                    throw new TemplateException(placeholder.Source, $"Filter '{placeholder.Filters[i].Name}' is repeated in placeholder '{placeholder.Source}'.");
                }
            }
        }
    }

    private static void RequireArgs(TemplateFilter filter, PlaceholderPart placeholder, int count)
    {
        if (filter.Args.Count != count)
        {
            throw new TemplateException(
                placeholder.Source,
                $"Filter '{filter.Name}' takes {count} argument(s) but has {filter.Args.Count} in placeholder '{placeholder.Source}'.");
        }
    }

    private string RenderPlaceholder(PlaceholderPart placeholder, SlotMap slots)
    {
        var value = GetSlot(slots, placeholder.SlotName, placeholder);
        RejectDuplicates(placeholder);

        TemplateFilter? plural = null;
        TemplateFilter? article = null;
        TemplateFilter? conj = null;
        TemplateFilter? words = null;
        var capitalize = false;
        foreach (var filter in placeholder.Filters)
        {
            switch (filter.Name)
            {
                case "plural-if":
                    RequireArgs(filter, placeholder, 1);
                    plural = filter;
                    break;
                case "article":
                    RequireArgs(filter, placeholder, 1);
                    article = filter;
                    break;
                case "conj":
                    RequireArgs(filter, placeholder, 3);
                    conj = filter;
                    break;
                case "words":
                    if (filter.Args.Count > 1)
                    {
                        RequireArgs(filter, placeholder, 1);
                    }

                    words = filter;
                    break;
                case "cap":
                    RequireArgs(filter, placeholder, 0);
                    capitalize = true;
                    break;
                default:
                    throw new TemplateException(placeholder.Source, $"Unknown filter '{filter.Name}' in placeholder '{placeholder.Source}'.");
            }
        }

        var nounFilters = plural is not null || article is not null;
        var exclusive = (conj is not null ? 1 : 0) + (words is not null ? 1 : 0) + (nounFilters ? 1 : 0);
        if (exclusive > 1)
        {
            throw new TemplateException(placeholder.Source, $"Filters cannot be combined this way in placeholder '{placeholder.Source}'.");
        }

        string result;
        if (conj is not null)
        {
            result = RenderConjugation(placeholder, conj, value);
        }
        else if (words is not null)
        {
            result = RenderWords(placeholder, words, value, slots);
        }
        else if (nounFilters)
        {
            result = RenderNoun(placeholder, plural, article, value, slots);
        }
        else
        {
            result = value;
        }

        return capitalize ? Capitalize(result) : result;
    }

    private string RenderConjugation(PlaceholderPart placeholder, TemplateFilter conj, string value)
    {
        var person = conj.Args[0];
        var number = conj.Args[1];
        var tense = conj.Args[2];
        if (person is not ("1" or "2" or "3")
            || number is not (Singular or Plural)
            || !_language.LexiconProfile.Tenses.Contains(tense))
        {
            throw new TemplateException(placeholder.Source, $"Invalid conj({person},{number},{tense}) in placeholder '{placeholder.Source}'.");
        }

        var lemma = ResolveVerbLemma(placeholder, value.Trim());
        return _functions.Conjugate(lemma, person, number, tense)
            ?? throw new TemplateException(
                placeholder.Source,
                $"Cannot conjugate '{lemma}' for ({person},{number},{tense}) in placeholder '{placeholder.Source}'.");
    }

    private string ResolveVerbLemma(PlaceholderPart placeholder, string value)
    {
        if (value.Length == 0)
        {
            throw new TemplateException(placeholder.Source, $"Slot '{placeholder.SlotName}' holds no verb lemma in placeholder '{placeholder.Source}'.");
        }

        return _functions.ResolveVerbLemma(value);
    }

    private string RenderWords(PlaceholderPart placeholder, TemplateFilter words, string value, SlotMap slots)
    {
        var text = value.Trim();
        int number;
        if (IsAsciiDigits(text) && text.Length <= 6)
        {
            number = int.Parse(text, NumberStyles.None, CultureInfo.InvariantCulture);
        }
        else if (!_language.Numbers.TryParse(text.ToLowerInvariant(), out number))
        {
            throw new TemplateException(placeholder.Source, $"'{text}' is not a number between 0 and {_language.Numbers.MaxValue} in placeholder '{placeholder.Source}'.");
        }

        var spelled = _language.Numbers.ToWords(number);
        if (words.Args.Count == 0)
        {
            return spelled;
        }

        var argument = words.Args[0];
        var gender = argument is "m" or "f"
            ? argument
            : _functions.GenderOf(_functions.InflectNoun(GetSlot(slots, argument, placeholder).Trim(), Singular));
        return _language.Numbers.Agree(spelled, gender);
    }

    private string RenderNoun(PlaceholderPart placeholder, TemplateFilter? plural, TemplateFilter? article, string value, SlotMap slots)
    {
        var word = value.Trim();
        if (word.Length == 0 || word.Any(char.IsWhiteSpace))
        {
            throw new TemplateException(placeholder.Source, $"Noun filters need a single word but slot '{placeholder.SlotName}' holds '{value}' in placeholder '{placeholder.Source}'.");
        }

        Definiteness? definiteness = null;
        if (article is not null)
        {
            definiteness = article.Args[0] switch
            {
                "def" or "definite" => Definiteness.Definite,
                "indef" or "indefinite" => Definiteness.Indefinite,
                _ => throw new TemplateException(placeholder.Source, $"article() takes def or indef, not '{article.Args[0]}', in placeholder '{placeholder.Source}'."),
            };
        }

        var number = plural is not null
            ? (IsPluralQuantity(placeholder, plural.Args[0], GetSlot(slots, plural.Args[0], placeholder)) ? Plural : Singular)
            : _functions.InferNumber(word);
        var singular = _functions.InflectNoun(word, Singular);
        var form = number == Plural ? _functions.InflectNoun(word, Plural) : singular;
        if (definiteness is null)
        {
            return form;
        }

        var gender = _functions.GenderOf(singular);
        return _functions.Article(definiteness.Value, gender, number, singular) + " " + form;
    }

    private bool IsPluralQuantity(PlaceholderPart placeholder, string slotName, string quantity)
    {
        var text = quantity.Trim().ToLowerInvariant();
        if (IsAsciiDigits(text))
        {
            return !string.Equals(text.TrimStart('0'), "1", StringComparison.Ordinal);
        }

        if (_language.Numbers.TryParse(text, out var parsed))
        {
            return parsed != 1;
        }

        var sawSingular = false;
        foreach (var reading in _lexicon.Lookup(text))
        {
            if (reading.Number == Plural)
            {
                return true;
            }

            sawSingular |= reading.Number == Singular;
        }

        return sawSingular
            ? false
            : throw new TemplateException(placeholder.Source, $"Slot '{slotName}' holds '{quantity}', which is not a quantity, in placeholder '{placeholder.Source}'.");
    }
}
