using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.Voxel;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.World.Generation;

/// <summary>The inn and the meadows (Y04, settlement plan 9.1 and 9.2): the building at the village's west edge, its inside, its bed, the place its keeper will stand, and the two cleared meadows.</summary>
public sealed class InnTests
{
    private const ulong Seed = 42;

    // The inn's north-west corner (village dx -16, dz 8); col c, row r of the inside stamp is the block (X0 + c, Z0 + r).
    private static (Scene Scene, int X0, int Z0, int Floor) Inn()
    {
        var scene = VoxelTestSupport.BuildRoute(Seed);
        var village = scene.Point("village");
        var inn = scene.Point("inn");
        return (scene, village.X - 16, village.Z + 8, inn.Y);
    }

    private static BlockId At(Scene scene, int x, int y, int z) => scene.Grid.GetBlock(x, y, z);

    [Fact]
    public void Given_TheBundle_When_TheSceneIsBuilt_Then_TheInnIsALabelledNineBySevenHouseWithItsDoorInTheNorthWall()
    {
        var (scene, x0, z0, floor) = Inn();

        Assert.Equal("posada", scene.LabelOf("inn"));
        Assert.Equal(new BlockPos(x0 + 4, floor, z0 + 3), scene.Point("inn")); // the middle of the floor
        Assert.Equal(BlockId.Door, At(scene, x0 + 4, floor + 1, z0));
        Assert.Equal(BlockId.Door, At(scene, x0 + 4, floor + 2, z0));
        Assert.Equal(BlockId.Timber, At(scene, x0, floor + 1, z0)); // the corner posts
        Assert.Equal(BlockId.Timber, At(scene, x0 + 8, floor + 1, z0 + 6));
        Assert.Equal(BlockId.Planks, At(scene, x0 + 8, floor, z0 + 6));
        Assert.Equal(BlockId.Roof, At(scene, x0 + 4, floor + 4, z0 + 3));
    }

    [Fact]
    public void Given_TheInn_When_ItsInsideIsRead_Then_TheCounterThePartitionTheGapAndTheBedsAreWhereTheSectionSaysTheyAre()
    {
        var (scene, x0, z0, floor) = Inn();

        foreach (var z in new[] { z0 + 1, z0 + 2 })
        {
            Assert.Equal(BlockId.Counter, At(scene, x0 + 5, floor + 1, z)); // the counter, one block high but tall for a body (a table within reach would be a workbench, a crate a step)
            Assert.Equal(BlockId.Air, At(scene, x0 + 5, floor + 2, z));
        }

        foreach (var x in new[] { 1, 2, 3, 5, 6, 7 })
        {
            Assert.Equal(BlockId.Wall, At(scene, x0 + x, floor + 1, z0 + 3)); // the partition
            Assert.Equal(BlockId.Wall, At(scene, x0 + x, floor + 2, z0 + 3));
        }

        Assert.Equal(BlockId.Air, At(scene, x0 + 4, floor + 1, z0 + 3)); // the gap, on the door's axis
        Assert.Equal(BlockId.Air, At(scene, x0 + 4, floor + 2, z0 + 3));
        Assert.Equal(BlockId.Wall, At(scene, x0 + 4, floor + 3, z0 + 3)); // with a lintel
        // Y08: two beds, each two cells long along row 5, the head cell against the wall: cols 1 and 2 on the west, cols 7 and 6 on the east.
        Assert.Equal(BlockId.Bed, At(scene, x0 + 1, floor + 1, z0 + 5));
        Assert.Equal(BlockId.BedFoot, At(scene, x0 + 2, floor + 1, z0 + 5));
        Assert.Equal(BlockId.Bed, At(scene, x0 + 7, floor + 1, z0 + 5));
        Assert.Equal(BlockId.BedFoot, At(scene, x0 + 6, floor + 1, z0 + 5));
        Assert.Equal(2, scene.Grid.BlocksOfKind(BlockId.Bed).Count);
        Assert.Equal(2, scene.Grid.BlocksOfKind(BlockId.BedFoot).Count);
        Assert.True(scene.Grid.IsSolid(x0, floor + 1, z0 + 5)); // each head lies against the wall beside it
        Assert.True(scene.Grid.IsSolid(x0 + 8, floor + 1, z0 + 5));
        Assert.Equal(new BlockPos(x0 + 3, floor, z0 + 5), scene.Point("bed.inn")); // the stand cell at the foot of the west bed, which covers cols 1 and 2
        Assert.Equal("cama", scene.LabelOf("bed.inn"));
        Assert.Equal(BlockId.Air, At(scene, x0 + 3, floor + 1, z0 + 5));
        Assert.Equal(BlockId.Air, At(scene, x0 + 3, floor + 2, z0 + 5));
        Assert.Equal(BlockId.Air, At(scene, x0 + 4, floor + 1, z0 + 5)); // the gap's line stays clear
        Assert.Equal(BlockId.Air, At(scene, x0 + 4, floor + 1, z0 + 4));

        foreach (var (c, r) in new[] { (2, 0), (6, 0), (0, 2), (8, 2), (0, 4), (8, 4), (3, 6), (5, 6) })
        {
            Assert.Equal(BlockId.Air, At(scene, x0 + c, floor + 2, z0 + r)); // the eight windows at eye height
        }
    }

    [Fact]
    public void Given_TheKeepersCell_When_ItsSurroundingsAreRead_Then_ItIsEnclosedByTheCounterTheWallAndThePartition()
    {
        var (scene, x0, z0, floor) = Inn();
        var keeper = scene.Point("npc.innkeeper");
        Assert.Equal(new BlockPos(x0 + 6, floor, z0 + 1), keeper);

        // The cells round the 2 x 2 keeper's room (cols 6 and 7, rows 1 and 2) are solid at the feet's height: the counter, the east wall, the partition, the north wall.
        var ring = new List<(int C, int R)> { (5, 1), (5, 2), (8, 1), (8, 2), (6, 0), (7, 0), (6, 3), (7, 3) };
        foreach (var (c, r) in ring)
        {
            Assert.True(scene.Grid.IsSolid(x0 + c, floor + 1, z0 + r), $"({c}, {r}) is open beside the keeper's room");
        }

        foreach (var (c, r) in new[] { (6, 1), (7, 1), (6, 2), (7, 2) })
        {
            Assert.False(scene.Grid.IsSolid(x0 + c, floor + 1, z0 + r), $"({c}, {r}) in the keeper's room is not free");
            Assert.False(scene.Grid.IsSolid(x0 + c, floor + 2, z0 + r));
        }
    }

    [Fact]
    public void Given_TheKeepersCell_When_AnNpcIsPlacedThere_Then_ItIsAValidStandingCellOnTheFloor()
    {
        var (scene, _, _, floor) = Inn();
        var keeper = scene.Point("npc.innkeeper");
        var feet = new BlockPos(keeper.X, floor + 1, keeper.Z);

        Assert.True(VoxelWalker.IsStandable(scene.Grid, feet));
        Assert.True(scene.Grid.IsSolid(feet.X, floor, feet.Z)); // the inn's planks under the feet
        Assert.False(scene.Grid.IsSolid(feet.X, feet.Y, feet.Z));
        Assert.False(scene.Grid.IsSolid(feet.X, feet.Y + 1, feet.Z));
    }

    [Fact]
    public void Given_ThePlayerAtTheTalkCell_When_TheLineToTheKeeperIsTraced_Then_ItIsClearFromThereAndBlockedFromTheStreetAndTheBedAndTheGap()
    {
        var (scene, x0, z0, floor) = Inn();
        var keeper = scene.Point("npc.innkeeper");
        var body = NpcAim.BodyOf(new BlockPos(keeper.X, floor + 1, keeper.Z));
        var eyeAbove = Vocaterra.Game.Core.Physics.VoxelPhysics.EyeHeight / (double)Vocaterra.Game.Core.Physics.VoxelPhysics.UnitsPerBlock; // the camera: the feet plus the eye height

        bool Blocked(double ex, double ez, double feetY)
        {
            var eye = ((float)ex, (float)(feetY + eyeAbove), (float)ez);
            var distance = NpcAim.Distance((eye.Item1, eye.Item2, eye.Item3), body);
            return LineOfSight.Blocked(scene.Grid, eye, ((float)body.X, (float)body.Y, (float)body.Z), distance);
        }

        // The talk cell: one block south of the door's cell on the door's axis, in front of the counter's south end; the keeper is two blocks east.
        var talk = (X: x0 + 4, Z: z0 + 1);
        Assert.False(Blocked(talk.X + 0.5, talk.Z + 0.5, floor + 1), "the counter is one block high: the keeper is seen over it");
        Assert.InRange(NpcAim.Distance((talk.X + 0.5, floor + 1 + eyeAbove, talk.Z + 0.5), body), 1.5, NpcAim.ReachBlocks);
        Assert.True(Blocked(x0 + 4 + 0.5, z0 - 3 + 0.5, floor + 1), "from the street the north wall is in the way");
        Assert.True(Blocked(x0 + 2 + 0.5, z0 + 4 + 0.5, floor + 1), "from the guest room, west of the gap, the partition is in the way");
        Assert.True(Blocked(x0 + 4 + 0.5, z0 + 4 + 0.5, floor + 1), "from the guest room's side of the gap the partition is in the way");
    }

    [Fact]
    public void Given_ABodyInTheStreet_When_ItWalksToTheBedsStandCell_Then_ItGoesThroughTheDoorAndTheGap()
    {
        var (scene, x0, z0, floor) = Inn();
        var street = new BlockPos(x0 + 4, floor + 1, z0 - 4);
        var stand = scene.Point("bed.inn");
        var standFeet = new BlockPos(stand.X, floor + 1, stand.Z);

        var path = VoxelWalker.FindPath(scene.Grid, street, cell => cell == standFeet);

        Assert.NotNull(path);
        Assert.Contains(path, cell => cell.X == x0 + 4 && cell.Z == z0); // the door
        Assert.Contains(path, cell => cell.X == x0 + 4 && cell.Z == z0 + 3); // the partition's gap
        Assert.InRange(path.Count, 8, 30);

        // The body does walk it, tick by tick, with the door closed behind no one.
        var session = TestGame.NewSession();
        session.Simulation.TeleportTo(street);
        Assert.True(VoxelWalker.WalkTo(session, cell => cell == standFeet));
        Assert.Equal(standFeet, session.Simulation.Player.FeetBlock);
    }

    [Fact]
    public void Given_TheVillage_When_TheHousesAreCounted_Then_ThreeAreLabelledCasaAndNoneOverlapsAnythingElse()
    {
        var layout = VoxelTestSupport.RouteDraft;
        var scene = VoxelTestSupport.BuildRoute(Seed);

        Assert.Equal(["house1", "house2", "house3"], layout.Props.Where(p => p.Type == "house" && scene.LabelOf(p.Name!) == "casa").Select(static p => p.Name));
        Assert.Equal("posada", scene.LabelOf("inn"));

        // house3's door is reached from the street: a path from the village's square to the cell in front of it.
        var house3 = scene.Point("house3");
        var village = scene.Point("village");
        var start = new BlockPos(village.X, village.Y + 1, village.Z);
        var door = layout.Props.Single(static p => p.Name == "house3");
        var (doorX, doorZ) = (door.X + 2, door.Z); // the door faces north: the middle of the north wall
        var front = new BlockPos(doorX, house3.Y + 1, doorZ - 1);
        Assert.NotNull(VoxelWalker.FindPath(scene.Grid, start, cell => cell == front));
        Assert.NotNull(VoxelWalker.FindPath(scene.Grid, front, cell => cell.X == doorX && cell.Z == doorZ + 1)); // and through the door, into the room
    }

    [Theory]
    [InlineData(42UL)]
    [InlineData(7UL)]
    [InlineData(1234UL)]
    [InlineData(2025UL)]
    public void Given_ASeed_When_TheSceneIsBuilt_Then_NoTrunkAndNoLeavesStandWithin20BlocksOfEitherMeadowsCentre(ulong seed)
    {
        var scene = VoxelTestSupport.BuildRoute(seed);
        foreach (var name in new[] { "meadow.north", "meadow.south" })
        {
            var centre = scene.Point(name);
            for (var z = centre.Z - 20; z <= centre.Z + 20; z++)
            {
                for (var x = centre.X - 20; x <= centre.X + 20; x++)
                {
                    if (((x - centre.X) * (x - centre.X)) + ((z - centre.Z) * (z - centre.Z)) > 20 * 20)
                    {
                        continue;
                    }

                    var ground = scene.Grid.HighestBlock(x, z);
                    for (var y = Math.Max(0, ground - 30); y <= ground; y++)
                    {
                        var block = scene.Grid.GetBlock(x, y, z);
                        Assert.False(BlockDefinition.IsTrunk(block) || BlockDefinition.IsLeaves(block), $"seed {seed}: {block} at ({x}, {y}, {z}) near {name}");
                    }
                }
            }
        }
    }

    [Fact]
    public void Given_TheMeadows_When_TheirSitesAreRead_Then_TheyLevelNothingAndAreClearedByTwoClearings()
    {
        var layout = VoxelTestSupport.RouteDraft;
        foreach (var (name, x, z) in new[] { ("meadow.north", 270, 180), ("meadow.south", 264, 258) })
        {
            var site = layout.Site(name)!;
            Assert.Equal((x, z, 0), (site.X, site.Z, site.Radius));
            var clearing = Assert.Single(layout.Trees.ClearingList, c => c.Site == name);
            Assert.Equal((22, 12, 0), (clearing.Radius, clearing.Fade, clearing.Keep));
        }
    }
}
