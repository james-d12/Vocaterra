using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Scripted;

/// <summary>The <c>--wear</c> start aid and the <c>--repair</c> step (I07): the wear is set where the item is, and a repair is one command that the console reports.</summary>
public sealed class RepairStepTests
{
    private static List<string> Run(Core.Session.VoxelSession session, params VoxelStep[] steps)
    {
        var log = new List<string>();
        var script = new VoxelScript(session.Simulation, new LookControl(), steps, name => ScriptItemNames.Resolve(session.Nouns, session.Data.Items, name), log.Add);
        var commands = new List<VoxelCommand>();
        for (var tick = 0; tick < 50 && !script.IsDone; tick++)
        {
            commands.Clear();
            script.Fill(commands);
            session.Step(commands);
        }

        Assert.True(script.IsDone);
        return log;
    }

    private static Core.Session.VoxelSession AtTheTable()
    {
        var session = TestGame.NewSession();
        var feet = session.Simulation.Player.FeetBlock;
        session.Scene.Grid.SetBlock(feet.X + 1, feet.Y, feet.Z, BlockId.Table);
        return session;
    }

    [Fact]
    public void Given_NoAxe_When_ItsWearIsSet_Then_TheAxeIsGivenAndWornToThatAmount()
    {
        var session = TestGame.NewSession();

        var set = session.Simulation.SetWear(TestItems.Axe, 55);

        Assert.Equal(55, set);
        Assert.Equal(new ItemStack(TestItems.Axe, 1, 55), session.Simulation.Inventory.Slots.Single(static s => !s.IsEmpty && s.Item == TestItems.Axe));
    }

    [Fact]
    public void Given_AnAmountPastTheDurability_When_TheWearIsSet_Then_ItIsClampedAndTheToolIsWornOut()
    {
        var session = TestGame.NewSession();

        Assert.Equal(350, session.Simulation.SetWear(TestItems.Axe, 9999));
    }

    [Fact]
    public void Given_AWornShirt_When_ItsWearIsSet_Then_TheWornShirtTakesIt()
    {
        var session = TestGame.NewSession();
        var shirt = TestItems.Catalogue.Find("shirt")!.Id;

        Assert.Equal(100, session.Simulation.SetWear(shirt, 100));

        Assert.Equal(100, session.Simulation.Equipment.Worn().Select(w => session.Simulation.Equipment.Get(w.Slot)).Single(s => s.Item == shirt).Wear);
        Assert.Equal(0, session.Simulation.Inventory.Count(shirt)); // none was given: the worn one was set
    }

    [Fact]
    public void Given_AnItemWithNoDurability_When_ItsWearIsSet_Then_ItIsRefused()
    {
        var session = TestGame.NewSession();

        Assert.Equal(-1, session.Simulation.SetWear(TestItems.Apple, 3));
    }

    [Fact]
    public void Given_AWornAxeAndWoodAtTheTable_When_TheRepairStepRuns_Then_ItSaysItRepairedTheAxe()
    {
        var session = AtTheTable();
        _ = session.Simulation.SetWear(TestItems.Axe, 20);
        _ = session.Simulation.GrantItems(ItemId.Wood, 1);

        var log = Run(session, VoxelStep.Repair("hacha"));

        Assert.Equal(["repair: hacha repaired, still worn: False"], log);
    }

    [Fact]
    public void Given_NothingWorn_When_TheRepairStepRuns_Then_ItReportsThereIsNothingToRepair()
    {
        var session = AtTheTable();
        _ = session.Simulation.GrantItems(TestItems.Axe, 1);

        var log = Run(session, VoxelStep.Repair("hacha"));

        Assert.Equal(["repair: hacha refused (NothingToRepair)"], log);
    }

    [Fact]
    public void Given_AnItemWithNoRepairRow_When_TheRepairStepRuns_Then_ItSaysNoRecipeRepairsIt()
    {
        var log = Run(AtTheTable(), VoxelStep.Repair("manzana"));

        Assert.Equal(["repair: no recipe repairs manzana"], log);
    }

    [Fact]
    public void Given_TheAxe_When_TheCraftStepNamesIt_Then_TheRepairRowIsNotTakenForARecipe()
    {
        var log = Run(AtTheTable(), VoxelStep.Craft("hacha"));

        Assert.Equal(["craft: no recipe makes hacha"], log);
    }
}
