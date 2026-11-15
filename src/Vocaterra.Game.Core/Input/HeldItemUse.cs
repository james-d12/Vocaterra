using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Input;

/// <summary>
/// Using a held item on a block (Y10, Y12): a right click with the item selected and the crosshair on a block uses it there; in the air the click does nothing. The stake marks a plot,
/// the camp kit sets up the camp.
/// </summary>
public static class HeldItemUse
{
    /// <summary>Whether this frame's right click uses the selected <paramref name="item"/> on the block under the crosshair.</summary>
    /// <param name="input">The frame's input.</param>
    /// <param name="captured">Whether the mouse is captured (the world has the input: no talk and no panel is open).</param>
    /// <param name="selected">The hotbar's selected stack.</param>
    /// <param name="target">The block under the crosshair within reach, or <see langword="null"/>.</param>
    /// <param name="item">The item that is used (<see cref="ItemId.Stake"/>, <see cref="ItemId.CampKit"/>).</param>
    public static bool Applies(IInputFrame input, bool captured, ItemStack selected, BlockRayHit? target, ItemId item)
    {
        ArgumentNullException.ThrowIfNull(input);
        return captured && input.RightClicked && !selected.IsEmpty && selected.Item == item && target is not null;
    }
}
