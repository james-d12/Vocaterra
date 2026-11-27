using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Voxel;
using static Vocaterra.Game.Core.Tests.Voxel.VoxelTestSupport;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>K03: the block ray against the boxes of the oak blocks: the face of the box entered, the hit point inside the cell, the way through an empty part, and <c>Adjacent</c> for a face inside the cell.</summary>
public sealed class ShapedBlockRayTests
{
    private const int One = Angles.One;
    private const int Sixteenth = Units / 16;
    private static readonly BlockPos Cell = new(10, 5, 10);

    // The flat scene with its ground taken away (the tests work in the air at y 5, in the open).
    private static VoxelGrid World(Action<VoxelGrid> build) =>
        FlatScene(g =>
        {
            for (var z = 0; z < 64; z++)
            {
                for (var x = 0; x < 64; x++)
                {
                    for (var y = 0; y <= GroundTop; y++)
                    {
                        g.SetBlock(x, y, z, BlockId.Air);
                    }
                }
            }

            build(g);
        }).Grid;

    private static VoxelGrid WorldWith(BlockId id, byte state, params (int X, int Y, int Z, BlockId Id)[] more) =>
        World(g =>
        {
            g.SetBlock(Cell.X, Cell.Y, Cell.Z, id, state);
            foreach (var (x, y, z, other) in more)
            {
                g.SetBlock(x, y, z, other);
            }
        });

    // A ray from a point in blocks (as thousandths) along a direction that is axis-aligned.
    private static BlockRayHit? Cast(VoxelGrid grid, double ox, double oy, double oz, int dx, int dy, int dz, int reach = 4) =>
        BlockRay.Cast(grid, (int)(ox * Units), (int)(oy * Units), (int)(oz * Units), dx * One, dy * One, dz * One, (long)reach * Units);

    private static byte Bottom => BlockState.Slab.Pack(SlabKind.Bottom);

    [Fact]
    public void ADownRayOntoABottomSlabHitsItsTopInsideTheCellAndAdjacentIsTheCellItself()
    {
        var hit = Cast(WorldWith(BlockId.OakSlab, Bottom), 10.5, 8, 10.5, 0, -1, 0);
        Assert.NotNull(hit);
        Assert.Equal(Cell, hit.Value.Block);
        Assert.Equal(BlockFace.Up, hit.Value.Face);
        Assert.Equal(Cell, hit.Value.Adjacent); // the face is half way up the cell: the click is on its open half
        Assert.Equal((2048, 2048, 2048), (hit.Value.HitX, hit.Value.HitY, hit.Value.HitZ));
        Assert.Equal((int)(2.5 * Units), hit.Value.DistanceUnits);
    }

    [Fact]
    public void ADownRayOntoATopSlabHitsItsTopOnTheBoundaryAndAdjacentIsTheCellAbove()
    {
        var hit = Cast(WorldWith(BlockId.OakSlab, BlockState.Slab.Pack(SlabKind.Top)), 10.25, 8, 10.75, 0, -1, 0);
        Assert.NotNull(hit);
        Assert.Equal(BlockFace.Up, hit.Value.Face);
        Assert.Equal(new BlockPos(10, 6, 10), hit.Value.Adjacent);
        Assert.Equal((1024, Units, 3072), (hit.Value.HitX, hit.Value.HitY, hit.Value.HitZ));
    }

    [Fact]
    public void AnUpRayHitsTheUndersideOfATopSlabAndTheBottomOfABottomSlab()
    {
        var top = Cast(WorldWith(BlockId.OakSlab, BlockState.Slab.Pack(SlabKind.Top)), 10.5, 2, 10.5, 0, 1, 0);
        Assert.Equal(BlockFace.Down, top!.Value.Face);
        Assert.Equal(Cell, top.Value.Adjacent); // the underside of a top slab is half way up the cell
        Assert.Equal(2048, top.Value.HitY);

        var bottom = Cast(WorldWith(BlockId.OakSlab, Bottom), 10.5, 2, 10.5, 0, 1, 0);
        Assert.Equal(BlockFace.Down, bottom!.Value.Face);
        Assert.Equal(new BlockPos(10, 4, 10), bottom.Value.Adjacent);
        Assert.Equal(0, bottom.Value.HitY);
    }

    [Fact]
    public void ALevelRayGoesThroughTheEmptyHalfOfASlabAndHitsTheBlockBehind()
    {
        var grid = WorldWith(BlockId.OakSlab, Bottom, (12, 5, 10, BlockId.Stone));
        var high = Cast(grid, 8.5, 5.75, 10.5, 1, 0, 0);
        Assert.Equal(new BlockPos(12, 5, 10), high!.Value.Block);
        Assert.Equal(BlockFace.West, high.Value.Face);

        var low = Cast(grid, 8.5, 5.25, 10.5, 1, 0, 0);
        Assert.Equal(Cell, low!.Value.Block);
        Assert.Equal(BlockFace.West, low.Value.Face);
        Assert.Equal(new BlockPos(9, 5, 10), low.Value.Adjacent); // the slab's side is on the cell's boundary
        Assert.Equal((0, 1024, 2048), (low.Value.HitX, low.Value.HitY, low.Value.HitZ));
    }

    [Fact]
    public void ASlopingRayThroughTheEmptyHalfHitsTheTopFaceFromTheSide()
    {
        // From (8.5, 6.5) going east and down at 2 to 1: it enters the cell's x range at y 6.0, above the slab's top at 5.5, and meets the top at x 10.5.
        var grid = WorldWith(BlockId.OakSlab, Bottom);
        var hit = BlockRay.Cast(grid, (int)(8.5 * Units), (int)(6.5 * Units), (int)(10.5 * Units), 58_617, -29_309, 0, 4L * Units);
        Assert.NotNull(hit);
        Assert.Equal(Cell, hit.Value.Block);
        Assert.Equal(BlockFace.Up, hit.Value.Face);
        Assert.Equal(Cell, hit.Value.Adjacent);
        Assert.InRange(hit.Value.HitX, 2048 - 8, 2048 + 8);
        Assert.Equal(2048, hit.Value.HitY);
    }

    [Fact]
    public void AStairsFaceInsideTheCellIsTheCellItselfAndItsTopIsTheBoundary()
    {
        var north = BlockState.Stairs.Pack(Facing.North, false); // the high step is the north half (z 0 to 8)
        var grid = WorldWith(BlockId.OakStairs, north);

        var low = Cast(grid, 10.5, 8, 10.75, 0, -1, 0); // over the south half: the low step's top at 0.5
        Assert.Equal(BlockFace.Up, low!.Value.Face);
        Assert.Equal(Cell, low.Value.Adjacent);
        Assert.Equal(2048, low.Value.HitY);

        var high = Cast(grid, 10.5, 8, 10.25, 0, -1, 0); // over the north half: the high step's top at 1.0
        Assert.Equal(BlockFace.Up, high!.Value.Face);
        Assert.Equal(new BlockPos(10, 6, 10), high.Value.Adjacent);
        Assert.Equal(Units, high.Value.HitY);

        var riser = Cast(grid, 10.5, 5.75, 12, 0, 0, -1); // level, from the south, above the low step: the high step's south face at z 8
        Assert.Equal(BlockFace.South, riser!.Value.Face);
        Assert.Equal(Cell, riser.Value.Adjacent);
        Assert.Equal(2048, riser.Value.HitZ);

        var low2 = Cast(grid, 10.5, 5.25, 12, 0, 0, -1); // level, from the south, at the low step's height: its south face on the boundary
        Assert.Equal(BlockFace.South, low2!.Value.Face);
        Assert.Equal(new BlockPos(10, 5, 11), low2.Value.Adjacent);
    }

    [Fact]
    public void AnUpsideDownStairsHighStepHangsFromTheTop()
    {
        var grid = WorldWith(BlockId.OakStairs, BlockState.Stairs.Pack(Facing.North, true));
        var open = Cast(grid, 10.5, 2, 10.75, 0, 1, 0); // from below, the south half has nothing under the upper step: the ray meets the underside at 0.5
        Assert.Equal(BlockFace.Down, open!.Value.Face);
        Assert.Equal(Cell, open.Value.Adjacent);
        Assert.Equal(2048, open.Value.HitY);
    }

    [Fact]
    public void AnOpenGateHasAGapAndPosts()
    {
        // A gate facing north runs along X: posts at x 0 to 2 and 14 to 16 sixteenths.
        var open = BlockState.Gate.Pack(Facing.North, true);
        var grid = WorldWith(BlockId.OakFenceGate, open, (10, 5, 13, BlockId.Stone));
        var gap = Cast(grid, 10.5, 5.5, 9.5, 0, 0, 1);
        Assert.Equal(new BlockPos(10, 5, 13), gap!.Value.Block); // through the open gate and on to the stone
        Assert.Equal(BlockFace.North, gap.Value.Face);

        var post = Cast(grid, 10.05, 5.5, 9.5, 0, 0, 1);
        Assert.Equal(Cell, post!.Value.Block);
        Assert.Equal(BlockFace.North, post.Value.Face);
        Assert.Equal(0, post.Value.HitZ); // the swung bar's end is on the cell's north edge
    }

    [Fact]
    public void AClosedGateIsHitOnItsBarsAndPostsAndNotBetweenThem()
    {
        var closed = BlockState.Gate.Pack(Facing.North, false);
        var grid = WorldWith(BlockId.OakFenceGate, closed, (10, 5, 13, BlockId.Stone));
        var rail = Cast(grid, 10.5, 5 + (7.5 / 16), 9.5, 0, 0, 1); // the lower rail, 6 to 9 sixteenths
        Assert.Equal(Cell, rail!.Value.Block);
        Assert.Equal(BlockFace.North, rail.Value.Face);
        Assert.Equal(7 * Sixteenth, rail.Value.HitZ);
        Assert.Equal(Cell, rail.Value.Adjacent); // the rail's face is inside the cell

        var between = Cast(grid, 10.5, 5 + (10.5 / 16), 9.5, 0, 0, 1); // between the rails, 9 to 12: the ray goes through to the stone
        Assert.Equal(new BlockPos(10, 5, 13), between!.Value.Block);
    }

    [Fact]
    public void AFencePostIsHitInTheMiddleAndMissedAtTheCorner()
    {
        var grid = WorldWith(BlockId.OakFence, 0, (10, 5, 13, BlockId.Stone));
        var post = Cast(grid, 10.5, 5.5, 9.5, 0, 0, 1);
        Assert.Equal(Cell, post!.Value.Block);
        Assert.Equal(BlockFace.North, post.Value.Face);
        Assert.Equal(6 * Sixteenth, post.Value.HitZ);
        Assert.Equal(Cell, post.Value.Adjacent);

        var corner = Cast(grid, 10.15, 5.5, 9.5, 0, 0, 1);
        Assert.Equal(new BlockPos(10, 5, 13), corner!.Value.Block);
    }

    [Fact]
    public void AClosedDoorPanelAndATrapdoorAreHitOnTheirFaces()
    {
        // A closed door facing north: the panel is the south 3 sixteenths (z 13 to 16): a ray from the north goes through the empty 13 sixteenths and meets it at z 13.
        var door = WorldWith(BlockId.OakDoor, BlockState.Door.Pack(Facing.North, false, false, false));
        var fromNorth = Cast(door, 10.5, 5.5, 9.5, 0, 0, 1);
        Assert.Equal(Cell, fromNorth!.Value.Block);
        Assert.Equal(BlockFace.North, fromNorth.Value.Face);
        Assert.Equal(13 * Sixteenth, fromNorth.Value.HitZ);
        Assert.Equal(Cell, fromNorth.Value.Adjacent);
        var fromSouth = Cast(door, 10.5, 5.5, 12.5, 0, 0, -1);
        Assert.Equal(BlockFace.South, fromSouth!.Value.Face);
        Assert.Equal(new BlockPos(10, 5, 11), fromSouth.Value.Adjacent);

        // A closed trapdoor at the bottom: a down ray meets it at 3 sixteenths; a level ray passes over it.
        var trapdoor = WorldWith(BlockId.OakTrapdoor, BlockState.Trapdoor.Pack(Facing.North, false, false), (12, 5, 10, BlockId.Stone));
        var down = Cast(trapdoor, 10.5, 8, 10.5, 0, -1, 0);
        Assert.Equal(BlockFace.Up, down!.Value.Face);
        Assert.Equal(3 * Sixteenth, down.Value.HitY);
        Assert.Equal(Cell, down.Value.Adjacent);
        var over = Cast(trapdoor, 8.5, 5.5, 10.5, 1, 0, 0);
        Assert.Equal(new BlockPos(12, 5, 10), over!.Value.Block);
    }

    [Fact]
    public void AFullCubeKeepsItsOwnFaceAndReportsTheHitPoint()
    {
        var log = WorldWith(BlockId.OakLog, BlockState.Log.Pack(LogAxis.Y));
        var hit = Cast(log, 8.5, 5.25, 10.75, 1, 0, 0);
        Assert.Equal(Cell, hit!.Value.Block);
        Assert.Equal(BlockFace.West, hit.Value.Face);
        Assert.Equal(new BlockPos(9, 5, 10), hit.Value.Adjacent);
        Assert.Equal((0, 1024, 3072), (hit.Value.HitX, hit.Value.HitY, hit.Value.HitZ));

        var stone = World(g => g.SetBlock(10, 5, 10, BlockId.Stone));
        var top = Cast(stone, 10.25, 8, 10.5, 0, -1, 0);
        Assert.Equal((1024, Units, 2048), (top!.Value.HitX, top.Value.HitY, top.Value.HitZ));
        Assert.Equal(new BlockPos(10, 6, 10), top.Value.Adjacent);
    }

    [Fact]
    public void EveryFaceOfASlabIsReported()
    {
        var grid = WorldWith(BlockId.OakSlab, Bottom);
        (double Ox, double Oy, double Oz, int Dx, int Dy, int Dz, BlockFace Face)[] cases =
        [
            (8.5, 5.25, 10.5, 1, 0, 0, BlockFace.West),
            (12.5, 5.25, 10.5, -1, 0, 0, BlockFace.East),
            (10.5, 5.25, 8.5, 0, 0, 1, BlockFace.North),
            (10.5, 5.25, 12.5, 0, 0, -1, BlockFace.South),
            (10.5, 8, 10.5, 0, -1, 0, BlockFace.Up),
            (10.5, 2, 10.5, 0, 1, 0, BlockFace.Down),
        ];
        foreach (var (ox, oy, oz, dx, dy, dz, face) in cases)
        {
            var hit = Cast(grid, ox, oy, oz, dx, dy, dz);
            Assert.True(hit is not null && hit.Value.Block == Cell && hit.Value.Face == face, $"{face}: {hit}");
        }
    }

    [Fact]
    public void ABoxBeyondTheReachIsNotHit()
    {
        var grid = WorldWith(BlockId.OakSlab, Bottom);
        Assert.Null(Cast(grid, 10.5, 12, 10.5, 0, -1, 0)); // the top is 6.5 blocks away
        Assert.NotNull(Cast(grid, 10.5, 9.4, 10.5, 0, -1, 0)); // 3.9 blocks
        Assert.Null(Cast(grid, 10.5, 9.6, 10.5, 0, -1, 0)); // 4.1 blocks: the cell is entered within the reach but its top is not
    }
}
