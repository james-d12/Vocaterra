using Vocaterra.Nlp.Asr.Models;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Asr.Tests;

/// <summary>Tests for the Vosk adapter: those that need no model, and the model-gated runs.</summary>
[Collection(NativeRuntimeCollection.Name)]
public class VoskTests
{
    private static readonly string[] Phrases = ["hola", "adiós"];

    [Fact]
    public void ConstructingWithAnAbsentDirectoryThrows()
    {
        var missing = Path.Combine(Path.GetTempPath(), "vocaterra-no-such-vosk-model");

        Assert.Throws<DirectoryNotFoundException>(() => new VoskWordRecognizer(missing));
    }

    [VoskModelFact]
    public void ConstrainedSilenceIsUnknownOrEmptyAndRepeatable()
    {
        using var recognizer = VoskWordRecognizer.FromStore(ModelGate.Store!, ModelGate.VoskId);
        var clip = TestData.SilenceClip(2.0);
        var request = RecognitionRequest.Constrained(Phrases);

        var first = recognizer.Recognize(clip, request);
        var second = recognizer.Recognize(clip, request);

        Assert.Contains(first.Transcript, new[] { string.Empty, GrammarBuilder.UnknownToken });
        Assert.Equal(RecognitionMode.Constrained, first.Mode);
        Assert.Equal(recognizer.ModelHash, first.ModelHash);
        Assert.Equal(ModelGate.Store!.ModelHash(ModelGate.VoskId), first.ModelHash);
        Assert.NotEmpty(first.NBest);
        Assert.Equal(first, second);
    }

    [VoskModelFact]
    public void TwoSeparateRecognizersGiveIdenticalResults()
    {
        var clip = TestData.VowelClip();
        var request = RecognitionRequest.Constrained(Phrases, 3);
        var directory = ModelGate.Store!.ModelDirectory(ModelGate.VoskId);

        AsrResult first;
        using (var recognizer = new VoskWordRecognizer(directory))
        {
            first = recognizer.Recognize(clip, request);
        }

        using var again = new VoskWordRecognizer(directory);
        var second = again.Recognize(clip, request);

        Assert.Equal(first, second);
        Assert.Equal(ModelStore.HashDirectory(directory), again.ModelHash);
    }

    [VoskModelFact]
    public void RepeatedDecodesOfSpeechLikeAudioAreBitIdentical()
    {
        // Stock Vosk models dither with a time-seeded generator, so without the adapter's dither-free runtime copy
        // every one of these decodes would differ in its scores and often in its n-best order.
        using var recognizer = VoskWordRecognizer.FromStore(ModelGate.Store!, ModelGate.VoskId);
        var clip = TestData.VowelClip();

        foreach (var request in new[] { RecognitionRequest.Open(0), RecognitionRequest.Open(5), RecognitionRequest.Constrained(Phrases, 5) })
        {
            var expected = recognizer.Recognize(clip, request);
            for (var i = 0; i < 4; i++)
            {
                var again = recognizer.Recognize(clip, request);
                Assert.Equal(AsrResultJson.Serialize(expected), AsrResultJson.Serialize(again));
            }
        }
    }

    [VoskModelFact]
    public void AlternativesAreReturnedInScoreOrderAndKeepTheBestPathWords()
    {
        using var recognizer = VoskWordRecognizer.FromStore(ModelGate.Store!, ModelGate.VoskId);
        var clip = TestData.VowelClip();

        var result = recognizer.Recognize(clip, RecognitionRequest.Open(5));

        Assert.NotEmpty(result.Transcript);
        Assert.InRange(result.NBest.Count, 2, 5);
        Assert.Equal(result.Transcript, result.NBest[0].Text);
        Assert.Equal(result.NBest[0].Score, result.Confidence);
        Assert.Equal(1.0, result.NBest.Sum(h => h.Score), 9);
        Assert.Equal(result.NBest.OrderByDescending(h => h.Score).Select(h => h.Text).ToArray(), result.NBest.Select(h => h.Text).ToArray());
        Assert.Equal(result.Transcript, string.Join(' ', result.Words.Select(w => w.Text)));
        Assert.All(result.Words, w => Assert.InRange(w.Confidence, 0.0, 1.0));
        Assert.All(result.Words, w => Assert.True(w.StartSeconds <= w.EndSeconds));
    }

    [VoskModelFact]
    public void OpenModeRunsWithoutAGrammarAndKeepsTheRequestedMode()
    {
        using var recognizer = VoskWordRecognizer.FromStore(ModelGate.Store!, ModelGate.VoskId);
        var clip = TestData.SilenceClip(1.0);

        var result = recognizer.Recognize(clip, RecognitionRequest.Open(3));

        Assert.Equal(RecognitionMode.Open, result.Mode);
        Assert.Equal(string.Empty, result.Transcript);
    }

    [VoskModelFact]
    public void ClipsAtAnotherSampleRateAndEmptyGrammarsAreRejected()
    {
        using var recognizer = VoskWordRecognizer.FromStore(ModelGate.Store!, ModelGate.VoskId);
        var wrongRate = Vocaterra.Nlp.Core.Audio.PcmClip.FromSamples(new short[8000], 8000);

        Assert.Throws<ArgumentException>(() => recognizer.Recognize(wrongRate, RecognitionRequest.Constrained(Phrases)));
        Assert.Throws<ArgumentException>(() => recognizer.Recognize(TestData.SilenceClip(0.5), RecognitionRequest.Constrained([])));
    }

    [VoskModelFact]
    public void DisposedRecognizerRefusesFurtherCalls()
    {
        var recognizer = VoskWordRecognizer.FromStore(ModelGate.Store!, ModelGate.VoskId);
        recognizer.Dispose();
        recognizer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => recognizer.Recognize(TestData.SilenceClip(0.5), RecognitionRequest.Open()));
    }

    [VoskModelFact]
    public void FetchedVoskModelMatchesTheCommittedManifest()
    {
        var store = ModelGate.Store!;

        var report = store.Verify(ModelGate.VoskId);

        Assert.Equal(ModelStatus.Ok, report.Status);
        Assert.Equal(ModelStore.HashDirectory(store.ModelDirectory(ModelGate.VoskId)), store.ModelHash(ModelGate.VoskId));
    }
}
