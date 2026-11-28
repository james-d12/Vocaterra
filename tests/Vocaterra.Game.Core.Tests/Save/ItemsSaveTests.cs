using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>Save version 9 (I01): a bag slot stores its item by the name of <c>game/items.json</c>, an empty slot is empty, and an item only the data knows is saved and loaded like any other.</summary>
public sealed class ItemsSaveTests
{
    private static readonly ItemId Gem = (ItemId)900;

    private static Scene Pristine(ulong seed = TestGame.Seed) => TestGame.Route(seed);

    private static VoxelSession SessionWithGem()
    {
        var items = TestItems.With(new ItemDefinition(Gem, "gem", 7, "gem", ItemHeld.Sprite, ["material"]));
        return new VoxelSession(TestGame.Data.WithItems(items), TestGame.Seed, GuideSession.DefaultStartedAt, Pristine(), CreditRules.FullTyped, TestGame.NoLessons);
    }

    [Fact]
    public void Given_ABag_When_ItIsCaptured_Then_EachSlotNamesItsItemAndAnEmptySlotHasNoNameAndNoCount()
    {
        var session = TestGame.NewSession();
        _ = session.Simulation.GrantItems(ItemId.Wood, 3);
        _ = session.Simulation.GrantItems(TestItems.Axe, 1);

        var inventory = SaveGame.Capture(session).Inventory;

        Assert.Equal(session.Simulation.Inventory.SlotCount, inventory.Count);
        Assert.Equal(30, inventory.Count);
        Assert.Contains(new SlotSave("wood", 3), inventory);
        Assert.Contains(new SlotSave("axe", 1), inventory);
        Assert.Equal(new SlotSave(string.Empty, 0), inventory[^1]);
        Assert.DoesNotContain(inventory, static slot => slot.Count == 0 && slot.Item.Length > 0);
    }

    [Fact]
    public void Given_AnItemOnlyTheDataKnows_When_TheBagIsSavedAndLoaded_Then_TheStacksComeBackAndTheDigestAgrees()
    {
        var session = SessionWithGem();
        Assert.Equal(0, session.Simulation.GrantItems(Gem, 10));
        _ = session.Simulation.GrantItems(ItemId.Berry, 2);
        var stacks = session.Simulation.Inventory.Slots.ToArray();
        var digest = session.Simulation.ComputeStateDigest();

        var text = SaveGame.Serialize(SaveGame.Capture(session));
        Assert.Contains("\"item\":\"gem\"", text, StringComparison.Ordinal);
        var loaded = SaveGame.Load(text, session.Data, Pristine, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.Equal(stacks, loaded.Simulation.Inventory.Slots);
        Assert.Equal(10, loaded.Simulation.Inventory.Count(Gem));
        Assert.Equal(new ItemStack(Gem, 7), loaded.Simulation.Inventory.Slots.First(static s => s.Item == (ItemId)900));
        Assert.Equal(digest, loaded.Simulation.ComputeStateDigest());
    }

    [Fact]
    public void Given_ASlotWithoutACount_When_ItIsRead_Then_ItIsTheEmptyStack()
    {
        Assert.Equal(ItemStack.Empty, new SlotSave(string.Empty, 0).ToStack(TestItems.Catalogue));
        Assert.Equal(ItemStack.Empty, new SlotSave("wood", 0).ToStack(TestItems.Catalogue));
        Assert.Equal(ItemStack.Empty, new SlotSave("no-such-item", 4).ToStack(TestItems.Catalogue));
        Assert.Equal(new ItemStack(ItemId.CampKit, 1), new SlotSave("camp-kit", 1).ToStack(TestItems.Catalogue));
        Assert.Equal(new SlotSave(string.Empty, 0), SlotSave.Of(ItemStack.Empty, TestItems.Catalogue));
        Assert.Equal(new SlotSave("water-bottle", 2), SlotSave.Of(new ItemStack(ItemId.WaterBottle, 2), TestItems.Catalogue));
    }

    [Fact]
    public void Given_TheVersion10Fixture_When_ItLoads_Then_TheBagHoldsTheItemsItNames()
    {
        var text = SaveFixture.Read("save-v11.json");

        var loaded = SaveGame.Load(text, TestGame.Data, Pristine, credit: CreditRules.FullTyped, bumpSession: false);

        Assert.Equal(3, loaded.Simulation.Inventory.Count(ItemId.Wood));
        Assert.Equal(1, loaded.Simulation.Inventory.Count(ItemId.Book));
        Assert.Equal(3, loaded.Simulation.Inventory.Count(ItemId.Berry));
        Assert.Equal(1, loaded.Simulation.Inventory.Count(ItemId.Bottle));
    }

    [Fact]
    public void Given_ASaveOfVersion11_When_ItIsRead_Then_ItIsRefusedWithTheMessageOfEveryOlderVersion()
    {
        var text = SaveFixture.Read("save-v11.json").Replace("\"version\":12,", "\"version\":11,", StringComparison.Ordinal);

        var error = Assert.Throws<SaveException>(() => SaveGame.Parse(text));

        Assert.Equal("The save is format version 11; the oldest this game loads is 12 (it writes 12), and older saves are not migrated.", error.Message);
        Assert.Equal(12, SaveGame.OldestReadableVersion);
        _ = Assert.Throws<SaveException>(() => SaveGame.Load(text, TestGame.Data, Pristine));
    }
}
