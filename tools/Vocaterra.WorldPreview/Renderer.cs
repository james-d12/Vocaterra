using System.Globalization;
using System.Text;

namespace Vocaterra.WorldPreview;

/// <summary>Turns the sampled columns into pictures: the biome map, the height map and the vertical slice, and the legend of the biome map.</summary>
internal static class Renderer
{
    private static readonly Rgb ShallowSea = new(120, 185, 225);
    private static readonly Rgb DeepSea = new(18, 48, 120);
    private static readonly Rgb Snowfield = new(246, 249, 253);

    // The hypsometric ramp of the land: height in blocks, colour. Lowland green, upland brown, grey stone.
    private static readonly (int Height, Rgb Colour)[] LandRamp =
    [
        (32, new Rgb(196, 200, 140)),
        (34, new Rgb(78, 150, 72)),
        (64, new Rgb(120, 175, 82)),
        (96, new Rgb(176, 172, 96)),
        (128, new Rgb(158, 118, 76)),
        (176, new Rgb(120, 100, 86)),
        (210, new Rgb(140, 140, 142)),
        (280, new Rgb(196, 196, 200)),
    ];

    /// <summary>The biome map: each pixel in its row's colour.</summary>
    public static Image Biomes(WorldMap map, Palette palette)
    {
        var image = new Image(map.Width, map.Height, default);
        for (var py = 0; py < map.Height; py++)
        {
            for (var px = 0; px < map.Width; px++)
            {
                image.Set(px, py, palette.Of(map.Biome[(py * map.Width) + px]));
            }
        }

        return image;
    }

    /// <summary>The height map: sea blue deepening with depth, then the land's ramp, white where snow lies, lit from the north-west so the relief reads.</summary>
    public static Image Heights(WorldMap map, int seaLevel)
    {
        var image = new Image(map.Width, map.Height, default);
        for (var py = 0; py < map.Height; py++)
        {
            for (var px = 0; px < map.Width; px++)
            {
                var i = (py * map.Width) + px;
                var h = map.Ground[i];
                Rgb colour;
                if (map.Water[i])
                {
                    colour = Rgb.Mix(ShallowSea, DeepSea, Math.Clamp((seaLevel - h) / 24.0, 0, 1));
                }
                else
                {
                    colour = map.Snow[i] ? Snowfield : Land(h);
                    colour = colour.Scaled(Shade(map, px, py));
                }

                image.Set(px, py, colour);
            }
        }

        return image;
    }

    /// <summary>
    /// The vertical section along a line: the ground in the surface biome's colour, water blue up to its level, the snow line (white) and the tree line (green) as thin lines, the
    /// sea level dashed, a tick on the left every 64 blocks of height and one along the foot every 1 024 blocks of the line.
    /// </summary>
    public static Image Slice(WorldSampler sampler, int x0, int z0, int x1, int z1, int scale, int zoom)
    {
        var length = Math.Sqrt(((double)(x1 - x0) * (x1 - x0)) + ((double)(z1 - z0) * (z1 - z0)));
        var columns = Math.Max(2, (int)Math.Floor(length / scale) + 1);
        var minY = sampler.MinY;
        var maxY = sampler.MaxY;
        var image = new Image(columns, (maxY - minY + 1) * zoom, new Rgb(28, 32, 44));
        Rgb water = new(54, 110, 200);
        var snowLine = new Rgb(255, 255, 255);
        var treeLine = new Rgb(60, 230, 80);
        var seaMark = new Rgb(255, 214, 64);

        int Row(int y) => (maxY - y) * zoom;

        int? previousSnow = null;
        int? previousTree = null;
        for (var i = 0; i < columns; i++)
        {
            var t = length == 0 ? 0 : Math.Min(1.0, i * (double)scale / length);
            var x = (int)Math.Round(x0 + ((x1 - x0) * t));
            var z = (int)Math.Round(z0 + ((z1 - z0) * t));
            var column = sampler.At(x, z);
            var ground = Math.Clamp(column.Ground, minY, maxY);
            var colour = sampler.Palette.Of(column.Biome);
            image.VerticalRun(i, Row(ground), Row(minY) + zoom - 1, colour.Scaled(0.8));
            image.VerticalRun(i, Row(ground), Row(ground) + Math.Max(0, zoom - 1), colour);
            if (column.Water && column.WaterLevel > ground)
            {
                image.VerticalRun(i, Row(Math.Min(column.WaterLevel, maxY)), Row(ground) - 1, water);
            }

            previousSnow = Line(image, i, column.SnowLine, previousSnow, snowLine, Row, maxY);
            previousTree = Line(image, i, column.TreeLine, previousTree, treeLine, Row, maxY);
        }

        for (var i = 0; i < columns; i++)
        {
            if ((i / 6) % 2 == 0)
            {
                image.Set(i, Row(sampler.SeaLevel), seaMark);
            }
        }

        var white = new Rgb(255, 255, 255);
        for (var y = Math.Max(0, (minY + 63) / 64 * 64); y <= maxY; y += 64)
        {
            for (var k = 0; k < 8; k++)
            {
                image.Set(k, Row(y), white);
            }
        }

        for (var d = 0; d < length; d += 1024)
        {
            var px = (int)(d / scale);
            for (var k = 0; k < 8; k++)
            {
                image.Set(px, image.Height - 1 - k, white);
            }
        }

        return image;
    }

    /// <summary>
    /// The vertical section of the real blocks along a line (B11): every block of the column in its own colour, y from the bottom of the world to the top, so the strata, ores, caves, lava, bedrock
    /// and the entrances show, with sea level dashed yellow, a tick on the left every 64 blocks of height and one along the foot every 1 024 blocks of the line.
    /// </summary>
    public static Image BlockSlice(BlockWorld world, int x0, int z0, int x1, int z1, int scale, int zoom)
    {
        var length = Math.Sqrt(((double)(x1 - x0) * (x1 - x0)) + ((double)(z1 - z0) * (z1 - z0)));
        var columns = Math.Max(2, (int)Math.Floor(length / scale) + 1);
        var (minY, maxY) = (world.MinY, world.MaxY - 1);
        var image = new Image(columns, (maxY - minY + 1) * zoom, new Rgb(28, 32, 44));

        int Row(int y) => (maxY - y) * zoom;

        for (var i = 0; i < columns; i++)
        {
            var t = length == 0 ? 0 : Math.Min(1.0, i * (double)scale / length);
            var x = (int)Math.Round(x0 + ((x1 - x0) * t));
            var z = (int)Math.Round(z0 + ((z1 - z0) * t));
            for (var y = minY; y <= maxY; y++)
            {
                var block = world.At(x, y, z);
                if (block != Vocaterra.Game.Core.Voxel.BlockId.Air)
                {
                    image.VerticalRun(i, Row(y), Row(y) + zoom - 1, BlockColours.Of(block));
                }
            }
        }

        var seaMark = new Rgb(255, 214, 64);
        for (var i = 0; i < columns; i++)
        {
            if ((i / 6) % 2 == 0)
            {
                image.Set(i, Row(world.SeaLevel), seaMark);
            }
        }

        var white = new Rgb(255, 255, 255);
        for (var y = Math.Max(0, (minY + 63) / 64 * 64); y <= maxY; y += 64)
        {
            for (var k = 0; k < 8; k++)
            {
                image.Set(k, Row(y), white);
            }
        }

        for (var d = 0; d < length; d += 1024)
        {
            var px = (int)(d / scale);
            for (var k = 0; k < 8; k++)
            {
                image.Set(px, image.Height - 1 - k, white);
            }
        }

        return image;
    }

    /// <summary>
    /// The map of the cave entrances of a square area (B11), one pixel a block: the ground in grey by height (water blue), and every column whose top block lies at least three blocks under the ground the
    /// height model gives (the column is cut open to a cave) in bright red.
    /// </summary>
    /// <param name="world">The blocks.</param>
    /// <param name="sampler">The height model's ground, to tell a cut column from the surface.</param>
    /// <param name="left">The x of the first pixel.</param>
    /// <param name="top">The z of the first pixel row.</param>
    /// <param name="size">The side in blocks (= pixels).</param>
    /// <param name="count">The number of entrance columns found.</param>
    public static Image Entrances(BlockWorld world, WorldSampler sampler, int left, int top, int size, out int count)
    {
        var image = new Image(size, size, default);
        count = 0;
        for (var py = 0; py < size; py++)
        {
            for (var px = 0; px < size; px++)
            {
                var (x, z) = (left + px, top + py);
                var ground = sampler.At(x, z);
                var topSolid = world.TopSolid(x, z);
                var grey = (byte)Math.Clamp(60 + (Math.Max(ground.Ground, 0) * 150 / 200), 0, 255);
                Rgb colour = ground.Water ? new Rgb(40, 80, 150) : new Rgb(grey, grey, grey);
                if (!ground.Water && topSolid <= ground.Ground - 3)
                {
                    colour = new Rgb(255, 40, 40);
                    count++;
                }

                image.Set(px, py, colour);
            }
        }

        return image;
    }

    /// <summary>The legend of the biome map as text: each row's id, colour and share of the area, in file order (rows with no column are listed with 0).</summary>
    public static string Legend(WorldSampler sampler, double[] shares)
    {
        var text = new StringBuilder();
        text.AppendLine("id\tcolour\tshare");
        for (var i = 0; i < sampler.Rows.Count; i++)
        {
            text.Append(sampler.Rows[i].Id).Append('\t').Append(sampler.Palette.Of(i).Hex()).Append('\t')
                .AppendLine((shares[i] * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%");
        }

        return text.ToString();
    }

    /// <summary>
    /// The height map's numbers as text: the lowest and highest ground and where the highest is, the share of the columns under water, and the share of the land above 100, 150, 200
    /// and 250 blocks (to read the ranges' height without the picture).
    /// </summary>
    public static string HeightSummary(WorldMap map)
    {
        var text = new StringBuilder();
        var land = 0L;
        var water = 0L;
        var min = int.MaxValue;
        var max = int.MinValue;
        var at = 0;
        int[] marks = [100, 150, 200, 250];
        var above = new long[marks.Length];
        for (var i = 0; i < map.Ground.Length; i++)
        {
            var h = map.Ground[i];
            min = Math.Min(min, h);
            if (h > max)
            {
                max = h;
                at = i;
            }

            if (map.Water[i])
            {
                water++;
                continue;
            }

            land++;
            for (var k = 0; k < marks.Length; k++)
            {
                if (h > marks[k])
                {
                    above[k]++;
                }
            }
        }

        var inv = CultureInfo.InvariantCulture;
        text.AppendLine(string.Create(inv, $"ground: lowest {min}, highest {max} at x {map.BlockX(at % map.Width)}, z {map.BlockZ(at / map.Width)}"));
        text.AppendLine(string.Create(inv, $"under water: {100.0 * water / map.Ground.Length:0.00}% of the columns"));
        for (var k = 0; k < marks.Length; k++)
        {
            text.AppendLine(string.Create(inv, $"land above {marks[k]}: {(land == 0 ? 0 : 100.0 * above[k] / land):0.00}% of the land"));
        }

        return text.ToString();
    }

    private static int? Line(Image image, int x, int? y, int? previous, Rgb colour, Func<int, int> row, int maxY)
    {
        if (y is not { } line || line > maxY)
        {
            return null;
        }

        var r = row(line);
        image.Set(x, r, colour);
        if (previous is { } p)
        {
            image.VerticalRun(x, r, row(p), colour);
        }

        return line;
    }

    private static Rgb Land(int height)
    {
        if (height <= LandRamp[0].Height)
        {
            return LandRamp[0].Colour;
        }

        for (var i = 1; i < LandRamp.Length; i++)
        {
            if (height <= LandRamp[i].Height)
            {
                var (h0, c0) = LandRamp[i - 1];
                var (h1, c1) = LandRamp[i];
                return Rgb.Mix(c0, c1, (height - h0) / (double)(h1 - h0));
            }
        }

        return LandRamp[^1].Colour;
    }

    // The light from the north-west: the ground falling away to the south-east is lit, rising to it is dark. One pixel's rise is judged against the blocks it spans.
    private static double Shade(WorldMap map, int px, int py)
    {
        int At(int x, int y) => map.Ground[(Math.Clamp(y, 0, map.Height - 1) * map.Width) + Math.Clamp(x, 0, map.Width - 1)];
        var fall = At(px - 1, py - 1) - At(px + 1, py + 1);
        return Math.Clamp(1.0 + (fall * 0.5 / (2 * map.Scale)), 0.6, 1.35);
    }
}
