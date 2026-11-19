using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Items.Gear;

/// <summary>What shows of the worn gear (I09): the top layer's colour for each part, the back and the off-hand found by the slots' wear kind, and a new layer as a data row.</summary>
public sealed class AppearanceBuilderTests
{
    private static readonly ItemId Tunic = (ItemId)230;
    private static readonly ItemId Mail = (ItemId)231;
    private static readonly ItemId Dress = (ItemId)232;
    private static readonly ItemId Cap = (ItemId)233;
    private static readonly ItemId Rucksack = (ItemId)234;
    private static readonly ItemId Buckler = (ItemId)235;
    private static readonly ItemId Cloak = (ItemId)236;

    private static readonly LookColour Red = new(255, 0, 0);
    private static readonly LookColour Green = new(0, 255, 0);
    private static readonly LookColour Blue = new(0, 0, 255);
    private static readonly LookColour White = new(255, 255, 255);

    private static readonly Lazy<IItemCatalogue> Catalogue = new(() => TestItems.With(
        Worn(Tunic, "look-tunic", "clothing", "body", ("body", Red), ("sleeve", Red), ("legs", Red)),
        Worn(Mail, "look-mail", "armour", "body", ("body", Green)),
        Worn(Dress, "look-dress", "clothing", "body", ("body", Blue), ("legs", Blue), ("feet", Blue)),
        Worn(Cap, "look-cap", "clothing", "head", ("head", Blue)),
        Worn(Rucksack, "look-rucksack", "bag", "back", ("back", White)),
        Worn(Buckler, "look-buckler", "offhand", "offhand"),
        Worn(Cloak, "look-cloak", "clothing", "body", ("body", White))));

    private static ItemDefinition Worn(ItemId id, string name, string tag, string wear, params (string Part, LookColour Colour)[] look) =>
        new(id, name, 1, name, ItemHeld.Sprite, [tag], Wear: wear, Look: look.Length == 0 ? null : new ItemLook(look.ToDictionary(static p => p.Part, static p => p.Colour)),
            Capacity: tag == "bag" ? 10 : null);

    private static Equipment Fresh(EquipmentData? data = null) => new(data ?? GearItems.Data, Catalogue.Value);

    [Fact]
    public void Given_NothingWorn_When_TheAppearanceIsBuilt_Then_NothingShows()
    {
        var appearance = AppearanceBuilder.Of(Fresh());

        Assert.True(appearance.IsNone);
        Assert.Same(Appearance.None, appearance);
    }

    [Fact]
    public void Given_ClothingArmourAndCosmeticOnTheBody_When_TheAppearanceIsBuilt_Then_CosmeticOverArmourOverClothing()
    {
        var equipment = Fresh();
        equipment.Set(GearItems.Slot("clothing", "body"), new ItemStack(Tunic, 1));
        Assert.Equal(Red, AppearanceBuilder.Of(equipment).Parts["body"]);

        equipment.Set(GearItems.Slot("armour", "body"), new ItemStack(Mail, 1));
        Assert.Equal(Green, AppearanceBuilder.Of(equipment).Parts["body"]);

        equipment.Set(GearItems.Slot("cosmetic", "body"), new ItemStack(Dress, 1));
        Assert.Equal(Blue, AppearanceBuilder.Of(equipment).Parts["body"]);
    }

    [Fact]
    public void Given_APartOnlyClothingCoversAndAPartNothingCovers_When_TheAppearanceIsBuilt_Then_TheFirstShowsAndTheSecondIsAbsent()
    {
        var equipment = Fresh();
        equipment.Set(GearItems.Slot("clothing", "body"), new ItemStack(Tunic, 1));
        equipment.Set(GearItems.Slot("armour", "body"), new ItemStack(Mail, 1));

        var appearance = AppearanceBuilder.Of(equipment);

        Assert.Equal(Green, appearance.Parts["body"]);
        Assert.Equal(Red, appearance.Parts["legs"]); // the mail covers only the body; the tunic's trousers show
        Assert.False(appearance.Parts.ContainsKey("feet"));
        Assert.False(appearance.Parts.ContainsKey("head"));
    }

    [Fact]
    public void Given_ABodyPieceWithASleeveAndOneWithout_When_TheAppearanceIsBuilt_Then_TheSleeveIsItsOwnOrTheBodys()
    {
        var equipment = Fresh();
        equipment.Set(GearItems.Slot("clothing", "body"), new ItemStack(Tunic, 1));
        Assert.Equal(Red, AppearanceBuilder.Of(equipment).Sleeve);

        equipment.Set(GearItems.Slot("armour", "body"), new ItemStack(Mail, 1));
        Assert.Equal(Red, AppearanceBuilder.Of(equipment).Sleeve); // the tunic still gives its sleeve under the mail

        equipment.Set(GearItems.Slot("clothing", "body"), new ItemStack(Cloak, 1));
        Assert.Equal(Green, AppearanceBuilder.Of(equipment).Sleeve); // no sleeve key anywhere: the top body colour

        Assert.Null(AppearanceBuilder.Of(Fresh()).Sleeve);
    }

    [Fact]
    public void Given_ABagAndAnOffHandItemWorn_When_TheAppearanceIsBuilt_Then_TheyAreFoundByTheirSlotsWearKind()
    {
        var equipment = Fresh();
        equipment.Set(GearItems.Nth("backpack", 0), new ItemStack(Rucksack, 1));
        equipment.Set(GearItems.Nth("offhand", 0), new ItemStack(Buckler, 1));

        var appearance = AppearanceBuilder.Of(equipment);

        Assert.Equal(Rucksack, appearance.Back!.Id);
        Assert.Equal(Buckler, appearance.OffHand!.Id);
        Assert.Empty(appearance.Parts); // neither covers a body part
    }

    [Fact]
    public void Given_GroupsWithOtherIdsAndOtherWearKinds_When_TheRolesNameThem_Then_NoGroupIdIsNeeded()
    {
        var data = new EquipmentData(
        [
            new EquipmentGroup("pouch", null, [new EquipmentSlotSpec("pouch", "satchel", null)], ["bag"], false, true),
            new EquipmentGroup("grip", null, [new EquipmentSlotSpec("grip", "wield", null)], ["offhand"], false, true),
        ]);
        var items = TestItems.With(
            new ItemDefinition((ItemId)237, "look-satchel", 1, "wood", ItemHeld.Sprite, ["bag"], Wear: "satchel"),
            new ItemDefinition((ItemId)238, "look-wand", 1, "wood", ItemHeld.Sprite, ["offhand"], Wear: "wield"));
        var equipment = new Equipment(data, items);
        equipment.Set(new EquipSlot(0, 0), new ItemStack((ItemId)237, 1));
        equipment.Set(new EquipSlot(1, 0), new ItemStack((ItemId)238, 1));

        var appearance = AppearanceBuilder.Of(equipment, new AppearanceRoles(BackWear: "satchel", OffHandWear: "wield"));

        Assert.Equal("look-satchel", appearance.Back!.Name);
        Assert.Equal("look-wand", appearance.OffHand!.Name);
    }

    [Fact]
    public void Given_AFourthLayerAddedAsADataRow_When_ItCoversAPart_Then_ItShowsOverTheCosmeticWithNoCodeEdit()
    {
        var slots = new[] { new EquipmentSlotSpec("body", "body", null) };
        var data = new EquipmentData(
        [
            new EquipmentGroup("clothing", 1, slots, ["clothing"], false, true),
            new EquipmentGroup("armour", 2, slots, ["armour"], false, true),
            new EquipmentGroup("cosmetic", 3, slots, ["clothing", "armour"], false, false),
            new EquipmentGroup("overcoat", 4, slots, ["clothing"], false, true),
        ]);
        var equipment = Fresh(data);
        equipment.Set(new EquipSlot(2, 0), new ItemStack(Dress, 1));
        Assert.Equal(Blue, AppearanceBuilder.Of(equipment).Parts["body"]);

        equipment.Set(new EquipSlot(3, 0), new ItemStack(Cloak, 1));

        Assert.Equal(White, AppearanceBuilder.Of(equipment).Parts["body"]);
    }

    [Fact]
    public void Given_ALayerNumberedBelowTheOthersListedLast_When_TwoItemsCoverAPart_Then_TheLayerNumberDecidesNotTheFileOrder()
    {
        var slots = new[] { new EquipmentSlotSpec("body", "body", null) };
        var data = new EquipmentData(
        [
            new EquipmentGroup("top", 9, slots, ["clothing"], false, true),
            new EquipmentGroup("under", 1, slots, ["clothing"], false, true),
        ]);
        var equipment = Fresh(data);
        equipment.Set(new EquipSlot(1, 0), new ItemStack(Tunic, 1));
        equipment.Set(new EquipSlot(0, 0), new ItemStack(Dress, 1));

        Assert.Equal(Blue, AppearanceBuilder.Of(equipment).Parts["body"]);
    }

    [Fact]
    public void Given_TheEquipment_When_ItChanges_Then_TheRevisionMoves()
    {
        var equipment = Fresh();
        var before = equipment.Revision;

        equipment.Set(GearItems.Slot("clothing", "head"), new ItemStack(Cap, 1));
        var afterSet = equipment.Revision;
        equipment.Restore(null);

        Assert.True(afterSet > before);
        Assert.True(equipment.Revision > afterSet);
    }
}
