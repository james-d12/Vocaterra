using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>What a light band bakes into a mesh (E02): the sun's multiplier in every colour that takes the sun's light, the lanterns' strength, and none of it in the self-lit pieces.</summary>
public sealed class ChunkMesherLightBandTests
{
    private static ChunkMeshes Build(MapBlocks blocks, LightBand band, IReadOnlyList<BlockPos>? lights = null) =>
        ChunkMesher.Build(blocks, 0, 0, 0, 0, new MeshOptions(true, 100, lights, band));

    private static double Brightness(MeshBuffer mesh) => mesh.Vertices.ToArray().Average(static v => (double)((v.Color & 0xFF) + ((v.Color >> 8) & 0xFF) + ((v.Color >> 16) & 0xFF)));

    private static MapBlocks Scene() => new MapBlocks().Fill(0, 4, 0, 15, 5, 15, BlockId.Grass)
        .Set(8, 6, 8, BlockId.Fence).Set(3, 6, 3, BlockId.Bush).Set(5, 6, 5, BlockId.TallGrass).Set(10, 6, 4, BlockId.Water);

    [Fact]
    public void Given_TheDayBand_When_TheMeshIsBuilt_Then_ItIsTheMeshOfBeforeTheCycle()
    {
        // The Day band is the old default: the same vertices as a mesh built with no band given.
        var plain = ChunkMesher.Build(Scene(), 0, 0, 0);
        var day = Build(Scene(), LightBand.Day);

        Assert.Equal(plain.Opaque.Vertices.ToArray(), day.Opaque.Vertices.ToArray());
        Assert.Equal(plain.Alpha.Vertices.ToArray(), day.Alpha.Vertices.ToArray());
        Assert.Equal(plain.PlantMesh.Vertices.ToArray(), day.PlantMesh.Vertices.ToArray());
    }

    [Fact]
    public void Given_TheBandsInOrder_When_TheMeshIsBuilt_Then_EveryColourTakingTheSunIsDarkerEachTime()
    {
        var day = Build(Scene(), LightBand.Day);
        var dusk = Build(Scene(), LightBand.Dusk);
        var night = Build(Scene(), LightBand.Night);

        Assert.True(Brightness(dusk.Opaque) < Brightness(day.Opaque));
        Assert.True(Brightness(night.Opaque) < Brightness(dusk.Opaque));
        Assert.True(Brightness(dusk.PlantMesh) < Brightness(day.PlantMesh), "the undergrowth takes the sun's light too");
        Assert.True(Brightness(night.PlantMesh) < Brightness(dusk.PlantMesh));
        Assert.True(Brightness(dusk.Alpha) < Brightness(day.Alpha), "and so does the water");
        Assert.True(Brightness(night.Alpha) < Brightness(dusk.Alpha));
    }

    [Fact]
    public void Given_ABandsBakedLight_When_AFaceInFullSunIsRead_Then_ItIsTheBandsMultiplierTimesTheFaceTint()
    {
        var floor = new MapBlocks().Fill(0, 4, 0, 15, 5, 15, BlockId.Stone);
        double Red(LightBand band) => Build(floor, band).Opaque.Vertices.ToArray().Where(static v => v.Y == 6f).Average(static v => (double)(v.Color & 0xFF));

        Assert.InRange(Math.Abs((Red(LightBand.Day) * ChunkMesher.DuskLight.R) - Red(LightBand.Dusk)), 0, 1); // within the byte's rounding
        Assert.InRange(Math.Abs((Red(LightBand.Day) * ChunkMesher.NightLight.R) - Red(LightBand.Night)), 0, 1);
    }

    [Fact]
    public void Given_ALanternAndABand_When_TheMeshIsBuilt_Then_TheGlassIsInTheSelfLitMeshAtFullBrightnessInEveryBand()
    {
        var blocks = new MapBlocks().Set(5, 5, 5, BlockId.Lantern).Set(5, 8, 5, BlockId.Torch);
        var day = Build(blocks, LightBand.Day);

        foreach (var band in Enum.GetValues<LightBand>())
        {
            var meshes = Build(blocks, band);
            Assert.Equal(6, meshes.Opaque.FaceCount); // a torch's stick only
            Assert.Equal(12, meshes.LitMesh.FaceCount); // a lantern's glass and a torch's flame
            Assert.Equal(day.LitMesh.Vertices.ToArray(), meshes.LitMesh.Vertices.ToArray()); // the band changes nothing in them
        }

        var glass = Build(new MapBlocks().Set(5, 5, 5, BlockId.Lantern), LightBand.Night).LitMesh;
        Assert.All(glass.Vertices.ToArray(), static v => Assert.Equal(0xFFFFFFFFu, v.Color)); // unshaded and unlit by the sun: the light itself
    }

    [Fact]
    public void Given_ALanternAndTheBands_When_TheFaceNextToItIsRead_Then_ItIsWarmedMoreAtDuskAndNightThanByDay()
    {
        var floor = new MapBlocks().Fill(0, 4, 0, 15, 5, 15, BlockId.Stone);
        var lights = new[] { new BlockPos(8, 6, 8) };

        // How much redder than blue the floor beside the lantern is, over the same floor with no lantern in the same band (a lantern's glow adds red and takes blue away).
        double Warming(LightBand band)
        {
            double Warmth(IReadOnlyList<BlockPos>? l) => Build(floor, band, l).Opaque.Vertices.ToArray().Where(static v => v.Y == 6f && v.X is >= 8f and <= 9f && v.Z is >= 8f and <= 9f)
                .Average(static v => (double)(v.Color & 0xFF) / Math.Max(1, (v.Color >> 16) & 0xFF));
            return Warmth(lights) - Warmth(null);
        }

        double day = Warming(LightBand.Day), dusk = Warming(LightBand.Dusk), night = Warming(LightBand.Night);
        Assert.True(day > 0, $"day {day}");
        Assert.True(dusk > day, $"day {day}, dusk {dusk}");
        Assert.True(night >= dusk, $"dusk {dusk}, night {night}");
    }
}
