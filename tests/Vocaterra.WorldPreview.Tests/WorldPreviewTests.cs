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
    public void The_generation_bench_runs_in_quick_mode_and_writes_its_json()
    {
        // B36: a smoke test only; the bench is a tool and its times are not asserted.
        var json = Path.Combine(_folder, "bench.json");
        Directory.CreateDirectory(_folder);
        var output = new StringWriter();
        var code = Cli.Run(["bench", "--quick", "--passes", "1", "--columns", "2", "--json", json], output, new StringWriter());

        Assert.Equal(0, code);
        Assert.Contains("far model stack L7, land", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"rows\"", File.ReadAllText(json), StringComparison.Ordinal);
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
    public void The_stats_command_reads_the_review_numbers_off_a_map_and_the_same_arguments_give_the_same_text()
    {
        string Stats()
        {
            var output = new StringWriter();
            var error = new StringWriter();
            Assert.Equal(0, Cli.Run(["stats", "--seed", "42", "--size", "4096", "--scale", "16"], output, error));
            Assert.Equal(string.Empty, error.ToString());
            return output.ToString();
        }

        var text = Stats();

        Assert.Equal(text, Stats());
        foreach (var line in new[] { "area: 4096 x 4096 blocks", "water:", "land above 150 / 200 / 250", "snow:", "sea bodies:", "rivers:", "coast profile", "  land:", "  sea:" })
        {
            Assert.Contains(line, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_snow_of_the_height_map_is_the_biome_tables_not_the_temperature_alone()
    {
        var sampler = new WorldSampler(_data, 42);
        var map = sampler.Map(0, 0, 4096, 16);
        var table = new BiomeTable(_data.Biomes, _data.WorldGen, 42);
        var climate = new ClimateSampler(_data.WorldGen, 42);
        var height = new HeightModel(_data.WorldGen, 42);

        for (var py = 0; py < map.Height; py += 5)
        {
            for (var px = 0; px < map.Width; px += 5)
            {
                var (x, z) = (map.BlockX(px), map.BlockZ(py));
                var c = climate.At(x, z);
                var shape = height.Shape(x, z, c);
                Assert.Equal(table.Classify(c, shape.Height, x, z, shape.Water, shape.RiverWeight).Snow, map.Snow[(py * map.Width) + px]);
            }
        }
    }

    [Fact]
    public void The_valley_world_samples_the_place_query_and_the_free_world_is_not_the_valley_world()
    {
        var places = Vocaterra.Game.Core.World.WorldFactory.Create(_data, 42, Vocaterra.Game.Core.World.WorldMode.Bounded).Places!;
        var valley = new WorldSampler(_data, 42, 2, places);
        var free = new WorldSampler(_data, 42);

        foreach (var (x, z) in new[] { (100, 100), (383, 208), (400, 208), (450, 300), (-20, 50), (1000, 1000) })
        {
            var column = valley.At(x, z);
            Assert.Equal(places.NaturalSurfaceAt(x, z), column.Ground);
            Assert.Equal(places.BiomeAt(x, z).Index, column.Biome);
        }

        var map = valley.Map(192, 192, 256, 4);
        Assert.Equal(valley.At(map.BlockX(10), map.BlockZ(20)).Ground, map.Ground[(20 * map.Width) + 10]);
        Assert.NotEqual(free.At(200, 200).Ground, valley.At(200, 200).Ground); // the valley's own ground is not the free world's
    }

    [Fact]
    public void The_valley_option_draws_the_footprint_outline_and_refuses_an_unknown_world()
    {
        var folder = Path.Combine(_folder, "valley");
        Run("heights", "--world", "valley", "--seed", "42", "--x", "192", "--z", "192", "--size", "1024", "--scale", "4", "--out", folder);
        var file = Assert.Single(Directory.GetFiles(folder, "*.png"));
        var (width, _, pixels) = PngDecoder.Decode(File.ReadAllBytes(file));
        Assert.Equal(256, width);
        var footprint = Overlay.Footprint;
        var drawn = Enumerable.Range(0, pixels.Length / 3).Count(i => pixels[i * 3] == footprint.R && pixels[(i * 3) + 1] == footprint.G && pixels[(i * 3) + 2] == footprint.B);
        Assert.InRange(drawn, 300, 400); // the footprint's outline: four sides of 96 pixels, less the corners drawn twice

        var error = new StringWriter();
        Assert.Equal(2, Cli.Run(["maps", "--world", "moon"], new StringWriter(), error));
        Assert.Contains("--world", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_region_option_places_a_town_from_a_layout_file_and_draws_its_outline()
    {
        var layout = Repo.Path("tests", "Vocaterra.Game.Core.Tests", "Fixtures", "sample-town.json");
        var folder = Path.Combine(_folder, "town");
        Run("heights", "--seed", "42", "--region", layout, "--region-x", "1264", "--region-z", "96", "--x", "1312", "--z", "144", "--size", "256", "--scale", "1", "--out", folder);
        var file = Assert.Single(Directory.GetFiles(folder, "*.png"));
        Assert.StartsWith("heights-town-seed42", Path.GetFileName(file), StringComparison.Ordinal);
        var (width, _, pixels) = PngDecoder.Decode(File.ReadAllBytes(file));
        Assert.Equal(256, width);
        var footprint = Overlay.Footprint;
        var drawn = Enumerable.Range(0, pixels.Length / 3).Count(i => pixels[i * 3] == footprint.R && pixels[(i * 3) + 1] == footprint.G && pixels[(i * 3) + 2] == footprint.B);
        Assert.InRange(drawn, 300, 400); // the town's footprint, 96 blocks a side (the valley is out of the picture)

        var error = new StringWriter();
        Assert.Equal(2, Cli.Run(["maps", "--region", layout, "--world", "free"], new StringWriter(), error));
        Assert.Contains("--region", error.ToString(), StringComparison.Ordinal);
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
                var shape = height.Shape(x, z, c);
                var row = table.Classify(c, shape.Height, x, z, shape.Water, shape.RiverWeight).Row;
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
            // The top of the ground, unless a snow or tree line passes over it (white or green), or the ground is at the sea level and the dashed mark is drawn over it.
            var pixel = image.Get(i, row);
            Assert.True(pixel == expected || pixel == new Rgb(255, 255, 255) || pixel == new Rgb(60, 230, 80) || (column.Ground == sampler.SeaLevel && pixel == new Rgb(255, 214, 64)), $"column {i}: {pixel}, expected {expected}, ground {column.Ground}, row {row}");
        }

        var seaRow = (sampler.MaxY - sampler.SeaLevel) * 2;
        Assert.Equal(new Rgb(255, 214, 64), image.Get(0, seaRow));
    }

    [Fact]
    public void A_block_slice_draws_each_block_in_its_own_colour_the_floor_black_and_marks_the_sea_level()
    {
        var world = new BlockWorld(_data, 42, valley: false);
        var image = Renderer.BlockSlice(world, -256, 0, 256, 0, 2, 2);

        Assert.Equal(257, image.Width);
        Assert.Equal((world.MaxY - world.MinY) * 2, image.Height);
        var background = new Rgb(28, 32, 44);
        for (var i = 10; i < image.Width; i += 11) // not the first columns: the height ticks are drawn there
        {
            var x = -256 + (i * 2);
            Assert.Equal(BlockColours.Of(Vocaterra.Game.Core.Voxel.BlockId.Bedrock), image.Get(i, (world.MaxY - 1 - world.MinY) * 2)); // the floor's lowest row is bedrock everywhere
            foreach (var y in new[] { -50, -30, -2, 12 })
            {
                var block = world.At(x, y, 0);
                Assert.Equal(block == Vocaterra.Game.Core.Voxel.BlockId.Air ? background : BlockColours.Of(block), image.Get(i, (world.MaxY - 1 - y) * 2));
            }
        }

        Assert.Equal(new Rgb(255, 214, 64), image.Get(0, (world.MaxY - 1 - world.SeaLevel) * 2));
    }

    [Fact]
    public void A_block_slice_and_the_entrance_map_are_written_by_the_command_line_and_are_the_same_every_time()
    {
        var (a, b) = (Path.Combine(_folder, "a"), Path.Combine(_folder, "b"));
        foreach (var folder in new[] { a, b })
        {
            Run("slice", "--mode", "blocks", "--seed", "42", "--x0", "-128", "--z0", "0", "--x1", "128", "--z1", "0", "--scale", "2", "--out", folder);
            Run("entrances", "--seed", "42", "--x", "0", "--z", "0", "--size", "256", "--out", folder);
        }

        var names = Directory.GetFiles(a).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(2, names.Length);
        Assert.Contains(names, static n => n!.StartsWith("slice-blocks-", StringComparison.Ordinal));
        Assert.Contains(names, static n => n!.StartsWith("entrances-", StringComparison.Ordinal));
        foreach (var name in names)
        {
            Assert.Equal(File.ReadAllBytes(Path.Combine(a, name!)), File.ReadAllBytes(Path.Combine(b, name!)));
        }
    }

    [Fact]
    public void The_entrance_map_marks_the_columns_cut_open_and_only_land_columns()
    {
        var world = new BlockWorld(_data, 42, valley: false);
        var sampler = new WorldSampler(_data, 42);
        var image = Renderer.Entrances(world, sampler, -128, -128, 256, out var count);

        Assert.Equal((256, 256), (image.Width, image.Height));
        var red = 0;
        for (var py = 0; py < 256; py++)
        {
            for (var px = 0; px < 256; px++)
            {
                if (image.Get(px, py) == new Rgb(255, 40, 40))
                {
                    red++;
                    Assert.False(sampler.At(-128 + px, -128 + py).Water);
                }
            }
        }

        Assert.Equal(count, red);
        Assert.True(count > 0 || world.Entrances(0, 0).Count == 0);
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("biomes", "--size", "1000", "--scale", "16")]
    [InlineData("biomes", "--nope", "1")]
    [InlineData("slice", "--seed", "abc")]
    [InlineData("slice", "--mode", "caves")]
    [InlineData("entrances", "--size", "4096")]
    public void A_bad_command_line_is_refused_with_exit_code_two(params string[] args) =>
        Assert.Equal(2, Cli.Run(args, new StringWriter(), new StringWriter()));
}
