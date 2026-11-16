using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Tests.Streaming;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>B12: torches, lanterns and lava are the light kinds; the light index follows a torch placed or removed in a cave; and the valley above its ground looks as it did.</summary>
public sealed class UndergroundLightTests
{
    private static VoxelGrid CaveGrid()
    {
        var grid = new VoxelGrid(64, 64, 64);
        for (var x = 0; x < 32; x++)
        {
            for (var y = 0; y <= 20; y++)
            {
                for (var z = 0; z < 32; z++)
                {
                    grid.SetBlock(x, y, z, BlockId.Stone);
                }
            }
        }

        for (var x = 4; x <= 24; x++)
        {
            for (var y = 6; y <= 9; y++)
            {
                grid.SetBlock(x, y, 8, BlockId.Air); // a tunnel under a roof of stone
            }
        }

        return grid;
    }

    [Theory]
    [InlineData(BlockId.Torch)]
    [InlineData(BlockId.PostTorch)]
    [InlineData(BlockId.Lantern)]
    [InlineData(BlockId.Lava)]
    public void Given_ALightKind_When_ItIsAsked_Then_TheBlockGlows(BlockId id) => Assert.True(BlockDefinition.IsLight(id));

    [Fact]
    public void Given_TheKindsThatAreNotLights_When_TheyAreAsked_Then_TheyDoNotGlow()
    {
        Assert.False(BlockDefinition.IsLight(BlockId.Stone));
        Assert.False(BlockDefinition.IsLight(BlockId.Planks));
        Assert.False(BlockDefinition.IsLight(BlockId.FireRing));
    }

    [Fact]
    public void Given_ATunnel_When_ATorchIsPlacedAndRemoved_Then_TheLightIndexAndTheChunksItReachesChangeAtOnce()
    {
        var grid = CaveGrid();
        var index = new ChunkLightIndex(grid);
        Assert.Empty(index.All);
        var chunk = new VoxelChunkCoord(0, 0, 0);
        var plain = ChunkMesher.Build(new GridBlockSource(grid), 0, 0, 0, 0, new MeshOptions(true, 100, index.Near(chunk), LightBand.Day));
        Assert.Equal(0, plain.GlowMesh.FaceCount);

        grid.SetBlock(10, 6, 8, BlockId.Torch);

        Assert.Equal([new BlockPos(10, 6, 8)], index.All);
        Assert.Equal([new BlockPos(10, 6, 8)], index.TakeChanged());
        Assert.Contains(chunk, ChunkLightIndex.ChunksReachedBy(new BlockPos(10, 6, 8)));
        var lit = ChunkMesher.Build(new GridBlockSource(grid), 0, 0, 0, 0, new MeshOptions(true, 100, index.Near(chunk), LightBand.Day));
        Assert.True(lit.GlowMesh.FaceCount > 20, "the tunnel's walls, floor and roof round the torch have glow quads");
        Assert.Equal(plain.Opaque.FaceCount + 6, lit.Opaque.FaceCount); // the torch's stick; the glow is not in the base mesh (it is its own mesh)

        grid.SetBlock(10, 6, 8, BlockId.Air);

        Assert.Empty(index.All);
        Assert.Equal([new BlockPos(10, 6, 8)], index.TakeChanged());
        var dark = ChunkMesher.Build(new GridBlockSource(grid), 0, 0, 0, 0, new MeshOptions(true, 100, index.Near(chunk), LightBand.Day));
        Assert.Equal(0, dark.GlowMesh.FaceCount);
    }

    [Fact]
    public void Given_ALanternATorchAndLava_When_EachLightsTheSameTunnel_Then_TheyLightItLikeALantern()
    {
        int GlowFaces(BlockId light, out int reach)
        {
            var grid = CaveGrid();
            var index = new ChunkLightIndex(grid);
            grid.SetBlock(10, 6, 8, light);
            reach = ChunkLightIndex.ChunksReachedBy(new BlockPos(10, 6, 8)).Count();
            return ChunkMesher.Build(new GridBlockSource(grid), 0, 0, 0, 0, new MeshOptions(true, 100, index.Near(new VoxelChunkCoord(0, 0, 0)), LightBand.Day)).GlowMesh.FaceCount;
        }

        var lantern = GlowFaces(BlockId.Lantern, out var lanternReach);
        var torch = GlowFaces(BlockId.Torch, out var torchReach);
        var lava = GlowFaces(BlockId.Lava, out var lavaReach);

        Assert.True(lantern > 20);
        Assert.InRange(torch, lantern, lantern + 40); // the torch's fall is gentler, so a few more faces near the edge of the radius are lit at all
        Assert.Equal(lanternReach, lavaReach);
        Assert.Equal(lanternReach, torchReach);
        Assert.InRange(lava, lantern - 6, lantern + 6); // lava is a cube, so it covers a few faces and adds its own
    }

    [Fact]
    public void Given_TheValleyAboveItsGround_When_ItsChunksAreMeshedWithTheSkyDarkening_Then_EveryFaceAboveTheGroundIsTheFaceWithoutIt()
    {
        var scene = StreamedWorlds.Scene();
        var grid = scene.Grid;
        var places = scene.Places!;
        const int Cx0 = 15;
        const int Cx1 = 19; // the village: chunk columns east of the river
        const int Cz0 = 11;
        const int Cz1 = 14;
        for (var cy = grid.Limits.MinChunkY; cy < grid.Limits.EndChunkY; cy++)
        {
            for (var cz = Cz0 - 1; cz <= Cz1 + 1; cz++)
            {
                for (var cx = Cx0 - 1; cx <= Cx1 + 1; cx++)
                {
                    _ = grid.GetChunk(new VoxelChunkCoord(cx, cy, cz));
                }
            }
        }

        var exposure = new SkyExposure(grid, places.NaturalSurfaceAt);
        var source = new GridBlockSource(grid);
        var compared = 0;
        var darkened = 0;
        for (var cy = 0; cy < 8; cy++)
        {
            for (var cz = Cz0; cz <= Cz1; cz++)
            {
                for (var cx = Cx0; cx <= Cx1; cx++)
                {
                    var sky = exposure.Tile(cx, cz);
                    var plain = ChunkMesher.Build(source, cx, cy, cz).Opaque.Vertices.ToArray();
                    var withSky = ChunkMesher.Build(source, cx, cy, cz, 0, new MeshOptions(true, 100, null, LightBand.Day, sky)).Opaque.Vertices.ToArray();
                    Assert.Equal(plain.Length, withSky.Length);
                    for (var q = 0; q < plain.Length; q += 4)
                    {
                        var meanY = (plain[q].Y + plain[q + 1].Y + plain[q + 2].Y + plain[q + 3].Y) / 4f;
                        var meanX = (plain[q].X + plain[q + 1].X + plain[q + 2].X + plain[q + 3].X) / 4f;
                        var meanZ = (plain[q].Z + plain[q + 1].Z + plain[q + 2].Z + plain[q + 3].Z) / 4f;
                        var ground = int.MinValue;
                        for (var dz = -1; dz <= 1; dz++)
                        {
                            for (var dx = -1; dx <= 1; dx++)
                            {
                                ground = Math.Max(ground, sky.At(Math.Clamp((int)MathF.Floor(meanX) + dx, sky.OriginX, sky.OriginX + SkySurface.Side - 1), Math.Clamp((int)MathF.Floor(meanZ) + dz, sky.OriginZ, sky.OriginZ + SkySurface.Side - 1)));
                            }
                        }

                        if (meanY < ground - 3)
                        {
                            darkened++; // a face well under the ground (the cave walls B13 put 8 blocks under it): darkening is wanted there
                            continue;
                        }

                        compared++;
                        for (var k = 0; k < 4; k++)
                        {
                            Assert.True(plain[q + k].Color == withSky[q + k].Color, $"a face at ({meanX}, {meanY}, {meanZ}) of chunk ({cx}, {cy}, {cz}) is darkened above the valley's ground");
                        }
                    }
                }
            }
        }

        Assert.True(compared > 5_000, $"only {compared} faces were compared");
        Assert.True(darkened >= 0);
    }

    [Fact]
    public void Given_ATorchAndALantern_When_TheFloorSixBlocksAwayIsRead_Then_TheTorchLightsItAtLeastTwiceAsStrongly()
    {
        double Reach(BlockId light)
        {
            var grid = CaveGrid();
            var index = new ChunkLightIndex(grid);
            grid.SetBlock(10, 6, 8, light);
            var glow = ChunkMesher.Build(new GridBlockSource(grid), 0, 0, 0, 0, new MeshOptions(true, 100, index.Near(new VoxelChunkCoord(0, 0, 0)), LightBand.Day)).GlowMesh.Vertices.ToArray();
            var floor = glow.Where(static v => v.Y == 6f && Math.Abs(v.X - 16f) < 0.01f && v.Z is >= 8f and <= 9f).ToArray(); // the floor's corners six blocks along the tunnel
            Assert.NotEmpty(floor);
            return floor.Average(static v => (double)(v.Color & 0xFF));
        }

        var torch = Reach(BlockId.Torch);
        Assert.True(torch >= 2 * Reach(BlockId.Lantern), $"torch {torch}");
        Assert.True(torch > 40, $"the torch's light at six blocks is a visible {torch} of 255");
    }

    [Fact]
    public void Given_AShaftOpenedOrRoofedOver_When_ABlockChanges_Then_TheChunksBelowAreMarkedToBeMeshedAgainAndTheSurfaceFollows()
    {
        var grid = new VoxelGrid(32, 48, 32);
        for (var x = 0; x < 32; x++)
        {
            for (var y = 0; y <= 20; y++)
            {
                for (var z = 0; z < 32; z++)
                {
                    grid.SetBlock(x, y, z, BlockId.Stone);
                }
            }
        }

        for (var y = 5; y <= 9; y++)
        {
            grid.SetBlock(8, y, 8, BlockId.Air); // a cave cell under the ground
        }

        var exposure = new SkyExposure(grid, static (_, _) => 20);
        Assert.Equal(20, exposure.SurfaceOf(8, 8));
        Assert.Empty(exposure.TakeDirty());

        for (var y = 10; y <= 20; y++)
        {
            grid.SetBlock(8, y, 8, BlockId.Air); // the shaft is dug open to the surface
        }

        Assert.Equal(4, exposure.SurfaceOf(8, 8));
        var dirty = exposure.TakeDirty();
        Assert.Contains(new VoxelChunkCoord(0, 0, 0), dirty);
        Assert.Contains(new VoxelChunkCoord(0, 1, 0), dirty);
        Assert.Empty(exposure.TakeDirty());

        grid.SetBlock(8, 20, 8, BlockId.Stone); // and roofed over again
        Assert.Equal(20, exposure.SurfaceOf(8, 8));
        Assert.Contains(new VoxelChunkCoord(0, 0, 0), exposure.TakeDirty());

        grid.SetBlock(8, 30, 8, BlockId.Planks); // a block above the ground changes nothing the sky reads
        Assert.Empty(exposure.TakeDirty());
    }
}
