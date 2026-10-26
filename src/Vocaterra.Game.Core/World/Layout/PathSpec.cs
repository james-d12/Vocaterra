namespace Vocaterra.Game.Core.World.Layout;

/// <summary>A path of trodden ground between two sites, cut into the terrain; trees keep off it.</summary>
/// <param name="Name">The path's name.</param>
/// <param name="From">The site it starts at.</param>
/// <param name="To">The site it ends at.</param>
/// <param name="Via">The waypoints between the two sites (x, z); the path is a straight line from one to the next.</param>
/// <param name="HalfWidth">Half the width in blocks (1 gives a path three blocks wide).</param>
/// <param name="Mode">How the path takes the ground.</param>
public sealed record PathSpec(string Name, string From, string To, IReadOnlyList<(int X, int Z)> Via, int HalfWidth, PathMode Mode);

