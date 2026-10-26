using System.Text.Json;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Nlp.Language.Normalization;
using Xunit.Abstractions;

namespace Vocaterra.Game.Core.Tests;

/// <summary>
/// One typed turn with the guide on seed 42 and the fixed stored time: the core's verdict, the guide's state, the host event, the target
/// tile and the world-fact line.
/// </summary>
public sealed class GuideTurnTests(ITestOutputHelper output)
{
    [Fact]
    public void AskingWhereTheTreeIsPointsAtTheNearestTrunk()
    {
        var session = TestGame.NewSession();
        var player = session.Simulation.Player.FeetBlock;

        var turn = session.Say("¿dónde está el árbol?");

        output.WriteLine($"player block {player}, guide {session.Scene.Npc("guide").Feet}");
        foreach (var line in turn.GuideLines)
        {
            output.WriteLine("guide: " + line);
        }

        Assert.Equal("ask_directions", turn.Intent);
        Assert.True(turn.Communicated);
        Assert.Empty(turn.Errors);
        Assert.Null(turn.Feedback);
        Assert.Equal("pointing", turn.AgentState);
        Assert.Equal(["guide.point"], turn.HostEvents);

        // The árbol names the trunk and the leaves of a tree of any species (S14): the nearest of either is the answer.
        var expected = TestGame.NearestBlock(session, BlockId.Trunk, BlockId.Leaves, BlockId.SpruceTrunk, BlockId.SpruceLeaves, BlockId.BirchTrunk, BlockId.BirchLeaves);
        Assert.NotNull(turn.Pointer);
        Assert.Equal(expected, turn.Pointer.Target);
        Assert.Contains(session.Scene.Grid.GetBlock(expected.X, expected.Y, expected.Z), new[] { BlockId.Trunk, BlockId.Leaves, BlockId.SpruceTrunk, BlockId.SpruceLeaves, BlockId.BirchTrunk, BlockId.BirchLeaves });
        Assert.Equal("árbol", turn.Pointer.Place);
        Assert.Equal(new PointerMarker("árbol", expected), session.Pointer);

        var dx = expected.X - player.X;
        var dz = expected.Z - player.Z;
        Assert.Equal(WorldFacts.BlocksOf((long)((dx * dx) + (dz * dz)) * VoxelPhysics.UnitsPerBlock * VoxelPhysics.UnitsPerBlock), turn.Pointer.Blocks); // the horizontal distance, rounded
        Assert.NotNull(turn.WorldLine);
        Assert.StartsWith("El árbol está", turn.WorldLine, StringComparison.Ordinal);
        Assert.Contains(turn.Pointer.Direction, turn.WorldLine, StringComparison.Ordinal); // the direction is relative to where the player faces
        Assert.Contains(SpanishNumbers.ToWords(turn.Pointer.Blocks), turn.WorldLine, StringComparison.Ordinal);
        Assert.Contains("bloque", turn.WorldLine, StringComparison.Ordinal);
    }

    [Fact]
    public void TheWorldLineOnSeedFortyTwoIsPinned()
    {
        // The stored time, the seed, the world and the interaction counter fix the variant the game draws, so this exact line is the
        // regression anchor for the content and the drawing rule: change the pool, the stream id or the world and it changes on purpose.
        var turn = TestGame.NewSession().Say("¿dónde está el árbol?");

        Assert.Equal("El árbol está allí, adelante, a doce bloques.", turn.WorldLine);
    }

    [Theory]
    // The unaccented lines read "esta" as estar (CR-013, core M26b): the game's bundle has no entry for it.
    [InlineData("dónde esta el arbol")]
    [InlineData("donde esta el arbol")]
    [InlineData("DÓNDE ESTÁ EL ÁRBOL")]
    [InlineData("dónde está el árbol")]
    public void AccentsAndCapitalsDoNotStopTheGuideUnderstanding(string line)
    {
        var accented = TestGame.NewSession().Say("¿dónde está el árbol?");
        var turn = TestGame.NewSession().Say(line);

        Assert.Equal("ask_directions", turn.Intent);
        Assert.True(turn.Communicated);
        Assert.Empty(turn.Errors);
        Assert.Null(turn.Feedback);
        Assert.Equal(accented.Pointer, turn.Pointer);
        Assert.Equal(accented.WorldLine, turn.WorldLine);
    }

    [Fact]
    public void ANonsenseLineGetsTheRepairPathAndNoArrow()
    {
        var session = TestGame.NewSession();

        var first = session.Say("blah blah");

        Assert.False(first.Communicated);
        Assert.Equal("unknown", first.Intent);
        Assert.Equal("idle", first.AgentState);
        Assert.Empty(first.HostEvents);
        Assert.Null(first.WorldLine);
        Assert.Null(first.Pointer);
        Assert.Null(session.Pointer);
        Assert.Equal("¿Cómo?", first.Repair);
        Assert.Single(first.Replies);
        Assert.NotEmpty(first.Errors);

        // The repeat counts are carried from turn to turn, so the repair ladder moves on (CR-012, core M26a): ask again, say it slowly,
        // then offer choices.
        var second = session.Say("blah blah");
        Assert.NotEqual(first.Repair, second.Repair);
        Assert.StartsWith("Perdón, te lo repito", second.Repair, StringComparison.Ordinal);
        Assert.Equal(2, session.Conversation.Context.RepeatCountsByTag["not_communicated"]);
        var third = session.Say("blah blah");
        Assert.StartsWith("¿Quieres decir", third.Repair, StringComparison.Ordinal);
        Assert.Equal(3, session.Conversation.Context.RepeatCountsByTag["not_communicated"]);

        // A line the guide understands starts the count again.
        session.Say("hola");
        Assert.Empty(session.Conversation.Context.RepeatCountsByTag);
    }

    [Fact]
    public void TheSameMistakeTwiceInARowGetsAHintInsteadOfTheSameRecast()
    {
        var session = TestGame.NewSession();

        // The guide's ladder (the core counts each utterance once, M26a): recast, then a hint, then a hint again.
        var first = session.Say("dónde está la árbol");
        var second = session.Say("dónde está la árbol");
        var third = session.Say("dónde está la árbol");

        Assert.Contains("Dónde está el árbol", first.Feedback, StringComparison.Ordinal);
        Assert.NotEqual(first.Feedback, second.Feedback);
        Assert.DoesNotContain("el árbol", second.Feedback, StringComparison.Ordinal); // a hint points at the word, it does not give the form
        Assert.DoesNotContain("el árbol", third.Feedback, StringComparison.Ordinal);
        Assert.Equal(3, session.Conversation.Context.RepeatCountsByTag["gender.agree"]);
    }

    [Fact]
    public void AMistakeStillGetsAnAnswerAndTheCorrectionComesFirst()
    {
        var session = TestGame.NewSession();

        var turn = session.Say("dónde está la árbol");

        Assert.True(turn.Communicated);
        Assert.Equal("ask_directions", turn.Intent);
        Assert.Contains(turn.Errors, e => e.Tag == "gender.agree");
        Assert.NotNull(turn.Feedback);
        Assert.Contains("el árbol", turn.Feedback, StringComparison.Ordinal);
        Assert.NotNull(turn.Pointer);
        Assert.Equal(turn.Feedback, turn.GuideLines[0]);
        Assert.Equal(turn.WorldLine, turn.GuideLines[^1]);
        Assert.True(turn.Correctness < 1.0);
    }

    [Fact]
    public void AskingForStoneFindsStoneBelowTheGround()
    {
        var session = TestGame.NewSession();

        var turn = session.Say("dónde hay piedra");

        Assert.Equal("ask_find", turn.Intent); // "dónde hay" is the material question since P6b; stone is its own source
        Assert.Equal("piedra", turn.Pointer!.Place);
        Assert.NotNull(turn.Pointer);
        var expected = TestGame.NearestBlock(session, BlockId.Stone);
        Assert.Equal(expected, turn.Pointer.Target);
        Assert.Contains("piedra", turn.WorldLine, StringComparison.Ordinal); // the nearest stone is under the ground, so the guide may say it is here
        Assert.Contains(turn.Pointer.Direction, turn.WorldLine, StringComparison.Ordinal);
    }

    [Fact]
    public void ANounTheWorldDoesNotHaveYetGetsAnHonestNo()
    {
        var session = TestGame.NewSession();

        var turn = session.Say("dónde está la cueva");

        Assert.Equal("ask_directions", turn.Intent);
        Assert.True(turn.Communicated);
        Assert.Null(turn.Pointer);
        Assert.Null(session.Pointer);
        Assert.Contains("una cueva", turn.WorldLine, StringComparison.Ordinal);
        Assert.Equal(["guide.point"], turn.HostEvents);
    }

    [Fact]
    public void TheGuideStateMachineMovesWithTheConversation()
    {
        var session = TestGame.NewSession();
        Assert.Equal("idle", session.Conversation.GuideState.StateId);

        Assert.Equal("talking", session.Say("hola").AgentState);
        Assert.Equal("pointing", session.Say("dónde está el árbol").AgentState);
        Assert.Equal("talking", session.Say("gracias").AgentState);
        Assert.Equal("talking", session.Say("ayuda").AgentState);
        Assert.Equal("idle", session.Say("adiós").AgentState);
        Assert.Equal(5, session.Conversation.GuideState.InteractionCounter);
    }

    [Fact]
    public void HelpChangesWhileAnArrowIsOut()
    {
        var session = TestGame.NewSession();

        var before = session.Say("ayuda");
        Assert.Equal(["guide.help"], before.HostEvents);
        Assert.StartsWith("Pregunta", before.WorldLine, StringComparison.Ordinal);

        session.Say("dónde está el árbol");
        var during = session.Say("qué hago");
        Assert.Equal("ask_help", during.Intent);
        Assert.Contains("flecha", during.WorldLine, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCursorTileIsNamedWhenTheGuideIsAskedWhatThisIs()
    {
        var session = TestGame.NewSession();
        var trunk = TestGame.NearestBlock(session, BlockId.Trunk);
        var stone = TestGame.NearestBlock(session, BlockId.Stone);

        var onTrunk = session.Say("¿qué es esto?", trunk);
        Assert.Equal("ask_what", onTrunk.Intent);
        Assert.Equal(["guide.identify"], onTrunk.HostEvents);
        Assert.Equal("árbol", onTrunk.IdentifiedNoun);
        Assert.EndsWith("un árbol.", onTrunk.WorldLine, StringComparison.Ordinal);

        var onStone = session.Say("qué es eso", stone);
        Assert.Equal("piedra", onStone.IdentifiedNoun);
        Assert.EndsWith("una piedra.", onStone.WorldLine, StringComparison.Ordinal);

        var onNothing = session.Say("cómo se llama esto");
        Assert.Equal("ask_what", onNothing.Intent);
        Assert.Null(onNothing.IdentifiedNoun);
        Assert.NotNull(onNothing.WorldLine);
    }

    [Fact]
    public void AUsedNounIsExercisedAsAWordSkillInTheSingleLearnerUpdate()
    {
        // CR-005 (core M26d): the nouns a turn uses join the one learner update as word.<lemma> skills; nothing else is stepped for them.
        var session = TestGame.NewSession();

        session.Say("hola");
        Assert.Null(session.Conversation.Context.Learner.Find("word.árbol"));

        session.Say("¿dónde está el árbol?");
        Assert.NotNull(session.Conversation.Context.Learner.Find("word.árbol"));
        Assert.NotNull(session.Conversation.Context.Learner.Find("intent.ask_directions"));
    }

    [Fact]
    public void ThePromptTextIsTheGuidesOpeningLine()
    {
        var session = TestGame.NewSession();

        Assert.Equal("¡Hola! Soy la guía. ¿En qué te ayudo?", session.OpeningLine);
    }

    [Fact]
    public void TheSameInputsGiveTheSameTurnsTwice()
    {
        var lines = new[]
        {
            "hola", "¿dónde está el árbol?", "dónde esta la árbol", "blah blah", "ayuda", "¿qué es esto?", "dónde hay piedra", "gracias", "adiós",
        };

        var first = RunConversation(lines);
        var second = RunConversation(lines);

        Assert.Equal(first.Json, second.Json);
        Assert.Equal(first.ContextHash, second.ContextHash);
    }

    [Fact]
    public void ADifferentSeedChangesTheWorldAndSoTheAnswer()
    {
        var a = TestGame.NewSession(42).Say("¿dónde está el árbol?");
        var b = TestGame.NewSession(7).Say("¿dónde está el árbol?");

        Assert.NotNull(a.Pointer);
        Assert.NotNull(b.Pointer);
        Assert.NotEqual(a.Pointer.Target, b.Pointer.Target);
    }

    [Fact]
    public void TheCoreIsGivenTheSimulationsStateDigestAsTheHostStateHash()
    {
        var session = TestGame.NewSession();
        for (var i = 0; i < 90; i++)
        {
            session.Step([VoxelCommand.Move(1, 0)]);
        }

        var digest = session.Simulation.ComputeStateDigest();
        var first = session.Say("blah"); // a line the guide does not understand carries no skills and no facts: the bare digest
        Assert.Equal(digest, first.HostStateHash);

        session.Step([]);
        var second = session.Say("blah");
        Assert.Equal(session.Simulation.ComputeStateDigest(), second.HostStateHash);
        Assert.NotEqual(first.HostStateHash, second.HostStateHash);
    }

    [Fact]
    public void ATurnWithHostFactsOrSkillsHashesThemWithTheSimulationDigest()
    {
        // The second evaluation's digest covers the host slots and the word and phrase skills (HostDigest); a turn with neither keeps the bare digest.
        var session = TestGame.NewSession();
        var digest = session.Simulation.ComputeStateDigest();

        var unclear = session.Say("blah");
        Assert.Equal(digest, unclear.HostStateHash);
        var greeting = session.Say("hola"); // the sheet's phrase "Hola." is in play (S8): its skill is part of the digest
        Assert.Equal(HostDigest.Of(digest, new Dictionary<string, string>(), ["phrase.hola"]), greeting.HostStateHash);

        var point = session.Say("¿dónde está el árbol?");
        Assert.NotEqual(digest, point.HostStateHash);
        Assert.Equal(64, point.HostStateHash.Length);
    }

    [Fact]
    public void TheStoredTimeIsTheStartPlusTheTicksAndNeverTheClock()
    {
        var session = TestGame.NewSession();
        Assert.Equal(GuideSession.DefaultStartedAt, session.StoredTime.UnixSeconds);

        for (var i = 0; i < 125; i++)
        {
            session.Step([]);
        }

        Assert.Equal(GuideSession.DefaultStartedAt + 2, session.StoredTime.UnixSeconds);
        session.Say("hola");
        Assert.Equal(session.StoredTime, session.Conversation.Context.Now);
    }

    [Fact]
    public void ATurnTakesPartOfAFrameAndNeverTheSimulationTick()
    {
        var session = TestGame.NewSession();
        session.Say("hola"); // warm up the pipeline's first-call costs
        var ticks = session.Simulation.Tick;

        var watch = System.Diagnostics.Stopwatch.StartNew();
        const int turns = 20;
        for (var i = 0; i < turns; i++)
        {
            session.Say(i % 2 == 0 ? "¿dónde está el árbol?" : "dónde hay piedra");
        }

        watch.Stop();
        output.WriteLine($"{turns} turns in {watch.Elapsed.TotalMilliseconds:0.0} ms, {watch.Elapsed.TotalMilliseconds / turns:0.00} ms per turn");
        Assert.Equal(ticks, session.Simulation.Tick);
        Assert.True(watch.Elapsed.TotalMilliseconds / turns < 250, "A turn should be far below a quarter of a second.");
    }

    private static (string Json, string ContextHash) RunConversation(IEnumerable<string> lines)
    {
        var session = TestGame.NewSession();
        var turns = new List<GuideTurn>();
        var cursor = TestGame.NearestBlock(session, BlockId.Trunk);
        foreach (var line in lines)
        {
            turns.Add(session.Say(line, cursor));
            for (var i = 0; i < 30; i++)
            {
                session.Step([VoxelCommand.Move(1, 0)]);
            }
        }

        return (JsonSerializer.Serialize(turns), session.Conversation.Context.HostStateHash);
    }
}
