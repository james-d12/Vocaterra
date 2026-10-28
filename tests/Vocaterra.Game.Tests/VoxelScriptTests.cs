using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Utilities;

namespace Vocaterra.Game.Tests;

public sealed class VoxelScriptTests
{
    private const double U = VoxelPhysics.UnitsPerBlock;

    private sealed class Run
    {
        public Run(params VoxelStep[] steps)
        {
            Scene = RouteScene.Clearing();
            Sim = VoxelSimulation.Create(Scene);
            Look = new LookControl();
            Look.Set(Scene.Spawn.Yaw, 0);
            Script = new VoxelScript(Sim, Look, steps, ResolveItem, Log.Add);
        }

        public Scene Scene { get; }

        public VoxelSimulation Sim { get; }

        public LookControl Look { get; }

        public VoxelScript Script { get; }

        public List<string> Log { get; } = [];

        public int Ticks { get; private set; }

        public (double X, double Z) Feet => (Sim.Player.CenterX / U, Sim.Player.CenterZ / U);

        public void Go(int limit = 20000)
        {
            var commands = new List<VoxelCommand>();
            while (!Script.IsDone && Ticks < limit)
            {
                Script.Fill(commands);
                commands.Insert(0, VoxelCommand.Look(Look.Yaw, Look.Pitch));
                Sim.Step(commands);
                Ticks++;
            }

            Assert.True(Script.IsDone, "the script did not finish");
        }

        private static ItemId? ResolveItem(string name) => name switch
        {
            "madera" or "wood" => ItemId.Wood,
            "antorcha" => ItemId.Torch,
            _ => null,
        };
    }

    [Fact]
    public void WalkToArrivesWithinHalfABlockAndReportsIt()
    {
        var run = new Run(VoxelStep.WalkTo(22, 17));
        run.Go();
        var (x, z) = run.Feet;
        Assert.InRange(Math.Sqrt(Math.Pow(x - 22.5, 2) + Math.Pow(z - 17.5, 2)), 0, VoxelScript.ArrivedDistance);
        Assert.Contains(run.Log, static l => l.StartsWith("walk-to 22,17: arrived", StringComparison.Ordinal));
        Assert.InRange(run.Ticks, 20, 200);
    }

    [Fact]
    public void WalkToCrossesTheClearingToTheVillageOfTheDemo()
    {
        var run = new Run(VoxelStep.WalkTo(60, 52));
        run.Go();
        var (x, z) = run.Feet;
        Assert.InRange(Math.Sqrt(Math.Pow(x - 60.5, 2) + Math.Pow(z - 52.5, 2)), 0, VoxelScript.ArrivedDistance);
        Assert.Contains(run.Log, static l => l.Contains("arrived", StringComparison.Ordinal));
    }

    [Fact]
    public void WalkToGivesUpAtTheTickLimitWhenTheTargetCannotBeReached()
    {
        // A column outside the world: the body stops at the edge and the script reports that it gave up instead of walking for ever.
        var run = new Run(VoxelStep.WalkTo(200, 17));
        run.Go();
        Assert.InRange(run.Ticks, VoxelScript.WalkTickLimit, VoxelScript.WalkTickLimit + 1); // the tick of the last call still runs
        Assert.Contains(run.Log, static l => l.StartsWith("walk-to 200,17: gave up", StringComparison.Ordinal));
        Assert.True(run.Feet.X < 96);
    }

    [Fact]
    public void WalkToIsDeterministic()
    {
        var a = new Run(VoxelStep.WalkTo(60, 52));
        var b = new Run(VoxelStep.WalkTo(60, 52));
        a.Go();
        b.Go();
        Assert.Equal(a.Ticks, b.Ticks);
        Assert.Equal(a.Sim.ComputeStateDigest(), b.Sim.ComputeStateDigest());
    }

    [Fact]
    public void LookSetsTheViewInDegreesWithoutTakingATick()
    {
        var run = new Run(VoxelStep.Look(120, -5), VoxelStep.Look(-90, 30));
        var commands = new List<VoxelCommand>();
        run.Script.Fill(commands);
        Assert.True(run.Script.IsDone);
        Assert.Empty(commands);
        Assert.Equal(270.0, run.Look.YawDegrees, 1);
        Assert.Equal(30.0, run.Look.PitchDegrees, 1);
        Assert.Equal(["look: yaw 120, pitch -5", "look: yaw -90, pitch 30"], run.Log);
    }

    [Fact]
    public void InteractHoldsUntilTheTrunkIsGatheredAndGivesWood()
    {
        var run = new Run(VoxelStep.WalkTo(11, 17), VoxelStep.Interact(9, 11, 17));
        Assert.Equal(BlockId.Trunk, run.Sim.Grid.GetBlock(9, 11, 17));
        run.Go();
        Assert.Equal(BlockId.Air, run.Sim.Grid.GetBlock(9, 11, 17));
        Assert.Equal(1, run.Sim.Inventory.Count(ItemId.Wood));
        Assert.Contains(run.Log, static l => l.Contains("gathered trunk in 60 ticks", StringComparison.Ordinal) && l.Contains("+1 wood", StringComparison.Ordinal));
    }

    [Fact]
    public void InteractAimsTheViewAtTheBlock()
    {
        var run = new Run(VoxelStep.WalkTo(11, 17), VoxelStep.Interact(9, 11, 17));
        run.Go();
        // The trunk is two and a half blocks to the west and a little below the eye: the view faces west (270 degrees) and down by 24 degrees.
        Assert.InRange(run.Look.YawDegrees, 265, 275);
        Assert.InRange(run.Look.PitchDegrees, -30, -18);
    }

    [Fact]
    public void InteractOnABerryBushLeavesAPlainBush()
    {
        var run = new Run(VoxelStep.WalkTo(16, 20), VoxelStep.Interact(15, 11, 19));
        Assert.Equal(BlockId.BerryBush, run.Sim.Grid.GetBlock(15, 11, 19));
        run.Go();
        Assert.Equal(BlockId.Bush, run.Sim.Grid.GetBlock(15, 11, 19));
        Assert.Equal(1, run.Sim.Inventory.Count(ItemId.Berry));
        Assert.Contains(run.Log, static l => l.Contains("gathered berry-bush", StringComparison.Ordinal) && l.Contains("+1 berry", StringComparison.Ordinal));
    }

    [Fact]
    public void InteractOutOfReachOrOnAnythingNotGatherableStopsAtTheTickLimit()
    {
        var far = new Run(VoxelStep.Interact(9, 11, 17)); // the spawn is eight blocks from this trunk
        far.Go();
        Assert.InRange(far.Ticks, VoxelScript.InteractTickLimit, VoxelScript.InteractTickLimit + 1);
        Assert.Contains(far.Log, static l => l.Contains("not gathered", StringComparison.Ordinal));
        Assert.Equal(BlockId.Trunk, far.Sim.Grid.GetBlock(9, 11, 17));

        var air = new Run(VoxelStep.Interact(18, 12, 17));
        air.Go();
        Assert.Contains(air.Log, static l => l.Contains("nothing there", StringComparison.Ordinal));
        Assert.True(air.Ticks < 5);
    }

    [Fact]
    public void PlaceNamesTheItemAsANounOrItsKeyAndPutsItInTheCell()
    {
        var run = new Run(VoxelStep.Place("madera", 18, 11, 17));
        Assert.Equal(0, run.Sim.GrantItems(ItemId.Wood, 3));
        Assert.Equal(BlockId.Air, run.Sim.Grid.GetBlock(18, 11, 17));
        run.Go();
        Assert.Equal(BlockId.Planks, run.Sim.Grid.GetBlock(18, 11, 17));
        Assert.Equal(2, run.Sim.Inventory.Count(ItemId.Wood));
        Assert.Contains(run.Log, static l => l.Contains("placed planks", StringComparison.Ordinal));
    }

    [Fact]
    public void PlaceReportsARefusalAndAnUnknownItem()
    {
        var empty = new Run(VoxelStep.Place("madera", 18, 11, 17)); // nothing in the inventory
        empty.Go();
        Assert.Equal(BlockId.Air, empty.Sim.Grid.GetBlock(18, 11, 17));
        Assert.Contains(empty.Log, static l => l.Contains("refused", StringComparison.Ordinal));

        var unknown = new Run(VoxelStep.Place("espada", 18, 11, 17));
        unknown.Go();
        Assert.Contains(unknown.Log, static l => l.Contains("no item called 'espada'", StringComparison.Ordinal));
    }

    [Fact]
    public void SelectChoosesTheHotbarSlotOneToTen()
    {
        var run = new Run(VoxelStep.Select(3));
        run.Go();
        Assert.Equal(2, run.Sim.SelectedSlot);
        var last = new Run(VoxelStep.Select(10));
        last.Go();
        Assert.Equal(9, last.Sim.SelectedSlot);
    }

    [Fact]
    public void StepsRunOneAfterTheOtherInTheOrderGiven()
    {
        var run = new Run(VoxelStep.Look(90, 0), VoxelStep.WalkTo(20, 17), VoxelStep.Select(2), VoxelStep.Look(0, -10));
        run.Go();
        Assert.Equal(4, run.Log.Count);
        Assert.StartsWith("look", run.Log[0], StringComparison.Ordinal);
        Assert.StartsWith("walk-to", run.Log[1], StringComparison.Ordinal);
        Assert.StartsWith("select", run.Log[2], StringComparison.Ordinal);
        Assert.StartsWith("look", run.Log[3], StringComparison.Ordinal);
        Assert.Equal(1, run.Sim.SelectedSlot);
        Assert.Equal(-10.0, run.Look.PitchDegrees, 1);
    }

    [Fact]
    public void ParsingHelpersReadColumnsAndBlocks()
    {
        Assert.True(VoxelStep.TryParseColumn("60,52", out var x, out var z));
        Assert.Equal((60, 52), (x, z));
        Assert.False(VoxelStep.TryParseColumn("60,52,3", out _, out _));
        Assert.True(VoxelStep.TryParseBlock("9,11,17", out var bx, out var by, out var bz));
        Assert.Equal((9, 11, 17), (bx, by, bz));
        Assert.False(VoxelStep.TryParseBlock("9,11", out _, out _, out _));
        Assert.False(VoxelStep.TryParseBlock("9,-1,17", out _, out _, out _));
    }
}
