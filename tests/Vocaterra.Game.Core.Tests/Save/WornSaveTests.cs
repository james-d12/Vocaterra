using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>Save version 10 (I03b): the bag is saved at its active size and the worn items by group id, slot name and item name; what the data lacks loads as nothing, and a migration reports it.</summary>
public sealed class WornSaveTests
{
    private const string OtherBundle = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly EquipSlot Back = GearItems.Slot("backpack", "back");
    private static readonly EquipSlot Body = GearItems.Slot("clothing", "body");

    private static Scene Pristine(ulong seed = TestGame.Seed) => TestGame.Route(seed);

    private static VoxelSession Dressed()
    {
        var session = GearItems.Session();
        var sim = session.Simulation;
        Assert.Equal(EquipResult.Done, sim.Equip(GearItems.Bag(sim, GearItems.Shirt, 2), Body));
        Assert.Equal(EquipResult.Done, sim.Equip(GearItems.Bag(sim, GearItems.Rucksack, 3), Back));
        sim.Inventory.SetSlot(47, new ItemStack(TestItems.Apple, 2));
        return session;
    }

    private static VoxelSession Load(SaveDocument document, VoxelSession origin, bool migrate = false) =>
        SaveGame.Load(SaveGame.Serialize(document), origin.Data, Pristine, credit: CreditRules.FullTyped, bumpSession: false, migrate: migrate);

    [Fact]
    public void Given_NothingWorn_When_TheGameIsCaptured_Then_TheBagHasThirtySlotsAndNoWornEntryIsWritten()
    {
        var document = SaveGame.Capture(GearItems.Session(), Pristine());

        Assert.Equal(10, document.Version);
        Assert.Equal(30, document.Inventory.Count);
        Assert.Null(document.Worn);
        Assert.DoesNotContain("\"worn\"", SaveGame.Serialize(document), StringComparison.Ordinal);
    }

    [Fact]
    public void Given_AShirtAndALargeBagWorn_When_TheGameIsCaptured_Then_TheBagHasFiftySlotsAndTheWornItemsAreNamedByGroupSlotAndItem()
    {
        var document = SaveGame.Capture(Dressed(), Pristine());

        Assert.Equal(50, document.Inventory.Count);
        Assert.Equal(
            [new WornSave("clothing", "body", "test-shirt"), new WornSave("backpack", "back", "test-rucksack")],
            document.Worn);
        var text = SaveGame.Serialize(document);
        Assert.Contains("\"worn\":[{\"group\":\"clothing\",\"slot\":\"body\",\"item\":\"test-shirt\"}", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_WornItemsAndAFullActiveBag_When_TheGameIsSavedAndLoaded_Then_TheItemsTheCountAndTheDigestComeBack()
    {
        var session = Dressed();
        session.Step([]);
        var document = SaveGame.Capture(session, Pristine());

        var loaded = Load(document, session);

        Assert.Equal(GearItems.Shirt, loaded.Simulation.Equipment.Get(Body).Item);
        Assert.Equal(GearItems.Rucksack, loaded.Simulation.Equipment.Get(Back).Item);
        Assert.Equal(50, loaded.Simulation.Inventory.SlotCount);
        Assert.Equal(new ItemStack(TestItems.Apple, 2), loaded.Simulation.Inventory.Slots[47]);
        Assert.Equal(session.Simulation.ComputeStateDigest(), loaded.Simulation.ComputeStateDigest());
        Assert.Equal(SaveGame.Serialize(document), SaveGame.Serialize(SaveGame.Capture(loaded, Pristine()))); // byte for byte
    }

    [Fact]
    public void Given_ASaveNamingAnUnknownGroupSlotAndItem_When_ItLoads_Then_TheRestLoadsAndTheUnknownIsNothing()
    {
        var session = Dressed();
        var document = SaveGame.Capture(session, Pristine());
        document = document with
        {
            Worn =
            [
                .. document.Worn!,
                new WornSave("wings", "left", "test-shirt"), // no such group
                new WornSave("clothing", "tail", "test-shirt"), // no such slot
                new WornSave("clothing", "head", "no-such-hat"), // no such item
            ],
        };

        var loaded = Load(document, session);

        Assert.Equal(GearItems.Shirt, loaded.Simulation.Equipment.Get(Body).Item);
        Assert.Equal(GearItems.Rucksack, loaded.Simulation.Equipment.Get(Back).Item);
        Assert.Equal(2, loaded.Simulation.Equipment.Worn().Count());
        Assert.Equal(session.Simulation.ComputeStateDigest(), loaded.Simulation.ComputeStateDigest());
    }

    [Fact]
    public void Given_TheGroupsOfEquipmentJsonInAnotherOrder_When_TheSaveIsRead_Then_EachItemLandsInItsOwnSlot()
    {
        var data = GearItems.Data;
        var reversed = new EquipmentData([.. data.Groups.Reverse()], data.BagSlots);
        var worn = new[] { new WornSave("clothing", "body", "test-shirt"), new WornSave("backpack", "back", "test-rucksack") };

        var stacks = WornSave.ToStacks(worn, reversed, GearItems.Catalogue)!;

        Assert.Equal(GearItems.Shirt, stacks[reversed.FlatIndex(GearItems.Slot(reversed, "clothing", "body"))].Item);
        Assert.Equal(GearItems.Rucksack, stacks[reversed.FlatIndex(GearItems.Slot(reversed, "backpack", "back"))].Item);
        Assert.Equal(2, stacks.Count(static s => !s.IsEmpty));
        Assert.NotEqual(data.FlatIndex(Body), reversed.FlatIndex(GearItems.Slot(reversed, "clothing", "body"))); // the indexes did move
    }

    [Fact]
    public void Given_NoEntriesOrOnlyUnknownOnes_When_TheyAreRead_Then_NothingIsWorn()
    {
        Assert.Null(WornSave.ToStacks(null, GearItems.Data, GearItems.Catalogue));
        Assert.Null(WornSave.ToStacks([], GearItems.Data, GearItems.Catalogue));
        Assert.Null(WornSave.ToStacks([new WornSave("wings", "left", "test-shirt")], GearItems.Data, GearItems.Catalogue));
        Assert.Null(WornSave.Of(new Equipment(GearItems.Data, GearItems.Catalogue)));
    }

    [Fact]
    public void Given_ASaveWhoseBagSlotsDoNotMatchItsWornBag_When_ItLoads_Then_ItIsRefused()
    {
        var session = Dressed();
        var document = SaveGame.Capture(session, Pristine());

        _ = Assert.Throws<SaveException>(() => Load(document with { Inventory = document.Inventory.Take(30).ToArray() }, session));
        _ = Assert.Throws<SaveException>(() => Load(document with { Worn = null }, session));
    }

    [Fact]
    public void Given_ASaveOfAnotherBundleWhoseLargeBagIsGone_When_ItIsMigrated_Then_TheBagShrinksAndTheDroppedAreReported()
    {
        var session = Dressed();
        var document = SaveGame.Capture(session, Pristine());
        document = document with
        {
            BundleHash = OtherBundle,
            Worn = [new WornSave("clothing", "body", "test-shirt"), new WornSave("backpack", "back", "gone-bag")],
        };

        var loaded = Load(document, session, migrate: true);

        Assert.Equal(30, loaded.Simulation.Inventory.SlotCount);
        Assert.Equal(GearItems.Shirt, loaded.Simulation.Equipment.Get(Body).Item);
        Assert.True(loaded.Simulation.Equipment.Get(Back).IsEmpty);
        var report = loaded.Migration!;
        Assert.Contains("backpack/back: gone-bag", report.Find("worn items")!.Dropped);
        Assert.Contains("apple", report.Find("inventory slots")!.Dropped); // the slot past the new count held an apple
        Assert.Equal(1, report.Find("worn items")!.Kept);
    }
}
