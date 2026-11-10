using System.Globalization;
using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.UI.Hud;

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
            Atmosphere(sim.Atmosphere, sim.Weather.Previous.Id, sim.Weather.BlendAt(sim.Tick), sim.Clock.IsFrozen, sim.Weather.IsHeld),
            "target: " + target,
        ];
    }

    /// <summary>
    /// The clock and weather line (E03): the hour as hh:mm, the phase, and <c>held</c> when the clock is held; the weather's kind and, during a change of kind, the kind it
    /// comes from and the blend in percent; <c>held</c> when the weather is held.
    /// </summary>
    /// <param name="atmosphere">The sky and weather now.</param>
    /// <param name="previousKind">The id of the kind the weather is leaving (the current one when nothing changes).</param>
    /// <param name="blend">How far the change has gone, 0 to 1 (1 when nothing is changing).</param>
    /// <param name="clockHeld">Whether the clock is held.</param>
    /// <param name="weatherHeld">Whether the weather is held.</param>
    public static string Atmosphere(AtmosphereSnapshot atmosphere, string previousKind, double blend, bool clockHeld, bool weatherHeld)
    {
        ArgumentNullException.ThrowIfNull(previousKind);
        var inv = CultureInfo.InvariantCulture;
        var minutes = (int)Math.Round(atmosphere.Hour * 60) % (24 * 60);
        var time = string.Create(inv, $"time {minutes / 60:00}:{minutes % 60:00} {DayPhaseNames.Of(atmosphere.Phase)}") + (clockHeld ? " held" : string.Empty);
        var weather = "weather " + atmosphere.KindId;
        if (blend < 1)
        {
            weather += string.Create(inv, $" from {previousKind} {Math.Clamp(blend, 0, 1) * 100:0}%");
        }

        if (weatherHeld)
        {
            weather += " held";
        }

        return time + "   " + weather;
    }
}
