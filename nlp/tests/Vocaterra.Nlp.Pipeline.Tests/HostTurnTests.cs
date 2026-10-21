using System.Text.Json.Nodes;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>Tests for host-initiated agent turns (CR-015): <see cref="UtterancePipeline.Advance"/> and the transition trigger it uses.</summary>
public sealed class HostTurnTests : IDisposable
{
    private const string Event = "guide.ask";

    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("vocaterra-host-turn-");
    private readonly ContentBundle _bundle;
    private readonly UtterancePipeline _pipeline;

    public HostTurnTests()
    {
        _bundle = FixtureBundle.Create(
            _directory,
            pools =>
            {
                TwoPhaseTests.SetVariants(pools, "guide_ask", "¿Qué es {target:article(def)}?", "¿Y {target:article(def)}, qué es?");
                TwoPhaseTests.SetVariants(pools, "guide_hint", "Mira bien.", "Piénsalo.", "Con calma.");
            },
            blacksmith =>
            {
                var variantPools = blacksmith["variantPools"]!.AsArray();
                variantPools.Add(new JsonObject { ["id"] = "guide_ask_v", ["count"] = 2 });
                var idle = blacksmith["states"]!.AsArray().Single(static s => s!["id"]!.GetValue<string>() == "idle")!.AsObject();
                var transitions = idle["transitions"]!.AsArray();
                transitions.Insert(0, JsonNode.Parse("""
                    {"onHostEvent":"guide.ask","to":"trading","actions":[
                      {"dialogueIntent":"guide_ask","hostEvent":"ui.ask","variantPoolId":"guide_ask_v"},
                      {"dialogueIntent":"guide_hint"}]}
                    """));
            });
        var services = new PipelineServices(new ScriptedWordRecognizer(TestSupport.Asr(TestSupport.WorkedExample)), null, null, null, _bundle);
        _pipeline = new UtterancePipeline(services);
    }

    public void Dispose()
    {
        _directory.Delete(recursive: true);
    }

    private static ConversationContext Idle(long counter = 0, SlotMap? slots = null)
    {
        return ConversationContext.Create(
            42,
            "blacksmith",
            new AgentState("blacksmith", "idle", counter),
            new StoredTimestamp(1_700_000_000),
            Vocaterra.Nlp.Decision.Learning.LearnerModel.New(),
            new Dictionary<string, int> { ["conj.person"] = 2 },
            slots ?? SlotMap.From([new KeyValuePair<string, string>("target", "arena")]));
    }

    [Fact]
    public void AnEventTransitionSpeaksItsLinesAndMovesTheState()
    {
        var turn = _pipeline.Advance(Event, Idle());

        Assert.NotNull(turn);
        Assert.Equal("trading", turn.Agent.Next.StateId);
        Assert.Equal(1, turn.Agent.Next.InteractionCounter);
        Assert.Equal(["guide_ask", "guide_hint"], turn.Agent.Actions.Select(static a => a.DialogueIntent).ToArray());
        Assert.Equal("ui.ask", turn.Agent.Actions[0].HostEvent);
        Assert.Equal(2, turn.Response.Lines.Count);
        Assert.Contains("la arena", turn.Response.Lines[0].Text, StringComparison.Ordinal);
        Assert.StartsWith("guide_ask.", turn.Response.Lines[0].LineId, StringComparison.Ordinal);
        Assert.StartsWith("guide_hint.", turn.Response.Lines[1].LineId, StringComparison.Ordinal);
        Assert.Equal(Idle().HostStateHash, turn.HostStateHash);
        Assert.Equal(_pipeline.Services.Versions.ContentHash, turn.ContentHash);
    }

    [Fact]
    public void TheTurnIsDeterministicAndTheVariantsFollowTheSeededStream()
    {
        var first = _pipeline.Advance(Event, Idle());
        var second = _pipeline.Advance(Event, Idle());
        var seen = Enumerable.Range(0, 40)
            .Select(counter => _pipeline.Advance(Event, Idle(counter))!.Response.Lines[0].LineId)
            .Distinct(StringComparer.Ordinal)
            .Count();

        Assert.Equal(first, second);
        Assert.Equal(TestSupport.Json(first), TestSupport.Json(second));
        Assert.Equal(2, seen);
    }

    [Fact]
    public void AnEventTheStateDoesNotHandleReportsNoStep()
    {
        Assert.Null(_pipeline.Advance("guide.unknown", Idle()));

        // "trading" has no transition for the event either.
        var trading = Idle() with { AgentState = new AgentState("blacksmith", "trading", 3) };
        Assert.Null(_pipeline.Advance(Event, trading));
    }

    [Fact]
    public void AnAgentWithoutAStateMachineReportsNoStep()
    {
        var stranger = ConversationContext.Create(1, "nobody", new AgentState("nobody", "x", 0), new StoredTimestamp(0), Vocaterra.Nlp.Decision.Learning.LearnerModel.New());

        Assert.Null(_pipeline.Advance(Event, stranger));
    }

    [Fact]
    public void AStateOfAnotherAgentIsRejected()
    {
        var wrong = Idle() with { AgentState = new AgentState("other", "idle", 0) };

        Assert.Throws<ArgumentException>(() => _pipeline.Advance(Event, wrong));
    }

    [Fact]
    public void ALineNeedingAHostSlotTheContextLacksThrows()
    {
        var context = ConversationContext.Create(42, "blacksmith", new AgentState("blacksmith", "idle", 0), new StoredTimestamp(0), Vocaterra.Nlp.Decision.Learning.LearnerModel.New());

        Assert.Throws<TemplateException>(() => _pipeline.Advance(Event, context));
    }

    [Fact]
    public void NextCarriesTheAdvancedAgentAndLeavesLearnerAndRepeatCountsUntouched()
    {
        var context = Idle();
        var turn = _pipeline.Advance(Event, context)!;

        var next = context.Next(turn, new StoredTimestamp(1_700_000_050));

        Assert.Equal(turn.Agent.Next, next.AgentState);
        Assert.Equal(context.Learner, next.Learner);
        Assert.Equal(context.RepeatCountsByTag, next.RepeatCountsByTag);
        Assert.Null(next.HostSlots);
        Assert.Equal(new StoredTimestamp(1_700_000_050), next.Now);
        Assert.Equal(
            next.HostStateHash,
            ConversationContext.Create(next.WorldSeed, next.AgentId, next.AgentState, next.Now, next.Learner, next.RepeatCountsByTag).HostStateHash);
    }

    [Fact]
    public void AnUtteranceAfterAHostTurnUsesTheAdvancedContext()
    {
        var context = Idle();
        var turn = _pipeline.Advance(Event, context)!;
        var next = context.Next(turn, context.Now);
        var prompt = _bundle.Prompts[TestSupport.OfferPrompt];

        var result = _pipeline.Evaluate(TestSupport.Clip(), prompt, next);
        var byHand = _pipeline.Evaluate(
            TestSupport.Clip(),
            prompt,
            ConversationContext.Create(42, "blacksmith", new AgentState("blacksmith", "trading", 1), context.Now, context.Learner, context.RepeatCountsByTag));

        Assert.Equal(byHand, result);
        Assert.Equal(2, result.Decision.Agent!.Next.InteractionCounter);

        // The counter is part of the random stream: the same state at another counter draws differently somewhere across counters.
        var replies = Enumerable.Range(0, 12)
            .Select(counter => _pipeline.Evaluate(TestSupport.Clip(), prompt, next with { AgentState = next.AgentState with { InteractionCounter = counter } }).Response.Lines.First().LineId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        Assert.True(replies > 1);
    }

    [Fact]
    public void ReplayReproducesAHostTurnAndASecondOneAfterIt()
    {
        var context = Idle();
        var turn = _pipeline.Advance(Event, context)!;
        using var writer = new StringWriter();
        var log = new ReplayLog(writer);
        log.Append(HostTurnEnvelope.From(turn, context));
        var next = context.Next(turn, context.Now);
        var trace = _pipeline.Trace(TestSupport.Clip(), _bundle.Prompts[TestSupport.OfferPrompt], next);
        log.Append(ReplayEnvelope.From(trace, next));
        var text = writer.ToString();

        var entries = ReplayReader.ReadEntries(new StringReader(text));

        Assert.Equal(2, entries.Count);
        Assert.Contains("\"kind\":\"hostTurn\"", text.Split('\n')[0], StringComparison.Ordinal);
        Assert.Equal(context, entries[0].HostTurn!.Context);
        Assert.Equal(turn, entries[0].HostTurn!.Result);
        Assert.Null(Replayer.Replay(entries[0].HostTurn!, _pipeline));
        Assert.Null(Replayer.Replay(entries[1].Utterance!, _pipeline, static hash => new Vocaterra.Nlp.Core.Audio.PcmClip([], Vocaterra.Nlp.Core.Audio.PcmClip.DefaultSampleRate, hash)));
        Assert.Equal(text, string.Concat(entries.Select(static e => e.HostTurn is { } h ? TestSupport.Json(h) + "\n" : TestSupport.Json(e.Utterance) + "\n")));
    }

    [Fact]
    public void AChangedRecordingOrAChangedContextShowsInTheReplayDiff()
    {
        var context = Idle();
        var envelope = HostTurnEnvelope.From(_pipeline.Advance(Event, context)!, context);

        var tampered = envelope with { Result = envelope.Result with { Response = new ResponsePlan([new ResponseLine("x", "otra", false)]) } };
        var moved = envelope with { Context = context with { AgentState = context.AgentState with { InteractionCounter = 9 } } };
        var gone = envelope with { HostEvent = "guide.unknown" };

        Assert.Contains(Replayer.Replay(tampered, _pipeline)!.Paths, static p => p.StartsWith("/response", StringComparison.Ordinal));
        Assert.NotNull(Replayer.Replay(moved, _pipeline));
        Assert.Equal(["/result"], Replayer.Replay(gone, _pipeline)!.Paths);
    }

    [Fact]
    public void OlderReadersOfUtteranceLogsSkipHostTurnLines()
    {
        var context = Idle();
        var turn = _pipeline.Advance(Event, context)!;
        var next = context.Next(turn, context.Now);
        var trace = _pipeline.Trace(TestSupport.Clip(), _bundle.Prompts[TestSupport.OfferPrompt], next);
        using var writer = new StringWriter();
        var log = new ReplayLog(writer);
        log.Append(HostTurnEnvelope.From(turn, context));
        log.Append(ReplayEnvelope.From(trace, next));

        Assert.Single(ReplayReader.Read(new StringReader(writer.ToString())));
        Assert.Single(ReplayReader.ReadEnvelopes(new StringReader(writer.ToString())));
    }

    [Fact]
    public void AnUtteranceLogWithoutHostTurnsReadsAsBeforeThroughReadEntries()
    {
        var pipeline = TestSupport.Pipeline(TestSupport.WorkedExample);
        var context = TestSupport.Context();
        var trace = pipeline.Trace(TestSupport.Clip(), TestSupport.Bundle.Prompts[TestSupport.OfferPrompt], context);
        using var writer = new StringWriter();
        new ReplayLog(writer).Append(ReplayEnvelope.From(trace, context));

        var entry = ReplayReader.ReadEntries(new StringReader(writer.ToString())).Single();

        Assert.Null(entry.HostTurn);
        Assert.Equal(ReplayEnvelope.From(trace, context), entry.Utterance);
    }

    [Fact]
    public void ABadHostTurnLineIsReportedWithItsLineNumber()
    {
        var ex = Assert.Throws<InvalidDataException>(() => ReplayReader.ReadEntries(new StringReader("{\"kind\":\"hostTurn\"}\n")));

        Assert.Contains("line 1", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AHostEventTransitionIsNeverTakenByAnUtterance()
    {
        // In "idle" an unrecognised utterance falls back to not_understood and stays: the event transition (whose "on" is the empty condition) must not match it.
        var recognizer = new ScriptedWordRecognizer(TestSupport.Asr("blorp"));
        var pipeline = new UtterancePipeline(new PipelineServices(recognizer, null, null, null, _bundle));

        var result = pipeline.Evaluate(TestSupport.Clip(), _bundle.Prompts[TestSupport.OfferPrompt], Idle());

        Assert.Equal("idle", result.Decision.Agent!.Next.StateId);
        Assert.DoesNotContain(result.Decision.Agent.Actions, static a => a.DialogueIntent == "guide_ask");
    }

    [Fact]
    public void ExistingAgentFilesLoadUnchangedAndCarryNoHostEventTransitions()
    {
        foreach (var machine in TestSupport.Bundle.Agents.Values)
        {
            Assert.All(machine.Definition.States.SelectMany(static s => s.Transitions), static t => Assert.Null(t.OnHostEvent));
        }

        Assert.Equal("1c2240c78d8a811afd4da12076b41055bab5a35bdc7d24b8d4b07fb16c9d38f2", TestSupport.Bundle.ContentHash);
        Assert.Null(new UtterancePipeline(TestSupport.Services("hola")).Advance(Event, TestSupport.Context("idle")));
    }

    [Fact]
    public void AnEmptyHostEventNameIsRejected()
    {
        Assert.Throws<ArgumentException>(() => _pipeline.Advance(string.Empty, Idle()));
        Assert.Throws<ArgumentNullException>(() => _pipeline.Advance(Event, null!));
    }

    [Fact]
    public void TheCliReplayReadsHostTurnEntriesAndReportsOneThatNoLongerHappens()
    {
        // The committed bundle has no host-event transition, so a turn recorded in the fixture bundle (same content hash) does not happen there.
        var path = Path.Combine(Path.GetTempPath(), "vocaterra-hostturn-" + Guid.NewGuid().ToString("N") + ".jsonl");
        try
        {
            var context = Idle();
            using (var writer = new StreamWriter(path, append: false, new System.Text.UTF8Encoding(false)))
            {
                new ReplayLog(writer).Append(HostTurnEnvelope.From(_pipeline.Advance(Event, context)!, context));
            }

            using var output = new StringWriter();
            using var error = new StringWriter();
            var code = Vocaterra.Nlp.Cli.Program.Run(["replay", path, "--content", TestSupport.BundleDir], output, error);

            Assert.Equal(Vocaterra.Nlp.Cli.Program.ExitFailed, code);
            Assert.Equal("DIFF entry 1: /result", output.ToString().Trim());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
