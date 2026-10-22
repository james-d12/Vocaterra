using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Game.Tests;

/// <summary>
/// The camp objective played headless from the spawn on the bundle's route: five wood, three berries, the torch crafted at the workbench and
/// placed at the fire ring. The checklist reads <c>madera 5/5 · frutas 3/3 · antorcha 1/1 · agua 0/1</c>; the water step waits for its host event.
/// </summary>
public sealed class CampObjectiveTests
{
    private const string ThreeOfFour = "Campamento: madera 5/5 · frutas 3/3 · antorcha 1/1 · agua 0/1";

    /// <summary>Plays the camp run once and returns the session and what it did.</summary>
    internal static (VoxelSession Session, List<WordCard> Cards, string Digest) PlayCamp(ulong seed = TestGame.Seed)
    {
        var scene = SceneBuilder.Build(SceneLayout.Load(Path.Combine(GameBundle.ResolveDirectory(), "game", "scene.json")), seed);
        var session = new VoxelSession(TestGame.Bundle, seed, GuideSession.DefaultStartedAt, TestGame.Nouns, scene, CreditRules.FullTyped);
        var cards = new List<WordCard>();
        var grid = scene.Grid;

        // Five trunks: each gathered block is one wood, the trunk's own cell turns to air. Walk next to the nearest ones, one tree at a time.
        var gathered = 0;
        while (gathered < 5)
        {
            var trunk = FindTrunk(grid, session.Simulation.Player.FeetBlock, out var stand);
            Assert.True(trunk is not null && stand is not null, "a tree is near the spawn");
            var standing = stand!.Value;
            Assert.True(VoxelWalker.WalkTo(session, cell => cell == standing));
            Assert.True(VoxelWalker.Gather(session, trunk!.Value));
            gathered++;
            cards.AddRange(session.TakeWordCards());
        }

        // Three berry bushes: each one gives a berry and becomes a plain bush.
        var berries = 0;
        while (berries < 3)
        {
            var bush = FindBerryBush(grid, session.Simulation.Player.FeetBlock, out var stand);
            Assert.True(bush is not null, "a berry bush is left");
            Assert.True(VoxelWalker.WalkTo(session, cell => cell == stand!.Value));
            Assert.True(VoxelWalker.Gather(session, bush!.Value));
            berries++;
            cards.AddRange(session.TakeWordCards());
        }

        // The workbench: walk within reach of the mesa and craft the torch (two wood).
        var table = FindBlock(grid, BlockId.Table);
        Assert.True(VoxelWalker.WalkTo(session, cell => Distance(cell, table) <= 3.0));
        var torchRecipe = session.Recipes.IndexOf("antorcha");
        Assert.True(torchRecipe >= 0);
        var torchesBefore = session.Simulation.Inventory.Count(ItemId.Torch);
        VoxelWalker.Craft(session, torchRecipe);
        Assert.Equal(torchesBefore + 1, session.Simulation.Inventory.Count(ItemId.Torch));
        cards.AddRange(session.TakeWordCards());

        // The fire ring: the torch is set on the rim, on top of a stone, from a cell within reach of it.
        var ringBlock = FindBlock(grid, BlockId.FireRing);
        var rim = new BlockPos(ringBlock.X, ringBlock.Y + 1, ringBlock.Z);
        Assert.True(VoxelWalker.WalkTo(session, cell => Distance(cell, rim) <= 3.0));
        Assert.True(VoxelWalker.Place(session, ItemId.Torch, rim));
        Assert.Equal(BlockId.Torch, grid.GetBlock(rim.X, rim.Y, rim.Z));
        return (session, cards, session.Simulation.ComputeStateDigest());
    }

    [Fact]
    public void FromSpawnGatheringAndCraftingReadThreeOfFourSteps()
    {
        var (session, _, _) = PlayCamp();

        Assert.Equal(ThreeOfFour, session.Objective.Summary);
        Assert.False(session.Objective.Complete);
        Assert.Equal(3, session.Simulation.Inventory.Count(ItemId.Wood));
        Assert.Equal(3, session.Simulation.Inventory.Count(ItemId.Berry));
        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Torch));
    }

    [Fact]
    public void TheWaterStepCompletesOnItsHostEventAndTheChecklistIsDone()
    {
        var (session, _, _) = PlayCamp();

        var completed = session.HostEvent(Objective.WaterEvent);

        Assert.Single(completed);
        Assert.Equal("agua", completed[0].Label);
        Assert.True(session.Objective.Complete);
        Assert.Equal("Campamento: madera 5/5 · frutas 3/3 · antorcha 1/1 · agua 1/1", session.Objective.Summary);
    }

    [Fact]
    public void TheFirstPickupsShowTheWordCardsOnceEach()
    {
        var (session, cards, _) = PlayCamp();

        // madera (the first trunk), fruta (the first berry), antorcha (the first craft); no card for the second or later pickups.
        Assert.Equal(new[] { "madera", "fruta", "antorcha" }, cards.Select(static c => c.Lemma).ToArray());
        Assert.Equal(new[] { "la madera", "la fruta", "la antorcha" }, cards.Select(static c => c.Phrase).ToArray());
        Assert.Empty(session.TakeWordCards());
    }

    [Fact]
    public void ACardCountsAsHeardAndReadNeverAsAUse()
    {
        var (session, _, _) = PlayCamp();

        foreach (var lemma in new[] { "madera", "fruta", "antorcha" })
        {
            var record = session.Words.RecordOf(lemma);
            Assert.True(record.Heard >= 1, lemma);
            Assert.True(record.Read >= 1, lemma);
            Assert.Equal(0, record.Spoken);
            Assert.Equal(0, record.CreditUnits);
        }
    }

    [Fact]
    public void TheSameRunGivesTheSameState()
    {
        var (_, _, first) = PlayCamp();
        var (_, _, second) = PlayCamp();

        Assert.Equal(first, second);
    }

    // The nearest trunk block (by the body's distance) and a standing cell within reach of it: the cell on the same ground, nearest to the trunk.
    private static BlockPos? FindTrunk(VoxelGrid grid, BlockPos from, out BlockPos? stand)
    {
        BlockPos? best = null;
        stand = null;
        var bestDistance = double.MaxValue;
        for (var y = 0; y < grid.SizeY; y++)
        {
            for (var z = 0; z < grid.SizeZ; z++)
            {
                for (var x = 0; x < grid.SizeX; x++)
                {
                    if (grid.GetBlock(x, y, z) != BlockId.Trunk)
                    {
                        continue;
                    }

                    var cell = new BlockPos(x, y, z);
                    var d = Distance(cell, from);
                    if (d < bestDistance && Standing(grid, cell, out var cellStand))
                    {
                        bestDistance = d;
                        best = cell;
                        stand = cellStand;
                    }
                }
            }
        }

        return best;
    }

    private static BlockPos? FindBerryBush(VoxelGrid grid, BlockPos from, out BlockPos? stand)
    {
        BlockPos? best = null;
        stand = null;
        var bestDistance = double.MaxValue;
        for (var y = 0; y < grid.SizeY; y++)
        {
            for (var z = 0; z < grid.SizeZ; z++)
            {
                for (var x = 0; x < grid.SizeX; x++)
                {
                    if (grid.GetBlock(x, y, z) != BlockId.BerryBush)
                    {
                        continue;
                    }

                    var cell = new BlockPos(x, y, z);
                    var d = Distance(cell, from);
                    if (d < bestDistance && Standing(grid, cell, out var cellStand))
                    {
                        bestDistance = d;
                        best = cell;
                        stand = cellStand;
                    }
                }
            }
        }

        return best;
    }

    // A feet cell next to the target block (on its own level or the one below), from which the block is in reach.
    private static bool Standing(VoxelGrid grid, BlockPos target, out BlockPos? stand)
    {
        for (var dz = -2; dz <= 2; dz++)
        {
            for (var dx = -2; dx <= 2; dx++)
            {
                var feet = new BlockPos(target.X + dx, target.Y, target.Z + dz);
                if ((dx == 0 && dz == 0) || grid.IsSolid(feet.X, feet.Y, feet.Z) || grid.IsSolid(feet.X, feet.Y + 1, feet.Z) || !grid.IsSolid(feet.X, feet.Y - 1, feet.Z))
                {
                    continue;
                }

                if (Distance(feet, target) <= 2.5)
                {
                    stand = feet;
                    return true;
                }
            }
        }

        stand = null;
        return false;
    }

    private static BlockPos FindBlock(VoxelGrid grid, BlockId id)
    {
        for (var y = 0; y < grid.SizeY; y++)
        {
            for (var z = 0; z < grid.SizeZ; z++)
            {
                for (var x = 0; x < grid.SizeX; x++)
                {
                    if (grid.GetBlock(x, y, z) == id)
                    {
                        return new BlockPos(x, y, z);
                    }
                }
            }
        }

        throw new InvalidOperationException($"No {id} in the scene.");
    }

    internal static double Distance(BlockPos a, BlockPos b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}
