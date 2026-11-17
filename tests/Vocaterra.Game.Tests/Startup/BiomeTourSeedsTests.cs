using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Startup;

/// <summary>
/// B03b: the biome tours of seeds 7 and 2026 (r52 and r53 in scripts/scripted-runs.sh, docs/game/scripted-runs.md) have the shape of seed 42's (<see cref="BiomeTourTests"/>): they parse, have a stop in every biome of
/// <c>game/biomes.json</c>, each stop stands in (or beside, for water) its biome of that seed's world and outside the valley's footprint, every teleport finds ground and the torch goes in an open cell with ground under it.
/// </summary>
public sealed class BiomeTourSeedsTests
{
    public static TheoryData<string, ulong> Tours => new() { { "r52-biome-tour-seed7", 7UL }, { "r53-biome-tour-seed2026", 2026UL } };

    private static Scene Scene(ulong seed)
    {
        var scene = WorldFactory.Create(TestGame.Data, seed, WorldMode.Streamed);
        if (scene.Grid.Barrier is { } wall)
        {
            wall.Locked = false; // the tours run with --pass open
        }

        return scene;
    }

    [Theory]
    [MemberData(nameof(Tours))]
    public void Given_ATourLine_When_ItIsParsed_Then_ItIsAValidRunOfTeleportsAndShots(string name, ulong seed)
    {
        Assert.True(LaunchOptions.TryParse(BiomeTourTests.TourArguments(name), out var options, out var error), error);

        Assert.True(options.PassOpen);
        Assert.Equal(seed, options.Seed);
        Assert.True(options.Script.Count(static s => s.Kind == ScriptStepKind.Shot) >= TestGame.Data.Biomes.Rows.Count + 3);
        Assert.Contains(options.VoxelSteps, static s => s.Kind == VoxelStepKind.Place);
    }

    [Theory]
    [MemberData(nameof(Tours))]
    public void Given_TheShippedBiomes_When_ATourIsRead_Then_EveryBiomeHasAStop(string name, ulong seed)
    {
        Assert.True(seed > 0);
        var named = BiomeTourTests.Stops(name).Select(static s => s.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var row in TestGame.Data.Biomes.Rows)
        {
            Assert.Contains(row.Id, named);
        }

        Assert.Contains("cave-mouth", named);
    }

    [Theory]
    [MemberData(nameof(Tours))]
    public void Given_TheStopsOfATour_When_TheyAreLookedUpInItsWorld_Then_EachBiomeStopStandsInItsBiome(string name, ulong seed)
    {
        var places = Scene(seed).Places!;
        var ids = TestGame.Data.Biomes.Rows.Select(static r => r.Id).ToHashSet(StringComparer.Ordinal);
        var checkedStops = 0;

        foreach (var (stop, x, z) in BiomeTourTests.Stops(name))
        {
            if (!ids.Contains(stop))
            {
                continue;
            }

            Assert.Null(places.RegionAt(x, z));
            if (TestGame.Data.Biomes.Row(stop).When.Water == true)
            {
                // A water biome is looked at from its bank: a column of it lies within a dozen blocks of the stop.
                var near = new[] { (0, 0), (8, 0), (-8, 0), (0, 8), (0, -8), (12, 0), (-12, 0), (0, 12), (0, -12), (8, 8), (-8, 8), (8, -8), (-8, -8) }.Any(d => places.BiomeAt(x + d.Item1, z + d.Item2).Id == stop);
                Assert.True(near, $"{stop}: no column of it within 12 blocks of {x},{z}");
            }
            else
            {
                Assert.Equal(stop, places.BiomeAt(x, z).Id);
            }

            checkedStops++;
        }

        Assert.Equal(ids.Count, checkedStops);
    }

    [Theory]
    [MemberData(nameof(Tours))]
    public void Given_TheTourSteps_When_TheTeleportsRunOnTheSeedsWorld_Then_EveryStopFindsGroundAndTheTorchHasAnOpenCell(string name, ulong seed)
    {
        Assert.True(LaunchOptions.TryParse(BiomeTourTests.TourArguments(name), out var options, out _));
        var scene = Scene(seed);
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
            Assert.Equal(BlockId.Air, sim.Grid.GetBlock(torch.X, torch.Y, torch.Z));
            Assert.True(sim.Grid.IsSolid(torch.X, torch.Y - 1, torch.Z), "with ground under it");
        }

        Assert.Equal(steps.Count, log.Count);
        Assert.All(log, l => Assert.True(l.Contains("standing at", StringComparison.Ordinal), l));
    }
}
