using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using static Vocaterra.Game.Core.Tests.Voxel.VoxelTestSupport;

namespace Vocaterra.Game.Core.Tests.Needs;

/// <summary>Eating, drinking, sprinting, the gathering cost and rest at a lit fire (N02, adaptation design 9.4 and 9.6), through the session.</summary>
public sealed class EatDrinkSprintTests
{
    private static double Meter(VoxelSession session, NeedMeter meter) => session.Needs.Value(meter);

    private static IReadOnlyList<WorldEvent> Named(VoxelSession session, string name) => session.Events.OfType<WorldEvent>().Where(e => e.Name == name).ToList();

    private static VoxelSession Eating(double hunger = 40, double thirst = 100)
    {
        var session = TestGame.NewSession();
        session.SetNeed(NeedMeter.Hunger, hunger);
        session.SetNeed(NeedMeter.Thirst, thirst);
        return session;
    }

    private static string Noun(ItemId item) => TestGame.Nouns.ForItem(item)!.Lemma;

    private static void Tick(VoxelSession session, int ticks, params VoxelCommand[] commands)
    {
        for (var i = 0; i < ticks; i++)
        {
            session.Step(commands);
        }
    }

    // Walks to the bank next to a water block and looks at some water the ray really hits within reach (the nearest block may hide behind the bank's edge).
    private static BlockPos AtTheWater(VoxelSession session, BlockPos near, bool onTheRim = false)
    {
        if (onTheRim)
        {
            // The well's water lies behind its rim from the ground: stand on the rim beside it.
            Assert.True(VoxelWalker.WalkTo(session, cell => Math.Abs(cell.X - near.X) + Math.Abs(cell.Z - near.Z) <= 2 && cell.Y > near.Y), "no way up onto the well's rim");
        }
        else
        {
            Stage.WalkToWater(session, near);
        }

        var feet = session.Simulation.Player.FeetBlock;
        var candidates = session.Scene.Grid.BlocksOfKind(BlockId.Water)
            .Where(b => Math.Abs(b.X - feet.X) <= 4 && Math.Abs(b.Z - feet.Z) <= 4 && Math.Abs(b.Y - feet.Y) <= 3)
            .OrderBy(b => (((long)b.X - near.X) * (b.X - near.X)) + (((long)b.Z - near.Z) * (b.Z - near.Z)));
        foreach (var water in candidates)
        {
            var (yaw, pitch) = VoxelWalker.AimAt(session, water);
            session.Step([VoxelCommand.Look(yaw, pitch)]);
            if (session.AimedAtWater)
            {
                return water;
            }
        }

        throw new InvalidOperationException("No water can be aimed at from the bank.");
    }

    private static BlockPos AtTheRiver(VoxelSession session) => AtTheWater(session, Stage.NearestRiver(session));

    [Fact]
    public void EatingRestoresAndCaps()
    {
        var session = Eating(40);
        session.SetNeed(NeedMeter.Health, 80);
        session.Simulation.GrantItems(ItemId.Bread, 2);

        Assert.Equal(NeedActionResult.Done, session.EatItem(ItemId.Bread));

        Assert.Equal(65, Meter(session, NeedMeter.Hunger), 6); // bread: hunger +25
        Assert.Equal(85, Meter(session, NeedMeter.Health), 6); // and healing +5
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bread));

        session.SetNeed(NeedMeter.Hunger, 95);
        session.Simulation.GrantItems(ItemId.Berry, 1);
        Assert.Equal(NeedActionResult.Done, session.EatItem(ItemId.Berry));
        Assert.Equal(100, Meter(session, NeedMeter.Hunger)); // capped at 100, the berry used up
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Berry));
    }

    [Fact]
    public void ARightClickEatsTheSelectedFoodAndNothingElse()
    {
        var session = Eating(40);
        session.Simulation.GrantItems(ItemId.Apple, 1);
        var slot = Enumerable.Range(0, Inventory.HotbarSize).First(i => session.Simulation.Inventory.Slots[i] is { IsEmpty: false, Item: ItemId.Apple });
        session.Step([VoxelCommand.SelectSlot(slot)]);

        Assert.Equal(NeedActionResult.Done, session.EatSelected());

        Assert.Equal(52, Meter(session, NeedMeter.Hunger), 1); // apple: hunger +12 (the tick before it cost a hair)
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Apple));
        Assert.Equal(NeedActionResult.Nothing, session.EatSelected()); // an empty slot is no food

        session.Step([VoxelCommand.SelectSlot(0)]);
        Assert.Equal(ItemId.Book, session.Simulation.SelectedStack.Item);
        Assert.Equal(NeedActionResult.Nothing, session.EatSelected()); // the book is no food
    }

    [Fact]
    public void AtFullHungerTheFoodIsKeptWithALineAndNoEvent()
    {
        var session = TestGame.NewSession();
        session.Simulation.GrantItems(ItemId.Bread, 1);

        Assert.Equal(NeedActionResult.Kept, session.EatItem(ItemId.Bread));

        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bread));
        Assert.Equal([session.Locale.Ui(VoxelSession.FoodKeptHint)], session.TakeHints());
        Assert.Empty(Named(session, WorldEvents.MealEaten));

        session.SetNeed(NeedMeter.Thirst, 10); // a thirsty player who is not hungry still keeps the bread
        Assert.Equal(NeedActionResult.Kept, session.EatItem(ItemId.Bread));
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bread));
    }

    [Fact]
    public void TheBottleComesBackEmptyAndTheDrinkRaisesWaterDrunkWithBotella()
    {
        var session = Eating(100, 30);
        session.Simulation.GrantItems(ItemId.WaterBottle, 1);

        Assert.Equal(NeedActionResult.Done, session.EatItem(ItemId.WaterBottle));

        Assert.Equal(70, Meter(session, NeedMeter.Thirst), 6);
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.WaterBottle));
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bottle));
        Assert.Equal("botella", Assert.Single(Named(session, WorldEvents.WaterDrunk)).Detail);
        Assert.Empty(Named(session, WorldEvents.MealEaten));

        // At full thirst the bottle is kept, full, and a line says so.
        session.SetNeed(NeedMeter.Thirst, 100);
        session.Simulation.GrantItems(ItemId.WaterBottle, 1);
        session.TakeHints();
        Assert.Equal(NeedActionResult.Kept, session.EatItem(ItemId.WaterBottle));
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.WaterBottle));
        Assert.Equal([session.Locale.Ui(VoxelSession.DrinkKeptHint)], session.TakeHints());
    }

    [Fact]
    public void AMealRaisesMealEatenWithTheFoodsNoun()
    {
        var session = Eating(40);
        session.Simulation.GrantItems(ItemId.Bread, 1);
        session.Simulation.GrantItems(ItemId.Berry, 1);

        session.EatItem(ItemId.Bread);
        session.EatItem(ItemId.Berry);

        Assert.Equal([Noun(ItemId.Bread), Noun(ItemId.Berry)], Named(session, WorldEvents.MealEaten).Select(static e => e.Detail).ToList());
    }

    [Fact]
    public void ADrinkByHandAtTheRiverNeedsNothingHeldAndNoSpanish()
    {
        var session = TestGame.NewSession();
        session.SetNeed(NeedMeter.Thirst, 30);
        Assert.Equal(NeedActionResult.Nothing, session.DrinkByHand()); // far from the water: nothing to drink
        Assert.False(session.AimedAtWater);
        AtTheRiver(session);
        Assert.True(session.AimedAtWater);
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Bottle) + session.Simulation.Inventory.Count(ItemId.WaterBottle));
        var before = Meter(session, NeedMeter.Thirst);

        Assert.Equal(NeedActionResult.Done, session.DrinkByHand());

        Assert.Equal(before + 10, Meter(session, NeedMeter.Thirst), 6);
        Assert.Equal(TestGame.Data.Words.Noun("river"), Assert.Single(Named(session, WorldEvents.WaterDrunk)).Detail);
    }

    [Fact]
    public void ADrinkByHandAtTheWellNamesTheWellAndAtFullThirstDrinksNothing()
    {
        var session = TestGame.NewSession();
        var well = session.Scene.Point("well");
        var water = session.Scene.Grid.BlocksOfKind(BlockId.Water).OrderBy(b => (((long)b.X - well.X) * (b.X - well.X)) + (((long)b.Z - well.Z) * (b.Z - well.Z))).First();
        AtTheWater(session, water, onTheRim: true);
        session.SetNeed(NeedMeter.Thirst, 100); // the walk cost a hair

        Assert.Equal(NeedActionResult.Kept, session.DrinkByHand()); // full thirst
        Assert.Equal([session.Locale.Ui(VoxelSession.DrinkKeptHint)], session.TakeHints());
        Assert.Empty(Named(session, WorldEvents.WaterDrunk));

        session.SetNeed(NeedMeter.Thirst, 50);
        Assert.Equal(NeedActionResult.Done, session.DrinkByHand());
        Assert.Equal("pozo", Assert.Single(Named(session, WorldEvents.WaterDrunk)).Detail);
    }

    [Fact]
    public void TheBottleStillFillsAtTheRiverAndWaterWithoutABottleStillHintsAndNeitherCostsStamina()
    {
        var session = TestGame.NewSession();
        var water = Stage.NearestRiver(session);
        Stage.WalkToWater(session, water);
        var (yaw, pitch) = VoxelWalker.AimAt(session, water);

        session.Step([VoxelCommand.Look(yaw, pitch), VoxelCommand.Interact(water.X, water.Y, water.Z)]);
        Assert.Equal([session.Locale.Ui(VoxelSession.NeedBottleHint)], session.TakeHints());
        session.Simulation.GrantItems(ItemId.Bottle, 1);
        session.Needs.Restore(new NeedsSave(100, 100, 100, 60, 100));
        Assert.True(Stage.Fill(session, water) > 0);

        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.WaterBottle));
        Assert.Single(Named(session, WorldEvents.BottleFilled));
        Assert.True(Meter(session, NeedMeter.Stamina) >= 60, "filling a bottle is no labour");
    }

    [Fact]
    public void WithTheNeedsOffTheFoodIsStillUsedButTheMetersDoNotMove()
    {
        var session = Eating(40, 30);
        session.NeedsEnabled = false;
        session.Simulation.GrantItems(ItemId.Bread, 1);
        session.Simulation.GrantItems(ItemId.WaterBottle, 1);

        Assert.Equal(NeedActionResult.Done, session.EatItem(ItemId.Bread));
        Assert.Equal(NeedActionResult.Done, session.EatItem(ItemId.WaterBottle));
        AtTheRiver(session);
        Assert.Equal(NeedActionResult.Done, session.DrinkByHand());

        Assert.Equal(40, Meter(session, NeedMeter.Hunger));
        Assert.Equal(30, Meter(session, NeedMeter.Thirst));
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Bread));
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bottle));
        Assert.Single(Named(session, WorldEvents.MealEaten));
        Assert.Equal(["botella", TestGame.Data.Words.Noun("river")], Named(session, WorldEvents.WaterDrunk).Select(static e => e.Detail).ToList());
    }

    // How far the player walks in ticks of one held direction, the first of the four that is free to walk.
    private static long Walked(VoxelSession session, int ticks)
    {
        foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var startX = session.Simulation.Player.CenterX;
            var startZ = session.Simulation.Player.CenterZ;
            Tick(session, 3, VoxelCommand.Move(dx, dz));
            var gone = Math.Abs(session.Simulation.Player.CenterX - startX) + Math.Abs(session.Simulation.Player.CenterZ - startZ);
            if (gone >= 3 * 200)
            {
                var x0 = session.Simulation.Player.CenterX;
                var z0 = session.Simulation.Player.CenterZ;
                Tick(session, ticks, VoxelCommand.Move(dx, dz));
                return Math.Abs(session.Simulation.Player.CenterX - x0) + Math.Abs(session.Simulation.Player.CenterZ - z0);
            }
        }

        throw new InvalidOperationException("The player cannot walk any way from the spawn.");
    }

    private static long WalkedBy(Action<VoxelSession> arrange, bool sprint)
    {
        var session = TestGame.NewSession();
        arrange(session);
        session.SprintHeld = sprint;
        return Walked(session, 20);
    }

    [Fact]
    public void SprintWalksAboutOnePointSixTimesAsFastAsWalking()
    {
        var walk = WalkedBy(static _ => { }, sprint: false);
        var sprint = WalkedBy(static _ => { }, sprint: true);

        Assert.Equal(20 * 294, walk);
        Assert.InRange(sprint / (double)walk, 1.59, 1.61);
    }

    [Theory]
    [InlineData(NeedMeter.Hunger, 0)]
    [InlineData(NeedMeter.Thirst, 0)]
    [InlineData(NeedMeter.Health, 20)]
    public void SprintIsRefusedAtADrivesLastStageAndAtVeryWeakHealthAndTheSlowestFactorApplies(NeedMeter meter, double value)
    {
        var walk = WalkedBy(session => session.SetNeed(meter, value), sprint: false);
        var sprint = WalkedBy(session => session.SetNeed(meter, value), sprint: true);

        Assert.Equal(walk, sprint); // a forbidden sprint is a walk
        Assert.True(walk < 20 * 294, "the last stage slows the walk (it never stops it)");
        Assert.True(walk > 0);
    }

    [Fact]
    public void SprintIsRefusedAtZeroStaminaAndAMiddleStageDoesNotForbidIt()
    {
        var empty = WalkedBy(static session => session.Needs.Restore(new NeedsSave(100, 100, 100, 0, 100)), sprint: true);
        Assert.Equal(20 * 294, empty);

        var hungry = WalkedBy(static session => session.SetNeed(NeedMeter.Hunger, 30), sprint: true); // stage 3 (hunger below 25 is stage 4)
        Assert.InRange(hungry / (double)(20 * 294), 1.59, 1.61);
    }

    [Fact]
    public void SprintCostsStaminaAndMakesTheDrivesFallFasterOnlyWhileMoving()
    {
        var sprinting = TestGame.NewSession();
        sprinting.SprintHeld = true;
        Walked(sprinting, 120);

        var walking = TestGame.NewSession();
        Walked(walking, 120);

        var standing = TestGame.NewSession();
        standing.SprintHeld = true;
        Tick(standing, 123);

        Assert.True(Meter(sprinting, NeedMeter.Stamina) < 100 - 15, "two seconds of sprint cost about 20 stamina");
        Assert.Equal(100, Meter(walking, NeedMeter.Stamina));
        Assert.Equal(100, Meter(standing, NeedMeter.Stamina)); // the key held while standing still costs nothing
        Assert.True(Meter(sprinting, NeedMeter.Hunger) < Meter(walking, NeedMeter.Hunger));
        Assert.True(Meter(standing, NeedMeter.Hunger) > Meter(sprinting, NeedMeter.Hunger));
    }

    [Fact]
    public void WithTheNeedsOffSprintStillWorksAndCostsNothing()
    {
        var session = TestGame.NewSession();
        session.NeedsEnabled = false;
        session.SprintHeld = true;

        var walked = Walked(session, 20);

        Assert.InRange(walked / (double)(20 * 294), 1.59, 1.61);
        Assert.Equal(100, Meter(session, NeedMeter.Stamina));
    }

    private static BlockPos TrunkNear(VoxelSession session)
    {
        var trunk = TestGame.NearestBlock(session, BlockId.Trunk);
        Assert.True(VoxelWalker.WalkTo(session, cell => Math.Abs(cell.X - trunk.X) + Math.Abs(cell.Z - trunk.Z) <= 2 && Math.Abs(cell.Y - trunk.Y) <= 1), "no path to a trunk");
        return trunk;
    }

    private static int GatherTicks(VoxelSession session, BlockPos target)
    {
        var (yaw, pitch) = VoxelWalker.AimAt(session, target);
        var before = session.Scene.Grid.GetBlock(target.X, target.Y, target.Z);
        for (var ticks = 1; ticks <= 2000; ticks++)
        {
            session.Step([VoxelCommand.Look(yaw, pitch), VoxelCommand.Interact(target.X, target.Y, target.Z)]);
            if (session.Scene.Grid.GetBlock(target.X, target.Y, target.Z) != before)
            {
                return ticks;
            }
        }

        return -1;
    }

    [Fact]
    public void GatheringByHandCostsStaminaAtTheDataRate()
    {
        var session = TestGame.NewSession();
        var trunk = TrunkNear(session);
        var stamina = Meter(session, NeedMeter.Stamina);

        var ticks = GatherTicks(session, trunk);

        Assert.True(ticks > 0);
        Assert.Equal(stamina - (ticks * TestGame.Data.Needs.Stamina.GatherPerTick), Meter(session, NeedMeter.Stamina), 6);
    }

    [Fact]
    public void GatheringWithTheAxeCostsAQuarterBecauseItTakesAQuarterOfTheTicks()
    {
        var byHand = TestGame.NewSession();
        var handTicks = GatherTicks(byHand, TrunkNear(byHand));
        var handCost = 100 - Meter(byHand, NeedMeter.Stamina);

        var withAxe = TestGame.NewSession();
        withAxe.Simulation.GrantItems(ItemId.Axe, 1);
        var slot = Enumerable.Range(0, Inventory.HotbarSize).First(i => withAxe.Simulation.Inventory.Slots[i] is { IsEmpty: false, Item: ItemId.Axe });
        withAxe.Step([VoxelCommand.SelectSlot(slot)]);
        var axeTicks = GatherTicks(withAxe, TrunkNear(withAxe));
        var axeCost = 100 - Meter(withAxe, NeedMeter.Stamina);

        Assert.True(axeTicks < handTicks);
        Assert.InRange(axeCost / handCost, 0.2, 0.3);
    }

    [Fact]
    public void AtZeroStaminaGatheringSlowsAndNeverStops()
    {
        var full = TestGame.NewSession();
        var fullTicks = GatherTicks(full, TrunkNear(full));

        var empty = TestGame.NewSession();
        var trunk = TrunkNear(empty);
        empty.Needs.Restore(new NeedsSave(100, 100, 100, 0, 100));
        var emptyTicks = GatherTicks(empty, trunk);

        Assert.True(emptyTicks > 0, "gathering never stops");
        Assert.InRange(emptyTicks / (double)fullTicks, 1.8, 2.2); // the data's slow factor, a half
        Assert.Equal(0, Meter(empty, NeedMeter.Stamina));
    }

    [Fact]
    public void ASlowedGatherAdvancesOnTheTicksThatCrossAWholeStep()
    {
        var sim = FlatSim(static g => g.SetBlock(32, GroundTop + 2, 30, BlockId.Trunk));
        sim.GatherPermille = 250;
        var progress = new List<int>();
        for (var i = 0; i < 8; i++)
        {
            sim.Step([VoxelCommand.Interact(32, GroundTop + 2, 30)]);
            progress.Add(sim.GatherProgress);
        }

        Assert.Equal([0, 0, 0, 1, 1, 1, 1, 2], progress);
    }

    // The cells round a fire's middle at a squared distance, standable (air at the feet and above, ground below).
    private static BlockPos? StandableAt(VoxelSession session, BlockPos middle, int squared)
    {
        var grid = session.Scene.Grid;
        for (var dx = -4; dx <= 4; dx++)
        {
            for (var dz = -4; dz <= 4; dz++)
            {
                if ((dx * dx) + (dz * dz) != squared)
                {
                    continue;
                }

                for (var y = middle.Y - 2; y <= middle.Y + 2; y++)
                {
                    var (x, z) = (middle.X + dx, middle.Z + dz);
                    if (!BlockDefinition.IsSolid(grid.GetBlock(x, y, z)) && !BlockDefinition.IsSolid(grid.GetBlock(x, y + 1, z)) && !BlockDefinition.IsSolid(grid.GetBlock(x, y + 2, z)) && BlockDefinition.IsSolid(grid.GetBlock(x, y - 1, z)))
                    {
                        return new BlockPos(x, y, z);
                    }
                }
            }
        }

        return null;
    }

    private static BlockPos ForgeMiddle(VoxelSession session) => session.Fires.Middles.OrderBy(m => Math.Abs(m.X - session.Scene.Point("forge").X) + Math.Abs(m.Z - session.Scene.Point("forge").Z)).First();

    // The stamina and health a player standing still at a cell gains in the seconds, starting low.
    private static (double Stamina, double Health) RestedAt(VoxelSession session, BlockPos feet, int seconds, int extraTicks = 0)
    {
        session.Simulation.TeleportTo(feet);
        session.Needs.Restore(new NeedsSave(100, 100, 100, 20, 50, 0, 5));
        Tick(session, (seconds * 60) + extraTicks);
        return (Meter(session, NeedMeter.Stamina) - 20, Meter(session, NeedMeter.Health) - 50);
    }

    [Fact]
    public void RestAtALitFireDoublesTheRefillAndTheRecoveryJustInsideThreeBlocksAndNotJustOutside()
    {
        var session = TestGame.NewSession();
        var forge = ForgeMiddle(session);
        Assert.True(session.Fires.IsLit(forge), "the forge's post torch always burns");
        var inside = StandableAt(session, forge, 9) ?? throw new InvalidOperationException("no standable cell three blocks from the forge");
        var outside = StandableAt(session, forge, 10) ?? throw new InvalidOperationException("no standable cell just beyond three blocks from the forge");

        var (staminaIn, healthIn) = RestedAt(session, inside, 2);
        var (staminaOut, healthOut) = RestedAt(session, outside, 2);

        Assert.InRange(staminaIn / staminaOut, 1.9, 2.1);
        Assert.InRange(healthIn / healthOut, 1.9, 2.1);
        Assert.True(healthOut > 0);
    }

    [Fact]
    public void RestNeedsStandingStillWalkingAtTheFireRefillsAtTheOrdinaryRate()
    {
        var session = TestGame.NewSession();
        var at = StandableAt(session, ForgeMiddle(session), 4) ?? throw new InvalidOperationException("no standable cell two blocks from the forge");
        var (rested, _) = RestedAt(session, at, 0, 13);

        session.Simulation.TeleportTo(at);
        session.Needs.Restore(new NeedsSave(100, 100, 100, 20, 50, 0, 5));
        Walked(session, 10); // three probing ticks and ten more, away from the fire's middle or towards it, never beyond three blocks
        var walked = Meter(session, NeedMeter.Stamina) - 20;

        Assert.InRange(walked / rested, 0.45, 0.6); // half: the walk is not rest
    }

    [Fact]
    public void RestDoesNotRestoreSleep()
    {
        var session = TestGame.NewSession();
        var at = StandableAt(session, ForgeMiddle(session), 4) ?? throw new InvalidOperationException("no standable cell near the forge");
        session.Simulation.TeleportTo(at);
        session.Needs.Restore(new NeedsSave(100, 100, 60, 20, 50, 0, 5));

        Tick(session, 600); // ten seconds standing still at a lit fire

        Assert.True(Meter(session, NeedMeter.Sleep) < 60, "rest does not restore sleep: only sleeping does");
        Assert.True(Meter(session, NeedMeter.Health) > 50);
    }

    [Fact]
    public void TheFiresOfTheWorldAreFoundByShapeAndTheGuidesFireBurnsOnlyOnceATorchIsOnTheRimOrInTheMiddle()
    {
        var session = TestGame.NewSession();
        var rings = session.Fires.Middles;
        Assert.True(rings.Count >= 2, "the guide's camp fire and the forge (Y08: no ring on the mountain)");

        var camp = session.Scene.Point(VoxelSession.CampFireRing);
        var campMiddle = rings.Single(m => m.X == camp.X && m.Z == camp.Z);
        Assert.False(session.Fires.IsLit(campMiddle));

        session.Scene.Grid.SetBlock(campMiddle.X + 1, campMiddle.Y + 1, campMiddle.Z, BlockId.Torch); // a torch on the rim
        Assert.True(session.Fires.IsLit(campMiddle));

        session.Scene.Grid.SetBlock(campMiddle.X + 1, campMiddle.Y + 1, campMiddle.Z, BlockId.Air);
        Assert.False(session.Fires.IsLit(campMiddle));
        session.Scene.Grid.SetBlock(campMiddle.X, campMiddle.Y, campMiddle.Z, BlockId.Torch); // and one in the middle
        Assert.True(session.Fires.IsLit(campMiddle));
    }

    [Fact]
    public void ARingIsAnEmptyMiddleRoundedByEightStonesAndLitByATorchOnItsLayerOrOnTopOnly()
    {
        var scene = FlatScene(static g =>
        {
            var y = GroundTop + 1;
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dz = -1; dz <= 1; dz++)
                {
                    if (dx != 0 || dz != 0)
                    {
                        g.SetBlock(20 + dx, y, 20 + dz, BlockId.FireRing);
                    }
                }
            }
        });
        var fires = new FireRings(scene);
        var middle = Assert.Single(fires.Middles);
        Assert.Equal(new BlockPos(20, GroundTop + 1, 20), middle);
        Assert.False(fires.IsLit(middle));

        scene.Grid.SetBlock(20, middle.Y + 3, 20, BlockId.Torch);
        Assert.False(fires.IsLit(middle)); // too high to be the fire's
        scene.Grid.SetBlock(21, middle.Y + 1, 20, BlockId.PostTorch);
        Assert.True(fires.IsLit(middle));
        Assert.True(fires.NearLit(new BlockPos(23, middle.Y, 20), 3));
        Assert.False(fires.NearLit(new BlockPos(23, middle.Y, 21), 3));
    }

    [Fact]
    public void TheSameInputsGiveTheSameMetersAndEvents()
    {
        (NeedsSave, string) Play()
        {
            var session = TestGame.NewSession();
            session.SetNeed(NeedMeter.Hunger, 40);
            session.SetNeed(NeedMeter.Thirst, 30);
            session.Simulation.GrantItems(ItemId.Bread, 1);
            session.Simulation.GrantItems(ItemId.WaterBottle, 1);
            session.SprintHeld = true;
            Walked(session, 90);
            session.EatItem(ItemId.Bread);
            session.EatItem(ItemId.WaterBottle);
            Tick(session, 600);
            return (session.Needs.Capture(), string.Join(';', session.Events.OfType<WorldEvent>().Select(static e => e.Name + e.Text + e.Tick)));
        }

        Assert.Equal(Play(), Play());
    }

    [Fact]
    public void ASaveAfterEatingAndDrinkingRoundTrips()
    {
        var session = Eating(40, 30);
        session.Simulation.GrantItems(ItemId.Bread, 1);
        session.Simulation.GrantItems(ItemId.WaterBottle, 1);
        session.EatItem(ItemId.Bread);
        session.EatItem(ItemId.WaterBottle);
        Tick(session, 300);

        var loaded = SaveGame.Load(SaveGame.Save(session), TestGame.Data, TestGame.Route);

        Assert.Equal(session.Needs.Capture(), loaded.Needs.Capture());
        Assert.Equal(1, loaded.Simulation.Inventory.Count(ItemId.Bottle));
        Assert.Equal(0, loaded.Simulation.Inventory.Count(ItemId.Bread));
    }

    [Fact]
    public void TheFoodItemUseRightClickEatsOnlyAFoodWithTheMouseCaptured()
    {
        var click = new TestInputFrame { RightDown = true };
        var needs = TestGame.Data.Needs;
        Assert.True(FoodItemUse.Applies(click, true, new ItemStack(ItemId.Bread, 1), needs));
        Assert.True(FoodItemUse.Applies(click, true, new ItemStack(ItemId.WaterBottle, 1), needs));
        Assert.False(FoodItemUse.Applies(click, false, new ItemStack(ItemId.Bread, 1), needs)); // the mouse is free
        Assert.False(FoodItemUse.Applies(click, true, new ItemStack(ItemId.Book, 1), needs));
        Assert.False(FoodItemUse.Applies(click, true, ItemStack.Empty, needs));
        Assert.False(FoodItemUse.Applies(new TestInputFrame { RightDown = true, PreviousRightDown = true }, true, new ItemStack(ItemId.Bread, 1), needs)); // held, not a click
    }
}
