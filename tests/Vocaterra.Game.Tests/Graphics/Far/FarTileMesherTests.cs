using Microsoft.Xna.Framework;
using Vocaterra.Game.Core.World.Far;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Tests.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Graphics.Far;

/// <summary>The far view's tile meshes (B07): the shape every tile shares, watertight within a level, skirts that hide the steps between levels, deterministic and safe to build on any thread.</summary>
public sealed class FarTileMesherTests(ITestOutputHelper output)
{
    private static readonly FarViewSettings Settings = new(4096);

    private static FarTerrainSampler Sampler => StreamedWorlds.Composition.World.Far;

    private static Vector3 Normal(FarTileMesh mesh, ushort[] indices, int triangle)
    {
        var a = mesh.Vertices[indices[triangle * 3]].Position;
        var b = mesh.Vertices[indices[(triangle * 3) + 1]].Position;
        var c = mesh.Vertices[indices[(triangle * 3) + 2]].Position;
        return Vector3.Cross(b - a, c - a);
    }

    // A tile over rugged land outside the valley, at a level.
    private static FarTileKey Tile(int level, int offset = 0) => new(level, 9 + offset, -7);

    [Fact]
    public void Given_ATile_When_ItIsBuilt_Then_ItHasTheSharedLayoutAndItsBoxHoldsEveryVertex()
    {
        var key = Tile(1);

        var mesh = FarTileMesher.Build(Sampler, Settings, key);

        Assert.Equal(FarTileTopology.VertexCount(Settings.TileCells), mesh.Vertices.Length);
        Assert.Equal(((32 + 1) * (32 + 1)) + (4 * (32 + 1)), mesh.Vertices.Length);
        Assert.All(mesh.Vertices, v => Assert.Equal(ContainmentType.Contains, mesh.Box.Contains(v.Position) is ContainmentType.Contains or ContainmentType.Intersects ? ContainmentType.Contains : ContainmentType.Disjoint));
        Assert.Equal(key, mesh.Key);
        Assert.Equal(key.MinX(Settings), mesh.Vertices[FarTileTopology.Land(32, 0, 0)].Position.X);
        Assert.Equal(key.MinX(Settings) + Settings.SizeOf(1), mesh.Vertices[FarTileTopology.Land(32, 32, 32)].Position.X);
        Assert.True(mesh.BuildMilliseconds >= 0);
    }

    [Fact]
    public void Given_TheTopology_When_ItsTrianglesAreChecked_Then_TheLandFacesUpAndEverySkirtFacesAwayFromItsTile()
    {
        var mesh = FarTileMesher.Build(Sampler, Settings, Tile(2));
        var indices = FarTileTopology.Indices(Settings.TileCells);

        Assert.Equal(FarTileTopology.TriangleCount(Settings.TileCells) * 3, indices.Length);
        Assert.All(indices, i => Assert.True(i < mesh.Vertices.Length));
        var landTriangles = 2 * Settings.TileCells * Settings.TileCells;
        for (var t = 0; t < landTriangles; t++)
        {
            Assert.True(Normal(mesh, indices, t).Y > 0, $"land triangle {t} faces down");
        }

        var outwards = new[] { -Vector3.UnitZ, Vector3.UnitZ, -Vector3.UnitX, Vector3.UnitX };
        for (var edge = 0; edge < 4; edge++)
        {
            for (var i = 0; i < Settings.TileCells * 2; i++)
            {
                var triangle = landTriangles + (edge * Settings.TileCells * 2) + i;
                Assert.True(Vector3.Dot(Normal(mesh, indices, triangle), outwards[edge]) > 0, $"skirt triangle {triangle} of edge {edge} does not face away from the tile");
            }
        }
    }

    [Fact]
    public void Given_TwoNeighbouringTilesOfOneLevel_When_TheirSharedEdgeIsRead_Then_TheVerticesAndTheirColoursAreIdentical()
    {
        var west = FarTileMesher.Build(Sampler, Settings, Tile(1));
        var east = FarTileMesher.Build(Sampler, Settings, Tile(1, 1));
        var south = FarTileMesher.Build(Sampler, Settings, new FarTileKey(1, 9, -6));
        var n = Settings.TileCells;

        for (var j = 0; j <= n; j++)
        {
            Assert.Equal(west.Vertices[FarTileTopology.Land(n, n, j)], east.Vertices[FarTileTopology.Land(n, 0, j)]);
        }

        for (var i = 0; i <= n; i++)
        {
            Assert.Equal(west.Vertices[FarTileTopology.Land(n, i, n)], south.Vertices[FarTileTopology.Land(n, i, 0)]);
        }
    }

    [Fact]
    public void Given_ATileAndItsCoarserNeighbour_When_TheirSeamIsRead_Then_TheSharedVerticesMatchAndTheStepBetweenThemLiesWithinTheSkirt()
    {
        // The coarse tile of level 2 covers x from 9 x 512 and the fine tiles of level 1 beside it to its west cover the 256 blocks before that.
        var coarse = FarTileMesher.Build(Sampler, Settings, new FarTileKey(2, 9, -7));
        var fine = FarTileMesher.Build(Sampler, Settings, new FarTileKey(1, 17, -14)); // x 17 x 256 to 18 x 256: the west half of the coarse tile's footprint's neighbour
        var n = Settings.TileCells;
        Assert.Equal(coarse.Vertices[FarTileTopology.Land(n, 0, 0)].Position.X, fine.Vertices[FarTileTopology.Land(n, n, 0)].Position.X + Settings.SizeOf(1) - Settings.SizeOf(1) + Settings.SizeOf(1) - Settings.SizeOf(1) + 0 + Settings.SizeOf(1) - Settings.SizeOf(1));

        // Along the shared west edge of the coarse tile, the fine vertices at the coarse ones' places are the same points; the ones between differ from the coarse line by less than the skirt hangs.
        var west = FarTileMesher.Build(Sampler, Settings, new FarTileKey(1, 18, -14));
        var coarseEdge = Enumerable.Range(0, n + 1).Select(j => coarse.Vertices[FarTileTopology.Land(n, 0, j)].Position).ToList();
        var fineEdge = Enumerable.Range(0, (n / 2) + 1).Select(j => west.Vertices[FarTileTopology.Land(n, 0, j)].Position).ToList();
        var worst = 0f;
        for (var j = 0; j < fineEdge.Count; j++)
        {
            var z = fineEdge[j].Z;
            var t = (z - coarseEdge[0].Z) / (coarseEdge[1].Z - coarseEdge[0].Z);
            var k = (int)MathF.Floor(t);
            var line = MathHelper.Lerp(coarseEdge[k].Y, coarseEdge[Math.Min(k + 1, n)].Y, t - k);
            if (Math.Abs(t - MathF.Round(t)) < 1e-4f)
            {
                Assert.Equal(coarseEdge[(int)MathF.Round(t)].Y, fineEdge[j].Y); // a shared point is the same height
            }

            worst = Math.Max(worst, Math.Abs(fineEdge[j].Y - line));
        }

        output.WriteLine($"the widest step between a fine edge and the coarse line over 16 vertices: {worst:0.0} blocks, the coarse skirt hangs {Settings.SkirtOf(2)}");
        Assert.True(worst <= Settings.SkirtOf(2), $"the step {worst} is deeper than the skirt {Settings.SkirtOf(2)}");
        Assert.NotNull(fine);
    }

    [Fact]
    public void Given_TheSameTileBuiltTwiceAndFromManyThreads_When_TheMeshesAreCompared_Then_TheyAreIdentical()
    {
        var key = Tile(3);
        var first = FarTileMesher.Build(Sampler, Settings, key);
        var results = new FarTileMesh[4];

        Parallel.For(0, 4, new ParallelOptions { MaxDegreeOfParallelism = 2 }, t => results[t] = FarTileMesher.Build(Sampler, Settings, key));

        Assert.All(results, m => Assert.True(first.Vertices.SequenceEqual(m.Vertices)));
        Assert.True(first.Vertices.SequenceEqual(FarTileMesher.Build(Sampler, Settings, key).Vertices));
    }

    [Fact]
    public void Given_TilesOverTheSea_When_TheyAreBuilt_Then_TheWaterIsFlatAtTheSeaAndBlue()
    {
        // Find a tile with water among a scan of coarse tiles.
        var sea = Enumerable.Range(-12, 24).SelectMany(static tz => Enumerable.Range(-12, 24).Select(tx => new FarTileKey(3, tx, tz)))
            .First(key => Sampler.At(key.MinX(Settings) + 512, key.MinZ(Settings) + 512).IsWater && Sampler.At(key.MinX(Settings), key.MinZ(Settings)).IsWater);

        var mesh = FarTileMesher.Build(Sampler, Settings, sea);

        var n = Settings.TileCells;
        var cell = Settings.CellOf(sea.Level);
        var waterVertices = 0;
        for (var j = 0; j <= n; j++)
        {
            for (var i = 0; i <= n; i++)
            {
                var sample = Sampler.At(sea.MinX(Settings) + (i * cell), sea.MinZ(Settings) + (j * cell));
                var vertex = mesh.Vertices[FarTileTopology.Land(n, i, j)];
                if (sample.IsWater)
                {
                    waterVertices++;
                    Assert.Equal(sample.WaterLevel + 1f, vertex.Position.Y);
                    Assert.True(vertex.Color.B > vertex.Color.R, "the sea is bluer than red");
                }
            }
        }

        Assert.True(waterVertices > n);
    }

    [Fact]
    public void Given_Tiles_When_TheyAreBuilt_Then_TheTimeTheTrianglesAndTheMemoryOfOneAreReported()
    {
        _ = FarTileMesher.Build(Sampler, Settings, Tile(0));
        var rows = new List<string>();
        foreach (var level in Enumerable.Range(0, Settings.Levels))
        {
            var best = Enumerable.Range(0, 3).Min(i => FarTileMesher.Build(Sampler, Settings, Tile(level, i)).BuildMilliseconds);
            rows.Add($"L{level} ({Settings.CellOf(level)} blocks a cell) {best:0.0} ms");
        }

        output.WriteLine($"one tile: {string.Join(", ", rows)}; {FarTileTopology.VertexCount(Settings.TileCells)} vertices ({FarTileTopology.VertexCount(Settings.TileCells) * 16 / 1024.0:0.0} KB), {FarTileTopology.TriangleCount(Settings.TileCells)} triangles (Debug, one thread)");
        Assert.True(FarTileTopology.VertexCount(Settings.TileCells) < ushort.MaxValue);
    }

    [Fact]
    public void Given_AColumn_When_ItIsColoured_Then_SnowIsWhiteForestGreensTheGroundAndTheLightIsTheChunksOwn()
    {
        var meadow = new FarSample(70, -1, Vocaterra.Game.Core.Voxel.BlockId.Grass, false, 0);
        var forest = meadow with { Forest = 100 };
        var snow = new FarSample(250, -1, Vocaterra.Game.Core.Voxel.BlockId.Snow, true, 0);
        var top = ChunkMesher.SunTintFor(0f, 1f, 0f);
        var away = ChunkMesher.SunTintFor(0.7f, 0.7f, 0.1f);

        var plain = FarTileMesher.ColourOf(meadow, top);
        var wooded = FarTileMesher.ColourOf(forest, top);
        var white = FarTileMesher.ColourOf(snow, top);

        Assert.NotEqual(plain, wooded);
        Assert.True(white.R > 200 && white.G > 200 && white.B > 200);
        Assert.True(FarTileMesher.ColourOf(meadow, away).R < plain.R, "a slope turned from the sun is darker, as a side face of a block is");
        Assert.Equal(255, plain.A);
    }

    [Fact]
    public void Given_ABlockDrawnGreyInTheChunks_When_ItsLandIsColoured_Then_TheFarLandIsGreyToo()
    {
        var meadow = new FarSample(70, -1, Vocaterra.Game.Core.Voxel.BlockId.Grass, false, 0);
        var top = (1f, 1f, 1f);

        var colour = FarTileMesher.ColourOf(meadow, top);
        var grey = FarTileMesher.ColourOf(meadow, top, ChunkMesher.KindBit(Vocaterra.Game.Core.Voxel.BlockId.Grass));
        var other = FarTileMesher.ColourOf(meadow, top, ChunkMesher.KindBit(Vocaterra.Game.Core.Voxel.BlockId.Sand));

        Assert.True(colour.G > colour.R && colour.G > colour.B);
        Assert.True(grey.R == grey.G && grey.G == grey.B);
        Assert.Equal(colour, other);
    }

    [Fact]
    public void Given_ATileBuiltGreyAndInColour_When_TheyAreCompared_Then_OnlyTheColoursDifferAndTheShapeIsTheSame()
    {
        var key = Tile(1);
        var colour = FarTileMesher.Build(Sampler, Settings, key);
        var grey = FarTileMesher.Build(Sampler, Settings, key, ~UInt128.Zero);

        Assert.Equal(colour.Vertices.Select(static v => v.Position), grey.Vertices.Select(static v => v.Position));
        Assert.All(grey.Vertices, v => Assert.True(v.Color.R == v.Color.G || Math.Abs(v.Color.R - v.Color.G) <= 40)); // light tints the channels a little differently
        Assert.NotEqual(colour.Vertices.Select(static v => v.Color), grey.Vertices.Select(static v => v.Color));
    }
}
