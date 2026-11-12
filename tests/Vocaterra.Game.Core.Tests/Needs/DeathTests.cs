using System.Text.Json;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Needs;

/// <summary>Health in play, death, the belongings chest and the no-dead-ends rule (N04, adaptation design 9.4 and 9.7), through the session.</summary>
public sealed class DeathTests
{
    private const int Tps = 60;

    private static Scene Pristine() => TestGame.Route(TestGame.Seed);

    private static double Meter(VoxelSession session, NeedMeter meter) => session.Needs.Value(meter);

    private static void Tick(VoxelSession session, int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            session.Step([]);
        }
    }

    private static List<WorldEvent> Named(VoxelSession session, string name) => session.Events.OfType<WorldEvent>().Where(e => e.Name == name).ToList();

    // A session with an empty bag but for what the test puts in, and the drives and health as given.
    private static VoxelSession Playing(double hunger = 100, double thirst = 100, double health = 100)
    {
        var session = TestGame.NewSession();
        for (var slot = 0; slot < Inventory.SlotCount; slot++)
        {
            session.Simulation.Inventory.SetSlot(slot, ItemStack.Empty);
        }

        session.SetNeed(NeedMeter.Hunger, hunger);
        session.SetNeed(NeedMeter.Thirst, thirst);
        session.SetNeed(NeedMeter.Health, health);
        return session;
    }

    private static VoxelSession Dying(Action<VoxelSession>? beforeDeath = null)
    {
        var session = Playing(thirst: 0, health: 0.25);
        session.Simulation.Inventory.Add(ItemId.Wood, 12);
        session.Simulation.Inventory.Add(ItemId.Berry, 3);
        session.Simulation.Inventory.Add(ItemId.Book, 1);
        session.Simulation.Inventory.Add(ItemId.Axe, 1);
        beforeDeath?.Invoke(session);
        Tick(session, Tps); // a quarter of a point a second at thirst's last stage
        return session;
    }

    [Theory]
    [InlineData(0, 100, 360, 99)] // hunger's last stage: 1 every 6 s
    [InlineData(100, 0, 240, 99)] // thirst's: 1 every 4 s
    [InlineData(0, 0, 720, 95)] // both add: 5 in 12 s
    public void HealthFallsAtTheRatesOfTheLastStagesAndTheyAdd(double hunger, double thirst, int ticks, double expected)
    {
        var session = Playing(hunger: hunger, thirst: thirst);

        Tick(session, ticks);

        Assert.Equal(expected, Meter(session, NeedMeter.Health), 6);
    }

    [Fact]
    public void HealthRecoversOnePointEveryTwelveSecondsWhileNoDriveIsAtStageThreeOrFour()
    {
        var session = Playing(health: 50);

        Tick(session, 12 * Tps * 3);

        Assert.Equal(53, Meter(session, NeedMeter.Health), 6);
    }

    [Theory]
    [InlineData(20, 100)] // thirst at stage 3
    [InlineData(100, 20)] // hunger at stage 3
    public void HealthDoesNotRecoverWithADriveAtStageThree(double thirst, double hunger)
    {
        var session = Playing(hunger: hunger, thirst: thirst, health: 50);

        Tick(session, 12 * Tps);

        Assert.Equal(50, Meter(session, NeedMeter.Health), 6);
    }

    [Fact]
    public void HealthRecoversTwiceAsFastWhileResting()
    {
        var needs = new PlayerNeeds(TestGame.Data.Needs);
        needs.Set(NeedMeter.Health, 50);

        for (var i = 0; i < 12 * Tps; i++)
        {
            needs.Step(1, NeedActivity.Resting);
        }

        Assert.Equal(52, needs.Value(NeedMeter.Health), 6);
    }

    [Fact]
    public void NothingHappensToAPlayerWithHealthLeft()
    {
        var session = Playing(thirst: 0, health: 0.25);

        Tick(session, Tps - 1);

        Assert.Empty(Named(session, WorldEvents.PlayerDied));
        Assert.True(Meter(session, NeedMeter.Health) > 0);
    }

    [Fact]
    public void ThePlayerDiesAtExactlyZeroHealthAndWakesAtTheFireWithTheWakingValues()
    {
        var session = Dying();

        Assert.Equal(1, session.DeathCount);
        foreach (var meter in new[] { NeedMeter.Health, NeedMeter.Hunger, NeedMeter.Thirst, NeedMeter.Sleep })
        {
            Assert.Equal(50, Meter(session, meter));
        }

        Assert.Equal(session.Needs.Effects().StaminaMaximum, Meter(session, NeedMeter.Stamina));
        var ring = session.Scene.Points[VoxelSession.CampFireRing];
        var feet = session.Simulation.Player.FeetBlock;
        var apart = Math.Sqrt((((long)ring.X - feet.X) * ((long)ring.X - feet.X)) + (((long)ring.Z - feet.Z) * ((long)ring.Z - feet.Z)));
        Assert.InRange(apart, 2, 5);
        Assert.True(SleepPlaces.IsWithinReach(session.SleepPlaces.WakeAt!, feet)); // Y01: the fire's reach, whether or not its torch is on (AtSleepPlace asks for a usable place)
        Assert.True(VoxelWalker.IsStandable(session.Scene.Grid, feet));
        Assert.Equal(0, session.Needs.ExhaustionSeconds);
    }

    [Fact]
    public void Given_ABedOnTheSpotThePlayerWouldWakeAt_When_ThePlayerDies_Then_TheWakeIsSomewhereElse()
    {
        // Y03: a bed is not free ground (the search takes air with a solid floor), so the wake moves to the nearest other cell.
        var wake = Dying().Simulation.Player.FeetBlock;

        var session = Dying(s => s.Scene.Grid.SetBlock(wake.X, wake.Y, wake.Z, BlockId.Bed));

        Assert.Equal(BlockId.Bed, session.Scene.Grid.GetBlock(wake.X, wake.Y, wake.Z));
        var feet = session.Simulation.Player.FeetBlock;
        Assert.NotEqual(wake, feet);
        Assert.True(VoxelWalker.IsStandable(session.Scene.Grid, feet));
        Assert.True(SleepPlaces.IsWithinReach(session.SleepPlaces.WakeAt!, feet));
    }

    [Theory]
    [InlineData(0, 100, "sed")]
    [InlineData(100, 0, "hambre")]
    [InlineData(0, 0, "sed")]
    public void TheDeathNamesTheDriveThatKilled(double thirst, double hunger, string cause)
    {
        var session = Playing(hunger: hunger, thirst: thirst, health: 0.1);

        Tick(session, 2 * Tps);

        var died = Assert.Single(Named(session, WorldEvents.PlayerDied));
        Assert.Equal(cause, died.Detail);
        Assert.Equal("death", Assert.Single(Named(session, WorldEvents.PlayerWoke)).Detail);
        Assert.Empty(Named(session, WorldEvents.Slept));
        Assert.Empty(Named(session, WorldEvents.PlayerCollapsed));
    }

    [Fact]
    public void ADeathIsNotANight()
    {
        var session = Dying();

        Assert.Equal(1, session.Day);
        Assert.Equal(1, session.Sitting);
        Assert.Equal(0, session.DayResetCount);
    }

    [Fact]
    public void NoOneDiesWhilePlayIsPaused()
    {
        var session = Playing(thirst: 0, health: 0.25);
        session.PlayPauses = PlayPause.Conversation;

        Tick(session, 5 * Tps);

        Assert.Equal(0, session.DeathCount);
        Assert.Equal(0.25, Meter(session, NeedMeter.Health), 6);
        session.PlayPauses = PlayPause.None;
        Tick(session, Tps);
        Assert.Equal(1, session.DeathCount);
    }

    [Fact]
    public void NoOneDiesWithTheNeedsOff()
    {
        var session = Playing(thirst: 0, health: 0.25);
        session.NeedsEnabled = false;

        Tick(session, 5 * Tps);

        Assert.Equal(0, session.DeathCount);
    }

    [Fact]
    public void TheChestHoldsTheBagMinusTheItemsThatStackToOneOnTheNearestFreeGround()
    {
        var before = Playing(thirst: 0, health: 0.25).Simulation.Player.FeetBlock;
        var session = Dying();

        var chest = session.Chest!;
        Assert.Equal([(ItemId.Wood, 12), (ItemId.Berry, 3)], chest.Items);
        Assert.Equal(BlockId.Chest, session.Scene.Grid.GetBlock(chest.Cell.X, chest.Cell.Y, chest.Cell.Z));
        Assert.True(session.Scene.Grid.IsSolid(chest.Cell.X, chest.Cell.Y - 1, chest.Cell.Z));
        Assert.Equal(BlockId.Air, session.Scene.Grid.GetBlock(chest.Cell.X, chest.Cell.Y + 1, chest.Cell.Z));
        Assert.InRange(Math.Abs(chest.Cell.X - before.X) + Math.Abs(chest.Cell.Z - before.Z), 0, 3);
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Wood));
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Book));
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Axe));
    }

    [Fact]
    public void ABagWithNothingToLeaveLeavesNoChest()
    {
        var session = Playing(thirst: 0, health: 0.25);
        session.Simulation.Inventory.Add(ItemId.Book, 1);

        Tick(session, Tps);

        Assert.Equal(1, session.DeathCount);
        Assert.Null(session.Chest);
    }

    [Fact]
    public void ASecondDeathMovesTheFirstChestIntoTheNew()
    {
        var session = Dying();
        var first = session.Chest!.Cell;
        session.Simulation.Inventory.Add(ItemId.Coin, 5);
        session.SetNeed(NeedMeter.Thirst, 0);
        session.SetNeed(NeedMeter.Health, 0.25);

        Tick(session, Tps);

        Assert.Equal(2, session.DeathCount);
        var chest = session.Chest!;
        Assert.Equal(20, chest.Total);
        Assert.Equal(BlockId.Chest, session.Scene.Grid.GetBlock(chest.Cell.X, chest.Cell.Y, chest.Cell.Z));
        Assert.Equal(chest.Cell == first ? BlockId.Chest : BlockId.Air, session.Scene.Grid.GetBlock(first.X, first.Y, first.Z));
    }

    [Fact]
    public void TakingBackTakesWhatTheBagCanHoldAndLeavesTheRest()
    {
        var session = Dying();
        var bag = session.Simulation.Inventory;
        for (var slot = 0; slot < Inventory.SlotCount - 1; slot++)
        {
            if (bag.Slots[slot].IsEmpty)
            {
                bag.SetSlot(slot, new ItemStack(ItemId.Coin, 99));
            }
        }

        var chest = session.Chest!;
        AimAtChest(session);
        var result = session.TakeBelongings();

        Assert.Equal(NeedActionResult.Done, result);
        Assert.Equal(12, bag.Count(ItemId.Wood)); // the one free slot took the wood
        Assert.Equal([(ItemId.Berry, 3)], chest.Items);
        Assert.Equal(BlockId.Chest, session.Scene.Grid.GetBlock(chest.Cell.X, chest.Cell.Y, chest.Cell.Z));
        Assert.Equal("some", Assert.Single(Named(session, WorldEvents.BelongingsRecovered)).Detail);
        Assert.Equal(NeedActionResult.Kept, session.TakeBelongings()); // nothing more fits
    }

    [Fact]
    public void TakingBackEverythingRemovesTheChest()
    {
        var session = Dying();
        var chest = session.Chest!;
        AimAtChest(session);

        Assert.Equal(NeedActionResult.Done, session.TakeBelongings());

        Assert.Null(session.Chest);
        Assert.Equal(BlockId.Air, session.Scene.Grid.GetBlock(chest.Cell.X, chest.Cell.Y, chest.Cell.Z));
        Assert.Equal(12, session.Simulation.Inventory.Count(ItemId.Wood));
        Assert.Equal(3, session.Simulation.Inventory.Count(ItemId.Berry));
        Assert.Equal("all", Assert.Single(Named(session, WorldEvents.BelongingsRecovered)).Detail);
    }

    [Fact]
    public void TakingBackNeedsTheCrosshairOnTheChest()
    {
        var session = Dying();

        Assert.Equal(NeedActionResult.Nothing, session.TakeBelongings());
        Assert.NotNull(session.Chest);
    }

    // Stands next to the chest and looks at it.
    private static void AimAtChest(VoxelSession session)
    {
        var cell = session.Chest!.Cell;
        Assert.True(VoxelWalker.WalkTo(session, c => Math.Abs(c.X - cell.X) + Math.Abs(c.Z - cell.Z) <= 2 && Math.Abs(c.Y - cell.Y) <= 1 && c != cell));
        var (yaw, pitch) = VoxelWalker.AimAt(session, cell);
        session.Step([VoxelCommand.Look(yaw, pitch)]);
        Assert.True(session.AimedAtChest);
    }

    [Fact]
    public void TheChestGoesWithTheNextNightAndNotWithALoad()
    {
        var session = Dying();
        var cell = session.Chest!.Cell;

        var loaded = SaveGame.Load(SaveGame.Save(session, Pristine()), TestGame.Data, _ => Pristine());
        Assert.NotNull(loaded.Chest);
        Assert.Equal(session.Chest.Items, loaded.Chest.Items);
        Assert.Equal(cell, loaded.Chest.Cell);
        Assert.Equal(BlockId.Chest, loaded.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z));

        SaveGame.Sleep(session, SaveGame.Capture(session, Pristine()));

        Assert.Null(session.Chest);
        Assert.Equal(BlockId.Air, session.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z));
        Assert.Equal(1, session.DayResetCount);
        Assert.Null(SaveGame.Capture(session, Pristine()).Systems!.Chest);
    }

    [Fact]
    public void ASaveWithAChestRoundTripsAndTheSavedChestIsInTheSystemsMap()
    {
        var session = Dying();

        var text = SaveGame.Save(session, Pristine());
        var again = SaveGame.Serialize(SaveGame.Parse(text));

        Assert.Equal(text, again);
        Assert.Contains("\"chest\":{", text, StringComparison.Ordinal);
        var loaded = SaveGame.Load(text, TestGame.Data, _ => Pristine());
        Assert.Equal(JsonSerializer.Serialize(SaveGame.Capture(session, Pristine()).Systems), JsonSerializer.Serialize(SaveGame.Parse(text).Systems));
        Assert.Equal(session.Chest!.Items, loaded.Chest!.Items);
    }

    [Fact]
    public void TheLearnersRecordTheLessonsTheObjectiveAndThePlacedBlocksAreUnchangedByADeath()
    {
        var session = Playing(thirst: 0, health: 3);
        session.Simulation.Inventory.Add(ItemId.Book, 1); // nothing to leave: no chest, so the world's edits are the same too
        Tick(session, 30);
        var before = SaveGame.Capture(session, Pristine());

        Tick(session, 20 * Tps);
        Assert.Equal(1, session.DeathCount);
        var after = SaveGame.Capture(session, Pristine());

        string Json(object? value) => JsonSerializer.Serialize(value);
        Assert.Equal(Json(before.Learner), Json(after.Learner));
        Assert.Equal(Json(before.Lessons), Json(after.Lessons));
        Assert.Equal(Json(before.Objective), Json(after.Objective));
        Assert.Equal(Json(before.Words), Json(after.Words));
        Assert.Equal(Json(before.Phrases), Json(after.Phrases));
        Assert.Equal(before.Session, after.Session);
        Assert.Equal(before.Sitting, after.Sitting);
        Assert.Equal(Json(before.Chunks), Json(after.Chunks));
    }

    [Fact]
    public void ADeathIsDeterministic()
    {
        string Run()
        {
            var session = Dying();
            Tick(session, 600);
            return SaveGame.Save(session, Pristine());
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void FromTheFarthestWalkablePointTheWalkToWaterTakesLessTimeThanTheHealthThatIsLeftLasts()
    {
        var session = Playing();
        var grid = session.Scene.Grid;
        var needs = new PlayerNeeds(TestGame.Data.Needs);
        needs.Set(NeedMeter.Hunger, 0);
        needs.Set(NeedMeter.Thirst, 0);
        var effects = needs.Effects();
        var healthLasts = 100 / effects.HealthLossPerSecond; // seconds, full health at both last stages
        var blocksPerSecond = VoxelPhysics.WalkSpeed * VoxelPhysics.TicksPerSecond / (double)VoxelPhysics.UnitsPerBlock * effects.WalkFactor;

        // Every dry standable cell next to surface water is a bank; a breadth first search from them gives each cell its steps to water.
        var distance = new Dictionary<BlockPos, int>();
        var queue = new Queue<BlockPos>();
        foreach (var water in grid.BlocksOfKind(BlockId.Water))
        {
            if (grid.GetBlock(water.X, water.Y + 1, water.Z) == BlockId.Water)
            {
                continue;
            }

            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dz = -1; dz <= 1; dz++)
                {
                    for (var dy = 0; dy <= 1; dy++)
                    {
                        var bank = new BlockPos(water.X + dx, water.Y + dy, water.Z + dz);
                        if (VoxelWalker.IsStandable(grid, bank) && distance.TryAdd(bank, 0))
                        {
                            queue.Enqueue(bank);
                        }
                    }
                }
            }
        }

        Assert.NotEmpty(queue);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in VoxelWalker.Neighbours(grid, current))
            {
                if (distance.TryAdd(next, distance[current] + 1))
                {
                    queue.Enqueue(next);
                }
            }
        }

        // The player's valley: every cell a walk from the start reaches. None may be cut off from water.
        var start = session.Simulation.Player.FeetBlock;
        var seen = new HashSet<BlockPos> { start };
        var walk = new Queue<BlockPos>();
        walk.Enqueue(start);
        var farthest = new List<(BlockPos Cell, int Steps)>();
        while (walk.Count > 0)
        {
            var current = walk.Dequeue();
            Assert.True(distance.TryGetValue(current, out var steps) || current == start, $"{current} is cut off from water");
            if (distance.TryGetValue(current, out steps))
            {
                farthest.Add((current, steps));
            }

            foreach (var next in VoxelWalker.Neighbours(grid, current))
            {
                if (seen.Add(next))
                {
                    walk.Enqueue(next);
                }
            }
        }

        // The real walk from the farthest cells, by the walker's own shortest path (down in the valley a fall is free, a climb is a block).
        var worst = 0;
        foreach (var (cell, _) in farthest.OrderByDescending(static f => f.Steps).Take(30))
        {
            var path = VoxelWalker.FindPath(grid, cell, c => distance.TryGetValue(c, out var d) && d == 0);
            Assert.NotNull(path);
            worst = Math.Max(worst, path.Count - 1);
        }

        var seconds = worst / blocksPerSecond;
        Assert.InRange(worst, 1, int.MaxValue);
        Assert.True(seconds < healthLasts, $"the longest walk to water is {worst} blocks, {seconds:0} s at {blocksPerSecond:0.00} blocks a second; health lasts {healthLasts:0} s");
    }
}
