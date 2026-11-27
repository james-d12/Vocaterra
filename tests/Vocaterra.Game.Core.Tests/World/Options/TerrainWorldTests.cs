using System.Collections.Concurrent;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Options;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Options;

/// <summary>
/// The worlds the terrain options make (Q02): at the defaults the world is the shipped one to the block, every option at the lowest and the highest of its slider generates without error across the valley's edge and the
/// land round it, the valley is untouched and still ringed by walkable land, the same seed and options give the same chunks and other options give other ones, and the options survive a save.
/// </summary>
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class TerrainWorldTests(ITestOutputHelper output)
{
    private const ulong Seed = 42;

    private static readonly ConcurrentDictionary<string, Lazy<Scene>> Scenes = new(StringComparer.Ordinal);

    private static TerrainOptionData Table => TestGame.Data.TerrainOptions;

    private static WorldOptions Of(string id, int value) => Table.Create([new KeyValuePair<string, int>(id, value)]);

    /// <summary>The streamed world of the seed with the options, built once per options (the sources are pure and read-only).</summary>
    private static Scene World(WorldOptions options, ulong seed = Seed) =>
        Scenes.GetOrAdd($"{seed}/{options.Canonical}", _ => new Lazy<Scene>(() => WorldFactory.Create(TestGame.Data, seed, WorldMode.Streamed, null, null, options))).Value;

    public static TheoryData<string, int> Extremes()
    {
        var data = new TheoryData<string, int>();
        foreach (var option in TestGame.Data.TerrainOptions.Options)
        {
            data.Add(option.Id, option.Min);
            data.Add(option.Id, option.Max);
        }

        return data;
    }

    // Chunk columns to read: the valley's rim and a few inside, the exit corridor's end, and rings of land out to 5 000 blocks on every side.
    private static IEnumerable<(int Cx, int Cz)> SampleColumns()
    {
        foreach (var c in new[] { -2, -1, 0, 1, 12, 22, 23, 24, 25 })
        {
            yield return (c, -1);
            yield return (c, 24);
            yield return (-1, c);
            yield return (24, c);
        }

        yield return (24, 13); // the pass: the corridor leaves the valley's east edge at z 208
        yield return (30, 13);
        foreach (var d in new[] { 40, 80, 160, 320 })
        {
            yield return (d, 12);
            yield return (-d, 12);
            yield return (12, d);
            yield return (12, -d);
        }
    }

    [Fact]
    public void Given_TheDefaults_When_AWorldIsMade_Then_ItIsTheWorldMadeWithNoOptionsToTheBlockAndTheDigest()
    {
        var plain = WorldFactory.Create(TestGame.Data, Seed, WorldMode.Streamed);
        var explicitDefaults = World(WorldOptions.Default);
        var spelledOut = World(Table.Create([new("mountains", 100), new("hills", 100), new("ocean", 1), new("continents", 100), new("rivers", 100)]));
        Assert.True(spelledOut.TerrainOptions.IsDefault);
        foreach (var scene in new[] { explicitDefaults, spelledOut })
        {
            foreach (var (cx, cz) in SampleColumns())
            {
                foreach (var cy in new[] { 0, 3, 4, 5, 7, 12, 17, 22 })
                {
                    var coord = new VoxelChunkCoord(cx, cy, cz);
                    Assert.Equal(plain.ChunkSource!.Generate(coord).ComputeDigestHex(), scene.ChunkSource!.Generate(coord).ComputeDigestHex());
                }
            }
        }

        var header = WorldStateHeader.For(TestGame.Bundle, Seed, plain.TemplateDigest);
        var same = WorldStateHeader.For(TestGame.Bundle, Seed, plain.TemplateDigest, spelledOut.TerrainOptions);
        Assert.Equal(header.GenerationDataHash, same.GenerationDataHash);
        Assert.Equal(string.Empty, same.TerrainKey);
        Assert.Equal(WorldStateDigest.ComputeHex(plain.Grid, header), WorldStateDigest.ComputeHex(spelledOut.Grid, same));
    }

    [Fact]
    public void Given_OptionsOffTheirDefaults_When_TheHeaderIsMade_Then_TheGenerationHashAndTheKeyNameThem()
    {
        var plain = WorldStateHeader.For(TestGame.Bundle, Seed);
        var one = WorldStateHeader.For(TestGame.Bundle, Seed, null, Of("mountains", 150));
        var other = WorldStateHeader.For(TestGame.Bundle, Seed, null, Of("mountains", 125));
        Assert.NotEqual(plain.GenerationDataHash, one.GenerationDataHash);
        Assert.NotEqual(one.GenerationDataHash, other.GenerationDataHash);
        Assert.Equal(Of("mountains", 150).Key, one.TerrainKey);
        Assert.NotEqual(one.TerrainKey, other.TerrainKey);
        var scene = World(Of("mountains", 150));
        Assert.Equal(Of("mountains", 150), scene.TerrainOptions);
        Assert.Equal(one.GenerationDataHash, new VoxelSession(TestGame.Data, Seed, GuideSession.DefaultStartedAt, scene, CreditRules.FullTyped, TestGame.NoLessons).Simulation.StateHeader.GenerationDataHash);
    }

    [Theory]
    [MemberData(nameof(Extremes))]
    public void Given_AnOptionAtAnExtreme_When_ChunksAcrossTheValleyEdgeAndTheLandAreGenerated_Then_NoneFailsAndTheValleyIsTheShippedOne(string id, int value)
    {
        var options = Of(id, value);
        var scene = World(options);
        var shipped = World(WorldOptions.Default);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var generated = 0;
        foreach (var (cx, cz) in SampleColumns())
        {
            for (var cy = 0; cy < 24; cy += 2)
            {
                var coord = new VoxelChunkCoord(cx, cy, cz);
                var chunk = scene.ChunkSource!.Generate(coord);
                generated++;
                if (cx is >= 0 and < 24 && cz is >= 0 and < 24 && cy < 8)
                {
                    Assert.Equal(shipped.ChunkSource!.Generate(coord).ComputeDigestHex(), chunk.ComputeDigestHex()); // inside the valley the world is the authored one at every value
                }
            }
        }

        output.WriteLine($"{options}: {generated} chunks in {watch.ElapsedMilliseconds} ms");
        Assert.True(generated > 300);
    }

    [Theory]
    [MemberData(nameof(Extremes))]
    public void Given_AnOptionAtAnExtreme_When_TheLandRoundTheValleyIsRead_Then_NoSeaIsWithinAThousandBlocksAndTheCorridorLeavesTheEdgeOnLand(string id, int value)
    {
        var scene = World(Of(id, value));
        var places = scene.Places!;
        var sea = TestGame.Data.WorldGen.Vertical.SeaLevel;
        foreach (var d in new[] { 100, 300, 600, 1000 })
        {
            foreach (var (x, z) in new[] { (192 + d + 192, 192), (192 - d - 192, 192), (192, 192 + d + 192), (192, 192 - d - 192) })
            {
                Assert.True(places.ClimateAt(x, z).Continentalness >= 0, $"{id}={value}: sea at {x},{z}");
            }
        }

        // The pass: the road runs from the valley's east edge at z 208 on dry ground, and the ground beside it never steps more than three blocks between neighbours.
        var previous = places.NaturalSurfaceAt(384, 208);
        for (var x = 385; x <= 520; x++)
        {
            var height = places.NaturalSurfaceAt(x, 208);
            Assert.True(height >= sea, $"{id}={value}: the corridor is under water at {x}");
            Assert.True(Math.Abs(height - previous) <= 3, $"{id}={value}: a step of {height - previous} at x {x}");
            previous = height;
        }
    }

    [Theory]
    [InlineData("hills", 200)]
    [InlineData("hills", 0)]
    [InlineData("mountains", 150)]
    [InlineData("ocean", 3)]
    [InlineData("continents", 50)]
    [InlineData("continents", 150)]
    public void Given_AnExtremeOption_When_AWalkIsSoughtFromTheBorderOfTheValley_Then_TheMarginIsStillCrossedOnFootFromNearlyAllOfThem(string id, int value)
    {
        var scene = World(Of(id, value));
        var places = scene.Places!;
        var margin = TestGame.Data.WorldGen.Blend.Margin;
        var sea = TestGame.Data.WorldGen.Vertical.SeaLevel;
        var (min, max) = (0, 383);
        var depth = margin + 16;
        var (minX, minZ) = (min - depth, min - depth);
        var size = (max - min + 1) + (2 * depth);
        var height = new int[size * size];
        var open = new bool[size * size];
        for (var j = 0; j < size; j++)
        {
            for (var i = 0; i < size; i++)
            {
                var (x, z) = (minX + i, minZ + j);
                height[(j * size) + i] = places.NaturalSurfaceAt(x, z);
                open[(j * size) + i] = !(x is >= 0 and <= 383 && z is >= 0 and <= 383) && height[(j * size) + i] >= sea;
            }
        }

        static int Distance(int x, int z) => Math.Max(Math.Max(0 - x, x - 383), Math.Max(0 - z, z - 383));
        var reaches = new bool[size * size];
        var queue = new Queue<int>();
        for (var k = 0; k < size * size; k++)
        {
            if (open[k] && Distance(minX + (k % size), minZ + (k / size)) >= margin)
            {
                reaches[k] = true;
                queue.Enqueue(k);
            }
        }

        while (queue.TryDequeue(out var k))
        {
            var (i, j) = (k % size, k / size);
            foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var (ni, nj) = (i + di, j + dj);
                if ((uint)ni >= (uint)size || (uint)nj >= (uint)size)
                {
                    continue;
                }

                var n = (nj * size) + ni;
                var rise = height[k] - height[n];
                if (!reaches[n] && open[n] && rise <= 1 && -rise <= 3)
                {
                    reaches[n] = true;
                    queue.Enqueue(n);
                }
            }
        }

        var (starts, ok) = (0, 0);
        for (var t = 0; t <= 383; t += 8)
        {
            foreach (var (x, z) in new[] { (t, -1), (t, 384), (-1, t), (384, t) })
            {
                var k = ((z - minZ) * size) + (x - minX);
                if (open[k])
                {
                    starts++;
                    ok += reaches[k] ? 1 : 0;
                }
            }
        }

        output.WriteLine($"{id}={value}: {ok} of {starts} starts leave the margin on foot");
        Assert.True(ok >= starts * 0.9, $"{ok} of {starts}");
    }

    [Fact]
    public void Given_TheSameSeedAndOptions_When_TwoWorldsAreMade_Then_TheirChunksAreEqual_AndOtherOptionsGiveOtherLand()
    {
        var options = Table.Create([new("mountains", 150), new("hills", 50), new("ocean", 2)]);
        var a = WorldFactory.Create(TestGame.Data, Seed, WorldMode.Streamed, null, null, options);
        var b = WorldFactory.Create(TestGame.Data, Seed, WorldMode.Streamed, null, null, Table.Create([new("ocean", 2), new("hills", 50), new("mountains", 150)]));
        var other = World(Of("mountains", 150));
        var differs = 0;
        foreach (var (cx, cz) in SampleColumns())
        {
            foreach (var cy in new[] { 1, 4, 7, 10, 14 })
            {
                var coord = new VoxelChunkCoord(cx, cy, cz);
                var digest = a.ChunkSource!.Generate(coord).ComputeDigestHex();
                Assert.Equal(digest, b.ChunkSource!.Generate(coord).ComputeDigestHex());
                differs += digest == other.ChunkSource!.Generate(coord).ComputeDigestHex() ? 0 : 1;
            }
        }

        Assert.True(differs > 0, "the sea and the hills differ from mountains alone somewhere");
        Assert.Equal(options, a.TerrainOptions);
        Assert.Equal(options, b.TerrainOptions);
    }

    [Fact]
    public void Given_TheOptionsAreSet_When_TheLandFarFromTheValleyIsRead_Then_EachOptionChangesTheLandItNames()
    {
        // The mountains: higher peaks somewhere far; the hills: a rougher ground; the sea: a different share of ocean.
        static int Highest(Scene scene) => Enumerable.Range(0, 80).SelectMany(static i => Enumerable.Range(0, 80).Select(j => (X: 3000 + (i * 150), Z: -9000 + (j * 150)))).Max(p => scene.Places!.NaturalSurfaceAt(p.X, p.Z));
        Assert.True(Highest(World(Of("mountains", 150))) > Highest(World(Of("mountains", 25))));

        static int WetColumns(Scene scene) => Enumerable.Range(0, 120).SelectMany(static i => Enumerable.Range(0, 120).Select(j => (X: -9000 + (i * 150), Z: -9000 + (j * 150)))).Count(p => scene.Places!.ClimateAt(p.X, p.Z).Continentalness < 0);
        var (narrow, wide) = (WetColumns(World(Of("ocean", 0))), WetColumns(World(Of("ocean", 3))));
        output.WriteLine($"sea columns of 14 400: narrow seas {narrow}, mostly ocean {wide}");
        Assert.True(wide > narrow);
    }

    [Fact]
    public void Given_AWorldWithOptions_When_ItIsSavedAndLoaded_Then_TheOptionsAreKeptWithTheSeedAndAWorldOfOtherOptionsIsRefused()
    {
        var options = Table.Create([new("mountains", 150), new("rivers", 50)]);
        var scene = WorldFactory.Create(TestGame.Data, Seed, WorldMode.Streamed, null, null, options);
        var session = new VoxelSession(TestGame.Data, Seed, GuideSession.DefaultStartedAt, scene, CreditRules.FullTyped);
        var text = SaveGame.Save(session);
        var document = SaveGame.Parse(text);
        Assert.Equal([new TerrainOptionSave("mountains", 150), new TerrainOptionSave("rivers", 50)], document.Terrain);
        Assert.Contains("\"terrain\":[{\"id\":\"mountains\",\"value\":150},{\"id\":\"rivers\",\"value\":50}]", text, StringComparison.Ordinal);
        Assert.Equal(options, SaveGame.TerrainOptionsOf(document, TestGame.Data));

        var restored = SaveGame.Restore(document, TestGame.Data, WorldFactory.Create(TestGame.Data, document.WorldSeed, WorldMode.Streamed, null, null, SaveGame.TerrainOptionsOf(document, TestGame.Data)), CreditRules.FullTyped, bumpSession: false);
        Assert.Equal(options, restored.Scene.TerrainOptions);
        Assert.Equal(session.Simulation.ComputeStateDigest(), restored.Simulation.ComputeStateDigest());
        Assert.Equal(text, SaveGame.Save(restored));

        var wrong = Assert.Throws<SaveException>(() => SaveGame.Restore(document, TestGame.Data, World(WorldOptions.Default), CreditRules.FullTyped, bumpSession: false));
        Assert.Contains("terrain options", wrong.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_ASaveAtTheDefaults_When_ItIsWritten_Then_ItHasNoTerrainEntryAndASaveNamingAnUnknownOptionIsRefused()
    {
        var scene = World(WorldOptions.Default);
        var session = new VoxelSession(TestGame.Data, Seed, GuideSession.DefaultStartedAt, scene, CreditRules.FullTyped);
        var text = SaveGame.Save(session);
        Assert.DoesNotContain("terrain", text, StringComparison.Ordinal);
        Assert.Null(SaveGame.Parse(text).Terrain);
        var bad = SaveGame.Parse(text) with { Terrain = [new TerrainOptionSave("volcanoes", 3)] };
        var error = Assert.Throws<SaveException>(() => SaveGame.TerrainOptionsOf(bad, TestGame.Data));
        Assert.Contains("volcanoes", error.Message, StringComparison.Ordinal);
        var off = SaveGame.Parse(text) with { Terrain = [new TerrainOptionSave("mountains", 110)] };
        _ = Assert.Throws<SaveException>(() => SaveGame.TerrainOptionsOf(off, TestGame.Data));
    }
}
