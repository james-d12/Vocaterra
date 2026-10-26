using Vocaterra.Game.Core.Physics;

namespace Vocaterra.Game.Core.Simulation;

/// <summary>
/// The player's body as plain values, in 1/4096-block units and 1/4096-turn angles (the fixed-point units of <see cref="PlayerBody3D"/>).
/// </summary>
/// <param name="MinX">The west edge of the box.</param>
/// <param name="MinY">The bottom of the box (the feet).</param>
/// <param name="MinZ">The north edge of the box.</param>
/// <param name="VelocityX">The velocity along X.</param>
/// <param name="VelocityY">The vertical velocity.</param>
/// <param name="VelocityZ">The velocity along Z.</param>
/// <param name="Grounded">Whether the body stands on a block.</param>
/// <param name="SteppedUp">Whether the last tick stepped up a block (the camera's flag).</param>
/// <param name="Yaw">The facing, in 1/4096 turns (0 north, clockwise).</param>
/// <param name="Pitch">The look angle, positive up.</param>
public readonly record struct PlayerBodyState(
    int MinX,
    int MinY,
    int MinZ,
    int VelocityX,
    int VelocityY,
    int VelocityZ,
    bool Grounded,
    bool SteppedUp,
    int Yaw,
    int Pitch);

