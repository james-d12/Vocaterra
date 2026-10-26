using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Input;
using Vocaterra.Game.UI;
using Vocaterra.Game.Utilities;

namespace Vocaterra.Game.Tests;

/// <summary>The front-end pieces of S6: the talk steps of the command line, the panel that shows an NPC's name and lines, aiming at every NPC, and the interact step on water.</summary>
public sealed class NpcFrontendTests
{
    private static LaunchOptions Parse(params string[] args)
    {
        Assert.True(LaunchOptions.TryParse(args, out var options, out var error), error);
        return options;
    }

    private static string Refuse(params string[] args)
    {
        Assert.False(LaunchOptions.TryParse(args, out _, out var error));
        return error;
    }

    [Fact]
    public void TheTalkStepsKeepTheOrderTheyWereGivenWithTheBodysSteps()
    {
        var options = Parse(
            "--screenshot", "end.png", "--walk-to", "60,52", "--talk", "shopkeeper", "--say", "quiero agua, por favor", "--shot", "stall.png", "--say", "sí, por favor",
            "--leave", "--interact", "61,7,80", "--talk", "guide");

        Assert.Equal(
            [ScriptStepKind.Move, ScriptStepKind.Talk, ScriptStepKind.Say, ScriptStepKind.Shot, ScriptStepKind.Say, ScriptStepKind.Leave, ScriptStepKind.Move, ScriptStepKind.Talk],
            options.Script.Select(static s => s.Kind));
        Assert.Equal("shopkeeper", options.Script[1].Value);
        Assert.Equal("quiero agua, por favor", options.Script[2].Value);
        Assert.Equal("stall.png", options.Script[3].Value);
        Assert.Equal((61, 7, 80), (options.Script[6].Step!.X, options.Script[6].Step!.Y, options.Script[6].Step!.Z));
        Assert.Equal(["quiero agua, por favor", "sí, por favor"], options.Says.ToArray()); // the old list is still there
        Assert.Equal(2, options.VoxelSteps.Count);
    }

    [Fact]
    public void ASayAloneIsStillTheS3DemoAndTheTalkStepsAreChecked()
    {
        var s3 = Parse("--seed", "42", "--look", "120,-5", "--say", "¿dónde está el río?", "--screenshot", "s3.png", "--frames", "300");
        Assert.Equal([ScriptStepKind.Move, ScriptStepKind.Say], s3.Script.Select(static s => s.Kind));

        Assert.Contains("--talk needs an NPC", Refuse("--screenshot", "a.png", "--talk", "dragon"), StringComparison.Ordinal);
        Assert.Contains("need --screenshot", Refuse("--talk", "guide"), StringComparison.Ordinal);
        Assert.Contains("need --screenshot", Refuse("--leave"), StringComparison.Ordinal);
        Assert.Contains("--shot needs", Refuse("--screenshot", "a.png", "--shot", ""), StringComparison.Ordinal);
    }

    [Fact]
    public void ThePanelShowsTheNpcsNameItsHostTurnLinesAndAFollowUp()
    {
        var focus = new InputFocus();
        var dialogue = new DialogueModel(focus, "¡Hola! Soy la guía.");
        var opening = new NpcOpening("shopkeeper", "stall-need", ["¿Qué necesitas?"], true, [], false);

        dialogue.Open(opening, "Tendero");

        Assert.True(dialogue.IsOpen);
        Assert.True(focus.TextEntryActive);
        Assert.Equal("Tendero", dialogue.NpcName);
        Assert.Equal("shopkeeper", dialogue.Npc);
        Assert.Equal([new DialogueLine(DialogueSpeaker.Guide, "¿Qué necesitas?")], dialogue.Lines);

        dialogue.Close();
        dialogue.Open(opening, "Tendero"); // a host turn opens with its line every time
        Assert.Equal(2, dialogue.Lines.Count);
    }

    [Fact]
    public void APromptOpeningIsShownOnceAndAnotherNpcStartsACleanPage()
    {
        var dialogue = new DialogueModel(new InputFocus(), "¡Hola! Soy la guía.");
        var villager = new NpcOpening("villager", "villager-open", ["Hola."], false, [], false);

        dialogue.Open(villager, "Aldeano");
        dialogue.Close();
        dialogue.Open(villager, "Aldeano");
        Assert.Equal([new DialogueLine(DialogueSpeaker.Guide, "Hola.")], dialogue.Lines); // the prompt's text once

        dialogue.Close();
        dialogue.Open(); // the guide: her own page, her own first line
        Assert.Equal("Guía", dialogue.NpcName);
        Assert.Equal([new DialogueLine(DialogueSpeaker.Guide, "¡Hola! Soy la guía.")], dialogue.Lines);
    }

    [Fact]
    public void ComingBackToAnNpcAfterAnotherOneStillShowsItsPromptOnAnEmptyPage()
    {
        var dialogue = new DialogueModel(new InputFocus(), "hola");
        var villager = new NpcOpening("villager", "villager-open", ["Hola."], false, [], false);
        var offer = new NpcOpening("guide", "guide-offer", ["¿Quieres agua?"], false, [], false);
        dialogue.Open(villager, "Aldeano");
        dialogue.Close();
        dialogue.Open(offer, "Guía"); // another page: the villager's is gone
        dialogue.Close();

        dialogue.Open(villager, "Aldeano");

        Assert.Equal([new DialogueLine(DialogueSpeaker.Guide, "Hola.")], dialogue.Lines);
    }

    [Fact]
    public void ATurnsFollowUpLinesAreShownAfterTheRest()
    {
        var dialogue = new DialogueModel(new InputFocus(), "hola");
        dialogue.Open(new NpcOpening("shopkeeper", "stall-need", ["¿Qué necesitas?"], true, [], false), "Tendero");
        var turn = new GuideTurn(
            "no sé",
            "dont_know",
            true,
            1.0,
            [],
            null,
            ["Puedes decir: «Quiero agua, por favor»."],
            null,
            "offering",
            ["stall.give_water"],
            null,
            null,
            null,
            "digest",
            Vocaterra.Nlp.Core.Verdicts.SlotMap.Empty,
            Npc: "shopkeeper",
            Prompt: "stall-need",
            FollowUp: ["Toma la botella. El agua está en el río.", "¿Quieres pan?"],
            FailForward: true);
        var applied = new AppliedTurn(new TurnOutcome(new TurnRequest(1, TurnSource.Typed, "no sé", null, null, new GuideScene(default(GuideViewpoint)), new Vocaterra.Nlp.Core.Determinism.StoredTimestamp(0), "d", null, 0, "shopkeeper"), turn, null, new TurnTimings(0, 0, 0, false)), null);

        dialogue.Complete(applied);

        var texts = dialogue.Lines.Select(static l => l.Text).ToArray();
        Assert.Equal("Puedes decir: «Quiero agua, por favor».", texts[^3]);
        Assert.Equal("Toma la botella. El agua está en el río.", texts[^2]);
        Assert.Equal("¿Quieres pan?", texts[^1]);
    }

    [Fact]
    public void EveryFigureCanBeAimedAtAndTalkedToNotOnlyTheGuide()
    {
        var scene = RouteScene.Clearing();
        var figures = NpcFigures.FromScene(scene);
        Assert.Equal(["guide", "shopkeeper", "villager"], figures.Select(static f => f.Name).Order(StringComparer.Ordinal));
        foreach (var figure in figures)
        {
            var chest = NpcAim.BodyOf(figure.Feet);
            var eye = (x: chest.X, y: chest.Y + 0.42, z: chest.Z + 2.0); // two blocks south of the figure at eye height (the stand-off of a player who comes from the south)
            const int north = 0; // yaw 0 looks north, towards -z
            const int east = Angles.StepsPerTurn / 4;
            var pitch = (int)Math.Round(Math.Atan2(-0.42, 2.0) * Angles.StepsPerTurn / (2 * Math.PI));
            Assert.True(NpcAim.Hits(eye, north, pitch, chest), $"{figure.Name}: the crosshair on the chest hits");
            Assert.False(NpcAim.Hits(eye, east, 0, chest), $"{figure.Name}: looking east misses");
        }

        Assert.Equal("Tendero", NpcNames.LabelOf("shopkeeper"));
        Assert.Equal("Aldeano", NpcNames.LabelOf("villager"));
        Assert.Equal("Guía", NpcNames.LabelOf("guide"));
    }

    [Fact]
    public void TheInteractButtonWorksOnWaterAndStillOnGatherables()
    {
        var water = new BlockRayHit(new BlockPos(61, 7, 80), BlockId.Water, BlockFace.Up, new BlockPos(61, 8, 80), 3000);
        Assert.Equal(new BlockPos(61, 7, 80), LiveVoxelInput.GatherFor(water));
        var stone = new BlockRayHit(new BlockPos(61, 7, 80), BlockId.Stone, BlockFace.Up, new BlockPos(61, 8, 80), 3000);
        Assert.Null(LiveVoxelInput.GatherFor(stone));
        var trunk = new BlockRayHit(new BlockPos(5, 11, 5), BlockId.Trunk, BlockFace.South, new BlockPos(5, 11, 6), 3000);
        Assert.Equal(new BlockPos(5, 11, 5), LiveVoxelInput.GatherFor(trunk));
    }

    // A cell on the bank next to a water block: the top of a neighbouring dry column.
    private static BlockPos BesideWater(Scene scene, BlockPos water)
    {
        var grid = scene.Grid;
        foreach (var (dx, dz) in new[] { (0, -1), (-1, 0), (1, 0), (0, 1) })
        {
            var x = water.X + dx;
            var z = water.Z + dz;
            for (var y = 20; y >= 0; y--)
            {
                if (grid.IsSolid(x, y, z))
                {
                    if (grid.GetBlock(x, y, z) != BlockId.Water && y >= water.Y && !grid.IsSolid(x, y + 1, z) && !grid.IsSolid(x, y + 2, z))
                    {
                        return new BlockPos(x, y + 1, z);
                    }

                    break;
                }
            }
        }

        throw new InvalidOperationException("no bank beside that water");
    }

    private static (VoxelSimulation Sim, List<string> Log, VoxelScript Script) AtTheWater(Func<BlockPos, VoxelStep[]> steps)
    {
        var scene = RouteScene.Clearing();
        var sim = VoxelSimulation.Create(scene);
        var river = scene.Point("river");
        var water = river;
        while (scene.Grid.GetBlock(water.X - 1, water.Y, water.Z) == BlockId.Water)
        {
            water = water with { X = water.X - 1 }; // the water at the lake's western edge, with a bank beside it
        }

        Assert.Equal(BlockId.Water, scene.Grid.GetBlock(water.X, water.Y, water.Z));
        sim.TeleportTo(BesideWater(scene, water));
        var log = new List<string>();
        var look = new LookControl();
        var script = new VoxelScript(sim, look, steps(water), static _ => null, log.Add);
        return (sim, log, script);
    }

    private static int Run(VoxelSimulation sim, VoxelScript script)
    {
        var commands = new List<VoxelCommand>();
        var ticks = 0;
        while (!script.IsDone && ticks < 2000)
        {
            script.Fill(commands);
            sim.Step(commands);
            ticks++;
        }

        Assert.True(script.IsDone);
        return ticks;
    }

    [Fact]
    public void TheInteractStepFillsABottleAtTheWaterAndSaysSo()
    {
        var (sim, log, script) = AtTheWater(static w => [VoxelStep.Interact(w.X, w.Y, w.Z)]);
        sim.GrantItems(ItemId.Bottle, 1);

        Run(sim, script);

        Assert.Equal(1, sim.Inventory.Count(ItemId.WaterBottle));
        Assert.Contains(log, static l => l.Contains("filled the bottle in 30 ticks", StringComparison.Ordinal));
    }

    [Fact]
    public void TheInteractStepAtTheWaterWithoutABottleEndsWithTheHintAndBlocksNothing()
    {
        var (sim, log, script) = AtTheWater(static w => [VoxelStep.Interact(w.X, w.Y, w.Z)]);

        var ticks = Run(sim, script);

        Assert.InRange(ticks, 2, 5);
        Assert.Contains(log, static l => l.Contains("no bottle in the inventory", StringComparison.Ordinal));
        Assert.Equal(0, sim.Inventory.Count(ItemId.WaterBottle));
    }
}
