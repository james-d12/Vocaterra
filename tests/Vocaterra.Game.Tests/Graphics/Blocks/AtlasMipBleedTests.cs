using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Blocks;

namespace Vocaterra.Game.Tests.Graphics.Blocks;

/// <summary>
/// B17c's choice about the texture array: the atlas stays one texture, because the bleeding between cells that an array would remove does not occur where the atlas is sampled. The mips are made cell by cell up to the level at which a texel is a whole
/// cell (a cell is 16 texels wide: level 4), the far view's sampler stops at that level, and the chunks, which are at most a view distance away, are drawn at a level well under it (a block is at least two pixels wide there).
/// </summary>
public sealed class AtlasMipBleedTests
{
    private static byte[] WithCellChanged(byte[] atlas, BlockCell cell)
    {
        var copy = (byte[])atlas.Clone();
        var (x, y, w, h) = BlockAtlasLayout.PixelRectangle(cell);
        for (var py = y; py < y + h; py++)
        {
            for (var px = x; px < x + w; px++)
            {
                var i = ((py * BlockAtlasLayout.Width) + px) * 4;
                copy[i] = (byte)(255 - copy[i]);
                copy[i + 1] = (byte)(255 - copy[i + 1]);
                copy[i + 2] = (byte)(255 - copy[i + 2]);
                copy[i + 3] = 255;
            }
        }

        return copy;
    }

    [Fact]
    public void Given_ACellChanged_When_TheMipLevelsAreMade_Then_NoOtherCellChangesUpToTheLevelWhereATexelIsAWholeCell()
    {
        var level0 = BlockArt.AtlasPixels();
        var cell = BlockCell.GrassTop;
        var changed = WithCellChanged(level0, cell);
        var cellLevel = (int)Math.Log2(BlockAtlasLayout.CellSize);
        var (cx, cy, cw, ch) = BlockAtlasLayout.PixelRectangle(cell);
        for (var level = 0; level <= cellLevel; level++)
        {
            var a = BlockArt.MipLevel(level0, level);
            var b = BlockArt.MipLevel(changed, level);
            var (width, _) = BlockArt.MipSize(level);
            var inside = 0;
            for (var i = 0; i < a.Length; i += 4)
            {
                var texel = i / 4;
                var x = (texel % width) << level;
                var y = (texel / width) << level;
                var isInCell = x >= cx && x < cx + cw && y >= cy && y < cy + ch;
                if (isInCell)
                {
                    inside++;
                    continue;
                }

                Assert.True(a[i] == b[i] && a[i + 1] == b[i + 1] && a[i + 2] == b[i + 2] && a[i + 3] == b[i + 3], $"level {level}: a texel outside the cell changed with it (the cell's colours bled into its neighbour)");
            }

            Assert.True(inside > 0);
        }

        // One level further a texel spans two cells, which is where an array would have mattered: the far view's sampler is held at the cell's level.
        Assert.Equal(4, cellLevel);
    }

    [Fact]
    public void Given_TheFarViewsSamplerAndTheChunksView_When_TheLevelsAreCounted_Then_NeitherSamplesPastTheCellLevel()
    {
        var far = File.ReadAllText(Repo.Path("src", "Vocaterra.Game", "Graphics", "Far", "FarTerrainRenderer.cs"));
        Assert.Contains("MaxMipLevel = (int)Math.Log2(Blocks.BlockAtlasLayout.CellSize)", far, StringComparison.Ordinal);
        var pass = File.ReadAllText(Repo.Path("src", "Vocaterra.Game", "Graphics", "Far", "FarTerrainRenderer.cs"));
        Assert.Contains("pass.TryAddFarTextures(_atlas, _noise, (int)Math.Log2(Blocks.BlockAtlasLayout.CellSize))", pass, StringComparison.Ordinal);

        // A chunk is drawn at most a view distance away (the High preset's 400 blocks): a block is 1 / (2 tan(fov / 2) * distance / width) pixels wide, a texel a sixteenth of that; the level is log2 of the texels a pixel covers.
        foreach (var width in new[] { 1280, 2560, 3840 })
        {
            var blockPixels = width / (2 * Math.Tan(35 * Math.PI / 180) * 400);
            var level = Math.Log2(BlockAtlasLayout.CellSize / blockPixels);
            Assert.True(level < 4, $"{width} wide: the level at 400 blocks is {level:0.0}");
        }
    }
}
