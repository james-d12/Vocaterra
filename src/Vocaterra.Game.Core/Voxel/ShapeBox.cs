namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// An axis-aligned box inside (or, for a fence's collision height, above) one cell, in sixteenths of a block: x and z run 0 to 16 across the cell, y runs 0 to 16 up
/// the cell and up to 24 for the collision of a fence or a gate. Min is inclusive, max exclusive, so a box with an equal min and max has no volume.
/// </summary>
public readonly record struct ShapeBox(int MinX, int MinY, int MinZ, int MaxX, int MaxY, int MaxZ)
{
    /// <summary>The whole cell.</summary>
    public static readonly ShapeBox Full = new(0, 0, 0, 16, 16, 16);

    /// <summary>The volume in cubic sixteenths.</summary>
    public int Volume => Math.Max(0, MaxX - MinX) * Math.Max(0, MaxY - MinY) * Math.Max(0, MaxZ - MinZ);

    /// <summary>Whether the two boxes share volume (touching faces do not count).</summary>
    public bool Overlaps(ShapeBox other) =>
        MinX < other.MaxX && other.MinX < MaxX && MinY < other.MaxY && other.MinY < MaxY && MinZ < other.MaxZ && other.MinZ < MaxZ;

    /// <summary>The box turned <paramref name="quarterTurns"/> quarter turns to the right (clockwise seen from above) about the cell's vertical axis: north to east to south to west.</summary>
    public ShapeBox Turned(int quarterTurns)
    {
        var box = this;
        for (var i = 0; i < (quarterTurns & 3); i++)
        {
            box = new ShapeBox(16 - box.MaxZ, box.MinY, box.MinX, 16 - box.MinZ, box.MaxY, box.MaxX);
        }

        return box;
    }

    /// <summary>The box mirrored in y within the cell (the upper half becomes the lower).</summary>
    public ShapeBox FlippedY() => new(MinX, 16 - MaxY, MinZ, MaxX, 16 - MinY, MaxZ);
}
