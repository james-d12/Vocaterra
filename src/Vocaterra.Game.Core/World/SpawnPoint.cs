using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World;

/// <summary>Where the player starts: the cell the feet are in and the facing.</summary>
/// <param name="Feet">The cell the feet are in (the air cell above the ground); the body starts at its middle.</param>
/// <param name="Yaw">The facing in <see cref="Angles"/> steps.</param>
/// <param name="Pitch">The look up or down in <see cref="Angles"/> steps (negative looks down), 0 for level.</param>
public readonly record struct SpawnPoint(BlockPos Feet, int Yaw, int Pitch = 0);

