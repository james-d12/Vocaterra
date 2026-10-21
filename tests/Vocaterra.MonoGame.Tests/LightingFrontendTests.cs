using Microsoft.Xna.Framework;
using Vocaterra.MonoGame;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.MonoGame.Tests;

/// <summary>P7 in the front end: the premultiplied tint that composes brightness with the grey/colour rule, the dark sky of an unlit cave, and the debug steps.</summary>
public sealed class LightingFrontendTests
{
    [Fact]
    public void TheTintIsPremultipliedSoBrightnessAndSaturationCompose()
    {
        var full = TileAtlas.Tint(1.0, 1.0);
        Assert.Equal(new Color(255, 255, 255, 255), full);

        var half = TileAtlas.Tint(0.5, 0.5);
        Assert.InRange(half.A, 127, 128);
        Assert.InRange(half.R, 63, 64);

        var dark = TileAtlas.Tint(1.0, 0.0);
        Assert.Equal(0, dark.R);
        Assert.Equal(255, dark.A);
    }

    [Fact]
    public void AnUnlitAirSquareIsBlackAndAFullyLitOneIsTheSky()
    {
        Assert.Equal(new Color(0, 0, 0, 255), WorldRenderer.Darken(WorldRenderer.Sky, 0.0));
        Assert.Equal(WorldRenderer.Sky, WorldRenderer.Darken(WorldRenderer.Sky, 1.0));
        var half = WorldRenderer.Darken(WorldRenderer.Sky, 0.5);
        Assert.InRange(half.R, 66, 69);
    }

    [Fact]
    public void TheDebugStepsParseInOrderAndNeedAScreenshot()
    {
        var ok = LaunchOptions.TryParse(["--screenshot", "/tmp/x.png", "--teleport", "503,211", "--grant", "carbón:3"], out var options, out var error);
        Assert.True(ok, error);
        Assert.Equal(ScriptStepKind.Teleport, options.Steps[0].Kind);
        Assert.Equal(new TilePoint(503, 211), options.Steps[0].Tile);
        Assert.Equal(ScriptStepKind.Grant, options.Steps[1].Kind);
        Assert.Equal("carbón", options.Steps[1].Item);
        Assert.Equal(3, options.Steps[1].Count);

        Assert.False(LaunchOptions.TryParse(["--teleport", "503,211"], out _, out var needsShot));
        Assert.Contains("--screenshot", needsShot, StringComparison.Ordinal);
        Assert.False(LaunchOptions.TryParse(["--screenshot", "/tmp/x.png", "--grant", "carbón"], out _, out _));
        Assert.False(LaunchOptions.TryParse(["--screenshot", "/tmp/x.png", "--teleport", "a,b"], out _, out _));
    }

    [Fact]
    public void TheNewTileAndToolArtIsDrawnInCodeAndHasTheRightSize()
    {
        foreach (var tile in new[] { TileId.CoalOre, TileId.CopperOre, TileId.IronOre, TileId.GoldOre, TileId.Torch, TileId.TallGrass })
        {
            Assert.Equal(TileArt.Size * TileArt.Size * 4, TileArt.Pixels(tile).Length);
        }

        Assert.Equal(6, TileArt.ToolCount);
        for (var tool = 0; tool < TileArt.ToolCount; tool++)
        {
            Assert.Equal(TileArt.Size * TileArt.Size * 4, TileArt.ToolPixels(tool).Length);
        }
    }
}
