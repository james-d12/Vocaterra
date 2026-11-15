using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Generation.Procedural;

namespace Vocaterra.WorldPreview.Tests;

public sealed class BundleFixture
{
    public BundleFixture() => Data = Cli.Load(Repo.Path("content", "game-v1"));

    public GameData Data { get; }
}

public sealed class WorldPreviewTests : IClassFixture<BundleFixture>, IDisposable
{
    private readonly GameData _data;
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "worldpreview-" + Guid.NewGuid().ToString("N"));

    public WorldPreviewTests(BundleFixture fixture) => _data = fixture.Data;

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, true);
        }
    }

    private int Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Cli.Run(args, output, error);
        Assert.True(code == 0, error.ToString());
        return code;
    }

    [Fact]
    public void The_same_arguments_write_byte_identical_files()
    {
        var a = Path.Combine(_folder, "a");
        var b = Path.Combine(_folder, "b");
        foreach (var folder in new[] { a, b })
        {
            Run("maps", "--seed", "7", "--x", "300", "--z", "-500", "--size", "512", "--scale", "4", "--out", folder);
            Run("slice", "--seed", "7", "--x0", "-256", "--z0", "0", "--x1", "256", "--z1", "100", "--scale", "4", "--out", folder);
        }

        var names = Directory.GetFiles(a).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(5, names.Length); // the biome map and its legend, the height map and its stats, the slice
        Assert.Equal(names, Directory.GetFiles(b).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray());
        foreach (var name in names)
        {
            Assert.Equal(File.ReadAllBytes(Path.Combine(a, name!)), File.ReadAllBytes(Path.Combine(b, name!)));
        }
    }

    [Fact]
    public void The_fill_path_and_the_one_column_path_draw_the_same_map()
    {
        var sampler = new WorldSampler(_data, 42);
        var map = sampler.Map(-1000, 2000, 512, 4); // scale 4 fills; check every pixel against At
        for (var py = 0; py < map.Height; py += 5)
        {
            for (var px = 0; px < map.Width; px += 3)
            {
                var column = sampler.At(map.BlockX(px), map.BlockZ(py));
                var i = (py * map.Width) + px;
                Assert.Equal(column.Biome, map.Biome[i]);
                Assert.Equal(column.Ground, map.Ground[i]);
                Assert.Equal(column.Water, map.Water[i]);
            }
        }
    }

    [Theory]
    [InlineData(42UL, 4)]
    [InlineData(2026UL, 16)]
    public void A_biome_maps_colours_match_the_biome_tables_classification(ulong seed, int scale)
    {
        Run("biomes", "--seed", seed.ToString(System.Globalization.CultureInfo.InvariantCulture), "--x", "100", "--z", "-300", "--size", "1024", "--scale", scale.ToString(System.Globalization.CultureInfo.InvariantCulture), "--out", _folder);
        var file = Directory.GetFiles(_folder, "biomes-*.png").Single();
        var image = PngDecoder.Decode(File.ReadAllBytes(file));
        Assert.Equal(1024 / scale, image.Width);

        // The model as the generator reads it, built here by hand and not through the tool's sampler.
        var world = _data.WorldGen;
        var climate = new ClimateSampler(world, seed);
        var height = new HeightModel(world, seed);
        var table = new BiomeTable(_data.Biomes, world, seed);
        var palette = new Palette(table.Rows);
        var left = 100 - 512;
        var top = -300 - 512;
        var seen = new HashSet<int>();
        for (var py = 0; py < image.Height; py += 7)
        {
            for (var px = 0; px < image.Width; px += 5)
            {
                var x = left + (px * scale);
                var z = top + (py * scale);
                var c = climate.At(x, z);
                var row = table.Classify(c, height.Shape(x, z, c).Height, x, z).Row;
                seen.Add(row.Index);
                Assert.Equal(palette.Of(row), PngDecoder.Pixel(image, px, py));
            }
        }

        Assert.True(seen.Count >= 2, "the sampled area should cross more than one biome");
    }

    [Fact]
    public void The_legend_lists_every_row_and_the_shares_add_up_to_one()
    {
        var sampler = new WorldSampler(_data, 42);
        var shares = sampler.Shares(sampler.Map(0, 0, 512, 4));

        Assert.Equal(sampler.Rows.Count, shares.Length);
        Assert.Equal(1.0, shares.Sum(), 9);
        var legend = Renderer.Legend(sampler, shares).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(sampler.Rows.Count + 1, legend.Length);
        foreach (var row in sampler.Rows)
        {
            Assert.Contains(legend, line => line.StartsWith(row.Id + "\t#", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Every_biome_row_has_its_own_colour_and_water_rows_are_blue()
    {
        var table = new BiomeTable(_data.Biomes, _data.WorldGen, 1);
        var palette = new Palette(table.Rows);

        Assert.Equal(table.Rows.Count, table.Rows.Select(palette.Of).Distinct().Count());
        foreach (var row in table.Rows.Where(static r => r.When.Water == true))
        {
            var colour = palette.Of(row);
            Assert.True(colour.B > colour.R && colour.B > colour.G, $"{row.Id} should be blue");
        }
    }

    [Fact]
    public void A_slice_draws_the_ground_in_the_surface_biomes_colour_and_marks_the_sea_level()
    {
        var sampler = new WorldSampler(_data, 42);
        var image = Renderer.Slice(sampler, -512, 0, 512, 0, 4, 2);

        Assert.Equal(257, image.Width); // a column every 4 blocks along the 1 024, both ends included
        Assert.Equal((sampler.MaxY - sampler.MinY + 1) * 2, image.Height);
        for (var i = 3; i < image.Width; i += 17)
        {
            var column = sampler.At(-512 + (i * 4), 0);
            var row = (sampler.MaxY - Math.Clamp(column.Ground, sampler.MinY, sampler.MaxY)) * 2;
            var expected = sampler.Palette.Of(column.Biome);
            // The top of the ground, unless a snow or tree line passes over it (white or green).
            var pixel = image.Get(i, row);
            Assert.True(pixel == expected || pixel == new Rgb(255, 255, 255) || pixel == new Rgb(60, 230, 80), $"column {i}: {pixel}, expected {expected}, ground {column.Ground}, row {row}");
        }

        var seaRow = (sampler.MaxY - sampler.SeaLevel) * 2;
        Assert.Equal(new Rgb(255, 214, 64), image.Get(0, seaRow));
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("biomes", "--size", "1000", "--scale", "16")]
    [InlineData("biomes", "--nope", "1")]
    [InlineData("slice", "--seed", "abc")]
    public void A_bad_command_line_is_refused_with_exit_code_two(params string[] args) =>
        Assert.Equal(2, Cli.Run(args, new StringWriter(), new StringWriter()));
}
