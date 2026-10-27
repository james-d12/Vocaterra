using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Content;

/// <summary>
/// Builds a sample slot map for a dialogue template so the loader can prove it renders. The words are the language's own (<see cref="SampleWords"/>, the
/// pack's <see cref="ILanguagePack.Samples"/>); for Spanish <c>qty</c>, <c>count</c>, <c>number</c>, <c>amount</c> and <c>price</c> are the number 2, <c>verb</c>
/// and <c>action</c> are the verb tener, <c>spell</c> is fuego, the reserved feedback slots <c>tag</c>, <c>expected</c>, <c>actual</c> and <c>recast</c> are short
/// sample texts, and any other slot is the noun espada. A slot the template reads through <c>words</c>, <c>plural-if</c> or <c>conj</c>
/// (<see cref="TemplateEngine.SlotKinds"/>) is a number or a verb whatever its name, so a template that reads a host slot such as
/// <c>{distance:words(m)}</c> validates. A toy language (M35) gets its own words, so a validator never renders a template with a Spanish word.
/// </summary>
internal static class SampleSlots
{
    /// <summary>Returns one sample value for every slot the template reads.</summary>
    /// <param name="template">The dialogue template.</param>
    /// <param name="samples">The language's sample words.</param>
    public static SlotMap For(DialogueTemplate template, SampleWords samples)
    {
        var kinds = TemplateEngine.SlotKinds(template);
        return SlotMap.From(TemplateEngine.ReferencedSlots(template)
            .Select(name => new KeyValuePair<string, string>(name, ValueFor(name, kinds, samples))));
    }

    private static string ValueFor(string name, IReadOnlyDictionary<string, TemplateSlotKind> kinds, SampleWords samples)
    {
        if (kinds.TryGetValue(name, out var kind) && kind != TemplateSlotKind.Text && !IsReserved(name))
        {
            return kind == TemplateSlotKind.Number ? "2" : samples.Verb;
        }

        return name switch
        {
            "tag" => samples.Tag,
            "expected" => samples.Expected,
            "actual" => samples.Actual,
            "recast" => samples.Recast,
            "qty" or "count" or "number" or "amount" or "price" => "2",
            "verb" or "action" => samples.Verb,
            "spell" => samples.Spell,
            _ => samples.Noun,
        };
    }

    private static bool IsReserved(string name)
    {
        return name is "tag" or "expected" or "actual" or "recast";
    }
}
