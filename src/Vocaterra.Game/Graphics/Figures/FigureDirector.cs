using Microsoft.Xna.Framework;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Graphics.Figures;

/// <summary>
/// The figures in the scene (S15): the NPCs, who face the player and gesture while they talk with them, the one under the crosshair, and the
/// player's own figure for the view over the shoulder, whose stride follows the distance walked.
/// </summary>
public sealed class FigureDirector : ITickObserver
{
    private readonly VoxelSimulation _sim;
    private readonly LookControl _look;
    private readonly DialogueModel _dialogue;
    private double _walkDistance;
    private (double X, double Z)? _lastWalkPoint;

    /// <summary>Creates the figures of a scene's NPCs.</summary>
    /// <param name="scene">The scene whose NPCs stand in the world.</param>
    /// <param name="sim">The simulation whose body is the player.</param>
    /// <param name="look">The player's look.</param>
    /// <param name="dialogue">The panel: the NPC it is open on is the one talking.</param>
    public FigureDirector(Scene scene, VoxelSimulation sim, LookControl look, DialogueModel dialogue)
    {
        Figures = NpcFigures.FromScene(scene);
        _sim = sim;
        _look = look;
        _dialogue = dialogue;
    }

    /// <summary>The NPCs' figures, in the scene's order.</summary>
    public IReadOnlyList<NpcFigure> Figures { get; }

    /// <summary>
    /// Once a tick: the figures in a conversation face the player and gesture, the others stand as the scene put them, and the distance the
    /// player has walked grows.
    /// </summary>
    public void AfterTick()
    {
        var px = _sim.Player.CenterX / (double)VoxelPhysics.UnitsPerBlock;
        var pz = _sim.Player.CenterZ / (double)VoxelPhysics.UnitsPerBlock;
        foreach (var figure in Figures)
        {
            figure.Talking = _dialogue.IsOpen && string.Equals(_dialogue.Npc, figure.Name, StringComparison.Ordinal);
            FigureFacing.Update(figure, px, pz);
        }

        if (_lastWalkPoint is { } last)
        {
            _walkDistance += Math.Sqrt(((px - last.X) * (px - last.X)) + ((pz - last.Z) * (pz - last.Z)));
        }

        _lastWalkPoint = (px, pz);
    }

    /// <inheritdoc />
    void ITickObserver.AfterTick(IReadOnlyList<VoxelCommand> commands) => AfterTick();

    /// <summary>The NPC figure under the crosshair from <paramref name="eye"/>, within reach with no block in the way: the one E talks to.</summary>
    public NpcFigure? Aimed(Vector3 eye)
    {
        if (_dialogue.IsOpen)
        {
            return null;
        }

        var from = (x: (double)eye.X, y: (double)eye.Y, z: (double)eye.Z);
        foreach (var figure in Figures)
        {
            var point = NpcAim.BodyOf(figure.Feet);
            if (NpcAim.Hits(from, _look.Yaw, _look.Pitch, point)
                && !LineOfSight.Blocked(_sim.Grid, (eye.X, eye.Y, eye.Z), ((float)point.X, (float)point.Y, (float)point.Z), NpcAim.Distance(from, point)))
            {
                return figure;
            }
        }

        return null;
    }

    /// <summary>The player's own figure for the view over the shoulder: at the feet of the body, facing the look.</summary>
    public PlayerAvatar Avatar()
    {
        var body = _sim.Player;
        var moving = body.VelocityX != 0 || body.VelocityZ != 0;
        return new PlayerAvatar(
            (float)(body.CenterX / (double)VoxelPhysics.UnitsPerBlock),
            (float)(body.FeetY / (double)VoxelPhysics.UnitsPerBlock),
            (float)(body.CenterZ / (double)VoxelPhysics.UnitsPerBlock),
            (float)(_look.YawSteps * Math.Tau / Angles.StepsPerTurn),
            (float)(_look.PitchSteps * Math.Tau / Angles.StepsPerTurn),
            (float)(_walkDistance * 2.6),
            moving);
    }
}
