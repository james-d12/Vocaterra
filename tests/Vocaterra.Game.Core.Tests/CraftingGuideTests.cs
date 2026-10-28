using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Game.Core.Tests;

/// <summary>The guide's crafting and finding answers (P6b) through the real core, with typed lines and a hand-set inventory.</summary>
public sealed class CraftingGuideTests
{
    private static VoxelSession SessionWith(ItemId? item = null, int count = 0)
    {
        var session = TestGame.NewSession();
        if (item is { } granted && count > 0)
        {
            session.Simulation.GrantItems(granted, count);
        }

        return session;
    }

    [Theory]
    [InlineData("¿cómo hago una mesa?", "mesa")]
    [InlineData("¿qué necesito para una puerta?", "puerta")]
    [InlineData("¿cómo se hace un horno?", "horno")]
    [InlineData("quiero hacer una silla", "silla")]
    [InlineData("¿cómo hago la espada?", "espada")]
    public void TheCraftingQuestionsResolveToAskCraftWithTheItem(string line, string item)
    {
        var turn = SessionWith().Say(line);

        Assert.Equal("ask_craft", turn.Intent);
        Assert.True(turn.Communicated);
        Assert.Equal(item, turn.Slots["item"]);
        Assert.Contains(GuideEvents.Craft, turn.HostEvents);
        Assert.Equal("talking", turn.AgentState);
    }

    [Theory]
    [InlineData("¿dónde hay arena?", "arena")]
    [InlineData("¿dónde encuentro madera?", "madera")]
    [InlineData("¿de dónde sale la madera?", "madera")]
    public void TheFindingQuestionsResolveToAskFindWithTheMaterial(string line, string material)
    {
        var turn = SessionWith().Say(line);

        Assert.Equal("ask_find", turn.Intent);
        Assert.True(turn.Communicated);
        Assert.Equal(material, turn.Slots["material"]);
        Assert.Contains(GuideEvents.Find, turn.HostEvents);
    }

    [Fact]
    public void ARecipeWithTheIngredientsTheStationAndWhatIsMissing()
    {
        var turn = SessionWith(ItemId.Wood, 1).Say("¿cómo hago una antorcha?");

        Assert.Contains("antorcha", turn.WorldLine, StringComparison.Ordinal);
        Assert.Contains("dos bloques de madera", turn.WorldLine, StringComparison.Ordinal);
        Assert.Contains("Usa la mesa", turn.WorldLine, StringComparison.Ordinal);
        Assert.Contains("Te falta un bloque de madera", turn.WorldLine, StringComparison.Ordinal);
        Assert.Equal(turn.WorldLine, turn.GuideLines[^1]);
    }

    [Fact]
    public void WhenTheInventoryHasEverythingTheGuideSaysSo()
    {
        var turn = SessionWith(ItemId.Wood, 2).Say("¿cómo hago una antorcha?");

        Assert.Contains("dos bloques de madera", turn.WorldLine, StringComparison.Ordinal);
        Assert.Contains("Ya lo tienes todo", turn.WorldLine, StringComparison.Ordinal);
    }

    [Fact]
    public void ARecipeAtAStationNamesTheStationAndTheMissingIngredientIsSaidInTheSingular()
    {
        var empty = SessionWith().Say("¿qué necesito para una antorcha?");
        Assert.Contains("Usa la mesa", empty.WorldLine, StringComparison.Ordinal);
        Assert.Contains("Te faltan dos bloques de madera", empty.WorldLine, StringComparison.Ordinal);
    }

    [Fact]
    public void AnItemWithNoRecipeGetsTheHonestLine()
    {
        var tree = SessionWith().Say("¿cómo hago un árbol?");
        Assert.Equal("ask_craft", tree.Intent);
        Assert.Equal("Eso no se puede hacer.", tree.WorldLine);

        var cloud = SessionWith().Say("¿cómo hago una nube?");
        Assert.True(cloud.Communicated);
        Assert.Equal("Eso no se puede hacer.", cloud.WorldLine);

        var axe = SessionWith().Say("¿cómo hago un hacha?");
        Assert.Equal("Eso no se puede hacer.", axe.WorldLine);
    }

    [Fact]
    public void WoodComesFromTreesAndTheNearestTreeIsPointedAt()
    {
        var session = SessionWith();

        var turn = session.Say("¿dónde hay madera?");

        Assert.Equal("ask_find", turn.Intent);
        Assert.StartsWith("La madera sale de los árboles. ", turn.WorldLine, StringComparison.Ordinal);
        Assert.Contains("árbol está", turn.WorldLine, StringComparison.Ordinal); // "El árbol está" or "Mira: el árbol está": the pointing pool's variants
        Assert.NotNull(turn.Pointer);
        Assert.Equal("árbol", turn.Pointer.Place);
        var nearest = TestGame.NearestBlock(session, BlockId.Trunk, BlockId.Leaves, BlockId.SpruceTrunk, BlockId.SpruceLeaves, BlockId.BirchTrunk, BlockId.BirchLeaves);
        Assert.Equal(nearest, turn.Pointer.Target);
        Assert.Contains(turn.Pointer.Direction, turn.WorldLine, StringComparison.Ordinal);
        Assert.Equal("árbol", session.Pointer!.Place);
    }

    [Fact]
    public void SandIsItsOwnSourceSoTheLineIsJustTheNearestSand()
    {
        var session = SessionWith();

        var turn = session.Say("¿dónde hay arena?");

        Assert.StartsWith("La arena", turn.WorldLine, StringComparison.Ordinal);
        Assert.DoesNotContain("sale de", turn.WorldLine, StringComparison.Ordinal);
        var nearest = TestGame.NearestBlock(session, BlockId.Sand);
        Assert.Equal(nearest, turn.Pointer!.Target);
        Assert.Equal("arena", turn.Pointer.Place);
    }

    [Fact]
    public void WhatThePlayerAlreadyCarriesIsSaidInsteadOfPointedAt()
    {
        var wood = SessionWith(ItemId.Wood, 3);
        var turn = wood.Say("¿dónde hay madera?");
        Assert.Equal("Tienes tres bloques de madera.", turn.WorldLine);
        Assert.Null(turn.Pointer);
        Assert.Null(wood.Pointer);

    }

    [Fact]
    public void AskingWhereTheTorchIsWhileCarryingItSaysSoAndWithoutItTheNoLine()
    {
        var session = SessionWith(ItemId.Torch, 1);
        var held = session.Say("¿dónde está la antorcha?");
        Assert.Equal("ask_directions", held.Intent);
        Assert.Contains("antorcha", held.WorldLine, StringComparison.Ordinal);
        Assert.Contains("Tienes", held.WorldLine, StringComparison.Ordinal);
        Assert.Null(held.Pointer);

        session.Simulation.Inventory.Remove(ItemId.Torch, 1);
        var gone = session.Say("¿dónde está la antorcha?");
        Assert.DoesNotContain("Tienes", gone.WorldLine, StringComparison.Ordinal);
        Assert.Null(gone.Pointer);
    }

    [Fact]
    public void AskingWhereATreeIsStillPointsEvenWhenWoodIsCarried()
    {
        var turn = SessionWith(ItemId.Wood, 5).Say("¿dónde está el árbol?");

        Assert.Equal("ask_directions", turn.Intent);
        Assert.NotNull(turn.Pointer);
        Assert.StartsWith("El árbol está", turn.WorldLine, StringComparison.Ordinal);
    }

    [Fact]
    public void ARecipeTurnCreditsTheItemWordAndTheGuidesFactsAreInTheReplayRecord()
    {
        var session = SessionWith();
        var turn = session.Say("¿cómo hago una mesa?");

        Assert.Equal(1, session.Words.RecordOf("mesa").CorrectTurns);
        Assert.NotEqual(session.Simulation.ComputeStateDigest(), turn.HostStateHash); // the host slots and skills are in the digest
    }

    [Fact]
    public void TheSameConversationGivesTheSameLines()
    {
        string[] Run()
        {
            var session = SessionWith(ItemId.Wood, 4);
            return new[] { "¿cómo hago una mesa?", "¿dónde hay madera?", "¿dónde hay arena?" }.Select(l => session.Say(l).WorldLine!).ToArray();
        }

        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void EveryNewPoolRendersWithTheSlotsTheGameGives()
    {
        var bundle = TestGame.Bundle;
        var slots = new Dictionary<string, SlotMap>
        {
            ["guide.craft.part.mass"] = SlotMap.Empty.With("count", "6").With("noun", "madera").With("unit", "bloque"),
            ["guide.craft.part.count"] = SlotMap.Empty.With("count", "2").With("noun", "silla").With("unit", "bloque"),
            ["guide.craft.recipe"] = SlotMap.Empty.With("thing", "puerta").With("need", "seis bloques de madera"),
            ["guide.craft.station.use"] = SlotMap.Empty.With("station", "mesa"),
            ["guide.craft.station.none"] = SlotMap.Empty,
            ["guide.craft.lack.one"] = SlotMap.Empty.With("lack", "un bloque de madera"),
            ["guide.craft.lack.many"] = SlotMap.Empty.With("lack", "dos bloques de madera"),
            ["guide.craft.ready"] = SlotMap.Empty,
            ["guide.craft.none"] = SlotMap.Empty,
            ["guide.find.source"] = SlotMap.Empty.With("material", "madera").With("source", "árbol").With("many", "2"),
            ["guide.find.have.one"] = SlotMap.Empty.With("thing", "hacha"),
            ["guide.find.have.many"] = SlotMap.Empty.With("thing", "hacha").With("count", "2"),
            ["guide.find.have.mass"] = SlotMap.Empty.With("thing", "madera").With("count", "3").With("unit", "bloque"),
        };
        foreach (var (poolId, map) in slots)
        {
            Assert.True(bundle.Dialogue.TryGetPool(poolId, out var pool), $"No pool {poolId}.");
            foreach (var variant in pool.Variants)
            {
                Assert.False(string.IsNullOrWhiteSpace(bundle.TemplateEngine.Render(variant, map)), $"{variant.LineId} rendered empty.");
            }
        }

        Assert.Equal("Tienes dos hachas.", bundle.TemplateEngine.Render(bundle.Dialogue.Pools["guide.find.have.many"].Variants[0], slots["guide.find.have.many"]));
        Assert.Equal("La madera sale de los árboles.", bundle.TemplateEngine.Render(bundle.Dialogue.Pools["guide.find.source"].Variants[0], slots["guide.find.source"]));
    }
}
