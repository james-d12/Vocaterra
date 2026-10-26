using Vocaterra.Nlp.Asr.Models;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Prompts;
using Vocaterra.Nlp.Core.Stages;
using VoskModel = Vosk.Model;
using VoskRecognizer = Vosk.VoskRecognizer;

namespace Vocaterra.Nlp.Asr;

/// <summary>Settings of <see cref="VoskWordRecognizer"/>.</summary>
public sealed record VoskOptions
{
    /// <summary>The default options.</summary>
    public static VoskOptions Default { get; } = new();

    /// <summary>
    /// The model hash to report, normally <see cref="ModelStore.ModelHash"/> of the manifest entry. When null the recognizer hashes
    /// every file of the model directory with <see cref="ModelStore.HashDirectory"/>.
    /// </summary>
    public string? ModelHash { get; init; }

    /// <summary>Upper bound applied to <see cref="RecognitionRequest.MaxAlternatives"/>.</summary>
    public int MaxAlternativesLimit { get; init; } = 20;

    /// <summary>
    /// Directory in which the dither-free runtime copy of the model is kept (see the remarks of <see cref="VoskWordRecognizer"/>);
    /// null means a folder in the temp directory.
    /// </summary>
    public string? RuntimeRoot { get; init; }
}

/// <summary>
/// Vosk (Kaldi) recognizer: grammar-constrained for <see cref="RecognitionMode.Constrained"/> requests, free vocabulary otherwise.
/// The model is loaded once; every call creates its own native recognizer so no mutable state is shared between calls.
/// </summary>
/// <remarks>
/// Stock Vosk models keep Kaldi's default <c>--dither=1.0</c>, which injects time-seeded noise and makes every decode differ slightly.
/// To honour the determinism contract of <see cref="IWordRecognizer"/> the adapter loads a runtime copy of the model directory
/// (links to the original files) whose <c>conf/mfcc.conf</c> sets <c>--dither=0.0</c>. <see cref="ModelHash"/> still identifies the
/// original files, which is what the manifest pins.
/// </remarks>
public sealed class VoskWordRecognizer : IWordRecognizer, IDisposable
{
    /// <summary>The only sample rate the Vosk models accept, in hertz.</summary>
    public const int SampleRate = PcmClip.DefaultSampleRate;

    private readonly VoskModel _model;
    private readonly VoskOptions _options;
    private int _disposed;

    static VoskWordRecognizer()
    {
        Vosk.Vosk.SetLogLevel(-1);
    }

    /// <summary>Loads the model in <paramref name="modelDir"/>.</summary>
    /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
    public VoskWordRecognizer(string modelDir, VoskOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(modelDir);
        if (!Directory.Exists(modelDir))
        {
            throw new DirectoryNotFoundException($"Vosk model directory '{modelDir}' does not exist.");
        }

        _options = options ?? VoskOptions.Default;
        ModelHash = _options.ModelHash ?? ModelStore.HashDirectory(modelDir);
        _model = new VoskModel(VoskRuntimeModel.Prepare(modelDir, ModelHash, _options.RuntimeRoot));
    }

    /// <inheritdoc />
    public string ModelHash { get; }

    /// <summary>Creates a recognizer for the manifest entry <paramref name="id"/>, using the manifest's model hash.</summary>
    public static VoskWordRecognizer FromStore(ModelStore store, string id)
    {
        ArgumentNullException.ThrowIfNull(store);
        return new VoskWordRecognizer(store.ModelDirectory(id), new VoskOptions { ModelHash = store.ModelHash(id) });
    }

    /// <inheritdoc />
    public AsrResult Recognize(PcmClip clip, RecognitionRequest request)
    {
        ArgumentNullException.ThrowIfNull(clip);
        ArgumentNullException.ThrowIfNull(request);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (clip.SampleRate != SampleRate)
        {
            throw new ArgumentException($"Vosk models need {SampleRate} Hz audio but the clip is {clip.SampleRate} Hz.", nameof(clip));
        }

        var alternatives = Math.Clamp(request.MaxAlternatives, 0, _options.MaxAlternativesLimit);

        // Vosk only reports per-word confidences when alternatives are off, so the best path is always decoded that way first;
        // an n-best list needs a second pass, whose top hypothesis normally has the same text and then inherits those words.
        var best = Decode(clip, request, 0);
        if (alternatives < 2)
        {
            return best;
        }

        var nbest = Decode(clip, request, alternatives);
        return string.Equals(nbest.Transcript, best.Transcript, StringComparison.Ordinal) ? nbest with { Words = best.Words } : nbest;
    }

    /// <summary>Releases the native model.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _model.Dispose();
        }
    }

    private AsrResult Decode(PcmClip clip, RecognitionRequest request, int alternatives)
    {
        // One native recognizer per decode: nothing mutable is shared between calls.
        using var recognizer = CreateRecognizer(request);
        recognizer.SetMaxAlternatives(alternatives);
        recognizer.SetWords(true);

        // The whole clip goes in one call, so Vosk can detect at most one endpoint, at the very end: then Result() holds the
        // complete utterance and FinalResult() would be empty; otherwise FinalResult() flushes it.
        var samples = clip.Samples;
        var endpoint = samples.Length > 0 && recognizer.AcceptWaveform(samples, samples.Length);
        var json = endpoint ? recognizer.Result() : recognizer.FinalResult();
        return VoskResultParser.Parse(json, request.Mode, ModelHash);
    }

    private VoskRecognizer CreateRecognizer(RecognitionRequest request)
    {
        if (request.Mode == RecognitionMode.Constrained)
        {
            var phrases = GrammarBuilder.BuildList(request.GrammarPhrases);
            if (phrases.Count < 2)
            {
                throw new ArgumentException("A constrained request needs at least one usable grammar phrase.", nameof(request));
            }

            return new VoskRecognizer(_model, SampleRate, GrammarBuilder.Build(request.GrammarPhrases));
        }

        return new VoskRecognizer(_model, SampleRate);
    }
}
