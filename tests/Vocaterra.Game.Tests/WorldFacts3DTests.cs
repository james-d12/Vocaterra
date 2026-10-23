using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Game.Tests;

/// <summary>WorldFacts3D on small hand-made scenes: the eight directions relative to the facing, the distance in words, the nearest choice and its ties, the block-to-noun map.</summary>
public sealed class WorldFacts3DTests
{
    private const int Unit = VoxelPhysics.UnitsPerBlock;

    // A 32 x 4 x 32 scene of air with the given blocks.
    private static WorldFacts3D Facts(params (int X, int Y, int Z, BlockId Id)[] blocks)
    {
        var grid = new Dictionary<(int, int, int), BlockId>();
        foreach (var (x, y, z, id) in blocks)
        {
            grid[(x, y, z)] = id;
        }

        return new WorldFacts3D(32, 4, 32, (x, y, z) => grid.GetValueOrDefault((x, y, z), BlockId.Air), TestGame.Nouns);
    }

    // A player standing in the middle of block (x, z) and facing yaw degrees (0 north, 90 east), as the body reports it.
    private static GuideViewpoint Standing(int x, int z, int yawDegrees) => new(
        new GuideCell(x, 1, z),
        (x * (long)Unit) + (Unit / 2),
        (z * (long)Unit) + (Unit / 2),
        Angles.FromDegrees(yawDegrees));

    // The squared distance in units between two block centres, for the expected values.
    private static long SquaredBlocks(int blocks) => (long)blocks * Unit * blocks * Unit;

    [Theory]
    [InlineData(16, 8, 0, "adelante", 8)]               // ahead, facing north
    [InlineData(16, 24, 0, "detrás", 8)]                // behind
    [InlineData(24, 16, 0, "a la derecha", 8)]          // east is the right of a north-facing player
    [InlineData(8, 16, 0, "a la izquierda", 8)]         // west is the left
    [InlineData(24, 8, 0, "adelante, a la derecha", 11)] // a diagonal: the forward part leads (a tie), the right is added
    [InlineData(8, 24, 0, "detrás, a la izquierda", 11)]
    [InlineData(16, 8, 90, "a la izquierda", 8)]        // facing east, the block to the north is on the left
    [InlineData(24, 16, 90, "adelante", 8)]             // facing east, the block to the east is ahead
    [InlineData(16, 24, 180, "adelante", 8)]            // facing south, the block to the south is ahead
    [InlineData(8, 16, 270, "adelante", 8)]             // facing west, the block to the west is ahead
    public void TheDirectionIsRelativeToTheFacingInSpanish(int x, int z, int yawDegrees, string direction, int blocks)
    {
        var found = Facts((x, 1, z, BlockId.Trunk)).NearestOfNoun(Standing(16, 16, yawDegrees), "árbol");
        Assert.NotNull(found);
        Assert.Equal(direction, found.Direction);
        Assert.Equal(blocks, found.Blocks);
    }

    [Fact]
    public void ALeadingBlockWithASmallOtherComponentSaysOnlyTheLead()
    {
        // Eight blocks ahead and three to the right: the other side is under half the lead, so only "adelante" is said.
        var found = Facts((19, 1, 8, BlockId.Trunk)).NearestOfNoun(Standing(16, 16, 0), "árbol");
        Assert.Equal("adelante", found!.Direction);

        // Eight ahead and five to the right: the other side is at least half the lead, so both are said, forward first.
        var both = Facts((21, 1, 8, BlockId.Trunk)).NearestOfNoun(Standing(16, 16, 0), "árbol");
        Assert.Equal("adelante, a la derecha", both!.Direction);
    }

    [Fact]
    public void ABlockOneStepAwayIsSaidInWordsAndABlockAtNoDistanceIsHere()
    {
        var near = Facts((17, 1, 16, BlockId.Trunk)).NearestOfNoun(Standing(16, 16, 0), "árbol");
        Assert.Equal(1, near!.Blocks);
        Assert.Equal("uno", near.BlocksInWords);
        Assert.Equal("a la derecha", near.Direction);

        var here = Facts((16, 3, 16, BlockId.Leaves)).NearestOfNoun(Standing(16, 16, 0), "árbol"); // the canopy over the player's head
        Assert.Equal(0, here!.Blocks);
        Assert.Equal(string.Empty, here.Direction);
        Assert.Equal("cero", here.BlocksInWords);
    }

    [Theory]
    [InlineData(0L, 0)]
    [InlineData(2047L * 2047L, 0)] // just under half a block
    [InlineData(2048L * 2048L, 1)] // half a block rounds up
    [InlineData((long)Unit * Unit, 1)]
    [InlineData(3L * Unit * 3L * Unit, 3)]
    [InlineData(12L * Unit * 12L * Unit, 12)]
    public void TheDistanceInBlocksRoundsToTheNearestWholeBlock(long squaredUnits, int blocks)
    {
        Assert.Equal(blocks, WorldFacts3D.BlocksOf(squaredUnits));
    }

    [Theory]
    [InlineData(0L, 0L)]
    [InlineData(1L, 1L)]
    [InlineData(15L, 3L)]
    [InlineData(16L, 4L)]
    [InlineData(17L * 17L, 17L)]
    [InlineData(1_000_000_000_000L, 1_000_000L)]
    public void TheIntegerSquareRootIsTheFloorOfTheRoot(long value, long root)
    {
        Assert.Equal(root, WorldFacts3D.IntegerSquareRoot(value));
    }

    [Fact]
    public void TheNearestBlockIsTheOneWithTheSmallestHorizontalDistance()
    {
        // Four blocks east, six north and six west: the east one is the nearest.
        var facts = Facts((20, 1, 16, BlockId.Trunk), (16, 1, 10, BlockId.Trunk), (10, 1, 16, BlockId.Trunk));
        var found = facts.NearestOfNoun(Standing(16, 16, 0), "árbol");
        Assert.Equal(new GuideCell(20, 1, 16), found!.Cell);
        Assert.Equal(SquaredBlocks(4), found.Distance2);
        Assert.Equal(4, found.Blocks);
    }

    [Fact]
    public void TheVerticalDistanceIsNotCounted()
    {
        // A trunk high above and a little further away on the ground: the canopy overhead is the nearer by horizontal distance.
        var facts = Facts((16, 3, 16, BlockId.Leaves), (19, 1, 16, BlockId.Trunk));
        Assert.Equal(new GuideCell(16, 3, 16), facts.NearestOfNoun(Standing(16, 16, 0), "árbol")!.Cell);
    }

    [Fact]
    public void ATieGoesToTheBlockFirstInReadingOrderNorthFirstThenWestThenLower()
    {
        // Two trunks at the same distance, one west and one east: west (smaller x) wins.
        var westEast = Facts((12, 1, 16, BlockId.Trunk), (20, 1, 16, BlockId.Trunk)).NearestOfNoun(Standing(16, 16, 0), "árbol");
        Assert.Equal(new GuideCell(12, 1, 16), westEast!.Cell);

        // A tie between north and south: north (smaller z) wins.
        var northSouth = Facts((16, 1, 12, BlockId.Trunk), (16, 1, 20, BlockId.Trunk)).NearestOfNoun(Standing(16, 16, 0), "árbol");
        Assert.Equal(new GuideCell(16, 1, 12), northSouth!.Cell);

        // The same column: the lower block (smaller y) wins.
        var column = Facts((16, 1, 12, BlockId.Trunk), (16, 2, 12, BlockId.Leaves)).NearestOfNoun(Standing(16, 16, 0), "árbol");
        Assert.Equal(new GuideCell(16, 1, 12), column!.Cell);
    }

    [Fact]
    public void ANounWithNoBlockInTheSceneHasNoNearestPlace()
    {
        var facts = Facts((16, 1, 12, BlockId.Trunk));
        Assert.Null(facts.NearestOfNoun(Standing(16, 16, 0), "piedra")); // no stone in this scene
        Assert.Null(facts.NearestOfNoun(Standing(16, 16, 0), "hacha")); // an item with no block of its own
        Assert.Null(facts.NearestOfNoun(Standing(16, 16, 0), "cueva")); // not a catalogue noun
    }

    [Fact]
    public void TheNounsOfAThingAreListedInLemmaOrder()
    {
        var facts = Facts((5, 1, 5, BlockId.Water), (6, 1, 5, BlockId.Trunk), (7, 1, 5, BlockId.Air));
        Assert.Equal(["agua", "río"], facts.NounsAt(new GuideCell(5, 1, 5)));
        Assert.Equal(["árbol"], facts.NounsAt(new GuideCell(6, 1, 5)));
        Assert.Empty(facts.NounsAt(new GuideCell(7, 1, 5)));
        Assert.Empty(facts.NounsAt(new GuideCell(-1, 1, 5)));
    }

    [Fact]
    public void WaterIsBothAguaAndRioAndTheFirstByLemmaIsTheBlocksNoun()
    {
        var catalogue = TestGame.Nouns;
        Assert.Equal("agua", catalogue.ForBlock(BlockId.Water)!.Lemma);
        Assert.Equal(["agua", "río"], catalogue.NounsOfBlock(BlockId.Water).Select(static n => n.Lemma));
        Assert.Equal("casa", catalogue.ForBlock(BlockId.Roof)!.Lemma);
        Assert.Equal([BlockId.BerryBush], catalogue.BlocksOf("fruta"));
        Assert.Equal("fruta", catalogue.ForItem(ItemId.Berry)!.Lemma); // the berry is the inventory's fruta, not "berry"
        Assert.Equal(["trunk", "leaves", "spruce-trunk", "spruce-leaves", "birch-trunk", "birch-leaves"], catalogue.BlocksOf("árbol").Select(static b => BlockDefinition.Get(b).Name));
    }

    [Fact]
    public void TheSameBlockIsTheSameDistanceFromEveryFacing()
    {
        var facts = Facts((16, 1, 8, BlockId.Water));
        var north = facts.NearestOfNoun(Standing(16, 16, 0), "agua");
        var east = facts.NearestOfNoun(Standing(16, 16, 90), "agua");
        Assert.Equal(north!.Blocks, east!.Blocks);
        Assert.Equal(north.Distance2, east.Distance2);
        Assert.NotEqual(north.Direction, east.Direction);
    }

    // ------------------------------------------------------------------ the large world (S13)

    // The guide's rule written out again over every block of the kinds: the least horizontal distance from the viewpoint's centre, then z, then x, then y.
    private static (long Distance, BlockPos Cell) Brute(VoxelGrid grid, GuideViewpoint from, params BlockId[] kinds)
    {
        (long Distance, BlockPos Cell)? best = null;
        foreach (var kind in kinds)
        {
            foreach (var p in grid.BlocksOfKind(kind))
            {
                var dx = ((long)p.X * Unit) + (Unit / 2) - from.CentreX;
                var dz = ((long)p.Z * Unit) + (Unit / 2) - from.CentreZ;
                var distance = (dx * dx) + (dz * dz);
                if (best is null || distance < best.Value.Distance
                    || (distance == best.Value.Distance && (p.Z, p.X, p.Y).CompareTo((best.Value.Cell.Z, best.Value.Cell.X, best.Value.Cell.Y)) < 0))
                {
                    best = (distance, p);
                }
            }
        }

        return best!.Value;
    }

    [Theory]
    [InlineData("camp.player", "río")]
    [InlineData("camp.guide", "árbol")]
    [InlineData("village", "río")]
    [InlineData("village", "puente")]
    [InlineData("camp.guide", "fruta")]
    [InlineData("lake", "piedra")]
    public void TheChunkSearchAnswersLikeTheRuleOverEveryBlockOfTheLargeWorld(string site, string noun)
    {
        var scene = TestGame.Route();
        var point = scene.Point(site);
        var kinds = TestGame.Nouns.BlocksOf(noun).ToArray();
        var from = new GuideViewpoint(new GuideCell(point.X, point.Y + 1, point.Z), (point.X * (long)Unit) + (Unit / 2) + 1234, (point.Z * (long)Unit) + (Unit / 2) - 777, Angles.FromDegrees(135));
        var expected = Brute(scene.Grid, from, kinds);
        var live = WorldFacts3D.Live(scene.Grid, TestGame.Nouns).NearestOfNoun(from, noun);
        var snapshot = WorldFacts3D.Snapshot(scene.Grid, TestGame.Nouns).NearestOfNoun(from, noun);

        Assert.NotNull(live);
        Assert.Equal(expected.Cell, live.Cell.ToBlock());
        Assert.Equal(expected.Distance, live.Distance2);
        Assert.Equal(live, snapshot); // the copy answers the same
    }

    [Fact]
    public void ADistanceOfHundredsOfBlocksIsRightInNumbersWordsAndDirection()
    {
        // From the player's camp the nearest water is the falls, the pool or the river, a hundred blocks and more away.
        var scene = TestGame.Route();
        var spawn = scene.Spawn.Feet;
        var facts = WorldFacts3D.Live(scene.Grid, TestGame.Nouns);
        var viewpoint = new GuideViewpoint(spawn, (spawn.X * (long)Unit) + (Unit / 2), (spawn.Z * (long)Unit) + (Unit / 2), scene.Spawn.Yaw);
        var river = facts.NearestOfNoun(viewpoint, "río");

        Assert.NotNull(river);
        Assert.InRange(river.Blocks, 80, 400);
        Assert.Equal(Vocaterra.Nlp.Language.Normalization.SpanishNumbers.ToWords(river.Blocks), river.BlocksInWords);
        Assert.Contains(river.Direction, new[] { "adelante", "a la izquierda", "adelante, a la izquierda", "a la derecha", "adelante, a la derecha", "detrás", "detrás, a la izquierda", "detrás, a la derecha" });

        // The direction words and the rounding hold at any distance: 250 blocks ahead and 130 to the right (the other side reaches half the lead), and the number in words.
        Assert.Equal("adelante, a la derecha", WorldFacts3D.DirectionPhrase(250L * Unit, 130L * Unit));
        Assert.Equal("detrás, a la izquierda", WorldFacts3D.DirectionPhrase(-300L * Unit, -200L * Unit));
        Assert.Equal("a la derecha", WorldFacts3D.DirectionPhrase(10L * Unit, 300L * Unit));
        Assert.Equal(250, WorldFacts3D.BlocksOf(250L * Unit * 250L * Unit));
        Assert.Equal(361, WorldFacts3D.BlocksOf(((255L * Unit) * (255L * Unit)) * 2));
        Assert.Equal("doscientos cincuenta", Vocaterra.Nlp.Language.Normalization.SpanishNumbers.ToWords(250));
    }

    [Fact]
    public void ASnapshotOfTheLargeWorldIsQuickAndAnswersEveryNounOfTheRoute()
    {
        var scene = TestGame.Route();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var snapshot = WorldFacts3D.Snapshot(scene.Grid, TestGame.Nouns);
        var copied = watch.Elapsed.TotalMilliseconds;
        var spawn = scene.Spawn.Feet;
        var viewpoint = new GuideViewpoint(spawn, (spawn.X * (long)Unit) + (Unit / 2), (spawn.Z * (long)Unit) + (Unit / 2), 0);
        watch.Restart();
        foreach (var noun in new[] { "árbol", "río", "fruta", "mesa", "piedra", "camino" })
        {
            Assert.NotNull(snapshot.NearestOfNoun(viewpoint, noun));
        }

        // Taking the copy for a turn, and six searches over it, are a small part of a second even on a slow machine.
        Assert.True(copied < 1500, $"the snapshot took {copied:F0} ms");
        Assert.True(watch.Elapsed.TotalMilliseconds < 3000, $"six searches took {watch.Elapsed.TotalMilliseconds:F0} ms");
        Assert.Equal(BlockId.Air, snapshot.BlockAt(new GuideCell(5, 127, 5)));
        Assert.Equal(BlockId.Water, snapshot.BlockAt(new GuideCell(scene.Point("lake").X, scene.Point("lake").Y, scene.Point("lake").Z)));
    }
}
