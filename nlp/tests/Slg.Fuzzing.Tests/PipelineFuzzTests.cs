using System.Text.Json;
using System.Text.Json.Nodes;
using CsCheck;
using Slg.Asr;
using Slg.Core.Audio;
using Slg.Core.Determinism;
using Slg.Core.Prompts;
using Slg.Core.Replay;
using Slg.Core.Stages;
using Slg.Core.Verdicts;
using Slg.Decision.Feedback;
using Slg.Decision.Npc;
using Slg.Decision.Scheduling;
using Slg.Evaluation;
using Slg.Fuzzing.Tests.Generation;
using Slg.Fuzzing.Tests.Oracles;
using Slg.Nlp.Analysis;
using Slg.Nlp.Intent;
using Slg.Nlp.Matching;
using Slg.Nlp.Normalization;
using Slg.Phonology;
using Slg.Pipeline;
using Slg.Pronunciation;
using Xunit.Abstractions;
using SlotGrammarType = Slg.Nlp.SlotGrammar.SlotGrammar;

namespace Slg.Fuzzing.Tests;

/// <summary>
/// O14: the pipeline end to end, with the recognizers scripted. It never throws, gives the same result on a fresh instance (also with the
/// recognizers on one task), produces the verdict that calling the stages by hand produces, makes the decision the decision layer makes, and writes a
/// replay envelope that round-trips through canonical JSON and replays clean. Changing one recorded output of the envelope makes the replayer report
/// exactly that JSON pointer.
/// </summary>
/// <remarks>
/// Not every envelope field can be named by the replayer, and the oracle says so instead of pretending: fields it reads back as inputs (the asr and
/// phoneme recordings, the game context except its hash, the prompt id and the phoneme model hash of the record) change the replayed result rather than
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

                diagnostics.Count($"source:{c.Source.Split(':')[0]}");
                diagnostics.Count(trace.Result.Verdict.Communicated ? "verdict:communicated" : "verdict:not-communicated");
                diagnostics.Count(c.Phonemes ? "phonemes:on" : "phonemes:off");
                diagnostics.Count(trace.Result.Decision.Npc is null ? "npc:none" : "npc:step");
                diagnostics.Count(trace.Result.Repair is null ? "repair:none" : "repair:step");
                diagnostics.Count(trace.Result.Verdict.Errors.Count > 0 ? "verdict:errors" : "verdict:clean");
            },
            writeLine: output.WriteLine,
            iter: iterations,
            threads: 1);

        output.WriteLine("Pipeline diagnostics:\n" + diagnostics.Report());
        var minimum = Math.Max(1, Math.Min(10, iterations / 30));
        foreach (var key in new[] { "source:correct", "source:mutated", "source:answer", "source:text", "verdict:communicated", "verdict:not-communicated", "phonemes:on", "npc:none", "npc:step", "repair:step", "verdict:errors", "verdict:clean" })
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
                    var path = rng.Pick(named);
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

    private static (UtterancePipeline Pipeline, PromptSpec Prompt, GameContext Context, PcmClip Clip) Prepare(PipelineCase c)
    {
        var prompt = Harness.Prompt(c.PromptId);
        var services = Harness.NewServices(c.Transcript, prompt.Mode, c.Phonemes, c.Embeddings);
        var options = new PipelineOptions { PronunciationMode = c.PronunciationMode, RunRecognizersConcurrently = c.Concurrent };
        var context = GameContext.Create(c.WorldSeed, c.NpcId, new NpcState(c.NpcId, c.StateId, c.Counter), new StoredTimestamp(PipelineHarness.FixedNow), c.Learner, c.Repeats);
        return (new UtterancePipeline(services, options), prompt, context, PipelineHarness.Clip());
    }

    // ---- Structure and decision ------------------------------------------------------------------------------------------------

    private static void CheckStructure(PipelineCase c, PipelineTrace trace, PcmClip clip, PromptSpec prompt, GameContext context)
    {
        var result = trace.Result;
        var replay = result.Replay;
        if (replay.ClipHash != clip.ClipHash || replay.PromptId != prompt.PromptId || replay.GameStateHash != context.GameStateHash || !replay.Verdict.Equals(result.Verdict))
        {
            throw Determinism.Fail(Oracle, "The replay record does not carry the clip hash, prompt id, game state hash and verdict of the run.", c);
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

    // The decision layer's pieces restated: the NPC step for the context's seed, the learner update, the rating and the feedback plan.
    private static void CheckDecision(PipelineCase c, PipelineTrace trace, UtterancePipeline pipeline, PromptSpec prompt, GameContext context)
    {
        var verdict = trace.Result.Verdict;
        var decision = trace.Result.Decision;
        var bundle = pipeline.Services.Content;
        if (bundle.Npcs.TryGetValue(context.NpcId, out var machine))
        {
            var expectedStep = machine.Step(context.NpcState, verdict, SeededRng.ForInteraction(context.WorldSeed, context.NpcId, context.NpcState.InteractionCounter));
            if (decision.Npc is null || !decision.Npc.Equals(expectedStep))
            {
                throw Determinism.Fail(Oracle, $"The NPC step {CanonicalJson.Serialize(decision.Npc)} differs from the state machine's {CanonicalJson.Serialize(expectedStep)}.", c);
            }
        }
        else if (decision.Npc is not null)
        {
            throw Determinism.Fail(Oracle, $"An NPC step was taken for '{context.NpcId}', which the bundle has no machine for.", c);
        }

        var skills = verdict.Intent.IsUnknown ? Array.Empty<string>() : [$"intent.{verdict.Intent.Value}"];
        if (!decision.Learner.Equals(context.Learner.Update(verdict, skills)))
        {
            throw Determinism.Fail(Oracle, "The learner model is not the context's model updated with the verdict.", c);
        }

        if (decision.Rating != RatingMapper.FromVerdict(verdict, pipeline.Options.RatingThresholds))
        {
            throw Determinism.Fail(Oracle, $"The rating {decision.Rating} is not the mapper's.", c);
        }

        if (!decision.Feedback.Equals(bundle.FeedbackPolicy.Plan(verdict, context.RepeatCountsByTag, prompt.Difficulty)))
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
        var match = AnswerMatcher.Match(normalized, answerSet, prompt.Difficulty, PipelineHarness.Normalization);
        var analysis = new LinguisticAnalyzer(bundle.Lexicon, bundle.Morphology).Analyze(normalized, GrammarsFor(bundle, prompt));
        var intent = prompt.AllowedIntentIds.Count == 0 ? NoIntent : new IntentResolver(bundle.Intents, services.Embeddings).Resolve(normalized, analysis, prompt);

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
            services.Versions));

        Determinism.AssertSame(Oracle, "The verdict (pipeline against the stages called by hand)", c, () => trace.Result.Verdict, () => byHand);
    }

    // The prompt's own grammars first, then every other grammar of the allowed intents, each once.
    private static List<SlotGrammarType> GrammarsFor(Slg.Content.ContentBundle bundle, PromptSpec prompt)
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

    private static void CheckReplay(PipelineCase c, PipelineTrace trace, UtterancePipeline pipeline, PcmClip clip, GameContext context)
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
        var other = Harness.NewServices("una transcripcion que nadie dijo", RecognitionMode.Open, phonemes: false, embeddings: c.Embeddings);
        return new UtterancePipeline(other, pipeline.Options);
    }

    private static void CheckOneMutation(PipelineCase c, string envelopeJson, string[] path, UtterancePipeline pipeline, PcmClip clip, FuzzDiagnostics diagnostics)
    {
        var expected = ExpectedPointer(path);
        var region = path[0] == "result" ? path[1] : path[0];
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
    // the downstream parts of the result, and the game state hash of the context (copied into the record).
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
                return path.Length == 2 && path[1] == "gameStateHash";
            default:
                return false;
        }
    }

    // The pointer the replayer reports: record paths and context.gameStateHash are compared with the replayed record, result paths with the
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
