using System.Diagnostics;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tests.World.Regions;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Streaming;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests.World.Streaming;

/// <summary>The guide's facts over a streamed world (B07, B06's finding): a chunk that is not resident is read as the generator made it, without installing it, so a snapshot of thousands of chunks neither stalls on the ones that hold only air or rock nor fills the grid; the rock under the ground is not part of the guide's world, resident or not.</summary>
public sealed class PristineChunksTests(ITestOutputHelper output)
{
    [Fact]
    public void Given_ChunksOfEveryKind_When_TheyAreReadPristine_Then_EachIsWhatTheSourceGeneratesAndNothingIsInstalled()
    {
        var scene = RegionTestWorld.StreamedScene();
        var world = scene.Streaming!;
        var reader = new PristineChunks(world);
        var coords = new List<VoxelChunkCoord>();
        foreach (var (cx, cz) in new[] { (5, 5), (12, 12), (25, 13), (-8, 40), (60, -20), (-3, 3) })
        {
            for (var cy = -4; cy < 20; cy++)
            {
                coords.Add(new VoxelChunkCoord(cx, cy, cz));
            }
        }

        foreach (var coord in coords)
        {
            var band = world.Bands.Of(coord.X, coord.Z);
            var chunk = reader.Read(coord);
            var covered = world.Regions.Covering(coord) is not null;
            if (!covered && (band.IsEmpty || coord.Y < band.MinLayer || coord.Y > band.MaxLayer))
            {
                Assert.Null(chunk); // above the ground: air; under it: not part of the guide's world
                continue;
            }

            var expected = world.Source.Generate(coord);
            if (expected.IsEmpty)
            {
                Assert.Null(chunk);
            }
            else
            {
                Assert.NotNull(chunk);
                Assert.Equal(expected.ComputeDigestHex(), chunk!.ComputeDigestHex());
            }
        }

        Assert.Equal(0, scene.Grid.ResidentChunkCount);
    }

    [Fact]
    public void Given_TheValleyAndTheLandOutsideIt_When_ASnapshotIsTaken_Then_NothingIsInstalledAndTheAnswersEqualTheInstallingSnapshots()
    {
        var nouns = TestGame.Nouns;
        var directions = TestGame.Data.Words.Directions;
        var lemmas = nouns.Nouns.Where(static n => n.Blocks.Count > 0).Select(static n => n.Lemma).Take(10).ToList();
        foreach (var (x, z) in new[] { (200, 200), (600, 260), (-150, 300) })
        {
            var transient = RegionTestWorld.StreamedScene();
            var resident = RegionTestWorld.StreamedScene();
            var feet = new BlockPos(x, 70, z);
            var window = WorldWindow.For(transient, x, 70, z);

            var watch = Stopwatch.StartNew();
            var cold = WorldFacts.Snapshot(transient.Grid, nouns, directions, window, new PristineChunks(transient.Streaming!));
            var elapsed = watch.Elapsed.TotalMilliseconds;

            // The same world with every chunk of the window's columns resident, the rock under the ground as well: the residency must not change what the guide sees.
            for (var cz = window.MinZ >> 4; cz < (window.EndZ + 15) >> 4; cz++)
            {
                for (var cx = window.MinX >> 4; cx < (window.EndX + 15) >> 4; cx++)
                {
                    foreach (var chunk in resident.Streaming!.Source.GenerateColumn(cx, cz))
                    {
                        _ = GridResidency.Install(resident.Grid, chunk);
                    }
                }
            }

            var warm = WorldFacts.Snapshot(resident.Grid, nouns, directions, window, new PristineChunks(resident.Streaming!));

            Assert.Equal(0, transient.Grid.ResidentChunkCount);
            Assert.True(resident.Grid.ResidentChunkCount > 100);
            var from = Viewpoint(feet);
            foreach (var lemma in lemmas)
            {
                Assert.Equal(warm.NearestOfNoun(from, lemma), cold.NearestOfNoun(from, lemma));
            }

            output.WriteLine($"snapshot at ({x}, {z}): {elapsed:0} ms without installing, {window.EndX - window.MinX} x {window.EndY - window.MinY} x {window.EndZ - window.MinZ} blocks");
            Assert.True(elapsed < 20_000, $"{elapsed} ms");
        }
    }

    [Fact]
    public void Given_ALiveFactsOverAStreamedWorld_When_ItIsAsked_Then_NoChunkIsInstalledToAnswerWhichChunksHoldTheNoun()
    {
        var scene = RegionTestWorld.StreamedScene();
        var nouns = TestGame.Nouns;
        var live = WorldFacts.Live(scene.Grid, nouns, TestGame.Data.Words.Directions, from => WorldWindow.For(scene, from.Cell.X, from.Cell.Y, from.Cell.Z), new PristineChunks(scene.Streaming!));
        var lemma = nouns.Nouns.First(static n => n.Blocks.Count > 0).Lemma;
        var from = Viewpoint(new BlockPos(600, 70, 260));

        _ = live.NearestOfNoun(from, lemma);

        Assert.True(scene.Grid.ResidentChunkCount < 400, $"{scene.Grid.ResidentChunkCount} chunks resident after the question; only the chunks that hold the noun are read");
    }

    private static GuideViewpoint Viewpoint(BlockPos p) =>
        new(new GuideCell(p.X, p.Y + 1, p.Z), ((long)p.X * WorldFacts.UnitsPerBlock) + WorldFacts.HalfBlock, ((long)p.Z * WorldFacts.UnitsPerBlock) + WorldFacts.HalfBlock, 0);
}
