using Vocaterra.MonoGame.Frontend;
using Vocaterra.Simulation.Physics;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.MonoGame.Tests;

public sealed class CameraTests
{
    private const int U = PhysicsConstants.UnitsPerTile;
    private const int Ppt = 32;

    private static Camera Make(int worldW = 1024, int worldH = 512, int viewW = 1280, int viewH = 720)
    {
        var camera = new Camera(worldW, worldH, Ppt);
        camera.Resize(viewW, viewH);
        return camera;
    }

    [Fact]
    public void ThePlayerIsCentredInTheMiddleOfTheWorld()
    {
        var camera = Make();
        camera.Follow(512L * U, 256L * U);
        Assert.Equal((512 * Ppt) - 640, camera.OriginX);
        Assert.Equal((256 * Ppt) - 360, camera.OriginY);
        Assert.Equal((640, 360), camera.UnitsToScreen(512L * U, 256L * U));
    }

    [Fact]
    public void TheViewClampsAtEveryEdgeOfTheWorld()
    {
        var camera = Make();
        camera.Follow(0, 0);
        Assert.Equal((0, 0), (camera.OriginX, camera.OriginY));
        camera.Follow(1024L * U, 512L * U);
        Assert.Equal((1024 * Ppt) - 1280, camera.OriginX);
        Assert.Equal((512 * Ppt) - 720, camera.OriginY);
        camera.Follow(-50L * U, -50L * U);
        Assert.Equal((0, 0), (camera.OriginX, camera.OriginY));
        camera.Follow(5000L * U, 5000L * U);
        Assert.Equal((1024 * Ppt) - 1280, camera.OriginX);
    }

    [Fact]
    public void AWorldSmallerThanTheViewportIsCentred()
    {
        var camera = Make(worldW: 32, worldH: 32, viewW: 1280, viewH: 720); // 1024 x 1024 px world
        camera.Follow(16L * U, 16L * U);
        Assert.Equal(-128, camera.OriginX); // (1024 - 1280) / 2
        Assert.Equal(152, camera.OriginY); // clamped: 1024 - 720 = 304 of room, centre on the player
        camera.Resize(2048, 2048);
        camera.Follow(0, 0);
        Assert.Equal(-512, camera.OriginX);
        Assert.Equal(-512, camera.OriginY);
        var tiles = camera.VisibleTiles();
        Assert.Equal(new TileRange(0, 0, 32, 32), tiles);
    }

    [Fact]
    public void OnlyTilesThatTouchTheViewportAreVisible()
    {
        var camera = Make();
        camera.Follow(512L * U, 256L * U);
        var range = camera.VisibleTiles();
        Assert.Equal(512 - 20, range.MinX);
        Assert.Equal(512 + 20, range.MaxX);
        Assert.Equal(256 - 12, range.MinY); // 360 / 32 = 11.25, so the top edge (row 244.75) lies inside row 244
        Assert.Equal(256 + 12, range.MaxY);
        Assert.True(range.Width * range.Height < 60 * 30, "Far fewer tiles than the 1024 x 512 world.");
    }

    [Fact]
    public void ASubTileOffsetIncludesTheTileThatIsPartlyVisible()
    {
        var camera = Make();
        camera.Follow((512L * U) + (U / 2), (256L * U) + (U / 4)); // moves the origin by 16 px and 8 px
        var range = camera.VisibleTiles();
        Assert.Equal((512 * Ppt) + 16 - 640, camera.OriginX);
        Assert.Equal(512 - 20, range.MinX); // origin 15 904 / 32 = 497.0 exactly
        Assert.Equal(512 + 21, range.MaxX); // the right edge falls inside tile 532
        var (sx, _) = camera.TileToScreen(range.MinX, range.MinY);
        Assert.InRange(sx, -Ppt + 1, 0);
    }

    [Fact]
    public void VisibleTilesNeverLeaveTheWorld()
    {
        var camera = Make();
        foreach (var (x, y) in new[] { (0, 0), (1024, 512), (0, 512), (1024, 0), (-100, 900) })
        {
            camera.Follow((long)x * U, (long)y * U);
            var range = camera.VisibleTiles();
            Assert.InRange(range.MinX, 0, 1024);
            Assert.InRange(range.MaxX, 0, 1024);
            Assert.InRange(range.MinY, 0, 512);
            Assert.InRange(range.MaxY, 0, 512);
            Assert.False(range.IsEmpty);
        }
    }

    [Fact]
    public void VisibleChunksCoverExactlyTheVisibleTiles()
    {
        var camera = Make();
        camera.Follow(512L * U, 256L * U);
        var tiles = camera.VisibleTiles();
        var chunks = camera.VisibleChunks();
        Assert.Equal(tiles.MinX / Chunk.Size, chunks.MinX);
        Assert.Equal(((tiles.MaxX - 1) / Chunk.Size) + 1, chunks.MaxX);
        Assert.Equal(tiles.MinY / Chunk.Size, chunks.MinY);
        Assert.Equal(((tiles.MaxY - 1) / Chunk.Size) + 1, chunks.MaxY);
        Assert.True(chunks.Width * chunks.Height <= 5 * 3, "A 1280 x 720 view at 32 px touches at most 5 x 3 chunks of 32 tiles here.");
    }

    [Fact]
    public void ScreenToTileInvertsTileToScreen()
    {
        var camera = Make();
        camera.Follow((300L * U) + 1234, (200L * U) + 99);
        foreach (var (tx, ty) in new[] { (290, 195), (300, 200), (315, 210) })
        {
            var (sx, sy) = camera.TileToScreen(tx, ty);
            Assert.Equal(new TilePoint(tx, ty), camera.ScreenToTile(sx, sy));
            Assert.Equal(new TilePoint(tx, ty), camera.ScreenToTile(sx + Ppt - 1, sy + Ppt - 1));
            Assert.Equal(new TilePoint(tx + 1, ty), camera.ScreenToTile(sx + Ppt, sy));
            Assert.Equal(new TilePoint(tx - 1, ty), camera.ScreenToTile(sx - 1, sy));
        }
    }

    [Fact]
    public void ScreenToTileFloorsOutsideTheWorld()
    {
        var camera = Make();
        camera.Follow(0, 0);
        Assert.Equal(new TilePoint(-1, -1), camera.ScreenToTile(-1, -1));
        Assert.Equal(new TilePoint(0, 0), camera.ScreenToTile(0, 0));
    }

    [Fact]
    public void AnObjectOnATilePositionDrawsOnThatTilesPixels()
    {
        var camera = Make();
        camera.Follow(512L * U, 256L * U);
        Assert.Equal(camera.TileToScreen(500, 250), camera.UnitsToScreen(500L * U, 250L * U));
    }

    [Fact]
    public void ABadSizeIsRejectedAndAViewportOfZeroBecomesOnePixel()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Camera(0, 10, 16));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Camera(10, 10, 0));
        var camera = Make();
        camera.Resize(0, 0);
        Assert.Equal((1, 1), (camera.ViewportWidth, camera.ViewportHeight));
    }
}
