using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.World.Layout;

/// <summary>An NPC position: the feet cell is (X, terrain height + 1, Z).</summary>
/// <param name="Name">The NPC's name (<c>guide</c>, <c>shopkeeper</c>, <c>villager</c>).</param>
/// <param name="X">The column.</param>
/// <param name="Z">The row.</param>
/// <param name="Yaw">The facing in <see cref="Angles"/> steps.</param>
public sealed record NpcSpec(string Name, int X, int Z, int Yaw);

