using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Gear;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.Figures;
using Vocaterra.Game.Graphics.Sky;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.Figures;

/// <summary>The gear on the player's figure and arm (I09): nothing worn draws as before, a worn piece adds inflated boxes in its colour, the bag sits on the back and the off-hand item moves with the left arm.</summary>
public sealed class PlayerGearTests
{
    private static readonly ItemId Tunic = (ItemId)240;
    private static readonly ItemId Cap = (ItemId)241;
    private static readonly ItemId Trousers = (ItemId)242;
    private static readonly ItemId Shoes = (ItemId)243;
    private static readonly ItemId Rucksack = (ItemId)244;
    private static readonly ItemId Buckler = (ItemId)245;
    private static readonly ItemId Plain = (ItemId)246;

    private static readonly IItemCatalogue Catalogue = TestItems.With(
        Gear(Tunic, "pg-tunic", "clothing", "body", ("body", "#c03020"), ("sleeve", "#a02818")),
        Gear(Cap, "pg-cap", "clothing", "head", ("head", "#2040c0")),
        Gear(Trousers, "pg-trousers", "clothing", "legs", ("legs", "#404040")),
        Gear(Shoes, "pg-shoes", "clothing", "feet", ("feet", "#202020")),
        Gear(Rucksack, "pg-rucksack", "bag", "back", ("back", "#10a040")),
        Gear(Buckler, "pg-buckler", "offhand", "offhand"),
        Gear(Plain, "pg-plain", "clothing", "body"));

    private static ItemDefinition Gear(ItemId id, string name, string tag, string wear, params (string Part, string Colour)[] look)
    {
        var parts = look.ToDictionary(static p => p.Part, static p => LookColour.TryParse(p.Colour, out var c) ? c : default);
        return new ItemDefinition(id, name, 1, "coin", ItemHeld.Sprite, [tag], Wear: wear, Look: parts.Count == 0 ? null : new ItemLook(parts), Capacity: tag == "bag" ? 10 : null);
    }

    private static Equipment Fresh() => new(TestGame.Data.Equipment, Catalogue);

    private static EquipSlot Slot(Equipment e, string group, string name)
    {
        var g = e.Data.IndexOf(group);
        return new EquipSlot(g, e.Data.Groups[g].Slots.ToList().FindIndex(s => s.Name == name));
    }

    private static EquipSlot First(Equipment e, string wear) => e.Data.AllSlots().First(s => e.Data.Spec(s).Wear == wear);

    private static MeshVertex[] Mesh(FigurePart[] parts)
    {
        var buffer = new MeshBuffer();
        FigureMesher.Append(buffer, parts, FigurePose.Rest, 0f, 0f, 0f, 0f, (1f, 1f, 1f));
        return buffer.ToArray();
    }

    [Fact]
    public void Given_NothingWorn_When_TheGearIsRead_Then_TheFigureIsTheSameBoxesAndColoursAsWithoutGear()
    {
        var equipment = Fresh();
        var gear = new PlayerGear(equipment);
        gear.Refresh();

        Assert.True(gear.Appearance.IsNone);
        Assert.Same(NpcFigures.PartsOf(NpcFigures.PlayerName), gear.Parts);
        Assert.Equal(Mesh(NpcFigures.PartsOf(NpcFigures.PlayerName)), Mesh(gear.Parts));
        Assert.Null(gear.OffHand);
        Assert.Equal(0, gear.BoxesAdded);
    }

    [Fact]
    public void Given_ABodyPieceWorn_When_TheGearIsRead_Then_AnInflatedBodyBoxAndTwoSleevesAreAddedInItsColours()
    {
        var equipment = Fresh();
        equipment.Set(Slot(equipment, "clothing", "body"), new ItemStack(Tunic, 1));
        var gear = new PlayerGear(equipment);
        gear.Refresh();
        var baseParts = NpcFigures.PartsOf(NpcFigures.PlayerName);

        var added = gear.Parts.Skip(baseParts.Length).ToArray();

        Assert.Equal(baseParts, gear.Parts.Take(baseParts.Length));
        Assert.Equal(3, added.Length);
        var body = Assert.Single(added, static p => p.Group == FigureGroup.Body);
        Assert.Equal((0xc0, 0x30, 0x20), body.Colour);
        Assert.True(body.Box.X0 < -4f && body.Box.X1 > 4f && body.Box.Y0 < 10f && body.Box.Y1 > 22f && body.Box.Z0 < -2.5f && body.Box.Z1 > 2.5f, "the box is inflated over the body (-4..4, 10..22, -2.5..2.5)");
        Assert.All(added.Where(static p => p.Group != FigureGroup.Body), static p => Assert.Equal((0xa0, 0x28, 0x18), p.Colour));
        Assert.Equal([FigureGroup.ArmLeft, FigureGroup.ArmRight], added.Where(static p => p.Group != FigureGroup.Body).Select(static p => p.Group));
        Assert.Equal(3, gear.BoxesAdded);
    }

    [Fact]
    public void Given_APartWithNoSleeveColour_When_TheArmsAreDrawn_Then_TheyTakeTheBodysColour()
    {
        var coat = (ItemId)247;
        var worn = new Equipment(TestGame.Data.Equipment, TestItems.With(Gear(coat, "pg-coat", "clothing", "body", ("body", "#112233"))));
        worn.Set(Slot(worn, "clothing", "body"), new ItemStack(coat, 1));
        var gear = new PlayerGear(worn);
        gear.Refresh();

        Assert.All(gear.Parts.Skip(NpcFigures.PartsOf(NpcFigures.PlayerName).Length), static p => Assert.Equal((0x11, 0x22, 0x33), p.Colour));
        Assert.Equal(new LookColour(0x11, 0x22, 0x33), gear.Appearance.Sleeve);
    }

    [Fact]
    public void Given_APieceOnEveryPartAndABag_When_TheBoxesAreCounted_Then_TenAreAdded()
    {
        var equipment = Fresh();
        equipment.Set(Slot(equipment, "clothing", "body"), new ItemStack(Tunic, 1));
        equipment.Set(Slot(equipment, "clothing", "head"), new ItemStack(Cap, 1));
        equipment.Set(Slot(equipment, "clothing", "legs"), new ItemStack(Trousers, 1));
        equipment.Set(Slot(equipment, "clothing", "feet"), new ItemStack(Shoes, 1));
        equipment.Set(First(equipment, "back"), new ItemStack(Rucksack, 1));
        var gear = new PlayerGear(equipment);
        gear.Refresh();

        Assert.Equal(10, gear.BoxesAdded); // head 2 (the cap), body 1, sleeves 2, legs 2, feet 2, pack 1
        Assert.Equal(NpcFigures.PartsOf(NpcFigures.PlayerName).Length + 10, gear.Parts.Length);
    }

    [Fact]
    public void Given_AWornBag_When_TheGearIsRead_Then_APackBoxSitsOnTheBackInTheBagsColourWithTheFlapPicture()
    {
        var equipment = Fresh();
        equipment.Set(First(equipment, "back"), new ItemStack(Rucksack, 1));
        var gear = new PlayerGear(equipment);
        gear.Refresh();

        var pack = Assert.Single(gear.Parts.Skip(NpcFigures.PartsOf(NpcFigures.PlayerName).Length));

        Assert.Equal((0x10, 0xa0, 0x40), pack.Colour);
        Assert.True(pack.Box.Z0 >= 2.5f, "behind the body (z towards the back)");
        Assert.Equal(BlockCell.Backpack, pack.Special);
        Assert.Equal(FigureGroup.Body, pack.Group);
    }

    [Fact]
    public void Given_AnItemWithNoLook_When_ItIsWorn_Then_TheFigureIsUnchanged()
    {
        var equipment = Fresh();
        equipment.Set(Slot(equipment, "clothing", "body"), new ItemStack(Plain, 1));
        var gear = new PlayerGear(equipment);
        gear.Refresh();

        Assert.Same(NpcFigures.PartsOf(NpcFigures.PlayerName), gear.Parts);
    }

    [Fact]
    public void Given_AnOffHandItem_When_TheGearIsRead_Then_ItsMeshIsMadeOnceAndSwingsWithTheLeftArm()
    {
        var equipment = Fresh();
        equipment.Set(First(equipment, "offhand"), new ItemStack(Buckler, 1));
        var gear = new PlayerGear(equipment);
        gear.Refresh();
        var first = gear.OffHand;
        gear.Refresh();

        Assert.NotNull(first);
        Assert.Same(first, gear.OffHand);
        Assert.True(first.FaceCount > 0);
        var rest = new MeshBuffer();
        var swung = new MeshBuffer();
        FigureMesher.AppendAttached(rest, first, FigureGroup.ArmLeft, PlayerGear.OffHandOrigin, FigurePose.Rest, 0f, 0f, 0f, 0f, (1f, 1f, 1f));
        FigureMesher.AppendAttached(swung, first, FigureGroup.ArmLeft, PlayerGear.OffHandOrigin, FigurePose.Rest with { ArmLeft = 0.6f }, 0f, 0f, 0f, 0f, (1f, 1f, 1f));
        Assert.Equal(first.VertexCount, rest.VertexCount);
        Assert.NotEqual(rest.ToArray(), swung.ToArray());
        Assert.InRange(rest.Vertices[0].X, -0.6f, 0f); // at the left hand, the figure's left being -x
    }

    [Fact]
    public void Given_TheEquipmentChanges_When_TheGearIsRefreshed_Then_ItIsRebuiltOnlyThen()
    {
        var equipment = Fresh();
        var gear = new PlayerGear(equipment);
        gear.Refresh();
        var before = gear.Parts;
        gear.Refresh();
        Assert.Same(before, gear.Parts);

        equipment.Set(Slot(equipment, "clothing", "head"), new ItemStack(Cap, 1));
        gear.Refresh();

        Assert.NotSame(before, gear.Parts);
        var cached = gear.Parts;
        gear.Refresh();
        Assert.Same(cached, gear.Parts);
    }

    [Fact]
    public void Given_NothingWorn_When_TheFirstPersonArmIsMade_Then_ItIsTheSameAsTheOneWithThePlainSleeve()
    {
        Assert.Same(ArmMeshes.Arm(), ArmMeshes.Arm(ArmMeshes.Sleeve));
        var sim = VoxelSimulation.Create(RouteScene.Clearing(), Catalogue, equipment: TestGame.Data.Equipment);
        var presenter = new ArmPresenter(sim, sim.Grid.IsSolid);

        var frame = presenter.Frame(new Microsoft.Xna.Framework.Vector3(10.5f, 20f, 10.5f), [], SkyLook.Noon);

        Assert.Null(frame.Sleeve);
        Assert.Null(frame.OffHand);
    }

    [Fact]
    public void Given_ABodyPieceWorn_When_TheFirstPersonArmIsMade_Then_TheSleeveTakesItsColourAndTheOffHandItemShowsInTheLeftHand()
    {
        var sim = VoxelSimulation.Create(RouteScene.Clearing(), Catalogue, equipment: TestGame.Data.Equipment);
        sim.Equipment.Set(Slot(sim.Equipment, "clothing", "body"), new ItemStack(Tunic, 1));
        sim.Equipment.Set(First(sim.Equipment, "offhand"), new ItemStack(Buckler, 1));
        var presenter = new ArmPresenter(sim, sim.Grid.IsSolid);

        var frame = presenter.Frame(new Microsoft.Xna.Framework.Vector3(10.5f, 20f, 10.5f), [], SkyLook.Noon);

        Assert.Equal(((byte)0xa0, (byte)0x28, (byte)0x18), frame.Sleeve);
        Assert.Equal(HeldKind.Sprite, frame.OffHand!.Value.Kind);
        var plain = ArmMeshes.Arm().Vertices[0].Color;
        var tinted = ArmMeshes.Arm(frame.Sleeve).Vertices[0].Color;
        Assert.NotEqual(plain, tinted);
        Assert.Equal(ArmMeshes.Arm().VertexCount, ArmMeshes.Arm(frame.Sleeve).VertexCount);
        Assert.Same(ArmMeshes.Arm(frame.Sleeve), ArmMeshes.Arm(frame.Sleeve));
    }

    [Fact]
    public void Given_NothingWorn_When_ThePlayersFigureIsBuilt_Then_ItHasNoPackBox()
    {
        var parts = NpcFigures.PartsOf(NpcFigures.PlayerName);

        Assert.DoesNotContain(parts, static p => p.Special == BlockCell.Backpack);
        Assert.DoesNotContain(parts, static p => p.Box.Z1 > 5f); // nothing sticks out behind the body
    }

    [Fact]
    public void Given_ABagWorn_When_TheGearIsRead_Then_OnePackBoxShowsInTheBagsColour()
    {
        var equipment = Fresh();
        equipment.Set(First(equipment, "back"), new ItemStack(Rucksack, 1));
        var gear = new PlayerGear(equipment);
        gear.Refresh();

        var pack = Assert.Single(gear.Parts, static p => p.Special == BlockCell.Backpack);

        Assert.Equal((0x10, 0xa0, 0x40), pack.Colour);
    }

    [Fact]
    public void Given_TheShippedBags_When_TheirLooksAreRead_Then_EachHasItsOwnBackColour()
    {
        var items = TestItems.Catalogue;
        var sack = items.Find("sack")!.Look!.Parts[GearFigureParts.BackKey];
        var backpack = items.Find("backpack")!.Look!.Parts[GearFigureParts.BackKey];

        Assert.NotEqual(sack, backpack);
        Assert.True(sack.R > sack.B, "the sack is tan");
        Assert.True(backpack.G > backpack.R && backpack.G > backpack.B, "the backpack is green");
    }

    [Fact]
    public void Given_AHeadPiece_When_TheGearIsRead_Then_TheFaceOfTheHeadIsNotCovered()
    {
        var equipment = Fresh();
        equipment.Set(First(equipment, "head"), new ItemStack(Cap, 1));
        var gear = new PlayerGear(equipment);
        gear.Refresh();
        var baseParts = NpcFigures.PartsOf(NpcFigures.PlayerName);
        var head = baseParts.First(static p => p.Special is not null && p.Group == FigureGroup.Head).Box;

        var cap = gear.Parts.Skip(baseParts.Length).Where(static p => p.Group == FigureGroup.Head).ToArray();

        Assert.NotEmpty(cap);
        foreach (var piece in cap)
        {
            // Every cap box stays clear of the face: above the face's lower part at the front, or behind the middle of the head.
            Assert.True(piece.Box.Y0 >= head.Y1 - 1.5f || piece.Box.Z0 >= (head.Z0 + head.Z1) / 2f);
        }

        Assert.DoesNotContain(cap, p => p.Box.Z0 <= head.Z0 && p.Box.Y0 < head.Y1 - 1.5f); // none reaches the front below the forehead
    }

    [Fact]
    public void Given_TheShippedGear_When_TheAxeAndTheTorchAreRead_Then_TheAxeLastsThreeHundredAndFiftyAndTheTorchHasNoWear()
    {
        Assert.Equal(350, TestItems.Catalogue.Find("axe")!.Durability!.Max);
        Assert.Null(TestItems.Catalogue.Find("torch")!.Durability);
    }

    [Fact]
    public void Given_TheNpcFigures_When_TheyAreBuilt_Then_TheGuideKeepsItsPack()
    {
        Assert.Contains(NpcFigures.PartsOf("guide"), static p => p.Special == BlockCell.Backpack);
        Assert.DoesNotContain(NpcFigures.PartsOf("villager"), static p => p.Special == BlockCell.Backpack);
    }
}
