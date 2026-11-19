using Vocaterra.Game.Core.Language.Words;

namespace Vocaterra.Game.Core.Items;

/// <summary>How a scripted run names an item: as a noun of the catalogue (<c>madera</c>) or as the item's own key (<c>wood</c>).</summary>
public static class ScriptItemNames
{
    /// <summary>The item <paramref name="name"/> names, or <see langword="null"/>.</summary>
    /// <param name="nouns">The noun catalogue.</param>
    /// <param name="items">The item catalogue (its keys, any case).</param>
    /// <param name="name">A noun's lemma or an item's key.</param>
    public static ItemId? Resolve(NounCatalog nouns, IItemCatalogue items, string name)
    {
        var noun = nouns.ForLemma(name);
        if (noun is not null && noun.Items.Count > 0)
        {
            return noun.Items[0];
        }

        foreach (var definition in items.All)
        {
            if (string.Equals(definition.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return definition.Id;
            }
        }

        return null;
    }
}
