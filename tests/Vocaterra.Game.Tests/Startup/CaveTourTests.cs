using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Startup;
using Vocaterra.Game.Tests.Streaming;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>B12: the cave tour (r50-cave-tour in scripts/scripted-runs.sh, docs/game/scripted-runs.md) parses with the launch options and its teleports land where it says: on the open ground, at the mouth of B11b's natural entrance, on its winding passage, at its landing in the cave, in a cavern and by a lava lake.</summary>
public sealed class CaveTourTests
{
    private static string[] TourArguments()
    {
        var line = File.ReadAllLines(Repo.Path("scripts", "scripted-runs.sh")).Single(static l => l.StartsWith("run r50-cave-tour ", StringComparison.Ordinal));
        var tokens = Regex.Matches(line["run r50-cave-tour ".Length..], "\"([^\"]*)\"|(\\S+)").Select(static m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);
        return [.. tokens.Select(static t => t.Replace("$W", "/tmp/cave-tour", StringComparison.Ordinal))];
    }

    [Fact]
    public void Given_TheTourLine_When_ItIsParsed_Then_ItIsTheSevenStopsWithTheirShots()
    {
        Assert.True(LaunchOptions.TryParse(TourArguments(), out var options, out var error), error);

        Assert.True(options.PassOpen);
        var kinds = options.VoxelSteps.Select(static s => s.Kind).ToList();
        Assert.Equal(3, kinds.Count(static k => k == VoxelStepKind.Teleport));
        Assert.Equal(4, kinds.Count(static k => k == VoxelStepKind.TeleportTo));
        Assert.Contains(options.VoxelSteps, static s => s.Kind == VoxelStepKind.Place && s.X == 467 && s.Y == 62);
        Assert.True(options.Script.Count(static s => s.Kind == ScriptStepKind.Shot) >= 7);
    }

    [Fact]
    public void Given_TheTourSteps_When_TheTeleportsRunOnTheStreamedWorld_Then_ThePlayerStandsWhereEachStopSays()
    {
        Assert.True(LaunchOptions.TryParse(TourArguments(), out var options, out _));
        var scene = StreamedWorlds.Scene();
        scene.Grid.Barrier = null; // the tour runs with --pass open: the valley's edge wall is lifted
        var sim = VoxelSimulation.Create(scene, TestItems.Catalogue);
        var places = scene.Places!;
        var log = new List<string>();
        var steps = options.VoxelSteps.Where(static s => s.Kind is VoxelStepKind.Teleport or VoxelStepKind.TeleportTo).ToList();
        var script = new VoxelScript(sim, new LookControl(), steps, static _ => null, log.Add);
        var commands = new List<VoxelCommand>();
        var depths = new List<int>();
        for (var tick = 0; tick < 20 && !script.IsDone; tick++)
        {
            script.Fill(commands);
            sim.Step(commands);
        }

        Assert.True(script.IsDone);
        Assert.Equal(7, log.Count);
        Assert.All(log, l => Assert.True(l.Contains("standing at", StringComparison.Ordinal), string.Join(" / ", log)));
        foreach (var l in log)
        {
            var at = l[(l.IndexOf("standing at ", StringComparison.Ordinal) + 12)..].Split(',').Select(int.Parse).ToArray();
            depths.Add(places.NaturalSurfaceAt(at[0], at[2]) - at[1]);
        }

        Assert.True(depths[0] <= 0 && depths[1] <= 0, "the pass and the open ground are at the surface");
        Assert.InRange(depths[2], -1, 2); // the mouth: at the ground, or a block or two under it in the funnel
        Assert.InRange(depths[3], 8, 24); // the winding passage, some way down
        Assert.InRange(depths[4], 15, 30); // the landing: a cave 20 or more blocks under the mouth
        Assert.True(depths[5] > 100 && depths[6] > 120, $"the cavern and the lava lake are deep ({string.Join(",", depths)})");
        foreach (var torch in options.VoxelSteps.Where(static s => s.Kind == VoxelStepKind.Place))
        {
            Assert.Equal(BlockId.Air, sim.Grid.GetBlock(torch.X, torch.Y, torch.Z)); // a torch goes in an open cell (the report of a place step names the block, not the torch, so a solid cell would pass unseen)
            Assert.True(sim.Grid.IsSolid(torch.X, torch.Y - 1, torch.Z), "with ground under it");
        }

        Assert.True(sim.Grid.GetBlock(528, -54, 190) == BlockId.Lava || Enumerable.Range(-6, 13).Any(d => sim.Grid.GetBlock(528 + d, -54, 190) == BlockId.Lava || sim.Grid.GetBlock(528, -54, 190 + d) == BlockId.Lava), "lava lies at the last stop");
    }
}
