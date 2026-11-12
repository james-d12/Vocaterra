using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.Camera;
using Vocaterra.Game.Graphics.Figures;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Startup;
using Vocaterra.Game.UI.Dialogue;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.Component;

/// <summary>The front-end pieces of S15 that need no graphics card: the figures and their poses, the facing rule, the F5 view, the arm's held item and light, the recipe panel and the speaker's bubble.</summary>
public sealed class VillageFrontendTests(ITestOutputHelper output)
{
    // ---- the facing rule ----

    [Theory]
    [InlineData(0, -5, 0)]
    [InlineData(5, 0, 1024)]
    [InlineData(0, 5, 2048)]
    [InlineData(-5, 0, 3072)]
    [InlineData(5, -5, 512)]
    [InlineData(-5, 5, 2560)]
    public void AFigureFacesTheDirectionOfTheTarget(double dx, double dz, double expectedSteps)
    {
        Assert.Equal(expectedSteps, FigureFacing.TowardSteps(10, 10, 10 + dx, 10 + dz), 3);
    }

    [Fact]
    public void TurningTakesTheShorterWayRoundAndNeverOvershoots()
    {
        Assert.Equal(-200, FigureFacing.Difference(100, 3996), 6);
        Assert.Equal(200, FigureFacing.Difference(3996, 100), 6);
        Assert.Equal(60, FigureFacing.Step(0, 60, 80));
        Assert.Equal(4096 - 80, FigureFacing.Step(0, 3900, 80)); // 196 to go the short way (backwards past north), 80 at a time
        Assert.Equal(4080, FigureFacing.Step(4000, 100, 80)); // forwards across north: 196 to go, 80 at a time
        Assert.Equal(20, FigureFacing.Step(4056, 20, 80)); // 60 to go: it arrives
        var facing = 2000.0;
        for (var i = 0; i < 40; i++)
        {
            facing = FigureFacing.Step(facing, 100);
        }

        Assert.Equal(100, facing, 6);
    }

    [Fact]
    public void ANpcInAConversationTurnsToThePlayerAndTurnsBackWhenItIsOver()
    {
        var figure = new NpcFigure("guide", "Guía", new BlockPos(100, 40, 100), Angles.FromDegrees(270)); // stands facing west
        Assert.Equal(3072, figure.FacingSteps);
        Assert.Equal(3072, FigureFacing.Wanted(figure, 100.5, 90.5), 6); // not talking: its own facing, wherever the player is
        figure.Talking = true;
        Assert.Equal(0, FigureFacing.Wanted(figure, 100.5, 90.5), 6); // the player is to the north
        Assert.Equal(1024, FigureFacing.Wanted(figure, 110.5, 100.5), 6); // to the east
        for (var i = 0; i < 60; i++)
        {
            FigureFacing.Update(figure, 100.5, 90.5);
        }

        Assert.Equal(0, figure.FacingSteps, 6);
        Assert.Equal(0f, figure.FacingRadians, 4);
        figure.Talking = false;
        for (var i = 0; i < 60; i++)
        {
            FigureFacing.Update(figure, 100.5, 90.5);
        }

        Assert.Equal(3072, figure.FacingSteps, 6);
    }

    [Fact]
    public void ATurningFigureTurnsAFewStepsATickSoItNeverSnaps()
    {
        var figure = new NpcFigure("villager", "Aldeano", new BlockPos(5, 5, 5), 0) { Talking = true };
        FigureFacing.Update(figure, 5.5, 10.5); // the player is to the south, half a turn away
        Assert.InRange(figure.FacingSteps, 1.0, FigureFacing.MaxStepPerTick + 0.001);
    }

    // ---- the figures ----

    // The looks the game draws: the three NPCs of S15, the village's four of village-plan V05 and the player.
    private static readonly string[] Looks = ["guide", "shopkeeper", "villager", "fisher", "baker", "smith", "farmer", NpcFigures.PlayerName];

    [Fact]
    public void EveryFigureHasAFaceAHeadBodyArmsAndLegsAndTheirClothesDiffer()
    {
        foreach (var name in Looks)
        {
            var parts = NpcFigures.PartsOf(name);
            Assert.Contains(parts, static p => p.Special is not null && p.SpecialFace == BlockFace.North && p.Group == FigureGroup.Head);
            foreach (var group in Enum.GetValues<FigureGroup>())
            {
                Assert.Contains(parts, p => p.Group == group);
            }
        }

        var faces = Looks.Select(static n => NpcFigures.PartsOf(n).First(static p => p.Special is not null && p.Group == FigureGroup.Head).Special).ToList();
        Assert.Equal(Looks.Length, faces.Distinct().Count()); // a face each
        Assert.Contains(NpcFigures.PartsOf("shopkeeper"), static p => p.Colour == (112, 70, 42)); // the apron
        Assert.Contains(NpcFigures.PartsOf("shopkeeper"), static p => p.Box.Y1 > 37f); // the baker's hat above the head
        Assert.Contains(NpcFigures.PartsOf("villager"), static p => p.Box.X1 - p.Box.X0 > 15f); // the brim of the straw hat
        Assert.Contains(NpcFigures.PartsOf("guide"), static p => p.Special == BlockCell.Backpack); // the guide's pack
        Assert.Contains(NpcFigures.PartsOf(NpcFigures.PlayerName), static p => p.Special == BlockCell.Backpack); // the player's backpack, seen over the shoulder
        Assert.DoesNotContain(NpcFigures.PartsOf("villager"), static p => p.Special == BlockCell.Backpack);
    }

    [Fact]
    public void TheVillagesFourFiguresAreDrawnByTheirOwnKeysAndNeverAsTheGreyStranger()
    {
        // village-plan V05: the fisher, the baker, the smith and the farmer (the figure keys their NPC files will name), each with its own clothes and face; an unknown
        // key is the plain grey figure, and none of the four is it.
        static (byte R, byte G, byte B) Body(string look) => NpcFigures.PartsOf(look)[2].Colour; // the body is the third part
        static BlockCell? Face(string look) => NpcFigures.PartsOf(look).First(static p => p.Special is not null && p.Group == FigureGroup.Head).Special;
        var stranger = (Body: Body("nobody"), Face: Face("nobody"));
        Assert.Equal(((byte)160, (byte)160, (byte)160), stranger.Body);
        foreach (var (look, face) in new[] { ("fisher", BlockCell.FaceFisher), ("baker", BlockCell.FaceBaker), ("smith", BlockCell.FaceSmith), ("farmer", BlockCell.FaceFarmer), ("innkeeper", BlockCell.FaceInnkeeper) })
        {
            Assert.NotEqual(stranger.Body, Body(look));
            Assert.Equal(face, Face(look));
        }

        // Every figure key the NPC files name is drawn by its own look.
        foreach (var npc in TestGame.Data.Npcs.All)
        {
            var key = npc.Figure ?? npc.Id;
            Assert.NotEqual(stranger.Body, Body(key)); // (the stranger borrows the villager's face, so the clothes tell them apart)
        }

        var fisher = NpcFigures.PartsOf("fisher");
        Assert.True(Body("fisher").B > Body("fisher").R * 2, "the fisher's coat is blue");
        Assert.Contains(fisher, static p => p.Group == FigureGroup.LegLeft && p.Box.Y0 == 0f && p.Box.Y1 >= 6.5f); // tall boots
        Assert.Contains(fisher, static p => p.Group == FigureGroup.Head && p.Box.Y1 > 34f); // a cap on his head

        var baker = NpcFigures.PartsOf("baker");
        Assert.All(baker.Where(static p => p.Group is FigureGroup.ArmLeft or FigureGroup.ArmRight && p.Cell == BlockCell.Cloth), static p => Assert.True(p.Colour.R > 240 && p.Colour.B > 230, "flour-white sleeves"));
        Assert.Contains(baker, static p => p.Group == FigureGroup.Body && p.Box.Z0 < -3.3f && p.Colour.R > 240); // a white apron over the front
        Assert.Contains(baker, static p => p.Group == FigureGroup.Head && p.Colour.R > 245 && p.Box.Y1 > 33f); // a white cap

        var smith = NpcFigures.PartsOf("smith");
        Assert.Contains(smith, static p => p.Group == FigureGroup.ArmLeft && p.Cell == BlockCell.Skin && p.Box.Y1 >= 17f); // rolled sleeves, the forearms bare
        Assert.Contains(smith, static p => p.Group == FigureGroup.Body && p.Box.Z0 < -3.3f && p.Colour.R < 70 && p.Box.Y0 < 5f); // a long dark apron
        Assert.DoesNotContain(smith, static p => p.Box.Y1 > 32.5f); // no hat

        var farmer = NpcFigures.PartsOf("farmer");
        Assert.True(Body("farmer").G > Body("farmer").R && Body("farmer").G > Body("farmer").B, "the farmer's shirt is green");
        var brim = Assert.Single(farmer, static p => p.Box.X1 - p.Box.X0 > 15f); // the villager's straw hat
        var villagersBrim = Assert.Single(NpcFigures.PartsOf("villager"), static p => p.Box.X1 - p.Box.X0 > 15f);
        Assert.Equal(villagersBrim.Box, brim.Box);
        Assert.NotEqual(villagersBrim.Colour, brim.Colour); // in another colour
    }

    [Fact]
    public void TheIdlePoseMovesWithTheTickAndEachFigureHasItsOwnRhythm()
    {
        var a = FigureAnimation.Idle(0, 1);
        var b = FigureAnimation.Idle(120, 1);
        Assert.NotEqual(a, b);
        Assert.NotEqual(FigureAnimation.Idle(60, 1), FigureAnimation.Idle(60, 5));
        Assert.Equal(FigureAnimation.Idle(77, 3), FigureAnimation.Idle(77, 3)); // no clock, no random numbers
        for (var tick = 0L; tick < 2000; tick += 7)
        {
            var pose = FigureAnimation.Idle(tick, 2);
            Assert.InRange(pose.Bob, -0.31f, 0.31f);
            Assert.InRange(pose.HeadYaw, -0.41f, 0.41f);
            Assert.InRange(pose.ArmLeft, -0.09f, 0.09f); // the arms only sway
            Assert.Equal(0f, pose.LegLeft);
        }

        Assert.True(new[] { "guide", "shopkeeper", "villager" }.Select(FigureAnimation.Phase).Distinct().Count() > 1);
    }

    [Fact]
    public void ATalkingFigureRaisesItsRightArmAndLooksAtThePlayer()
    {
        var figure = new NpcFigure("guide", "Guía", new BlockPos(10, 10, 10), 0);
        var idle = FigureAnimation.For(figure, 100);
        figure.Talking = true;
        var talking = FigureAnimation.For(figure, 100);
        Assert.InRange(idle.ArmRight, -0.1f, 0.1f);
        Assert.InRange(talking.ArmRight, 1.8f, 2.8f); // the arm is up and waving
        Assert.Equal(0f, talking.HeadYaw); // the head is straight at the player
    }

    [Fact]
    public void AWalkingPoseSwingsTheLegsAgainstEachOtherAndTheArmsAgainstTheLegs()
    {
        var pose = FigureAnimation.Walking(0.9f);
        Assert.True(pose.LegLeft * pose.LegRight < 0);
        Assert.True(pose.ArmLeft * pose.LegLeft < 0);
        Assert.InRange(MathF.Abs(pose.LegLeft), 0f, FigureAnimation.WalkSwing + 0.001f);
    }

    [Fact]
    public void ThePoseMovesTheArmUpButNeverTheFeetOrTheCountOfFaces()
    {
        var buffer = new MeshBuffer();
        var parts = NpcFigures.PartsOf("guide");
        FigureMesher.Append(buffer, parts, FigurePose.Rest, 10.5f, 40f, 10.5f, 0f, (1f, 1f, 1f));
        var restCount = buffer.VertexCount;
        var restTop = buffer.Vertices.ToArray().Min(static v => v.Y);
        Assert.Equal(40f, restTop, 3); // the feet are on the ground
        var restArmTop = ArmTop(parts, FigurePose.Rest);
        var raised = ArmTop(parts, FigureAnimation.Talking(10, 0));
        Assert.True(raised > restArmTop + 0.25f, "the waving hand is above where it hangs");

        buffer.Clear(); // the renderer reuses one buffer for every figure of every frame
        FigureMesher.Append(buffer, parts, FigureAnimation.Walking(1.2f), 10.5f, 40f, 10.5f, 1.3f, (1f, 1f, 1f));
        Assert.Equal(restCount, buffer.VertexCount);
        Assert.Equal(0, buffer.VertexCount % 4);
    }

    private static float ArmTop(FigurePart[] parts, FigurePose pose)
    {
        var buffer = new MeshBuffer();
        var single = parts.Where(static p => p.Group == FigureGroup.ArmRight).ToArray();
        FigureMesher.Append(buffer, single, pose, 0f, 0f, 0f, 0f, (1f, 1f, 1f));
        return buffer.Vertices.ToArray().Max(static v => v.Y);
    }

    [Fact]
    public void ALanternsWarmthTintsAFigureNearIt()
    {
        var buffer = new MeshBuffer();
        var parts = NpcFigures.PartsOf("villager");
        FigureMesher.Append(buffer, parts, FigurePose.Rest, 0f, 0f, 0f, 0f, (1f, 1f, 1f));
        var plain = buffer.Vertices.ToArray().Average(static v => (int)(v.Color & 0xFF) - (int)((v.Color >> 16) & 0xFF));
        buffer.Clear();
        FigureMesher.Append(buffer, parts, FigurePose.Rest, 0f, 0f, 0f, 0f, (1.2f, 1.1f, 0.9f));
        var warm = buffer.Vertices.ToArray().Average(static v => (int)(v.Color & 0xFF) - (int)((v.Color >> 16) & 0xFF));
        Assert.True(warm > plain, "red over blue grows under a warm light");
    }

    [Fact]
    public void DrawingTheFiguresTheArmAndTheShoulderCameraAllocatesNothingOnceWarm()
    {
        var buffer = new MeshBuffer();
        var figures = new[] { "guide", "shopkeeper", "villager", NpcFigures.PlayerName }.Select(static n => (Name: n, Parts: NpcFigures.PartsOf(n))).ToArray();
        var lights = new[] { new BlockPos(5, 5, 5), new BlockPos(9, 5, 9) };
        Func<int, int, int, bool> solid = static (x, y, z) => y < 4;
        var arm = ArmMeshes.Arm();
        var item = HeldItems.For(false, ItemId.Torch);
        ArmMeshes.Item(item);

        void Frame(long tick)
        {
            foreach (var (name, parts) in figures)
            {
                buffer.Clear();
                var pose = tick % 3 == 0 ? FigureAnimation.Talking(tick, 1) : FigureAnimation.Idle(tick, FigureAnimation.Phase(name));
                FigureMesher.Append(buffer, parts, pose, 10f, 4f, 10f, tick * 0.01f, (1f, 1.05f, 0.95f));
            }

            _ = ArmMotion.At(tick, tick % 2 == 0, (int)(tick % 30), tick % 40);
            _ = ArmLight.At(solid, (6f, 6f, 6f), lights, true, tick % 5 == 0 ? ChunkMesher.DuskLight : Day, tick % 5 == 0 ? 1f : 0.5f);
            _ = ThirdPersonRig.Eye(solid, (6f, 6f, 6f), (0f, 0f, -1f), (1f, 0f, 0f));
            _ = ArmMeshes.Item(item);
            _ = FigureAnimation.Walking(tick * 0.1f);
        }

        for (var i = 0; i < 200; i++)
        {
            Frame(i); // warm up: the buffer grows to its size, the code is compiled
        }

        // Nothing per frame: one object a frame would be 24 000 bytes or more over 1 000 frames; the few hundred bytes that may remain are the runtime's own (tiered compilation).
        // The test failed once in a loaded parallel run (S25): the runtime promotes a method to its optimised tier on its own schedule, and under load that can fall inside the
        // window, where its own allocations are counted against the code. So the 1 000-frame window is measured up to three times and the smallest count is the one asserted:
        // the runtime's promotion happens once and does not come back, while a real per-frame allocation is in every window and fails all three (24 000 bytes or more each).
        const int Bound = 4000;
        const int Attempts = 3;
        var smallest = long.MaxValue;
        var tick = 200;
        var elapsed = TimeSpan.Zero;
        for (var attempt = 0; attempt < Attempts && smallest > Bound; attempt++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < 1000; i++)
            {
                Frame(tick++);
            }

            stopwatch.Stop();
            elapsed = stopwatch.Elapsed;
            smallest = Math.Min(smallest, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        output.WriteLine($"four figures, the arm's light and motion and the shoulder camera: {elapsed.TotalMilliseconds / 1000:F4} ms a frame on the CPU, {buffer.FaceCount} quads in the last figure, {smallest} bytes allocated in the smallest window");
        Assert.InRange(smallest, 0, Bound);
        Assert.True(arm.FaceCount > 0);
    }

    // ---- the F5 view ----

    [Fact]
    public void FirstPersonIsTheDefaultAndF5FlipsTheViewBackAndForth()
    {
        var view = new ViewModeState();
        Assert.Equal(CameraMode.FirstPerson, view.Mode);
        Assert.False(view.IsThirdPerson);
        Assert.Equal(CameraMode.ThirdPerson, view.Toggle());
        Assert.True(view.IsThirdPerson);
        Assert.Equal(CameraMode.FirstPerson, view.Toggle());
        Assert.False(view.IsThirdPerson);
        view.Set(CameraMode.ThirdPerson);
        Assert.True(view.IsThirdPerson);
    }

    [Fact]
    public void TheLaunchOptionsStartInFirstPersonUnlessAskedAndSaveMovedToF6()
    {
        Assert.True(LaunchOptions.TryParse(["--seed", "42"], out var plain, out var error), error);
        Assert.False(plain.ThirdPerson);
        Assert.False(plain.Dusk);
        Assert.True(LaunchOptions.TryParse(["--third-person", "--dusk"], out var shoulder, out error), error);
        Assert.True(shoulder.ThirdPerson);
        Assert.True(shoulder.Dusk);
        Assert.Contains("F6", LaunchOptions.Usage, StringComparison.Ordinal);
        Assert.DoesNotContain("F5 writes", LaunchOptions.Usage, StringComparison.Ordinal);
        Assert.Contains("--third-person", LaunchOptions.Usage, StringComparison.Ordinal);
    }

    [Fact]
    public void TheShoulderCameraStandsBackInTheOpenAndComesInFrontOfAWall()
    {
        var head = (X: 50.5f, Y: 40.6f, Z: 50.5f);
        var forward = (X: 0f, Y: 0f, Z: -1f); // looking north
        var right = (X: 1f, Y: 0f, Z: 0f);
        var open = ThirdPersonRig.Eye(static (_, _, _) => false, head, forward, right);
        Assert.Equal(head.Z + ThirdPersonRig.Distance, open.Z, 2); // behind: south of a head that looks north
        Assert.Equal(head.X + ThirdPersonRig.Side, open.X, 2); // over the right shoulder
        Assert.Equal(head.Y + ThirdPersonRig.Rise, open.Y, 2);

        // A wall two blocks behind the player: the camera stops short of it, and never inside a solid block.
        bool Wall(int x, int y, int z) => z >= 52;
        var near = ThirdPersonRig.Eye(Wall, head, forward, right);
        Assert.True(near.Z < 52f - ThirdPersonRig.Margin + 0.2f);
        Assert.True(near.Z > head.Z);
        Assert.False(Wall((int)MathF.Floor(near.X), (int)MathF.Floor(near.Y), (int)MathF.Floor(near.Z)));

        // Boxed in: the camera is at the head.
        var boxed = ThirdPersonRig.Eye(static (_, _, _) => true, head, forward, right);
        Assert.Equal(head.X, boxed.X, 3);
        Assert.Equal(head.Z, boxed.Z, 3);
    }

    // ---- the arm ----

    [Fact]
    public void TheHandHoldsALogATorchABottleOrASmallItemAndNothingWhenTheSlotIsEmpty()
    {
        Assert.Equal(HeldKind.None, HeldItems.For(true, ItemId.Wood).Kind);
        Assert.Equal(HeldChoice.Nothing, HeldItems.For(new ItemStack()));
        Assert.Equal(HeldKind.Log, HeldItems.For(false, ItemId.Wood).Kind);
        Assert.Equal(HeldKind.Torch, HeldItems.For(false, ItemId.Torch).Kind);
        Assert.Equal(new HeldChoice(HeldKind.Bottle, BlockCell.IconBottle), HeldItems.For(false, ItemId.Bottle));
        Assert.Equal(new HeldChoice(HeldKind.Bottle, BlockCell.IconWaterBottle), HeldItems.For(false, ItemId.WaterBottle));
        Assert.Equal(new HeldChoice(HeldKind.Sprite, BlockCell.IconBerry), HeldItems.For(false, ItemId.Berry));
        Assert.Equal(new HeldChoice(HeldKind.Sprite, BlockCell.IconBread), HeldItems.For(false, ItemId.Bread));
        Assert.Equal(new HeldChoice(HeldKind.Sprite, BlockCell.IconCoin), HeldItems.For(false, ItemId.Coin)); // village-plan V05: their own pictures
        Assert.Equal(new HeldChoice(HeldKind.Sprite, BlockCell.IconApple), HeldItems.For(false, ItemId.Apple));
        Assert.Equal(new HeldChoice(HeldKind.Sprite, BlockCell.IconAxe), HeldItems.For(false, ItemId.Axe));
        Assert.Equal(new HeldChoice(HeldKind.Sprite, BlockCell.IconBook), HeldItems.For(false, ItemId.Book)); // issue #44: the hand showed nothing
        Assert.Equal(HeldKind.Torch, HeldItems.For(new ItemStack(ItemId.Torch, 1)).Kind);
    }

    [Fact]
    public void EveryItemInTheHandHasA3DModel()
    {
        // Issue #44: every item the game has is held as a model with depth, not as nothing or a flat card.
        foreach (var definition in ItemDefinition.All)
        {
            var held = HeldItems.For(false, definition.Id);
            Assert.NotEqual(HeldKind.None, held.Kind);
            var (body, _) = ArmMeshes.Item(held);
            Assert.True(body.FaceCount > 0, $"{definition.Id} has no model");
            var vertices = body.Vertices.ToArray();
            var depth = vertices.Max(static v => v.Z) - vertices.Min(static v => v.Z);
            var width = vertices.Max(static v => v.X) - vertices.Min(static v => v.X);
            var height = vertices.Max(static v => v.Y) - vertices.Min(static v => v.Y);
            Assert.True(depth > 0.01f && width > 0.05f && height > 0.05f, $"{definition.Id}: {width} x {height} x {depth}");
            Assert.True(width < 0.5f && height < 0.7f, $"{definition.Id} is too large for the hand: {width} x {height}");
        }
    }

    [Theory]
    [InlineData(BlockCell.IconApple)]
    [InlineData(BlockCell.IconCoin)]
    [InlineData(BlockCell.IconBerry)]
    [InlineData(BlockCell.IconBread)]
    [InlineData(BlockCell.IconAxe)]
    [InlineData(BlockCell.IconBook)]
    [InlineData(BlockCell.IconBottle)]
    [InlineData(BlockCell.IconFlour)]
    public void AnExtrudedIconIsOneVoxelPerSolidPixelWithSidesOnlyOnItsOutline(BlockCell cell)
    {
        const int Size = BlockAtlasLayout.CellSize;
        var pixels = BlockArt.Pixels(cell);
        int solid = 0, edges = 0;
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (!ItemModels.IsSolid(pixels, x, y))
                {
                    continue;
                }

                solid++;
                edges += new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) }.Count(n => !ItemModels.IsSolid(pixels, n.Item1, n.Item2));
            }
        }

        var buffer = new MeshBuffer();
        var voxels = ItemModels.AppendExtruded(buffer, cell, 0.02f, 0f, -1f);
        Assert.True(solid >= 12, $"{cell} has only {solid} solid pixels");
        Assert.Equal(solid, voxels);
        Assert.Equal((2 * solid) + edges, buffer.FaceCount); // a front and a back per pixel, a side per clear neighbour: no hidden faces
        var vertices = buffer.Vertices.ToArray();
        Assert.Equal(0f, vertices.Min(static v => v.Y), 4); // it rests on the base
        Assert.Equal(-0.99f, vertices.Max(static v => v.Z), 4); // one pixel thick around z
        Assert.Equal(-1.01f, vertices.Min(static v => v.Z), 4);
        Assert.Equal(-vertices.Min(static v => v.X), vertices.Max(static v => v.X), 4); // centred

        // Every face samples a texel of the icon's own cell, so the model has the icon's colours.
        var (cx, cy, w, h) = BlockAtlasLayout.PixelRectangle(cell);
        Assert.All(vertices, v =>
        {
            Assert.InRange(v.U * BlockAtlasLayout.Width, cx, cx + w);
            Assert.InRange(v.V * BlockAtlasLayout.Height, cy, cy + h);
        });
    }

    [Fact]
    public void TheTorchInTheHandHasAFlameThatIsDrawnUnlitAndTheOtherItemsHaveNone()
    {
        var torch = ArmMeshes.Item(HeldItems.For(false, ItemId.Torch));
        Assert.True(torch.Body.FaceCount > 0);
        Assert.True(torch.Glow.FaceCount > 0);
        Assert.All(torch.Glow.Vertices.ToArray(), static v => Assert.True((v.Color & 0xFF) >= 250)); // full brightness
        var log = ArmMeshes.Item(HeldItems.For(false, ItemId.Wood));
        Assert.True(log.Body.FaceCount == 6);
        Assert.Equal(0, log.Glow.FaceCount);
        var empty = ArmMeshes.Item(HeldChoice.Nothing);
        Assert.Equal(0, empty.Body.FaceCount + empty.Glow.FaceCount);
        Assert.Same(ArmMeshes.Arm(), ArmMeshes.Arm()); // built once
    }

    [Fact]
    public void TheArmIsASleeveAndAHandInThePlayersColoursWithNoOutline()
    {
        var arm = ArmMeshes.Arm();
        Assert.Equal(3 * 6, arm.FaceCount); // the sleeve, the cuff and the hand are boxes of six faces: nothing else, so no outline
        var colours = arm.Vertices.ToArray().Select(static v => (v.Color & 0xFF, (v.Color >> 8) & 0xFF, (v.Color >> 16) & 0xFF)).ToList();
        Assert.DoesNotContain(colours, static c => c.Item1 < 20 && c.Item2 < 20 && c.Item3 < 20); // no black
        // The sleeve is the blue of the player's shirt (the figure's), the hand its skin.
        Assert.Equal(NpcFigures.PartsOf(NpcFigures.PlayerName).First(static p => p.Group == FigureGroup.Body).Colour, ArmMeshes.Sleeve);
        Assert.Equal(((byte)244, (byte)200, (byte)154), ArmMeshes.SkinColour);
        Assert.Contains(colours, static c => c.Item3 > c.Item1 + 40); // blue sleeve faces
        var cells = arm.Vertices.ToArray();
        Assert.All(cells, static v => Assert.InRange(v.U, 0f, 1f));
    }

    [Fact]
    public void TheArmSwingsOnlyWhileGatheringAndOnceWhenPlacingAndSwaysWhenWalking()
    {
        var still = ArmMotion.At(100, false, 0, 1000);
        Assert.Equal(0f, still.Pitch);
        Assert.Equal(0f, still.Forward);
        Assert.Equal(0f, still.SwayX);
        var gather = Enumerable.Range(1, 40).Select(p => ArmMotion.At(100, false, p, 1000).Pitch).ToList();
        Assert.True(gather.Max() > 0.3f && gather.Min() < -0.3f, "the arm chops up and down while the button is held");
        var place = Enumerable.Range(0, ArmMotion.PlaceTicks + 4).Select(t => ArmMotion.At(100, false, 0, t)).ToList();
        Assert.Equal(0f, place[0].Pitch, 3);
        Assert.True(place[ArmMotion.PlaceTicks / 2].Pitch < -0.4f, "the dip is deepest half way");
        Assert.True(place[ArmMotion.PlaceTicks / 2].Forward > 0.05f);
        Assert.Equal(0f, place[ArmMotion.PlaceTicks].Pitch); // and it is over after PlaceTicks ticks
        Assert.Equal(0f, place[^1].Pitch);
        var walking = ArmMotion.At(37, true, 0, 1000);
        Assert.NotEqual(0f, walking.SwayY);
    }

    [Fact]
    public void TheArmIsLitByTheSceneDimmerUnderACoverAndWarmerNearALanternOrWithATorch()
    {
        var eye = (X: 20.5f, Y: 40.5f, Z: 20.5f);
        var open = ArmLight.At(static (_, _, _) => false, eye, [], false, Day, 0.5f);
        var covered = ArmLight.At((x, y, z) => y == 43, eye, [], false, Day, 0.5f);
        Assert.True(covered.R < open.R);
        Assert.Equal(ArmLight.CoveredShare, covered.R, 3);
        Assert.Equal(1f, open.R, 3);
        var lantern = new BlockPos(22, 41, 20);
        var near = ArmLight.At(static (_, _, _) => false, eye, [lantern], false, Day, 0.5f);
        Assert.True(near.R > open.R && near.B < near.R && near.G < near.R + 0.001f, "the light is warm");
        Assert.True((near.R / near.B) > (open.R / open.B));
        var far = ArmLight.At(static (_, _, _) => false, eye, [new BlockPos(60, 41, 60)], false, Day, 0.5f);
        Assert.Equal(open, far);
        var torch = ArmLight.At(static (_, _, _) => false, eye, [], true, Day, 0.5f);
        Assert.True(torch.R > torch.B);
        var dusk = ArmLight.At(static (_, _, _) => false, eye, [lantern], false, ChunkMesher.DuskLight, 1f);
        Assert.True(dusk.R / dusk.B > near.R / near.B, "at dusk the lantern's share is greater");
        var duskOpen = ArmLight.At(static (_, _, _) => false, eye, [], false, ChunkMesher.DuskLight, 1f);
        Assert.Equal(ChunkMesher.DuskLight.R, duskOpen.R, 3); // the sun's light is the look's: dimmer and redder at dusk
        Assert.Equal(ChunkMesher.DuskLight.B, duskOpen.B, 3);
    }

    // The sun's light at noon: white.
    private static readonly (float R, float G, float B) Day = (1f, 1f, 1f);

    // ---- the recipe panel ----

    [Fact]
    public void TheRecipePanelShowsTheOutputAndEachIngredientWithHaveAndNeed()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns, CreditRules.FullTyped);
        var book = session.Recipes;
        session.Simulation.GrantItems(ItemId.Wood, 1);
        var rows = CraftingScreen.Rows(session.Simulation, book);
        var model = RecipePanelModel.From(rows[book.IndexOf("antorcha")]);
        Assert.Equal(ItemId.Torch, model.Output);
        Assert.Equal(1, model.OutputCount);
        var wood = Assert.Single(model.Ingredients);
        Assert.Equal(ItemId.Wood, wood.Item);
        Assert.Equal((1, 2), (wood.Have, wood.Need));
        Assert.Equal("1/2", wood.Counts);
        Assert.False(wood.Enough);
        Assert.False(model.CanMake);

        session.Simulation.GrantItems(ItemId.Wood, 3);
        var enough = RecipePanelModel.From(CraftingScreen.Rows(session.Simulation, book)[book.IndexOf("antorcha")]);
        Assert.Equal("4/2", enough.Ingredients[0].Counts);
        Assert.True(enough.Ingredients[0].Enough);
        Assert.False(enough.CanMake); // the workbench is not in reach from here
    }

    [Fact]
    public void TheRecipePanelIsOnlyShownWhereTheStationIsInReach()
    {
        var session = RouteScene.NewSession(TestGame.Bundle, TestGame.Nouns, CreditRules.FullTyped);
        session.Simulation.GrantItems(ItemId.Wood, 2);
        var rows = CraftingScreen.Rows(session.Simulation, session.Recipes);
        Assert.Empty(RecipePanelModel.AtStation(rows, static _ => false));
        var shown = RecipePanelModel.AtStation(rows, static station => station == BlockId.Table);
        Assert.Equal(ItemId.Torch, Assert.Single(shown).Output);
    }

    // ---- the speaker's bubble ----

    [Fact]
    public void TheBubbleHangsOverTheHeadIsKeptInTheWindowAndHasNoneWhenTheHeadIsOffScreen()
    {
        var layout = DialogueBubble.Place(1280, 720, 640, 300, 200, 70);
        Assert.NotNull(layout);
        Assert.Equal(540, layout.Value.Box.X); // centred over the head
        Assert.Equal(300 - DialogueBubble.TailHeight, layout.Value.Box.Bottom); // the pointer fills the gap down to the head
        Assert.Equal((640, 300), (layout.Value.TailX, layout.Value.TailY));
        Assert.Equal(640, layout.Value.TailBaseX);

        var left = DialogueBubble.Place(1280, 720, 20, 300, 200, 70)!.Value;
        Assert.Equal(DialogueBubble.Margin, left.Box.X); // moved in from the edge
        Assert.Equal(20, left.TailX); // the pointer still points at the head
        Assert.InRange(left.TailBaseX, left.Box.X + 16, left.Box.Right - 16);
        var right = DialogueBubble.Place(1280, 720, 1270, 300, 200, 70)!.Value;
        Assert.Equal(1280 - DialogueBubble.Margin, right.Box.Right);

        var high = DialogueBubble.Place(1280, 720, 640, 40, 200, 70)!.Value;
        Assert.Equal(DialogueBubble.Margin, high.Box.Y); // never above the top of the window

        Assert.Null(DialogueBubble.Place(1280, 720, -30, 300, 200, 70));
        Assert.Null(DialogueBubble.Place(1280, 720, 640, 800, 200, 70));
    }

    [Fact]
    public void TheBubbleShowsTheSpeakersNewestLineCutToThreeRows()
    {
        var dialogue = new DialogueModel(new InputFocus(), "¡Hola! Soy la guía.", RouteScene.Shipped);
        Assert.Null(DialogueBubble.LineOf(dialogue)); // nothing said yet
        dialogue.Open();
        Assert.Equal("¡Hola! Soy la guía.", DialogueBubble.LineOf(dialogue));
        var rows = DialogueBubble.Rows(string.Join(' ', Enumerable.Repeat("palabra", 60)), 150, static t => t.Length * 8f);
        Assert.Equal(DialogueBubble.MaxRows, rows.Count);
        Assert.EndsWith("…", rows[^1], StringComparison.Ordinal);
        Assert.Single(DialogueBubble.Rows("¡Hola!", 150, static t => t.Length * 8f));
    }
}
