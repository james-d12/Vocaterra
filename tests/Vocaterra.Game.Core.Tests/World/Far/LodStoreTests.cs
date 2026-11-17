using System.Collections.Concurrent;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Far;
using Vocaterra.Game.Core.World.Lod;
using Vocaterra.Game.Core.World.Regions;
using Vocaterra.Game.Core.World.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Far;

/// <summary>The far view's store of three-dimensional sections (B07c): the packed format, the mip that makes a level from the one below, the light, the store's rules and its disk, the model that fills it and the real chunks that replace the model.</summary>
public sealed class LodStoreTests(ITestOutputHelper output)
{
    private sealed class MemoryDisk : ILodPersistence
    {
        public ConcurrentDictionary<LodSectionKey, byte[]> Files { get; } = new();

        public byte[]? Load(LodSectionKey key) => Files.TryGetValue(key, out var blob) ? blob : null;

        public int SaveDelayMilliseconds { get; init; }

        public void Save(LodSectionKey key, byte[] blob)
        {
            if (SaveDelayMilliseconds > 0)
            {
                Thread.Sleep(SaveDelayMilliseconds); // a slow disk keeps a store step open longer, so the threads of a test overlap
            }

            Files[key] = blob;
        }
    }

    private static LodWorld WorldOf(ulong seed, LodStore? store = null, int treeLevels = 2)
    {
        var sampler = new FarTerrainSampler(ProceduralTestWorld.Source(seed).World, RegionCatalog.Empty);
        return sampler.FarWorld(store ?? new LodStore(), treeLevels);
    }

    private static LodSection Mixed(Func<int, int, int, BlockId> block)
    {
        var section = LodSection.Air();
        for (var y = 0; y < 32; y++)
        {
            for (var z = 0; z < 32; z++)
            {
                for (var x = 0; x < 32; x++)
                {
                    section.SetBlock(LodSection.Index(x, y, z), block(x, y, z));
                }
            }
        }

        return section;
    }

    [Fact]
    public void Given_UniformAndMixedSections_When_TheyArePackedAndUnpacked_Then_TheyAreTheSameAndSmall()
    {
        var uniform = new LodSection(BlockId.Stone, 0);
        var mixed = Mixed((x, y, z) => y < 12 + (x % 3) ? BlockId.Stone : y == 12 + (x % 3) ? BlockId.Grass : x == 5 && z == 5 && y < 20 ? BlockId.Trunk : BlockId.Air);
        mixed.SetLight(LodSection.Index(3, 3, 3), 0xAB);

        var back = LodSection.Unpack(uniform.Pack());
        var again = LodSection.Unpack(mixed.Pack());

        Assert.True(back.IsUniform);
        Assert.Equal(BlockId.Stone, back.FillBlock);
        Assert.Equal(4, uniform.Pack().Length);
        for (var i = 0; i < LodSection.Volume; i++)
        {
            Assert.Equal(mixed.BlockAt(i), again.BlockAt(i));
            Assert.Equal(mixed.LightAt(i), again.LightAt(i));
        }

        output.WriteLine($"a mixed section packs to {mixed.Pack().Length} bytes of {LodSection.Volume * 3}");
        Assert.True(mixed.Pack().Length < LodSection.Volume / 2);
    }

    [Fact]
    public void Given_AWallOneBlockThick_When_ItIsMipped_Then_TheShellIsNotHollowedAndTheCrownCarryingTrunkSurvives()
    {
        // A wall one block thick in every direction fills four of the eight voxels it passes through; a trunk of one block stays; a lone block goes.
        var blocks = new BlockId[8];
        var lights = new byte[8];
        for (var k = 0; k < 4; k++)
        {
            blocks[k] = BlockId.Planks; // the lower layer
        }

        Assert.Equal(BlockId.Planks, LodMip.Voxel(blocks, lights, out _));
        var vertical = new BlockId[8];
        vertical[0] = vertical[1] = vertical[2] = vertical[3] = BlockId.Planks;
        vertical[1] = BlockId.Air;
        vertical[4] = BlockId.Planks;
        vertical[5] = BlockId.Planks;
        Assert.Equal(BlockId.Planks, LodMip.Voxel(vertical, lights, out _)); // five of eight
        var trunk = new BlockId[8];
        trunk[2] = BlockId.Trunk;
        Assert.Equal(BlockId.Trunk, LodMip.Voxel(trunk, lights, out _));
        var lone = new BlockId[8];
        lone[6] = BlockId.Stone;
        Assert.Equal(BlockId.Air, LodMip.Voxel(lone, lights, out _));
        var crown = new BlockId[8];
        crown[0] = crown[5] = BlockId.Leaves;
        Assert.Equal(BlockId.Leaves, LodMip.Voxel(crown, lights, out _));
        var shore = new BlockId[8];
        shore[0] = shore[1] = shore[2] = BlockId.Water;
        Assert.Equal(BlockId.Water, LodMip.Voxel(shore, lights, out _));
    }

    [Fact]
    public void Given_ATrunkWithACrownOver_When_ThreeLevelsAreMipped_Then_TheTrunkAndTheCrownBothSurviveEachLevel()
    {
        var ground = 8;
        var children = new LodSection[8];
        for (var c = 0; c < 8; c++)
        {
            children[c] = LodSection.Air();
        }

        // An oak: a trunk at (4, 9..13, 4), a crown over it, in the lower-x lower-z child column of the lower child.
        children[0] = Mixed((x, y, z) => y <= ground ? BlockId.Stone : x == 4 && z == 4 && y <= 13 ? BlockId.Trunk : y is >= 12 and <= 16 && Math.Abs(x - 4) <= 2 && Math.Abs(z - 4) <= 2 ? BlockId.Leaves : BlockId.Air);
        var level1 = LodMip.Parent(children);
        var upper = new LodSection[8];
        for (var c = 0; c < 8; c++)
        {
            upper[c] = c == 0 ? level1 : LodSection.Air();
        }

        var level2 = LodMip.Parent(upper);

        var blocks1 = Enumerable.Range(0, LodSection.Volume).Select(level1.BlockAt).ToList();
        var blocks2 = Enumerable.Range(0, LodSection.Volume).Select(level2.BlockAt).ToList();
        Assert.Contains(BlockId.Trunk, blocks1);
        Assert.Contains(BlockId.Leaves, blocks1);
        Assert.Contains(BlockId.Leaves, blocks2);
        Assert.Contains(BlockId.Trunk, blocks2);
    }

    [Fact]
    public void Given_AColumnWithACrownAndWater_When_ItIsLit_Then_TheSkyIsDimmedUnderLeavesAndWaterAndNotInTheOpen()
    {
        var section = Mixed((x, y, z) => y < 10 ? BlockId.Stone : x == 3 && z == 3 && y is >= 14 and <= 17 ? BlockId.Leaves : x == 8 && z == 8 && y is >= 10 and <= 12 ? BlockId.Water : BlockId.Air);
        var sky = new byte[1024];
        Array.Fill(sky, (byte)15);

        LodMip.Light(section, sky, new byte[1024]);

        Assert.Equal(15, section.LightAt(LodSection.Index(20, 12, 20)) >> 4); // the open
        Assert.Equal(15 - 4, section.LightAt(LodSection.Index(3, 12, 3)) >> 4); // under four leaves
        Assert.Equal(15 - 1, section.LightAt(LodSection.Index(8, 11, 8)) >> 4); // one water over it
        var lamp = Mixed((x, y, z) => x == 1 && y == 1 && z == 1 ? BlockId.Lava : BlockId.Stone);
        LodMip.Light(lamp, sky, new byte[1024]);
        Assert.Equal(15, lamp.LightAt(LodSection.Index(1, 1, 1)) & 0xF);
    }

    [Fact]
    public void Given_TheStore_When_SectionsAreKeptAndReplaced_Then_ARealSectionIsNeverReplacedByTheModelAndEveryChangeIsTold()
    {
        var disk = new MemoryDisk();
        var store = new LodStore(disk);
        var key = new LodSectionKey(0, 3, 1, -2);
        var model = new LodSection(BlockId.Stone, 0);
        var real = new LodSection(BlockId.Dirt, 0);

        Assert.True(store.Put(key, model, LodOrigin.Model));
        Assert.False(store.Put(key, new LodSection(BlockId.Sand, 0), LodOrigin.Model));
        Assert.True(store.Put(key, real, LodOrigin.Real, 0xFF));
        Assert.False(store.Put(key, model, LodOrigin.Model));
        Assert.False(store.Put(key, new LodSection(BlockId.Sand, 0), LodOrigin.Real, 0x0F)); // a part does not replace a whole

        Assert.Equal(BlockId.Dirt, store.Get(key)!.FillBlock);
        Assert.Single(store.Changed); // only the real section: a model one fills a place nothing was drawn from

        // A new store over the same disk finds it again: a restart is instant.
        var restarted = new LodStore(disk);
        Assert.Equal(BlockId.Dirt, restarted.Get(key)!.FillBlock);
        Assert.Equal(LodOrigin.Real, restarted.Entry(key)!.Origin);
        Assert.Null(restarted.Get(new LodSectionKey(0, 9, 9, 9)));
    }

    [Fact]
    public void Given_APlaceTheModelKnows_When_ItsStackIsAsked_Then_TheWholeStackIsMadeKeptAndSurfaceSectionsHoldTheGroundAndTrees()
    {
        var world = WorldOf(42);
        var (cx, cz) = ProceduralTestWorld.Find(42, Feature.Forest);
        var stack = world.Stack(0, cx >> 1, cz >> 1);
        var (min, max) = world.SyRange(0);

        Assert.Equal(min, stack.SyMin);
        Assert.Equal(max - min + 1, stack.Sections.Length);
        Assert.All(Enumerable.Range(min, max - min + 1), sy => Assert.NotNull(world.Store.Entry(new LodSectionKey(0, cx >> 1, sy, cz >> 1))));
        Assert.True(stack.Sections[0].IsUniform && stack.Sections[0].FillBlock == BlockId.Stone, "the world's floor is stone");
        Assert.True(stack.Sections[^1].IsEmpty, "the top of the world is air");
        var surface = stack.Sections.Where(static s => !s.IsUniform).ToList();
        Assert.NotEmpty(surface);
        var blocks = surface.SelectMany(static s => Enumerable.Range(0, LodSection.Volume).Select(s.BlockAt)).ToHashSet();
        Assert.Contains(BlockId.Grass, blocks);
        Assert.Contains(BlockId.Leaves, blocks);
        Assert.Contains(BlockId.Trunk, blocks);
        Assert.Equal(stack.Sections.Length, world.Stack(0, cx >> 1, cz >> 1).Sections.Length);
        output.WriteLine($"{world.Store.Count} sections, {world.Store.Bytes / 1024} KB packed for one stack");
    }

    [Fact]
    public void Given_EveryLevel_When_AStackIsMade_Then_TheLevelsAgreeOnWhereTheGroundIs()
    {
        var world = WorldOf(7);
        var x = 3000;
        var z = 1200;
        var heights = new List<int>();
        foreach (var level in new[] { 0, 1, 2, 3, 4, 5 })
        {
            var cell = 1 << level;
            var stack = world.Stack(level, x / (32 * cell), z / (32 * cell));
            var (ix, iz) = ((x / cell) % 32, (z / cell) % 32);
            var top = int.MinValue;
            for (var s = 0; s < stack.Sections.Length; s++)
            {
                for (var vy = 0; vy < 32; vy++)
                {
                    if (LodBlocks.IsOpaque(stack.Sections[s].BlockAt(LodSection.Index(ix, vy, iz))))
                    {
                        top = (((stack.SyMin + s) * 32) + vy + 1) * cell;
                    }
                }
            }

            heights.Add(top);
        }

        output.WriteLine("the ground at one column by level: " + string.Join(", ", heights));
        var ground = heights[0];
        Assert.All(heights, h => Assert.InRange(h, ground - 48, ground + 48));
    }

    [Fact]
    public void Given_RealChunksOfTwoByTwoStacks_When_TheyAreIngested_Then_ARealMipAppearsAboveAndReplacesTheModelThroughTheStore()
    {
        var world = WorldOf(42, treeLevels: 1);
        var store = world.Store;
        var (min, max) = world.SyRange(0);
        // The model's stacks first (the far view made its tiles from them), then the real ones over them.
        for (var dz = 0; dz < 2; dz++)
        {
            for (var dx = 0; dx < 2; dx++)
            {
                _ = world.Stack(0, 10 + dx, 10 + dz);
            }
        }

        store.Changed.Clear();
        var stone = new ChunkData(new VoxelChunkCoord(0, 0, 0), Enumerable.Repeat(BlockId.Stone, ChunkData.Volume).ToArray(), null);
        for (var dz = 0; dz < 2; dz++)
        {
            for (var dx = 0; dx < 2; dx++)
            {
                world.IngestStack(10 + dx, 10 + dz, (cx, cy, cz) => cy < 4 ? stone : null);
            }
        }

        var real = store.Entry(new LodSectionKey(0, 10, 1, 10));
        var parent = store.Entry(new LodSectionKey(1, 5, 0, 5));
        Assert.True(real is { IsWhollyReal: true });
        Assert.Equal(BlockId.Stone, store.Get(new LodSectionKey(0, 10, 1, 10))!.BlockAt(LodSection.Index(7, 7, 7)));
        Assert.NotNull(parent);
        Assert.Equal(LodOrigin.RealMip, parent.Origin);
        Assert.Contains(new LodSectionKey(1, 5, 0, 5), store.Changed);
        Assert.Equal(max - min + 1, world.Stack(0, 10, 10).Sections.Length);
        Assert.True(world.Stack(0, 10, 10).Sections[(1 - min)].IsUniform);
    }

    [Fact]
    public void Given_AnEditedChunk_When_ItIsIngested_Then_ItsBlocksAreInTheStoreAndTheStackIsLitAgain()
    {
        var world = WorldOf(42, treeLevels: 1);
        var coord = new VoxelChunkCoord(40, 4, 40); // a chunk over the ground
        var blocks = new BlockId[ChunkData.Volume];
        blocks[(((3 << 4) | 5) << 4) | 7] = BlockId.Planks; // a block of a house
        blocks[(((9 << 4) | 5) << 4) | 7] = BlockId.Lantern;

        world.IngestChunk(coord, blocks);

        var key = new LodSectionKey(0, 20, 2, 20);
        var section = world.Store.Get(key)!;
        var (bx, by, bz) = ((coord.X & 1) * 16, (coord.Y & 1) * 16, (coord.Z & 1) * 16);
        Assert.Equal(BlockId.Planks, section.BlockAt(LodSection.Index(bx + 7, by + 3, bz + 5)));
        Assert.Equal(15, section.LightAt(LodSection.Index(bx + 7, by + 9, bz + 5)) & 0xF);
        var entry = world.Store.Entry(key)!;
        Assert.Equal(LodOrigin.Real, entry.Origin);
        Assert.Equal(1 << 0, entry.RealMask & (1 << 0));
        Assert.NotEqual(0xFF, entry.RealMask);
    }

    // A sample of the model's sea: a block at least 40 blocks from any land, found on a grid (the water's own level is the sea's).
    private static (int X, int Z, int Level) OpenSea(FarTerrainSampler sampler)
    {
        for (var z = -9000; z <= 9000; z += 397)
        {
            for (var x = -9000; x <= 9000; x += 401)
            {
                var all = true;
                for (var k = 0; k < 9 && all; k++)
                {
                    all = sampler.At(x + (((k % 3) - 1) * 40), z + (((k / 3) - 1) * 40)).IsWater;
                }

                if (all)
                {
                    return (x, z, sampler.At(x, z).WaterLevel);
                }
            }
        }

        throw new InvalidOperationException("no open sea");
    }

    [Fact]
    public void Given_AnOpenSea_When_EveryLevelOfItsStackIsMade_Then_TheWaterTopIsAWaterVoxelWithNoHoleToTheSky()
    {
        var sampler = new FarTerrainSampler(ProceduralTestWorld.Source(42).World, RegionCatalog.Empty);
        var (x, z, sea) = OpenSea(sampler);
        var world = sampler.FarWorld(new LodStore(), 2);
        for (var level = 0; level <= 7; level++)
        {
            var cell = 1 << level;
            var stack = world.Stack(level, Math.DivRem(x, 32 * cell, out var rx) is var sx && rx < 0 ? sx - 1 : sx, Math.DivRem(z, 32 * cell, out var rz) is var sz && rz < 0 ? sz - 1 : sz);
            var (ix, iz) = ((((x / cell) % 32) + 32) % 32, (((z / cell) % 32) + 32) % 32);
            var topWater = int.MinValue;
            for (var i = 0; i < stack.Sections.Length; i++)
            {
                for (var vy = 0; vy < 32; vy++)
                {
                    if (stack.Sections[i].BlockAt(LodSection.Index(ix, vy, iz)) == BlockId.Water)
                    {
                        topWater = (((stack.SyMin + i) * 32) + vy + 1) * cell;
                    }
                }
            }

            output.WriteLine($"level {level}: the water's top at {topWater} for a sea at {sea}");
            Assert.True(topWater != int.MinValue, $"no water at level {level}");
            Assert.InRange(topWater, sea + 1 - cell, sea + 1 + cell);
        }
    }

    [Fact]
    public void Given_WaterOverStoneInTheUpperChildren_When_ItIsMipped_Then_TheVoxelIsWaterAndTheExposedBlockOfASlopeWins()
    {
        BlockId[] blocks = [BlockId.Stone, BlockId.Stone, BlockId.Stone, BlockId.Stone, BlockId.Water, BlockId.Water, BlockId.Air, BlockId.Air];
        var lights = new byte[8];
        Assert.Equal(BlockId.Water, LodMip.Voxel(blocks, lights, out _));

        // Grass on top of a slope over dirt: the grass is what is seen.
        BlockId[] slope = [BlockId.Dirt, BlockId.Dirt, BlockId.Dirt, BlockId.Dirt, BlockId.Grass, BlockId.Air, BlockId.Air, BlockId.Air];
        Assert.Equal(BlockId.Grass, LodMip.Voxel(slope, lights, out _));

        // Snow over stone, a peak's tip with one child over four of stone.
        BlockId[] tip = [BlockId.Stone, BlockId.Stone, BlockId.Stone, BlockId.Stone, BlockId.Snow, BlockId.Air, BlockId.Air, BlockId.Air];
        Assert.Equal(BlockId.Snow, LodMip.Voxel(tip, lights, out _));
    }

    [Fact]
    public void Given_ACone30BlocksHighOverAHill_When_ItIsMippedFourLevels_Then_ItsTipStillStandsAbove16Blocks()
    {
        var section = Mixed((x, y, z) => y < 8 ? BlockId.Stone : (((x - 24) * (x - 24)) + ((z - 24) * (z - 24))) * 4 <= (30 - y) * (30 - y) && y < 30 ? BlockId.Stone : BlockId.Air);
        var level = section;
        for (var l = 0; l < 4; l++)
        {
            var children = new LodSection[8];
            Array.Fill(children, new LodSection(BlockId.Air, 0));
            children[0] = level;
            level = LodMip.Parent(children);
        }

        // The cone's centre (24, 24) is the voxel (1, 1) of the level-4 section: its voxel over the first 16 blocks is solid, as the cone is there.
        var top = -1;
        for (var y = 0; y < 32; y++)
        {
            if (level.BlockAt(LodSection.Index(1, y, 1)) != BlockId.Air)
            {
                top = y;
            }
        }

        Assert.True(top >= 1, $"the peak is {top}");
    }

    [Fact]
    public void Given_AModelPutRacingARealOne_When_ManyThreadsPutBoth_Then_TheRealSectionAlwaysRemainsAndIsOnDisk()
    {
        var disk = new MemoryDisk();
        var store = new LodStore(disk);
        var key = new LodSectionKey(0, 1, 1, 1);
        var real = new LodSection(BlockId.Stone, 0);
        var model = new LodSection(BlockId.Dirt, 0);
        for (var round = 0; round < 200; round++)
        {
            var k = key with { X = round };
            Parallel.Invoke(
                () => store.Put(k, model, LodOrigin.Model),
                () => store.Put(k, real, LodOrigin.RealMip));
            Assert.Equal(LodOrigin.RealMip, store.Entry(k)!.Origin);
            Assert.Equal(BlockId.Stone, store.Get(k)!.FillBlock);
            Assert.Equal(LodOrigin.RealMip, (LodOrigin)disk.Files[k][0]);
        }
    }

    [Fact]
    public void Given_AnEditedChunkIngestedBeforeTheGeneratorsStack_When_TheStackIsIngested_Then_TheEditSurvives()
    {
        var world = WorldOf(42, treeLevels: 1);
        var coord = new VoxelChunkCoord(40, 4, 40);
        var blocks = new BlockId[ChunkData.Volume];
        blocks[(((3 << 4) | 5) << 4) | 7] = BlockId.Planks;
        world.IngestChunk(coord, blocks);

        var stone = new ChunkData(new VoxelChunkCoord(0, 0, 0), Enumerable.Repeat(BlockId.Stone, ChunkData.Volume).ToArray(), null);

        world.IngestStack(20, 20, (cx, cy, cz) => cy < 6 ? stone : null);

        var section = world.Store.Get(new LodSectionKey(0, 20, 2, 20))!;
        var (bx, by, bz) = ((coord.X & 1) * 16, (coord.Y & 1) * 16, (coord.Z & 1) * 16);
        Assert.Equal(BlockId.Planks, section.BlockAt(LodSection.Index(bx + 7, by + 3, bz + 5)));
        Assert.Equal(BlockId.Air, section.BlockAt(LodSection.Index(bx + 8, by + 3, bz + 5))); // the edited chunk is whole, as the edit left it
        Assert.Equal(BlockId.Stone, section.BlockAt(LodSection.Index(bx + 16, by + 3, bz + 5))); // the generator's chunk beside it
        Assert.Equal(0xFF, world.Store.Entry(new LodSectionKey(0, 20, 2, 20))!.RealMask);
    }

    [Fact]
    public void Given_RealStacksOfTwoByTwoByTwoParents_When_TheyAreIngested_Then_TheLevelsUpToTheStoresMaxAreRealIncludingAtTheTopAndBottomOfTheWorld()
    {
        var store = new LodStore(maxMipLevel: 4);
        var world = WorldOf(42, store, treeLevels: 1);
        var stone = new ChunkData(new VoxelChunkCoord(0, 0, 0), Enumerable.Repeat(BlockId.Stone, ChunkData.Volume).ToArray(), null);

        for (var dz = 0; dz < 16; dz++)
        {
            for (var dx = 0; dx < 16; dx++)
            {
                world.IngestStack(dx, dz, (cx, cy, cz) => cy < 4 ? stone : null);
            }
        }

        foreach (var level in new[] { 1, 2, 3, 4 })
        {
            var (min, max) = world.SyRange(level);
            for (var sy = min; sy <= max; sy++)
            {
                var entry = store.Entry(new LodSectionKey(level, 0, sy, 0));
                Assert.True(entry is { Origin: LodOrigin.RealMip }, $"level {level} sy {sy}: {entry?.Origin}");
            }
        }
    }

    [Fact]
    public void Given_TornBytesOnDisk_When_TheSectionIsAsked_Then_ItReadsAsMissingAndDoesNotThrow()
    {
        var disk = new MemoryDisk();
        var key = new LodSectionKey(0, 1, 2, 3);
        var good = new LodSection(BlockId.Stone, 0).Pack();
        disk.Files[key] = [(byte)LodOrigin.Real, 0xFF, 0, 1, 7, 7]; // a deflated section cut short
        var store = new LodStore(disk);
        Assert.Null(store.Get(key));
        Assert.Null(store.Entry(key));
        disk.Files[key] = [(byte)LodOrigin.Real, 0xFF, 0, .. good, 9]; // a uniform section with a byte too many
        Assert.Null(new LodStore(disk).Get(key));
        Assert.Throws<InvalidDataException>(() => LodSection.Unpack([1, 2, 3]));
    }

    [Fact]
    public void Given_ModelSectionsPastTheCap_When_MoreArePut_Then_TheLeastRecentlyUsedAreDroppedAndRealOnesStay()
    {
        var store = new LodStore(maxModelBytes: 4 * 100);
        var real = new LodSectionKey(0, 0, 0, 0);
        _ = store.Put(real, new LodSection(BlockId.Stone, 0), LodOrigin.Real, 0xFF);
        for (var i = 1; i <= 200; i++)
        {
            _ = store.Put(new LodSectionKey(0, i, 0, 0), new LodSection(BlockId.Dirt, 0), LodOrigin.Model);
        }

        Assert.True(store.Count < 150, $"{store.Count} sections");
        Assert.NotNull(store.Entry(real));
    }

    // The generator's stack as a ring ingest makes it (stone under chunk row 4, a column of planks in each footprint) and an edit's chunk (a house block in the second section up).
    private static ChunkData? GeneratorChunk(int cx, int cy, int cz) => cy < 4 ? new ChunkData(new VoxelChunkCoord(cx, cy, cz), Enumerable.Repeat(BlockId.Timber, ChunkData.Volume).ToArray(), null) : null; // a block the model never makes, so a section the model made is told from one the generator did

    private static BlockId[] HouseChunk()
    {
        var blocks = new BlockId[ChunkData.Volume];
        blocks[(((3 << 4) | 5) << 4) | 7] = BlockId.Planks;
        return blocks;
    }

    private static void AssertSameBlocks(LodSection expected, LodSection actual, string where)
    {
        for (var i = 0; i < LodSection.Volume; i++)
        {
            if (expected.BlockAt(i) != actual.BlockAt(i))
            {
                Assert.Fail($"{where}: voxel {i} is {actual.BlockAt(i)}, not {expected.BlockAt(i)}");
            }
        }
    }

    [Fact]
    public void Given_TheRingsStackAndAnEditsChunkOfTheSameFootprint_When_TheyAreIngestedOnTwoThreadsManyTimes_Then_TheRealBlocksAndTheEditBothSurviveInMemoryAndOnDisk()
    {
        // B07f, C1: the edit's relight used to write back the stack it had read before the ring's real sections arrived, over them.
        var disk = new MemoryDisk { SaveDelayMilliseconds = 1 };
        var world = WorldOf(42, new LodStore(disk), treeLevels: 1);
        var (min, max) = world.SyRange(0);
        var edit = HouseChunk();
        var reference = WorldOf(42, new LodStore(), treeLevels: 1);
        reference.IngestStack(0, 0, GeneratorChunk);
        reference.IngestChunk(new VoxelChunkCoord(0, 4, 0), edit);

        for (var round = 0; round < 90; round++)
        {
            var (sx, sz) = (round, 7);
            var coord = new VoxelChunkCoord(2 * sx, 4, 2 * sz); // a chunk of the second section from the ground (cy 4 is section 2)
            using var go = new ManualResetEventSlim();
            Exception? failure = null;
            Thread Start(Action work) => new(() =>
            {
                try
                {
                    go.Wait();
                    work();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            var ring = Start(() =>
            {
                Thread.Sleep(round); // the ring's ingest begins a millisecond later each round, so it overlaps every point of the edit's relight
                world.IngestStack(sx, sz, (cx, cy, cz) => GeneratorChunk(cx - (2 * sx), cy, cz - (2 * sz)));
            });
            var player = Start(() => world.IngestChunk(coord, edit));
            ring.Start();
            player.Start();
            go.Set();
            ring.Join();
            player.Join();
            Assert.Null(failure);

            for (var sy = min; sy <= max; sy++)
            {
                var key = new LodSectionKey(0, sx, sy, sz);
                var entry = world.Store.Entry(key)!;
                Assert.Equal(LodOrigin.Real, entry.Origin);
                Assert.Equal(0xFF, entry.RealMask);
                var here = world.Store.Get(key)!;
                AssertSameBlocks(reference.Store.Get(new LodSectionKey(0, 0, sy, 0))!, here, $"round {round} section {sy}");
                var saved = disk.Files[key];
                Assert.Equal(LodOrigin.Real, (LodOrigin)saved[0]);
                AssertSameBlocks(here, LodSection.Unpack(saved[3..]), $"round {round} section {sy} on disk");
            }

            Assert.Equal(1 << 0, world.Store.Entry(new LodSectionKey(0, sx, 2, sz))!.EditedMask & 1);
        }
    }

    [Fact]
    public void Given_TwoThreadsEditingChunksOfTheSameStackWhileTheRingIngestsIt_When_TheyAreDone_Then_BothEditsAreInAndTheStackIsStillTheGenerators()
    {
        var world = WorldOf(42, new LodStore(new MemoryDisk()), treeLevels: 1);
        var edit = HouseChunk();
        for (var round = 0; round < 40; round++)
        {
            var sx = round;
            Parallel.Invoke(
                () => world.IngestStack(sx, 3, (cx, cy, cz) => GeneratorChunk(cx - (2 * sx), cy, cz - 6)),
                () => world.IngestChunk(new VoxelChunkCoord(2 * sx, 4, 6), edit),
                () => world.IngestChunk(new VoxelChunkCoord((2 * sx) + 1, 4, 6), edit));
            var section = world.Store.Get(new LodSectionKey(0, sx, 2, 3))!;
            Assert.Equal(BlockId.Planks, section.BlockAt(LodSection.Index(7, 3, 5)));
            Assert.Equal(BlockId.Planks, section.BlockAt(LodSection.Index(16 + 7, 3, 5)));
            Assert.Equal(BlockId.Timber, world.Store.Get(new LodSectionKey(0, sx, 1, 3))!.BlockAt(LodSection.Index(9, 9, 9)));
        }
    }

    [Fact]
    public void Given_ManyThreadsPuttingUpdatingAndTrimmingModelSections_When_TheyAreDone_Then_TheModelByteCountIsExact()
    {
        // B07f, C3: a slot trimmed between an Entry() and a Keep() was subtracted twice.
        var store = new LodStore(maxModelBytes: 4 * 10);
        Parallel.For(0, 8, worker =>
        {
            var random = new Random(worker);
            for (var i = 0; i < 3000; i++)
            {
                var key = new LodSectionKey(0, random.Next(40), 0, 0);
                var section = new LodSection(random.Next(2) == 0 ? BlockId.Dirt : BlockId.Stone, (byte)random.Next(2));
                switch (random.Next(3))
                {
                    case 0:
                        _ = store.Put(key, section, LodOrigin.Model);
                        break;
                    case 1:
                        _ = store.Update(key, _ => (section, LodOrigin.Model, (byte)0, (byte)0));
                        break;
                    default:
                        _ = store.Update(key, _ => (section, LodOrigin.Real, (byte)0xFF, (byte)0)); // a real one over a model one takes it out of the count
                        break;
                }
            }
        });

        Assert.Equal(store.ModelBytesRecounted(), store.ModelBytes);
        Assert.True(store.ModelBytes >= 0);
    }

    [Fact]
    public void Given_ACorruptModelSectionInMemory_When_ItIsAsked_Then_ItIsDroppedAndTheModelByteCountFollows()
    {
        var store = new LodStore();
        var key = new LodSectionKey(0, 1, 1, 1);
        _ = store.Put(new LodSectionKey(0, 2, 2, 2), new LodSection(BlockId.Dirt, 0), LodOrigin.Model);
        Assert.True(store.PutEntry(key, new LodEntry([1, 2, 3, 4, 5, 6], LodOrigin.Model, 0)));
        Assert.Equal(10, store.ModelBytes);

        Assert.Null(store.Get(key));

        Assert.Equal(4, store.ModelBytes);
        Assert.Equal(store.ModelBytesRecounted(), store.ModelBytes);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public void Given_AFootprintHoldingAnEdit_When_ItIsReset_Then_ItsSectionsAndTheMipsOverThemAreGoneInMemoryAndOnDiskAndTheFarViewIsTold()
    {
        var disk = new MemoryDisk();
        var store = new LodStore(disk, maxMipLevel: 2);
        var world = WorldOf(42, store, treeLevels: 1);
        var (min, max) = world.SyRange(0);
        for (var dz = 0; dz < 2; dz++)
        {
            for (var dx = 0; dx < 2; dx++)
            {
                world.IngestStack(dx, dz, GeneratorChunk);
            }
        }

        world.IngestChunk(new VoxelChunkCoord(0, 4, 0), HouseChunk());
        Assert.NotNull(store.Entry(new LodSectionKey(1, 0, 0, 0)));
        store.Changed.Clear();

        var dropped = world.ResetFootprint(0, 0);

        Assert.True(dropped >= max - min + 1, $"{dropped} dropped");
        for (var sy = min; sy <= max; sy++)
        {
            Assert.Null(store.Entry(new LodSectionKey(0, 0, sy, 0)));
        }

        Assert.Null(store.Entry(new LodSectionKey(1, 0, 0, 0)));
        Assert.NotNull(store.Entry(new LodSectionKey(0, 1, 1, 0))); // the next footprint is untouched
        Assert.Contains(new LodSectionKey(0, 0, 2, 0), store.Changed);
        Assert.Null(new LodStore(disk).Entry(new LodSectionKey(0, 0, 2, 0))); // a store opened on the same disk finds nothing there

        // The generator's stack makes it again, without the edit.
        world.IngestStack(0, 0, GeneratorChunk);
        Assert.Equal(BlockId.Air, store.Get(new LodSectionKey(0, 0, 2, 0))!.BlockAt(LodSection.Index(7, 3, 5)));
        Assert.Equal(0, store.Entry(new LodSectionKey(0, 0, 2, 0))!.EditedMask);
    }

    [Fact]
    public void Given_AnEditFarFromEveryRealStack_When_ItIsIngested_Then_EveryLevelAboveHoldsItAsAPartlyRealSectionAndARealMipLaterTakesItsPlace()
    {
        // B07f: an edit was told at level 0 only, because the levels above were remade only where the ring had made all eight children real: a house far from the eye never showed in the far view.
        var store = new LodStore(maxMipLevel: 4);
        var world = WorldOf(42, store, treeLevels: 1);
        var coord = new VoxelChunkCoord(640, 12, 640); // over the ground, where the model is air (x 10240, y 192)
        var (bx, by, bz) = (coord.X << 4, coord.Y << 4, coord.Z << 4);
        Assert.Equal(BlockId.Air, world.Stack(0, bx >> 5, bz >> 5).Sections[(by >> 5) - world.SyRange(0).Min].BlockAt(LodSection.Index(bx & 31, by & 31, bz & 31)));

        world.IngestChunk(coord, Enumerable.Repeat(BlockId.Timber, ChunkData.Volume).ToArray());

        for (var level = 1; level <= 4; level++)
        {
            var key = new LodSectionKey(level, bx >> (5 + level), by >> (5 + level), bz >> (5 + level));
            var entry = store.Entry(key);
            Assert.True(entry is { Origin: LodOrigin.Real }, $"level {level}: {entry?.Origin}");
            Assert.False(entry!.IsWhollyReal);
            var voxel = LodSection.Index((bx >> level) & 31, (by >> level) & 31, (bz >> level) & 31);
            Assert.Equal(BlockId.Timber, store.Get(key)!.BlockAt(voxel));
        }

        // The ring then makes the four stacks round it real: level 1 becomes a mip of real sections, with the edit in it, and the levels above stay as they were.
        var (sx, sz) = (bx >> 5, bz >> 5);
        for (var dz = 0; dz < 2; dz++)
        {
            for (var dx = 0; dx < 2; dx++)
            {
                world.IngestStack(sx + dx, sz + dz, GeneratorChunk);
            }
        }

        var up = new LodSectionKey(1, sx >> 1, by >> 6, sz >> 1);
        Assert.Equal(LodOrigin.RealMip, store.Entry(up)!.Origin);
        Assert.Equal(BlockId.Timber, store.Get(up)!.BlockAt(LodSection.Index((bx >> 1) & 31, (by >> 1) & 31, (bz >> 1) & 31)));
        Assert.Equal(LodOrigin.Real, store.Entry(new LodSectionKey(2, bx >> 7, by >> 7, bz >> 7))!.Origin);
    }
}
