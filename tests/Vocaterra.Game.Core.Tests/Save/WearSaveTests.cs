using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.Items.Wearing;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Save;

/// <summary>Save version 11 (I07): a worn stack saves its wear, in the bag and on the body; no wear is left out of the file; the digest carries the wear; an older version is refused.</summary>
public sealed class WearSaveTests
{
    private static readonly EquipSlot Head = GearItems.Slot("clothing", "head");

    private static Scene Pristine(ulong seed = TestGame.Seed) => TestGame.Route(seed);

    private static VoxelSession Worn()
    {
        var session = GearItems.Session();
        var sim = session.Simulation;
        sim.Inventory.SetSlot(20, new ItemStack(GearItems.Cap, 1, 3));
        Assert.Equal(EquipResult.Done, sim.Equip(20, Head));
        sim.Inventory.SetSlot(2, new ItemStack(GearItems.Hatchet, 1, 2));
        sim.Inventory.SetSlot(15, new ItemStack(TestItems.Apple, 4));
        return session;
    }

    private static VoxelSession Load(SaveDocument document, VoxelSession origin, bool migrate = false) =>
        SaveGame.Load(SaveGame.Serialize(document), origin.Data, Pristine, credit: CreditRules.FullTyped, bumpSession: false, migrate: migrate);

    [Fact]
    public void Given_TheSaveFormat_When_ItsVersionIsRead_Then_ItIsEleven()
    {
        Assert.Equal(12, SaveGame.CurrentVersion);
        Assert.Equal(12, SaveGame.OldestReadableVersion);
    }

    [Fact]
    public void Given_WornStacks_When_TheGameIsCaptured_Then_TheSlotAndTheWornItemCarryTheirWearAndOthersCarryNone()
    {
        var document = SaveGame.Capture(Worn());

        Assert.Equal(new SlotSave("test-hatchet", 1, 2), document.Inventory[2]);
        Assert.Equal(new SlotSave("apple", 4), document.Inventory[15]);
        Assert.Equal([new WornSave("clothing", "head", "test-cap", 3)], document.Worn);
        var text = SaveGame.Serialize(document);
        Assert.Contains("\"item\":\"test-hatchet\",\"count\":1,\"wear\":2", text, StringComparison.Ordinal);
        Assert.Contains("\"item\":\"test-cap\",\"wear\":3", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"item\":\"apple\",\"count\":4,\"wear\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_NothingWorn_When_TheGameIsCaptured_Then_NoWearIsWrittenAnywhere() =>
        Assert.DoesNotContain("\"wear\"", SaveGame.Serialize(SaveGame.Capture(GearItems.Session())), StringComparison.Ordinal);

    [Fact]
    public void Given_WornStacks_When_TheGameIsSavedAndLoaded_Then_TheWearAndTheDigestComeBack()
    {
        var session = Worn();
        session.Step([]);
        var document = SaveGame.Capture(session);

        var loaded = Load(document, session);

        Assert.Equal(new ItemStack(GearItems.Cap, 1, 3), loaded.Simulation.Equipment.Get(Head));
        Assert.Equal(new ItemStack(GearItems.Hatchet, 1, 2), loaded.Simulation.Inventory.Slots[2]);
        Assert.Equal(session.Simulation.ComputeStateDigest(), loaded.Simulation.ComputeStateDigest());
        Assert.Equal(SaveGame.Serialize(document), SaveGame.Serialize(SaveGame.Capture(loaded))); // byte for byte
    }

    [Fact]
    public void Given_ASaveOfAWornOutPiece_When_ItLoads_Then_ItIsStillWornOutAndGivesNothing()
    {
        var session = Worn();
        session.Simulation.Equipment.SetWear(Head, 4);
        session.Simulation.RefreshStats();

        var loaded = Load(SaveGame.Capture(session), session);

        Assert.Equal(4, loaded.Simulation.Equipment.Get(Head).Wear);
        Assert.Equal(0, loaded.Simulation.Stats.Sum("warmth"));
    }

    [Fact]
    public void Given_AWearPastTheDurabilityOrOnAnItemWithNone_When_TheSlotIsRead_Then_ItIsClampedOrDropped()
    {
        Assert.Equal(new ItemStack(GearItems.Hatchet, 1, 3), new SlotSave("test-hatchet", 1, 99).ToStack(GearItems.Catalogue));
        Assert.Equal(new ItemStack(TestItems.Apple, 4), new SlotSave("apple", 4, 7).ToStack(GearItems.Catalogue));
        Assert.Equal(new ItemStack(GearItems.Hatchet, 1, 0), new SlotSave("test-hatchet", 1, -5).ToStack(GearItems.Catalogue));
    }

    [Fact]
    public void Given_AWornEntryWithWearPastTheDurability_When_ItsStacksAreRead_Then_ItIsClamped()
    {
        var worn = new[] { new WornSave("clothing", "head", "test-cap", 99) };

        var stacks = WornSave.ToStacks(worn, GearItems.Data, GearItems.Catalogue)!;

        Assert.Equal(4, stacks[GearItems.Data.FlatIndex(Head)].Wear);
    }

    [Fact]
    public void Given_TheDigest_When_AnItemWears_Then_ItChangesAndTwoEqualWearsAgree()
    {
        var plain = Worn().Simulation.ComputeStateDigest();
        var more = Worn();
        more.Simulation.Inventory.SetSlot(2, new ItemStack(GearItems.Hatchet, 1, 3));

        Assert.NotEqual(plain, more.Simulation.ComputeStateDigest());
        Assert.Equal(plain, Worn().Simulation.ComputeStateDigest());
    }

    [Fact]
    public void Given_NoWearAnywhere_When_TheDigestIsTaken_Then_ItIsTheDigestOfAGameThatNeverHadWear()
    {
        var a = GearItems.Session();
        var b = GearItems.Session();
        b.Simulation.Inventory.SetSlot(5, new ItemStack(GearItems.Hatchet, 1, 1));
        b.Simulation.Inventory.SetSlot(5, new ItemStack(GearItems.Hatchet, 1, 0));
        a.Simulation.Inventory.SetSlot(5, new ItemStack(GearItems.Hatchet, 1, 0));

        Assert.Equal(a.Simulation.ComputeStateDigest(), b.Simulation.ComputeStateDigest());
    }

    [Fact]
    public void Given_TheSameWearTimeAndCommands_When_TwoRunsGo_Then_TheirDigestsAndWearAreTheSame()
    {
        string Run()
        {
            var sim = WearRig.Sim(hourInOneTick: true);
            WearRig.Wear(sim, GearItems.Cap, Head);
            sim.Inventory.SetSlot(0, new ItemStack(GearItems.Hatchet, 1));
            sim.Step([VoxelCommand.SelectSlot(0)]);
            WearRig.Chop(sim);
            for (var i = 0; i < 3; i++)
            {
                sim.Step([]);
            }

            return sim.ComputeStateDigest();
        }

        Assert.Equal(Run(), Run());
    }
}
