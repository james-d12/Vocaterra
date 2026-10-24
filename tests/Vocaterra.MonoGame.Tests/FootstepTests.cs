using Vocaterra.Game;
using Vocaterra.MonoGame.Sound;
using Vocaterra.MonoGame.Utilities;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.MonoGame.Tests;

/// <summary>
/// The footsteps (S25): the surface of every block kind, the cue of every surface, the pace of the steps with the walking speed, the jump, the landing and the
/// splash, and the variation of the takes (never the same take twice in a row).
/// </summary>
public sealed class FootstepTests
{
    private const double WalkBlocksPerSecond = 4.3; // the walking speed of the game (VoxelPhysics.WalkSpeed at 60 ticks a second)

    private static FootState Ground(double x, double z, Surface under = Surface.Grass, bool water = false, bool grounded = true) =>
        new(x, z, grounded, under, water);

    [Fact]
    public void EveryBlockKindHasASurfaceAndEverySolidKindIsWalkable()
    {
        foreach (var id in Enum.GetValues<BlockId>())
        {
            var surface = Footsteps.SurfaceOf(id);
            if (BlockDefinition.IsSolid(id))
            {
                Assert.NotEqual(Surface.Water, surface);
            }
        }

        // The solid kinds stand on something a step can sound on: every surface has a cue of the catalogue.
        foreach (var surface in Enum.GetValues<Surface>())
        {
            var cue = Footsteps.CueOf(surface);
            Assert.Equal(SoundCategory.Sfx, SoundCues.CategoryOf(cue));
            Assert.True(SoundCatalog.Of(cue).Takes >= 1, $"{surface} has a take");
        }
    }

    [Theory]
    [InlineData(BlockId.Grass, Surface.Grass)]
    [InlineData(BlockId.Dirt, Surface.Dirt)]
    [InlineData(BlockId.Farmland, Surface.Dirt)]
    [InlineData(BlockId.Path, Surface.Gravel)]
    [InlineData(BlockId.Stone, Surface.Stone)]
    [InlineData(BlockId.Cobble, Surface.Stone)]
    [InlineData(BlockId.MossyStone, Surface.Stone)]
    [InlineData(BlockId.FireRing, Surface.Stone)]
    [InlineData(BlockId.Wall, Surface.Stone)]
    [InlineData(BlockId.Planks, Surface.Wood)]
    [InlineData(BlockId.Bridge, Surface.Wood)]
    [InlineData(BlockId.Timber, Surface.Wood)]
    [InlineData(BlockId.Trunk, Surface.Wood)]
    [InlineData(BlockId.Sand, Surface.Sand)]
    [InlineData(BlockId.Snow, Surface.Snow)]
    [InlineData(BlockId.Water, Surface.Water)]
    [InlineData(BlockId.Leaves, Surface.Grass)]
    [InlineData(BlockId.Bush, Surface.Grass)]
    public void TheSurfaceOfABlockIsWhatItSoundsLike(BlockId id, Surface expected)
    {
        Assert.Equal(expected, Footsteps.SurfaceOf(id));
    }

    [Fact]
    public void EachSurfaceHasItsOwnStepCueAndTheSevenWalkableSurfacesAreAllUsed()
    {
        Assert.Equal(SoundCue.FootGrass, Footsteps.CueOf(Surface.Grass));
        Assert.Equal(SoundCue.FootDirt, Footsteps.CueOf(Surface.Dirt));
        Assert.Equal(SoundCue.FootGravel, Footsteps.CueOf(Surface.Gravel));
        Assert.Equal(SoundCue.FootStone, Footsteps.CueOf(Surface.Stone));
        Assert.Equal(SoundCue.FootWood, Footsteps.CueOf(Surface.Wood));
        Assert.Equal(SoundCue.FootSand, Footsteps.CueOf(Surface.Sand));
        Assert.Equal(SoundCue.FootSnow, Footsteps.CueOf(Surface.Snow));
        Assert.Equal(SoundCue.FootWater, Footsteps.CueOf(Surface.Water));

        var used = Enum.GetValues<BlockId>().Where(BlockDefinition.IsSolid).Select(Footsteps.SurfaceOf).ToHashSet();
        foreach (var surface in new[] { Surface.Grass, Surface.Dirt, Surface.Gravel, Surface.Stone, Surface.Wood, Surface.Sand, Surface.Snow })
        {
            Assert.Contains(surface, used);
        }
    }

    [Fact]
    public void StepsFollowTheWalkingSpeedAtTwoStepsAStrideOfTwoBlocks()
    {
        var steps = StepsWhileWalking(seconds: 10, blocksPerSecond: WalkBlocksPerSecond, out _);

        // 43 blocks in 10 seconds: one step every 2 blocks, so 21 or 22.
        Assert.InRange(steps.Count, 21, 22);
        Assert.All(steps, static cue => Assert.Equal(SoundCue.FootGrass, cue));
    }

    [Fact]
    public void TheStepsComeTwiceAsOftenWhenThePlayerWalksTwiceAsFast()
    {
        var slow = StepsWhileWalking(10, WalkBlocksPerSecond, out _).Count;
        var fast = StepsWhileWalking(10, WalkBlocksPerSecond * 2, out _).Count;

        Assert.InRange(fast, 2 * slow - 1, 2 * slow + 1);
    }

    [Fact]
    public void NoStepIsHeardInTheAirOrStandingStill()
    {
        var footsteps = new Footsteps();
        var cues = new List<SoundCue>();
        for (var i = 0; i < 120; i++)
        {
            cues.AddRange(footsteps.Observe(Ground(i * 0.1, 0, grounded: false), jumped: false, landed: false));
        }

        Assert.Empty(cues);

        var still = new Footsteps();
        for (var i = 0; i < 120; i++)
        {
            cues.AddRange(still.Observe(Ground(5, 5), jumped: false, landed: false));
        }

        Assert.Empty(cues);
    }

    [Fact]
    public void AStepTakesTheSurfaceUnderfootAndAWaterStepSplashes()
    {
        var footsteps = new Footsteps();
        var wood = new List<SoundCue>();
        for (var i = 0; i < 60; i++)
        {
            wood.AddRange(footsteps.Observe(Ground(i * (WalkBlocksPerSecond / 60), 0, Surface.Wood), jumped: false, landed: false));
        }

        Assert.NotEmpty(wood);
        Assert.All(wood, static cue => Assert.Equal(SoundCue.FootWood, cue));

        var water = new Footsteps();
        var splashes = new List<SoundCue>();
        for (var i = 0; i < 60; i++)
        {
            splashes.AddRange(water.Observe(Ground(i * (WalkBlocksPerSecond / 60), 0, Surface.Stone, water: true), jumped: false, landed: false));
        }

        Assert.NotEmpty(splashes);
        Assert.All(splashes, static cue => Assert.Equal(SoundCue.FootWater, cue));
    }

    [Fact]
    public void AJumpAndALandingSoundOnTheirOwnAndALandingInWaterSplashes()
    {
        var footsteps = new Footsteps();
        Assert.Equal([SoundCue.Jump], footsteps.Observe(Ground(0, 0, grounded: false), jumped: true, landed: false));
        Assert.Equal([SoundCue.Land], footsteps.Observe(Ground(0, 0), jumped: false, landed: true));
        Assert.Equal([SoundCue.FootWater], footsteps.Observe(Ground(0, 0, water: true), jumped: false, landed: true));
    }

    [Fact]
    public void TheTakesOfAStepVaryAndNeverComeTwiceInARow()
    {
        var output = new RecordingOutput();
        var mixer = new SoundMixer(output, new SoundVolumes(1.0, 1.0, 1.0, 1.0), new PresentationRng(21), null);
        for (var i = 0; i < 200; i++)
        {
            _ = mixer.Play(SoundCue.FootGrass);
        }

        var takes = output.Played.Select(static r => r.Take).ToList();
        Assert.Equal(200, takes.Count);
        Assert.All(takes, static t => Assert.InRange(t, 0, 2));
        Assert.Equal(new HashSet<int> { 0, 1, 2 }, takes.ToHashSet());
        Assert.All(Enumerable.Range(1, takes.Count - 1), i => Assert.NotEqual(takes[i - 1], takes[i]));
    }

    [Fact]
    public void TheVariationIsTheSameForTheSameSeed()
    {
        var first = Takes(seed: 4);
        var second = Takes(seed: 4);
        var other = Takes(seed: 5);

        Assert.Equal(first, second);
        Assert.NotEqual(first, other);
    }

    [Fact]
    public void WalkingOffAOneBlockStepIsAFootstepAndNotALanding()
    {
        var footsteps = new Footsteps();
        for (var i = 0; i < 10; i++)
        {
            _ = footsteps.Observe(new FootState(i * 0.1, 0, true, Surface.Stone, false, FeetY: 5), jumped: false, landed: false);
        }

        // The ledge: one airborne tick as the feet drop a block, then the ground at the lower level. The simulation reports a landing; it is not a thump.
        var cues = new List<SoundCue>();
        cues.AddRange(footsteps.Observe(new FootState(1.0, 0, false, Surface.Stone, false, FeetY: 4.6), jumped: false, landed: false));
        cues.AddRange(footsteps.Observe(new FootState(1.1, 0, true, Surface.Stone, false, FeetY: 4), jumped: false, landed: true));

        Assert.DoesNotContain(SoundCue.Land, cues);
        Assert.Contains(SoundCue.FootStone, cues);
    }

    [Fact]
    public void AJumpAndItsLandingPlayTheJumpAndTheLanding()
    {
        var footsteps = new Footsteps();
        var cues = new List<SoundCue>();
        cues.AddRange(footsteps.Observe(new FootState(0, 0, true, Surface.Grass, false, FeetY: 5), jumped: false, landed: false));
        cues.AddRange(footsteps.Observe(new FootState(0, 0, false, Surface.Grass, false, FeetY: 5.4), jumped: true, landed: false));
        cues.AddRange(footsteps.Observe(new FootState(0, 0, false, Surface.Grass, false, FeetY: 5.6), jumped: false, landed: false));
        cues.AddRange(footsteps.Observe(new FootState(0, 0, false, Surface.Grass, false, FeetY: 5.2), jumped: false, landed: false));
        cues.AddRange(footsteps.Observe(new FootState(0, 0, true, Surface.Grass, false, FeetY: 5), jumped: false, landed: true));

        Assert.Contains(SoundCue.Jump, cues);
        Assert.Contains(SoundCue.Land, cues);
        Assert.True(cues.IndexOf(SoundCue.Jump) < cues.IndexOf(SoundCue.Land), "the jump comes before the landing");
    }

    [Fact]
    public void AFallOfThreeBlocksPlaysTheLanding()
    {
        var footsteps = new Footsteps();
        for (var i = 0; i < 5; i++)
        {
            _ = footsteps.Observe(new FootState(i * 0.1, 0, true, Surface.Grass, false, FeetY: 5), jumped: false, landed: false);
        }

        var cues = new List<SoundCue>();
        cues.AddRange(footsteps.Observe(new FootState(0.6, 0, false, Surface.Grass, false, FeetY: 4.5), jumped: false, landed: false));
        cues.AddRange(footsteps.Observe(new FootState(0.7, 0, false, Surface.Grass, false, FeetY: 3.5), jumped: false, landed: false));
        cues.AddRange(footsteps.Observe(new FootState(0.8, 0, false, Surface.Grass, false, FeetY: 2.5), jumped: false, landed: false));
        cues.AddRange(footsteps.Observe(new FootState(0.9, 0, true, Surface.Grass, false, FeetY: 2), jumped: false, landed: true));

        Assert.Contains(SoundCue.Land, cues);
    }

    [Fact]
    public void AFallOfJustUnderTheThresholdIsAFootstep()
    {
        Assert.Equal(1.5, Footsteps.HardFallBlocks);
        var footsteps = new Footsteps();
        _ = footsteps.Observe(new FootState(0, 0, true, Surface.Sand, false, FeetY: 5), jumped: false, landed: false);
        _ = footsteps.Observe(new FootState(0, 0, false, Surface.Sand, false, FeetY: 4.2), jumped: false, landed: false);
        var cues = footsteps.Observe(new FootState(0, 0, true, Surface.Sand, false, FeetY: 3.6), jumped: false, landed: true); // a 1.4 block fall

        Assert.DoesNotContain(SoundCue.Land, cues);
        Assert.Contains(SoundCue.FootSand, cues);
    }

    [Fact]
    public void AScriptedJumpInTheSimulationSoundsAJumpAndALanding()
    {
        var scene = SceneBuilder.Build(SceneLayout.Load(Repo.Path("content", "game-v1", "game", "scene.json")), 42);
        var sim = VoxelSimulation.Create(scene);
        var footsteps = new Footsteps();
        var cues = new List<SoundCue>();

        void Tick(IReadOnlyList<VoxelCommand> commands)
        {
            sim.Step(commands);
            var jumped = sim.Events.Any(static e => e.Kind == SimulationEventKind.PlayerJumped);
            var landed = sim.Events.Any(static e => e.Kind == SimulationEventKind.PlayerLanded);
            cues.AddRange(footsteps.Observe(Footsteps.StateOf(sim.Player, (x, y, z) => sim.Grid.GetBlock(x, y, z)), jumped, landed));
        }

        for (var i = 0; i < 60; i++)
        {
            Tick([]); // the spawn settles on the ground
        }

        cues.Clear();
        Tick([VoxelCommand.Jump]);
        for (var i = 0; i < 90 && !cues.Contains(SoundCue.Land); i++)
        {
            Tick([]);
        }

        Assert.Contains(SoundCue.Jump, cues);
        Assert.Contains(SoundCue.Land, cues);
        Assert.True(cues.IndexOf(SoundCue.Jump) < cues.IndexOf(SoundCue.Land));
    }

    private static List<int> Takes(ulong seed)
    {
        var output = new RecordingOutput();
        var mixer = new SoundMixer(output, new SoundVolumes(1.0, 1.0, 1.0, 1.0), new PresentationRng(seed), null);
        for (var i = 0; i < 30; i++)
        {
            _ = mixer.Play(SoundCue.FootWood);
        }

        return output.Played.Select(static r => r.Take).ToList();
    }

    // Walks the given distance at a speed (blocks per second), one tick at a time, from a standing start, and returns the cues of the steps.
    private static List<SoundCue> StepsWhileWalking(double seconds, double blocksPerSecond, out int ticks)
    {
        var footsteps = new Footsteps();
        var cues = new List<SoundCue>();
        ticks = (int)(seconds * VoxelPhysics.TicksPerSecond);
        var perTick = blocksPerSecond / VoxelPhysics.TicksPerSecond;
        for (var i = 0; i <= ticks; i++)
        {
            cues.AddRange(footsteps.Observe(Ground(i * perTick, 0), jumped: false, landed: false));
        }

        return cues;
    }
}
