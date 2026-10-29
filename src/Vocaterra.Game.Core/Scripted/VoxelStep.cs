using System.Globalization;

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
public sealed record VoxelStep(VoxelStepKind Kind, double Yaw = 0, double Pitch = 0, int X = 0, int Y = 0, int Z = 0, string Item = "", int Slot = 0)
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
