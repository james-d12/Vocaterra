using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Needs;

/// <summary>N07, cooking at the fire (adaptation design 9.6): a lit fire ring is the station <c>fuego</c>, an apple roasts there, and only cooked food heals.</summary>
public sealed class FireCookingTests
{
    private static int RoastIndex(VoxelSession session) => session.Recipes.IndexOf("manzana");

    private static int TorchIndex(VoxelSession session) => session.Recipes.IndexOf("antorcha");

    private static BlockPos LitMiddle(VoxelSession session) =>
        session.Fires.Middles.First(m => session.Fires.IsLit(m));

    private static BlockPos UnlitMiddle(VoxelSession session)
    {
        var lit = session.Fires.Middles.Where(m => session.Fires.IsLit(m)).ToList();
        return session.Fires.Middles.First(m => !session.Fires.IsLit(m) && lit.All(l => Math.Abs(l.X - m.X) + Math.Abs(l.Z - m.Z) > 14));
    }

    private static VoxelSession At(BlockPos middle)
    {
        var session = TestGame.NewSession();
        session.Simulation.TeleportTo(middle);
        return session;
    }

    private static VoxelSession AtTheFire() => At(LitMiddle(TestGame.NewSession()));

    [Fact]
    public void TheFireIsAStationOnlyWhileItsRingBurns()
    {
        var probe = TestGame.NewSession();
        var lit = At(LitMiddle(probe));
        var cold = At(UnlitMiddle(probe));

        Assert.True(lit.Simulation.StationInReach(BlockId.FireRing));
        Assert.False(cold.Simulation.StationInReach(BlockId.FireRing));

        // A torch on the cold ring's rim lights it, and the station with it; taking it off puts it out.
        var middle = UnlitMiddle(cold);
        cold.Scene.Grid.SetBlock(middle.X + 1, middle.Y + 1, middle.Z, BlockId.Torch);
        Assert.True(cold.Simulation.StationInReach(BlockId.FireRing));
        cold.Scene.Grid.SetBlock(middle.X + 1, middle.Y + 1, middle.Z, BlockId.Air);
        Assert.False(cold.Simulation.StationInReach(BlockId.FireRing));
    }

    [Fact]
    public void TheFireHasItsRecipeAndTheWorkbenchKeepsItsOwn()
    {
        var session = TestGame.NewSession();
        var roast = session.Recipes.Entries[RoastIndex(session)];

        Assert.Equal("fuego", roast.Station!.Lemma);
        Assert.Equal(BlockId.FireRing, roast.Rule.Station);
        Assert.Equal(TestItems.RoastApple, roast.Rule.Output);
        Assert.Equal([("manzana", 1)], roast.Ingredients.Select(static i => (i.Noun.Lemma, i.Count)).ToArray());
        Assert.Equal("mesa", session.Recipes.Entries[TorchIndex(session)].Station!.Lemma);

        // At the fire the torch is not craftable (its station is the table); at the table the roast is not.
        var fire = AtTheFire();
        fire.Simulation.GrantItems(ItemId.Wood, 2);
        fire.Simulation.GrantItems(TestItems.Apple, 1);
        Assert.Equal(CraftResult.MissingStation, fire.Simulation.CanCraft(TorchIndex(fire)));
        Assert.Equal(CraftResult.Done, fire.Simulation.CanCraft(RoastIndex(fire)));
    }

    [Fact]
    public void AnAppleAtALitFireBecomesARoastAppleAndTheCraftIsLoggedWithTheNounManzana()
    {
        var session = AtTheFire();
        session.Simulation.GrantItems(TestItems.Apple, 1);

        session.Step([VoxelCommand.Craft(RoastIndex(session))]);

        Assert.Equal(0, session.Simulation.Inventory.Count(TestItems.Apple));
        Assert.Equal(1, session.Simulation.Inventory.Count(TestItems.RoastApple));
        var crafted = session.Events.OfType<WorldEvent>().Single(static e => e.Name == WorldEvents.ItemCrafted);
        Assert.Equal("manzana", crafted.Detail);
    }

    [Fact]
    public void WithoutAnAppleOrAwayFromALitFireNothingIsMade()
    {
        var bare = AtTheFire();
        bare.Step([VoxelCommand.Craft(RoastIndex(bare))]);
        Assert.Equal(0, bare.Simulation.Inventory.Count(TestItems.RoastApple));
        Assert.DoesNotContain(bare.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.ItemCrafted);

        var cold = At(UnlitMiddle(TestGame.NewSession()));
        cold.Simulation.GrantItems(TestItems.Apple, 1);
        cold.Step([VoxelCommand.Craft(RoastIndex(cold))]);
        Assert.Equal(1, cold.Simulation.Inventory.Count(TestItems.Apple));
        Assert.Equal(0, cold.Simulation.Inventory.Count(TestItems.RoastApple));
    }

    [Fact]
    public void ARoastAppleFeedsTwentyAndHealsFiveWhileARawAppleFeedsTwelveAndHealsNothing()
    {
        var roast = TestGame.NewSession();
        roast.SetNeed(NeedMeter.Hunger, 40);
        roast.SetNeed(NeedMeter.Health, 50);
        roast.Simulation.GrantItems(TestItems.RoastApple, 1);
        Assert.Equal(NeedActionResult.Done, roast.EatItem(TestItems.RoastApple));
        Assert.Equal(60, roast.Needs.Value(NeedMeter.Hunger), 6);
        Assert.Equal(55, roast.Needs.Value(NeedMeter.Health), 6);

        var raw = TestGame.NewSession();
        raw.SetNeed(NeedMeter.Hunger, 40);
        raw.SetNeed(NeedMeter.Health, 50);
        raw.Simulation.GrantItems(TestItems.Apple, 1);
        Assert.Equal(NeedActionResult.Done, raw.EatItem(TestItems.Apple));
        Assert.Equal(52, raw.Needs.Value(NeedMeter.Hunger), 6);
        Assert.Equal(50, raw.Needs.Value(NeedMeter.Health), 6);
        Assert.Contains(roast.Events.OfType<WorldEvent>(), static e => e.Name == WorldEvents.MealEaten && e.Detail == "manzana");
    }

    [Fact]
    public void TheRoastAppleIsLabelledAsaAndTheAppleKeepsItsLemma()
    {
        Assert.Equal("manzana asada", TestGame.Nouns.LabelOf(TestItems.RoastApple));
        Assert.Equal("manzana", TestGame.Nouns.LabelOf(TestItems.Apple));
        Assert.Equal("manzana", TestGame.Nouns.ForItem(TestItems.RoastApple)!.Lemma);
        Assert.Equal("fuego", TestGame.Nouns.ForBlock(BlockId.FireRing)!.Lemma);
    }

    [Fact]
    public void ASaveHoldingARoastAppleRoundTripsAndTheSameWorldDigestsTheSame()
    {
        var session = AtTheFire();
        session.Simulation.GrantItems(TestItems.RoastApple, 3);
        var scene = TestGame.Route(TestGame.Seed);

        var loaded = SaveGame.Load(SaveGame.Save(session, scene), TestGame.Data, TestGame.Route);

        Assert.Equal(3, loaded.Simulation.Inventory.Count(TestItems.RoastApple));
        Assert.Equal(session.Simulation.ComputeStateDigest(), loaded.Simulation.ComputeStateDigest());
    }
}
