using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Layout;

namespace Vocaterra.Game.Core.Tests.Land;

/// <summary>Setting up the camp with the camp kit (Y12, D-game-32): every cell is checked before anything changes, then the blocks are set, the kit is taken and the bedroll and the ring become sleep places.</summary>
public sealed class CampPlacementTests
{
    private static string Digest(VoxelSession session) => session.Scene.Grid.ComputeDigestHex();

    private static IReadOnlyList<WorldEvent> Named(VoxelSession session, string name) => session.Events.OfType<WorldEvent>().Where(e => e.Name == name).ToList();

    private static BlockPos GroundAt(VoxelSession session, int x, int z) => new(x, CampPlan.GroundOf(session.Scene.Grid, x, z), z);

    private static (string Grid, int Kits, int Places, int Events, int Hints) Snapshot(VoxelSession session) =>
        (Digest(session), session.Simulation.Inventory.Count(ItemId.CampKit), session.SleepPlaces.MadeInPlay.Count, Named(session, WorldEvents.CampPlaced).Count, session.PeekHints().Count);

    [Fact]
    public void Given_OwnedLevelFreeGround_When_TheKitIsUsed_Then_EveryBlockStandsTheKitIsTakenAndTwoPlacesAndTheEventAppear()
    {
        var session = CampRig.Owner();
        var (x, z) = CampRig.Spot(session, Direction.North);
        var expected = CampRig.PlanAt(session, x, z, Direction.North);

        var result = session.UseCampKit(GroundAt(session, x, z), Direction.North);

        Assert.Equal(VoxelSession.CampResult.Placed, result);
        var camp = session.Camp!;
        Assert.NotEmpty(camp.Blocks);
        Assert.All(camp.Blocks, b => Assert.Equal(b.Block, CampRig.BlockAt(session, b.Cell)));
        Assert.Equal(expected.Blocks.Select(static b => (b.Cell, b.Block)), camp.Blocks.Select(static b => (b.Cell, b.Block)));
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.CampKit));
        Assert.Equal([VoxelSession.CampBedPlace, VoxelSession.CampFirePlace], session.SleepPlaces.MadeInPlay.Select(static p => p.Id));
        Assert.Equal(camp.Bed, session.SleepPlaces.Find(VoxelSession.CampBedPlace)!.Cell);
        Assert.Equal(camp.Fire, session.SleepPlaces.Find(VoxelSession.CampFirePlace)!.Cell);
        Assert.Equal(camp.Wake, session.SleepPlaces.Find(VoxelSession.CampBedPlace)!.Wake);
        var raised = Assert.Single(Named(session, WorldEvents.CampPlaced));
        Assert.Equal($"{camp.Fire.X},{camp.Fire.Y},{camp.Fire.Z}", raised.Detail);
        Assert.Contains("Camp set up", Assert.Single(session.PeekHints()), StringComparison.Ordinal);
        Assert.Equal("camp placed", AutosavePolicy.ReasonOf(raised));
        Assert.Contains(camp.Fire, session.Fires.Middles); // the rings were scanned again
    }

    [Fact]
    public void Given_ThePlacedCamp_When_ItsShapeIsRead_Then_ItIsTheTentTheBedrollAndTheRingWithTheWakeCellFreeBetween()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);

        Assert.Equal(BlockId.Bedroll, CampRig.BlockAt(session, camp.Bed));
        Assert.Contains(new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }, d => CampRig.BlockAt(session, new BlockPos(camp.Bed.X + d.Item1, camp.Bed.Y, camp.Bed.Z + d.Item2)) == BlockId.BedrollFoot);
        Assert.Equal(8, camp.RingStones.Count);
        Assert.All(camp.RingStones, s => Assert.Equal(BlockId.FireRing, CampRig.BlockAt(session, s)));
        Assert.Equal(BlockId.Air, CampRig.BlockAt(session, camp.Fire));
        Assert.Equal(BlockId.Air, CampRig.BlockAt(session, camp.Wake));
        Assert.Equal(BlockId.Air, CampRig.BlockAt(session, camp.Wake with { Y = camp.Wake.Y + 1 }));
        Assert.Equal(5, Math.Abs(camp.Fire.Z - camp.Bed.Z)); // drawn north: the ring's middle on row 1, the bedroll's head on row 6
        Assert.True(camp.Width <= 5 && camp.Depth <= 8, "the camp fits a 10 by 10 plot with room to walk round");
        Assert.All(camp.Blocks, b => Assert.True(session.IsOwned(b.Cell.X, b.Cell.Z)));
    }

    [Theory]
    [InlineData(Direction.North)]
    [InlineData(Direction.East)]
    [InlineData(Direction.South)]
    [InlineData(Direction.West)]
    public void Given_EachSide_When_TheCampIsPlaced_Then_ItStandsTurnedWithTheRingBetweenTheSideAndTheTent(Direction facing)
    {
        var session = CampRig.Owner();

        var camp = CampRig.Place(session, facing);

        var toRing = facing switch
        {
            Direction.North => (0, -1),
            Direction.South => (0, 1),
            Direction.East => (1, 0),
            _ => (-1, 0),
        };
        var (dx, dz) = (camp.Bed.X - camp.Fire.X, camp.Bed.Z - camp.Fire.Z);
        Assert.True((dx * toRing.Item1) + (dz * toRing.Item2) < 0, "the tent lies away from the side the camp faces, beyond the ring");
        Assert.Equal(facing is Direction.North or Direction.South ? 5 : 8, camp.Width);
        Assert.Equal(VoxelSession.CampResult.NotApplied, session.UseCampKit(GroundAt(session, camp.Fire.X, camp.Fire.Z), facing)); // the kit went
    }

    [Fact]
    public void Given_ThePlayerLooksEast_When_TheKitIsUsedWithoutASide_Then_TheCampFacesWestTowardsThePlayer()
    {
        var session = CampRig.Owner();
        var (x, z) = CampRig.Spot(session, Direction.West);
        session.Step([VoxelCommand.Look(Angles90, 0)]);

        var result = session.UseCampKit(GroundAt(session, x, z));

        Assert.Equal(VoxelSession.CampResult.Placed, result);
        Assert.Equal(Direction.West, session.Camp!.Facing);
    }

    private const int Angles90 = 1024; // a quarter turn of the yaw: east (yaw 0 is north)

    [Fact]
    public void Given_AColumnOutsideThePlot_When_TheKitIsUsed_Then_ItIsRefusedWithTheLandHintAndNothingChanges()
    {
        var session = CampRig.Owner();
        var plot = session.OwnedPlot!.Value;
        var before = Snapshot(session);

        var result = session.UseCampKit(GroundAt(session, plot.X - 6, plot.Z + 4), Direction.North);

        Assert.Equal(VoxelSession.CampResult.Refused, result);
        Assert.Equal("Put your camp on your own land.", Assert.Single(session.PeekHints()));
        Assert.Equal(before with { Hints = 1 }, Snapshot(session));
    }

    [Fact]
    public void Given_APlotThatStraddlesItsEdge_When_TheKitIsUsedNearTheBorder_Then_TheWholeFootprintMustBeOwned()
    {
        var session = CampRig.Owner();
        var plot = session.OwnedPlot!.Value;
        var before = Snapshot(session);

        var result = session.UseCampKit(GroundAt(session, plot.X, plot.Z), Direction.North); // the ring's middle on the plot's corner: the rest hangs outside

        Assert.Equal(VoxelSession.CampResult.Refused, result);
        Assert.Equal("Put your camp on your own land.", Assert.Single(session.PeekHints()));
        Assert.Equal(before with { Hints = 1 }, Snapshot(session));
    }

    [Fact]
    public void Given_NoLandAtAll_When_TheKitIsUsed_Then_ItIsRefusedAsNotTheirLand()
    {
        var session = TestGame.NewSession(LandTestScene.Seed);
        _ = session.Simulation.GrantItems(ItemId.CampKit, 1);
        var (x, z) = LandTestScene.NorthPlot;
        var before = Digest(session);

        var result = session.UseCampKit(GroundAt(session, x + 5, z + 5), Direction.North);

        Assert.Equal(VoxelSession.CampResult.Refused, result);
        Assert.Equal("Put your camp on your own land.", Assert.Single(session.PeekHints()));
        Assert.Equal(before, Digest(session));
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.CampKit));
    }

    [Fact]
    public void Given_AHoleInTheGroundUnderTheCamp_When_TheKitIsUsed_Then_ItIsRefusedAsNotLevelAndNothingChanges()
    {
        var session = CampRig.Owner();
        var (x, z) = CampRig.Spot(session, Direction.North);
        var plan = CampRig.PlanAt(session, x, z, Direction.North);
        session.Scene.Grid.SetBlock(plan.CornerX + 1, plan.GroundY, plan.CornerZ + 6, BlockId.Air); // a hole in the ground under the tent
        var before = Snapshot(session);

        var result = session.UseCampKit(GroundAt(session, x, z), Direction.North);

        Assert.Equal(VoxelSession.CampResult.Refused, result);
        Assert.Equal("The ground is not level enough here.", Assert.Single(session.PeekHints()));
        Assert.Equal(before with { Hints = 1 }, Snapshot(session));
    }

    [Fact]
    public void Given_APlankInTheCampsWay_When_TheKitIsUsed_Then_ItIsRefusedAsBlockedAndNothingChanges()
    {
        var session = CampRig.Owner();
        var (x, z) = CampRig.Spot(session, Direction.North);
        var plan = CampRig.PlanAt(session, x, z, Direction.North);
        session.Scene.Grid.SetBlock(plan.Wake.X, plan.Wake.Y, plan.Wake.Z, BlockId.Planks);
        var before = Snapshot(session);

        var result = session.UseCampKit(GroundAt(session, x, z), Direction.North);

        Assert.Equal(VoxelSession.CampResult.Refused, result);
        Assert.Equal("Something is in the way.", Assert.Single(session.PeekHints()));
        Assert.Equal(before with { Hints = 1 }, Snapshot(session));
    }

    [Fact]
    public void Given_APlantedBushInTheCampsWay_When_TheKitIsUsed_Then_TheBushIsKeptAndTheCampRefused()
    {
        var session = CampRig.Owner();
        var (x, z) = CampRig.Spot(session, Direction.North);
        var plan = CampRig.PlanAt(session, x, z, Direction.North);
        session.Scene.Grid.SetBlock(plan.Bed.X + 1, plan.Bed.Y, plan.Bed.Z, BlockId.Bush);

        var result = session.UseCampKit(GroundAt(session, x, z), Direction.North);

        Assert.Equal(VoxelSession.CampResult.Refused, result);
        Assert.Equal(BlockId.Bush, CampRig.BlockAt(session, new BlockPos(plan.Bed.X + 1, plan.Bed.Y, plan.Bed.Z)));
    }

    [Fact]
    public void Given_TallGrassInTheCampsWay_When_TheKitIsUsed_Then_TheGrassIsClearedAndTheCampStands()
    {
        var session = CampRig.Owner();
        var (x, z) = CampRig.Spot(session, Direction.North);
        var plan = CampRig.PlanAt(session, x, z, Direction.North);
        session.Scene.Grid.SetBlock(plan.Wake.X, plan.Wake.Y, plan.Wake.Z, BlockId.TallGrass); // undergrowth on the cell to wake on
        session.Scene.Grid.SetBlock(plan.Bed.X + 1, plan.Bed.Y, plan.Bed.Z, BlockId.Fern); // and one under the canvas

        var result = session.UseCampKit(GroundAt(session, x, z), Direction.North);

        Assert.Equal(VoxelSession.CampResult.Placed, result);
        Assert.Equal(BlockId.Air, CampRig.BlockAt(session, plan.Wake));
        Assert.Equal(BlockId.Air, CampRig.BlockAt(session, new BlockPos(plan.Bed.X + 1, plan.Bed.Y, plan.Bed.Z)));
    }

    [Fact]
    public void Given_ThePlayerInsideTheCampsFootprint_When_TheKitIsUsed_Then_ItIsRefusedAsNotClearAndNothingChanges()
    {
        var session = CampRig.Owner();
        var (x, z) = CampRig.Spot(session, Direction.North);
        var plan = CampRig.PlanAt(session, x, z, Direction.North);
        session.Simulation.TeleportTo(plan.Fire with { Z = plan.Fire.Z - 1 }); // on a stone's cell
        var before = Snapshot(session);

        var result = session.UseCampKit(GroundAt(session, x, z), Direction.North);

        Assert.Equal(VoxelSession.CampResult.Refused, result);
        Assert.Equal("Stand clear of the camp.", Assert.Single(session.PeekHints()));
        Assert.Equal(before with { Hints = 1 }, Snapshot(session));
    }

    [Fact]
    public void Given_AWallRoundTheCamp_When_TheKitIsUsed_Then_ItIsRefusedAsWithoutAWayOutAndNothingChanges()
    {
        var session = CampRig.Owner();
        var (x, z) = CampRig.Spot(session, Direction.North);
        var plan = CampRig.PlanAt(session, x, z, Direction.North);
        for (var wz = plan.CornerZ - 1; wz <= plan.CornerZ + plan.Depth; wz++)
        {
            for (var wx = plan.CornerX - 1; wx <= plan.CornerX + plan.Width; wx++)
            {
                if (!plan.Covers(wx, wz))
                {
                    for (var dy = 1; dy <= 9; dy++) // taller than the tent's ridge, which a body climbs as steps
                    {
                        session.Scene.Grid.SetBlock(wx, plan.GroundY + dy, wz, BlockId.Stone);
                    }
                }
            }
        }

        var before = Snapshot(session);

        var result = session.UseCampKit(GroundAt(session, x, z), Direction.North);

        Assert.Equal(VoxelSession.CampResult.Refused, result);
        Assert.Equal("There would be no way in or out of the camp there.", Assert.Single(session.PeekHints()));
        Assert.Equal(before with { Hints = 1 }, Snapshot(session));
    }

    [Fact]
    public void Given_NoKitInTheBag_When_TheKitIsUsed_Then_NothingIsApplied()
    {
        var session = CampRig.Owner(kit: false);
        var (x, z) = CampRig.Spot(session, Direction.North);
        var before = Snapshot(session);

        Assert.Equal(VoxelSession.CampResult.NotApplied, session.UseCampKit(GroundAt(session, x, z), Direction.North));

        Assert.Equal(before, Snapshot(session));
    }

    [Fact]
    public void Given_ACampStands_When_ASecondKitIsUsed_Then_ItIsRefusedAndTheFirstCampIsKept()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);
        _ = session.Simulation.GrantItems(ItemId.CampKit, 1);
        var digest = Digest(session);

        var result = session.UseCampKit(GroundAt(session, camp.CornerX + 1, camp.CornerZ + 1), Direction.South);

        Assert.Equal(VoxelSession.CampResult.Refused, result);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.CampKit));
        Assert.Equal(digest, Digest(session));
        Assert.Same(camp, session.Camp);
    }

    [Fact]
    public void Given_APlacedCamp_When_TheCampsBlocksAreAskedToTheLandRules_Then_OnlyTheCampsCellsAreCampBlocks()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);

        Assert.All(camp.Blocks, b => Assert.True(session.Land.IsCampBlock(b.Cell)));
        Assert.False(session.Land.IsCampBlock(camp.Wake));
        Assert.False(session.Land.IsCampBlock(camp.Fire));
        Assert.Same(session.Land, session.Simulation.BuildRules);
    }

    [Fact]
    public void Given_ThePlacedCamp_When_TheChecklistIsRead_Then_TheCampStepIsDoneAndTheFireStepWaitsForTheTorch()
    {
        var session = CampRig.Owner();
        Assert.False(session.ObjectiveStep("my-land.camp")!.Done);

        _ = CampRig.Place(session);
        session.Step([]);

        Assert.True(session.ObjectiveStep("my-land.camp")!.Done);
        Assert.False(session.ObjectiveStep("my-land.fire")!.Done);
    }

    [Fact]
    public void Given_AllFourSteps_When_TheStakeThePurchaseTheCampAndTheFireAreDone_Then_TheChecklistIsCompleteAndSleepIsNotHeldBack()
    {
        var session = CampRig.Owner();
        _ = session.Simulation.GrantItems(ItemId.Stake, 1);
        _ = session.HostEvent(WorldEvents.PlotBought); // the sale itself is Y11's
        _ = CampRig.Place(session);
        var land = session.Objectives.First(static o => o.Id == "my-land");
        Assert.False(land.Complete);

        CampRig.Light(session);
        session.Step([]);

        Assert.True(land.Complete);
        Assert.Equal(4, land.Steps.Count(static s => s.Done));
        Assert.False(land.Spec.EndsSession);
        Assert.True(session.SleepPlaces.AnyUsable);
    }
}
