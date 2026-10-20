using Slg.Core.Verdicts;
using Slg.Response.Templates;

namespace Slg.Content;

/// <summary>
/// Builds a sample slot map for a dialogue template so the loader can prove it renders. Values follow the slot name:
/// <c>qty</c>, <c>count</c>, <c>number</c>, <c>amount</c> and <c>price</c> are the number 2; <c>verb</c> and <c>action</c> are the verb tener;
/// <c>spell</c> is fuego; the reserved feedback slots <c>tag</c>, <c>expected</c>, <c>actual</c> and <c>recast</c> are short sample texts;
/// any other slot is the noun espada.
/// </summary>
internal static class SampleSlots
{
    /// <summary>Returns one sample value for every slot the template reads.</summary>
    public static SlotMap For(DialogueTemplate template)
    {
        return SlotMap.From(TemplateEngine.ReferencedSlots(template)
            .Select(static name => new KeyValuePair<string, string>(name, ValueFor(name))));
    }

    private static string ValueFor(string name)
    {
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
}
