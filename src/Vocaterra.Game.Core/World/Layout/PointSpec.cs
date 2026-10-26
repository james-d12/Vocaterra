namespace Vocaterra.Game.Core.World.Layout;

/// <summary>A named point of the scene beside the sites, given by column and row; its Y is the top of the terrain (or of the water).</summary>
/// <param name="Name">The point's name.</param>
/// <param name="X">The column.</param>
/// <param name="Z">The row.</param>
public sealed record PointSpec(string Name, int X, int Z);

