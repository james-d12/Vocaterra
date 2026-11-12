using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Layout;

namespace Vocaterra.Game.Core.Tests.Land;

/// <summary>Living at the camp (Y12): the torch on the placed ring, sleeping at the bedroll and the ring, waking there after a death, planting on the plot, packing the camp back and the save.</summary>
public sealed class CampLifeTests
{
    private static Scene Pristine(ulong seed = LandTestScene.Seed) => TestGame.Route(seed);

    private static IReadOnlyList<WorldEvent> Named(VoxelSession session, string name) => session.Events.OfType<WorldEvent>().Where(e => e.Name == name).ToList();

    private static int Kits(VoxelSession session) => session.Simulation.Inventory.Count(ItemId.CampKit);

    private static void Sleep(VoxelSession session) => SaveGame.Sleep(session, SaveGame.Capture(session, Pristine()));

    private static VoxelSession Reload(VoxelSession session) => SaveGame.Load(SaveGame.Save(session, Pristine()), TestGame.Data, Pristine, credit: CreditRules.FullTyped, bumpSession: false);

    private static void StandAt(VoxelSession session, BlockPos cell) => session.Simulation.TeleportTo(cell);

    // Every empty slot holds a piece of wood: the bag has no room for the kit.
    private static void FillBag(VoxelSession session)
    {
        var bag = session.Simulation.Inventory;
        for (var i = 0; i < bag.Slots.Count; i++)
        {
            if (bag.Slots[i].IsEmpty)
            {
                bag.SetSlot(i, new ItemStack(ItemId.Wood, 1));
            }
        }
    }

    private static void Die(VoxelSession session)
    {
        session.SetNeed(NeedMeter.Thirst, 0);
        session.SetNeed(NeedMeter.Health, 0.25);
        for (var i = 0; i < 60; i++)
        {
            session.Step([]);
        }
    }

    // The ground cell of the first grass column of the plot outside the camp's footprint, one per call by the index (a bush each).
    private static IEnumerable<BlockPos> PlotGrass(VoxelSession session, CampPlan? camp)
    {
        var plot = session.OwnedPlot!.Value;
        for (var z = plot.Z; z < plot.Z + plot.Depth; z++)
        {
            for (var x = plot.X; x < plot.X + plot.Width; x++)
            {
                var y = CampPlan.GroundOf(session.Scene.Grid, x, z);
                if (y >= 0 && (camp is null || !camp.Covers(x, z)) && session.Scene.Grid.GetBlock(x, y, z) == BlockId.Grass && session.Scene.Grid.GetBlock(x, y + 1, z) is BlockId.Air or BlockId.TallGrass or BlockId.Fern or BlockId.YellowFlower or BlockId.WhiteFlower or BlockId.Reeds)
                {
                    yield return new BlockPos(x, y, z);
                }
            }
        }
    }

    [Fact]
    public void Given_ThePlacedRing_When_ATorchIsSetOnItsRim_Then_ItBurnsTheRingIsASleepPlaceAndLunasFireIsUntouched()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);
        var ring = session.SleepPlaces.Find(VoxelSession.CampFirePlace)!;
        Assert.False(session.SleepPlaces.IsUsable(ring));
        Assert.False(session.CampFireIsLit);

        CampRig.Light(session);

        Assert.True(session.CampFireIsLit);
        Assert.True(session.SleepPlaces.IsUsable(ring));
        Assert.True(session.Fires.IsLit(camp.Fire));
        Assert.Equal(VoxelSession.CampFireName, Assert.Single(Named(session, WorldEvents.TorchAtRing)).Detail);
        Assert.Equal(VoxelSession.CampFireName, Assert.Single(Named(session, WorldEvents.FireLit)).Detail);
        Assert.False(session.FireLit); // the latch the sound reads for Luna's ring
        Assert.Equal((camp.Fire.X, camp.Fire.Z), session.LitCampFire);
        Assert.True(session.ObjectiveStep("my-land.fire")!.Done);
        Assert.False(session.ObjectiveStep("camp.torch")!.Done); // Luna's checklist waits for her own ring
    }

    [Fact]
    public void Given_ALitRing_When_ASecondTorchIsSet_Then_TheFireIsNotLitAgain()
    {
        var session = CampRig.Owner();
        _ = CampRig.Place(session);
        CampRig.Light(session);
        _ = session.Simulation.GrantItems(ItemId.Torch, 1);
        var second = new BlockPos(session.Camp!.RingStones[2].X, session.Camp.RingStones[2].Y + 1, session.Camp.RingStones[2].Z);

        Assert.True(VoxelWalker.Place(session, ItemId.Torch, second));

        Assert.Single(Named(session, WorldEvents.FireLit));
        Assert.Equal(2, Named(session, WorldEvents.TorchAtRing).Count);
    }

    [Fact]
    public void Given_ALitRingAndThePlayerAtIt_When_TheyAreAtTheFireAndSleep_Then_TheNightPassesAndTheLastPlaceIsTheRing()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);
        CampRig.Light(session);
        StandAt(session, camp.Wake);
        Assert.Equal(VoxelSession.CampFirePlace, session.CurrentSleepPlace?.Id);
        Assert.True(session.CanSleep);
        var day = session.Day;

        Sleep(session);

        Assert.Equal(day + 1, session.Day);
        Assert.Equal(VoxelSession.CampFirePlace, session.LastSleepPlace?.Id);
        Assert.NotNull(session.Camp); // the night keeps the camp
        Assert.True(session.CampFireIsLit);
    }

    [Fact]
    public void Given_AnUnlitRing_When_ThePlayerStandsByItButNotAtTheBedroll_Then_NoPlaceIsUsableThere()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);

        StandAt(session, camp.Wake);

        Assert.Null(session.CurrentSleepPlace); // the ring is not lit, the bedroll is too far across
        Assert.False(session.AtSleepPlace);
    }

    [Fact]
    public void Given_TheBedroll_When_ThePlayerStandsOnItAndSleeps_Then_TheNightPassesWithTheRingUnlitAndTheConsoleNamesTheCamp()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);
        StandAt(session, camp.Bed);
        var place = session.CurrentSleepPlace;
        Assert.Equal(VoxelSession.CampBedPlace, place?.Id);
        Assert.Equal("at your camp", SleepOffer.EndedWhere(session, place, collapse: false));
        var day = session.Day;

        Sleep(session);

        Assert.Equal(day + 1, session.Day);
        Assert.Equal(new LastSleepSave(VoxelSession.CampBedPlace, camp.Bed.X, camp.Bed.Y, camp.Bed.Z), session.LastSleepPlace);
    }

    [Fact]
    public void Given_ANightAtTheBedroll_When_ThePlayerDiesLater_Then_TheyWakeOnTheCampsWakeCell()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);
        StandAt(session, camp.Bed);
        Sleep(session);
        session.Simulation.TeleportTo(new BlockPos(camp.CornerX - 4, camp.GroundY + 1, camp.CornerZ + 2)); // away from it

        Die(session);

        Assert.Equal(1, session.DeathCount);
        Assert.Equal(camp.Wake, session.Simulation.Player.FeetBlock);
    }

    [Fact]
    public void Given_ANightAtTheCampsRing_When_ThePlayerDiesLater_Then_TheyWakeOnTheCampsWakeCell()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);
        CampRig.Light(session);
        StandAt(session, camp.Wake);
        Sleep(session);
        session.Simulation.TeleportTo(new BlockPos(camp.CornerX - 4, camp.GroundY + 1, camp.CornerZ + 2));

        Die(session);

        Assert.Equal(1, session.DeathCount);
        Assert.Equal(camp.Wake, session.Simulation.Player.FeetBlock);
    }

    [Fact]
    public void Given_TheWakeCellIsTaken_When_ThePlayerDiesAfterANightAtTheCamp_Then_TheyWakeOnTheNearestFreeGroundByTheBedroll()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);
        StandAt(session, camp.Bed);
        Sleep(session);
        session.Scene.Grid.SetBlock(camp.Wake.X, camp.Wake.Y, camp.Wake.Z, BlockId.Planks); // something put on the cell
        session.Simulation.TeleportTo(new BlockPos(camp.CornerX - 4, camp.GroundY + 1, camp.CornerZ + 2));

        Die(session);

        Assert.Equal(1, session.DeathCount);
        var woke = session.Simulation.Player.FeetBlock;
        Assert.NotEqual(camp.Wake, woke);
        Assert.InRange(Math.Abs(woke.X - camp.Bed.X) + Math.Abs(woke.Z - camp.Bed.Z), 2, 10);
    }

    [Fact]
    public void Given_ThePlot_When_ABerryIsPlantedOnItsGrass_Then_ItIsPlantedAndANightGrowsIt()
    {
        var session = CampRig.Owner(kit: false);
        _ = session.Simulation.GrantItems(ItemId.Berry, 2);
        var ground = PlotGrass(session, null).First();

        var result = session.PlantOn(ground);

        Assert.Equal(VoxelSession.PlantResult.Planted, result);
        Assert.Equal(BlockId.Bush, CampRig.BlockAt(session, ground.Offset(BlockFace.Up)));
        Assert.Equal(1, session.PlantedAlive);
        Assert.Equal(1, session.PlantedAliveOn(onPlot: true));
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Berry));
    }

    [Fact]
    public void Given_AGroundOutsideThePlotAndLunasRadius_When_ABerryIsPlanted_Then_ItIsRefusedTooFar()
    {
        var session = CampRig.Owner(kit: false);
        _ = session.Simulation.GrantItems(ItemId.Berry, 1);
        var plot = session.OwnedPlot!.Value;
        var x = plot.X - 4;
        var z = plot.Z + 4;
        var ground = new BlockPos(x, CampPlan.GroundOf(session.Scene.Grid, x, z), z);
        Assert.False(session.IsOwned(x, z));

        var result = session.PlantOn(ground);

        Assert.Equal(VoxelSession.PlantResult.Refused, result);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Berry));
        Assert.Equal(0, session.PlantedAlive);
        Assert.Contains("on your own land", Assert.Single(session.PeekHints()), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_ThePlotHasItsMostBushes_When_AnotherIsPlantedThereAndAtLunasCamp_Then_ThePlotIsFullAndLunasCampStillTakesOne()
    {
        var session = CampRig.Owner(kit: false);
        var max = session.Data.Needs.Planting!.MaxBushes;
        _ = session.Simulation.GrantItems(ItemId.Berry, 8);
        var spots = PlotGrass(session, null).Take(max + 1).ToList();
        Assert.Equal(max + 1, spots.Count);
        foreach (var spot in spots.Take(max))
        {
            Assert.Equal(VoxelSession.PlantResult.Planted, session.PlantOn(spot));
        }

        Assert.Equal(VoxelSession.PlantResult.Refused, session.PlantOn(spots[max]));
        Assert.Contains("all the bushes", session.TakeHints().Last(), StringComparison.Ordinal);
        Assert.Equal(max, session.PlantedAliveOn(onPlot: true));

        var luna = session.Scene.Point(VoxelSession.CampFireRing);
        var near = Enumerable.Range(-5, 11).SelectMany(dx => Enumerable.Range(-5, 11).Select(dz => (X: luna.X + dx, Z: luna.Z + dz)))
            .Select(c => new BlockPos(c.X, CampPlan.GroundOf(session.Scene.Grid, c.X, c.Z), c.Z))
            .First(g => session.Scene.Grid.GetBlock(g.X, g.Y, g.Z) == BlockId.Grass && session.Scene.Grid.GetBlock(g.X, g.Y + 1, g.Z) == BlockId.Air && Math.Abs(g.X - luna.X) + Math.Abs(g.Z - luna.Z) is >= 3 and <= 6);
        Assert.Equal(VoxelSession.PlantResult.Planted, session.PlantOn(near));
        Assert.Equal(1, session.PlantedAliveOn(onPlot: false));
    }

    [Fact]
    public void Given_APlacedCamp_When_ABlockOfItIsHeldFor45Ticks_Then_TheWholeCampIsPackedIntoTheKit()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);
        var cells = camp.Blocks.Select(static b => b.Cell).ToList();
        var canvas = camp.Blocks.First(static b => b.Block == BlockId.Canvas).Cell;
        StandAt(session, camp.Wake);
        _ = session.TakeHints();

        Assert.True(VoxelWalker.Gather(session, canvas, VoxelSimulation.CampPackTicks + 5));

        Assert.All(cells, c => Assert.Equal(BlockId.Air, CampRig.BlockAt(session, c)));
        Assert.Equal(1, Kits(session));
        Assert.Null(session.Camp);
        Assert.Empty(session.SleepPlaces.MadeInPlay);
        Assert.False(session.Land.IsCampBlock(canvas));
        Assert.Contains("pack the camp up", Assert.Single(session.TakeHints()), StringComparison.Ordinal);
        Assert.DoesNotContain(camp.Fire, session.Fires.Middles);
    }

    [Fact]
    public void Given_APlacedCamp_When_ABlockIsHeldForLessThanTheTime_Then_NothingIsPacked()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);
        var canvas = camp.Blocks.First(static b => b.Block == BlockId.Canvas).Cell;
        StandAt(session, camp.Wake);

        Assert.False(VoxelWalker.Gather(session, canvas, VoxelSimulation.CampPackTicks - 5));

        Assert.NotNull(session.Camp);
        Assert.Equal(BlockId.Canvas, CampRig.BlockAt(session, canvas));
        Assert.Equal(0, Kits(session));
    }

    [Fact]
    public void Given_APlacedCamp_When_ItIsPackedByHoldingTheBedroll_Then_ItGoesAsWellAndTheKitCanBePlacedAgain()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);
        StandAt(session, camp.Wake);
        Assert.True(VoxelWalker.Gather(session, camp.Bed, VoxelSimulation.CampPackTicks + 5));
        Assert.Null(session.Camp);
        Assert.Equal(1, Kits(session));

        var again = CampRig.Place(session, Direction.East);

        Assert.NotNull(again);
        Assert.Equal(0, Kits(session));
        Assert.Equal(2, Named(session, WorldEvents.CampPlaced).Count);
    }

    [Fact]
    public void Given_ATorchOnTheRingAndABushBesideTheCamp_When_ItIsPacked_Then_TheTorchComesBackAndTheBushStays()
    {
        var session = CampRig.Owner();
        _ = session.Simulation.GrantItems(ItemId.Berry, 1);
        var camp = CampRig.Place(session);
        var spot = PlotGrass(session, camp).First(c => !camp.Covers(c.X, c.Z));
        Assert.Equal(VoxelSession.PlantResult.Planted, session.PlantOn(spot));
        CampRig.Light(session);
        var rim = CampRig.Rim(camp);
        Assert.Equal(BlockId.Torch, CampRig.BlockAt(session, rim));

        Assert.True(session.PackCamp());

        Assert.Equal(BlockId.Air, CampRig.BlockAt(session, rim));
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Torch));
        Assert.Equal(BlockId.Bush, CampRig.BlockAt(session, spot.Offset(BlockFace.Up)));
        Assert.Equal(1, session.PlantedAlive);
    }

    [Fact]
    public void Given_ABagWithNoRoom_When_ThePackingIsAsked_Then_ItIsRefusedWithALineAndNothingChanges()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);
        FillBag(session);

        Assert.False(session.Simulation.Inventory.CanAdd(ItemId.CampKit, 1));
        var digest = session.Scene.Grid.ComputeDigestHex();
        _ = session.TakeHints();

        Assert.False(session.PackCamp());

        Assert.Equal("Your bag is full. Make room for the camp kit to pack the camp up.", Assert.Single(session.TakeHints()));
        Assert.Equal(digest, session.Scene.Grid.ComputeDigestHex());
        Assert.Same(camp, session.Camp);
        Assert.Equal(2, session.SleepPlaces.MadeInPlay.Count);
        Assert.Equal(0, Kits(session));
    }

    [Fact]
    public void Given_TheLastNightWasAtTheCamp_When_ItIsPackedWithLodgingGiven_Then_TheLastPlaceIsTheInnsBed()
    {
        var session = CampRig.Owner();
        session.GrantLodging();
        var camp = CampRig.Place(session);
        StandAt(session, camp.Bed);
        Sleep(session);
        Assert.Equal(VoxelSession.CampBedPlace, session.LastSleepPlace?.Id);

        Assert.True(session.PackCamp());

        Assert.Equal("inn.bed", session.LastSleepPlace?.Id);
        Die(session);
        Assert.Equal(1, session.DeathCount);
        var bed = session.SleepPlaces.Find("inn.bed")!.Cell;
        Assert.InRange(Math.Abs(session.Simulation.Player.FeetBlock.X - bed.X) + Math.Abs(session.Simulation.Player.FeetBlock.Z - bed.Z), 0, 8); // woke in the guest room
    }

    [Fact]
    public void Given_TheLastNightWasAtTheCamp_When_ItIsPackedWithNoLodging_Then_ThereIsNoLastPlaceAndADeathWakesAtLunasFire()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);
        StandAt(session, camp.Bed);
        Sleep(session);

        Assert.True(session.PackCamp());

        Assert.Null(session.LastSleepPlace);
        Die(session);
        var luna = session.Scene.Point(VoxelSession.CampFireRing);
        var woke = session.Simulation.Player.FeetBlock;
        Assert.InRange(Math.Abs(woke.X - luna.X) + Math.Abs(woke.Z - luna.Z), 2, 10);
    }

    [Fact]
    public void Given_ALitCamp_When_TheGameIsSavedAndLoaded_Then_TheCampItsPlacesAndItsStepsComeBackWithoutRaisingAnythingAgain()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);
        CampRig.Light(session);
        session.Step([]);
        var text = SaveGame.Save(session, Pristine());

        var loaded = SaveGame.Load(text, TestGame.Data, Pristine, credit: CreditRules.FullTyped, bumpSession: false);

        var back = loaded.Camp!;
        Assert.Equal((camp.CornerX, camp.CornerZ, camp.GroundY, camp.Facing), (back.CornerX, back.CornerZ, back.GroundY, back.Facing));
        Assert.Equal(camp.Blocks.Select(static b => (b.Cell, b.Block)), back.Blocks.Select(static b => (b.Cell, b.Block)));
        Assert.Equal([VoxelSession.CampBedPlace, VoxelSession.CampFirePlace], loaded.SleepPlaces.MadeInPlay.Select(static p => p.Id));
        Assert.Equal(camp.Wake, loaded.SleepPlaces.Find(VoxelSession.CampBedPlace)!.Wake);
        Assert.True(loaded.CampFireIsLit);
        Assert.True(loaded.SleepPlaces.IsUsable(loaded.SleepPlaces.Find(VoxelSession.CampFirePlace)!));
        Assert.True(loaded.ObjectiveStep("my-land.camp")!.Done); // restored from the save's camp
        Assert.True(loaded.ObjectiveStep("my-land.fire")!.Done); // restored from the ring in the world
        Assert.Empty(Named(loaded, WorldEvents.CampPlaced));
        Assert.Equal(0, Kits(loaded));
        Assert.True(loaded.Land.IsCampBlock(camp.Blocks[0].Cell)); // packing still works after a load
    }

    [Fact]
    public void Given_APlacedCamp_When_TheSaveIsRead_Then_SystemsSleepHoldsTheCampsIdentityAndBothPlacesWithTheirCells()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session, Direction.East);

        using var json = JsonDocument.Parse(SaveGame.Save(session, Pristine()));

        var sleep = json.RootElement.GetProperty("systems").GetProperty("sleep");
        var saved = sleep.GetProperty("camp");
        Assert.Equal((camp.CornerX, camp.CornerZ, camp.GroundY, "east"), (saved.GetProperty("x").GetInt32(), saved.GetProperty("z").GetInt32(), saved.GetProperty("ground").GetInt32(), saved.GetProperty("facing").GetString()));
        var places = sleep.GetProperty("places").EnumerateArray().ToList();
        Assert.Equal([VoxelSession.CampBedPlace, VoxelSession.CampFirePlace], places.Select(static p => p.GetProperty("id").GetString()));
        Assert.Equal(["bed", "fire"], places.Select(static p => p.GetProperty("kind").GetString()));
        Assert.Equal((camp.Bed.X, camp.Bed.Y, camp.Bed.Z), (places[0].GetProperty("x").GetInt32(), places[0].GetProperty("y").GetInt32(), places[0].GetProperty("z").GetInt32()));
        Assert.Equal(camp.Wake.X, places[0].GetProperty("wake").GetProperty("x").GetInt32());
        Assert.False(sleep.TryGetProperty("kitOwed", out _));
    }

    [Fact]
    public void Given_NoCamp_When_TheGameIsSaved_Then_SystemsSleepHasNoCampEntry()
    {
        var session = CampRig.Owner();

        Assert.DoesNotContain("\"camp\"", SaveGame.Save(session, Pristine()), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_ACampWhoseBlockIsGoneBeforeTheLoad_When_TheGameLoads_Then_ItsPlacesAreDroppedAndTheKitComesBackOnce()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);
        CampRig.Light(session);
        var canvas = camp.Blocks.First(static b => b.Block == BlockId.Canvas).Cell;
        session.Scene.Grid.SetBlock(canvas.X, canvas.Y, canvas.Z, BlockId.Air); // the world is edited before the save is loaded
        session.Step([]);

        var loaded = Reload(session);

        Assert.Null(loaded.Camp);
        Assert.Empty(loaded.SleepPlaces.MadeInPlay);
        Assert.Equal(1, Kits(loaded));
        Assert.False(loaded.Land.IsCampBlock(camp.Blocks[0].Cell));
        Assert.False(loaded.ObjectiveStep("my-land.camp")!.Done);
        var again = Reload(loaded); // the kit is in the bag now: a second load gives no second one
        Assert.Equal(1, Kits(again));
        Assert.Null(again.Camp);
        Assert.DoesNotContain("kitOwed", SaveGame.Save(again, Pristine()), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_ALostCampAndAFullBag_When_TheGameLoadsAndRoomIsMade_Then_TheKitComesWhenThereIsRoomAndOnlyOnce()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);
        var canvas = camp.Blocks.First(static b => b.Block == BlockId.Canvas).Cell;
        session.Scene.Grid.SetBlock(canvas.X, canvas.Y, canvas.Z, BlockId.Air);
        FillBag(session);

        var loaded = Reload(session);

        Assert.Equal(0, Kits(loaded)); // no room yet
        Assert.Null(loaded.Camp);
        var owed = Reload(loaded); // the debt is saved
        Assert.Equal(0, Kits(owed));
        var bag = owed.Simulation.Inventory;
        Assert.True(bag.RemoveFromSlot(bag.Slots.Count - 1, bag.Slots[^1].Count)); // room is made
        owed.Step([]);

        Assert.Equal(1, Kits(owed));
        owed.Step([]);
        Assert.Equal(1, Kits(owed));
    }

    [Fact]
    public void Given_ASleepAtTheCamp_When_ItsBlocksAreGoneAtTheNextNight_Then_TheNightDropsThePlacesAndGivesTheKitBack()
    {
        var session = CampRig.Owner();
        var camp = CampRig.Place(session);
        StandAt(session, camp.Bed);
        var canvas = camp.Blocks.First(static b => b.Block == BlockId.Canvas).Cell;
        session.Scene.Grid.SetBlock(canvas.X, canvas.Y, canvas.Z, BlockId.Air);

        Sleep(session);

        Assert.Null(session.Camp);
        Assert.Equal(1, Kits(session));
        Assert.Empty(session.SleepPlaces.MadeInPlay);
    }

    [Fact]
    public void Given_EveryStampOfTheScene_When_TheyAreTurnedToEachSide_Then_TheCellsAreThoseTheScenesOldRotationGave()
    {
        var layout = SceneLayout.Load(Repo.Path("content", "game-v1", "game", "scene.json"));
        Assert.NotEmpty(layout.Stamps);

        foreach (var stamp in layout.Stamps.Values)
        {
            foreach (var facing in Enum.GetValues<Direction>())
            {
                var width = stamp.Layers.Max(static layer => layer.Count == 0 ? 0 : layer.Max(static row => row.Length));
                var depth = stamp.Layers.Max(static layer => layer.Count);
                var expected = new List<(int, int, int, BlockId)>();
                for (var layer = 0; layer < stamp.Layers.Count; layer++)
                {
                    for (var r = 0; r < stamp.Layers[layer].Count; r++)
                    {
                        for (var c = 0; c < stamp.Layers[layer][r].Length; c++)
                        {
                            if (stamp.Layers[layer][r][c] == ' ')
                            {
                                continue;
                            }

                            var (dx, dz) = facing switch // the scene builder's rotation as it was before the helper took it over
                            {
                                Direction.North => (c, r),
                                Direction.East => (depth - 1 - r, c),
                                Direction.South => (width - 1 - c, depth - 1 - r),
                                _ => (r, width - 1 - c),
                            };
                            expected.Add((dx, dz, layer, stamp.Legend[stamp.Layers[layer][r][c]]));
                        }
                    }
                }

                Assert.Equal(expected, stamp.Cells(facing).ToList());
                Assert.Equal(facing is Direction.East or Direction.West ? depth : width, stamp.WidthFacing(facing));
                Assert.Equal(facing is Direction.East or Direction.West ? width : depth, stamp.DepthFacing(facing));
            }
        }
    }

    [Fact]
    public void Given_TheTentStamp_When_ItIsComparedWithTheCampStamp_Then_TheCampHoldsTheTentsCanvasRowsAsDrawn()
    {
        var layout = SceneLayout.Load(Repo.Path("content", "game-v1", "game", "scene.json"));
        var tent = layout.Stamps["tent"];
        var camp = layout.Stamps[CampPlan.StampName];

        var tentCells = tent.Cells(Direction.North).Select(static c => (c.Dx, c.Dz, c.Layer, c.Block)).ToList();
        var campCanvas = camp.Cells(Direction.North).Where(static c => c.Block == BlockId.Canvas).Select(c => (c.Dx, c.Dz - 4, c.Layer, c.Block)).ToList();

        Assert.Equal(tentCells.OrderBy(static c => c).ToList(), campCanvas.OrderBy(static c => c).ToList());
    }

    [Fact]
    public void Given_TheShippedContent_When_ItIsValidated_Then_TheCampStampHasNoProblem()
    {
        Assert.DoesNotContain(GameDataValidator.Validate(TestGame.Data), static p => p.Contains("'camp'", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("\"wake\": [2, 3]", "\"waker\": [2, 3]", "has no anchor 'wake'")]
    [InlineData("\"wake\": [2, 3]", "\"wake\": [0, 4]", "'wake' anchor free")]
    [InlineData("\"bed\": [2, 6]", "\"bed\": [2, 7]", "'bed' anchor on a bedroll")]
    [InlineData("\"fire\": [2, 1]", "\"fire\": [2, 2]", "'fire' anchor on an empty cell")]
    public void Given_ABrokenCampStamp_When_TheContentIsValidated_Then_TheProblemNamesTheAnchor(string from, string to, string expected)
    {
        var text = File.ReadAllText(Repo.Path("content", "game-v1", "game", "scene.json"));
        Assert.Contains(from, text, StringComparison.Ordinal);

        var problems = GameDataValidator.Validate(TestGame.Data.WithScene(SceneLayout.Parse(text.Replace(from, to, StringComparison.Ordinal)))).Where(static p => p.Contains("'camp'", StringComparison.Ordinal)).ToList();

        Assert.Contains(problems, p => p.Contains(expected, StringComparison.Ordinal));
    }

    [Fact]
    public void Given_AnEmptyStamp_When_ItIsParsedWithAnAnchorOutsideTheDrawing_Then_TheLayoutIsRefused()
    {
        var text = File.ReadAllText(Repo.Path("content", "game-v1", "game", "scene.json"));

        Assert.Throws<SceneLayoutException>(() => SceneLayout.Parse(text.Replace("\"wake\": [2, 3]", "\"wake\": [2, 30]", StringComparison.Ordinal)));
    }
}
