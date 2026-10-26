using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.World.Layout;

/// <summary>The player's spawn: the feet cell is (X, terrain height + 1, Z).</summary>
/// <param name="X">The column.</param>
/// <param name="Z">The row.</param>
/// <param name="Yaw">The facing in <see cref="Angles"/> steps.</param>
/// <param name="Pitch">The look up or down in <see cref="Angles"/> steps (negative looks down; 0 level, S14).</param>
public sealed record SpawnSpec(int X, int Z, int Yaw, int Pitch = 0);

