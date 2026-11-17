using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>B22, G3: each light band bakes its own sun, so the faces are lit from where the sky's sun stands (the east at dawn, the west at dusk) and the ground darkens as the sun goes down.</summary>
public sealed class BandSunTests
{
    [Fact]
    public void Given_TheBands_When_TheirSunsAreAsked_Then_DawnIsInTheEastDuskInTheWestAndDayIsTheOldDirection()
    {
        var dawn = LightBands.SunDirectionOf(LightBand.Dawn);
        var dusk = LightBands.SunDirectionOf(LightBand.Dusk);

        Assert.True(dawn.X > 0.9f, "the sun rises in the east (x is east)");
        Assert.True(dusk.X < -0.9f, "and sets in the west");
        Assert.InRange(dawn.Y, 0.02f, 0.3f);
        Assert.InRange(dusk.Y, 0.02f, 0.3f);
        Assert.Equal(ChunkMesher.SunDirection, LightBands.SunDirectionOf(LightBand.Day));
        foreach (var band in Enum.GetValues<LightBand>())
        {
            var (x, y, z) = LightBands.SunDirectionOf(band);
            Assert.Equal(1f, MathF.Sqrt((x * x) + (y * y) + (z * z)), 2);
        }
    }

    [Fact]
    public void Given_ADawnAndADusk_When_TheFacesAreShaded_Then_TheSideTowardsTheSunIsLitAndTheOppositeIsNot()
    {
        Assert.Equal(1f, ChunkMesher.SunShare(BlockFace.East, LightBand.Dawn));
        Assert.Equal(0f, ChunkMesher.SunShare(BlockFace.West, LightBand.Dawn));
        Assert.Equal(1f, ChunkMesher.SunShare(BlockFace.West, LightBand.Dusk));
        Assert.Equal(0f, ChunkMesher.SunShare(BlockFace.East, LightBand.Dusk));
        Assert.True(ChunkMesher.FaceShade(BlockFace.East, LightBand.Dawn) > ChunkMesher.FaceShade(BlockFace.West, LightBand.Dawn));
        Assert.True(ChunkMesher.FaceShade(BlockFace.West, LightBand.Dusk) > ChunkMesher.FaceShade(BlockFace.East, LightBand.Dusk));
    }

    [Fact]
    public void Given_ALowSun_When_TheGroundIsShaded_Then_ItIsMuchDarkerThanAtNoonAndTheDaysShadingIsUnchanged()
    {
        var day = ChunkMesher.SunShare(BlockFace.Up, LightBand.Day);
        var dusk = ChunkMesher.SunShare(BlockFace.Up, LightBand.Dusk);

        Assert.Equal(1f, day);
        Assert.True(dusk < 0.3f, "a sun a few degrees up lights the ground a fraction of what the noon's does");
        Assert.True(ChunkMesher.FaceShade(BlockFace.Up, LightBand.Dusk) < 0.8f * ChunkMesher.FaceShade(BlockFace.Up, LightBand.Day));
        Assert.Equal(ChunkMesher.SunShare(BlockFace.West), ChunkMesher.SunShare(BlockFace.West, LightBand.Day)); // the old overload is the Day band's
    }

    [Fact]
    public void Given_ADawnAndADuskPhase_When_TheBandIsChosen_Then_TheirOwnBandsAreUsedBetweenTheDaysAndTheNights()
    {
        Assert.Equal(LightBand.Dawn, LightBands.For(DayPhase.Dawn, 0.5));
        Assert.Equal(LightBand.Dusk, LightBands.For(DayPhase.Dusk, 0.5));
        Assert.Equal(LightBand.Day, LightBands.For(DayPhase.Dawn, 0.95));
        Assert.Equal(LightBand.Night, LightBands.For(DayPhase.Dusk, 0.05));
        Assert.Equal(LightBands.Count, Enum.GetValues<LightBand>().Length);
        Assert.Equal(LightBands.BakedSun(LightBand.Dawn), LightBands.BakedSun(LightBand.Dusk));
    }

    [Fact]
    public void Given_ABlockOnTheGround_When_MeshedAtDawnAndDusk_Then_ItsEastFaceIsBrightAtDawnAndItsWestFaceAtDusk()
    {
        var blocks = new MapBlocks().Set(8, 8, 8, BlockId.Stone);
        double Brightness(LightBand band, float x)
        {
            var mesh = ChunkMesher.Build(blocks, 0, 0, 0, 0, new MeshOptions(false, 100, null, band)).Opaque;
            var face = mesh.Vertices.ToArray().Where(v => v.X == x).ToArray();
            return face.Average(static v => (double)(v.Color & 0xFF));
        }

        Assert.True(Brightness(LightBand.Dawn, 9f) > Brightness(LightBand.Dawn, 8f), "at dawn the east face (x = 9) is the lit one");
        Assert.True(Brightness(LightBand.Dusk, 8f) > Brightness(LightBand.Dusk, 9f), "at dusk the west face (x = 8) is");
    }

    [Fact]
    public void Given_TheDuskLight_When_ItsChannelsAreCompared_Then_ItIsADeepAmberNotAPaleOrange()
    {
        var (r, g, b) = ChunkMesher.DuskLight;

        Assert.True(r > 0.8f);
        Assert.True(g < 0.62f, "the green drops well below the red");
        Assert.True(b < 0.5f, "and the blue most of all");
        Assert.True(b / r < 0.55f);
    }
}
