using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Startup;
using Vocaterra.Game.Tests.Streaming;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>
/// B14: the biome tour (r51-biome-tour in scripts/scripted-runs.sh, docs/game/scripted-runs.md) parses with the launch options, has a stop in every biome of <c>game/biomes.json</c> (a new row is a new stop or
/// this fails), and each stop named for a biome stands in that biome of seed 42's world.
/// </summary>
public sealed class BiomeTourTests
{
    private const string Name = "r51-biome-tour";

    internal static string[] TourArguments(string name = Name)
    {
        var line = File.ReadAllLines(Repo.Path("scripts", "scripted-runs.sh")).Single(l => l.StartsWith($"run {name} ", StringComparison.Ordinal));
        var tokens = Regex.Matches(line[$"run {name} ".Length..], "\"([^\"]*)\"|(\\S+)").Select(static m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);
        return [.. tokens.Select(static t => t.Replace("$W", "/tmp/biome-tour", StringComparison.Ordinal))];
    }

    /// <summary>The stops: the last teleported column before each shot, with the name its picture carries (<c>07-river-channel.png</c> is <c>river-channel</c>).</summary>
    internal static List<(string Name, int X, int Z)> Stops(string name = Name)
    {
        var stops = new List<(string, int, int)>();
        var (x, z) = (0, 0);
        var arguments = TourArguments(name);
        for (var i = 0; i < arguments.Length - 1; i++)
        {
            if (arguments[i] is "--teleport" or "--teleport-to")
            {
                var parts = arguments[i + 1].Split(',').Select(int.Parse).ToArray();
                (x, z) = (parts[0], parts[^1]);
            }
            else if (arguments[i] == "--shot")
            {
                stops.Add((Regex.Replace(Path.GetFileNameWithoutExtension(arguments[i + 1]), "^[0-9]+-", string.Empty), x, z));
            }
        }

        return stops;
    }

    [Fact]
    public void Given_TheTourLine_When_ItIsParsed_Then_ItIsAValidRunOfTeleportsAndShots()
    {
        Assert.True(LaunchOptions.TryParse(TourArguments(), out var options, out var error), error);

        Assert.True(options.PassOpen);
        Assert.True(options.Script.Count(static s => s.Kind == ScriptStepKind.Shot) >= 20);
        Assert.True(options.VoxelSteps.Count(static s => s.Kind == VoxelStepKind.Teleport) >= 17);
        Assert.Contains(options.VoxelSteps, static s => s.Kind == VoxelStepKind.Place);
    }

    [Fact]
    public void Given_TheShippedBiomes_When_TheTourIsRead_Then_EveryBiomeHasAStop()
    {
        var named = Stops().Select(static s => s.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var row in TestGame.Data.Biomes.Rows)
        {
            Assert.Contains(row.Id, named);
        }
    }

    [Fact]
    public void Given_TheStopsOfTheTour_When_TheyAreLookedUpInTheWorld_Then_EachBiomeStopStandsInItsBiome()
    {
        var places = StreamedWorlds.Composition.Places;
        var ids = TestGame.Data.Biomes.Rows.Select(static r => r.Id).ToHashSet(StringComparer.Ordinal);
        var checkedStops = 0;

        foreach (var (name, x, z) in Stops())
        {
            if (ids.Contains(name))
            {
                Assert.Null(places.RegionAt(x, z));
                if (places.BiomeAt(x, z).When.Water == true || TestGame.Data.Biomes.Row(name).When.Water == true)
                {
                    // A water biome is looked at from its bank (a body in the water sinks to the bed): the biome lies within a dozen blocks of the stop, or the stop floats in it.
                    var near = new[] { (0, 0), (8, 0), (-8, 0), (0, 8), (0, -8), (12, 0), (-12, 0), (0, 12), (0, -12) }.Any(d => places.BiomeAt(x + d.Item1, z + d.Item2).Id == name);
                    Assert.True(near, $"{name}: no column of it within 12 blocks of {x},{z}");
                }
                else
                {
                    Assert.Equal(name, places.BiomeAt(x, z).Id);
                }

                checkedStops++;
            }
            else if (name == "valley")
            {
                Assert.NotNull(places.RegionAt(x, z));
            }
        }

        Assert.Equal(ids.Count, checkedStops);
    }

    [Fact]
    public void Given_TheTourSteps_When_TheTeleportsRunOnTheStreamedWorld_Then_EveryStopFindsGroundToStandOn()
    {
        Assert.True(LaunchOptions.TryParse(TourArguments(), out var options, out _));
        var scene = StreamedWorlds.Scene();
        scene.Grid.Barrier = null; // the tour runs with --pass open: the valley's edge wall is lifted
        var sim = VoxelSimulation.Create(scene);
        var log = new List<string>();
        var steps = options.VoxelSteps.Where(static s => s.Kind is VoxelStepKind.Teleport or VoxelStepKind.TeleportTo).ToList();
        var script = new VoxelScript(sim, new LookControl(), steps, static _ => null, log.Add);
        var commands = new List<VoxelCommand>();
        for (var tick = 0; tick < steps.Count + 5 && !script.IsDone; tick++)
        {
            script.Fill(commands);
            sim.Step(commands);
        }

        Assert.True(script.IsDone);
        foreach (var torch in options.VoxelSteps.Where(static s => s.Kind == VoxelStepKind.Place))
        {
            Assert.Equal(BlockId.Air, sim.Grid.GetBlock(torch.X, torch.Y, torch.Z)); // a torch goes in an open cell with ground under it
            Assert.True(sim.Grid.IsSolid(torch.X, torch.Y - 1, torch.Z), "with ground under it");
        }

        Assert.Equal(steps.Count, log.Count);
        Assert.All(log, l => Assert.True(l.Contains("standing at", StringComparison.Ordinal), l)); // the taiga stop once named a column under a crown: "no ground to stand on there"
    }
}
