using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Words;

namespace Vocaterra.Game.Scripted;

/// <summary>How a scripted run names an item: as a noun of the catalogue (<c>madera</c>) or as the item's own key (<c>wood</c>).</summary>
public static class ScriptItemNames
{
    /// <summary>The item <paramref name="name"/> names, or <see langword="null"/>.</summary>
    public static ItemId? Resolve(NounCatalog nouns, string name)
    {
        var noun = nouns.ForLemma(name);
        if (noun is not null && noun.Items.Count > 0)
        {
            return noun.Items[0];
        }

        foreach (var definition in ItemDefinition.All)
        {
            if (string.Equals(definition.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return definition.Id;
            }
        }

        return null;
    }
}
