using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.Land;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Scripted;

/// <summary>The scripted steps go through the land rules (Y09): a place, an interact on a plank and a pour on a protected column are refused and the log says so.</summary>
public sealed class LandStepTests
{
    private static List<string> Go(VoxelSession session, params VoxelStep[] steps)
    {
        var log = new List<string>();
        var look = new LookControl();
        var script = new VoxelScript(session.Simulation, look, steps, static name => name is "madera" ? ItemId.Wood : null, log.Add);
        var commands = new List<VoxelCommand>();
        for (var tick = 0; tick < 600 && !script.IsDone; tick++)
        {
            script.Fill(commands);
            commands.Insert(0, VoxelCommand.Look(look.Yaw, look.Pitch));
            session.Step(commands);
        }

        Assert.True(script.IsDone);
        return log;
    }

    [Fact]
    public void Given_APlaceStepInTheVillage_When_TheScriptRuns_Then_ItIsRefusedAndTheLogNamesTheLandRules()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        _ = session.Simulation.GrantItems(ItemId.Wood, 2);
        var (x, z) = LandTestScene.OpenColumn(session.Scene.Grid, 292, 218, 12, (cx, cz) => session.Land.Rules.ProtectionAt(cx, cz) == Protection.Village);
        var cell = LandTestScene.StandBeside(session, x, z);

        var log = Go(session, VoxelStep.Place("madera", cell.X, cell.Y, cell.Z));

        Assert.Equal(BlockId.Air, session.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z));
        Assert.Equal($"place madera@{cell.X},{cell.Y},{cell.Z}: refused by the land rules (village: no building there)", Assert.Single(log));
    }

    [Fact]
    public void Given_APlaceStepInTheMeadow_When_TheScriptRuns_Then_ThePlankIsPlacedAsBefore()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        _ = session.Simulation.GrantItems(ItemId.Wood, 2);
        var cell = LandTestScene.StandBeside(session, LandTestScene.NorthPlot.X + 5, LandTestScene.NorthPlot.Z + 5);

        var log = Go(session, VoxelStep.Place("madera", cell.X, cell.Y, cell.Z));

        Assert.Equal(BlockId.Planks, session.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z));
        Assert.Contains("placed planks", Assert.Single(log), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_AnInteractStepOnAPlankOutsideTheOwnedPlot_When_TheScriptRuns_Then_ItIsRefusedAndTheLogSaysWhy()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        _ = session.Simulation.GrantItems(ItemId.Wood, 2);
        var cell = LandTestScene.StandBeside(session, LandTestScene.NorthPlot.X + 5, LandTestScene.NorthPlot.Z + 5);
        _ = Go(session, VoxelStep.Place("madera", cell.X, cell.Y, cell.Z));

        var log = Go(session, VoxelStep.Interact(cell.X, cell.Y, cell.Z));

        Assert.Equal(BlockId.Planks, session.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z));
        Assert.Contains("refused by the land rules", Assert.Single(log), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_APourStepInTheVillageAndOneInTheMeadow_When_TheScriptRuns_Then_OnlyTheMeadowTakesTheWater()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        var (x, z) = LandTestScene.OpenColumn(session.Scene.Grid, 292, 218, 12, (cx, cz) => session.Land.Rules.ProtectionAt(cx, cz) == Protection.Village);
        var village = LandTestScene.AboveGround(session, x, z);
        var meadow = LandTestScene.AboveGround(session, LandTestScene.NorthPlot.X + 5, LandTestScene.NorthPlot.Z + 5);

        var log = Go(session, VoxelStep.Pour(village.X, village.Y, village.Z), VoxelStep.Pour(meadow.X, meadow.Y, meadow.Z));

        Assert.Equal(BlockId.Air, session.Scene.Grid.GetBlock(village.X, village.Y, village.Z));
        Assert.Contains("refused", log[0], StringComparison.Ordinal);
        Assert.Equal(BlockId.Water, session.Scene.Grid.GetBlock(meadow.X, meadow.Y, meadow.Z));
        Assert.Contains("a water source", log[1], StringComparison.Ordinal);
    }
}
