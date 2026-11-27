using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>The shapes of the oak blocks (K02): boxes in range with volume, no overlaps, every facing a turn of the north one, the fence's arms, the covers and no allocation.</summary>
public sealed class BlockShapesTests
{
    private static readonly BlockId[] Oak =
        [BlockId.OakLog, BlockId.OakStairs, BlockId.OakSlab, BlockId.OakFence, BlockId.OakFenceGate, BlockId.OakDoor, BlockId.OakTrapdoor];

    private static readonly BlockId[] Faced = [BlockId.OakStairs, BlockId.OakFenceGate, BlockId.OakDoor, BlockId.OakTrapdoor];

    private static ShapeBox[] Boxes(ReadOnlySpan<ShapeBox> span) => span.ToArray();

    private static ShapeBox[] Sorted(IEnumerable<ShapeBox> boxes) => [.. boxes.OrderBy(b => (b.MinX, b.MinY, b.MinZ, b.MaxX, b.MaxY, b.MaxZ))];

    public static TheoryData<BlockId, byte> EveryState()
    {
        var data = new TheoryData<BlockId, byte>();
        foreach (var id in Oak)
        {
            foreach (var state in BlockState.ValidStates(id))
            {
                data.Add(id, state);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryState))]
    public void Given_AnyValidOakState_When_Shaped_Then_TheBoxesAreInRangeWithVolumeAndDoNotOverlap(BlockId id, byte state)
    {
        var shape = BlockShapes.Of(id, state);

        Assert.True(shape.Drawn.Length > 0, "drawn boxes");
        Check(Boxes(shape.Drawn), 16);
        Check(Boxes(shape.Collides), 24);
        if (id == BlockId.OakFenceGate && BlockState.Gate.Open(state))
        {
            Assert.True(shape.Collides.IsEmpty);
        }
        else
        {
            Assert.True(shape.Collides.Length > 0, "a closed oak block collides");
        }
    }

    private static void Check(ShapeBox[] boxes, int maxY)
    {
        for (var i = 0; i < boxes.Length; i++)
        {
            var b = boxes[i];
            Assert.InRange(b.MinX, 0, 16);
            Assert.InRange(b.MaxX, 0, 16);
            Assert.InRange(b.MinZ, 0, 16);
            Assert.InRange(b.MaxZ, 0, 16);
            Assert.InRange(b.MinY, 0, maxY);
            Assert.InRange(b.MaxY, 0, maxY);
            Assert.True(b.Volume > 0, $"{b} has volume");
            for (var j = i + 1; j < boxes.Length; j++)
            {
                Assert.False(b.Overlaps(boxes[j]), $"{b} overlaps {boxes[j]}");
            }
        }
    }

    [Fact]
    public void Given_EveryFacedBlock_When_Turned_Then_EachFacingIsAQuarterTurnOfTheNorthOne()
    {
        foreach (var id in Faced)
        {
            foreach (var state in BlockState.ValidStates(id))
            {
                if ((state & 3) != 0)
                {
                    continue; // the north ones
                }

                var north = BlockShapes.Of(id, state);
                for (var turns = 1; turns < 4; turns++)
                {
                    var turned = BlockShapes.Of(id, (byte)(state | turns));
                    Assert.Equal(Sorted(Boxes(north.Drawn).Select(b => b.Turned(turns))), Sorted(Boxes(turned.Drawn)));
                    Assert.Equal(Sorted(Boxes(north.Collides).Select(b => b.Turned(turns))), Sorted(Boxes(turned.Collides)));
                }
            }
        }
    }

    [Fact]
    public void Given_ATurn_When_FourQuarterTurnsAreApplied_Then_TheBoxIsBackAndNorthGoesToEast()
    {
        var box = new ShapeBox(3, 2, 0, 9, 7, 5);

        Assert.Equal(box, box.Turned(4));
        Assert.Equal(new ShapeBox(11, 2, 3, 16, 7, 9), box.Turned(1));
    }

    [Fact]
    public void Given_Slabs_When_Shaped_Then_BottomTopAndDoubleAreTheHalvesAndTheCube()
    {
        Assert.Equal(new[] { new ShapeBox(0, 0, 0, 16, 8, 16) }, Boxes(BlockShapes.Of(BlockId.OakSlab, BlockState.Slab.Pack(SlabKind.Bottom)).Drawn));
        Assert.Equal(new[] { new ShapeBox(0, 8, 0, 16, 16, 16) }, Boxes(BlockShapes.Of(BlockId.OakSlab, BlockState.Slab.Pack(SlabKind.Top)).Collides));
        Assert.Same(VoxelShape.Cube, BlockShapes.Of(BlockId.OakSlab, BlockState.Slab.Pack(SlabKind.Double)));
    }

    [Fact]
    public void Given_Stairs_When_Shaped_Then_TheHighStepLiesTowardsTheFacingAndUpsideDownMirrors()
    {
        var north = BlockShapes.Of(BlockId.OakStairs, BlockState.Stairs.Pack(Facing.North, false));
        Assert.Equal(Sorted([new ShapeBox(0, 0, 0, 16, 8, 16), new ShapeBox(0, 8, 0, 16, 16, 8)]), Sorted(Boxes(north.Drawn)));

        var east = BlockShapes.Of(BlockId.OakStairs, BlockState.Stairs.Pack(Facing.East, false));
        Assert.Contains(new ShapeBox(8, 8, 0, 16, 16, 16), Boxes(east.Drawn));

        var upside = BlockShapes.Of(BlockId.OakStairs, BlockState.Stairs.Pack(Facing.North, true));
        Assert.Equal(Sorted([new ShapeBox(0, 8, 0, 16, 16, 16), new ShapeBox(0, 0, 0, 16, 8, 8)]), Sorted(Boxes(upside.Drawn)));
    }

    [Fact]
    public void Given_Doors_When_Shaped_Then_TheClosedPanelIsOnThePlayersSideAndTheOpenOneOnTheHingeSide()
    {
        var closedNorth = BlockShapes.Of(BlockId.OakDoor, BlockState.Door.Pack(Facing.North, false, false, false));
        Assert.Equal(new[] { new ShapeBox(0, 0, 13, 16, 16, 16) }, Boxes(closedNorth.Collides));

        var closedEast = BlockShapes.Of(BlockId.OakDoor, BlockState.Door.Pack(Facing.East, false, false, true));
        Assert.Equal(new[] { new ShapeBox(0, 0, 0, 3, 16, 16) }, Boxes(closedEast.Drawn));

        var rightNorth = BlockShapes.Of(BlockId.OakDoor, BlockState.Door.Pack(Facing.North, true, true, false));
        Assert.Equal(new[] { new ShapeBox(13, 0, 0, 16, 16, 16) }, Boxes(rightNorth.Drawn)); // to the right of north is east

        var leftNorth = BlockShapes.Of(BlockId.OakDoor, BlockState.Door.Pack(Facing.North, true, false, false));
        Assert.Equal(new[] { new ShapeBox(0, 0, 0, 3, 16, 16) }, Boxes(leftNorth.Drawn));
    }

    [Fact]
    public void Given_Trapdoors_When_Shaped_Then_ClosedLiesAtTheBottomOrTopAndOpenAgainstTheSideOppositeTheFacing()
    {
        Assert.Equal(new[] { new ShapeBox(0, 0, 0, 16, 3, 16) }, Boxes(BlockShapes.Of(BlockId.OakTrapdoor, BlockState.Trapdoor.Pack(Facing.West, false, false)).Drawn));
        Assert.Equal(new[] { new ShapeBox(0, 13, 0, 16, 16, 16) }, Boxes(BlockShapes.Of(BlockId.OakTrapdoor, BlockState.Trapdoor.Pack(Facing.South, true, false)).Collides));
        Assert.Equal(new[] { new ShapeBox(0, 0, 13, 16, 16, 16) }, Boxes(BlockShapes.Of(BlockId.OakTrapdoor, BlockState.Trapdoor.Pack(Facing.North, false, true)).Drawn));
        Assert.Equal(new[] { new ShapeBox(0, 0, 0, 16, 16, 3) }, Boxes(BlockShapes.Of(BlockId.OakTrapdoor, BlockState.Trapdoor.Pack(Facing.South, true, true)).Collides));
    }

    [Fact]
    public void Given_Gates_When_Shaped_Then_ClosedBlocksAlongItsLineAndOpenSwingsToTheFacing()
    {
        var closedNorth = BlockShapes.Of(BlockId.OakFenceGate, BlockState.Gate.Pack(Facing.North, false));
        Assert.Equal(new[] { new ShapeBox(0, 0, 7, 16, 24, 9) }, Boxes(closedNorth.Collides));
        var closedEast = BlockShapes.Of(BlockId.OakFenceGate, BlockState.Gate.Pack(Facing.East, false));
        Assert.Equal(new[] { new ShapeBox(7, 0, 0, 9, 24, 16) }, Boxes(closedEast.Collides));

        var open = BlockShapes.Of(BlockId.OakFenceGate, BlockState.Gate.Pack(Facing.North, true));
        Assert.True(open.Collides.IsEmpty);
        Assert.Equal(6, open.Drawn.Length);
        Assert.Contains(open.Drawn.ToArray(), b => b.MinZ == 0 && b.MaxZ == 6); // bars swung towards -Z
        Assert.DoesNotContain(open.Drawn.ToArray(), b => b.MaxZ > 10);
    }

    [Fact]
    public void Given_AllSixteenNeighbourCombinations_When_AFenceIsShaped_Then_ItHasThePostAndTheRightArms()
    {
        for (var arms = 0; arms < 16; arms++)
        {
            var grid = new VoxelGrid(16, 16, 16);
            grid.SetBlock(8, 4, 8, BlockId.OakFence);
            var count = 0;
            for (var d = 0; d < 4; d++)
            {
                if ((arms & (1 << d)) != 0)
                {
                    var (dx, dz) = ((Facing)d).Step();
                    grid.SetBlock(8 + dx, 4, 8 + dz, d % 2 == 0 ? BlockId.OakFence : BlockId.Stone);
                    count++;
                }
            }

            Assert.Equal(arms, BlockShapes.FenceArms(grid, 8, 4, 8));
            var shape = BlockShapes.Of(BlockId.OakFence, 0, grid, 8, 4, 8);
            Assert.Same(BlockShapes.Fence(arms), shape);
            Assert.Equal(1 + (2 * count), shape.Drawn.Length);
            Assert.Equal(1 + count, shape.Collides.Length);
            Assert.Contains(new ShapeBox(6, 0, 6, 10, 16, 10), Boxes(shape.Drawn));
            Assert.Contains(new ShapeBox(6, 0, 6, 10, 24, 10), Boxes(shape.Collides));
            Check(Boxes(shape.Drawn), 16);
            Check(Boxes(shape.Collides), 24);
        }

        Assert.Contains(new ShapeBox(6, 0, 0, 10, 24, 6), Boxes(BlockShapes.Fence(1).Collides));
        Assert.Contains(new ShapeBox(10, 6, 7, 16, 9, 9), Boxes(BlockShapes.Fence(2).Drawn));
    }

    [Fact]
    public void Given_FenceNeighbours_When_Read_Then_OnlyFencesGatesOnTheLineAndFullOpaqueCubesJoin()
    {
        var grid = new VoxelGrid(16, 16, 16);
        grid.SetBlock(8, 4, 8, BlockId.OakFence);
        grid.SetBlock(8, 4, 7, BlockId.OakFenceGate, BlockState.Gate.Pack(Facing.North, false)); // north: its line runs along X, across the arm
        grid.SetBlock(9, 4, 8, BlockId.OakFenceGate, BlockState.Gate.Pack(Facing.North, true)); // east: along X, in line with the arm
        grid.SetBlock(8, 4, 9, BlockId.OakSlab, BlockState.Slab.Pack(SlabKind.Bottom)); // a half block: no arm
        grid.SetBlock(7, 4, 8, BlockId.OakLog); // a full cube

        Assert.Equal(2 | 8, BlockShapes.FenceArms(grid, 8, 4, 8));

        grid.SetBlock(8, 4, 9, BlockId.OakSlab, BlockState.Slab.Pack(SlabKind.Double));
        grid.SetBlock(8, 4, 7, BlockId.Leaves);
        Assert.Equal(2 | 4 | 8, BlockShapes.FenceArms(grid, 8, 4, 8));

        grid.SetBlock(8, 4, 7, BlockId.Fence); // the scene's fence does not join
        Assert.Equal(2 | 4 | 8, BlockShapes.FenceArms(grid, 8, 4, 8));
    }

    [Fact]
    public void Given_BlocksWithoutAShapeOfTheirOwn_When_Shaped_Then_SolidIsTheCubeAndTheRestIsEmpty()
    {
        Assert.Same(VoxelShape.Cube, BlockShapes.Of(BlockId.Stone, 0));
        Assert.Same(VoxelShape.Cube, BlockShapes.Of(BlockId.Fence, 0));
        Assert.Same(VoxelShape.Cube, BlockShapes.Of(BlockId.Planks, 7));
        Assert.Same(VoxelShape.Cube, BlockShapes.Of(BlockId.OakLog, 2));
        Assert.Same(VoxelShape.Empty, BlockShapes.Of(BlockId.Air, 0));
        Assert.Same(VoxelShape.Empty, BlockShapes.Of(BlockId.Door, 0));
        Assert.Same(VoxelShape.Empty, BlockShapes.Of(BlockId.Water, 0));
        Assert.Equal([ShapeBox.Full], Boxes(VoxelShape.Cube.Drawn));
        foreach (var definition in BlockDefinition.All)
        {
            if (!Oak.Contains(definition.Id))
            {
                Assert.Same(definition.Solid ? VoxelShape.Cube : VoxelShape.Empty, BlockShapes.Of(definition.Id, 0));
            }
        }
    }

    [Fact]
    public void Given_AnInvalidState_When_Shaped_Then_ItReadsAsStateZero()
    {
        Assert.Same(BlockShapes.Of(BlockId.OakSlab, 0), BlockShapes.Of(BlockId.OakSlab, 3));
        Assert.Same(BlockShapes.Of(BlockId.OakDoor, 0), BlockShapes.Of(BlockId.OakDoor, 200));
        Assert.Same(BlockShapes.Of(BlockId.OakStairs, 0), BlockShapes.Of(BlockId.OakStairs, 31));
    }

    [Fact]
    public void Given_ABottomSlab_When_Covered_Then_DownIsFullUpIsNoneAndTheSidesArePartial()
    {
        var bottom = BlockState.Slab.Pack(SlabKind.Bottom);

        Assert.Equal(FaceCover.Full, BlockShapes.CoverOf(BlockId.OakSlab, bottom, BlockFace.Down));
        Assert.Equal(FaceCover.None, BlockShapes.CoverOf(BlockId.OakSlab, bottom, BlockFace.Up));
        foreach (var side in new[] { BlockFace.North, BlockFace.East, BlockFace.South, BlockFace.West })
        {
            Assert.Equal(FaceCover.Partial, BlockShapes.CoverOf(BlockId.OakSlab, bottom, side));
        }

        var top = BlockState.Slab.Pack(SlabKind.Top);
        Assert.Equal(FaceCover.Full, BlockShapes.CoverOf(BlockId.OakSlab, top, BlockFace.Up));
        Assert.Equal(FaceCover.None, BlockShapes.CoverOf(BlockId.OakSlab, top, BlockFace.Down));
    }

    [Fact]
    public void Given_Stairs_When_Covered_Then_DownAndTheBackSideAreFull()
    {
        var north = BlockState.Stairs.Pack(Facing.North, false);

        Assert.Equal(FaceCover.Full, BlockShapes.CoverOf(BlockId.OakStairs, north, BlockFace.Down));
        Assert.Equal(FaceCover.Full, BlockShapes.CoverOf(BlockId.OakStairs, north, BlockFace.North));
        Assert.Equal(FaceCover.Partial, BlockShapes.CoverOf(BlockId.OakStairs, north, BlockFace.Up));
        Assert.Equal(FaceCover.Partial, BlockShapes.CoverOf(BlockId.OakStairs, north, BlockFace.South));
        Assert.Equal(FaceCover.Partial, BlockShapes.CoverOf(BlockId.OakStairs, north, BlockFace.East));

        var upside = BlockState.Stairs.Pack(Facing.East, true);
        Assert.Equal(FaceCover.Full, BlockShapes.CoverOf(BlockId.OakStairs, upside, BlockFace.Up));
        Assert.Equal(FaceCover.Full, BlockShapes.CoverOf(BlockId.OakStairs, upside, BlockFace.East));
        Assert.Equal(FaceCover.Partial, BlockShapes.CoverOf(BlockId.OakStairs, upside, BlockFace.Down));
    }

    [Fact]
    public void Given_TheFullCubes_When_Covered_Then_AllSixFacesAreFullAndAnEmptyCellCoversNothing()
    {
        foreach (var face in Enum.GetValues<BlockFace>())
        {
            Assert.Equal(FaceCover.Full, BlockShapes.CoverOf(BlockId.OakSlab, BlockState.Slab.Pack(SlabKind.Double), face));
            Assert.Equal(FaceCover.Full, BlockShapes.CoverOf(BlockId.OakLog, 1, face));
            Assert.Equal(FaceCover.Full, BlockShapes.CoverOf(BlockId.Stone, 0, face));
            Assert.Equal(FaceCover.None, BlockShapes.CoverOf(BlockId.Air, 0, face));
        }
    }

    [Fact]
    public void Given_ThinPieces_When_Covered_Then_NoneIsFull()
    {
        foreach (var id in new[] { BlockId.OakFence, BlockId.OakFenceGate, BlockId.OakDoor, BlockId.OakTrapdoor })
        {
            foreach (var state in BlockState.ValidStates(id))
            {
                foreach (var face in Enum.GetValues<BlockFace>())
                {
                    var cover = BlockShapes.CoverOf(id, state, face);
                    if (id == BlockId.OakDoor || id == BlockId.OakTrapdoor)
                    {
                        continue; // a panel covers its own side fully, checked below
                    }

                    Assert.NotEqual(FaceCover.Full, cover);
                }
            }
        }

        // A closed door facing north (panel on the south side) covers the south face fully; a bottom trapdoor covers Down fully.
        Assert.Equal(FaceCover.Full, BlockShapes.CoverOf(BlockId.OakDoor, BlockState.Door.Pack(Facing.North, false, false, false), BlockFace.South));
        Assert.Equal(FaceCover.Full, BlockShapes.CoverOf(BlockId.OakTrapdoor, BlockState.Trapdoor.Pack(Facing.North, false, false), BlockFace.Down));
        Assert.Equal(FaceCover.None, BlockShapes.CoverOf(BlockId.OakTrapdoor, BlockState.Trapdoor.Pack(Facing.North, false, false), BlockFace.Up));
    }

    [Fact]
    public void Given_ManyCalls_When_ShapesAreRead_Then_NothingIsAllocated()
    {
        var grid = new VoxelGrid(16, 16, 16);
        grid.SetBlock(8, 4, 8, BlockId.OakFence);
        grid.SetBlock(9, 4, 8, BlockId.OakFence);
        var total = 0;

        // Warm up: the first call may JIT.
        total += Walk(grid);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 2000; i++)
        {
            total += Walk(grid);
        }

        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.True(total > 0);
        Assert.Equal(0, after - before);
    }

    private static int Walk(VoxelGrid grid)
    {
        var count = 0;
        for (var id = 0; id < 74; id++)
        {
            for (var state = 0; state < 33; state++)
            {
                var shape = BlockShapes.Of((BlockId)id, (byte)state);
                count += shape.Drawn.Length + shape.Collides.Length + (int)shape.CoverOf(BlockFace.Up);
            }
        }

        for (var arms = 0; arms < 16; arms++)
        {
            count += BlockShapes.Fence(arms).Drawn.Length;
        }

        count += BlockShapes.Of(BlockId.OakFence, 0, grid, 8, 4, 8).Collides.Length;
        return count;
    }
}
