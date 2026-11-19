using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Language.Words;

namespace Vocaterra.Game.Core.Items;

/// <summary>What the inventory panel says about the cell under the pointer (I04): the Spanish words and, until the learner knows the noun, its English. Pure data for the layer to draw.</summary>
/// <param name="Slot">The slot's label noun with its article (<c>la cabeza</c>) for an equipment cell, or <see langword="null"/> when the slot has no label or the cell is a bag cell.</param>
/// <param name="Item">The item's Spanish name as the cells write it (<c>sombrero</c>), or <see langword="null"/> for an empty cell.</param>
/// <param name="English">The English for the item (or, on an empty equipment cell, for the label) while the learner does not know the word, otherwise <see langword="null"/>.</param>
/// <param name="Saturation">How well the item's word is learnt, 0 to 1, for the colour of the text (1 for a cell with no item).</param>
public sealed record InventoryHover(string? Slot, string? Item, string? English, double Saturation)
{
    /// <summary>Whether there is nothing to say.</summary>
    public bool IsEmpty => Slot is null && Item is null;

    /// <summary>The words for a hit, or <see langword="null"/> when the hit is on no cell, or on an empty cell that has no words.</summary>
    /// <param name="hit">What the pointer is on.</param>
    /// <param name="bag">The inventory's slots.</param>
    /// <param name="equipment">The worn items and their data.</param>
    /// <param name="nouns">The noun catalogue (the labels, the articles and the items' names).</param>
    /// <param name="words">The learner's word progress.</param>
    /// <param name="support">The English for a lemma, or <see langword="null"/> when none is given (<c>LocaleText.NounSupport</c>).</param>
    public static InventoryHover? Of(InventoryHit hit, Inventory bag, Equipment equipment, NounCatalog nouns, WordProgress words, Func<string, string?> support)
    {
        ArgumentNullException.ThrowIfNull(bag);
        ArgumentNullException.ThrowIfNull(equipment);
        ArgumentNullException.ThrowIfNull(nouns);
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(support);
        string? slotText = null;
        string? slotLemma = null;
        ItemStack stack;
        switch (hit.Kind)
        {
            case InventoryHitKind.Cell:
                if ((uint)hit.Slot >= (uint)bag.SlotCount)
                {
                    return null;
                }

                stack = bag.Slots[hit.Slot];
                break;
            case InventoryHitKind.Equipment:
                if ((uint)hit.Slot >= (uint)equipment.Data.SlotCount)
                {
                    return null;
                }

                var spec = equipment.Data.Spec(equipment.Data.SlotAt(hit.Slot));
                stack = equipment.Slots[hit.Slot];
                if (spec.Label is { } label)
                {
                    slotLemma = label;
                    slotText = nouns.ForLemma(label)?.WithDefinite ?? label;
                }

                break;
            case InventoryHitKind.Heading:
                var headings = equipment.Data.Shape.ColumnHeadings;
                if ((uint)hit.Slot >= (uint)headings.Count || headings[hit.Slot] is not { } heading)
                {
                    return null;
                }

                return new InventoryHover(nouns.ForLemma(heading)?.WithDefinite ?? heading, null, EnglishUnlessKnown(heading, words, support), 1.0);
            default:
                return null;
        }

        if (stack.IsEmpty)
        {
            return slotText is null ? null : new InventoryHover(slotText, null, EnglishUnlessKnown(slotLemma, words, support), 1.0);
        }

        var itemLemma = nouns.ForItem(stack.Item)?.Lemma;
        var name = nouns.LabelOf(stack.Item) ?? equipment.Items.Get(stack.Item).Name;
        return new InventoryHover(slotText, name, EnglishUnlessKnown(itemLemma, words, support), itemLemma is null ? 1.0 : words.SaturationOf(itemLemma));
    }

    private static string? EnglishUnlessKnown(string? lemma, WordProgress words, Func<string, string?> support) =>
        lemma is not null && !words.RecordOf(lemma).Known ? support(lemma) : null;
}
