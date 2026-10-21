using System.Globalization;
using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.MonoGame.Tests;

public sealed class OverlayAndArtTests
{
    [Fact]
    public void TheOverlayShowsFpsTickSeedPlayerTileAndTheSpanishLine()
    {
        var lines = DebugText.Lines(59.6, 1234, new TilePoint(521, 182), 42, null);
        var text = string.Join('\n', lines);
        Assert.Contains("FPS 60", text, StringComparison.Ordinal);
        Assert.Contains("tick 1234", text, StringComparison.Ordinal);
        Assert.Contains("seed 42", text, StringComparison.Ordinal);
        Assert.Contains("(521, 182)", text, StringComparison.Ordinal);
        Assert.Contains("mouse tile -", text, StringComparison.Ordinal);
        Assert.Contains("¿Dónde está el árbol? ¡Mañana!", lines);
    }

    [Fact]
    public void TheMouseTileIsShownWhenThePointerIsInTheWindow()
    {
        var text = string.Join('\n', DebugText.Lines(60, 0, new TilePoint(0, 0), 1, new TilePoint(-3, 7)));
        Assert.Contains("mouse tile (-3, 7)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNumbersIgnoreTheCurrentCulture()
    {
        var old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("es-ES");
            var text = string.Join('\n', DebugText.Lines(1234.4, 1234567, new TilePoint(1000, 2), 99, null));
            Assert.Contains("tick 1234567", text, StringComparison.Ordinal);
            Assert.Contains("FPS 1234", text, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = old;
        }
    }

    [Fact]
    public void FpsIsMeasuredOverAWindowOfRealTime()
    {
        var stats = new FrameStats(0.5);
        Assert.Equal(0.0, stats.Fps);
        for (var i = 0; i < 40; i++)
        {
            stats.Record(1.0 / 60);
        }

        Assert.Equal(60.0, stats.Fps, 3);
        stats.Record(double.NaN);
        stats.Record(-1);
        Assert.Equal(60.0, stats.Fps, 3);
    }

    [Fact]
    public void EveryTileIdHasArtOfTheRightSize()
    {
        Assert.Equal(Enum.GetValues<TileId>().Length, TileArt.TileCount);
        foreach (var id in Enum.GetValues<TileId>())
        {
            Assert.Equal(TileArt.Size * TileArt.Size * 4, TileArt.Pixels(id).Length);
        }
    }

    [Fact]
    public void AirIsTransparentAndSolidTilesAreOpaque()
    {
        Assert.All(Alphas(TileId.Air), a => Assert.Equal(0, a));
        foreach (var id in new[] { TileId.Dirt, TileId.Grass, TileId.Stone })
        {
            Assert.All(Alphas(id), a => Assert.Equal(255, a));
        }
    }

    [Fact]
    public void TreeTilesLetTheSkyShowThrough()
    {
        Assert.Contains(0, Alphas(TileId.Trunk));
        Assert.Contains(255, Alphas(TileId.Trunk));
        Assert.Contains(0, Alphas(TileId.Leaves));
        Assert.Contains(255, Alphas(TileId.Leaves));
    }

    [Fact]
    public void EveryVisibleTileLooksDifferentAndTheArtIsDeterministic()
    {
        var visible = Enum.GetValues<TileId>().Where(id => id != TileId.Air).ToList();
        var distinct = visible.Select(id => Convert.ToHexString(TileArt.Pixels(id))).Distinct().Count();
        Assert.Equal(visible.Count, distinct);
        foreach (var id in visible)
        {
            Assert.Equal(TileArt.Pixels(id), TileArt.Pixels(id));
        }
    }

    private static List<int> Alphas(TileId id)
    {
        var pixels = TileArt.Pixels(id);
        var alphas = new List<int>();
        for (var i = 3; i < pixels.Length; i += 4)
        {
            alphas.Add(pixels[i]);
        }

        return alphas;
    }
}
