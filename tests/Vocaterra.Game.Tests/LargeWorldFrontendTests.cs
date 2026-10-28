using Vocaterra.Game.Core.Navigation;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests;

/// <summary>The front end's parts for the large world (S13), none of which needs a window: the view set, the buried-chunk rule, the presets, the walk planner and the teleport step.</summary>
public sealed class LargeWorldFrontendTests
{
    // ------------------------------------------------------------------ the chunks worth drawing

    [Fact]
    public void OnlyTheChunksWithinTheViewDistanceThatHoldSomethingAreInTheViewSetNearestFirst()
    {
        var scene = RouteScene.Build();
        var grid = scene.Grid;
        var eye = scene.Spawn.Feet;
        var set = new ChunkViewSet(grid, 96);
        var wanted = set.Update(eye.X + 0.5f, eye.Y + 1.6f, eye.Z + 0.5f);

        Assert.InRange(wanted.Count, 100, 900);
        Assert.True(wanted.Count < grid.Chunks.Count / 4, "a view of 96 blocks is a small part of the world");
        long last = -1;
        foreach (var index in wanted)
        {
            var chunk = grid.Chunks[index];
            Assert.False(chunk.IsEmpty);
            var distance = ChunkViewSet.DistanceSquared(eye.X + 0.5f, eye.Y + 1.6f, eye.Z + 0.5f, chunk.Coord.X, chunk.Coord.Y, chunk.Coord.Z);
            Assert.InRange(distance, last, 96L * 96);
            last = distance;
        }

        // Every non-empty chunk within the distance is in it: nothing is missed (checked by brute force over the whole grid).
        var expected = grid.Chunks.Count(c => !c.IsEmpty && ChunkViewSet.DistanceSquared(eye.X + 0.5f, eye.Y + 1.6f, eye.Z + 0.5f, c.Coord.X, c.Coord.Y, c.Coord.Z) <= 96L * 96);
        Assert.Equal(expected, wanted.Count);
        Assert.Equal(grid.Chunks.Count(c => c.IsEmpty), set.EmptyChunks);

        // A longer view holds more; the keep distance is the view and two chunks.
        Assert.True(new ChunkViewSet(grid, 200).Update(eye.X, eye.Y, eye.Z).Count > wanted.Count * 2);
        Assert.Equal(96 + 32, set.KeepDistance);
    }

    [Fact]
    public void TheViewSetIsRebuiltWhenTheEyeChangesChunkOrABlockIsEditedAndNotBefore()
    {
        var scene = RouteScene.Build();
        var set = new ChunkViewSet(scene.Grid, 64);
        var first = set.Update(151.5f, 74f, 111.5f);
        Assert.Same(first, set.Update(153.5f, 74f, 110.5f)); // the same chunk: the same list, nothing recomputed
        var moved = set.Update(300f, 40f, 300f);
        Assert.NotSame(first, moved);
        Assert.NotEqual(first, moved);
        var again = set.Update(300f, 40f, 300f);
        Assert.Same(moved, again);

        // Placing a block in a chunk of air makes the chunk worth drawing.
        var sky = scene.Grid.Chunks.First(c => c.IsEmpty && ChunkViewSet.DistanceSquared(300f, 40f, 300f, c.Coord.X, c.Coord.Y, c.Coord.Z) < 64 * 64);
        var origin = sky.Coord.Origin;
        scene.Grid.SetBlock(origin.X + 1, origin.Y + 1, origin.Z + 1, BlockId.Stone);
        var after = set.Update(300f, 40f, 300f);
        Assert.NotSame(moved, after);
        Assert.Equal(moved.Count + 1, after.Count);
    }

    [Fact]
    public void AChunkFullOfOpaqueBlocksAllRoundIsBuriedAndOneThatCanBeSeenIsNot()
    {
        var grid = new VoxelGrid(48, 48, 48);
        for (var x = 0; x < 48; x++)
        {
            for (var z = 0; z < 48; z++)
            {
                grid.FillColumn(x, z, 0, 47, BlockId.Stone);
            }
        }

        var source = new GridBlockSource(grid);
        var middle = grid.GetChunk(new VoxelChunkCoord(1, 1, 1));
        Assert.True(ChunkMesher.IsBuried(source, middle));
        Assert.Equal(0, ChunkMesher.Build(source, 1, 1, 1).FaceCount);

        // On the edge of the world the outside reads as air, so the chunk shows its side; the floor under the world reads as stone.
        Assert.False(ChunkMesher.IsBuried(source, grid.GetChunk(new VoxelChunkCoord(0, 1, 1))));
        Assert.True(ChunkMesher.Build(source, 0, 1, 1).FaceCount > 0);
        Assert.True(ChunkMesher.IsBuried(source, grid.GetChunk(new VoxelChunkCoord(1, 0, 1))));

        // A hole in a neighbour across the border shows a face of the middle chunk; a transparent block inside it means it is not buried.
        grid.SetBlock(32, 20, 20, BlockId.Air);
        Assert.False(ChunkMesher.IsBuried(source, middle));
        Assert.True(ChunkMesher.Build(source, 1, 1, 1).FaceCount > 0);
        grid.SetBlock(32, 20, 20, BlockId.Stone);
        Assert.True(ChunkMesher.IsBuried(source, middle));
        grid.SetBlock(20, 20, 20, BlockId.Leaves);
        Assert.False(ChunkMesher.IsBuried(source, middle));
        grid.SetBlock(20, 20, 20, BlockId.Air);
        Assert.False(ChunkMesher.IsBuried(source, middle));
    }

    [Fact]
    public void TheTrackerForgetsAChunkItIsToldToDiscardAndSaysWhichChunkHasAMesh()
    {
        var grid = new VoxelGrid(32, 32, 32);
        var tracker = new ChunkVersionTracker(grid);
        var coord = new VoxelChunkCoord(1, 0, 1);
        Assert.False(tracker.HasMesh(coord));
        Assert.True(tracker.IsDue(coord));
        tracker.MarkMeshed(coord);
        Assert.True(tracker.HasMesh(coord));
        Assert.False(tracker.IsDue(coord));
        grid.SetBlock(16, 3, 16, BlockId.Stone);
        Assert.True(tracker.IsDue(coord));
        tracker.MarkMeshed(coord);
        tracker.Discard(coord);
        Assert.False(tracker.HasMesh(coord));
        Assert.True(tracker.IsDue(coord));
    }

    // ------------------------------------------------------------------ quality

    [Fact]
    public void TheQualityPresetsSetTheViewDistanceAndTheFogAndTheFarPlaneFollowIt()
    {
        var low = QualityPreset.For(QualityLevel.Low);
        var medium = QualityPreset.For(QualityLevel.Medium);
        var high = QualityPreset.For(QualityLevel.High);
        Assert.True(low.ViewDistance < medium.ViewDistance && medium.ViewDistance < high.ViewDistance);
        Assert.Equal("low", low.Name);
        Assert.Equal(QualityLevel.High, QualityPreset.Parse("high"));
        Assert.Null(QualityPreset.Parse("ultra"));
        Assert.Equal(QualityPreset.MinimumViewDistance, low.WithViewDistance(5).ViewDistance);
        Assert.Equal(300, low.WithViewDistance(300).ViewDistance);

        foreach (var preset in new[] { low, medium, high })
        {
            // The fog is complete a little inside the view distance, so the last chunk fades out before it is cut; the far plane is past the last chunk's corner.
            Assert.InRange(VoxelRenderer.FogEndFor(preset.ViewDistance), preset.ViewDistance * 0.85f, preset.ViewDistance);
            Assert.True(VoxelRenderer.FarPlaneFor(preset.ViewDistance) > preset.ViewDistance + VoxelChunk.Size);
            Assert.True(VoxelRenderer.FogEndFor(preset.ViewDistance) * VoxelRenderer.FogStartFraction < VoxelRenderer.FogEndFor(preset.ViewDistance));
            Assert.True(preset.MeshBudgetMilliseconds > 0);
        }
    }

    // ------------------------------------------------------------------ walking

    [Fact]
    public void TheWalkPlannerGoesRoundAHouseAndEveryHopRunsAlongOneOfTheEightDirections()
    {
        var scene = RouteScene.Clearing();
        var grid = scene.Grid;
        var start = new BlockPos(66, 11, 57); // west of house 1 (71..75, 55..59)
        var route = WalkPlanner.Plan(grid, start, 78, 57);
        Assert.NotNull(route);
        Assert.True(route.Count >= 2, "the house is in the way: the route turns");
        Assert.Equal((78, 57), route[^1]);

        // Walk the hops cell by cell: every cell is one a body can stand in, and no hop passes through the house.
        var (x, z, y) = (start.X, start.Z, start.Y);
        foreach (var (tx, tz) in route)
        {
            var dx = Math.Sign(tx - x);
            var dz = Math.Sign(tz - z);
            Assert.True(dx == 0 || dz == 0 || Math.Abs(tx - x) == Math.Abs(tz - z), "a hop runs along one of the eight directions");
            while ((x, z) != (tx, tz))
            {
                var next = WalkPlanner.StepTo(grid, x, y, z, x + dx, z + dz);
                Assert.True(next >= 0, $"no standing cell at ({x + dx}, {z + dz})");
                (x, z, y) = (x + dx, z + dz, next);
            }
        }

        Assert.Equal(WalkPlanner.Plan(grid, start, 78, 57), route); // the same way every time
    }

    [Fact]
    public void TheWalkPlannerFindsNoWayIntoTheWaterOrOutOfTheWorldAndAStraightWalkNeedsNoTurn()
    {
        var scene = RouteScene.Clearing();
        var river = scene.Point("river");
        Assert.Null(WalkPlanner.Plan(scene.Grid, scene.Spawn.Feet, river.X, river.Z));
        Assert.Null(WalkPlanner.Plan(scene.Grid, scene.Spawn.Feet, 200, 17));
        var straight = WalkPlanner.Plan(scene.Grid, scene.Spawn.Feet, 30, 17);
        Assert.Equal([(30, 17)], straight);
        Assert.Equal([(17, 17)], WalkPlanner.Plan(scene.Grid, scene.Spawn.Feet, 17, 17));
    }

    [Fact]
    public void TheWalkPlannerFindsAWalkableWayDownFromThePlayersCampToTheGuidesCamp()
    {
        var scene = RouteScene.Build();
        var grid = scene.Grid;
        var guide = scene.Point("camp.guide");
        var route = WalkPlanner.Plan(grid, scene.Spawn.Feet, guide.X, guide.Z);
        Assert.NotNull(route);
        Assert.Equal((guide.X, guide.Z), route[^1]);

        var (x, z, y) = (scene.Spawn.Feet.X, scene.Spawn.Feet.Z, scene.Spawn.Feet.Y);
        var cells = 0;
        var onPath = 0;
        foreach (var (tx, tz) in route)
        {
            var dx = Math.Sign(tx - x);
            var dz = Math.Sign(tz - z);
            while ((x, z) != (tx, tz))
            {
                var next = WalkPlanner.StepTo(grid, x, y, z, x + dx, z + dz);
                Assert.True(next >= 0);
                (x, z, y) = (x + dx, z + dz, next);
                cells++;
                if (grid.GetBlock(x, y - 1, z) == BlockId.Path)
                {
                    onPath++;
                }
            }
        }

        // The cheapest way may cut across the mountainside instead of keeping to the switchbacks (a fall is free in this game), but it is a way a body can walk.
        Assert.InRange(cells, 60, 400);
        Assert.True(onPath > 0, "the way starts on the camp's own trail");
    }

    // ------------------------------------------------------------------ the teleport step

    [Fact]
    public void TheTeleportStepStandsThePlayerOnTheGroundOfAColumnAndRefusesWhereThereIsNone()
    {
        var scene = RouteScene.Clearing();
        var sim = VoxelSimulation.Create(scene);
        var log = new List<string>();
        var script = new VoxelScript(sim, new LookControl(), [VoxelStep.Teleport(40, 30), VoxelStep.Teleport(500, 30), VoxelStep.Teleport(9, 17)], static _ => null, log.Add);
        var commands = new List<VoxelCommand>();
        while (!script.IsDone)
        {
            script.Fill(commands);
            if (log.Count == 1 && sim.Player.FeetBlock.X != 40)
            {
                Assert.Fail("the first teleport did not move the player");
            }

            sim.Step(commands);
        }

        Assert.Equal("teleport 40,30: standing at 40,11,30", log[0]);
        Assert.Equal("teleport 500,30: outside the world", log[1]);
        Assert.Equal("teleport 9,17: no ground to stand on there", log[2]); // a tree stands there: the trunk is in the way
        Assert.Equal(new BlockPos(40, 11, 30), sim.Player.FeetBlock);
    }
}
