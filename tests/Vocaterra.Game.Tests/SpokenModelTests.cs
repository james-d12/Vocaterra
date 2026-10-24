using System.Buffers.Binary;
using Vocaterra.Game.Language.Speech;
using Vocaterra.Game.Language.Words;
using Vocaterra.Game.Npc;
using Vocaterra.Game.Session;
using Vocaterra.Nlp.Asr;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Pipeline;
using Vocaterra.Simulation.Voxel;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests;

/// <summary>
/// Spoken input with the real models: Kokoro speaks a line (into the cache under <c>~/.cache/vocaterra/tts</c>, never into the repository),
/// Vosk recognizes it, the guide evaluates it. Each test is skipped, with the reason in the report, when <c>VOCATERRA_MODELS_DIR</c> is unset
/// or a model is missing. The model tests share one collection so that no two of them run on different threads at once (Vosk, and a
/// synthesis cache key, belong to one thread at a time).
/// </summary>
[Collection("speech models")]
public sealed class SpokenModelTests(ITestOutputHelper output)
{
    private const string TreeQuestion = "¿dónde está el árbol?";

    // Counts the decodes: the guide's double evaluation must not decode a clip twice.
    private sealed class CountingRecognizer(IWordRecognizer inner) : IWordRecognizer
    {
        public int Calls { get; private set; }

        public string ModelHash => inner.ModelHash;

        public AsrResult Recognize(PcmClip clip, RecognitionRequest request)
        {
            Calls++;
            return inner.Recognize(clip, request);
        }
    }

    [Fact]
    public void LibVoskIsCopiedNextToTheTestAssemblyByTheDirectPackageReference()
    {
        // Vosk 0.3.38 copies libvosk.so only through build/Vosk.targets, which NuGet imports into a project that references Vosk directly.
        if (OperatingSystem.IsLinux())
        {
            Assert.True(File.Exists(Path.Combine(AppContext.BaseDirectory, "libvosk.so")), "libvosk.so is not in the test output: add the direct Vosk reference.");
        }
    }

    [ModelFact]
    public void TheRecognizerLoadsFromTheStoreAndDecodesSilence()
    {
        var store = SpeechEngine.FindModels(out var problem);
        Assert.True(store is not null, problem);

        using var vosk = VoskWordRecognizer.FromStore(store, SpeechEngine.VoskModelId);
        var result = vosk.Recognize(PcmClip.FromSamples(new short[8000]), RecognitionRequest.Open(1));

        Assert.Equal(string.Empty, result.Transcript);
        Assert.Equal(store.ModelHash(SpeechEngine.VoskModelId), vosk.ModelHash);
    }

    [ModelFact(needsKokoro: true)]
    public void AKokoroLineIsRecognizedByVoskAndTheGuideAnswersItLikeTheTypedLine()
    {
        using var engine = SpeechEngine.TryCreate(out var problem)!;
        Assert.NotNull(engine);
        var session = TestGame.NewSession();
        var counting = new CountingRecognizer(engine.Recognizer);
        var guide = new NpcService(TestGame.Bundle, TestGame.FactsOf(session), TestGame.Nouns, TestGame.Seed, new StoredTimestamp(GuideSession.DefaultStartedAt));
        guide.AttachSpeech(new SpokenUtteranceEvaluator(TestGame.Bundle, counting));
        var clip = engine.Synthesize(TreeQuestion, out var cached);
        output.WriteLine($"{TreeQuestion}: {clip.DurationSeconds:0.00} s, cached {cached}");

        var spoken = guide.SaySpoken(clip, new GuideScene(TestGame.ViewpointOf(session)), new StoredTimestamp(GuideSession.DefaultStartedAt), session.Simulation.ComputeStateDigest());
        var typed = TestGame.NewSession().Say("dónde está el árbol");

        output.WriteLine($"heard '{spoken.Line}' confidence {spoken.Confidence:0.00}");
        Assert.Equal(1, counting.Calls); // the two completions share the one recognition
        Assert.Equal(InputKind.Spoken, spoken.Input);
        Assert.Equal("dónde está el árbol", spoken.Line);
        Assert.InRange(spoken.Confidence!.Value, 0.5, 1.0);
        Assert.Equal("ask_directions", spoken.Intent);
        Assert.True(spoken.Communicated);
        Assert.Equal("pointing", spoken.AgentState);
        Assert.Equal(typed.GuideLines, spoken.GuideLines);
        Assert.Equal(typed.Pointer, spoken.Pointer);
        Assert.Equal(["guide.point"], spoken.HostEvents);
    }

    [ModelFact(needsKokoro: true)]
    public void TheSecondEvaluationSeesTheTranscriptAndConfidenceOfTheFirstWithoutDecodingAgain()
    {
        using var engine = SpeechEngine.TryCreate(out _)!;
        var counting = new CountingRecognizer(engine.Recognizer);
        var evaluator = new SpokenUtteranceEvaluator(TestGame.Bundle, counting);
        var prompt = TestGame.Bundle.Prompts[NpcService.GuidePrompt];
        var context = ConversationContext.Create(
            TestGame.Seed,
            NpcService.GuideAgent,
            TestGame.Bundle.Agents[NpcService.GuideAgent].InitialState(),
            new StoredTimestamp(GuideSession.DefaultStartedAt),
            Vocaterra.Nlp.Decision.Learning.LearnerModel.New(),
            new Dictionary<string, int>(StringComparer.Ordinal),
            Vocaterra.Nlp.Core.Verdicts.SlotMap.Empty,
            []);
        var clip = engine.Synthesize(TreeQuestion, out _);

        var first = evaluator.Trace(clip, prompt, context);
        var scripted = new TypedUtteranceEvaluator(TestGame.Bundle);
        var second = scripted.EvaluateRecognized(first.Asr, clip, prompt, context);

        Assert.Equal(1, counting.Calls);
        Assert.Equal(first.Result.Verdict.Intent, second.Verdict.Intent);
        Assert.Equal(first.Result.Verdict.Communicated, second.Verdict.Communicated);
        Assert.Equal(first.Result.Verdict.Correctness, second.Verdict.Correctness);
        Assert.Equal(first.Result.Verdict.Confidence, second.Verdict.Confidence);
        Assert.Equal(first.Result.Verdict.Errors.Select(static e => e.Tag), second.Verdict.Errors.Select(static e => e.Tag));
        Assert.Equal(first.Result.Replay.ClipHash, second.Replay.ClipHash); // the record names the same audio
        Assert.Equal(clip.ClipHash, second.Replay.ClipHash);
        Assert.Equal(first.Result.Replay.HostStateHash, second.Replay.HostStateHash);
    }

    [ModelFact(needsKokoro: true)]
    public void ASpokenUseOfTheTreeEarnsFullCreditAndATypedUseHalfThroughTheWorker()
    {
        using var spoken = TestGame.NewSession(credit: CreditRules.Default);
        using var typed = TestGame.NewSession(credit: CreditRules.Default);
        var worker = spoken.StartWorker();
        Assert.True(worker.WaitUntilReady(60000));
        var load = worker.Load!;
        Assert.True(typed.StartWorker(loadSpeech: false).WaitUntilReady(30000));
        var cursor = TestGame.NearestBlock(spoken, BlockId.Trunk);

        _ = spoken.SubmitSpeak("es un árbol", cursor);
        _ = typed.SubmitTyped("es un árbol", cursor);
        var heard = Drain(spoken, 1).Single();
        _ = Drain(typed, 1);

        output.WriteLine($"load: guide {load.GuideMilliseconds:0} ms, vosk {load.SpeechMilliseconds:0} ms, warm-up {load.WarmUpMilliseconds:0} ms");
        output.WriteLine($"heard '{heard.Turn?.Line}' {heard.Turn?.Confidence:0.00} {heard.Outcome.Problem}");
        Assert.Equal(InputKind.Spoken, heard.Turn!.Input);
        Assert.Equal("es un árbol", heard.Turn.Line);
        Assert.Equal("name_object", heard.Turn.Intent);
        Assert.Equal(WordProgress.UnitsPerUse, spoken.Words.RecordOf("árbol").CreditUnits); // 1 de 3
        Assert.Equal(WordProgress.UnitsPerUse / 2, typed.Words.RecordOf("árbol").CreditUnits); // half a use
        Assert.Equal(["árbol"], heard.Words!.Credited);
    }

    [ModelFact(needsKokoro: true)]
    public void AWavFileIsFedThroughTheWorkerAsSpokenInput()
    {
        using var engine = SpeechEngine.TryCreate(out _)!;
        var clip = engine.Synthesize(TreeQuestion, out _);
        var path = Path.Combine(Path.GetTempPath(), $"vocaterra-test-{Environment.ProcessId}-{Guid.NewGuid():N}.wav");
        try
        {
            File.WriteAllBytes(path, Wav16(clip.Samples, clip.SampleRate));
            using var session = TestGame.NewSession();
            Assert.True(session.StartWorker().WaitUntilReady(60000));

            _ = session.SubmitWav(path);
            var applied = Drain(session, 1).Single();

            Assert.Equal("ask_directions", applied.Turn!.Intent);
            Assert.Equal(InputKind.Spoken, applied.Turn.Input);
            Assert.NotNull(session.Pointer);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [ModelFact(needsKokoro: true)]
    public void AMistakeInASpokenLineIsHeardAsSaidAndTheCoreCorrectsIt()
    {
        using var session = TestGame.NewSession();
        Assert.True(session.StartWorker().WaitUntilReady(60000));

        _ = session.SubmitSpeak("dónde está la árbol");
        var applied = Drain(session, 1).Single();

        var turn = applied.Turn!;
        output.WriteLine($"heard '{turn.Line}' ({turn.Confidence:0.00}) -> {turn.Summary}; errors {string.Join(", ", turn.Errors.Select(static e => $"{e.Tag} {e.Actual}->{e.Expected}"))}");
        foreach (var line in turn.GuideLines)
        {
            output.WriteLine("guide: " + line);
        }

        Assert.Equal("dónde está la árbol", turn.Line);
        Assert.Contains(turn.Errors, static e => e.Tag == "gender.agree");
        Assert.NotNull(turn.Feedback);
    }

    [ModelFact(needsKokoro: true)]
    public void TheWorkerReportsWhatLoadingCostAndWhatASpokenTurnCost()
    {
        using var session = TestGame.NewSession();
        var worker = session.StartWorker(() => Environment.TickCount64);
        Assert.True(worker.WaitUntilReady(60000));

        _ = session.SubmitSpeak(TreeQuestion);
        var applied = Drain(session, 1).Single();

        Assert.True(worker.CanHear);
        Assert.True(worker.Load!.CanSpeak);
        Assert.Null(worker.Load.Problem);
        Assert.True(applied.Outcome.Timings.TotalMilliseconds >= applied.Outcome.Timings.RecognizeMilliseconds);
    }

    [ModelFact(needsKokoro: true)]
    public void ASpokenRequestAtTheStallThroughTheWorkerHandsOverTheBottleLikeATypedOne()
    {
        using var session = TestGame.NewSession();
        Assert.True(session.StartWorker().WaitUntilReady(60000));
        Stage.TeleportToNpc(session, "shopkeeper");
        var opening = session.OpenTalk("shopkeeper");
        Assert.Equal(["¿Qué necesitas?"], opening.Lines);

        _ = session.SubmitSpeak("quiero agua, por favor");
        var applied = Drain(session, 1).Single();

        var turn = applied.Turn!;
        output.WriteLine($"heard '{turn.Line}' ({turn.Confidence:0.00}) -> {turn.Summary}, {string.Join(' ', turn.GuideLines)}");
        Assert.Equal(InputKind.Spoken, turn.Input);
        Assert.Equal("shopkeeper", turn.Npc);
        Assert.Equal("stall-need", turn.Prompt);
        Assert.Equal("request_item", turn.Intent);
        Assert.Contains("stall.give_water", turn.HostEvents);
        Assert.Equal(1, session.Simulation.Inventory.Count(Vocaterra.Simulation.Items.ItemId.Bottle));
        Assert.Contains(session.Events.OfType<PhraseUse>(), static u => u is { PhraseId: PhraseIds.RequestWater, Kind: PhraseUseKind.Supported });
    }

    private static List<AppliedTurn> Drain(VoxelSession session, int count)
    {
        var all = new List<AppliedTurn>();
        var deadline = Environment.TickCount64 + 120000;
        while (all.Count < count)
        {
            all.AddRange(session.Collect());
            if (all.Count >= count)
            {
                break;
            }

            Assert.True(Environment.TickCount64 < deadline, $"Only {all.Count} of {count} turns came back.");
            Thread.Sleep(5);
        }

        return all;
    }

    private static byte[] Wav16(short[] samples, int rate)
    {
        var data = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(i * 2), samples[i]);
        }

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + data.Length);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write(rate);
        writer.Write(rate * 2);
        writer.Write((ushort)2);
        writer.Write((ushort)16);
        writer.Write("data"u8);
        writer.Write(data.Length);
        writer.Write(data);
        return stream.ToArray();
    }
}
