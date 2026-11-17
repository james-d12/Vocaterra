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
          stats     the owner's map-review numbers as text (shares, land above 150 to 250, the sea's connectivity, snow, the coast's profile); no picture
          river     a river of the drainage network followed from a point on it (--x --z --length --step, --direction down, up or both): the ground, the natural ground and the water along it as one section, with a text summary of its steps and rises (there must be none)
          slice     a vertical section along a line, y -64 to 320: ground in the surface biome's colour, water, snow line, tree line, sea level; with --mode blocks, the real blocks (B11: the strata, ores, caves, lava, bedrock) each in its own colour
          drainage  the heights with the drainage network drawn over them (B02c): every reach of river as a line, thicker the more ground drains into it; with --basin-x --basin-z (a point on a river) that river's whole basin in orange
          sites     where the drainage network has something worth a picture in the area (B02c): confluences of large rivers, the steepest stretches of river, lakes with the river that leaves them; text only
          views     good places to stand to look at a point (--x --z): dry ground a sixth of --size to half of it (default 50 to 150 blocks) from it, the highest first, with the yaw that faces the point (B02c, for pictures taken in the game); text only
          entrances the cave entrances of a square area (B11), one pixel a block: the ground in grey and every column cut open to a cave in red; give a --size of at most 2048

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
          river only:  --x --z (a point on or near the river), --length <n> (blocks to walk, default 4000), --step <n> (blocks per sample, default 4), --direction <down|up|both> (from the point to the sea, from the source to the point, or the whole river: the default), --zoom <n> (pixels per block of height, default 3), --wide <n> (pixels per sample, default 2)
          slice only:  --x0 <n> --z0 <n> --x1 <n> --z1 <n> (the line; default from -2048,0 to 2048,0), --scale (blocks per pixel along the line), --zoom <n> (pixels per block of height, default 2), --mode <surface|blocks> (default surface)
        """;

    private static readonly HashSet<string> Known = new(StringComparer.Ordinal)
    {
        "--seed", "--x", "--z", "--size", "--scale", "--out", "--content", "--threads", "--x0", "--z0", "--x1", "--z1", "--zoom", "--world", "--mode", "--region", "--region-x", "--region-z", "--region-layer", "--length", "--step", "--direction", "--wide", "--basin-x", "--basin-z",
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
        if (command is not ("biomes" or "heights" or "maps" or "slice" or "stats" or "entrances" or "river" or "drainage" or "sites" or "views"))
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

            if (options.ContainsKey("--region") && world != "valley")
            {
                throw new ArgumentException("--region needs --world valley.");
            }

            var town = options.TryGetValue("--region", out var layoutPath) ? BuildTown(layoutPath, options, data, seed) : null;

            var places = world == "valley"
                ? (town is null
                    ? Vocaterra.Game.Core.World.WorldFactory.Create(data, seed, Vocaterra.Game.Core.World.WorldMode.Bounded)
                    : Vocaterra.Game.Core.World.WorldFactory.Create(data, seed, Vocaterra.Game.Core.World.WorldMode.Streamed, null, [town])).Places
                : null;
            var sampler = new WorldSampler(data, seed, threads, places);
            var started = System.Diagnostics.Stopwatch.StartNew();
            if (command == "entrances")
            {
                var centreX = Int(options, "--x", 0);
                var centreZ = Int(options, "--z", 0);
                var side = Int(options, "--size", 1024);
                if (side > 2048)
                {
                    throw new ArgumentException("entrances draws one pixel a block: --size must be 2048 or less.");
                }

                var blocks = new BlockWorld(data, seed, world == "valley", town);
                var picture = Renderer.Entrances(blocks, sampler, centreX - (side / 2), centreZ - (side / 2), side, out var count);
                var path = Target(outPath, $"entrances-{Prefix(places, town)}seed{seed}-x{centreX}-z{centreZ}-s{side}");
                Save(path, picture);
                output.WriteLine($"{path} ({side} x {side}; {count} entrance columns, {started.Elapsed.TotalSeconds:0.0} s)");
                return 0;
            }

            if (command == "river")
            {
                var length = Int(options, "--length", 4000);
                var step = Int(options, "--step", 4);
                var walk = RiverWalk.Walk(sampler, Int(options, "--x", 0), Int(options, "--z", 0), length, step, Get(options, "--direction", "both"));
                var picture = RiverWalk.Draw(walk, sampler.SeaLevel, Int(options, "--zoom", 3), Int(options, "--wide", 2));
                var path = Target(outPath, $"river-seed{seed}-x{walk[0].X}-z{walk[0].Z}-{Get(options, "--direction", "both")}-{length}");
                Save(path, picture);
                var summary = RiverWalk.Summary(walk);
                File.WriteAllText(Path.ChangeExtension(path, ".txt"), summary);
                output.WriteLine($"{path} ({picture.Width} x {picture.Height}, {started.Elapsed.TotalSeconds:0.0} s)");
                output.Write(summary);
                return 0;
            }

            if (command == "slice")
            {
                var x0 = Int(options, "--x0", -2048);
                var z0 = Int(options, "--z0", 0);
                var x1 = Int(options, "--x1", 2048);
                var z1 = Int(options, "--z1", 0);
                var mode = Get(options, "--mode", "surface");
                if (mode is not ("surface" or "blocks"))
                {
                    throw new ArgumentException($"--mode is surface or blocks, not '{mode}'.");
                }

                var image = mode == "blocks"
                    ? Renderer.BlockSlice(new BlockWorld(data, seed, world == "valley", town), x0, z0, x1, z1, scale, Int(options, "--zoom", 2))
                    : Renderer.Slice(sampler, x0, z0, x1, z1, scale, Int(options, "--zoom", 2));
                var path = Target(outPath, $"slice-{(mode == "blocks" ? "blocks-" : string.Empty)}{Prefix(places, town)}seed{seed}-{x0},{z0}-{x1},{z1}-p{scale}");
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
            if (command == "stats")
            {
                var report = Stats.Report(sampler, map);
                output.Write(report);
                if (outPath != ".")
                {
                    File.WriteAllText(Target(outPath, "stats-" + stem, false).Replace(".png", ".txt"), report);
                }

                return 0;
            }

            if (command == "views")
            {
                output.Write(Sites.Views(sampler, Int(options, "--x", 0), Int(options, "--z", 0), options.ContainsKey("--size") ? Int(options, "--size", 150) / 2 : 150));
                return 0;
            }

            if (command == "sites")
            {
                output.Write(Sites.Describe(sampler, map));
                return 0;
            }

            if (command == "drainage")
            {
                var path = Target(outPath, "drainage-" + stem);
                var picture = Renderer.Heights(map, sampler.SeaLevel);
                var basin = options.ContainsKey("--basin-x") ? DrainageMap.Draw(picture, map, sampler, Int(options, "--basin-x", 0), Int(options, "--basin-z", 0)) : DrainageMap.Draw(picture, map, sampler, null, null);
                Save(path, picture);
                output.WriteLine($"{path} ({map.Width} x {map.Height}; {basin} reaches in the highlighted basin)");
                var mouths = sampler.Height.Reaches(map.Left, map.Top, map.Left + (map.Width * map.Scale), map.Top + (map.Height * map.Scale)).Where(r => r.IntoSea || r.IntoLake).OrderByDescending(r => r.Area).Take(6);
                output.WriteLine("the largest rivers' last reaches (x, z, nodes draining, ends in): " + string.Join("; ", mouths.Select(r => $"{r.FromX}, {r.FromZ}, {r.Area}, {(r.IntoSea ? "sea" : "lake")}")));
                return 0;
            }

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
