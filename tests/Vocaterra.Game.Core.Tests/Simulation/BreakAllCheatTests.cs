using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using static Vocaterra.Game.Core.Tests.Voxel.VoxelTestSupport;

namespace Vocaterra.Game.Core.Tests.Simulation;

/// <summary>The break-all cheat: the gather button breaks any block but bedrock and the liquids, a block that gives nothing in a short hold and for nothing; it is a setting in the Cheats group, saved with the others.</summary>
public sealed class BreakAllCheatTests
{
    // In the flat scene the player stands at (32.5, 10, 32.5); a block at (32, 11, 30) is about 2 blocks ahead (north).
    private static readonly BlockPos Ahead = new(32, GroundTop + 2, 30);

    private static VoxelSimulation WithBlock(BlockId block) => FlatSim(g => g.SetBlock(Ahead.X, Ahead.Y, Ahead.Z, block));

    private static VoxelCommand InteractAhead => VoxelCommand.Interact(Ahead.X, Ahead.Y, Ahead.Z);

    [Fact]
    public void Given_TheCheatOff_When_StoneIsHeld_Then_ItStays()
    {
        var sim = WithBlock(BlockId.Stone);

        Run(sim, 120, InteractAhead);

        Assert.Equal(BlockId.Stone, sim.Grid.GetBlock(Ahead.X, Ahead.Y, Ahead.Z));
        Assert.Null(LiveVoxelInput.GatherFor(new BlockRayHit(Ahead, BlockId.Stone, BlockFace.Up, Ahead, 0)));
    }

    [Theory]
    [InlineData(BlockId.Stone)]
    [InlineData(BlockId.Dirt)]
    [InlineData(BlockId.Grass)]
    [InlineData(BlockId.Leaves)]
    [InlineData(BlockId.Planks)]
    public void Given_TheCheatOn_When_ABlockThatGivesNothingIsHeld_Then_ItBreaksInTheCheatsTicksAndGivesNothing(BlockId block)
    {
        var sim = WithBlock(block);
        sim.BreakAll = true;
        Assert.Equal(VoxelSimulation.BreakAllTicks, sim.GatherTicksOf(block));

        Run(sim, VoxelSimulation.BreakAllTicks - 1, InteractAhead);
        Assert.Equal(block, sim.Grid.GetBlock(Ahead.X, Ahead.Y, Ahead.Z));
        Assert.Equal((VoxelSimulation.BreakAllTicks - 1, VoxelSimulation.BreakAllTicks), (sim.Events.Single().Detail, sim.Events.Single().Amount)); // the bar's progress
        sim.Step([InteractAhead]);

        Assert.Equal(BlockId.Air, sim.Grid.GetBlock(Ahead.X, Ahead.Y, Ahead.Z));
        Assert.Equal([SimulationEventKind.GatherProgress, SimulationEventKind.BlockRemoved], sim.Events.Select(static e => e.Kind));
        Assert.Equal((int)block, sim.Events.Single(static e => e.Kind == SimulationEventKind.BlockRemoved).Detail);
        Assert.All(sim.Inventory.Slots, static s => Assert.True(s.IsEmpty));
    }

    [Fact]
    public void Given_TheCheatOn_When_ATrunkIsHeld_Then_ItGathersAsUsual()
    {
        var sim = WithBlock(BlockId.Trunk);
        sim.BreakAll = true;
        Assert.Equal(BlockDefinition.TrunkGatherTicks, sim.GatherTicksOf(BlockId.Trunk));

        Run(sim, BlockDefinition.TrunkGatherTicks, InteractAhead);

        Assert.Equal(BlockId.Air, sim.Grid.GetBlock(Ahead.X, Ahead.Y, Ahead.Z));
        Assert.Equal(1, sim.Inventory.Count(ItemId.Wood));
    }

    [Fact]
    public void Given_TheCheatOnAndAFullInventory_When_ATrunkIsHeld_Then_ItBreaksForNothing()
    {
        var sim = WithBlock(BlockId.Trunk);
        sim.BreakAll = true;
        for (var slot = 0; slot < sim.Inventory.Slots.Count; slot++)
        {
            sim.Inventory.SetSlot(slot, new ItemStack(TestItems.Axe, 1)); // tools do not stack: nothing else fits
        }

        Assert.False(sim.Inventory.CanAdd(ItemId.Wood, 1));

        Run(sim, VoxelSimulation.BreakAllTicks, InteractAhead);

        Assert.Equal(BlockId.Air, sim.Grid.GetBlock(Ahead.X, Ahead.Y, Ahead.Z));
        Assert.Equal(0, sim.Inventory.Count(ItemId.Wood));
    }

    [Theory]
    [InlineData(BlockId.Bedrock)]
    [InlineData(BlockId.Water)]
    [InlineData(BlockId.Lava)]
    [InlineData(BlockId.Air)]
    public void Given_TheCheat_When_AskedAboutBedrockTheLiquidsOrAir_Then_ItLeavesThem(BlockId block)
    {
        Assert.False(VoxelSimulation.BreaksUnderCheat(block));
    }

    [Fact]
    public void Given_TheCheatOn_When_BedrockIsHeld_Then_ItStays()
    {
        var sim = WithBlock(BlockId.Bedrock);
        sim.BreakAll = true;

        Run(sim, 120, InteractAhead);

        Assert.Equal(BlockId.Bedrock, sim.Grid.GetBlock(Ahead.X, Ahead.Y, Ahead.Z));
    }

    [Fact]
    public void Given_TheLiveInput_When_TheCheatIsOn_Then_TheGatherButtonAimsAtStone()
    {
        var stone = new BlockRayHit(Ahead, BlockId.Stone, BlockFace.Up, Ahead, 0);
        var bedrock = new BlockRayHit(Ahead, BlockId.Bedrock, BlockFace.Up, Ahead, 0);

        Assert.Equal(Ahead, LiveVoxelInput.GatherFor(stone, breakAll: true));
        Assert.Null(LiveVoxelInput.GatherFor(bedrock, breakAll: true));
    }

    [Fact]
    public void Given_TheBreakAllSetting_When_Used_Then_ItIsOffByDefaultSavedWithTheSettingsAndShownUnderTheFlyRows()
    {
        var settings = new PlayerSettings();
        Assert.False(settings.BreakAll);
        Assert.False(settings.ToSave().BreakAllCheat);
        Assert.DoesNotContain("breakAll", System.Text.Json.JsonSerializer.Serialize(settings.ToSave()), StringComparison.OrdinalIgnoreCase);

        settings.Set(SettingId.BreakAll, 1);

        Assert.True(settings.BreakAll);
        var saved = settings.ToSave();
        Assert.True(saved.BreakAllCheat);
        Assert.True(PlayerSettings.Resolve(saved).BreakAll);
        var ids = MenuRows.Settings(settings, null).Select(static r => r.Id).ToList();
        Assert.Equal(ids.IndexOf(nameof(SettingId.FlySpeed)) + 1, ids.IndexOf(nameof(SettingId.BreakAll)));
        Assert.Equal("Break all blocks (cheat)", TestGame.Locale.Ui(PlayerSettings.Spec(SettingId.BreakAll).LabelKey));
    }
}
