namespace Vocaterra.Game.Core.World.Layout;

/// <summary>The river: a smooth curve through the waypoints, from the waterfall's pool to the lake.</summary>
/// <param name="HalfWidth">Half the width of the water in blocks.</param>
/// <param name="Depth">The depth of the water in the middle.</param>
/// <param name="Bank">The width in blocks of the slope from the water up to the land.</param>
/// <param name="Path">The waypoints (x, z), the first at the waterfall's pool, the last in the lake.</param>
public sealed record RiverSpec(int HalfWidth, int Depth, int Bank, IReadOnlyList<(int X, int Z)> Path);

