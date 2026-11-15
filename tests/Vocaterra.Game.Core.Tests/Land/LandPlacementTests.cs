using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Land;

/// <summary>
/// The rules hook in play (Y09): planks are refused on the village, the roads and Luna's camp, with a hint, and placed in the wild; a torch is always allowed; a placed plank comes back only on
/// owned land; ordinary gathering is as it was; the scene's own build is untouched.
/// </summary>
public sealed class LandPlacementTests
{
    private static VoxelSession NewSession()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        _ = session.Simulation.GrantItems(ItemId.Wood, 5);
        _ = session.Simulation.GrantItems(ItemId.Torch, 2);
        return session;
    }

    private static string Hint(VoxelSession session, string key) => session.Locale.Ui(key);

    private static void PlaceAt(VoxelSession session, ItemId item, BlockPos cell) => session.Step([VoxelCommand.Place(item, cell.X, cell.Y, cell.Z)]);

    private static (VoxelSession Session, BlockPos Cell) BesideVillage(Protection expected)
    {
        var session = NewSession();
        var (x, z) = LandTestScene.OpenColumn(session.Scene.Grid, 292, 218, 12, (cx, cz) => LandTestScene.RulesOf(session).ProtectionAt(cx, cz) == expected);
        return (session, LandTestScene.StandBeside(session, x, z));
    }

    [Fact]
    public void Given_ColumnsOfTheVillage_When_PlanksArePlaced_Then_ItIsRefusedWithTheVillageHintAndTheWoodStays()
    {
        var (session, cell) = BesideVillage(Protection.Village);
        var wood = session.Simulation.Inventory.Count(ItemId.Wood);

        PlaceAt(session, ItemId.Wood, cell);

        Assert.Equal(BlockId.Air, session.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z));
        Assert.Equal(wood, session.Simulation.Inventory.Count(ItemId.Wood));
        var refusal = Assert.Single(session.Simulation.Events, static e => e.Kind == SimulationEventKind.PlaceRefused);
        Assert.Equal((cell.X, cell.Y, cell.Z, (int)Protection.Village), (refusal.X, refusal.Y, refusal.Z, refusal.Detail));
        Assert.DoesNotContain(session.Simulation.Events, static e => e.Kind == SimulationEventKind.BlockPlaced);
        Assert.Equal(["You cannot build in the village."], session.TakeHints());
        Assert.Equal("You cannot build in the village.", Hint(session, VoxelSession.LandVillageHint));
    }

    [Fact]
    public void Given_TheBridge_When_PlanksArePlacedOnIt_Then_ItIsRefusedWithTheRoadHint()
    {
        var session = NewSession();
        var deck = session.Scene.Grid.BlocksOfKind(BlockId.Bridge).First(b => b.X == 230);
        var cell = LandTestScene.StandBeside(session, deck.X, deck.Z);

        PlaceAt(session, ItemId.Wood, cell);

        Assert.Equal(BlockId.Air, session.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z));
        Assert.Equal(["You cannot build on the road."], session.TakeHints());
    }

    [Fact]
    public void Given_ColumnsOfLunasCamp_When_PlanksArePlaced_Then_ItIsRefusedWithTheCampHint()
    {
        var session = NewSession();
        var camp = session.Scene.Point("camp.guide");
        var (x, z) = LandTestScene.OpenColumn(session.Scene.Grid, camp.X, camp.Z, 10, (cx, cz) => LandTestScene.RulesOf(session).ProtectionAt(cx, cz) == Protection.Camp);
        var cell = LandTestScene.StandBeside(session, x, z);

        PlaceAt(session, ItemId.Wood, cell);

        Assert.Equal(BlockId.Air, session.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z));
        Assert.Equal(["You cannot build at the camp."], session.TakeHints());
    }

    [Fact]
    public void Given_TheMeadowOutsideEveryProtection_When_PlanksArePlaced_Then_TheyStandAndNothingIsSaid()
    {
        var session = NewSession();
        var (x, z) = LandTestScene.NorthPlot;
        var cell = LandTestScene.StandBeside(session, x + 5, z + 5);
        Assert.False(LandTestScene.RulesOf(session).IsProtected(cell.X, cell.Z));

        PlaceAt(session, ItemId.Wood, cell);

        Assert.Equal(BlockId.Planks, session.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z));
        Assert.Equal(4, session.Simulation.Inventory.Count(ItemId.Wood));
        Assert.Empty(session.TakeHints());
    }

    [Fact]
    public void Given_TheRiverOutsideTheRoadsMargin_When_PlanksArePlacedInTheWater_Then_ADamIsStillPossible()
    {
        var session = NewSession();
        var grid = session.Scene.Grid;
        var level = session.Scene.WaterLevel;
        var river = session.Scene.Point("river");
        Assert.Equal(BlockId.Water, grid.GetBlock(river.X + 1, level, river.Z));
        Assert.False(LandTestScene.RulesOf(session).IsProtected(river.X + 1, river.Z));
        session.Simulation.TeleportTo(new BlockPos(river.X - 1, level, river.Z)); // the player wades in the stream

        PlaceAt(session, ItemId.Wood, new BlockPos(river.X + 1, level, river.Z));

        Assert.Equal(BlockId.Planks, grid.GetBlock(river.X + 1, level, river.Z));
        Assert.Empty(session.TakeHints());
    }

    [Fact]
    public void Given_ARimOfLunasFireRing_When_ATorchIsPlaced_Then_ItIsAllowedInTheProtectedCamp()
    {
        var session = NewSession();
        var ring = session.Scene.Point("firering");
        var stone = session.Scene.Grid.BlocksOfKind(BlockId.FireRing).First(p => Math.Abs(p.X - ring.X) <= 1 && Math.Abs(p.Z - ring.Z) <= 1);
        var rim = new BlockPos(stone.X, stone.Y + 1, stone.Z);
        Assert.Equal(Protection.Camp, LandTestScene.RulesOf(session).ProtectionAt(rim.X, rim.Z));
        session.Simulation.TeleportTo(new BlockPos(rim.X, stone.Y + 1, rim.Z + 3));

        PlaceAt(session, ItemId.Torch, rim);

        Assert.Equal(BlockId.Torch, session.Scene.Grid.GetBlock(rim.X, rim.Y, rim.Z));
        Assert.Contains(session.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.TorchAtRing);
        Assert.Empty(session.TakeHints());
    }

    [Fact]
    public void Given_AColumnOfTheVillage_When_ATorchIsPlaced_Then_ItIsAllowedThereToo()
    {
        var (session, cell) = BesideVillage(Protection.Village);

        PlaceAt(session, ItemId.Torch, cell);

        Assert.Equal(BlockId.Torch, session.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z));
        Assert.Empty(session.TakeHints());
    }

    [Fact]
    public void Given_APlankPlacedInTheWild_When_ItIsHeldOnOutsideTheOwnedPlot_Then_ItStaysAndTheHintSaysWhyOnce()
    {
        var session = NewSession();
        var (x, z) = LandTestScene.NorthPlot;
        var cell = LandTestScene.StandBeside(session, x + 5, z + 5);
        PlaceAt(session, ItemId.Wood, cell);
        _ = session.TakeHints();
        var (yaw, pitch) = VoxelWalker.AimAt(session, cell);

        for (var tick = 0; tick < 90; tick++)
        {
            session.Step([VoxelCommand.Look(yaw, pitch), VoxelCommand.Interact(cell.X, cell.Y, cell.Z)]);
        }

        Assert.Equal(BlockId.Planks, session.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z));
        Assert.Equal(4, session.Simulation.Inventory.Count(ItemId.Wood));
        Assert.Equal(["You can only take planks back on your own land."], session.TakeHints());
    }

    [Fact]
    public void Given_APlankPlacedInsideAnOwnedPlot_When_ItIsHeldOn_Then_ItComesBackAsWood()
    {
        var session = NewSession();
        var (x, z) = LandTestScene.NorthPlot;
        Assert.True(session.SetPendingPlot(x, z).Ok);
        Assert.True(session.OwnPlot());
        var cell = LandTestScene.StandBeside(session, x + 5, z + 5);
        PlaceAt(session, ItemId.Wood, cell);
        Assert.Equal(BlockId.Planks, session.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z));

        Assert.True(VoxelWalker.Gather(session, cell));

        Assert.Equal(BlockId.Air, session.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z));
        Assert.Equal(5, session.Simulation.Inventory.Count(ItemId.Wood));
        Assert.Empty(session.TakeHints());
    }

    [Fact]
    public void Given_AnOwnedPlot_When_APlankOutsideItIsHeldOn_Then_ItStaysAndTheHintSaysWhy()
    {
        var session = NewSession();
        var (x, z) = LandTestScene.NorthPlot;
        Assert.True(session.SetPendingPlot(x, z).Ok);
        Assert.True(session.OwnPlot());
        var (ox, oz) = (x + 14, z + 14); // beside the plot, in the meadow
        var cell = LandTestScene.StandBeside(session, ox, oz);
        Assert.False(session.IsOwned(cell.X, cell.Z));
        PlaceAt(session, ItemId.Wood, cell);
        Assert.Equal(BlockId.Planks, session.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z));
        _ = session.TakeHints();

        Assert.False(VoxelWalker.Gather(session, cell, maxTicks: 60));

        Assert.Equal(BlockId.Planks, session.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z));
        Assert.Equal(["You can only take planks back on your own land."], session.TakeHints());
    }

    [Fact]
    public void Given_TheVillagesOwnPlanks_When_TheyAreHeldOn_Then_TheyAreNotTakenAndTheHintSaysWhy()
    {
        var session = NewSession();
        var floor = session.Scene.Grid.BlocksOfKind(BlockId.Planks).First(b => LandTestScene.RulesOf(session).ProtectionAt(b.X, b.Z) == Protection.Village);
        session.Simulation.TeleportTo(Nearby(session, floor));

        Assert.False(VoxelWalker.Gather(session, floor, maxTicks: 60));

        Assert.Equal(BlockId.Planks, session.Scene.Grid.GetBlock(floor.X, floor.Y, floor.Z));
        Assert.Equal(["You can only take planks back on your own land."], session.TakeHints());
    }

    [Fact]
    public void Given_ATrunkInTheVillage_When_ItIsGathered_Then_OrdinaryGatheringStillWorksThere()
    {
        var session = NewSession();
        var rules = LandTestScene.RulesOf(session);
        var trunk = session.Scene.Grid.BlocksOfKind(BlockId.Trunk).FirstOrDefault(b => rules.ProtectionAt(b.X, b.Z) == Protection.Village);
        Assert.NotEqual(default, trunk); // the village has trees (its orchard)
        session.Simulation.TeleportTo(Nearby(session, trunk));
        var wood = session.Simulation.Inventory.Count(ItemId.Wood);

        Assert.True(VoxelWalker.Gather(session, trunk));

        Assert.Equal(wood + 1, session.Simulation.Inventory.Count(ItemId.Wood));
        Assert.Empty(session.TakeHints());
    }

    [Fact]
    public void Given_ABerryBushOnProtectedGround_When_ItIsGathered_Then_OrdinaryGatheringStillWorksThere()
    {
        var session = NewSession();
        var rules = LandTestScene.RulesOf(session);
        var bush = session.Scene.Grid.BlocksOfKind(BlockId.BerryBush).FirstOrDefault(b => rules.IsProtected(b.X, b.Z)); // the berries of Luna's camp: the camp task gathers them
        Assert.NotEqual(default, bush);
        session.Simulation.TeleportTo(Nearby(session, bush));

        Assert.True(VoxelWalker.Gather(session, bush));

        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Berry));
        Assert.Empty(session.TakeHints());
    }

    [Fact]
    public void Given_ASceneWhoseBuildPlacesBlocksInProtectedRegions_When_ASessionStartsOnIt_Then_TheWorldIsUntouched()
    {
        var scene = TestGame.Route(LandTestScene.Seed);
        var digest = scene.Grid.ComputeDigestHex();

        var session = new VoxelSession(TestGame.Data, LandTestScene.Seed, GuideSession.DefaultStartedAt, scene, CreditRules.FullTyped, TestGame.NoLessons);

        var rules = LandTestScene.RulesOf(session);
        var builtInside = scene.Grid.BlocksOfKind(BlockId.Planks).Concat(scene.Grid.BlocksOfKind(BlockId.Wall)).Count(b => rules.IsProtected(b.X, b.Z));
        Assert.True(builtInside > 100, $"the scene builds its houses on protected columns ({builtInside} blocks)");
        Assert.Equal(digest, scene.Grid.ComputeDigestHex());
    }

    [Fact]
    public void Given_ASimulationWithNoRulesHook_When_PlanksArePlacedAnywhere_Then_ItIsAsBeforeTheLandRules()
    {
        var scene = TestGame.Route(LandTestScene.Seed);
        var simulation = VoxelSimulation.Create(scene);
        _ = simulation.GrantItems(ItemId.Wood, 1);
        var village = scene.Point("village");
        var (x, z) = LandTestScene.OpenColumn(scene.Grid, village.X, village.Z, 12, static (_, _) => true);
        var (standY, _, _) = LandRules.GroundOf(scene.Grid, x, z + 2);
        var column = new BlockPos(x, LandRules.GroundOf(scene.Grid, x, z).Y + 1, z);
        simulation.TeleportTo(new BlockPos(x, standY + 1, z + 2));
        Assert.True(LandRules.Create(TestGame.Data.Land, TestGame.Data.Scene, scene).IsProtected(x, z));

        simulation.Step([VoxelCommand.Place(ItemId.Wood, column.X, column.Y, column.Z)]);

        Assert.Equal(BlockId.Planks, scene.Grid.GetBlock(column.X, column.Y, column.Z));
        Assert.Null(simulation.BuildRules);
    }

    [Fact]
    public void Given_APlankUnderTheCrosshair_When_TheInteractButtonIsAsked_Then_ItWorksOnItSoTheSimulationCanTakeItBackOrSayWhyNot()
    {
        var planks = new BlockRayHit(new BlockPos(5, 11, 5), BlockId.Planks, BlockFace.South, new BlockPos(5, 11, 6), 3000);
        var stone = new BlockRayHit(new BlockPos(5, 11, 5), BlockId.Stone, BlockFace.South, new BlockPos(5, 11, 6), 3000);

        Assert.Equal(new BlockPos(5, 11, 5), LiveVoxelInput.GatherFor(planks));
        Assert.Null(LiveVoxelInput.GatherFor(stone));
    }

    // A feet cell next to the block a player can stand on: the first standable cell within two blocks, nearest first.
    private static BlockPos Nearby(VoxelSession session, BlockPos block)
    {
        var grid = session.Scene.Grid;
        for (var radius = 1; radius <= 3; radius++)
        {
            for (var dy = 2; dy >= -2; dy--)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        var feet = new BlockPos(block.X + dx, block.Y + dy, block.Z + dz);
                        if ((dx != 0 || dz != 0) && VoxelWalker.IsStandable(grid, feet) && Math.Max(Math.Abs(dx), Math.Abs(dz)) == radius)
                        {
                            return feet;
                        }
                    }
                }
            }
        }

        throw new InvalidOperationException($"Nowhere to stand beside {block}.");
    }
}
