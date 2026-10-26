using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World;

/// <summary>An NPC's place in a scene: the block cell its feet occupy (the air cell above the ground) and the way it faces.</summary>
/// <param name="Name">The NPC's name (<c>guide</c>, <c>shopkeeper</c>, <c>villager</c>).</param>
/// <param name="Feet">The cell the NPC's feet are in; its middle is (X + 0.5, Y, Z + 0.5) in block units.</param>
/// <param name="Yaw">The facing in <see cref="Angles"/> steps (0 north, 1 024 east).</param>
public sealed record NpcPlacement(string Name, BlockPos Feet, int Yaw);

