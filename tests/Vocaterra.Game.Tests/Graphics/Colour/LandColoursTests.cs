using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Colour;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Tests.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.Colour;

/// <summary>B22, G2: grass and oak leaves take their colour from a climate map (data), the chunk mesher and the far view read the same map, and the data is validated.</summary>
public sealed class LandColoursTests
{
    private static readonly (byte R, byte G, byte B) GrassArt = FarPalette.Of(BlockId.Grass);
    private static readonly (byte R, byte G, byte B) LeafArt = FarPalette.Of(BlockId.Leaves);

    private static string Embedded()
    {
        using var stream = typeof(LandColours).Assembly.GetManifestResourceStream("Vocaterra.Game.Graphics.Colour.land-colours.json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static double Saturation((byte R, byte G, byte B) c)
    {
        var max = Math.Max(c.R, Math.Max(c.G, c.B));
        var min = Math.Min(c.R, Math.Min(c.G, c.B));
        return max == 0 ? 0 : (max - min) / (double)max;
    }

    [Fact]
    public void Given_TheShippedData_When_ItIsValidated_Then_NothingIsWrongWithIt()
    {
        Assert.Empty(LandColours.Validate(Embedded(), GrassArt, LeafArt));
        Assert.NotNull(LandColours.Default);
    }

    [Fact]
    public void Given_TheMapsNodes_When_TheColourIsAsked_Then_ItIsTheDataColourOfThatNode()
    {
        var map = LandColours.Default;

        Assert.Equal(((byte)0x96, (byte)0x9e, (byte)0x80), map.GrassColour(-4096, -4096)); // the cold, dry corner
        Assert.Equal(((byte)0x54, (byte)0x9c, (byte)0x38), map.GrassColour(4096, 4096)); // the hot, wet corner
        Assert.Equal(((byte)0x6a, (byte)0xa6, (byte)0x46), map.GrassColour(0, 0));
        Assert.Equal(map.GrassColour(-4096, 0), map.GrassColour(-9999, 0)); // outside the nodes: the edge's colour
        Assert.Equal(map.LeafColour(4096, -4096), map.LeafColour(99999, -99999));
    }

    [Fact]
    public void Given_APointBetweenNodes_When_TheColourIsAsked_Then_ItIsBetweenTheirColoursChannelByChannel()
    {
        var map = LandColours.Default;
        var (r0, g0, b0) = map.GrassColour(0, 0);
        var (r1, g1, b1) = map.GrassColour(2048, 0);
        var (r, g, b) = map.GrassColour(1024, 0);

        Assert.InRange(r, Math.Min(r0, r1), Math.Max(r0, r1));
        Assert.InRange(g, Math.Min(g0, g1), Math.Max(g0, g1));
        Assert.InRange(b, Math.Min(b0, b1), Math.Max(b0, b1));
        Assert.Equal((r0 + r1) / 2.0, r, 1.0);
    }

    [Fact]
    public void Given_TheClimates_When_TheGreensAreCompared_Then_TheLandVariesAndIsLessSaturatedThanTheOldLime()
    {
        var map = LandColours.Default;
        var temperate = map.GrassColour(0, 0);
        var hotDry = map.GrassColour(3000, -3000);
        var coldWet = map.GrassColour(-3000, 3000);

        Assert.True(hotDry.R > temperate.R + 20, "dry hot grass is yellower");
        Assert.True(coldWet.R < temperate.R && coldWet.B > temperate.B, "cold wet grass is bluer");
        Assert.True(Saturation(temperate) < 0.6, "the old lime was 0.73 saturated");
        foreach (var t in new[] { -4096, -2048, 0, 2048, 4096 })
        {
            foreach (var h in new[] { -4096, -2048, 0, 2048, 4096 })
            {
                Assert.True(Saturation(map.GrassColour(t, h)) < 0.7, $"grass at {t},{h}: under the old lime's 0.73");
                Assert.True(map.LeafColour(t, h).G > map.LeafColour(t, h).B, "leaves stay green");
            }
        }
    }

    [Fact]
    public void Given_TheTints_When_TheyAreRead_Then_NoChannelIsAboveOneAndTheArtTimesTheTintIsTheDataColour()
    {
        var map = LandColours.Default;
        foreach (var (t, h) in new[] { (0, 0), (-3000, 2000), (3500, -3500), (-4096, 4096) })
        {
            var tint = map.GrassTint(t, h);
            Assert.InRange(tint.R, 0f, 1f);
            Assert.InRange(tint.G, 0f, 1f);
            Assert.InRange(tint.B, 0f, 1f);
            var colour = map.GrassColour(t, h);
            Assert.Equal(colour.G, GrassArt.G * tint.G, 1.5);
        }
    }

    [Fact]
    public void Given_BadData_When_ItIsValidated_Then_EveryProblemIsNamed()
    {
        const string Grid = "[[\"#808080\",\"#808080\"],[\"#808080\",\"#808080\"]]";
        string Make(string nodes, string grass) => "{\"temperatureNodes\":" + nodes + ",\"humidityNodes\":" + nodes + ",\"grass\":" + grass + ",\"leaves\":" + Grid + "}";

        Assert.Empty(LandColours.Validate(Make("[-100,100]", Grid), (200, 200, 200), (200, 200, 200)));
        Assert.Contains(LandColours.Validate(Make("[100,-100]", Grid), (200, 200, 200), (200, 200, 200)), static p => p.Contains("must rise", StringComparison.Ordinal));
        Assert.Contains(LandColours.Validate(Make("[-5000,100]", Grid), (200, 200, 200), (200, 200, 200)), static p => p.Contains("outside", StringComparison.Ordinal));
        Assert.Contains(LandColours.Validate(Make("[-100,100]", "[[\"#808080\"],[\"#808080\"]]"), (200, 200, 200), (200, 200, 200)), static p => p.Contains("row 0 needs 2", StringComparison.Ordinal));
        Assert.Contains(LandColours.Validate(Make("[-100,100]", "[[\"#808080\",\"#808080\"]]"), (200, 200, 200), (200, 200, 200)), static p => p.Contains("needs 2 rows", StringComparison.Ordinal));
        Assert.Contains(LandColours.Validate(Make("[-100,100]", "[[\"red\",\"#808080\"],[\"#808080\",\"#808080\"]]"), (200, 200, 200), (200, 200, 200)), static p => p.Contains("not #rrggbb", StringComparison.Ordinal));
        Assert.Contains(LandColours.Validate(Make("[-100,100]", Grid), (100, 200, 200), (200, 200, 200)), static p => p.Contains("lighter than the block art", StringComparison.Ordinal));
        Assert.NotEmpty(LandColours.Validate("{", GrassArt, LeafArt));
        Assert.Throws<InvalidDataException>(() => LandColours.Parse(Make("[100,-100]", Grid), (200, 200, 200), (200, 200, 200)));
    }

    // The red of the first grass-top vertex of a floor of grass, in a given climate.
    private static (double R, double G, double B) TopColour(Func<int, int, int, (int Temperature, int Humidity)>? ground)
    {
        var blocks = new MapBlocks().Fill(0, 4, 0, 15, 4, 15, BlockId.Grass);
        var mesh = ChunkMesher.Build(blocks, 0, 0, 0, 0, new MeshOptions(false, 100, null, LightBand.Day, null, ground)).Opaque;
        var top = mesh.Vertices.ToArray().Where(static v => v.Y == 5f).ToArray();
        return (top.Average(static v => (double)(v.Color & 0xFF)), top.Average(static v => (double)((v.Color >> 8) & 0xFF)), top.Average(static v => (double)((v.Color >> 16) & 0xFF)));
    }

    [Fact]
    public void Given_ADryHotAndAWetColdColumn_When_GrassIsMeshed_Then_ItsVertexColourFollowsTheClimateAndNoOtherBlockDoes()
    {
        var hot = TopColour(static (_, _, _) => (3500, -3500));
        var cold = TopColour(static (_, _, _) => (-3500, 3500));
        var none = TopColour(null);

        Assert.True(hot.R > cold.R + 30, "yellow-green against blue-green");
        Assert.True(cold.B > hot.B);
        Assert.Equal(none, TopColour(static (_, _, _) => (LandColours.NeutralTemperature, LandColours.NeutralHumidity))); // no climate is the map's middle

        double StoneRed(Func<int, int, int, (int, int)>? g) => ChunkMesher.Build(new MapBlocks().Fill(0, 4, 0, 15, 4, 15, BlockId.Stone), 0, 0, 0, 0, new MeshOptions(false, 100, null, LightBand.Day, null, g)).Opaque.Vertices.ToArray().Average(static v => (double)(v.Color & 0xFF));
        Assert.Equal(StoneRed(null), StoneRed(static (_, _, _) => (3500, -3500)));
    }

    [Fact]
    public void Given_AColumnsGroundHeight_When_TheClimateIsAsked_Then_TheChunkPassesTheBlocksOwnPosition()
    {
        var asked = new List<(int X, int Y, int Z)>();
        var blocks = new MapBlocks().Set(3, 7, 9, BlockId.Grass);
        _ = ChunkMesher.Build(blocks, 0, 0, 0, 0, new MeshOptions(false, 100, null, LightBand.Day, null, (x, y, z) =>
        {
            asked.Add((x, y, z));
            return (0, 0);
        }));

        Assert.Equal([(3, 7, 9)], asked);
    }

    [Fact]
    public void Given_TheFarViewAndTheNear_When_TheSameColumnIsTinted_Then_BothTakeTheMapsColourAndOnlyGrassAndLeavesAreTinted()
    {
        Func<int, int, int, (int Temperature, int Humidity)> ground = static (_, _, _) => (3456, -3456);
        var near = LandColours.Default.GrassTint(3456, -3456); // the far view rounds the climate to its 128 step
        var far = FarPalette.LandTint(BlockId.Grass, false, ground, 100, 40, 100);

        Assert.Equal(near.R, far.R, 3);
        Assert.Equal(near.G, far.G, 3);
        Assert.Equal(near.B, far.B, 3);
        Assert.Equal(LandColours.Default.LeafTint(3456, -3456), FarPalette.LandTint(BlockId.Leaves, false, ground, 100, 40, 100));
        Assert.True(FarPalette.LandTint(BlockId.Grass, true, ground, 0, 0, 0).R > far.R, "a grass block's side takes part of the tint");
        Assert.Equal((1f, 1f, 1f), FarPalette.LandTint(BlockId.Stone, false, ground, 0, 0, 0));
        Assert.Equal((1f, 1f, 1f), FarPalette.LandTint(BlockId.Sand, true, ground, 0, 0, 0));
    }

    [Fact]
    public void Given_TheFarTintsOfNearbyColumns_When_TheyAreRead_Then_TheClimateIsRoundedSoRunsOfFacesKeepOneColour()
    {
        var calls = 0;
        Func<int, int, int, (int Temperature, int Humidity)> ground = (x, _, _) =>
        {
            calls++;
            return (x * 4, 0); // changes 4 units a block
        };

        var a = FarPalette.LandTint(BlockId.Grass, false, ground, 100, 30, 100);
        var b = FarPalette.LandTint(BlockId.Grass, false, ground, 101, 30, 100);

        Assert.Equal(a, b);
        Assert.Equal(1, calls); // one cell of 16 blocks is asked once
    }
}
