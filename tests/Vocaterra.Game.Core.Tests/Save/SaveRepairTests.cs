using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>
/// I10b (QA-2026-11-19-save-1): a save of this bundle that names an item the bundle lacks, wears an item where its slot does not take it or gives a slot no count. What the data lacks loads as nothing and is logged
/// (the digest, taken with the item, is then only a report); a worn item in a group that does not take it goes to the bag; a slot with no count and a bag of the wrong size are refused with a sentence.
/// </summary>
public sealed class SaveRepairTests
{
    private static Scene Pristine(ulong seed = TestGame.Seed) => TestGame.Route(seed);

    private static VoxelSession Dressed()
    {
        var session = GearItems.Session();
        var sim = session.Simulation;
        sim.Inventory.SetSlot(20, new ItemStack(GearItems.Helm, 1));
        Assert.Equal(EquipResult.Done, sim.Equip(20, GearItems.Slot("armour", "head")));
        sim.Inventory.SetSlot(3, new ItemStack(TestItems.Apple, 4));
        session.Step([]);
        return session;
    }

    private static VoxelSession Load(SaveDocument document, VoxelSession origin) =>
        SaveGame.Load(SaveGame.Serialize(document), origin.Data, Pristine, credit: CreditRules.FullTyped, bumpSession: false);

    private static IEnumerable<string?> Repairs(VoxelSession session) =>
        session.Events.OfType<WorldEvent>().Where(static e => e.Name == WorldEvents.SaveRepaired).Select(static e => e.Detail);

    [Fact]
    public void Given_AWornItemTheBundleLacks_When_TheSaveIsLoaded_Then_ItLoadsWithThatSlotEmptyAndSaysWhich()
    {
        var origin = Dressed();
        var document = SaveGame.Capture(origin) with { Worn = [new WornSave("armour", "head", "dragon-helm")] };

        var loaded = Load(document, origin);

        Assert.True(loaded.Simulation.Equipment.IsEmpty);
        var repair = Assert.Single(Repairs(loaded));
        Assert.Contains("dragon-helm", repair, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_ABagItemTheBundleLacks_When_TheSaveIsLoaded_Then_ThatSlotIsEmptyAndTheRestStays()
    {
        var origin = Dressed();
        var inventory = SaveGame.Capture(origin).Inventory.ToArray();
        inventory[3] = new SlotSave("dragon-egg", 2);

        var loaded = Load(SaveGame.Capture(origin) with { Inventory = inventory }, origin);

        Assert.True(loaded.Simulation.Inventory.Slots[3].IsEmpty);
        Assert.Equal(GearItems.Helm, loaded.Simulation.Equipment.Get(GearItems.Slot("armour", "head")).Item);
        Assert.Contains("bag slot 4", Assert.Single(Repairs(loaded)), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_ASlotWithAnItemAndNoCount_When_TheSaveIsLoaded_Then_ItIsRefusedWithAMessageNamingTheSlot()
    {
        var origin = Dressed();
        var inventory = SaveGame.Capture(origin).Inventory.ToArray();
        inventory[3] = new SlotSave("apple", 0);

        var refused = Assert.Throws<SaveException>(() => Load(SaveGame.Capture(origin) with { Inventory = inventory }, origin));

        Assert.Contains("bag slot 4", refused.Message, StringComparison.Ordinal);
        Assert.Contains("apple", refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Parameter", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_AWornItemInAGroupThatDoesNotTakeIt_When_TheSaveIsLoaded_Then_ItGoesToTheBagWithALogLine()
    {
        var origin = Dressed();
        var equipment = origin.Data.Equipment;
        var slotName = equipment.Groups[equipment.IndexOf("offhand")].Slots[0].Name;
        var document = SaveGame.Capture(origin) with { Worn = [new WornSave("offhand", slotName, "test-shirt")] };

        var loaded = Load(document, origin);

        Assert.True(loaded.Simulation.Equipment.IsEmpty);
        Assert.Equal(1, loaded.Simulation.Inventory.Count(GearItems.Shirt));
        Assert.Contains("moved to bag slot", Assert.Single(Repairs(loaded)), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_AnInventoryOfTheWrongSize_When_TheSaveIsLoaded_Then_TheRefusalIsASentenceWithNoParameterName()
    {
        var origin = Dressed();
        var document = SaveGame.Capture(origin) with { Inventory = SaveGame.Capture(origin).Inventory.Take(12).ToArray() };

        var refused = Assert.Throws<SaveException>(() => Load(document, origin));

        Assert.StartsWith("The save does not restore. The inventory has 12 slots", refused.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Parameter", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_ADamagedSaveWithNothingToPutRight_When_TheSaveIsLoaded_Then_TheDigestStillRefusesIt()
    {
        var origin = Dressed();
        var document = SaveGame.Capture(origin) with { StateDigest = "0000" };

        var refused = Assert.Throws<SaveException>(() => Load(document, origin));

        Assert.Contains("digest", refused.Message, StringComparison.Ordinal);
    }
}
