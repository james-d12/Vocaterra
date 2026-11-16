using System.Globalization;
using Vocaterra.Game.Core.Items.Containers;

namespace Vocaterra.Game.Core.Scripted;

/// <summary>One step of the scripted 3D run, in plain values.</summary>
/// <param name="Kind">What the step does.</param>
/// <param name="Yaw">For <see cref="VoxelStepKind.Look"/>: the yaw in degrees (0 north, 90 east).</param>
/// <param name="Pitch">For <see cref="VoxelStepKind.Look"/>: the pitch in degrees (positive up).</param>
/// <param name="X">The block column (east) of a walk, interact or place.</param>
/// <param name="Y">The block layer (up) of an interact or place.</param>
/// <param name="Z">The block row (south) of a walk, interact or place.</param>
/// <param name="Item">For <see cref="VoxelStepKind.Place"/>: the item as typed (a Spanish noun such as <c>madera</c>, or the item's key such as <c>wood</c>).</param>
/// <param name="Slot">For <see cref="VoxelStepKind.Select"/>: the hotbar slot, 1 to 10 as the number keys give them.</param>
/// <param name="Hour">For <see cref="VoxelStepKind.SetTime"/>: the hour the clock is held at, 0 to 24.</param>
public sealed record VoxelStep(VoxelStepKind Kind, double Yaw = 0, double Pitch = 0, int X = 0, int Y = 0, int Z = 0, string Item = "", int Slot = 0, double Hour = 0)
{
    /// <summary>A step that faces a direction.</summary>
    public static VoxelStep Look(double yawDegrees, double pitchDegrees) => new(VoxelStepKind.Look, Yaw: yawDegrees, Pitch: pitchDegrees);

    /// <summary>A step that walks to a column.</summary>
    public static VoxelStep WalkTo(int x, int z) => new(VoxelStepKind.WalkTo, X: x, Z: z);

    /// <summary>A step that puts the player at a column (a debug step).</summary>
    public static VoxelStep Teleport(int x, int z) => new(VoxelStepKind.Teleport, X: x, Z: z);

    /// <summary>A step that gathers a block.</summary>
    public static VoxelStep Interact(int x, int y, int z) => new(VoxelStepKind.Interact, X: x, Y: y, Z: z);

    /// <summary>A step that places an item.</summary>
    public static VoxelStep Place(string item, int x, int y, int z) => new(VoxelStepKind.Place, X: x, Y: y, Z: z, Item: item);

    /// <summary>A step that selects a hotbar slot (1 to 10).</summary>
    public static VoxelStep Select(int slot) => new(VoxelStepKind.Select, Slot: slot);

    /// <summary>Craft the recipe that makes the item (its Spanish noun or the item's key).</summary>
    public static VoxelStep Craft(string item) => new(VoxelStepKind.Craft, Item: item);

    /// <summary>A step that moves items by hand (I02): <paramref name="action"/> at a slot of a container (<see cref="ContainerIds"/>); the container and slot are ignored by sort and return.</summary>
    public static VoxelStep SlotMove(MoveAction action, int container, int slot) => new(VoxelStepKind.SlotMove, X: container, Y: slot, Slot: (int)action);

    /// <summary>
    /// Reads <c>action[,container,slot]</c> (I02): the action is <c>left</c>, <c>right</c>, <c>shift</c>, <c>sort</c> or <c>return</c>; the container <c>bag</c>, <c>equip</c> or <c>trash</c>; the slot a number from 0 (the equipment's is its flat index).
    /// Sort and return take no container and slot.
    /// </summary>
    /// <param name="value">The text after <c>--move</c>.</param>
    /// <param name="step">The step, when the text is valid.</param>
    public static bool TryParseMove(string value, out VoxelStep step)
    {
        step = null!;
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        MoveAction? action = parts[0] switch
        {
            "left" => MoveAction.LeftClick,
            "right" => MoveAction.RightClick,
            "shift" => MoveAction.QuickMove,
            "sort" => MoveAction.Sort,
            "return" => MoveAction.ReturnHeld,
            _ => null,
        };
        if (action is not { } move)
        {
            return false;
        }

        if (move is MoveAction.Sort or MoveAction.ReturnHeld)
        {
            step = SlotMove(move, ContainerIds.Bag, 0);
            return parts.Length == 1;
        }

        int? container = parts.Length == 3 ? parts[1] switch { "bag" => ContainerIds.Bag, "equip" => ContainerIds.Equipment, "trash" => ContainerIds.Trash, _ => null } : null;
        if (container is not { } id || !TryInt(parts[2], out var slot))
        {
            return false;
        }

        step = SlotMove(move, id, slot);
        return true;
    }

    /// <summary>A step that pours a water source into a cell (a debug step).</summary>
    public static VoxelStep Pour(int x, int y, int z) => new(VoxelStepKind.Pour, X: x, Y: y, Z: z);

    /// <summary>A step that holds the clock at an hour (0 to 24).</summary>
    public static VoxelStep SetTime(double hour) => new(VoxelStepKind.SetTime, Hour: hour);

    /// <summary>A step that lets a held clock run on.</summary>
    public static VoxelStep RunTime() => new(VoxelStepKind.RunTime);

    /// <summary>A step that holds a weather kind by its id.</summary>
    public static VoxelStep SetWeather(string id) => new(VoxelStepKind.SetWeather, Item: id);

    /// <summary>A step that lets a held weather run on.</summary>
    public static VoxelStep RunWeather() => new(VoxelStepKind.RunWeather);

    /// <summary>Reads <c>x,z</c> (two non-negative integers).</summary>
    public static bool TryParseColumn(string value, out int x, out int z)
    {
        x = 0;
        z = 0;
        var parts = value.Split(',');
        return parts.Length == 2 && TryInt(parts[0], out x) && TryInt(parts[1], out z);
    }

    /// <summary>Reads <c>x,y,z</c> (three non-negative integers).</summary>
    public static bool TryParseBlock(string value, out int x, out int y, out int z)
    {
        x = 0;
        y = 0;
        z = 0;
        var parts = value.Split(',');
        return parts.Length == 3 && TryInt(parts[0], out x) && TryInt(parts[1], out y) && TryInt(parts[2], out z);
    }

    private static bool TryInt(string text, out int value) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
