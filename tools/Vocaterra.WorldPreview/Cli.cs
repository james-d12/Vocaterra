using System.Globalization;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Session;

namespace Vocaterra.WorldPreview;

/// <summary>The command line of the world preview: the commands <c>biomes</c>, <c>heights</c>, <c>slice</c> and <c>maps</c> (biomes and heights of one area), and their options.</summary>
internal static class Cli
{
    private const string Usage = """
        Vocaterra.WorldPreview: draws the procedural world's climate, height and biome model as PNG pictures. It only reads.

        usage: WorldPreview <command> [options]
          biomes    each column in its biome row's colour, with a legend text file (id, colour, share)
          heights   sea blue by depth, lowland green to upland brown to grey stone, white above the snow line, hill-shaded, with a stats text file (lowest and highest ground, shares above 100 to 250)
          maps      both of them for the same area
          slice     a vertical section along a line, y -64 to 320: ground in the surface biome's colour, water, snow line, tree line, sea level

        options:
          --seed <n>       the world seed (default 42)
          --x <n> --z <n>  the centre of the area in blocks (default 0 0)
          --size <n>       the side of the area in blocks (default 4096)
          --scale <n>      blocks per pixel (default 4; the size must be a multiple of it)
          --out <path>     a folder, or a .png file for one picture (default: the current folder; the file is named from the seed, the centre, the size and the scale)
          --content <dir>  the content bundle (default: content/game-v1 of the checkout)
          --threads <n>    worker threads (default 4)
          --world <free|valley>  free: the procedural world alone (default); valley: the world as the game's streamed mode makes it round the valley (B06): the valley's own ground inside its footprint, the land eased into its edge across the blend margin, the continentalness pulled towards land; maps then show the footprint (red), the margin (orange) and the land-bias radius (yellow)
          --region <layout.json>  (B15) place a town from a layout file on the procedural ground (implies --world valley): its region is built at --region-x --region-z (the block of its first column, rounded down to a chunk; default 0 0) and --region-layer (its first chunk layer; default: the layer that puts its floor under its lowest ground), and the maps carry its outlines too
          slice only:  --x0 <n> --z0 <n> --x1 <n> --z1 <n> (the line; default from -2048,0 to 2048,0), --scale (blocks per pixel along the line), --zoom <n> (pixels per block of height, default 2)
        """;

    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        "--seed", "--x", "--z", "--size", "--scale", "--out", "--content", "--threads", "--x0", "--z0", "--x1", "--z1", "--zoom", "--world", "--region", "--region-x", "--region-z", "--region-layer",
    };

    /// <summary>Runs a command line. The exit code is 0 when the pictures were written, 2 for a bad command line.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            output.WriteLine(Usage);
            return args.Length == 0 ? 2 : 0;
        }

        var command = args[0];
        if (command is not ("biomes" or "heights" or "maps" or "slice"))
        {
            error.WriteLine($"Unknown command '{command}'.");
            error.WriteLine(Usage);
            return 2;
        }

        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Length; i += 2)
        {
            if (!Known.Contains(args[i]) || i + 1 >= args.Length)
            {
                error.WriteLine($"Unknown option or missing value: '{args[i]}'.");
                return 2;
            }

            options[args[i]] = args[i + 1];
        }

        try
        {
            var seed = ulong.Parse(Get(options, "--seed", "42"), CultureInfo.InvariantCulture);
            var scale = Int(options, "--scale", 4);
            var threads = Int(options, "--threads", 4);
            var outPath = Get(options, "--out", ".");
            var content = options.TryGetValue("--content", out var c) ? c : FindBundle();
            var data = Load(content);
            var world = Get(options, "--world", options.ContainsKey("--region") ? "valley" : "free");
            if (world is not ("free" or "valley"))
            {
                throw new ArgumentException($"--world is free or valley, not '{world}'.");
            }

            var town = options.TryGetValue("--region", out var layoutPath) ? BuildTown(layoutPath, options, data, seed) : null;
            if (town is not null && world != "valley")
            {
                throw new ArgumentException("--region needs --world valley.");
            }

            var places = world == "valley"
                ? (town is null
                    ? Vocaterra.Game.Core.World.WorldFactory.Create(data, seed, Vocaterra.Game.Core.World.WorldMode.Bounded)
                    : Vocaterra.Game.Core.World.WorldFactory.Create(data, seed, Vocaterra.Game.Core.World.WorldMode.Streamed, null, [town])).Places
                : null;
            var sampler = new WorldSampler(data, seed, threads, places);
            var started = System.Diagnostics.Stopwatch.StartNew();
            if (command == "slice")
            {
                var x0 = Int(options, "--x0", -2048);
                var z0 = Int(options, "--z0", 0);
                var x1 = Int(options, "--x1", 2048);
                var z1 = Int(options, "--z1", 0);
                var image = Renderer.Slice(sampler, x0, z0, x1, z1, scale, Int(options, "--zoom", 2));
                var path = Target(outPath, $"slice-{Prefix(places, town)}seed{seed}-{x0},{z0}-{x1},{z1}-p{scale}");
                Save(path, image);
                output.WriteLine($"{path} ({image.Width} x {image.Height}, {started.Elapsed.TotalSeconds:0.0} s)");
                return 0;
            }

            var cx = Int(options, "--x", 0);
            var cz = Int(options, "--z", 0);
            var size = Int(options, "--size", 4096);
            var map = sampler.Map(cx, cz, size, scale);
            var sampled = started.Elapsed;
            var stem = $"{Prefix(places, town)}seed{seed}-x{cx}-z{cz}-s{size}-p{scale}";
            if (command is "biomes" or "maps")
            {
                var path = Target(outPath, "biomes-" + stem, command == "biomes");
                var picture = Renderer.Biomes(map, sampler.Palette);
                Frame(picture, map, places, data, town);
                Save(path, picture);
                var legend = Renderer.Legend(sampler, sampler.Shares(map));
                File.WriteAllText(Path.ChangeExtension(path, ".legend.txt"), legend);
                output.WriteLine($"{path} ({map.Width} x {map.Height}; sampled in {sampled.TotalSeconds:0.0} s, drawn by {started.Elapsed.TotalSeconds:0.0} s)");
                output.Write(legend);
            }

            if (command is "heights" or "maps")
            {
                var path = Target(outPath, "heights-" + stem, command == "heights");
                var picture = Renderer.Heights(map, sampler.SeaLevel);
                Frame(picture, map, places, data, town);
                Save(path, picture);
                var summary = Renderer.HeightSummary(map);
                File.WriteAllText(Path.ChangeExtension(path, ".stats.txt"), summary);
                output.WriteLine($"{path} ({map.Width} x {map.Height}; sampled in {sampled.TotalSeconds:0.0} s, drawn by {started.Elapsed.TotalSeconds:0.0} s)");
                output.Write(summary);
            }

            return 0;
        }
        catch (Exception e) when (e is ArgumentException or FormatException or OverflowException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            error.WriteLine(e.Message);
            return 2;
        }
    }

    /// <summary>Loads the bundle and its default locale through <see cref="GameData"/>, as the game and the speech renderer do.</summary>
    public static GameData Load(string bundleDirectory)
    {
        var bundle = GameBundle.Load(bundleDirectory);
        return GameData.Load(bundle, LocaleBundle.Load(LocaleBundle.DirectoryBeside(bundleDirectory)).Text(LocaleBundle.DefaultCode));
    }

    private static string FindBundle()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var d = new DirectoryInfo(start); d is not null; d = d.Parent)
            {
                var candidate = Path.Combine(d.FullName, "content", "game-v1");
                if (File.Exists(Path.Combine(candidate, "manifest.json")))
                {
                    return candidate;
                }
            }
        }

        throw new ArgumentException("content/game-v1 was not found; run from the checkout or give --content.");
    }

    private static string Prefix(Vocaterra.Game.Core.World.IPlaceInfo? places, Vocaterra.Game.Core.World.Regions.ProceduralReliefRegion? town) =>
        places is null ? string.Empty : town is null ? "valley-" : "town-";

    // B15: the town of a layout file, built on the procedural ground of the seed at the given block (rounded down to a chunk) and layer.
    private static Vocaterra.Game.Core.World.Regions.ProceduralReliefRegion BuildTown(string layoutPath, Dictionary<string, string> options, GameData data, ulong seed)
    {
        var layout = Vocaterra.Game.Core.World.Layout.SceneLayout.Load(layoutPath);
        var chunkX = Int(options, "--region-x", 0) >> 4;
        var chunkZ = Int(options, "--region-z", 0) >> 4;
        var placement = options.ContainsKey("--region-layer")
            ? new Vocaterra.Game.Core.World.Regions.RegionPlacement(chunkX, Int(options, "--region-layer", 0), chunkZ)
            : Vocaterra.Game.Core.World.Regions.ProceduralReliefRegion.Fit(layout, chunkX, chunkZ, data.WorldGen, seed);
        return Vocaterra.Game.Core.World.Regions.ProceduralReliefRegion.Create(layout.Name, layout, placement, data.WorldGen, seed);
    }

    // The valley world's maps carry the outlines of its regions: the footprint, the end of the blend margin and the land bias's radius.
    private static void Frame(Image image, WorldMap map, Vocaterra.Game.Core.World.IPlaceInfo? places, GameData data, Vocaterra.Game.Core.World.Regions.IRegion? town = null)
    {
        if (places is null)
        {
            return;
        }

        foreach (var region in new[] { places.RegionAt(0, 0), town }.OfType<Vocaterra.Game.Core.World.Regions.IRegion>())
        {
            var b = region.Bounds;
            var blend = data.WorldGen.Blend;
            Overlay.Outline(image, map, b.MinX, b.MinZ, b.MaxX, b.MaxZ, blend.LandBias.Radius, Overlay.Bias, dashed: true);
            Overlay.Outline(image, map, b.MinX, b.MinZ, b.MaxX, b.MaxZ, blend.Margin, Overlay.Margin, dashed: false);
            Overlay.Outline(image, map, b.MinX, b.MinZ, b.MaxX, b.MaxZ, 0, Overlay.Footprint, dashed: false);
        }
    }

    private static string Get(Dictionary<string, string> options, string name, string fallback) => options.TryGetValue(name, out var v) ? v : fallback;

    private static int Int(Dictionary<string, string> options, string name, int fallback) =>
        options.TryGetValue(name, out var v) ? int.Parse(v, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture) : fallback;

    // --out names a .png file for one picture, or a folder.
    private static string Target(string output, string name, bool single = true)
    {
        if (single && output.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            return output;
        }

        return Path.Combine(output.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? Path.GetDirectoryName(output) ?? "." : output, name + ".png");
    }

    private static void Save(string path, Image image)
    {
        if (Path.GetDirectoryName(Path.GetFullPath(path)) is { } folder)
        {
            Directory.CreateDirectory(folder);
        }

        File.WriteAllBytes(path, PngWriter.Encode(image));
    }
}
