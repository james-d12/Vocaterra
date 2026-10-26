using Microsoft.Xna.Framework;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Frame;

namespace Vocaterra.Game.Graphics;

/// <summary>The first-person arm (S15): what the hand holds, how it moves (the walk, the gathering, one swing for each block placed) and the light on it.</summary>
public sealed class ArmPresenter : ITickObserver
{
    private readonly VoxelSimulation _sim;
    private readonly Func<int, int, int, bool> _isSolid;
    private readonly bool _dusk;
    private long _lastPlaceTick = long.MinValue / 2;

    /// <summary>Creates the arm of the simulation's body.</summary>
    /// <param name="sim">The simulation: the selected stack, the walk and the gathering.</param>
    /// <param name="isSolid">Whether a block is solid, for the light that reaches the hand.</param>
    /// <param name="dusk">Whether the scene is at dusk (<c>--dusk</c>).</param>
    public ArmPresenter(VoxelSimulation sim, Func<int, int, int, bool> isSolid, bool dusk)
    {
        _sim = sim;
        _isSolid = isSolid;
        _dusk = dusk;
    }

    /// <summary>Once a tick, after the step: a placement in the tick's commands swings the arm once.</summary>
    public void AfterTick(IReadOnlyList<VoxelCommand> commands)
    {
        foreach (var command in commands)
        {
            if (command.Kind == VoxelCommandKind.Place)
            {
                _lastPlaceTick = _sim.Tick;
            }
        }
    }

    /// <summary>The arm as it is now, lit at <paramref name="eye"/> by the scene's <paramref name="lights"/>.</summary>
    public ArmFrame Frame(Vector3 eye, IReadOnlyList<BlockPos> lights)
    {
        var held = HeldItems.For(_sim.SelectedStack);
        var moving = _sim.Player.VelocityX != 0 || _sim.Player.VelocityZ != 0;
        var motion = ArmMotion.At(_sim.Tick, moving, _sim.GatherProgress, _sim.Tick - _lastPlaceTick);
        var light = ArmLight.At(_isSolid, (eye.X, eye.Y, eye.Z), lights, held.Kind == HeldKind.Torch, _dusk);
        return new ArmFrame(held, motion, light);
    }
}
