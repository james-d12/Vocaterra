using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Needs;

namespace Vocaterra.Game.Core.Input;

/// <summary>Using a food item (N02, design 9.6): selected and right-clicked with the mouse captured, it is eaten or drunk (food cannot be placed, so the click is free).</summary>
public static class FoodItemUse
{
    /// <summary>Whether this frame's right click uses the selected item as food.</summary>
    /// <param name="input">The frame's input.</param>
    /// <param name="captured">Whether the mouse is captured (the world has the input).</param>
    /// <param name="selected">The hotbar's selected stack.</param>
    /// <param name="needs">The foods of <c>needs.json</c>.</param>
    /// <param name="items">The item catalogue (a food is named by its key).</param>
    public static bool Applies(IInputFrame input, bool captured, ItemStack selected, NeedsData needs, IItemCatalogue items)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(items);
        return captured && input.RightClicked && !selected.IsEmpty && needs.FoodOf(items.Get(selected.Item).Name) is not null;
    }
}
