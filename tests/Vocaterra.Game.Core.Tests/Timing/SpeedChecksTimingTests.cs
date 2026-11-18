using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.World.Generation.Procedural;
using Vocaterra.Game.Core.Tests.World.Regions;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Climate;
using Vocaterra.Game.Core.World.Drainage;
using Vocaterra.Game.Core.World.Generation.Procedural;
using Vocaterra.Game.Core.World.Regions;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.Timing;

/// <summary>
/// The speed checks that used to sit beside outcome checks as wall-clock asserts (B29b, review section 2: a raw <c>Stopwatch</c> assert failed two of four gates under load). Each is the same bound as before, now CPU time of the
/// process (<see cref="CpuTime"/>), the fastest of several attempts, in the timing collection (alone in the assembly), against twice what the Release build costs; <c>CI=true</c> multiplies the budget by 4. The outcome checks stay
/// in their own tests (<c>GuideTurnTests</c>, <c>WorldFactsTests</c>, <c>WorldEditsSaveTests</c>, <c>DrainageNetworkTests</c>, <c>ProceduralReliefRegionTests</c>, <c>FarTerrainSamplerTests</c>), which no longer read a clock.
/// </summary>
[Collection(TimingCollection.Name)]
[Trait(TestSpeed.Category, TestSpeed.Timing)]
[Trait(TestSpeed.Trait, TestSpeed.Slow)]
public sealed class SpeedChecksTimingTests(ITestOutputHelper output)
{
    private const int Attempts = 3;

    // B29b: Release CPU cost (fastest of three, measured on a machine at load 15, so a little high) and the budget (twice the cost, rounded up), local milliseconds:
    //   guide turn 1.4 to 2.6 -> 6; snapshot 16 to 36 -> 80; save 0.5 to 1.0 -> 2.5; town build 563 to 978 -> 2000; sampler, 943 columns, 34 to 46 -> 100.
    // The six searches (under 0.01 ms a set) and the dry look-up (under 0.05 ms) cost less than the process's CPU clock can resolve (ticks of about 10 ms over 2 000 repeats): their budgets, 2 ms and 1 ms,
    // are far below the wall-clock bounds they replace (3 000 and 100 ms) and still catch a real regression (a landmass read, a search that walks the world), which costs hundreds of milliseconds.
    private static readonly double GuideTurnBudget = CpuTime.Budget(6); // per turn; was 250 ms wall
    private static readonly double SnapshotBudget = CpuTime.Budget(80); // was 1 500 ms wall
    private static readonly double SearchesBudget = CpuTime.Budget(2); // six searches; was 3 000 ms wall
    private static readonly double SaveBudget = CpuTime.Budget(2.5); // was 2 000 ms wall
    private static readonly double DryColumnBudget = CpuTime.Budget(1); // was 100 ms wall
    private static readonly double TownBuildBudget = CpuTime.Budget(2000); // was 5 000 ms wall
    private static readonly double SamplerBudget = CpuTime.Budget(100); // 64-step grid of outside columns; was 20 000 ms wall

    [Fact]
    public void Given_AGuideSession_When_ItTakesTwentyTurns_Then_TheCpuTimePerTurnIsWithinBudget()
    {
        var session = TestGame.NewSession();
        session.Say("hola"); // warm up the pipeline's first-call costs
        const int turns = 20;
        var best = CpuTime.Fastest(Attempts, () =>
        {
            for (var i = 0; i < turns; i++)
            {
                session.Say(i % 2 == 0 ? "¿dónde está el árbol?" : "dónde hay piedra");
            }
        }) / turns;

        output.WriteLine($"a guide turn: {best:F2} ms of CPU time, fastest of {Attempts} runs of {turns} turns (budget {GuideTurnBudget:F2} ms)");
        Assert.True(best < GuideTurnBudget, $"a guide turn takes {best:F2} ms of CPU time (budget {GuideTurnBudget:F2})");
    }

    [Fact]
    public void Given_TheLargeWorld_When_ItIsSnapshotAndSearched_Then_TheCpuTimeOfTheCopyAndOfSixSearchesIsWithinBudget()
    {
        var scene = TestGame.Route();
        var spawn = scene.Spawn.Feet;
        var viewpoint = new GuideViewpoint(spawn, (spawn.X * (long)Unit) + (Unit / 2), (spawn.Z * (long)Unit) + (Unit / 2), 0);
        var snapshot = WorldFacts.Snapshot(scene.Grid, TestGame.Nouns, TestGame.Data.Words.Directions); // warms the code and the shared composition
        var copied = CpuTime.Fastest(Attempts, () => snapshot = WorldFacts.Snapshot(scene.Grid, TestGame.Nouns, TestGame.Data.Words.Directions));
        const int Repeats = 2000; // a set of six searches is far below the process's CPU clock (ticks of about 10 ms): time two thousand and take the mean
        var searched = CpuTime.Fastest(Attempts, () =>
        {
            for (var repeat = 0; repeat < Repeats; repeat++)
            {
                foreach (var noun in new[] { "árbol", "río", "fruta", "mesa", "piedra", "camino" })
                {
                    _ = snapshot.NearestOfNoun(viewpoint, noun);
                }
            }
        }) / Repeats;

        output.WriteLine($"snapshot {copied:F0} ms (budget {SnapshotBudget:F0}), six searches {searched:F0} ms (budget {SearchesBudget:F0}), CPU time, fastest of {Attempts}");
        Assert.True(copied < SnapshotBudget, $"the snapshot takes {copied:F0} ms of CPU time (budget {SnapshotBudget:F0})");
        Assert.True(searched < SearchesBudget, $"six searches take {searched:F0} ms of CPU time (budget {SearchesBudget:F0})");
    }

    [Fact]
    public void Given_TheValleyWithAnEdit_When_ItIsSaved_Then_TheCpuTimeOfTheSaveIsWithinBudget()
    {
        var session = TestGame.NewSession();
        session.Scene.Grid.SetBlock(50, 5, 2, BlockId.Torch);
        _ = SaveGame.Save(session); // warms the code paths
        const int Repeats = 200; // one save is too short for the process's CPU clock (ticks of about 10 ms): time two hundred and take the mean
        var best = CpuTime.Fastest(Attempts, () =>
        {
            for (var repeat = 0; repeat < Repeats; repeat++)
            {
                _ = SaveGame.Save(session);
            }
        }) / Repeats;

        output.WriteLine($"save {best:F1} ms of CPU time, fastest of {Attempts} (budget {SaveBudget:F0} ms)");
        Assert.True(best < SaveBudget, $"the save takes {best:F0} ms of CPU time (budget {SaveBudget:F0})");
    }

    [Fact]
    public void Given_AFreshModel_When_AColumnOfAnUnreadLandmassIsAskedForDry_Then_TheCpuTimeIsWithinBudget()
    {
        // The game loop must never wait for a landmass (up to a second): the dry look-up reads nothing, so it is quick. Each attempt takes a fresh model.
        var world = DrainageNetworkTestsBase.World;
        var climate = new ClimateSampler(world, 7);
        var reach = new HeightModel(world, 7).Reaches(3000, 7400, 3500, 7900).OrderByDescending(static r => r.Area).First();
        var (x, z) = (reach.FromX, reach.FromZ);
        const int Repeats = 2000; // one look-up is far below the process's CPU clock (ticks of about 10 ms): time two thousand (a fresh model each) and take the mean
        var best = double.MaxValue;
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var models = Enumerable.Range(0, Repeats).Select(_ => new HeightModel(world, 7)).ToList();
            best = Math.Min(best, CpuTime.Milliseconds(() =>
            {
                using (DrainageNetwork.WithoutReading())
                {
                    models.ForEach(model => _ = model.Shape(x, z, climate.At(x, z)));
                }
            }) / Repeats);
        }

        output.WriteLine($"a dry column: {best:F1} ms of CPU time, fastest of {Attempts} (budget {DryColumnBudget:F0} ms)");
        Assert.True(best < DryColumnBudget, $"a dry column takes {best:F1} ms of CPU time (budget {DryColumnBudget:F0})");
    }

    [Fact]
    public void Given_TheSampleTown_When_ItIsBuiltAgain_Then_TheCpuTimeOfTheBuildIsWithinBudget()
    {
        var layout = SampleTown.Layout;
        var placement = SampleTown.Town().Placement;
        _ = ProceduralReliefRegion.Create(SampleTown.Id, layout, placement, SampleTown.Data.WorldGen, SampleTown.Seed); // warms the code
        var best = CpuTime.Fastest(Attempts, () => _ = ProceduralReliefRegion.Create(SampleTown.Id, layout, placement, SampleTown.Data.WorldGen, SampleTown.Seed));

        output.WriteLine($"a 96 x 96 x 96 town region: built, frozen and its edge profile worked out in {best:F0} ms of CPU time, fastest of {Attempts} (budget {TownBuildBudget:F0} ms)");
        Assert.True(best < TownBuildBudget, $"the town builds in {best:F0} ms of CPU time (budget {TownBuildBudget:F0})");
    }

    [Fact]
    public void Given_ManyColumns_When_TheyAreSampled_Then_TheCpuTimeIsWithinBudget()
    {
        var sampler = RegionTestWorld.Composition().World.Far;
        _ = sampler.At(1000, 1000);
        var columns = new List<(int X, int Z)>();
        for (var z = -1000; z <= 1000; z += 64)
        {
            for (var x = -1000; x <= 1000; x += 64)
            {
                if (x is < -100 or > 480 || z is < -100 or > 480)
                {
                    columns.Add((x + 5, z + 3));
                }
            }
        }

        var best = CpuTime.Fastest(Attempts, () => columns.ForEach(c => _ = sampler.At(c.X, c.Z)));

        output.WriteLine($"{columns.Count} samples, {best / columns.Count * 1000:F1} us of CPU time each, fastest of {Attempts} (budget {SamplerBudget:F0} ms in all)");
        Assert.True(best < SamplerBudget, $"{columns.Count} samples take {best:F0} ms of CPU time (budget {SamplerBudget:F0})");
    }

    private const int Unit = VoxelPhysics.UnitsPerBlock;
}
