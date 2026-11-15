namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// What the height model decides about one column (B01): where the ground is, and whether water stands on it. The surface passes, the maps and the biome table read this and
/// never the noise behind it.
/// </summary>
/// <param name="Height">The y of the topmost solid block of the column (for a column under water, the bed).</param>
/// <param name="NaturalHeight">The ground before the rivers and lakes carved it, in blocks: the height a bank beside the water eases up to.</param>
/// <param name="Water">Whether water covers the column: the ground is below the sea level, and the column fills with water up to it (see <see cref="WaterLevel"/>).</param>
/// <param name="WaterLevel">The y of the water's top block in a column that has water (the sea level, as the valley's water top), and -1 where there is none.</param>
/// <param name="RiverWeight">How much a river carved this column, 0 to <see cref="Mathematics.IntMath.One"/>.</param>
/// <param name="LakeWeight">How much a lake pulled this column down, 0 to <see cref="Mathematics.IntMath.One"/>.</param>
public readonly record struct ColumnShape(int Height, int NaturalHeight, bool Water, int WaterLevel, int RiverWeight, int LakeWeight);
