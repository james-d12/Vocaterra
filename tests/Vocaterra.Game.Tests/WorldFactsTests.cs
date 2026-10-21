using Vocaterra.Game;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Generation;
using Vocaterra.Simulation.Physics;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Game.Tests;

/// <summary>WorldFacts on small hand-made grids: the nearest tile, its tie-breaks, the direction phrases and the distance in words.</summary>
public sealed class WorldFactsTests
{
    private static WorldFacts Facts(int width, int height, params (int X, int Y, TileId Tile)[] tiles)
    {
        var grid = new Dictionary<(int, int), TileId>();
        foreach (var (x, y, tile) in tiles)
        {
            grid[(x, y)] = tile;
        }

        return new WorldFacts(width, height, (x, y) => grid.GetValueOrDefault((x, y), TileId.Air));
    }

    [Fact]
    public void TheNearestOfSeveralIsTheOneWithTheSmallestStraightLineDistance()
    {
        var facts = Facts(100, 100, (10, 50, TileId.Trunk), (60, 50, TileId.Trunk), (53, 52, TileId.Trunk));

        var found = facts.Nearest(new TilePoint(50, 50), [TileId.Trunk]);

        Assert.NotNull(found);
        Assert.Equal(new TilePoint(53, 52), found.Tile);
        Assert.Equal(3, found.Dx);
        Assert.Equal(2, found.Dy);
        Assert.Equal(3, found.Blocks);
    }

    [Fact]
    public void ATieOnDistanceGoesToTheHigherRowThenTheLeftColumn()
    {
        var facts = Facts(
            100,
            100,
            (55, 50, TileId.Trunk), // right, 5 away
            (45, 50, TileId.Trunk), // left, 5 away
            (50, 45, TileId.Trunk), // above, 5 away
            (50, 55, TileId.Trunk)); // below, 5 away
        var from = new TilePoint(50, 50);

        Assert.Equal(new TilePoint(50, 45), facts.Nearest(from, [TileId.Trunk])!.Tile);

        var horizontalOnly = Facts(100, 100, (55, 50, TileId.Trunk), (45, 50, TileId.Trunk));
        Assert.Equal(new TilePoint(45, 50), horizontalOnly.Nearest(from, [TileId.Trunk])!.Tile);

        // Different tiles at the same distance on the same row: (47, 54) and (53, 54) are both 5 away; the left one wins.
        var sameRow = Facts(100, 100, (53, 54, TileId.Stone), (47, 54, TileId.Stone));
        Assert.Equal(new TilePoint(47, 54), sameRow.Nearest(from, [TileId.Stone])!.Tile);
    }

    [Fact]
    public void TheAnswerDoesNotDependOnTheOrderOfTheKindsOrOnAskingTwice()
    {
        var facts = Facts(100, 100, (60, 40, TileId.Trunk), (40, 60, TileId.Stone), (50, 65, TileId.Grass));
        var from = new TilePoint(50, 50);

        var a = facts.Nearest(from, [TileId.Trunk, TileId.Stone, TileId.Grass]);
        var b = facts.Nearest(from, [TileId.Grass, TileId.Stone, TileId.Trunk]);
        var c = facts.Nearest(from, [TileId.Trunk, TileId.Stone, TileId.Grass]);

        Assert.Equal(a, b);
        Assert.Equal(a, c);
        Assert.Equal(new TilePoint(60, 40), a!.Tile); // (60, 40) and (40, 60) are both 200 away; the one on row 40 is higher
    }

    [Fact]
    public void NothingInRangeGivesNull()
    {
        var facts = Facts(300, 300, (250, 50, TileId.Trunk));

        Assert.Null(facts.Nearest(new TilePoint(50, 50), [TileId.Trunk], radius: 64));
        Assert.NotNull(facts.Nearest(new TilePoint(50, 50), [TileId.Trunk], radius: 250));
        Assert.Null(facts.Nearest(new TilePoint(50, 50), []));
        Assert.Null(facts.Nearest(new TilePoint(50, 50), [TileId.Stone]));
    }

    [Fact]
    public void TheSearchStaysInsideTheGrid()
    {
        var facts = Facts(40, 40, (0, 0, TileId.Trunk), (39, 39, TileId.Trunk));

        Assert.Equal(new TilePoint(0, 0), facts.Nearest(new TilePoint(2, 2), [TileId.Trunk], radius: 500)!.Tile);
        Assert.Equal(new TilePoint(39, 39), facts.Nearest(new TilePoint(38, 38), [TileId.Trunk], radius: 500)!.Tile);
    }

    [Theory]
    [InlineData(12, 0, "a la derecha")]
    [InlineData(-12, 0, "a la izquierda")]
    [InlineData(0, -7, "arriba")]
    [InlineData(0, 7, "abajo")]
    [InlineData(12, 3, "a la derecha")] // the other axis is under half the lead
    [InlineData(12, 6, "abajo, a la derecha")] // half the lead and at least two
    [InlineData(-12, -6, "arriba, a la izquierda")]
    [InlineData(3, 12, "abajo")]
    [InlineData(6, 12, "abajo, a la derecha")]
    [InlineData(5, 5, "abajo, a la derecha")] // a tie leads with the horizontal axis, and the other axis is as large
    [InlineData(5, 1, "a la derecha")] // one block off is not worth saying
    [InlineData(-1, 0, "a la izquierda")]
    [InlineData(0, 0, "")]
    public void TheDirectionIsTheLargerAxisWithTheOtherAddedWhenItMatters(int dx, int dy, string expected)
    {
        Assert.Equal(expected, WorldFacts.DirectionPhrase(dx, dy));
    }

    [Theory]
    [InlineData(1, "uno")]
    [InlineData(9, "nueve")]
    [InlineData(12, "doce")]
    [InlineData(21, "veintiuno")]
    [InlineData(64, "sesenta y cuatro")]
    public void TheDistanceIsSpelledInSpanish(int blocks, string words)
    {
        Assert.Equal(words, WorldFacts.NumberInWords(blocks));
        Assert.Equal(words, new NearestTile(new TilePoint(blocks, 0), TileId.Trunk, blocks, 0).BlocksInWords);
    }

    [Fact]
    public void TheNounsOfTheWorldMapToTheirTilesAndBack()
    {
        Assert.Equal([TileId.Trunk], WorldFacts.TilesOfNoun("árbol"));
        Assert.Equal([TileId.Stone], WorldFacts.TilesOfNoun("piedra"));
        Assert.Equal([TileId.Stone], WorldFacts.TilesOfNoun("roca"));
        Assert.Equal([TileId.Grass], WorldFacts.TilesOfNoun("hierba"));
        Assert.Equal([TileId.Dirt], WorldFacts.TilesOfNoun("tierra"));
        Assert.Empty(WorldFacts.TilesOfNoun("cueva"));
        Assert.Equal("árbol", WorldFacts.NounOfTile(TileId.Trunk));
        Assert.Equal("piedra", WorldFacts.NounOfTile(TileId.Stone));
        Assert.Null(WorldFacts.NounOfTile(TileId.Air));
    }

    [Fact]
    public void TheNearestTreeOnTheRealWorldIsTheBruteForceAnswerFromAnywhereOnTheSurface()
    {
        var world = WorldGenerator.Generate(42);
        var facts = new WorldFacts(world);
        for (var dx = -30; dx <= 30; dx += 6)
        {
            var from = new TilePoint(world.Spawn.X + dx, world.Spawn.Y - 1);
            var found = facts.NearestOfNoun(from, "árbol");
            Assert.NotNull(found);
            Assert.Equal(TestGame.BruteForceNearest(world, from, TileId.Trunk), found.Tile);
        }
    }

    [Fact]
    public void TheCentreTileOfThePlayerIsTheTileUnderTheMiddleOfTheBody()
    {
        var player = new PlayerState(
            (10 * PhysicsConstants.UnitsPerTile) + 100,
            (20 * PhysicsConstants.UnitsPerTile) + 100,
            0,
            0,
            true,
            1);

        var center = WorldFacts.CenterTile(player);

        Assert.Equal(new TilePoint(10, 21), center);
    }
}
