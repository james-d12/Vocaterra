using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using static Vocaterra.Game.Core.Tests.Voxel.VoxelTestSupport;

namespace Vocaterra.Game.Core.Tests.Simulation;

/// <summary>Gathering trunks and berries, placing blocks, crafting at the workbench, and the events they raise.</summary>
public sealed class GatheringAndPlacingTests
{
    // In the flat scene the player stands at (32.5, 10, 32.5), eye at y = 11.62; a block at (32, 11, 30) is about 2 blocks ahead (north).
    private static readonly BlockPos Ahead = new(32, GroundTop + 2, 30);

    private static VoxelSimulation WithBlock(BlockId block) => FlatSim(g => g.SetBlock(Ahead.X, Ahead.Y, Ahead.Z, block));

    private static VoxelCommand InteractAhead => VoxelCommand.Interact(Ahead.X, Ahead.Y, Ahead.Z);

    [Fact]
    public void GatheringATrunkTakesItsTicksAndYieldsWood()
    {
        var sim = WithBlock(BlockId.Trunk);
        var ticks = BlockDefinition.Get(BlockId.Trunk).GatherTicks;
        Assert.Equal(60, ticks);
        for (var tick = 1; tick < ticks; tick++)
        {
            sim.Step([InteractAhead]);
            Assert.Equal(tick, sim.GatherProgress);
            Assert.Equal(Ahead, sim.GatherTarget);
            Assert.Equal(0, sim.Inventory.Count(ItemId.Wood));
            Assert.Equal(BlockId.Trunk, sim.Grid.GetBlock(Ahead.X, Ahead.Y, Ahead.Z));
            var progress = Assert.Single(sim.Events);
            Assert.Equal(SimulationEventKind.GatherProgress, progress.Kind);
            Assert.Equal((Ahead.X, Ahead.Y, Ahead.Z, tick, ticks), (progress.X, progress.Y, progress.Z, progress.Detail, progress.Amount));
        }

        sim.Step([InteractAhead]);
        Assert.Equal(1, sim.Inventory.Count(ItemId.Wood));
        Assert.Equal(BlockId.Air, sim.Grid.GetBlock(Ahead.X, Ahead.Y, Ahead.Z));
        Assert.Null(sim.GatherTarget);
        Assert.Equal(0, sim.GatherProgress);
        Assert.Equal(
            [SimulationEventKind.GatherProgress, SimulationEventKind.BlockRemoved, SimulationEventKind.ItemPickedUp],
            sim.Events.Select(e => e.Kind));
        var removed = sim.Events.Single(e => e.Kind == SimulationEventKind.BlockRemoved);
        Assert.Equal((int)BlockId.Trunk, removed.Detail);
        Assert.Equal(Ahead.Y, removed.Y);
        var picked = sim.Events.Single(e => e.Kind == SimulationEventKind.ItemPickedUp);
        Assert.Equal(((int)ItemId.Wood, 1), (picked.Detail, picked.Amount));
        Assert.Equal(sim.Tick, picked.Tick);
    }

    [Theory]
    [InlineData(BlockId.Trunk)]
    [InlineData(BlockId.SpruceTrunk)]
    [InlineData(BlockId.BirchTrunk)]
    public void TheAxeHeldCutsATrunkInAQuarterOfTheTicks(BlockId trunk)
    {
        // village-plan V03: the smith's axe, held (the selected hotbar slot), makes a trunk 15 ticks instead of 60; in the bag it does nothing.
        var sim = WithBlock(trunk);
        sim.Inventory.SetSlot(0, new ItemStack(ItemId.Axe, 1));
        sim.Step([VoxelCommand.SelectSlot(0)]);
        Assert.Equal(15, sim.GatherTicksOf(trunk)); // the axe's (Wood, 4): 60 by hand, 15 with the axe (W02)

        Run(sim, 14, InteractAhead);
        Assert.Equal(0, sim.Inventory.Count(ItemId.Wood));
        Assert.Equal((14, 15), (sim.Events.Single().Detail, sim.Events.Single().Amount)); // the progress event carries the shorter total, for the bar
        sim.Step([InteractAhead]);
        Assert.Equal(1, sim.Inventory.Count(ItemId.Wood));
        Assert.Equal(BlockId.Air, sim.Grid.GetBlock(Ahead.X, Ahead.Y, Ahead.Z));

        var inTheBag = WithBlock(trunk);
        inTheBag.Inventory.SetSlot(Inventory.HotbarSize, new ItemStack(ItemId.Axe, 1)); // carried, not held
        Assert.Equal(BlockDefinition.TrunkGatherTicks, inTheBag.GatherTicksOf(trunk));
        Run(inTheBag, 15, InteractAhead);
        Assert.Equal(0, inTheBag.Inventory.Count(ItemId.Wood));
    }

    [Fact]
    public void TheAxeDoesNotHurryWhatIsNoTrunk()
    {
        var sim = WithBlock(BlockId.BerryBush);
        sim.Inventory.SetSlot(0, new ItemStack(ItemId.Axe, 1));
        sim.Step([VoxelCommand.SelectSlot(0)]);

        Assert.Equal(BlockDefinition.BerryBushGatherTicks, sim.GatherTicksOf(BlockId.BerryBush));
        Run(sim, BlockDefinition.BerryBushGatherTicks - 1, InteractAhead);
        Assert.Equal(0, sim.Inventory.Count(ItemId.Berry));
    }

    [Fact]
    public void HandsGatherPlantsAtTheirOwnPace()
    {
        // W02: hands are speed 1, so a berry bush takes its own 45 ticks, and the progress event carries the same total the HUD bar reads.
        var sim = WithBlock(BlockId.BerryBush);
        Assert.Equal(BlockDefinition.BerryBushGatherTicks, sim.GatherTicksOf(BlockId.BerryBush));
        sim.Step([InteractAhead]);
        Assert.Equal(BlockDefinition.BerryBushGatherTicks, sim.Events.Single().Amount);
    }

    [Fact]
    public void ToolsAndMaterialsAreDeclaredPerBlockAndPerItem()
    {
        Assert.Equal(Material.Wood, BlockDefinition.Get(BlockId.Trunk).Material);
        Assert.Equal(Material.Wood, BlockDefinition.Get(BlockId.SpruceTrunk).Material);
        Assert.Equal(Material.Wood, BlockDefinition.Get(BlockId.BirchTrunk).Material);
        Assert.Equal(Material.Plant, BlockDefinition.Get(BlockId.BerryBush).Material);
        Assert.Equal(Material.None, BlockDefinition.Get(BlockId.Air).Material);
        Assert.Equal(new ToolSpec(Material.Wood, 4), ItemDefinition.Get(ItemId.Axe).Tool);
        Assert.Null(ItemDefinition.Get(ItemId.Wood).Tool);
    }

    [Fact]
    public void AToolOfAnotherMaterialChangesNothingOnAPlantOrAStone()
    {
        // W02: the axe works on wood only; a stone block (no gather ticks by hand) and the berry bush keep their own ticks with it held.
        var sim = WithBlock(BlockId.BerryBush);
        sim.Inventory.SetSlot(0, new ItemStack(ItemId.Axe, 1));
        sim.Step([VoxelCommand.SelectSlot(0)]);
        Assert.Equal(BlockDefinition.BerryBushGatherTicks, sim.GatherTicksOf(BlockId.BerryBush));
        Assert.Equal(0, sim.GatherTicksOf(BlockId.Stone));
    }

    [Fact]
    public void ReleasingOrSwitchingTargetsResetsTheProgress()
    {
        var sim = FlatSim(g =>
        {
            g.SetBlock(Ahead.X, Ahead.Y, Ahead.Z, BlockId.Trunk);
            g.SetBlock(Ahead.X + 1, Ahead.Y, Ahead.Z, BlockId.Trunk);
        });
        Run(sim, 30, InteractAhead);
        Assert.Equal(30, sim.GatherProgress);
        sim.Step();
        Assert.Equal(0, sim.GatherProgress);
        Run(sim, 59, InteractAhead);
        Assert.Equal(0, sim.Inventory.Count(ItemId.Wood));
        Run(sim, 20, VoxelCommand.Interact(Ahead.X + 1, Ahead.Y, Ahead.Z));
        Assert.Equal(20, sim.GatherProgress);
        Assert.Equal(0, sim.Inventory.Count(ItemId.Wood));
        Run(sim, 40, VoxelCommand.Interact(Ahead.X + 1, Ahead.Y, Ahead.Z));
        Assert.Equal(1, sim.Inventory.Count(ItemId.Wood));
    }

    [Fact]
    public void ABerryBushGivesABerryAndStaysAsAPlainBush()
    {
        var sim = WithBlock(BlockId.BerryBush);
        Run(sim, BlockDefinition.BerryBushGatherTicks, InteractAhead);
        Assert.Equal(1, sim.Inventory.Count(ItemId.Berry));
        Assert.Equal(BlockId.Bush, sim.Grid.GetBlock(Ahead.X, Ahead.Y, Ahead.Z));
        Run(sim, 200, InteractAhead);
        Assert.Equal(1, sim.Inventory.Count(ItemId.Berry)); // a plain bush gives nothing more
        Assert.Equal(BlockId.Bush, sim.Grid.GetBlock(Ahead.X, Ahead.Y, Ahead.Z));
    }

    [Fact]
    public void ThingsThatCannotBeGatheredAndThingsOutOfReachAreIgnored()
    {
        var stone = WithBlock(BlockId.Stone);
        Run(stone, 200, InteractAhead);
        Assert.Equal(BlockId.Stone, stone.Grid.GetBlock(Ahead.X, Ahead.Y, Ahead.Z));
        Assert.Equal(0, stone.GatherProgress);
        Assert.Empty(stone.Events);

        var far = FlatSim(g => g.SetBlock(32, GroundTop + 1, 20, BlockId.Trunk));
        Run(far, 200, VoxelCommand.Interact(32, GroundTop + 1, 20));
        Assert.Equal(BlockId.Trunk, far.Grid.GetBlock(32, GroundTop + 1, 20));
        Assert.Equal(0, far.Inventory.Count(ItemId.Wood));

        var air = FlatSim();
        Run(air, 10, VoxelCommand.Interact(1000, 1000, 1000));
        Run(air, 10, VoxelCommand.Interact(32, GroundTop + 5, 31));
        Assert.Equal(0, air.GatherProgress);
    }

    [Fact]
    public void AFullInventoryHoldsTheGatheringBackAndKeepsTheBlock()
    {
        var sim = WithBlock(BlockId.Trunk);
        for (var slot = 0; slot < Inventory.SlotCount; slot++)
        {
            sim.Inventory.SetSlot(slot, new ItemStack(ItemId.Berry, 99));
        }

        Run(sim, 200, InteractAhead);
        Assert.Equal(BlockId.Trunk, sim.Grid.GetBlock(Ahead.X, Ahead.Y, Ahead.Z));
        Assert.Equal(0, sim.Inventory.Count(ItemId.Wood));
        sim.Inventory.SetSlot(7, ItemStack.Empty);
        Run(sim, 60, InteractAhead);
        Assert.Equal(1, sim.Inventory.Count(ItemId.Wood));
    }

    [Fact]
    public void GatheringChangesTheWorldDigestAndTheStateDigest()
    {
        var sim = WithBlock(BlockId.Trunk);
        var world = sim.Grid.ComputeDigestHex();
        var state = sim.ComputeStateDigest();
        Run(sim, 30, InteractAhead);
        Assert.Equal(world, sim.Grid.ComputeDigestHex()); // progress alone does not touch the blocks
        Assert.NotEqual(state, sim.ComputeStateDigest());
        Run(sim, 30, InteractAhead);
        Assert.NotEqual(world, sim.Grid.ComputeDigestHex());
    }

    [Fact]
    public void ARealTreeOfTheRouteYieldsOneWoodPerTrunkBlock()
    {
        var scene = BuildRoute(42);
        var grid = scene.Grid;

        // The trunk nearest the guide's camp (the walk starts there): its lowest block, and the column's blocks above it.
        var camp = scene.Point("camp.guide");
        var trunkBase = grid.BlocksOfKind(BlockId.Trunk)
            .GroupBy(static p => (p.X, p.Z))
            .Select(g => (Column: g.Key, Base: g.Min(static p => p.Y)))
            .OrderBy(t => Math.Abs(t.Column.X - camp.X) + Math.Abs(t.Column.Z - camp.Z))
            .Select(t => new BlockPos(t.Column.X, t.Base, t.Column.Z))
            .First();
        var trunk = Enumerable.Range(trunkBase.Y, grid.SizeY - trunkBase.Y).TakeWhile(y => grid.GetBlock(trunkBase.X, y, trunkBase.Z) == BlockId.Trunk).ToList();
        Assert.InRange(trunk.Count, 4, 5);

        // Stand on the nearest open ground cell beside the trunk and gather the whole trunk.
        var sim = VoxelSimulation.Create(scene);
        var stand = new[] { (0, 1), (0, -1), (1, 0), (-1, 0) }
            .Select(o => new BlockPos(trunkBase.X + o.Item1, trunkBase.Y, trunkBase.Z + o.Item2))
            .First(c => !grid.IsSolid(c.X, c.Y, c.Z) && !grid.IsSolid(c.X, c.Y + 1, c.Z) && grid.IsSolid(c.X, c.Y - 1, c.Z));
        sim.TeleportTo(stand);
        foreach (var y in trunk)
        {
            Run(sim, BlockDefinition.TrunkGatherTicks, VoxelCommand.Interact(trunkBase.X, y, trunkBase.Z));
        }

        Assert.Equal(trunk.Count, sim.Inventory.Count(ItemId.Wood));
        Assert.All(trunk, y => Assert.Equal(BlockId.Air, grid.GetBlock(trunkBase.X, y, trunkBase.Z)));
    }

    [Fact]
    public void PlacingUsesUpTheItemAndRaisesTheEvent()
    {
        var sim = FlatSim();
        sim.GrantItems(ItemId.Wood, 3);
        var cell = new BlockPos(32, GroundTop + 1, 30); // on the ground, two blocks ahead
        sim.Step([VoxelCommand.Place(ItemId.Wood, cell.X, cell.Y, cell.Z)]);
        Assert.Equal(BlockId.Planks, sim.Grid.GetBlock(cell.X, cell.Y, cell.Z));
        Assert.Equal(2, sim.Inventory.Count(ItemId.Wood));
        var placed = Assert.Single(sim.Events, e => e.Kind == SimulationEventKind.BlockPlaced);
        Assert.Equal((cell.X, cell.Y, cell.Z, (int)BlockId.Planks, (int)ItemId.Wood), (placed.X, placed.Y, placed.Z, placed.Detail, placed.Amount));

        // The same cell is taken now; an item the player lacks places nothing; a cell in the air with nothing next to it is refused.
        sim.Step([VoxelCommand.Place(ItemId.Wood, cell.X, cell.Y, cell.Z)]);
        sim.Step([VoxelCommand.Place(ItemId.Torch, cell.X + 1, cell.Y, cell.Z)]);
        sim.Step([VoxelCommand.Place(ItemId.Wood, 32, GroundTop + 4, 31)]);
        Assert.Equal(2, sim.Inventory.Count(ItemId.Wood));
        Assert.DoesNotContain(sim.Events, e => e.Kind == SimulationEventKind.BlockPlaced);
        Assert.Equal(BlockId.Air, sim.Grid.GetBlock(32, GroundTop + 4, 31));
    }

    [Fact]
    public void PlacingIsLimitedByReachAndNeverBuriesThePlayer()
    {
        var sim = FlatSim();
        sim.GrantItems(ItemId.Wood, 5);
        sim.Step([VoxelCommand.Place(ItemId.Wood, 32, GroundTop + 1, 20)]); // 12 blocks away
        Assert.Equal(BlockId.Air, sim.Grid.GetBlock(32, GroundTop + 1, 20));
        sim.Step([VoxelCommand.Place(ItemId.Wood, 32, GroundTop + 1, 32)]); // the player's own cell
        sim.Step([VoxelCommand.Place(ItemId.Wood, 32, GroundTop + 2, 32)]); // the player's head cell
        Assert.Equal(BlockId.Air, sim.Grid.GetBlock(32, GroundTop + 1, 32));
        Assert.Equal(BlockId.Air, sim.Grid.GetBlock(32, GroundTop + 2, 32));
        Assert.Equal(5, sim.Inventory.Count(ItemId.Wood));

        // A cell the body only touches along an edge is fine.
        sim.Step([VoxelCommand.Place(ItemId.Wood, 33, GroundTop + 1, 32)]);
        Assert.Equal(BlockId.Planks, sim.Grid.GetBlock(33, GroundTop + 1, 32));
    }

    [Fact]
    public void ATorchCraftedAtTheWorkbenchCanBePlacedInTheFireRing()
    {
        var scene = BuildRoute(42);
        var sim = VoxelSimulation.Create(scene);
        var torch = new Recipe(ItemId.Torch, 1, [new Ingredient(ItemId.Wood, 2)], BlockId.Table);
        sim.SetRecipes([torch]);
        sim.GrantItems(ItemId.Wood, 2);

        // The workbench is at the guide's camp, a long walk from the player's camp where the player starts, and far from the village square.
        var workbench = scene.Point("workbench");
        Assert.False(sim.StationInReach(BlockId.Table));
        var village = scene.Point("village");
        sim.TeleportTo(new BlockPos(village.X, village.Y + 1, village.Z));
        Assert.False(sim.StationInReach(BlockId.Table));
        Assert.Equal(CraftResult.MissingStation, sim.CanCraft(0));
        sim.Step([VoxelCommand.Craft(0)]);
        Assert.Equal(0, sim.Inventory.Count(ItemId.Torch));

        sim.TeleportTo(new BlockPos(workbench.X, workbench.Y + 1, workbench.Z + 2));
        Assert.True(sim.StationInReach(BlockId.Table));
        Assert.Equal(CraftResult.Done, sim.CanCraft(0));
        sim.Step([VoxelCommand.Craft(0)]);
        Assert.Equal(1, sim.Inventory.Count(ItemId.Torch));
        Assert.Equal(0, sim.Inventory.Count(ItemId.Wood));
        var crafted = Assert.Single(sim.Events, e => e.Kind == SimulationEventKind.ItemCrafted);
        Assert.Equal(((int)ItemId.Torch, 1), (crafted.Detail, crafted.Amount));
        Assert.Equal(CraftResult.NoSuchRecipe, sim.CanCraft(5));
        Assert.Equal(CraftResult.MissingIngredients, sim.CanCraft(0));

        // Light the unlit fire ring: stand next to it and put the torch on its middle cell.
        var ring = scene.Point("firering");
        sim.TeleportTo(new BlockPos(ring.X, ring.Y + 1, ring.Z + 3));
        var centre = new BlockPos(ring.X, ring.Y + 1, ring.Z);
        Assert.Equal(BlockId.Air, sim.Grid.GetBlock(centre.X, centre.Y, centre.Z));
        sim.Step([VoxelCommand.Place(ItemId.Torch, centre.X, centre.Y, centre.Z)]);
        Assert.Equal(BlockId.Torch, sim.Grid.GetBlock(centre.X, centre.Y, centre.Z));
        Assert.Equal(0, sim.Inventory.Count(ItemId.Torch));
    }

    [Fact]
    public void BlocksPlacedAndGatheredAreInTheStateDigestAndTheRunReplays()
    {
        string Play()
        {
            var sim = FlatSim(g => g.SetBlock(Ahead.X, Ahead.Y, Ahead.Z, BlockId.Trunk));
            Run(sim, 60, InteractAhead);
            sim.Step([VoxelCommand.Place(ItemId.Wood, 32, GroundTop + 1, 30)]);
            sim.Step([VoxelCommand.SelectSlot(3)]);
            return sim.ComputeStateDigest();
        }

        Assert.Equal(Play(), Play());
        var other = FlatSim(g => g.SetBlock(Ahead.X, Ahead.Y, Ahead.Z, BlockId.Trunk));
        Run(other, 60, InteractAhead);
        other.Step([VoxelCommand.Place(ItemId.Wood, 32, GroundTop + 1, 30)]);
        other.Step([VoxelCommand.SelectSlot(2)]);
        Assert.NotEqual(Play(), other.ComputeStateDigest());
    }

    [Fact]
    public void TheHotbarSelectionIgnoresSlotsOutsideTheHotbar()
    {
        var sim = FlatSim();
        sim.Step([VoxelCommand.SelectSlot(4)]);
        Assert.Equal(4, sim.SelectedSlot);
        sim.Step([VoxelCommand.SelectSlot(25)]);
        Assert.Equal(4, sim.SelectedSlot);
        sim.Inventory.SetSlot(4, new ItemStack(ItemId.Torch, 2));
        Assert.Equal(ItemId.Torch, sim.SelectedStack.Item);
    }

    [Fact]
    public void AnUnknownCommandKindIsIgnored()
    {
        var sim = FlatSim();
        var before = sim.ComputeStateDigest();
        sim.Step([new VoxelCommand((VoxelCommandKind)99)]);
        var afterOneTick = sim.ComputeStateDigest();
        var control = FlatSim();
        control.Step();
        Assert.NotEqual(before, afterOneTick); // the tick counter moved
        Assert.Equal(control.ComputeStateDigest(), afterOneTick);
    }
}
