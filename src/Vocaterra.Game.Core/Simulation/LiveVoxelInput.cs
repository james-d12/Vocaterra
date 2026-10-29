using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Simulation;

/// <summary>
/// The keyboard and mouse of the 3D run as simulation commands, in plain values. Once per frame the game sets what the devices show: the
/// movement keys, the jump key, the block the crosshair is on while the gather button is held, a placement the second button asked for and the
/// hotbar slot a number key or the wheel picked. Each tick then takes the commands it needs: the walk, the jump and the gather are levels sent
/// every tick, a placement and a slot change are taken once, by the first tick that runs after they were set.
/// </summary>
public sealed class LiveVoxelInput
{
    private int _strafe;
    private int _forward;
    private bool _jump;
    private BlockPos? _gather;
    private (ItemId Item, BlockPos At)? _place;
    private int? _slot;

    /// <summary>Sets the frame's device state.</summary>
    /// <param name="strafe">-1 for left (A), 1 for right (D), 0 for none or both.</param>
    /// <param name="forward">1 for forward (W), -1 for back (S), 0 for none or both.</param>
    /// <param name="jump">Whether the jump key is down.</param>
    /// <param name="gatherBlock">The block under the crosshair while the gather button is held, or <see langword="null"/>.</param>
    /// <param name="placement">The item and the empty cell it goes in, when the place button was pressed this frame; or <see langword="null"/>.</param>
    /// <param name="selectSlot">The hotbar slot (0 to 9) a number key or the wheel picked this frame, or <see langword="null"/>.</param>
    public void SetFrame(int strafe, int forward, bool jump, BlockPos? gatherBlock, (ItemId Item, BlockPos At)? placement, int? selectSlot)
    {
        _strafe = Math.Clamp(strafe, -1, 1);
        _forward = Math.Clamp(forward, -1, 1);
        _jump = jump;
        _gather = gatherBlock;
        if (placement is { } place)
        {
            _place = place;
        }

        if (selectSlot is { } slot)
        {
            _slot = slot;
        }
    }

    /// <summary>Adds this tick's commands to <paramref name="commands"/>.</summary>
    public void Fill(List<VoxelCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (_slot is { } slot)
        {
            commands.Add(VoxelCommand.SelectSlot(slot));
            _slot = null;
        }

        if (_strafe != 0 || _forward != 0)
        {
            commands.Add(VoxelCommand.MoveRelative(_strafe, _forward));
        }

        if (_jump)
        {
            commands.Add(VoxelCommand.Jump);
        }

        if (_gather is { } block)
        {
            commands.Add(VoxelCommand.Interact(block.X, block.Y, block.Z));
        }

        if (_place is { } place)
        {
            commands.Add(VoxelCommand.Place(place.Item, place.At.X, place.At.Y, place.At.Z));
            _place = null;
        }
    }

    /// <summary>
    /// The placement a press of the place button makes, or <see langword="null"/> when nothing happens: the selected item must be placeable
    /// and the crosshair must be on a block (the item goes in the cell in front of the face that was hit).
    /// </summary>
    public static (ItemId Item, BlockPos At)? PlacementFor(ItemStack selected, BlockRayHit? target)
    {
        if (selected.IsEmpty || target is not { } hit || BlockDefinition.PlacedBy(selected.Item) is null)
        {
            return null;
        }

        return (selected.Item, hit.Adjacent);
    }

    /// <summary>
    /// The block the interact button works on: the one under the crosshair when it can be gathered, or when it is water (S6: with a bottle in the
    /// inventory the simulation fills it, without one it only says so), otherwise <see langword="null"/>.
    /// </summary>
    public static BlockPos? GatherFor(BlockRayHit? target) =>
        target is { } hit && (BlockDefinition.Get(hit.Id).Gatherable || hit.Id == BlockId.Water) ? hit.Block : null;
}
