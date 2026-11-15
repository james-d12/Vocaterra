using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Streaming;

namespace Vocaterra.Game.Core.Tests.World.Streaming;

/// <summary>The residency policy of the tall world (B07): the surface band of the columns round the player, padded so every band chunk has all 26 neighbours, nearest first, and what the plan lets go.</summary>
public sealed class BandStreamerTests
{
    private static readonly WorldLimits Limits = WorldLimits.Default;

    // A made-up ground: bands of two to four layers that wander from column to column, the way a hilly land's do.
    private static ColumnBand Hills(int x, int z) => new(2 + (((x * 7) + (z * 3)) & 3), 5 + (((x + z) & 3) / 2));

    private static ColumnBand Flat(int x, int z) => new(3, 4);

    private static BandStreamer Streamer(int load = 8, int keep = 10, int near = 2, int underground = 4) => new(new BandStreamPolicy(load, keep, near, underground), Limits);

    [Fact]
    public void Given_APlayer_When_TheBandIsPlanned_Then_TheOwnColumnComesFirstAndTheColumnsRunNearestFirstWithAFixedTieBreak()
    {
        var plan = Streamer().Plan(new VoxelChunkCoord(100, 4, -50), underground: false, Hills);

        Assert.Equal(100, plan.Wanted[0].X);
        Assert.Equal(-50, plan.Wanted[0].Z);
        Assert.Equal(plan.Wanted.Count, plan.Wanted.Distinct().Count());
        var distances = plan.Columns.Select(c => ((long)(c.ChunkX - 100) * (c.ChunkX - 100)) + ((long)(c.ChunkZ + 50) * (c.ChunkZ + 50))).ToList();
        Assert.Equal(distances.Order().ToList(), distances);
        Assert.Equal(distances.Count, plan.Columns.Select(c => (c.ChunkX, c.ChunkZ)).Distinct().Count());
        Assert.All(plan.Wanted, c => Assert.True(Limits.Contains(c)));
        var again = Streamer().Plan(new VoxelChunkCoord(100, 4, -50), underground: false, Hills);
        Assert.Equal(plan.Wanted, again.Wanted);
    }

    [Fact]
    public void Given_APlan_When_TheBandChunksOfTheInnerColumnsAreChecked_Then_AllTwentySixNeighboursOfEachAreWanted()
    {
        var player = new VoxelChunkCoord(-300, 4, 700);
        var plan = Streamer(load: 8).Plan(player, underground: false, Hills);
        var wanted = plan.Wanted.ToHashSet();
        var checkedChunks = 0;

        foreach (var column in plan.Columns.Where(c => ((c.ChunkX - player.X) * (c.ChunkX - player.X)) + ((c.ChunkZ - player.Z) * (c.ChunkZ - player.Z)) <= 6 * 6))
        {
            var band = Hills(column.ChunkX, column.ChunkZ);
            for (var layer = band.MinLayer; layer <= band.MaxLayer; layer++)
            {
                for (var dz = -1; dz <= 1; dz++)
                {
                    for (var dy = -1; dy <= 1; dy++)
                    {
                        for (var dx = -1; dx <= 1; dx++)
                        {
                            Assert.Contains(new VoxelChunkCoord(column.ChunkX + dx, layer + dy, column.ChunkZ + dz), wanted);
                            checkedChunks++;
                        }
                    }
                }
            }
        }

        Assert.True(checkedChunks > 1000);
    }

    [Fact]
    public void Given_TheNearZone_When_ItIsPlanned_Then_EveryLayerUnderTheBandIsWantedThereAndOnlyTheBandBeyond()
    {
        var player = new VoxelChunkCoord(0, 4, 0);
        var plan = Streamer(load: 9, near: 2).Plan(player, underground: false, Flat);

        Assert.True(plan.TryGetLayers(0, 0, out var nearMin, out var nearMax));
        Assert.True(nearMin <= Limits.MinChunkY + 1); // the near zone reaches the world's floor
        Assert.True(plan.TryGetLayers(7, 0, out var farMin, out var farMax));
        Assert.Equal((2, 5), (farMin, farMax)); // the band 3 to 4, one layer of padding each way
        Assert.True(nearMax >= 4);
        Assert.True(plan.Wanted.Count(c => c is { X: 0, Z: 0 }) > plan.Wanted.Count(c => c is { X: 7, Z: 0 }));
    }

    [Fact]
    public void Given_ThePlayerUnderground_When_TheBandIsPlanned_Then_TheNearZoneIsWider()
    {
        var player = new VoxelChunkCoord(0, 2, 0);
        var streamer = Streamer(load: 9, near: 1, underground: 4);

        var above = streamer.Plan(player, underground: false, Flat);
        var below = streamer.Plan(player, underground: true, Flat);

        Assert.True(above.TryGetLayers(3, 0, out var aboveMin, out _));
        Assert.True(below.TryGetLayers(3, 0, out var belowMin, out _));
        Assert.True(aboveMin > Limits.MinChunkY + 1, "above ground a column at three columns holds only its band");
        Assert.True(belowMin <= Limits.MinChunkY + 1, "underground it holds everything down to the floor");
        Assert.True(below.Wanted.Count > above.Wanted.Count);
        Assert.True(below.Underground);
    }

    [Fact]
    public void Given_APlan_When_TheHeldChunksAreAsked_Then_EveryWantedChunkIsHeldAndTheFarOnesAreNot()
    {
        var player = new VoxelChunkCoord(50, 4, 50);
        var plan = Streamer(load: 6, keep: 8).Plan(player, underground: false, Hills);

        Assert.All(plan.Wanted, c => Assert.True(plan.Holds(c)));
        Assert.False(plan.Holds(new VoxelChunkCoord(50 + 30, 4, 50)));
        Assert.False(plan.Holds(new VoxelChunkCoord(50, 4 + 9, 50))); // above everything the column holds
        Assert.False(plan.Holds(new VoxelChunkCoord(50 + 5, Limits.MinChunkY, 50))); // the stone far below a column beyond the near zone
        Assert.True(plan.Holds(new VoxelChunkCoord(50 + 9, Hills(59, 50).MinLayer, 50)), "the slack column beyond the load radius is still held");
    }

    [Fact]
    public void Given_ResidentChunks_When_TheEvictableAreAsked_Then_TheyAreTheUnheldOnesInAFixedOrderWhateverTheInputOrder()
    {
        var player = new VoxelChunkCoord(0, 4, 0);
        var streamer = Streamer(load: 5, keep: 6);
        var plan = streamer.Plan(player, underground: false, Hills);
        var resident = streamer.Plan(new VoxelChunkCoord(9, 4, 0), underground: false, Hills).Wanted.ToList();
        var shuffled = new List<VoxelChunkCoord>(resident);
        shuffled.Reverse();

        var evictable = BandStreamer.Evictable(plan, resident);

        Assert.Equal(evictable, BandStreamer.Evictable(plan, shuffled));
        Assert.All(evictable, c => Assert.False(plan.Holds(c)));
        Assert.Equal(resident.Count - resident.Count(plan.Holds), evictable.Count);
        Assert.NotEmpty(evictable);
        Assert.Equal(evictable.OrderBy(static c => c.Y).ThenBy(static c => c.Z).ThenBy(static c => c.X).ToList(), evictable);
    }

    [Fact]
    public void Given_ThePlayerAtTheWorldsEdge_When_TheBandIsPlanned_Then_NothingOutsideTheLimitsIsWanted()
    {
        var edge = Limits.EndChunkX - 1;
        var plan = Streamer(load: 6).Plan(new VoxelChunkCoord(edge, 4, 0), underground: false, Flat);

        Assert.NotEmpty(plan.Wanted);
        Assert.All(plan.Wanted, c => Assert.True(Limits.Contains(c)));
        Assert.Contains(plan.Wanted, c => c.X == edge);
    }

    [Fact]
    public void Given_ColumnsWithNothingToHold_When_TheyAreInTheDisc_Then_TheyAreSkipped()
    {
        var plan = Streamer(load: 4).Plan(new VoxelChunkCoord(0, 4, 0), underground: false, static (x, z) => x == 0 && z == 0 ? new ColumnBand(3, 4) : ColumnBand.Empty);

        Assert.All(plan.Columns, c => Assert.True(Math.Abs(c.ChunkX) <= 1 && Math.Abs(c.ChunkZ) <= 1));
        Assert.Equal(new WantedColumn(0, 0, Limits.MinChunkY, 5), plan.Columns[0]); // the near zone reaches the floor
    }

    [Fact]
    public void Given_RadiiThatDoNotMakeSense_When_AStreamerIsMade_Then_ItIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new BandStreamer(new BandStreamPolicy(0, 4, 1, 2), Limits));
        Assert.Throws<ArgumentException>(() => new BandStreamer(new BandStreamPolicy(6, 4, 1, 2), Limits));
        Assert.Throws<ArgumentException>(() => new BandStreamer(new BandStreamPolicy(6, 8, 3, 2), Limits));
    }
}
