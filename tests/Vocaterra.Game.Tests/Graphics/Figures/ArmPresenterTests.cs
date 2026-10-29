using Microsoft.Xna.Framework;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Figures;

namespace Vocaterra.Game.Tests.Graphics.Figures;

/// <summary>The first-person arm: a placement in a tick's commands swings it once, and an arm with no placement stays still.</summary>
public sealed class ArmPresenterTests
{
    [Fact]
    public void APlacementSwingsTheArmAndOtherCommandsDoNot()
    {
        var sim = VoxelSimulation.Create(RouteScene.Clearing());
        var placed = new ArmPresenter(sim, sim.Grid.IsSolid, dusk: false);
        var still = new ArmPresenter(sim, sim.Grid.IsSolid, dusk: false);
        sim.Step([]);
        placed.AfterTick([VoxelCommand.SelectSlot(0), VoxelCommand.Place(ItemId.Torch, 1, 2, 3)]);
        still.AfterTick([VoxelCommand.SelectSlot(0)]);
        sim.Step([]);
        sim.Step([]);

        var eye = new Vector3(10.5f, 20f, 10.5f);
        Assert.True(placed.Frame(eye, []).Motion.Pitch < 0, "two ticks after the placement the arm dips");
        Assert.Equal(0f, still.Frame(eye, []).Motion.Pitch);
    }
}
