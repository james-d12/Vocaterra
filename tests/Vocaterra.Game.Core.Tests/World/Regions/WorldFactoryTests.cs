using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Tests.World.Regions;

/// <summary>The composition root (B06): the bounded valley as today with the world's place query and template digest, and the streamed world that equals it block for block inside the valley.</summary>
public sealed class WorldFactoryTests
{
    private static readonly Lazy<Scene> BoundedWorld = new(() => WorldFactory.Create(TestGame.Data, 42, WorldMode.Bounded));

    private static Scene Bounded => BoundedWorld.Value;

    [Fact]
    public void Given_TheBoundedMode_When_AWorldIsMade_Then_ItIsTheScenebuildersValleyWithThePlaceQueryAndTheTemplateDigest()
    {
        var built = RouteScene.Build(); // the same layout and seed the factory builds
        var scene = Bounded;

        Assert.False(scene.Grid.IsStreamed);
        Assert.Equal(built.Grid.ComputeDigestHex(), scene.Grid.ComputeDigestHex());
        Assert.Equal((built.Spawn, built.Name, built.Seed, built.TreeCount, built.WaterLevel), (scene.Spawn, scene.Name, scene.Seed, scene.TreeCount, scene.WaterLevel));
        Assert.Equal(built.Points.Select(static p => (p.Key, p.Value)), scene.Points.Select(static p => (p.Key, p.Value)));
        Assert.NotNull(scene.Places);
        Assert.Equal(RegionTestWorld.Valley().TemplateDigest, scene.TemplateDigest);
        Assert.Null(scene.ChunkSource);
        Assert.Empty(scene.Grid.ChangedChunks());
    }

    [Fact]
    public void Given_TheStreamedMode_When_EveryChunkOfTheValleyIsRead_Then_ItEqualsTheBoundedValleyBlockForBlock()
    {
        var bounded = Bounded.Grid;
        var scene = WorldFactory.Create(TestGame.Data, 42, WorldMode.Streamed);
        var streamed = scene.Grid;
        Assert.True(streamed.IsStreamed);
        Assert.Equal(0, streamed.ResidentChunkCount);
        var air = Convert.ToHexString(VoxelChunk.AirChunkDigest).ToLowerInvariant();
        var compared = 0;
        for (var cy = 0; cy < 8; cy++)
        {
            for (var cz = 0; cz < 24; cz++)
            {
                for (var cx = 0; cx < 24; cx++)
                {
                    var coord = new VoxelChunkCoord(cx, cy, cz);
                    var expected = bounded.TryGetChunk(coord, out var chunk) ? chunk.ComputeDigestHex() : air;
                    Assert.Equal(expected, streamed.GetChunk(coord).ComputeDigestHex());
                    compared++;
                }
            }
        }

        Assert.Equal(4608, compared);
        foreach (var (x, y, z) in new[] { (0, 0, 0), (383, 127, 383), (200, 60, 200), (-1, 60, 200), (384, 58, 208), (200, 128, 200) })
        {
            Assert.Equal(bounded.GetBlock(x, y, z), x is >= 0 and < 384 && z is >= 0 and < 384 && y < 128 ? streamed.GetBlock(x, y, z) : bounded.GetBlock(x, y, z));
        }

        Assert.Empty(streamed.ChangedChunks()); // installing what a source made is not an edit: the whole valley read and nothing is state
        Assert.Equal(Bounded.Spawn, scene.Spawn);
        Assert.NotNull(scene.ChunkSource);
        Assert.NotNull(scene.Places);
        Assert.Equal(Bounded.TemplateDigest, scene.TemplateDigest);
    }

    [Fact]
    public void Given_TheStreamedWorldAndTheBoundedValley_When_TheGuideAsksForTheNearestOfManyNouns_Then_TheFactsAnswerTheSameFromInsideTheValley()
    {
        var nouns = TestGame.Nouns;
        var directions = TestGame.Data.Words.Directions;
        var lemmas = nouns.Nouns.Where(static n => n.Blocks.Count > 0).Select(static n => n.Lemma).Take(14).ToList();
        Assert.NotEmpty(lemmas);
        var streamedScene = RegionTestWorld.StreamedScene();
        var viewpoints = Bounded.Points.Values.Where((_, i) => i % Math.Max(1, Bounded.Points.Count / 4) == 0).Take(4).Append(Bounded.Spawn.Feet).Select(Viewpoint).ToList();

        foreach (var from in viewpoints)
        {
            var bounded = WorldFacts.Snapshot(Bounded.Grid, nouns, directions);
            var window = WorldWindow.For(streamedScene, from.Cell.X, from.Cell.Y, from.Cell.Z);
            Assert.Equal(WorldWindow.Of(RegionTestWorld.Bounds), window); // inside the valley the window is the valley
            var streamed = WorldFacts.Snapshot(streamedScene.Grid, nouns, directions, window);
            var live = WorldFacts.Live(streamedScene.Grid, nouns, directions, v => WorldWindow.For(streamedScene, v.Cell.X, v.Cell.Y, v.Cell.Z));
            foreach (var lemma in lemmas)
            {
                var expected = bounded.NearestOfNoun(from, lemma);
                Assert.Equal(expected, streamed.NearestOfNoun(from, lemma));
                Assert.Equal(expected, live.NearestOfNoun(from, lemma));
            }
        }
    }

    [Fact]
    public void Given_TheBoundedFactoryWorld_When_ASessionStartsOnIt_Then_ItsStateHeaderCarriesTheTemplateDigest()
    {
        var session = new VoxelSession(TestGame.Data, 42, GuideSession.DefaultStartedAt, Bounded.Clone(), CreditRules.FullTyped, TestGame.NoLessons);
        var header = session.Simulation.StateHeader;
        var plain = TestGame.NewSession();

        Assert.Equal(Bounded.TemplateDigest, header.TemplateDigest);
        Assert.Equal(RegionTestWorld.Valley().TemplateDigest, header.TemplateDigest);
        Assert.Equal(new byte[WorldStateHeader.DigestLength], plain.Simulation.StateHeader.TemplateDigest); // a scene the builder made alone has no template digest
        Assert.NotEqual(plain.Simulation.ComputeStateDigest(), session.Simulation.ComputeStateDigest());
        Assert.Equal(header.GenerationDataHash, plain.Simulation.StateHeader.GenerationDataHash);
    }

    [Fact]
    public void Given_TheStreamedWorld_When_ASessionStartsAndTheBodyWalks_Then_ItRunsOnTheSparseGridWithoutScanningIt()
    {
        var scene = RegionTestWorld.StreamedScene();
        var session = new VoxelSession(TestGame.Data, 42, GuideSession.DefaultStartedAt, scene, CreditRules.FullTyped, TestGame.NoLessons);

        for (var i = 0; i < 20; i++)
        {
            session.Step([]);
        }

        Assert.Equal(Bounded.TemplateDigest, session.Simulation.StateHeader.TemplateDigest);
        Assert.Equal(Bounded.Spawn.Feet, session.Simulation.Player.FeetBlock);
        Assert.True(scene.Grid.ResidentChunkCount < 500, "a session reads the chunks near the player, not the world");
        Assert.NotEmpty(session.Fires.Middles); // the fire rings come from the template, found without enumerating the grid
    }

    private static GuideViewpoint Viewpoint(BlockPos p) =>
        new(new GuideCell(p.X, p.Y + 1, p.Z), ((long)p.X * WorldFacts.UnitsPerBlock) + WorldFacts.HalfBlock, ((long)p.Z * WorldFacts.UnitsPerBlock) + WorldFacts.HalfBlock, 0);
}
