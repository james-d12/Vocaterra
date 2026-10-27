using System.Text.Json;
using System.Text.Json.Nodes;
using CsCheck;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Decision.ConversationAgents;
using Vocaterra.Nlp.Decision.Feedback;
using Vocaterra.Nlp.Decision.Scheduling;
using Vocaterra.Nlp.Evaluation;
using Vocaterra.Nlp.Fuzzing.Tests.Generation;
using Vocaterra.Nlp.Fuzzing.Tests.Oracles;
using Vocaterra.Nlp.Language.Analysis;
using Vocaterra.Nlp.Language.Intent;
using Vocaterra.Nlp.Language.Matching;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Phonology;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Nlp.Pronunciation;
using Xunit.Abstractions;
using SlotGrammarType = Vocaterra.Nlp.Language.SlotGrammar.SlotGrammar;

namespace Vocaterra.Nlp.Fuzzing.Tests;

/// <summary>
/// O14: the pipeline end to end, with the recognizers scripted. It never throws, gives the same result on a fresh instance (also with the
/// recognizers on one task), produces the verdict that calling the stages by hand produces, makes the decision the decision layer makes, and writes a
/// replay envelope that round-trips through canonical JSON and replays clean. Changing one recorded output of the envelope makes the replayer report
/// exactly that JSON pointer.
/// </summary>
/// <remarks>
/// Not every envelope field can be named by the replayer, and the oracle says so instead of pretending: fields it reads back as inputs (the asr and
/// phoneme recordings, the conversation context except its hash, the prompt id and the phoneme model hash of the record) change the replayed result rather than
/// being compared with it, and the duplicate copies <c>result/verdict</c> and <c>result/replay</c> of the record are never compared (README, known findings).
/// </remarks>
public sealed class PipelineFuzzTests(ITestOutputHelper output)
{
    private const string Oracle = "O14";

    private static readonly PipelineHarness Harness = PipelineHarness.Shared;

    private static readonly IntentResolution NoIntent = new(IntentId.Unknown, SlotMap.Empty, IntentRung.Unresolved, 0.0, 0.0);

    [Fact]
    public void Pipeline_NeverThrows_IsDeterministic_AgreesWithTheStagesByHand_AndReplaysClean()
    {
        var diagnostics = new FuzzDiagnostics();
        var iterations = FuzzOptions.Iterations(FuzzOptions.PipelineVar, FuzzOptions.PipelineDefault);

        PipelineGen.Case(Harness).Sample(
            c =>
            {
                var (pipeline, prompt, context, clip) = Prepare(c);
                PipelineTrace trace;
                try
                {
                    trace = pipeline.Trace(clip, prompt, context);
                }
                catch (Exception ex)
                {
                    throw Determinism.Fail(Oracle, $"The pipeline threw {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace?.Split('\n').FirstOrDefault()}", c);
                }

                // Same input, fresh instances: the same result (and with the recognizers on the other kind of scheduling).
                Determinism.AssertSame(Oracle, "UtterancePipeline.Evaluate", c, () => trace.Result, () => Prepare(c).Pipeline.Evaluate(clip, prompt, context));
                if (c.Phonemes)
                {
                    var flipped = c with { Concurrent = !c.Concurrent };
                    Determinism.AssertSame(Oracle, "UtterancePipeline.Evaluate with the other recognizer scheduling", c, () => trace.Result, () => Prepare(flipped).Pipeline.Evaluate(clip, prompt, context));
                }

                CheckStructure(c, trace, clip, prompt, context);
                CheckAgreementWithTheStagesByHand(c, trace, pipeline, clip, prompt);
                CheckDecision(c, trace, pipeline, prompt, context);
                CheckReplay(c, trace, pipeline, clip, context);
                if (c.TwoPhase)
                {
                    CheckTwoPhase(c, trace, clip, prompt, context);
                }

                if (c.HostTurn)
                {
                    CheckHostTurn(c, pipeline, context);
                }

                diagnostics.Count(c.TwoPhase ? "twophase:on" : "twophase:off");
                diagnostics.Count(c.HostTurn ? (pipeline.Services.Content.Agents.ContainsKey(c.AgentId) ? "hostturn:step" : "hostturn:none") : "hostturn:off");
                diagnostics.Count($"source:{c.Source.Split(':')[0]}");
                diagnostics.Count(trace.Result.Verdict.Communicated ? "verdict:communicated" : "verdict:not-communicated");
                diagnostics.Count(c.Phonemes ? "phonemes:on" : "phonemes:off");
                diagnostics.Count(trace.Result.Decision.Agent is null ? "agent:none" : "agent:step");
                diagnostics.Count(trace.Result.Repair is null ? "repair:none" : "repair:step");
                diagnostics.Count(trace.Result.Verdict.Errors.Count > 0 ? "verdict:errors" : "verdict:clean");
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Pipeline diagnostics:\n" + diagnostics.Report());
        var minimum = Math.Max(1, Math.Min(10, iterations / 30));
        foreach (var key in new[] { "source:correct", "source:mutated", "source:answer", "source:text", "verdict:communicated", "verdict:not-communicated", "phonemes:on", "twophase:on", "twophase:off", "hostturn:step", "hostturn:none", "agent:none", "agent:step", "repair:step", "verdict:errors", "verdict:clean" })
        {
            Assert.True(diagnostics.Get(key) >= minimum, $"The generator no longer reaches {key}.\n{diagnostics.Report()}");
        }
    }

    [Fact]
    public void ChangingOneRecordedOutput_MakesTheReplayerNameThatPath()
    {
        var diagnostics = new FuzzDiagnostics();
        var iterations = FuzzOptions.Iterations(FuzzOptions.PipelineVar, FuzzOptions.PipelineDefault);
        var gen = Gen.Select(PipelineGen.Case(Harness), Gen.ULong, static (c, seed) => (c, seed));

        gen.Sample(
            ((PipelineCase Case, ulong Seed) input) =>
            {
                // The mutated envelopes go through canonical JSON, so the transcript is made well-formed first, as a recognizer would return it.
                var c = input.Case with { Transcript = Repaired(input.Case.Transcript) };
                var seed = input.Seed;
                var (pipeline, prompt, context, clip) = Prepare(c);
                var trace = pipeline.Trace(clip, prompt, context);
                var envelope = CanonicalJson.Serialize(ReplayEnvelope.From(trace, context));
                var rng = new SeededRng(seed);

                var leaves = new List<string[]>();
                Leaves(JsonNode.Parse(envelope), [], leaves);
                var named = leaves.Where(IsComparedOutput).ToArray();
                diagnostics.Count($"leaves:{(named.Length > 0 ? "some" : "none")}");
                for (var attempt = 0; attempt < 3 && named.Length > 0; attempt++)
                {
                    var path = PickPath(rng, named);
                    CheckOneMutation(c, envelope, path, pipeline, clip, diagnostics);
                }
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Replay sensitivity diagnostics:\n" + diagnostics.Report());
        var minimum = Math.Max(1, Math.Min(30, iterations / 10));
        Assert.True(diagnostics.Get("mutation:named") >= minimum, $"Too few mutations were detected and named.\n{diagnostics.Report()}");
        foreach (var region in new[] { "record", "decision", "response", "effects", "context" })
        {
            Assert.True(diagnostics.Get($"region:{region}") >= 1, $"No mutation reached the {region} part of the envelope.\n{diagnostics.Report()}");
        }
    }

    // ---- Running ----------------------------------------------------------------------------------------------------------------

    private static (UtterancePipeline Pipeline, PromptSpec Prompt, ConversationContext Context, PcmClip Clip) Prepare(PipelineCase c)
    {
        var prompt = Harness.Prompt(c.PromptId);
        var services = Harness.NewServices(c.Transcript, prompt.Mode, c.Phonemes, c.Embeddings, c.HostTurn ? PipelineHarness.HostTurnBundle : null);
        var options = new PipelineOptions { PronunciationMode = c.PronunciationMode, RunRecognizersConcurrently = c.Concurrent };
        var pipeline = new UtterancePipeline(services, options);

        // A case with a host turn evaluates its utterance in the context the turn left: the agent moved and its counter advanced, with the
        // learner and the repeat counts as before; the host facts belong to the utterance, so they are set again.
        var context = BaseContext(c);
        if (c.HostTurn && pipeline.Advance(PipelineHarness.HostTurnEvent, context) is { } turn)
        {
            context = context.Next(turn, context.Now).WithHost(c.HostSlots, c.ExtraSkills);
        }

        return (pipeline, prompt, context, PipelineHarness.Clip());
    }

    // The context before any host turn.
    private static ConversationContext BaseContext(PipelineCase c)
    {
        return ConversationContext.Create(c.WorldSeed, c.AgentId, new AgentState(c.AgentId, c.StateId, c.Counter), new StoredTimestamp(PipelineHarness.FixedNow), c.Learner, c.Repeats, c.HostSlots, c.ExtraSkills);
    }

    // CR-017: phase 1 on one pipeline, phase 2 on another one (whose recognizer must never be called), with the host facts given to phase 2, equals Evaluate.
    private static void CheckTwoPhase(PipelineCase c, PipelineTrace trace, PcmClip clip, PromptSpec prompt, ConversationContext context)
    {
        var first = Prepare(c).Pipeline;
        var second = Prepare(c).Pipeline;
        var bare = context.WithHost(null, null);
        UtteranceAnalysis analysis;
        PipelineTrace completed;
        PipelineResult viaOverload;
        try
        {
            analysis = first.Analyze(clip, prompt);
            completed = second.CompleteTrace(analysis, bare.WithHost(c.HostSlots, c.ExtraSkills));
            viaOverload = second.Complete(analysis, bare, c.HostSlots, c.ExtraSkills);
        }
        catch (Exception ex)
        {
            throw Determinism.Fail(Oracle, $"The two-phase evaluation threw {ex.GetType().Name}: {ex.Message}", c);
        }

        Determinism.AssertSame(Oracle, "Evaluate against Analyze then Complete on another instance", c, () => trace.Result, () => completed.Result);
        Determinism.AssertSame(Oracle, "Complete(analysis, context, hostSlots, extraSkills) against Evaluate", c, () => trace.Result, () => viaOverload);
        Determinism.AssertSame(Oracle, "The recognizer outputs carried by phase 1", c, () => (trace.Asr, trace.Posteriors), () => (completed.Asr, completed.Posteriors));
        Determinism.AssertSame(Oracle, "The phase-1 verdict, words and answer", c, () => (trace.Result.Verdict, trace.Result.Words, trace.Result.Answer), () => (analysis.Verdict, analysis.Words, analysis.Answer));

        if (((ScriptedWordRecognizer)second.Services.WordRecognizer).CallCount != 0 || ((ScriptedWordRecognizer)first.Services.WordRecognizer).CallCount != 1)
        {
            throw Determinism.Fail(Oracle, "Phase 1 must run the recognizer once and phase 2 never.", c);
        }
    }

    // CR-015: the host turn the case took before its utterance. The agents of the host-turn bundle handle the event in every state; any other agent does nothing.
    private static void CheckHostTurn(PipelineCase c, UtterancePipeline pipeline, ConversationContext context)
    {
        var before = BaseContext(c);
        var known = pipeline.Services.Content.Agents.TryGetValue(c.AgentId, out var machine);
        HostTurnResult? turn;
        try
        {
            turn = pipeline.Advance(PipelineHarness.HostTurnEvent, before);
        }
        catch (Exception ex)
        {
            throw Determinism.Fail(Oracle, $"The host turn threw {ex.GetType().Name}: {ex.Message}", c);
        }

        if (!known)
        {
            if (turn is not null || !context.AgentState.Equals(before.AgentState))
            {
                throw Determinism.Fail(Oracle, "An agent the bundle has no machine for must report no host turn and stay where it was.", c);
            }

            return;
        }

        if (turn is null)
        {
            throw Determinism.Fail(Oracle, "The host-turn bundle's agents handle the event in every state, but no turn happened.", c);
        }

        var expected = machine!.StepHostEvent(before.AgentState, PipelineHarness.HostTurnEvent, SeededRng.ForInteraction(before.WorldSeed, before.AgentId, before.AgentState.InteractionCounter));
        if (expected is null || !turn.Agent.Equals(expected) || turn.Agent.Next.InteractionCounter != c.Counter + 1 || turn.Response.Lines.Count == 0)
        {
            throw Determinism.Fail(Oracle, $"The host turn {CanonicalJson.Serialize(turn)} is not the machine's step {CanonicalJson.Serialize(expected)} with the counter advanced.", c);
        }

        var after = before.Next(turn, before.Now);
        var countsKept = after.RepeatCountsByTag.OrderBy(static p => p.Key, StringComparer.Ordinal).SequenceEqual(before.RepeatCountsByTag.OrderBy(static p => p.Key, StringComparer.Ordinal));
        if (!after.Learner.Equals(before.Learner) || !countsKept || !after.AgentState.Equals(turn.Agent.Next) || after.HostSlots is not null)
        {
            throw Determinism.Fail(Oracle, "A host turn must move only the agent: the learner and the repeat counts stay, and the host facts are not carried over.", c);
        }

        if (!context.AgentState.Equals(turn.Agent.Next))
        {
            throw Determinism.Fail(Oracle, "The utterance was not evaluated in the context after the host turn.", c);
        }

        var fresh = Prepare(c).Pipeline;
        Determinism.AssertSame(Oracle, "UtterancePipeline.Advance", c, () => turn, () => fresh.Advance(PipelineHarness.HostTurnEvent, before));

        // The log entry round-trips through canonical JSON and the replayer reproduces it; readers of utterance entries skip it.
        var entry = HostTurnEnvelope.From(turn, before);
        using var writer = new StringWriter();
        new ReplayLog(writer).Append(entry);
        var text = writer.ToString();
        var back = ReplayReader.ReadEntries(new StringReader(text)).Single().HostTurn;
        if (back is null || !string.Equals(CanonicalJson.Serialize(back), text.TrimEnd('\n'), StringComparison.Ordinal))
        {
            throw Determinism.Fail(Oracle, "The host-turn entry does not round-trip through canonical JSON.", c);
        }

        if (ReplayReader.Read(new StringReader(text)).Count != 0 || ReplayReader.ReadEnvelopes(new StringReader(text)).Count != 0)
        {
            throw Determinism.Fail(Oracle, "Readers of utterance entries must skip a host-turn entry.", c);
        }

        if (Replayer.Replay(back, LivePipelineFor(c, pipeline)) is { } diff)
        {
            throw Determinism.Fail(Oracle, $"The replay of an unmodified host turn differs from the recording at {diff}.", c);
        }

        var moved = back with { Context = back.Context with { AgentState = back.Context.AgentState with { InteractionCounter = back.Context.AgentState.InteractionCounter + 1 } } };
        if (Replayer.Replay(moved, LivePipelineFor(c, pipeline)) is null)
        {
            throw Determinism.Fail(Oracle, "A host turn replayed in another interaction counter must differ from the recording.", c);
        }
    }

    // ---- Structure and decision ------------------------------------------------------------------------------------------------

    private static void CheckStructure(PipelineCase c, PipelineTrace trace, PcmClip clip, PromptSpec prompt, ConversationContext context)
    {
        var result = trace.Result;
        var replay = result.Replay;
        if (replay.ClipHash != clip.ClipHash || replay.PromptId != prompt.PromptId || replay.HostStateHash != context.HostStateHash || !replay.Verdict.Equals(result.Verdict))
        {
            throw Determinism.Fail(Oracle, "The replay record does not carry the clip hash, prompt id, host state hash and verdict of the run.", c);
        }

        // Host inputs are per utterance: they change the hash, and the next context starts without them.
        if (context.HostSlots is not null || context.ExtraSkills is not null)
        {
            var bare = ConversationContext.Create(context.WorldSeed, context.AgentId, context.AgentState, context.Now, context.Learner, context.RepeatCountsByTag);
            var next = context.Next(result, context.Now);
            if (bare.HostStateHash == context.HostStateHash || next.HostSlots is not null || next.ExtraSkills is not null)
            {
                throw Determinism.Fail(Oracle, "Host slots and extra skills must change the host state hash and must not carry over to the next context.", c);
            }
        }

        var effectIds = result.Effects.Select(static e => e.EffectId).ToArray();
        if (!effectIds.SequenceEqual(effectIds.Order(StringComparer.Ordinal)))
        {
            throw Determinism.Fail(Oracle, $"Effects are not in ordinal effect id order: {string.Join(", ", effectIds)}.", c);
        }

        var verdict = result.Verdict;
        if (!(verdict.Correctness >= 0.0 && verdict.Correctness <= 1.0) || !(verdict.Confidence >= 0.0 && verdict.Confidence <= 1.0))
        {
            throw Determinism.Fail(Oracle, $"Correctness {verdict.Correctness} and confidence {verdict.Confidence} must be in [0, 1].", c);
        }

        if (c.Phonemes != (trace.Posteriors is not null))
        {
            throw Determinism.Fail(Oracle, $"Posteriors are {(trace.Posteriors is null ? "missing" : "recorded")} for a run {(c.Phonemes ? "with" : "without")} a phoneme recognizer.", c);
        }

        var lines = result.Response.Lines;
        if (lines.Any(static l => l.Text.Contains('{', StringComparison.Ordinal) || l.Text.Contains('}', StringComparison.Ordinal)))
        {
            throw Determinism.Fail(Oracle, $"A response line has an unfilled placeholder: {string.Join(" | ", lines.Select(static l => l.Text))}.", c);
        }
    }

    // The decision layer's pieces restated: the agent step for the context's seed, the learner update, the rating and the feedback plan.
    private static void CheckDecision(PipelineCase c, PipelineTrace trace, UtterancePipeline pipeline, PromptSpec prompt, ConversationContext context)
    {
        var verdict = trace.Result.Verdict;
        var decision = trace.Result.Decision;
        var bundle = pipeline.Services.Content;
        if (bundle.Agents.TryGetValue(context.AgentId, out var machine))
        {
            var expectedStep = machine.Step(context.AgentState, verdict, SeededRng.ForInteraction(context.WorldSeed, context.AgentId, context.AgentState.InteractionCounter));
            if (decision.Agent is null || !decision.Agent.Equals(expectedStep))
            {
                throw Determinism.Fail(Oracle, $"The agent step {CanonicalJson.Serialize(decision.Agent)} differs from the state machine's {CanonicalJson.Serialize(expectedStep)}.", c);
            }
        }
        else if (decision.Agent is not null)
        {
            throw Determinism.Fail(Oracle, $"An agent step was taken for '{context.AgentId}', which the bundle has no machine for.", c);
        }

        // One update with the pipeline's own skill and the host-named skills together: a skill named twice, or an error family named as a skill,
        // is stepped once.
        var skills = (verdict.Intent.IsUnknown ? Array.Empty<string>() : [$"intent.{verdict.Intent.Value}"]).Concat(c.ExtraSkills ?? []).ToArray();
        if (!decision.Learner.Equals(context.Learner.Update(verdict, skills)))
        {
            throw Determinism.Fail(Oracle, "The learner model is not the context's model updated with the verdict and the host-named skills.", c);
        }

        foreach (var skill in c.ExtraSkills ?? [])
        {
            var before = context.Learner.Find(skill)?.Attempts ?? 0;
            if ((decision.Learner.Find(skill)?.Attempts ?? 0) != before + 1)
            {
                throw Determinism.Fail(Oracle, $"The host-named skill '{skill}' was not stepped exactly once.", c);
            }
        }

        if (decision.Rating != RatingMapper.FromVerdict(verdict, pipeline.Options.RatingThresholds))
        {
            throw Determinism.Fail(Oracle, $"The rating {decision.Rating} is not the mapper's.", c);
        }

        // The context holds the counts before this utterance; the policy reads the counts after it (RepeatCounts.Next), as the pipeline passes them.
        var counts = RepeatCounts.Next(context.RepeatCountsByTag, verdict);
        if (!decision.Feedback.Equals(bundle.FeedbackPolicy.Plan(verdict, counts, prompt.Difficulty)))
        {
            throw Determinism.Fail(Oracle, "The feedback plan is not the policy's plan for the verdict.", c);
        }
    }

    // ---- Agreement with the stages called by hand ------------------------------------------------------------------------------

    private static void CheckAgreementWithTheStagesByHand(PipelineCase c, PipelineTrace trace, UtterancePipeline pipeline, PcmClip clip, PromptSpec prompt)
    {
        var services = pipeline.Services;
        var bundle = services.Content;
        var asr = services.WordRecognizer.Recognize(clip, RecognitionRequest.Open(pipeline.Options.MaxAlternatives));
        var answerSet = bundle.AnswerSets[prompt.AnswerSetId];

        var normalized = TextNormalizer.Normalize(asr.Transcript.Replace(GrammarBuilder.UnknownToken, " ", StringComparison.Ordinal), PipelineHarness.Normalization);
        var match = AnswerMatcher.Match(normalized, answerSet, prompt.Difficulty, SpanishLanguagePack.Instance, PipelineHarness.Normalization);
        var analysis = AnswerCorrections.WithCanonicalWords(
            new LinguisticAnalyzer(bundle.Lexicon, bundle.Morphology, SpanishLanguagePack.Instance).Analyze(normalized, GrammarsFor(bundle, prompt)),
            normalized,
            match,
            answerSet, SpanishLanguagePack.Instance,
            PipelineHarness.Normalization);
        var intent = prompt.AllowedIntentIds.Count == 0 ? NoIntent : new IntentResolver(bundle.Intents, SpanishLanguagePack.Instance, services.Embeddings).Resolve(normalized, analysis, prompt);

        PronunciationResult? pronunciation = null;
        if (services.PhonemeRecognizer is { } recognizer)
        {
            var posteriors = recognizer.Recognize(clip);
            var spoken = SpanishNumbers.DigitsToWords(normalized.Text);
            if (posteriors.FrameCount > 0 && spoken.Length > 0)
            {
                var variants = SpanishG2p.PhonemizeAll(spoken);
                pronunciation = new PronunciationScorer().Score(posteriors, variants, PronunciationScorer.PredictedErrorsFor(variants), prompt.Difficulty, pipeline.Options.PronunciationMode, recognizer.ModelHash);
            }
        }

        var known = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var token in analysis.Tokens.Where(static t => t.Readings.Count > 0))
        {
            known.Add(token.Token.Text);
        }

        var byHand = VerdictAssembler.Assemble(new VerdictInputs(
            prompt,
            asr,
            match,
            analysis,
            intent,
            pronunciation,
            bundle.Intents.RequiredSlots(intent.Intent),
            [.. known],
            bundle.EvaluationConfig,
            services.Versions,
            [.. answerSet.Answers.Select(static answer => answer.Canonical)]));

        Determinism.AssertSame(Oracle, "The verdict (pipeline against the stages called by hand)", c, () => trace.Result.Verdict, () => byHand);
    }

    // The prompt's own grammars first, then every other grammar of the allowed intents, each once.
    private static List<SlotGrammarType> GrammarsFor(Vocaterra.Nlp.Content.ContentBundle bundle, PromptSpec prompt)
    {
        var grammars = new List<SlotGrammarType>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var grammar in bundle.Intents.Grammars.Where(g => prompt.SlotGrammarIds.Contains(g.Id, StringComparer.Ordinal)))
        {
            if (seen.Add(grammar.Id))
            {
                grammars.Add(grammar);
            }
        }

        foreach (var intent in bundle.Intents.Intents.Where(i => prompt.AllowedIntentIds.Contains(i.Id.Value, StringComparer.Ordinal)))
        {
            foreach (var grammar in intent.Grammars.Where(g => seen.Add(g.Id)))
            {
                grammars.Add(grammar);
            }
        }

        return grammars;
    }

    // ---- Replay ------------------------------------------------------------------------------------------------------------------

    private static void CheckReplay(PipelineCase c, PipelineTrace trace, UtterancePipeline pipeline, PcmClip clip, ConversationContext context)
    {
        var envelope = ReplayEnvelope.From(trace, context);
        var candidates = new List<ReplayEnvelope> { envelope };

        // A recognizer returns well-formed text. Canonical JSON writes a lone surrogate as an escaped U+FFFD but a real U+FFFD as the character
        // itself, so a transcript with a lone surrogate does not survive serialize, deserialize, serialize byte for byte (README, known findings); the
        // pipeline still has to run it, which is checked above, and the in-memory envelope still has to replay.
        if (IsWellFormed(c.Transcript))
        {
            var json = CanonicalJson.Serialize(envelope);
            var back = CanonicalJson.Deserialize<ReplayEnvelope>(json);
            if (!string.Equals(CanonicalJson.Serialize(back), json, StringComparison.Ordinal))
            {
                throw Determinism.Fail(Oracle, $"The envelope does not round-trip through canonical JSON; first difference at {Determinism.FirstDifference(json, CanonicalJson.Serialize(back))}.", c);
            }

            candidates.Add(back);
        }

        // The replayer needs no recognizer and no model: it is served the recorded outputs, whatever the live pipeline's recognizer would say.
        var live = LivePipelineFor(c, pipeline);
        foreach (var candidate in candidates)
        {
            ReplayDiff? diff;
            try
            {
                diff = Replayer.Replay(candidate, live, _ => clip);
            }
            catch (Exception ex)
            {
                throw Determinism.Fail(Oracle, $"Replay threw {ex.GetType().Name}: {ex.Message}", c);
            }

            if (diff is not null)
            {
                throw Determinism.Fail(Oracle, $"The replay of an unmodified envelope differs from the recording at {diff}.", c);
            }
        }
    }

    // A pipeline over the same content, options, embedding model and synthesizer, whose live recognizers would give a different answer, to show the
    // replay does not use them. (The replayer serves back the recorded word and phoneme outputs only; the embedding model and the synthesizer are the
    // live pipeline's, so a replay with another embedder reports the changed model id in the version stamp instead of replaying silently.)
    private static UtterancePipeline LivePipelineFor(PipelineCase c, UtterancePipeline pipeline)
    {
        var other = Harness.NewServices("una transcripcion que nadie dijo", RecognitionMode.Open, phonemes: false, embeddings: c.Embeddings, bundle: c.HostTurn ? PipelineHarness.HostTurnBundle : null);
        return new UtterancePipeline(other, pipeline.Options);
    }

    // The envelope part a path lies in: record, decision, response, effects, repair or context.
    private static string RegionOf(string[] path)
    {
        return path[0] == "result" ? path[1] : path[0];
    }

    // Drawn in two steps: a region uniformly among the regions the envelope has, then a leaf uniformly inside it. A leaf drawn uniformly from the
    // whole envelope reached the effects part (three leaves among several hundred) only a few times in 300 iterations, while the context part (one
    // leaf) was drawn about as often as the effects part. Drawing the region first gives every present region the same share of the mutations, so
    // a rare region is reached as often as a common one whenever the envelope has it (the generator makes effects common enough to be present).
    private static string[] PickPath(SeededRng rng, string[][] named)
    {
        var regions = named.Select(RegionOf).Distinct().ToArray();
        var region = rng.Pick(regions);
        return rng.Pick(named.Where(path => RegionOf(path) == region).ToArray());
    }

    private static void CheckOneMutation(PipelineCase c, string envelopeJson, string[] path, UtterancePipeline pipeline, PcmClip clip, FuzzDiagnostics diagnostics)
    {
        var expected = ExpectedPointer(path);
        var region = RegionOf(path);
        var original = JsonNode.Parse(envelopeJson)!;
        var current = Navigate(original, path);

        foreach (var replacement in Replacements(current))
        {
            var root = JsonNode.Parse(envelopeJson)!;
            Set(root, path, replacement);
            ReplayEnvelope mutated;
            try
            {
                mutated = CanonicalJson.Deserialize<ReplayEnvelope>(root.ToJsonString());
            }
            catch (JsonException)
            {
                continue;
            }

            ReplayDiff? diff;
            try
            {
                diff = Replayer.Replay(mutated, LivePipelineFor(c, pipeline), _ => clip);
            }
            catch (Exception ex)
            {
                throw Determinism.Fail(Oracle, $"Replay of an envelope with {string.Join('/', path)} changed threw {ex.GetType().Name}: {ex.Message}", new { c, path, replacement = replacement?.ToJsonString() });
            }

            if (diff is null || !diff.Paths.SequenceEqual([expected]))
            {
                throw Determinism.Fail(
                    Oracle,
                    $"Changing /{string.Join('/', path)} to {replacement?.ToJsonString() ?? "null"} should give exactly the difference [{expected}] but the replayer reported [{diff?.ToString() ?? "no difference"}].",
                    new { c, path, replacement = replacement?.ToJsonString() });
            }

            diagnostics.Count("mutation:named");
            diagnostics.Count($"region:{region}");
            return;
        }

        diagnostics.Count("mutation:skipped-no-valid-replacement");
    }

    // ---- Text helpers -------------------------------------------------------------------------------------------------------------

    private static bool IsWellFormed(string text)
    {
        return string.Equals(text, Repaired(text), StringComparison.Ordinal);
    }

    // Every lone surrogate becomes U+FFFD.
    private static string Repaired(string text)
    {
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsHighSurrogate(chars[i]) && i + 1 < chars.Length && char.IsLowSurrogate(chars[i + 1]))
            {
                i++;
            }
            else if (char.IsSurrogate(chars[i]))
            {
                chars[i] = (char)0xFFFD;
            }
        }

        return new string(chars);
    }

    // ---- JSON helpers -------------------------------------------------------------------------------------------------------------

    // The leaves the replayer compares with the replay: the record (but not the prompt id and the phoneme model hash it reads back as inputs),
    // the downstream parts of the result, and the host state hash of the context (copied into the record).
    private static bool IsComparedOutput(string[] path)
    {
        if (path.Length == 0)
        {
            return false;
        }

        switch (path[0])
        {
            case "record":
                return !(path.Length == 2 && path[1] == "promptId")
                    && !(path.Length == 4 && path[1] == "versions" && path[2] == "modelHashes" && path[3] == "phoneme");
            case "result":
                return path.Length > 1 && path[1] is "decision" or "response" or "effects" or "repair";
            case "context":
                return path.Length == 2 && path[1] == "hostStateHash";
            default:
                return false;
        }
    }

    // The pointer the replayer reports: record paths and context.hostStateHash are compared with the replayed record, result paths with the
    // replayed downstream parts, both without their top-level segment (RFC 6901 escapes for ~ and /).
    private static string ExpectedPointer(string[] path)
    {
        return "/" + string.Join('/', path[1..].Select(static s => s.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal)));
    }

    private static void Leaves(JsonNode? node, string[] path, List<string[]> leaves)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var (name, child) in jsonObject)
                {
                    Leaves(child, [.. path, name], leaves);
                }

                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    Leaves(array[i], [.. path, i.ToString(System.Globalization.CultureInfo.InvariantCulture)], leaves);
                }

                break;
            default:
                leaves.Add(path);
                break;
        }
    }

    private static JsonNode? Navigate(JsonNode? node, string[] path)
    {
        foreach (var segment in path)
        {
            node = node is JsonArray array ? array[int.Parse(segment, System.Globalization.CultureInfo.InvariantCulture)] : node![segment];
        }

        return node;
    }

    private static void Set(JsonNode root, string[] path, JsonNode? value)
    {
        var parent = Navigate(root, path[..^1])!;
        if (parent is JsonArray array)
        {
            array[int.Parse(path[^1], System.Globalization.CultureInfo.InvariantCulture)] = value;
        }
        else
        {
            parent[path[^1]] = value;
        }
    }

    // Replacement values to try in order until one still deserializes: another value of the same JSON kind first, then a few others.
    private static IEnumerable<JsonNode?> Replacements(JsonNode? current)
    {
        switch (current)
        {
            case null:
                yield return JsonValue.Create("x");
                yield return JsonValue.Create(1);
                yield return JsonValue.Create(true);
                break;
            case JsonValue value when value.TryGetValue<bool>(out var flag):
                yield return JsonValue.Create(!flag);
                break;
            case JsonValue value when value.TryGetValue<string>(out var text):
                foreach (var name in EnumNamesContaining(text))
                {
                    yield return JsonValue.Create(name);
                }

                yield return JsonValue.Create(text + "x");
                yield return JsonValue.Create(string.Empty);
                break;
            case JsonValue value when value.TryGetValue<long>(out var whole):
                yield return JsonValue.Create(whole + 1);
                yield return JsonValue.Create(whole + 2);
                break;
            case JsonValue value when value.TryGetValue<double>(out var real):
                yield return JsonValue.Create(real + 0.25);
                yield return JsonValue.Create(real + 1.0);
                break;
            default:
                break;
        }
    }

    // The other members of whichever enumeration the value belongs to, so a changed enum value still reads back.
    private static IEnumerable<string> EnumNamesContaining(string text)
    {
        foreach (var names in new[]
        {
            Enum.GetNames<Rating>(),
            Enum.GetNames<FeedbackType>(),
            Enum.GetNames<ResolvedBy>(),
            Enum.GetNames<IntentRung>(),
            Enum.GetNames<RecognitionMode>(),
            Enum.GetNames<Difficulty>(),
        })
        {
            if (names.Contains(text, StringComparer.Ordinal))
            {
                foreach (var name in names.Where(n => n != text))
                {
                    yield return name;
                }
            }
        }
    }
}
