using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Input;

/// <summary>Setting up the camp with the camp kit (Y12): a right click with the kit selected and the crosshair on a block uses it there; in the air the click does nothing.</summary>
public static class CampKitUse
{
    /// <summary>Whether this frame's right click uses the selected camp kit on the block under the crosshair.</summary>
    /// <param name="input">The frame's input.</param>
    /// <param name="captured">Whether the mouse is captured (the world has the input: no talk and no panel is open).</param>
    /// <param name="selected">The hotbar's selected stack.</param>
    /// <param name="target">The block under the crosshair within reach, or <see langword="null"/>.</param>
    public static bool Applies(IInputFrame input, bool captured, ItemStack selected, BlockRayHit? target)
    {
        ArgumentNullException.ThrowIfNull(input);
        return captured && input.RightClicked && !selected.IsEmpty && selected.Item == ItemId.CampKit && target is not null;
    }
}
