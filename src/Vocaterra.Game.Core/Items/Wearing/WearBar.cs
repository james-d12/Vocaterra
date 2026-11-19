namespace Vocaterra.Game.Core.Items.Wearing;

/// <summary>
/// The wear bar under an item's icon (I07): how much of it is left and what colour it is, from green (barely worn) through yellow to red (about to wear out). A pure function of the stack and its definition, so the layer that
/// draws it holds no rule and a test can check it without a window. An item with no durability, or no wear, has no bar.
/// </summary>
/// <param name="Remaining">The share of the item that is left, 0 (worn out) up to just under 1.</param>
/// <param name="Red">The colour's red, 0 to 255.</param>
/// <param name="Green">The colour's green, 0 to 255.</param>
/// <param name="Blue">The colour's blue, 0 to 255.</param>
public readonly record struct WearBar(double Remaining, int Red, int Green, int Blue)
{
    /// <summary>The bar of a stack, or <see langword="null"/> when none is drawn (nothing in the slot, no durability, or no wear yet).</summary>
    /// <param name="stack">The stack in the slot.</param>
    /// <param name="definition">Its item's definition, or <see langword="null"/> when unknown.</param>
    public static WearBar? Of(ItemStack stack, ItemDefinition? definition)
    {
        if (stack.IsEmpty || stack.Wear <= 0 || definition?.Durability is not { } durability)
        {
            return null;
        }

        var remaining = Math.Clamp(1.0 - (stack.Wear / (double)durability.Max), 0.0, 1.0);
        // Green at full, yellow at half, red at nothing: red rises over the first half, green falls over the second.
        var red = remaining >= 0.5 ? (int)Math.Round((1.0 - remaining) * 2 * 255) : 255;
        var green = remaining >= 0.5 ? 255 : (int)Math.Round(remaining * 2 * 255);
        return new WearBar(remaining, red, green, 40);
    }
}
