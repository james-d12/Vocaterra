using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Tests.Voxel;

/// <summary>The state layouts of the oak blocks (K02): every pack and unpack round-trips and every invalid value is refused.</summary>
public sealed class BlockStateTests
{
    private static readonly BlockId[] Oak =
        [BlockId.OakLog, BlockId.OakStairs, BlockId.OakSlab, BlockId.OakFence, BlockId.OakFenceGate, BlockId.OakDoor, BlockId.OakTrapdoor];

    [Fact]
    public void Given_TheOakBlocks_When_Listed_Then_TheyAreAppendedAfterBasaltWithTheirRows()
    {
        Assert.Equal(67, (int)BlockId.OakLog);
        Assert.Equal(73, (int)BlockId.OakTrapdoor);
        Assert.Equal(Oak.Length, Oak.Distinct().Count());
        foreach (var id in Oak)
        {
            var definition = BlockDefinition.Get(id);
            Assert.True(definition.Solid);
            Assert.False(definition.Tall);
            Assert.False(definition.Gatherable);
            Assert.Equal(Material.Wood, definition.Material);
            Assert.Equal(id != BlockId.OakLog, definition.Transparent);
        }
    }

    [Fact]
    public void Given_AnyAxis_When_PackedAndUnpacked_Then_ItRoundTrips()
    {
        foreach (var axis in Enum.GetValues<LogAxis>())
        {
            Assert.Equal(axis, BlockState.Log.Axis(BlockState.Log.Pack(axis)));
            Assert.True(BlockState.IsValid(BlockId.OakLog, BlockState.Log.Pack(axis)));
        }
    }

    [Fact]
    public void Given_AnyStairs_When_PackedAndUnpacked_Then_ItRoundTrips()
    {
        foreach (var facing in Enum.GetValues<Facing>())
        {
            foreach (var upside in new[] { false, true })
            {
                var state = BlockState.Stairs.Pack(facing, upside);
                Assert.Equal(facing, BlockState.Stairs.FacingOf(state));
                Assert.Equal(upside, BlockState.Stairs.UpsideDown(state));
                Assert.True(BlockState.IsValid(BlockId.OakStairs, state));
            }
        }
    }

    [Fact]
    public void Given_AnySlab_When_PackedAndUnpacked_Then_ItRoundTrips()
    {
        foreach (var kind in Enum.GetValues<SlabKind>())
        {
            Assert.Equal(kind, BlockState.Slab.Kind(BlockState.Slab.Pack(kind)));
            Assert.True(BlockState.IsValid(BlockId.OakSlab, BlockState.Slab.Pack(kind)));
        }
    }

    [Fact]
    public void Given_AnyGate_When_PackedAndUnpacked_Then_ItRoundTrips()
    {
        foreach (var facing in Enum.GetValues<Facing>())
        {
            foreach (var open in new[] { false, true })
            {
                var state = BlockState.Gate.Pack(facing, open);
                Assert.Equal(facing, BlockState.Gate.FacingOf(state));
                Assert.Equal(open, BlockState.Gate.Open(state));
                Assert.True(BlockState.IsValid(BlockId.OakFenceGate, state));
            }
        }
    }

    [Fact]
    public void Given_AnyDoor_When_PackedAndUnpacked_Then_ItRoundTrips()
    {
        var seen = new HashSet<byte>();
        foreach (var facing in Enum.GetValues<Facing>())
        {
            foreach (var open in new[] { false, true })
            {
                foreach (var hinge in new[] { false, true })
                {
                    foreach (var upper in new[] { false, true })
                    {
                        var state = BlockState.Door.Pack(facing, open, hinge, upper);
                        Assert.Equal(facing, BlockState.Door.FacingOf(state));
                        Assert.Equal(open, BlockState.Door.Open(state));
                        Assert.Equal(hinge, BlockState.Door.HingeRight(state));
                        Assert.Equal(upper, BlockState.Door.Upper(state));
                        Assert.True(BlockState.IsValid(BlockId.OakDoor, state));
                        Assert.True(seen.Add(state), "two doors share a state");
                    }
                }
            }
        }

        Assert.Equal(32, seen.Count);
    }

    [Fact]
    public void Given_AnyTrapdoor_When_PackedAndUnpacked_Then_ItRoundTrips()
    {
        var seen = new HashSet<byte>();
        foreach (var facing in Enum.GetValues<Facing>())
        {
            foreach (var top in new[] { false, true })
            {
                foreach (var open in new[] { false, true })
                {
                    var state = BlockState.Trapdoor.Pack(facing, top, open);
                    Assert.Equal(facing, BlockState.Trapdoor.FacingOf(state));
                    Assert.Equal(top, BlockState.Trapdoor.Top(state));
                    Assert.Equal(open, BlockState.Trapdoor.Open(state));
                    Assert.True(BlockState.IsValid(BlockId.OakTrapdoor, state));
                    Assert.True(seen.Add(state));
                }
            }
        }

        Assert.Equal(16, seen.Count);
    }

    [Fact]
    public void Given_EveryByteForEveryOakBlock_When_Validated_Then_OnlyTheDefinedStatesPass()
    {
        // log and slab: 0 to 2; stairs and gate: 0 to 7; trapdoor: 0 to 15; door: 0 to 31; fence: 0 only.
        var valid = new Dictionary<BlockId, int>
        {
            [BlockId.OakLog] = 3,
            [BlockId.OakSlab] = 3,
            [BlockId.OakStairs] = 8,
            [BlockId.OakFenceGate] = 8,
            [BlockId.OakTrapdoor] = 16,
            [BlockId.OakDoor] = 32,
            [BlockId.OakFence] = 1,
        };
        foreach (var (id, count) in valid)
        {
            for (var state = 0; state < 256; state++)
            {
                Assert.Equal(state < count, BlockState.IsValid(id, (byte)state));
            }

            Assert.Equal(count, BlockState.ValidStates(id).Count);
        }
    }

    [Fact]
    public void Given_ASlabOrLogReadingThree_When_Validated_Then_ItIsInvalid()
    {
        Assert.False(BlockState.IsValid(BlockId.OakSlab, 3));
        Assert.False(BlockState.IsValid(BlockId.OakLog, 3));
        Assert.False(BlockState.IsValid(BlockId.OakStairs, 8));
        Assert.False(BlockState.IsValid(BlockId.OakDoor, 32));
    }

    [Fact]
    public void Given_ABlockWithNoState_When_Validated_Then_OnlyZeroIsValid()
    {
        Assert.True(BlockState.IsValid(BlockId.Planks, 0));
        Assert.False(BlockState.IsValid(BlockId.Planks, 1));
        Assert.True(BlockState.IsValid(BlockId.Air, 0));
        Assert.False(BlockState.IsValid(BlockId.Stone, 255));
    }

    [Fact]
    public void Given_TheFacings_When_Turned_Then_TheyRunClockwise()
    {
        Assert.Equal(Facing.East, Facing.North.Right());
        Assert.Equal(Facing.North, Facing.West.Right());
        Assert.Equal(Facing.West, Facing.North.Left());
        Assert.Equal(Facing.South, Facing.North.Opposite());
        Assert.Equal(Facing.South, Facing.East.Turn(5));
        Assert.Equal(BlockFace.East, Facing.East.ToFace());
        Assert.Equal(Facing.West, FacingExtensions.FromFace(BlockFace.West));
        Assert.Null(FacingExtensions.FromFace(BlockFace.Up));
        foreach (var facing in Enum.GetValues<Facing>())
        {
            var (dx, dz) = facing.Step();
            var normal = facing.ToFace().Normal();
            Assert.Equal((normal.Dx, normal.Dz), (dx, dz));
        }
    }
}
