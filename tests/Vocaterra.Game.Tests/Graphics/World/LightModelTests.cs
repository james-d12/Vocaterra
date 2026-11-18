using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Water;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>
/// B17c: the light as data and the light baked are the same light. A mesh built with <see cref="MeshOptions.Data"/> carries each corner's sky, block and occlusion and its face; the shader's maths (<see cref="LightModel"/>, in C#) over a band's sun
/// gives the colour the baked mesh of that band holds, to the rounding of a byte: so the effects' fallback and the shader show one picture, and the hour moves the shader's without a new mesh.
/// </summary>
public sealed class LightModelTests
{
    private const float Tolerance = 3f / 255f; // the baked bytes are cut and the data's are rounded, and the sun's multiplier is divided and multiplied back

    private static MapBlocks Scene() => new MapBlocks().Fill(0, 4, 0, 15, 5, 15, BlockId.Grass)
        .Set(8, 6, 8, BlockId.Fence).Set(3, 6, 3, BlockId.Bush).Set(5, 6, 5, BlockId.TallGrass).Set(10, 6, 4, BlockId.Water)
        .Set(6, 6, 6, BlockId.Lantern).Set(9, 6, 9, BlockId.PostTorch).Set(12, 6, 12, BlockId.Bed).Set(12, 6, 13, BlockId.BedFoot).Set(2, 6, 12, BlockId.Door)
        .Fill(0, 6, 0, 3, 8, 0, BlockId.Stone).Set(14, 6, 2, BlockId.Loaves);

    private static MapBlocks Cave() => new MapBlocks().Fill(0, 0, 0, 15, 9, 15, BlockId.Stone).Fill(3, 1, 3, 12, 3, 12, BlockId.Air).Set(6, 1, 6, BlockId.Lava);

    private static readonly BlockPos[] Lights = [new BlockPos(6, 6, 6), new BlockPos(9, 9, 9)];

    private static SunLightState SunOf(LightBand band) => new(LightBands.SunDirectionOf(band), LightBands.BakedSun(band));

    private static void AssertSame(MeshBuffer baked, MeshBuffer data, SunLightState sun, string what)
    {
        var b = baked.Vertices.ToArray();
        var d = data.Vertices.ToArray();
        var words = data.Lights.ToArray();
        Assert.Equal(b.Length, d.Length);
        Assert.Equal(d.Length, words.Length);
        for (var i = 0; i < b.Length; i++)
        {
            Assert.Equal((b[i].X, b[i].Y, b[i].Z, b[i].U, b[i].V), (d[i].X, d[i].Y, d[i].Z, d[i].U, d[i].V)); // the same quads in the same order
            var (r, g, bl) = LightModel.Corner(d[i].Color, words[i], sun, 0f);
            Assert.True(Math.Abs(r - (b[i].Color & 0xFF) / 255f) <= Tolerance, $"{what} vertex {i}: red {r * 255f} against {b[i].Color & 0xFF}");
            Assert.True(Math.Abs(g - ((b[i].Color >> 8) & 0xFF) / 255f) <= Tolerance, $"{what} vertex {i}: green {g * 255f} against {(b[i].Color >> 8) & 0xFF}");
            Assert.True(Math.Abs(bl - ((b[i].Color >> 16) & 0xFF) / 255f) <= Tolerance, $"{what} vertex {i}: blue {bl * 255f} against {(b[i].Color >> 16) & 0xFF}");
            Assert.Equal(b[i].Color >> 24, d[i].Color >> 24); // the alpha is the material's
        }
    }

    [Fact]
    public void Given_EveryBand_When_TheDataMeshIsLitByThatBandsSun_Then_ItIsTheBakedMeshOfTheBandToTheByte()
    {
        foreach (var band in Enum.GetValues<LightBand>())
        {
            var baked = ChunkMesher.Build(Scene(), 0, 0, 0, 0, new MeshOptions(true, 100, null, band, WaterTint: false));
            var data = ChunkMesher.Build(Scene(), 0, 0, 0, 0, new MeshOptions(true, 100, null, band, Data: true));
            var sun = SunOf(band);
            AssertSame(baked.Opaque, data.Opaque, sun, $"{band} opaque");
            AssertSame(baked.PlantMesh, data.PlantMesh, sun, $"{band} plants");
            AssertSame(baked.LitMesh, data.LitMesh, sun, $"{band} lit");
        }
    }

    [Fact]
    public void Given_TheCaveUnderTheSky_When_TheDataMeshIsLit_Then_TheDarkeningByDepthIsTheSkyChannelAndTheBakedMeshIsTheSame()
    {
        var surface = new SkySurface(0, 0, static (_, _) => 9);
        var baked = ChunkMesher.Build(Cave(), 0, 0, 0, 0, new MeshOptions(true, 100, null, LightBand.Day, surface));
        var data = ChunkMesher.Build(Cave(), 0, 0, 0, 0, new MeshOptions(true, 100, null, LightBand.Day, surface, Data: true));
        AssertSame(baked.Opaque, data.Opaque, SunLightState.Noon, "cave");
        var sky = data.Opaque.Lights.ToArray().Select(static w => LightData.Sky(w)).ToArray();
        Assert.Equal(255, sky.Max());
        Assert.InRange(sky.Min(), (int)MathF.Round(SkyDepthLight.CaveMinimum * 255f) - 1, (int)MathF.Round(SkyDepthLight.CaveMinimum * 255f) + 1);
    }

    [Fact]
    public void Given_TheLanternsOfAScene_When_TheDataMeshIsBuilt_Then_TheBlockChannelIsTheReachOfTheBakedGlowMeshAndThereIsNoGlowMesh()
    {
        var baked = ChunkMesher.Build(Scene(), 0, 0, 0, 0, new MeshOptions(true, 100, Lights, LightBand.Day));
        var data = ChunkMesher.Build(Scene(), 0, 0, 0, 0, new MeshOptions(true, 100, Lights, LightBand.Day, Data: true));
        Assert.NotEqual(0, baked.GlowMesh.FaceCount);
        Assert.Equal(0, data.GlowMesh.FaceCount); // the glow is the block light in the faces' own vertices, drawn with them (no second pass)
        Assert.Equal(baked.Opaque.FaceCount, data.Opaque.FaceCount);

        // The glow quads are the very quads of the opaque mesh (the same four positions in the same order); the grey level of a glow corner is the block light there.
        var opaque = data.Opaque.Vertices.ToArray();
        var words = data.Opaque.Lights.ToArray();
        var glow = baked.GlowMesh.Vertices.ToArray();
        static (float, float, float, float, float, float, float, float, float, float, float, float) Key(MeshVertex[] v, int quad) => (v[quad * 4].X, v[quad * 4].Y, v[quad * 4].Z, v[(quad * 4) + 1].X, v[(quad * 4) + 1].Y, v[(quad * 4) + 1].Z, v[(quad * 4) + 2].X, v[(quad * 4) + 2].Y, v[(quad * 4) + 2].Z, v[(quad * 4) + 3].X, v[(quad * 4) + 3].Y, v[(quad * 4) + 3].Z);
        var byQuad = new Dictionary<(float, float, float, float, float, float, float, float, float, float, float, float), int>();
        for (var q = 0; q < opaque.Length / 4; q++)
        {
            byQuad[Key(opaque, q)] = q;
        }

        Assert.NotEmpty(glow);
        for (var q = 0; q < glow.Length / 4; q++)
        {
            var at = byQuad[Key(glow, q)];
            for (var k = 0; k < 4; k++)
            {
                var block = LightData.Block(words[(at * 4) + k]);
                var grey = (int)(glow[(q * 4) + k].Color & 0xFF);
                Assert.True(Math.Abs(block - grey) <= 1, $"block light {block} against the glow's {grey}");
            }
        }

        Assert.Contains(words, static w => LightData.Block(w) > 0);
        Assert.Equal(0, ChunkMesher.Build(Scene(), 0, 0, 0, 0, new MeshOptions(true, 100, null, LightBand.Day, Data: true)).Opaque.Lights.ToArray().Count(static w => LightData.Block(w) > 0));
    }

    [Fact]
    public void Given_TheBlockLight_When_ItIsAddedByTheShader_Then_ItIsAmberTimesTheStrengthAndTheLightLevelsOfB23FillTheSameByte()
    {
        var word = LightData.Pack(LightData.FromLevel(15), LightData.FromLevel(15), 3, LightFace.Up);
        var (r, g, b) = LightModel.Glow(word, 0.5f);
        Assert.Equal(LanternGlow.Amber.R * 0.5f, r, 4);
        Assert.Equal(LanternGlow.Amber.G * 0.5f, g, 4);
        Assert.Equal(LanternGlow.Amber.B * 0.5f, b, 4);
        Assert.Equal(0f, LightModel.Glow(LightData.Pack(255, 0, 3, LightFace.Up), 1f).R);
        Assert.Equal((0f, 0f, 0f), LightModel.Glow(LightData.Open(LightFace.Fixed), 1f)); // a fixed corner takes no glow
        var half = LightModel.Glow(LightData.Pack(255, LightData.FromLevel(7), 3, LightFace.Up), 1f);
        Assert.Equal(LanternGlow.Amber.R * LightData.FromLevel(7) / 255f, half.R, 4);
    }

    [Fact]
    public void Given_ASunOfTheMoment_When_ACornerIsLit_Then_TheFacesTheOcclusionAndTheSkyEachScaleTheMaterial()
    {
        var noon = SunLightState.Noon;
        var top = LightModel.Lit(LightData.Open(LightFace.Up), noon);
        Assert.Equal(ChunkMesher.SunTint.R, top.R, 4); // the top in the full sun
        var shut = LightModel.Lit(LightData.Pack(255, 0, 0, LightFace.Up), noon);
        Assert.Equal(top.R * ChunkMesher.AoKeepOf(0), shut.R, 4);
        var dark = LightModel.Lit(LightData.Pack(LightData.FromShare(0.28f), 0, 3, LightFace.Up), noon);
        Assert.Equal(top.R * 0.28f, dark.R, 2);
        var dusk = new SunLightState((-1f, 0.13f, 0f), (0.88f, 0.56f, 0.46f));
        Assert.True(LightModel.Lit(LightData.Open(LightFace.West), dusk).R > LightModel.Lit(LightData.Open(LightFace.East), dusk).R, "the side towards a setting sun is the lit one");
        Assert.Equal((1f, 1f, 1f), LightModel.Lit(LightData.Open(LightFace.Fixed), dusk));
        Assert.Equal(0.5f, LightModel.Lit(LightData.Open(LightFace.Flat, 128), new SunLightState(noon.Direction, (1f, 1f, 1f))).G, 2);
    }

    [Fact]
    public void Given_TheFarViewsBakedLand_When_TheSunIsTheDays_Then_ItIsNotRelitAndAtDuskTheSideTowardsTheSunBrightens()
    {
        foreach (var face in Enum.GetValues<BlockFace>())
        {
            var (nx, ny, nz) = ChunkMesher.NormalOf(face);
            var same = LightModel.Relight(nx, ny, nz, ChunkMesher.SunDirection);
            Assert.Equal((1f, 1f, 1f), same);
        }

        var west = LightModel.Relight(-1, 0, 0, (-1f, 0.13f, 0f));
        var top = LightModel.Relight(0, 1, 0, (-1f, 0.13f, 0f));
        Assert.True(west.R >= 1f && top.R < 1f, "the low sun lights its own side and not the tops");
    }

    [Fact]
    public void Given_TheWaterOfAData_When_ItIsBuiltWithAndWithoutTheTint_Then_TheDepthIsInTheVertexAndTheColourIsOnlyTheTransparency()
    {
        var blocks = new MapBlocks().Fill(0, 0, 0, 15, 2, 15, BlockId.Stone);
        for (var x = 4; x < 12; x++)
        {
            for (var z = 4; z < 12; z++)
            {
                for (var y = 3; y <= 8; y++)
                {
                    _ = blocks.Water(x, y, z);
                }
            }
        }

        var baked = ChunkMesher.Build(blocks, 0, 0, 0, 0, new MeshOptions(true, 100, null, LightBand.Day));
        var plain = ChunkMesher.Build(blocks, 0, 0, 0, 0, new MeshOptions(true, 100, null, LightBand.Day, WaterTint: false));
        var data = ChunkMesher.Build(blocks, 0, 0, 0, 0, new MeshOptions(true, 100, null, LightBand.Day, Data: true));
        var d = data.Alpha.Vertices.ToArray();
        var words = data.Alpha.Lights.ToArray();
        Assert.Equal(d.Length, words.Length);
        Assert.All(d, static v => Assert.Equal(0xFFFFFFu, v.Color & 0xFFFFFF)); // white: the transparency is the only material
        var surface = Enumerable.Range(0, d.Length).Where(i => LightData.Flags(words[i]) == LightData.WaveFlag && LightData.Face(words[i]) == LightFace.Flat && LightData.Extra(words[i]) > 0).ToArray();
        Assert.NotEmpty(surface);

        // A corner far from the shore is deep: the depth byte is the tint's input, and the tint at that depth is the baked colour's.
        var deepest = surface.OrderByDescending(i => LightData.Extra(words[i])).First();
        var depth = LightData.Extra(words[deepest]) / 255f * WaterDepthTint.CountDepth;
        Assert.InRange(depth, 5f, 8f);
        var tint = WaterDepthTint.At(depth);
        var bakedMatch = baked.Alpha.Vertices.ToArray().Where(v => v.X == d[deepest].X && v.Y == d[deepest].Y && v.Z == d[deepest].Z && v.U == d[deepest].U && v.V == d[deepest].V).ToArray();
        Assert.NotEmpty(bakedMatch);
        Assert.True(bakedMatch.Any(v => Math.Abs((v.Color & 0xFF) - (255f * tint.R)) <= 1.5f), "the baked surface carries the tint the data's depth gives");
        var plainMatch = plain.Alpha.Vertices.ToArray().Where(v => v.X == d[deepest].X && v.Y == d[deepest].Y && v.Z == d[deepest].Z).ToArray();
        Assert.All(plainMatch, static v => Assert.Equal(255u, v.Color & 0xFF)); // with the tint off the baked surface is the sun alone: white at the day's
    }
}
