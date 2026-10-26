using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Content;

/// <summary>
/// Builds a sample slot map for a dialogue template so the loader can prove it renders. Values follow the slot name:
/// <c>qty</c>, <c>count</c>, <c>number</c>, <c>amount</c> and <c>price</c> are the number 2; <c>verb</c> and <c>action</c> are the verb tener;
/// <c>spell</c> is fuego; the reserved feedback slots <c>tag</c>, <c>expected</c>, <c>actual</c> and <c>recast</c> are short sample texts;
/// any other slot is the noun espada. A slot the template reads through <c>words</c>, <c>plural-if</c> or <c>conj</c>
/// (<see cref="TemplateEngine.SlotKinds"/>) is a number or a verb whatever its name, so a template that reads a host slot such as
/// <c>{distance:words(m)}</c> validates.
/// </summary>
internal static class SampleSlots
{
    /// <summary>Returns one sample value for every slot the template reads.</summary>
    public static SlotMap For(DialogueTemplate template)
    {
        var kinds = TemplateEngine.SlotKinds(template);
        return SlotMap.From(TemplateEngine.ReferencedSlots(template)
            .Select(name => new KeyValuePair<string, string>(name, ValueFor(name, kinds))));
    }

    private static string ValueFor(string name, IReadOnlyDictionary<string, TemplateSlotKind> kinds)
    {
        if (kinds.TryGetValue(name, out var kind) && kind != TemplateSlotKind.Text && !IsReserved(name))
        {
            return kind == TemplateSlotKind.Number ? "2" : "tener";
        }

        return name switch
        {
            "tag" => "gender.agree",
            "expected" => "tengo",
            "actual" => "tiene",
            "recast" => "yo tengo dos espadas",
            "qty" or "count" or "number" or "amount" or "price" => "2",
            "verb" or "action" => "tener",
            "spell" => "fuego",
            _ => "espada",
        };
    }

    private static bool IsReserved(string name)
    {
        return name is "tag" or "expected" or "actual" or "recast";
    }
}
