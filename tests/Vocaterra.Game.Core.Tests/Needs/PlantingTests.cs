using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Needs;

/// <summary>The berry planted at the camp (N08, design decision 14): the rule in data, the planting, the night, the cap, the save and the input split.</summary>
public sealed class PlantingTests
{
    private static NeedsData Needs => TestGame.Data.Needs;

    private static PlantingRule Good() => new("berry", "bush", "berry-bush", 8, 6);

    private static IReadOnlyList<string> Problems(PlantingRule rule) => GameDataValidator.Validate(TestGame.Data.WithNeeds(Needs with { Planting = rule }));

    private static VoxelSession NewSession()
    {
        var data = TestGame.Data;
        var lessons = LessonBook.Parse("{ \"schemaVersion\": 1, \"lessons\": [] }", data.Phrases, data.Speech, data.Bundle.Prompts, data.Locale, data.Npcs.Ids);
        return new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), lessons: lessons) { Day = 10 };
    }

    private static void Night(VoxelSession session) => SaveGame.Sleep(session, SaveGame.Capture(session));

    private static int Berries(VoxelSession session) => session.Simulation.Inventory.Count(ItemId.Berry);

    private static BlockId BlockAt(VoxelSession session, BlockPos cell) => session.Scene.Grid.GetBlock(cell.X, cell.Y, cell.Z);

    private static IReadOnlyList<WorldEvent> Named(VoxelSession session, string name) => session.Events.OfType<WorldEvent>().Where(e => e.Name == name).ToList();

    private static BlockPos Above(BlockPos ground) => ground.Offset(BlockFace.Up);

    // The grass cells with air above, sorted by their distance from the camp's fire ring (squared, horizontally), nearest first.
    private static IReadOnlyList<BlockPos> GrassTops(VoxelSession session)
    {
        var camp = session.Scene.Points[VoxelSession.CampFireRing];
        var tops = new List<BlockPos>();
        for (var x = camp.X - 16; x <= camp.X + 16; x++)
        {
            for (var z = camp.Z - 16; z <= camp.Z + 16; z++)
            {
                for (var y = camp.Y + 8; y >= camp.Y - 8; y--)
                {
                    if (!session.Scene.Grid.InBounds(x, y, z) || session.Scene.Grid.GetBlock(x, y, z) is not BlockId.Grass)
                    {
                        continue;
                    }

                    if (session.Scene.Grid.InBounds(x, y + 1, z) && session.Scene.Grid.GetBlock(x, y + 1, z) == BlockId.Air)
                    {
                        tops.Add(new BlockPos(x, y, z));
                    }

                    break;
                }
            }
        }

        return tops.OrderBy(t => ((long)(t.X - camp.X) * (t.X - camp.X)) + ((long)(t.Z - camp.Z) * (t.Z - camp.Z))).ToList();
    }

    private static BlockPos NearTop(VoxelSession session) => GrassTops(session).First(t => InRadius(session, t));

    private static bool InRadius(VoxelSession session, BlockPos t)
    {
        var camp = session.Scene.Points[VoxelSession.CampFireRing];
        return ((long)(t.X - camp.X) * (t.X - camp.X)) + ((long)(t.Z - camp.Z) * (t.Z - camp.Z)) <= 64;
    }

    [Fact]
    public void TheShippedPlantingIsTheDesignsAndHoldsTogether()
    {
        Assert.Equal(Good(), Needs.Planting);
        Assert.Empty(GameDataValidator.Validate(TestGame.Data));
    }

    [Fact]
    public void TheValidatorRefusesAnUnknownItemOrBlock()
    {
        Assert.Contains(Problems(Good() with { Item = "moon-cheese" }), static p => p.Contains("'moon-cheese', which is not an item", StringComparison.Ordinal));
        Assert.Contains(Problems(Good() with { Block = "moon-bush" }), static p => p.Contains("'moon-bush', which is not a block", StringComparison.Ordinal));
        Assert.Contains(Problems(Good() with { GrownBlock = "moon-bush" }), static p => p.Contains("'moon-bush', which is not a block", StringComparison.Ordinal));
    }

    [Fact]
    public void TheValidatorRefusesAGrownBlockWithNoRegrowthRuleAndAPlantedBlockItWouldNotRegrowFrom()
    {
        Assert.Contains(Problems(Good() with { GrownBlock = "trunk" }), static p => p.Contains("which has no regrowth rule", StringComparison.Ordinal));
        Assert.Contains(Problems(Good() with { Block = "grass" }), static p => p.Contains("regrows from 'bush'", StringComparison.Ordinal));
    }

    [Fact]
    public void TheValidatorRefusesARadiusOrACountOfZeroOrLess()
    {
        Assert.Contains(Problems(Good() with { RadiusBlocks = 0 }), static p => p.Contains("radius is 0", StringComparison.Ordinal));
        Assert.Contains(Problems(Good() with { RadiusBlocks = -2 }), static p => p.Contains("radius is -2", StringComparison.Ordinal));
        Assert.Contains(Problems(Good() with { MaxBushes = 0 }), static p => p.Contains("allows 0 bushes", StringComparison.Ordinal));
    }

    [Fact]
    public void ThePlantingIsOptionalInTheFile()
    {
        var json = System.Text.Json.Nodes.JsonNode.Parse(GameData.HostText(TestGame.Bundle, NeedsData.RelativePath))!.AsObject();
        _ = json.Remove("planting");

        var parsed = NeedsData.Parse(json.ToJsonString());

        Assert.Null(parsed.Planting);
        Assert.Empty(GameDataValidator.Validate(TestGame.Data.WithNeeds(parsed)));
    }

    [Fact]
    public void ABerryPlantedInsideTheRadiusBecomesAPlainBushAndALoggedEvent()
    {
        var session = NewSession();
        _ = session.Simulation.GrantItems(ItemId.Berry, 1);
        var ground = NearTop(session);

        Assert.Equal(VoxelSession.PlantResult.Planted, session.PlantOn(ground));

        Assert.Equal(0, Berries(session));
        Assert.Equal(BlockId.Bush, BlockAt(session, Above(ground)));
        Assert.Equal(1, session.PlantedAlive);
        Assert.Equal([Above(ground)], session.PlantedBushes);
        Assert.Contains(Above(ground), session.PickedBushes);
        Assert.Equal("fruta", Assert.Single(Named(session, WorldEvents.BushPlanted)).Detail);
    }

    [Fact]
    public void ThePlantedBushGrowsIntoABerryBushAtTheNightAndPickingGivesABerry()
    {
        var session = NewSession();
        _ = session.Simulation.GrantItems(ItemId.Berry, 1);
        var ground = NearTop(session);
        _ = session.PlantOn(ground);

        Night(session);
        Assert.Equal(BlockId.BerryBush, BlockAt(session, Above(ground)));

        Assert.True(VoxelWalker.WalkTo(session, cell => Math.Abs(cell.X - ground.X) + Math.Abs(cell.Z - ground.Z) is > 0 and <= 2 && Math.Abs(cell.Y - ground.Y) <= 2));
        Assert.True(VoxelWalker.Gather(session, Above(ground)));
        Assert.Equal(1, Berries(session));
        Assert.Equal(BlockId.Bush, BlockAt(session, Above(ground)));
    }

    [Fact]
    public void OutsideTheRadiusOnStoneOnAnOccupiedCellAndWithNoBerryNothingChanges()
    {
        var session = NewSession();
        _ = session.Simulation.GrantItems(ItemId.Berry, 1);
        var far = GrassTops(session).First(t => !InRadius(session, t));
        Assert.Equal(VoxelSession.PlantResult.Refused, session.PlantOn(far));
        Assert.Equal(BlockId.Air, BlockAt(session, Above(far)));
        Assert.Equal(1, Berries(session));

        var near = NearTop(session);
        session.Scene.Grid.SetBlock(near.X, near.Y, near.Z, BlockId.Stone);
        Assert.Equal(VoxelSession.PlantResult.NotApplied, session.PlantOn(near));
        Assert.Equal(1, Berries(session));

        var occupied = GrassTops(session).First(t => InRadius(session, t) && t != near);
        session.Scene.Grid.SetBlock(Above(occupied).X, Above(occupied).Y, Above(occupied).Z, BlockId.Stone);
        Assert.Equal(VoxelSession.PlantResult.Refused, session.PlantOn(occupied));
        Assert.Equal(BlockId.Stone, BlockAt(session, Above(occupied)));
        Assert.Equal(1, Berries(session));

        var none = NewSession();
        Assert.Equal(VoxelSession.PlantResult.NotApplied, none.PlantOn(NearTop(none)));
        Assert.Equal(0, none.PlantedAlive);
        Assert.Empty(Named(session, WorldEvents.BushPlanted));
    }

    [Fact]
    public void TheCampHasSixPlantedBushesAtMostAndTheSeventhBerryIsKept()
    {
        var session = NewSession();
        _ = session.Simulation.GrantItems(ItemId.Berry, 7);
        var tops = GrassTops(session).Where(t => InRadius(session, t)).Take(7).ToList();

        var results = tops.Select(session.PlantOn).ToList();

        Assert.Equal(6, results.Count(static r => r == VoxelSession.PlantResult.Planted));
        Assert.Equal(VoxelSession.PlantResult.Refused, results[6]);
        Assert.Equal(6, session.PlantedAlive);
        Assert.Equal(1, Berries(session));
        Assert.Equal(BlockId.Air, BlockAt(session, Above(tops[6])));
    }

    [Fact]
    public void WithTheNeedsOffNothingIsPlantedAndNothingGrowsBack()
    {
        var session = NewSession();
        session.NeedsEnabled = false;
        _ = session.Simulation.GrantItems(ItemId.Berry, 1);
        var ground = NearTop(session);

        Assert.Equal(VoxelSession.PlantResult.NotApplied, session.PlantOn(ground));
        Night(session);

        Assert.Equal(1, Berries(session));
        Assert.Equal(BlockId.Air, BlockAt(session, Above(ground)));
        Assert.Equal(0, session.PlantedAlive);
    }

    [Fact]
    public void APlantedBushRoundTripsThroughASaveAndANightAfterTheLoadGrowsIt()
    {
        var session = NewSession();
        _ = session.Simulation.GrantItems(ItemId.Berry, 2);
        var ground = NearTop(session);
        _ = session.PlantOn(ground);
        var document = SaveGame.Capture(session);

        var loaded = SaveGame.Restore(document, TestGame.Data, TestGame.Route());

        Assert.Equal([Above(ground)], loaded.PlantedBushes);
        Assert.Equal(1, loaded.PlantedAlive);
        Night(loaded);
        Assert.Equal(BlockId.BerryBush, BlockAt(loaded, Above(ground)));
    }

    [Fact]
    public void ASaveWithoutThePlantedListLoadsWithNoPlantedBushes()
    {
        var session = NewSession();
        _ = session.Simulation.GrantItems(ItemId.Berry, 1);
        _ = session.PlantOn(NearTop(session));
        var document = SaveGame.Capture(session);
        var without = document with { Systems = document.Systems! with { Planted = null } };

        var loaded = SaveGame.Restore(without, TestGame.Data, TestGame.Route());

        Assert.Equal(0, loaded.PlantedAlive);
    }

    [Fact]
    public void TheRightClickPlantsOnGroundAndTheClickInTheAirStillEats()
    {
        var click = new TestInputFrame { RightDown = true };
        var needs = TestGame.Data.Needs;
        var berry = new ItemStack(ItemId.Berry, 1);
        var grass = new BlockRayHit(new BlockPos(0, 0, 0), BlockId.Grass, BlockFace.Up, new BlockPos(0, 1, 0), 10);
        var stone = grass with { Id = BlockId.Stone };

        Assert.True(PlantingUse.Applies(click, true, berry, grass, needs));
        Assert.False(PlantingUse.Applies(click, true, berry, null, needs)); // in the air: eaten
        Assert.False(PlantingUse.Applies(click, true, berry, stone, needs)); // not on the ground: eaten
        Assert.False(PlantingUse.Applies(click, true, new ItemStack(ItemId.Bread, 1), grass, needs));
        Assert.True(FoodItemUse.Applies(click, true, berry, needs)); // eating is unchanged
    }

    [Fact]
    public void EatingABerryStillWorksAndAPlantingIsTheSameInTwoRuns()
    {
        static (string, int) Play()
        {
            var session = NewSession();
            _ = session.Simulation.GrantItems(ItemId.Berry, 2);
            _ = session.PlantOn(NearTop(session));
            Night(session);
            return (session.Scene.Grid.ComputeDigestHex(), session.PlantedAlive);
        }

        Assert.Equal(Play(), Play());

        var session = NewSession();
        session.SetNeed(NeedMeter.Hunger, 40);
        _ = session.Simulation.GrantItems(ItemId.Berry, 1);
        Assert.Equal(NeedActionResult.Done, session.EatItem(ItemId.Berry));
        Assert.Equal(0, Berries(session));
        Assert.Single(Named(session, WorldEvents.MealEaten));
    }

    [Fact]
    public void TheScriptedPlantStepReadsACellAsXYZAndRefusesTheRest()
    {
        Assert.True(ScenarioScript.TryParseCell("10,4,-12", out var cell));
        Assert.Equal(new BlockPos(10, 4, -12), cell);
        Assert.Null(ScenarioScript.ParseCell("10,4"));
        Assert.Null(ScenarioScript.ParseCell("a,4,12"));
        Assert.Null(ScenarioScript.ParseCell(string.Empty));
    }
}
