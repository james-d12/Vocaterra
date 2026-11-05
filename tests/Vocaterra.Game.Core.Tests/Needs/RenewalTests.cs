using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.Needs;

/// <summary>Food that grows back, and fair gifts (N06, design 9.6): a picked berry bush grows back after the next night, the stall's and the villager's bread and the guide's water are given once per day.</summary>
public sealed class RenewalTests
{
    private const string Fallback = "Hasta luego.";

    private static NeedsData Needs => TestGame.Data.Needs;

    private static Scene Pristine() => TestGame.Route();

    private static VoxelSession NewSession(LessonBook? lessons = null)
    {
        var data = TestGame.Data;
        lessons ??= LessonBook.Parse("{ \"schemaVersion\": 1, \"lessons\": [] }", data.Phrases, data.Speech, data.Bundle.Prompts, data.Locale, data.Npcs.Ids);
        return new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, Pristine(), lessons: lessons) { Day = 10 };
    }

    private static void Night(VoxelSession session) => SaveGame.Sleep(session, SaveGame.Capture(session, Pristine()));

    private static int Count(VoxelSession session, ItemId item) => session.Simulation.Inventory.Count(item);

    private static BlockId BlockAt(VoxelSession session, BlockPos cell) => session.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z);

    // The nth berry bush from the start of the route, picked by hand: the player walks next to it and gathers it.
    private static BlockPos Pick(VoxelSession session, int skip = 0)
    {
        var start = session.Simulation.Player.FeetBlock;
        var bush = session.Scene.Grid.BlocksOfKind(BlockId.BerryBush)
            .OrderBy(b => ((b.X - start.X) * (b.X - start.X)) + ((b.Z - start.Z) * (b.Z - start.Z)))
            .Skip(skip)
            .First();
        Assert.True(VoxelWalker.WalkTo(session, cell => Math.Abs(cell.X - bush.X) + Math.Abs(cell.Z - bush.Z) is > 0 and <= 2 && Math.Abs(cell.Y - bush.Y) <= 1), "no way to the bush");
        Assert.True(VoxelWalker.Gather(session, bush));
        return bush;
    }

    private static IReadOnlyList<string> Lines(GuideTurn turn) => turn.Replies.Concat(turn.FollowUp ?? []).ToList();

    private static VoxelSession At(string npc)
    {
        var session = NewSession();
        Stage.TeleportToNpc(session, npc);
        _ = session.OpenTalk(npc);
        return session;
    }

    private static void Revisit(VoxelSession session, string npc)
    {
        session.EndTalk();
        Stage.TeleportToNpc(session, npc);
        _ = session.OpenTalk(npc);
    }

    // The bush ----------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ABushPickedByHandIsAPlainBushUntilTheNextNightAndThenABerryBushAgain()
    {
        var session = NewSession();
        var before = session.Scene.Grid.ComputeDigestHex();

        var cell = Pick(session);
        Assert.Equal(BlockId.Bush, BlockAt(session, cell));
        Assert.Equal([cell], session.PickedBushes);
        Assert.NotEqual(before, session.Scene.Grid.ComputeDigestHex()); // a gather changes the world's digest, as ever

        Night(session);

        Assert.Equal(BlockId.BerryBush, BlockAt(session, cell));
        Assert.Empty(session.PickedBushes);
        Assert.Equal(1, session.DayResetCount);
        Assert.Equal(before, session.Scene.Grid.ComputeDigestHex()); // grown back, the block is the generated one: the digest is the unpicked world's
    }

    [Fact]
    public void ABushPickedAfterTheNightWaitsForTheNextOne()
    {
        var session = NewSession();
        Night(session);
        var cell = Pick(session);

        Assert.Equal(BlockId.Bush, BlockAt(session, cell));
        Assert.Equal(BlockId.Bush, BlockAt(session, cell)); // nothing grows it back in the day
        Night(session);
        Assert.Equal(BlockId.BerryBush, BlockAt(session, cell));
    }

    [Fact]
    public void ABushPickedThenALoadIsStillPickedAndTheNightAfterItGrowsBack()
    {
        var session = NewSession();
        var cell = Pick(session);
        var document = SaveGame.Capture(session, Pristine());

        var loaded = SaveGame.Restore(document, TestGame.Data, Pristine());

        Assert.Equal(BlockId.Bush, BlockAt(loaded, cell));
        Assert.Equal([cell], loaded.PickedBushes);
        Assert.Equal(0, loaded.DayResetCount); // a load is not a night
        Night(loaded);
        Assert.Equal(BlockId.BerryBush, BlockAt(loaded, cell));
    }

    [Fact]
    public void TwoPicksOfTwoBushesAreBothSavedAndBothGrowBackAndAnotherBushIsLeftAlone()
    {
        var session = NewSession();
        var first = Pick(session);
        var second = Pick(session, 0);
        Assert.NotEqual(first, second);
        var untouched = session.Scene.Grid.BlocksOfKind(BlockId.BerryBush).First(b => b != first && b != second);

        var document = SaveGame.Capture(session, Pristine());
        Assert.Equal(2, document.Systems!.Bushes!.Length);
        var loaded = SaveGame.Restore(document, TestGame.Data, Pristine());
        Assert.Equal([first, second], loaded.PickedBushes);
        Assert.Equal(document.Systems.Bushes!, SaveGame.Capture(loaded, Pristine()).Systems!.Bushes!); // round-trips

        Night(loaded);

        Assert.Equal([BlockId.BerryBush, BlockId.BerryBush, BlockId.BerryBush], [BlockAt(loaded, first), BlockAt(loaded, second), BlockAt(loaded, untouched)]);
    }

    [Fact]
    public void ACellThePlayerBuiltOnIsLetGoAtTheNight()
    {
        var session = NewSession();
        var cell = Pick(session);
        session.Scene.Grid.SetBlock(cell.X, cell.Y, cell.Z, BlockId.Planks);

        Night(session);

        Assert.Equal(BlockId.Planks, BlockAt(session, cell));
        Assert.Empty(session.PickedBushes);
    }

    [Fact]
    public void WithTheNeedsOffNothingIsRecordedAndNothingGrowsBack()
    {
        var session = NewSession();
        session.NeedsEnabled = false;
        var cell = Pick(session);

        Assert.Empty(session.PickedBushes);
        Night(session);
        Assert.Equal(BlockId.Bush, BlockAt(session, cell));
    }

    // The gifts ---------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheVillagersBreadIsGivenOnceADayAndAnotherAskIsAnsweredWithAPoliteNo()
    {
        var session = At("villager");
        var first = session.Say("quiero pan, por favor");
        Assert.Contains("villager.give", first.HostEvents);
        Assert.Equal(1, Count(session, ItemId.Bread));

        var second = session.Say("quiero pan, por favor");

        Assert.DoesNotContain("villager.give", second.HostEvents);
        Assert.Equal(1, Count(session, ItemId.Bread));
        Assert.Contains(Lines(second), static line => line.EndsWith(Fallback, StringComparison.Ordinal)); // the villager's goodbye
        Assert.DoesNotContain(Lines(second), static line => line is "Aquí tienes." or "Toma.");
        Assert.Equal(["villager:bread"], session.GiftsGivenToday);
    }

    [Fact]
    public void ANightMakesTheVillagersBreadAvailableAgain()
    {
        var session = At("villager");
        _ = session.Say("quiero pan, por favor");
        Revisit(session, "villager");
        _ = session.Say("quiero pan, por favor");
        Assert.Equal(1, Count(session, ItemId.Bread));

        Night(session);
        Revisit(session, "villager");
        var third = session.Say("quiero pan, por favor");

        Assert.Contains("villager.give", third.HostEvents);
        Assert.Equal(2, Count(session, ItemId.Bread));
        Assert.Equal(["villager:bread"], session.GiftsGivenToday);
    }

    [Fact]
    public void TheStallsBreadIsGivenOnceADayToo()
    {
        var session = At("shopkeeper");
        _ = session.Say("quiero agua, por favor");
        var yes = session.Say("sí, por favor");
        Assert.Contains("stall.give_bread", yes.HostEvents);
        Assert.Equal(1, Count(session, ItemId.Bread));

        Night(session);
        Revisit(session, "shopkeeper");
        var again = session.Say("quiero pan, por favor");
        Assert.Contains("stall.give_bread", again.HostEvents);
        Assert.Equal(2, Count(session, ItemId.Bread));

        Revisit(session, "shopkeeper");
        var third = session.Say("quiero pan, por favor"); // bread asked for in place of the bottle (V21b) is the bread's gift too
        Assert.DoesNotContain("stall.give_bread", third.HostEvents);
        Assert.Equal(2, Count(session, ItemId.Bread));
        Assert.Contains("El pan cuesta dos monedas.", Lines(third));
    }

    [Fact]
    public void TheGuidesWaterIsTheSameRuleAndAnotherNpcsGiftIsNotCounted()
    {
        var closed = new HashSet<string> { "guide:water-bottle" };
        var slots = new Dictionary<string, string>();
        var water = NpcEffect.Give(ItemId.WaterBottle);

        Assert.Equal(NpcHostEvents.GiftUsed, water.GiftMiss("guide", slots, new Inventory(), TestGame.Data.Nouns, closed));
        Assert.Null(water.GiftMiss("guide", slots, new Inventory(), TestGame.Data.Nouns, new HashSet<string>()));
        Assert.Null(water.GiftMiss("villager", slots, new Inventory(), TestGame.Data.Nouns, closed)); // the key is the NPC's and the item's
        Assert.Null(NpcEffect.Give(ItemId.Coin, 2).GiftMiss("fisher", slots, new Inventory(), TestGame.Data.Nouns, closed)); // a payment is no gift
        Assert.NotNull(Needs.GiftOf("guide", "water-bottle"));
        Assert.Null(Needs.GiftOf("fisher", "coin"));
    }

    [Fact]
    public void ASaleIsNotAGiftAndIsNeverLimited()
    {
        var session = NewSession();
        _ = session.Simulation.GrantItems(ItemId.Coin, 6);
        Stage.TeleportToNpc(session, "baker");
        _ = session.OpenTalk("baker");

        _ = session.Say("quiero un pan, por favor");
        _ = session.Say("quiero un pan, por favor");

        Assert.Equal(2, Count(session, ItemId.Bread));
        Assert.Empty(session.GiftsGivenToday);
    }

    [Fact]
    public void WithTheNeedsOffEveryGiftIsGivenEveryTime()
    {
        var session = At("villager");
        session.NeedsEnabled = false;

        _ = session.Say("quiero pan, por favor");
        _ = session.Say("quiero pan, por favor");

        Assert.Equal(2, Count(session, ItemId.Bread));
        Assert.Empty(session.GiftsGivenToday);
    }

    [Fact]
    public void AGiftALessonStepRequiresIsNotClosedInThatStepWhateverTheDaysCount()
    {
        var data = TestGame.Data;
        // The shipped bread lesson's texts, with one step: the villager gives the bread (its real do step waits for a sale at the baker).
        const string book = """
            { "schemaVersion": 1, "lessons": [ { "id": "numeros-y-pan", "npc": "villager", "level": "a1", "session": 1, "steps": [
              { "id": "n6-buy", "type": "do", "npc": "villager", "trigger": { "event": "item_given", "npc": "villager", "detail": "pan, request" } }
            ] } ] }
            """;
        var lessons = LessonBook.Parse(book, data.Phrases, data.Speech, data.Bundle.Prompts, data.Locale, data.Npcs.Ids);
        var session = NewSession(lessons);
        Stage.TeleportToNpc(session, "villager");
        _ = session.OpenTalk("villager");
        _ = session.Say("quiero pan, por favor");
        Assert.True(session.GiftClosed("villager", ItemId.Bread)); // given today, and no lesson asks for it
        Assert.Contains("villager:bread", session.GiftsGivenToday);
        session.EndTalk();

        Assert.True(session.Lessons.Start("numeros-y-pan"));
        Assert.True(session.Lessons.Engaged);

        Assert.False(session.GiftClosed("villager", ItemId.Bread)); // the lesson's step waits for the villager's gift: it is given, whatever the day's count
        Assert.False(session.GiftClosed("shopkeeper", ItemId.Bread)); // and only that NPC's
    }

    // The save and the data ----------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheGiftsGivenTodayRoundTripThroughASaveAndANightClearsThem()
    {
        var session = At("villager");
        _ = session.Say("quiero pan, por favor");
        var document = SaveGame.Capture(session, Pristine());
        Assert.Equal(["villager:bread"], document.Systems!.Gifts!);

        var loaded = SaveGame.Restore(document, TestGame.Data, Pristine());
        Assert.Equal(["villager:bread"], loaded.GiftsGivenToday);
        Stage.TeleportToNpc(loaded, "villager");
        _ = loaded.OpenTalk("villager");
        var refused = loaded.Say("quiero pan, por favor"); // the use survived the load
        Assert.DoesNotContain("villager.give", refused.HostEvents);

        Night(loaded);
        Assert.Empty(loaded.GiftsGivenToday);
        Assert.Null(SaveGame.Capture(loaded, Pristine()).Systems!.Gifts);
    }

    [Fact]
    public void ASaveWithoutTheRenewalEntriesLoadsAsANewGameHasThem()
    {
        var document = SaveGame.Capture(NewSession(), Pristine());
        var loaded = SaveGame.Restore(document with { Systems = new SystemsSave(document.Systems!.Needs) }, TestGame.Data, Pristine());
        Assert.Empty(loaded.PickedBushes);
        Assert.Empty(loaded.GiftsGivenToday);
    }

    [Fact]
    public void TheShippedRulesAreTheDesignsAndHoldTogether()
    {
        Assert.Equal([("berry-bush", "night")], Needs.Regrowth.Select(static r => (r.Block, r.After)).ToArray());
        Assert.Equal([("shopkeeper", "bread"), ("villager", "bread"), ("guide", "water-bottle")], Needs.Gifts.Select(static g => (g.Npc, g.Item)).ToArray());
        Assert.Empty(GameDataValidator.Validate(TestGame.Data));
    }

    private static IReadOnlyList<string> Problems(NeedsData needs) => GameDataValidator.Validate(TestGame.Data.WithNeeds(needs));

    [Fact]
    public void TheValidatorRefusesARuleWithAnItemAnNpcOrABlockThatDoesNotExist()
    {
        Assert.Contains(Problems(Needs with { Gifts = [new GiftRule("villager", "moon-cheese", "day")] }), static p => p.Contains("'moon-cheese'", StringComparison.Ordinal) && p.Contains("does not exist", StringComparison.Ordinal));
        Assert.Contains(Problems(Needs with { Gifts = [new GiftRule("dragon", "bread", "day")] }), static p => p.Contains("'dragon', which is not an NPC", StringComparison.Ordinal));
        Assert.Contains(Problems(Needs with { Gifts = [new GiftRule("fisher", "bread", "day")] }), static p => p.Contains("fisher gives no 'bread'", StringComparison.Ordinal));
        Assert.Contains(Problems(Needs with { Gifts = [new GiftRule("villager", "bread", "week")] }), static p => p.Contains("per 'week'", StringComparison.Ordinal));
        Assert.Contains(Problems(Needs with { Gifts = [.. Needs.Gifts, new GiftRule("villager", "bread", "day")] }), static p => p.Contains("listed twice", StringComparison.Ordinal));
        Assert.Contains(Problems(Needs with { Regrowth = [new RegrowRule("moon-bush", "night")] }), static p => p.Contains("'moon-bush', which is not a block", StringComparison.Ordinal));
        Assert.Contains(Problems(Needs with { Regrowth = [new RegrowRule("planks", "night")] }), static p => p.Contains("cannot gather", StringComparison.Ordinal));
        Assert.Contains(Problems(Needs with { Regrowth = [new RegrowRule("berry-bush", "never")] }), static p => p.Contains("after 'never'", StringComparison.Ordinal));
    }

    [Fact]
    public void TheRulesAreOptionalInTheFile()
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(GameData.HostText(TestGame.Bundle, NeedsData.RelativePath))!.AsObject();
        _ = json.Remove("regrowth");
        _ = json.Remove("gifts");

        var parsed = NeedsData.Parse(json.ToJsonString());

        Assert.Empty(parsed.Regrowth);
        Assert.Empty(parsed.Gifts);
    }

    [Fact]
    public void TheSameDaysGiveTheSameBushesAndGiftsAgain()
    {
        static (string, string) Play()
        {
            var session = At("villager");
            var cell = Pick(session);
            _ = session.Say("quiero pan, por favor");
            Night(session);
            return (string.Join(';', session.GiftsGivenToday) + BlockAt(session, cell), session.Scene.Grid.ComputeDigestHex() + SaveGame.Capture(session, Pristine()).Systems!.Gifts?.Length);
        }

        Assert.Equal(Play(), Play());
    }
}
