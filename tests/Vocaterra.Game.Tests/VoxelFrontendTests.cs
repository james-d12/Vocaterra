using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Graphics.Camera;
using Vocaterra.Game.Graphics.Figures;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Input;
using Vocaterra.Game.Startup;
using Vocaterra.Game.UI.Hud;

namespace Vocaterra.Game.Tests;

public sealed class VoxelFrontendTests
{
    // A pitch in angle steps, signed (Angles.FromDegrees wraps into [0, 4096), which the simulation would clamp to straight up).
    private static int Pitch(double degrees) => (int)Math.Round(LookControl.DegreesToSteps(degrees));

    private static Scene RouteScene(ulong seed = 42) => Vocaterra.Game.Tests.RouteScene.Clearing(seed);

    // ---- the look ----

    [Fact]
    public void MouseMovementTurnsClockwiseToTheRightAndUpwardsLooksUp()
    {
        var look = new LookControl();
        look.Set(0, 0);
        look.Turn(100, 0);
        Assert.InRange(look.YawSteps, 100, 200);
        Assert.True(look.YawDegrees > 0 && look.YawDegrees < 90); // right is clockwise from north: towards east
        look.Set(0, 0);
        look.Turn(0, -50); // the mouse moved up the screen
        Assert.True(look.PitchSteps > 0);
        look.Turn(0, 200);
        Assert.True(look.PitchSteps < 0);
    }

    [Fact]
    public void TheYawWrapsAroundTheCircleAndThePitchStopsAtTheSimulationsLimit()
    {
        var look = new LookControl();
        look.Set(4090, 0);
        look.Turn(20, 0);
        Assert.InRange(look.YawSteps, 0, 100);
        Assert.InRange(look.Yaw, 0, 4095);
        look.Set(10, 0);
        look.Turn(-20, 0);
        Assert.InRange(look.YawSteps, 4000, 4096);
        look.Turn(0, -100000);
        Assert.Equal(Angles.MaxPitch, look.Pitch);
        look.Turn(0, 100000);
        Assert.Equal(-Angles.MaxPitch, look.Pitch);
    }

    [Fact]
    public void DegreesBecomeAngleStepsAsTheSimulationCountsThem()
    {
        var look = new LookControl();
        look.SetDegrees(90, 0);
        Assert.Equal(Angles.FromDegrees(90), look.Yaw);
        look.SetDegrees(-90, -45);
        Assert.Equal(3072, look.Yaw);
        Assert.Equal(-512, look.Pitch);
        look.SetDegrees(450, 0);
        Assert.Equal(1024, look.Yaw);
        Assert.Equal(1024.0, LookControl.DegreesToSteps(90), 9);
    }

    [Fact]
    public void LookAtFacesAPointInTheWorld()
    {
        var look = new LookControl();
        look.LookAt(10, 12, 10, 20, 12, 10); // due east
        Assert.Equal(90.0, look.YawDegrees, 6);
        Assert.Equal(0.0, look.PitchDegrees, 6);
        look.LookAt(10, 12, 10, 10, 12, 20); // due south
        Assert.Equal(180.0, look.YawDegrees, 6);
        look.LookAt(10, 12, 10, 0, 12, 10); // due west
        Assert.Equal(270.0, look.YawDegrees, 6);
        look.LookAt(10, 12, 10, 10, 12, 0); // due north
        Assert.True(look.YawDegrees is < 1e-6 or > 359.999999);
        look.LookAt(10, 12, 10, 20, 22, 10); // east and up at 45 degrees
        Assert.Equal(45.0, look.PitchDegrees, 6);
        look.LookAt(10, 12, 10, 10, 22, 10); // straight above: the yaw is kept, the pitch is clamped
        Assert.Equal(Angles.MaxPitch, look.Pitch);
        // What it faces is what the simulation's ray picks with: the same direction vector.
        look.LookAt(10, 12, 10, 14, 8, 6);
        var (dx, dy, dz) = Angles.Direction(look.Yaw, look.Pitch);
        Assert.InRange(dx / (double)Angles.One, 0.5, 0.65); // 4 / 6.93
        Assert.InRange(dy / (double)Angles.One, -0.65, -0.5);
        Assert.InRange(dz / (double)Angles.One, -0.65, -0.5);
    }

    [Fact]
    public void ParsingDegreesAcceptsDecimalsAndRefusesTheVerticalAndNonsense()
    {
        Assert.True(LookControl.TryParseDegrees("120,-5", out var yaw, out var pitch));
        Assert.Equal((120.0, -5.0), (yaw, pitch));
        Assert.True(LookControl.TryParseDegrees("0.5,89", out _, out _));
        Assert.False(LookControl.TryParseDegrees("0,90", out _, out _));
        Assert.False(LookControl.TryParseDegrees("0,-89.5", out _, out _));
        Assert.False(LookControl.TryParseDegrees("1;2", out _, out _));
        Assert.False(LookControl.TryParseDegrees("infinity,0", out _, out _));
    }

    [Fact]
    public void TheCameraLagsBehindAStepUpAndCatchesUpOverEightTicksPerBlock()
    {
        var ease = new CameraEase();
        ease.Tick(false);
        Assert.Equal(0.0, ease.Offset);
        ease.Tick(true); // the eye just jumped up a block
        Assert.InRange(ease.Offset, -1.0, -0.8);
        var last = ease.Offset;
        for (var i = 0; i < CameraEase.TicksPerBlock; i++)
        {
            ease.Tick(false);
            Assert.True(ease.Offset >= last); // never moves back down
            last = ease.Offset;
        }

        Assert.Equal(0.0, ease.Offset);
        ease.Tick(true);
        ease.Tick(true);
        ease.Tick(true);
        Assert.True(ease.Offset >= -CameraEase.MaximumLag);
        ease.Reset();
        Assert.Equal(0.0, ease.Offset);
    }

    [Fact]
    public void TheEyeRisesABlockInOneTickWhenTheBodyStepsOntoTheWorkbenchAndTheEaseHidesIt()
    {
        // The workbench at (24, 11, 17) is one block high: walking east into it steps the body up, and the camera must not jump with the eye.
        var sim = VoxelSimulation.Create(RouteScene());
        sim.TeleportTo(new BlockPos(21, 11, 17));
        var ease = new CameraEase();
        var found = false;
        for (var i = 0; i < 100 && !found; i++)
        {
            var before = sim.Player.EyeY;
            sim.Step([VoxelCommand.Move(1, 0)]);
            ease.Tick(sim.Player.SteppedUp);
            if (!sim.Player.SteppedUp)
            {
                continue;
            }

            found = true;
            Assert.InRange(sim.Player.EyeY - before, 4000, 4200); // the eye jumped about a block
            var drawn = (sim.Player.EyeY / 4096.0) + ease.Offset;
            Assert.InRange(drawn - (before / 4096.0), 0.0, 0.2); // the camera stays near the old height and has not jumped
        }

        Assert.True(found, "the body never stepped up");
    }

    // ---- the live input ----

    [Fact]
    public void TheLiveInputTurnsDeviceStateIntoCommandsAndTakesPressesOnce()
    {
        var input = new LiveVoxelInput();
        var commands = new List<VoxelCommand>();
        input.SetFrame(1, 1, true, new BlockPos(9, 11, 17), (ItemId.Wood, new BlockPos(5, 6, 7)), 3);
        input.Fill(commands);
        Assert.Contains(VoxelCommand.SelectSlot(3), commands);
        Assert.Contains(VoxelCommand.MoveRelative(1, 1), commands);
        Assert.Contains(VoxelCommand.Jump, commands);
        Assert.Contains(VoxelCommand.Interact(9, 11, 17), commands);
        Assert.Contains(VoxelCommand.Place(ItemId.Wood, 5, 6, 7), commands);

        commands.Clear();
        input.SetFrame(0, 0, false, null, null, null);
        input.Fill(commands);
        Assert.Empty(commands); // the press and the slot were taken once; the held ones are gone with their keys
    }

    [Fact]
    public void ThePressesSurviveFramesWithNoTickAndLevelsFollowTheLatestFrame()
    {
        var input = new LiveVoxelInput();
        input.SetFrame(0, 0, false, null, (ItemId.Torch, new BlockPos(1, 2, 3)), null);
        input.SetFrame(-1, 0, false, null, null, null); // a second frame before any tick: the press is not lost
        var commands = new List<VoxelCommand>();
        input.Fill(commands);
        Assert.Contains(VoxelCommand.Place(ItemId.Torch, 1, 2, 3), commands);
        Assert.Contains(VoxelCommand.MoveRelative(-1, 0), commands);
    }

    [Fact]
    public void GatherAndPlaceWorkFromWhatTheCrosshairHits()
    {
        var sim = VoxelSimulation.Create(RouteScene());
        sim.TeleportTo(new BlockPos(11, 11, 17));
        sim.Step([VoxelCommand.Look(Angles.FromDegrees(270), Pitch(-24))]);
        var target = sim.Target;
        Assert.NotNull(target);
        Assert.Equal(BlockId.Trunk, target.Value.Id);
        Assert.Equal(target.Value.Block, LiveVoxelInput.GatherFor(target));

        // Nothing is gatherable on the grass; placing needs a placeable item in the hand.
        sim.Step([VoxelCommand.Look(Angles.FromDegrees(270), Pitch(-60))]);
        var ground = sim.Target;
        Assert.NotNull(ground);
        Assert.Null(LiveVoxelInput.GatherFor(ground));
        Assert.Null(LiveVoxelInput.PlacementFor(sim.SelectedStack, ground)); // an empty hand
        _ = sim.GrantItems(ItemId.Wood, 2);
        var placement = LiveVoxelInput.PlacementFor(sim.SelectedStack, ground);
        Assert.NotNull(placement);
        Assert.Equal(ItemId.Wood, placement.Value.Item);
        Assert.Equal(ground.Value.Adjacent, placement.Value.At);
        Assert.Null(LiveVoxelInput.PlacementFor(new ItemStack(ItemId.Berry, 3), ground)); // a berry cannot be placed
        Assert.Null(LiveVoxelInput.GatherFor(null));
        Assert.Null(LiveVoxelInput.PlacementFor(sim.SelectedStack, null));

        // Sent through the simulation, the placement works.
        sim.Step([VoxelCommand.Place(placement.Value.Item, placement.Value.At.X, placement.Value.At.Y, placement.Value.At.Z)]);
        Assert.Equal(BlockId.Planks, sim.Grid.GetBlock(placement.Value.At.X, placement.Value.At.Y, placement.Value.At.Z));
    }

    [Fact]
    public void MouseLookReachesSdlThroughTheLibraryMonoGameLoadedOnLinux()
    {
        // The relative mouse mode is switched through SDL itself (MonoGame 3.8 has no switch). On Linux the library is in the output next to the
        // executable; if this ever fails the game falls back to warping the pointer, which works but is the older way.
        if (OperatingSystem.IsLinux())
        {
            Assert.True(MouseLook.UsesRelativeMode);
        }

        var look = new MouseLook();
        Assert.False(look.Captured);
        Assert.Equal((0, 0), look.Read(null!)); // not captured: no movement, and the window is not touched
    }

    // ---- re-meshing ----

    [Fact]
    public void EveryChunkIsDueUntilMeshedAndThenNoneIsUntilAnEdit()
    {
        var sim = VoxelSimulation.Create(RouteScene());
        var tracker = new ChunkVersionTracker(sim.Grid);
        Assert.Equal(72, tracker.Due().Count);
        foreach (var chunk in sim.Grid.Chunks)
        {
            tracker.MarkMeshed(chunk.Coord);
        }

        Assert.Empty(tracker.Due());
    }

    [Fact]
    public void AnEditInTheMiddleOfAChunkMakesOnlyThatChunkDue()
    {
        var sim = VoxelSimulation.Create(RouteScene());
        var tracker = new ChunkVersionTracker(sim.Grid);
        foreach (var chunk in sim.Grid.Chunks)
        {
            tracker.MarkMeshed(chunk.Coord);
        }

        sim.TeleportTo(new BlockPos(11, 11, 17));
        sim.Step([VoxelCommand.Look(Angles.FromDegrees(270), Pitch(-24))]);
        for (var i = 0; i < 60; i++)
        {
            sim.Step([VoxelCommand.Interact(9, 11, 17)]);
        }

        Assert.Equal(BlockId.Air, sim.Grid.GetBlock(9, 11, 17));
        var due = tracker.Due();
        Assert.Equal([VoxelChunkCoord.FromBlock(9, 11, 17)], due);
        tracker.MarkMeshed(due[0]);
        Assert.Empty(tracker.Due());
    }

    [Fact]
    public void AnEditOnAChunkBorderMakesTheNeighbourAcrossThatBorderDueAndNoOtherChunk()
    {
        var sim = VoxelSimulation.Create(RouteScene());
        var tracker = new ChunkVersionTracker(sim.Grid);
        foreach (var chunk in sim.Grid.Chunks)
        {
            tracker.MarkMeshed(chunk.Coord);
        }

        // (16, 11, 17) is the west edge of chunk (1, 0, 1): its faces depend on the block across the edge, in chunk (0, 0, 1).
        Assert.Equal(BlockId.Air, sim.Grid.GetBlock(16, 11, 17));
        _ = sim.GrantItems(ItemId.Wood, 1);
        sim.Step([VoxelCommand.Place(ItemId.Wood, 16, 11, 17)]);
        Assert.Equal(BlockId.Planks, sim.Grid.GetBlock(16, 11, 17));
        var due = tracker.Due().Select(static c => (c.X, c.Y, c.Z)).ToList();
        Assert.Equal([(0, 0, 1), (1, 0, 1)], due);
        foreach (var coord in tracker.Due())
        {
            tracker.MarkMeshed(coord);
        }

        Assert.Empty(tracker.Due());
    }

    [Fact]
    public void AnEditOnTheTopEdgeOfAChunkMakesTheChunkAboveDue()
    {
        var sim = VoxelSimulation.Create(RouteScene());
        var tracker = new ChunkVersionTracker(sim.Grid);
        foreach (var chunk in sim.Grid.Chunks)
        {
            tracker.MarkMeshed(chunk.Coord);
        }

        // Block y = 15 is the top layer of the chunks of the lowest level: put a plank on top of the trunk's column (9, 14) is not possible
        // (the trunk is four blocks high), so ask the simulation for a cell on that layer next to a block: the leaves at (8, 15, 17) and (11, 15, 17).
        Assert.Equal(BlockId.Leaves, sim.Grid.GetBlock(8, 15, 17));
        sim.TeleportTo(new BlockPos(11, 11, 17));
        _ = sim.GrantItems(ItemId.Wood, 1);
        sim.Step([VoxelCommand.Look(Angles.FromDegrees(270), Pitch(30))]);
        Assert.Equal(BlockId.Leaves, sim.Grid.GetBlock(11, 15, 17)); // S14's oak has a crown three columns wide: its east edge on this layer
        Assert.Equal(BlockId.Air, sim.Grid.GetBlock(12, 15, 17)); // the cell beside the leaves, on the top layer of chunk (0, 0, 1)
        sim.Step([VoxelCommand.Place(ItemId.Wood, 12, 15, 17)]);
        Assert.Equal(BlockId.Planks, sim.Grid.GetBlock(12, 15, 17));
        var due = tracker.Due().Select(static c => (c.X, c.Y, c.Z)).ToList();
        Assert.Equal([(0, 0, 1), (0, 1, 1)], due); // the chunk above looks down across the edge
    }

    [Fact]
    public void TheKeyOfAChunkIsZeroForNeighboursOutsideTheGridAndChangesWithTheEdgeLayer()
    {
        var sim = VoxelSimulation.Create(RouteScene());
        var tracker = new ChunkVersionTracker(sim.Grid);
        var corner = tracker.KeyOf(new VoxelChunkCoord(0, 0, 0));
        Assert.Equal(0UL, corner.West);
        Assert.Equal(0UL, corner.Down);
        Assert.Equal(0UL, corner.North);
        Assert.NotEqual(0UL, corner.East);
        Assert.NotEqual(0UL, corner.Up);
        Assert.NotEqual(0UL, corner.South);
        Assert.Equal(7, tracker.IndexOf(new VoxelChunkCoord(1, 0, 1))); // (cy * 6 + cz) * 6 + cx
        Assert.Equal(corner, tracker.KeyOf(new VoxelChunkCoord(0, 0, 0))); // reading the key does not change it
    }

    // ---- the figures, the labels and the text ----

    [Fact]
    public void TheThreeNpcsAreDrawnWithDistinctColoursAndSpanishNames()
    {
        var figures = NpcFigures.FromScene(RouteScene());
        Assert.Equal(["guide", "shopkeeper", "villager"], figures.Select(static f => f.Name).ToList());
        Assert.Equal(["Guía", "Tendero", "Aldeano"], figures.Select(static f => f.Label).ToList());
        var bodies = figures.Select(static f => NpcFigures.PartsOf(f.Name)[2].Colour).ToList(); // the body is the third part
        Assert.Equal(3, bodies.Distinct().Count());
        var heads = figures.Select(static f => NpcFigures.PartsOf(f.Name).Last().Colour).ToList();
        Assert.Equal(3, heads.Distinct().Count());
        Assert.Equal("Otro", NpcNames.LabelOf("Otro"));
    }

    [Fact]
    public void AFigureStandsOnItsBlockIsAboutTheHeightOfThePlayerAndFacesItsYaw()
    {
        foreach (var figure in NpcFigures.FromScene(RouteScene()))
        {
            var mesh = NpcFigures.Build(figure);
            Assert.True(mesh.FaceCount >= 60, "head, body, arms and legs have six faces each at least");
            var (fx, fy, fz) = figure.FeetPosition;
            var v = mesh.Vertices.ToArray();
            Assert.InRange(v.Min(static p => p.Y), fy - 0.001f, fy + 0.001f); // the feet are on the ground
            Assert.InRange(v.Max(static p => p.Y), fy + 1.8f, fy + 2.3f); // about the player's height, the hat above it
            Assert.All(v, p => Assert.InRange(Math.Abs(p.X - fx), 0f, 0.6f));
            Assert.All(v, p => Assert.InRange(Math.Abs(p.Z - fz), 0f, 0.6f));
        }
    }

    [Fact]
    public void TheFaceOfAFigureIsOnTheFrontOfItsHeadAtEveryYaw()
    {
        // The face is the picture on the north face of the head box (S15): its four vertices are the 4th to 7th of the head part's six faces (north first). Seen from the
        // feet they must lie in the direction the figure faces: yaw 0 north (-z), 90 east (+x), 180 south, 270 west.
        foreach (var name in new[] { "guide", "shopkeeper", "villager", NpcFigures.PlayerName })
        {
            var parts = NpcFigures.PartsOf(name);
            var headIndex = Array.FindIndex(parts, static p => p.Special is not null && p.SpecialFace == BlockFace.North && p.Group == FigureGroup.Head);
            Assert.True(headIndex >= 0, $"{name} has a face");
            var box = parts[headIndex].Box;
            var local = ((box.X0 + box.X1) / 2 * NpcFigure.PixelInBlocks, box.Z0 * NpcFigure.PixelInBlocks);
            foreach (var yawDegrees in new[] { 0, 90, 180, 270, 45 })
            {
                var figure = new NpcFigure(name, "x", new BlockPos(20, 10, 20), Angles.FromDegrees(yawDegrees));
                var vertices = NpcFigures.Build(figure).Vertices.ToArray();
                var face = vertices.Skip(headIndex * 24).Take(4).ToArray(); // the north face is the first face of a part
                var meanX = face.Average(static v => (double)v.X) - 20.5;
                var meanZ = face.Average(static v => (double)v.Z) - 20.5;
                var yaw = figure.YawRadians;
                Assert.InRange(Math.Abs(meanX - ((local.Item1 * Math.Cos(yaw)) - (local.Item2 * Math.Sin(yaw)))), 0, 2e-3);
                Assert.InRange(Math.Abs(meanZ - ((local.Item1 * Math.Sin(yaw)) + (local.Item2 * Math.Cos(yaw)))), 0, 2e-3);
                var forward = (X: Math.Sin(yaw), Z: -Math.Cos(yaw));
                Assert.True((meanX * forward.X) + (meanZ * forward.Z) > 0.15, $"the face of {name} facing {yawDegrees} is not in front of it");
            }
        }
    }

    [Fact]
    public void TheSameFigureAtAQuarterTurnIsTheSameShapeTurnedClockwiseAboutItsFeet()
    {
        var north = NpcFigures.Build(new NpcFigure("villager", "Aldeano", new BlockPos(10, 10, 10), 0));
        var east = NpcFigures.Build(new NpcFigure("villager", "Aldeano", new BlockPos(10, 10, 10), 1024));
        Assert.Equal(north.VertexCount, east.VertexCount);
        var a = north.Vertices.ToArray();
        var b = east.Vertices.ToArray();
        for (var i = 0; i < a.Length; i++)
        {
            // A quarter turn clockwise seen from above sends (dx, dz) to (-dz, dx), with dx towards the east and dz towards the south.
            var dx = a[i].X - 10.5f;
            var dz = a[i].Z - 10.5f;
            Assert.InRange(Math.Abs((10.5f - dz) - b[i].X), 0f, 1e-4f);
            Assert.InRange(Math.Abs((10.5f + dx) - b[i].Z), 0f, 1e-4f);
            Assert.Equal(a[i].Y, b[i].Y, 4);
        }
    }

    [Fact]
    public void TheDebugOverlayTellsThePositionTheFacingAndTheTarget()
    {
        var sim = VoxelSimulation.Create(RouteScene());
        sim.TeleportTo(new BlockPos(11, 11, 17));
        sim.Step([VoxelCommand.Look(Angles.FromDegrees(270), Pitch(-24))]);
        var lines = VoxelDebugText.Lines(60, sim, 42, sim.Player.Yaw, sim.Player.Pitch, 2);
        Assert.Equal(3, lines.Count); // the 2D test line is not part of the 3D overlay (S3)
        Assert.StartsWith("3D", lines[0], StringComparison.Ordinal);
        Assert.Contains("seed 42", lines[0], StringComparison.Ordinal);
        Assert.EndsWith("Session 2", lines[0], StringComparison.Ordinal); // the play session (S10)
        Assert.Contains("pos (11.5, 11.0, 17.5)", lines[1], StringComparison.Ordinal);
        Assert.Contains("yaw 270", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("target: trunk (9, 11, 17)", lines[2], StringComparison.Ordinal);
    }

    [Fact]
    public void TheSceneLocatorPrefersTheNamedFileThenTheBundlesOwnScene()
    {
        var bundle = Repo.Path("content", "game-v1");
        var resolved = SceneLocator.Resolve(null, bundle);
        Assert.True(File.Exists(resolved));
        Assert.EndsWith("game/scene.json", resolved.Replace('\\', '/'), StringComparison.Ordinal); // the bundle's own scene (S3)
        var explicitFile = Repo.Path("content", "game-v1", "game", "scene.json");
        Assert.Equal(explicitFile, SceneLocator.Resolve(explicitFile, bundle));
        Assert.Throws<FileNotFoundException>(() => SceneLocator.Resolve(Repo.Path("content", "no-such-scene.json"), bundle));
        Assert.Throws<FileNotFoundException>(() => SceneLocator.Resolve(null, Path.Combine(Path.GetTempPath(), "vocaterra-no-bundle")));
    }

    [Fact]
    public void TheRealRouteBuildsAtTheSeedAndFirstPersonKeepsTheGolden()
    {
        // The front-end only reads the world: building it for the renderer does not change it.
        var scene = Vocaterra.Game.Tests.RouteScene.Build();
        var before = scene.Grid.ComputeDigestHex();
        var tracker = new ChunkVersionTracker(scene.Grid);
        _ = tracker.Due();
        _ = ChunkMesher.Build(new GridBlockSource(scene.Grid), 10, 3, 6);
        Assert.Equal(before, scene.Grid.ComputeDigestHex());
        Assert.Equal(before, Vocaterra.Game.Tests.RouteScene.Build().Grid.ComputeDigestHex()); // built again, the same world
    }
}
