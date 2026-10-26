using System.Globalization;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.UI;

/// <summary>The words of the 3D debug overlay, built without any engine type so they can be tested.</summary>
public static class VoxelDebugText
{
    /// <summary>The overlay's lines, top to bottom.</summary>
    /// <param name="fps">Frames per second.</param>
    /// <param name="sim">The 3D simulation.</param>
    /// <param name="seed">The world seed.</param>
    /// <param name="yawSteps">The yaw being drawn, in angle steps.</param>
    /// <param name="pitchSteps">The pitch being drawn, in angle steps.</param>
    /// <param name="playSession">The play session the game runs in (1 for a new game; a load starts the next one).</param>
    public static IReadOnlyList<string> Lines(double fps, VoxelSimulation sim, ulong seed, double yawSteps, double pitchSteps, int playSession)
    {
        ArgumentNullException.ThrowIfNull(sim);
        var inv = CultureInfo.InvariantCulture;
        const double U = VoxelPhysics.UnitsPerBlock;
        var body = sim.Player;
        var yaw = yawSteps * 360.0 / Angles.StepsPerTurn;
        var pitch = pitchSteps * 360.0 / Angles.StepsPerTurn;
        var target = "-";
        if (sim.Target is { } hit)
        {
            var metres = hit.DistanceUnits / U;
            target = string.Create(inv, $"{BlockDefinition.Get(hit.Id).Name} ({hit.Block.X}, {hit.Block.Y}, {hit.Block.Z}) {metres:0.0} m");
            if (sim.GatherTarget == hit.Block && sim.GatherProgress > 0)
            {
                target += string.Create(inv, $"  gather {sim.GatherProgress}");
            }
        }

        return
        [
            string.Create(inv, $"3D   FPS {fps:0}   tick {sim.Tick}   seed {seed}   Session {playSession}"),
            string.Create(inv, $"pos ({body.CenterX / U:0.0}, {body.FeetY / U:0.0}, {body.CenterZ / U:0.0})   yaw {yaw:0}   pitch {pitch:0}"),
            "target: " + target,
        ];
    }
}
