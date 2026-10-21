using System.Text.Json;
using Vocaterra.Game;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Tiles;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests;

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
        var player = session.PlayerTile;

        var turn = session.Say("¿dónde está el árbol?");

        output.WriteLine($"player tile {player}, guide {session.Guide.Feet}");
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

        var expected = TestGame.BruteForceNearest(session.Simulation.World, player, TileId.Trunk);
        Assert.NotNull(turn.Pointer);
        Assert.Equal(expected, turn.Pointer.Target);
        Assert.Equal(TileId.Trunk, session.Simulation.World.GetTile(expected.X, expected.Y));
        Assert.Equal("árbol", turn.Pointer.Place);
        Assert.Equal(new PointerMarker("árbol", expected), session.Pointer);

        var dx = expected.X - player.X;
        var dy = expected.Y - player.Y;
        Assert.Equal(Math.Max(Math.Abs(dx), Math.Abs(dy)), turn.Pointer.Blocks);
        Assert.Equal(WorldFacts.DirectionPhrase(dx, dy), turn.Pointer.Direction);
        Assert.NotNull(turn.WorldLine);
        Assert.StartsWith("El árbol está", turn.WorldLine, StringComparison.Ordinal);
        Assert.Contains(dx < 0 ? "izquierda" : "derecha", turn.WorldLine, StringComparison.Ordinal);
        Assert.Contains(WorldFacts.NumberInWords(turn.Pointer.Blocks), turn.WorldLine, StringComparison.Ordinal);
        Assert.Contains("bloque", turn.WorldLine, StringComparison.Ordinal);
    }

    [Fact]
    public void TheWorldLineOnSeedFortyTwoIsPinned()
    {
        // The stored time, the seed, the world and the interaction counter fix the variant the game draws, so this exact line is the
        // regression anchor for the content and the drawing rule: change the pool, the stream id or the world and it changes on purpose.
        var turn = TestGame.NewSession().Say("¿dónde está el árbol?");

        Assert.Equal("El árbol está allí, a la derecha, a cinco bloques.", turn.WorldLine);
    }

    [Theory]
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

        // The repeat counts are carried from turn to turn, so the repair ladder moves on (CR-012's workaround): ask again, say it slowly,
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

        var first = session.Say("dónde está la árbol");
        var second = session.Say("dónde está la árbol");

        Assert.Contains("Dónde está el árbol", first.Feedback, StringComparison.Ordinal);
        Assert.NotEqual(first.Feedback, second.Feedback);
        Assert.DoesNotContain("el árbol", second.Feedback, StringComparison.Ordinal); // a hint points at the word, it does not give the form
        Assert.Equal(2, session.Conversation.Context.RepeatCountsByTag["gender.agree"]);
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

        Assert.Equal("ask_directions", turn.Intent);
        Assert.NotNull(turn.Pointer);
        var expected = TestGame.BruteForceNearest(session.Simulation.World, session.PlayerTile, TileId.Stone);
        Assert.Equal(expected, turn.Pointer.Target);
        Assert.StartsWith("La piedra está", turn.WorldLine, StringComparison.Ordinal);
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
        var trunk = TestGame.BruteForceNearest(session.Simulation.World, session.PlayerTile, TileId.Trunk);
        var stone = TestGame.BruteForceNearest(session.Simulation.World, session.PlayerTile, TileId.Stone);

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
            session.Step([PlayerCommand.MoveRight]);
        }

        var digest = session.Simulation.ComputeStateDigest();
        var first = session.Say("hola");
        Assert.Equal(digest, first.HostStateHash);

        session.Step([]);
        var second = session.Say("hola");
        Assert.Equal(session.Simulation.ComputeStateDigest(), second.HostStateHash);
        Assert.NotEqual(first.HostStateHash, second.HostStateHash);
    }

    [Fact]
    public void TheStoredTimeIsTheStartPlusTheTicksAndNeverTheClock()
    {
        var session = TestGame.NewSession();
        Assert.Equal(GameSession.DefaultStartedAt, session.StoredTime.UnixSeconds);

        for (var i = 0; i < 125; i++)
        {
            session.Step([]);
        }

        Assert.Equal(GameSession.DefaultStartedAt + 2, session.StoredTime.UnixSeconds);
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
        var cursor = TestGame.BruteForceNearest(session.Simulation.World, session.PlayerTile, TileId.Trunk);
        foreach (var line in lines)
        {
            turns.Add(session.Say(line, cursor));
            for (var i = 0; i < 30; i++)
            {
                session.Step([PlayerCommand.MoveRight]);
            }
        }

        return (JsonSerializer.Serialize(turns), session.Conversation.Context.HostStateHash);
    }
}
