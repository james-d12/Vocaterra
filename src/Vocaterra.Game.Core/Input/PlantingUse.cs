using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Input;

/// <summary>Planting a berry at the camp (N08): a right click with the planted item selected and the crosshair on grass or dirt plants it; in the air the click still eats (<see cref="FoodItemUse"/>).</summary>
public static class PlantingUse
{
    /// <summary>Whether this frame's right click plants the selected item on the ground under the crosshair.</summary>
    /// <param name="input">The frame's input.</param>
    /// <param name="captured">Whether the mouse is captured (the world has the input).</param>
    /// <param name="selected">The hotbar's selected stack.</param>
    /// <param name="target">The block under the crosshair within reach, or <see langword="null"/>.</param>
    /// <param name="needs">The needs, with the planting rule (optional in the file).</param>
    /// <param name="items">The item catalogue (the rule names its item by key).</param>
    public static bool Applies(IInputFrame input, bool captured, ItemStack selected, BlockRayHit? target, NeedsData needs, IItemCatalogue items)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(needs);
        ArgumentNullException.ThrowIfNull(items);
        return captured && input.RightClicked && !selected.IsEmpty && needs.Planting is { } rule
            && string.Equals(items.Get(selected.Item).Name, rule.Item, StringComparison.Ordinal)
            && target is { Id: BlockId.Grass or BlockId.Dirt };
    }
}
