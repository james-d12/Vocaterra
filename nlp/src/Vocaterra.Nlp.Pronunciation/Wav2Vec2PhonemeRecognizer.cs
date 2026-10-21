using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Stages;

namespace Vocaterra.Nlp.Pronunciation;

/// <summary>
/// ONNX adapter for the <c>wav2vec2-xlsr-53-espeak-cv-ft</c> phoneme recognizer (stage D1). The model directory holds
/// <c>model.onnx</c> and <c>vocab.json</c> (label to id). The waveform is normalized to zero mean and unit variance, the logits go
/// through a log-softmax per frame, and labels are mapped to the <c>Vocaterra.Nlp.Phonology</c> inventory with <see cref="EspeakPhoneMap"/>
/// (the padding token becomes the CTC blank). Labels that map to the same phone are merged by adding their probabilities, so the
/// output vocabulary has unique symbols. The session is CPU only with one intra-op and one inter-op thread.
/// </summary>
public sealed class Wav2Vec2PhonemeRecognizer : IPhonemeRecognizer, IDisposable
{
    /// <summary>File name of the ONNX model inside the model directory.</summary>
    public const string ModelFileName = "model.onnx";

    /// <summary>File name of the vocabulary inside the model directory.</summary>
    public const string VocabFileName = "vocab.json";

    /// <summary>Seconds between output frames (a 20 ms stride).</summary>
    public const double FrameSeconds = 0.02;

    /// <summary>The padding token of the model vocabulary, which is the CTC blank.</summary>
    public const string PaddingToken = "<pad>";

    /// <summary>The shortest input the convolutional front end accepts (one 25 ms window at 16 kHz).</summary>
    internal const int MinimumSamples = 400;

    private const double NormalizationEpsilon = 1e-7;

    private readonly InferenceSession session;
    private readonly string inputName;
    private readonly string outputName;
    private readonly string[] symbols;
    private readonly int[][] groups;
    private readonly int modelVocabularySize;

    /// <summary>Loads the model and vocabulary from a directory and hashes <c>model.onnx</c> once.</summary>
    public Wav2Vec2PhonemeRecognizer(string modelDir)
        : this(modelDir, EspeakPhoneMap.Default)
    {
    }

    /// <summary>Loads the model with a specific label mapping.</summary>
    public Wav2Vec2PhonemeRecognizer(string modelDir, EspeakPhoneMap labelMap)
    {
        ArgumentException.ThrowIfNullOrEmpty(modelDir);
        ArgumentNullException.ThrowIfNull(labelMap);
        var modelPath = Path.Combine(modelDir, ModelFileName);
        var vocabPath = Path.Combine(modelDir, VocabFileName);
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"Model file not found: {modelPath}", modelPath);
        }

        if (!File.Exists(vocabPath))
        {
            throw new FileNotFoundException($"Vocabulary file not found: {vocabPath}", vocabPath);
        }

        var labels = ReadVocabulary(File.ReadAllText(vocabPath));
        var mapping = VocabularyMapping.Build(labels, labelMap);
        symbols = mapping.Symbols;
        groups = mapping.Groups;
        modelVocabularySize = labels.Count;
        UnmappedLabels = mapping.Unmapped;

        ModelHash = HashFile(modelPath);

        using var options = new SessionOptions
        {
            IntraOpNumThreads = 1,
            InterOpNumThreads = 1,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
        };
        session = new InferenceSession(modelPath, options);
        if (session.InputNames.Count != 1 || session.OutputNames.Count < 1)
        {
            session.Dispose();
            throw new NotSupportedException("Expected a model with one waveform input and a logits output.");
        }

        inputName = session.InputNames[0];
        outputName = session.OutputNames[0];
    }

    /// <summary>SHA-256 of <c>model.onnx</c> as lowercase hex, computed once at construction.</summary>
    public string ModelHash { get; }

    /// <summary>Vocabulary labels (sorted ordinally) that are neither in the inventory nor in the mapping table; they are kept verbatim.</summary>
    public IReadOnlyList<string> UnmappedLabels { get; }

    /// <summary>The output vocabulary: unique mapped symbols in model order, with the blank included.</summary>
    public IReadOnlyList<string> Vocabulary => Array.AsReadOnly(symbols);

    /// <summary>Runs the model on a 16 kHz clip and returns log-probabilities per 20 ms frame; a clip shorter than 400 samples yields no frames.</summary>
    public PhonePosteriors Recognize(PcmClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        if (clip.SampleRate != PcmClip.DefaultSampleRate)
        {
            throw new ArgumentException($"The recognizer needs {PcmClip.DefaultSampleRate} Hz audio but the clip is {clip.SampleRate} Hz.", nameof(clip));
        }

        if (clip.Samples.Length < MinimumSamples)
        {
            return new PhonePosteriors(Array.AsReadOnly(symbols), [], 0, FrameSeconds);
        }

        var waveform = NormalizeWaveform(clip.Samples);
        using var input = OrtValue.CreateTensorValueFromMemory(waveform, [1L, waveform.Length]);
        using var runOptions = new RunOptions();
        using var outputs = session.Run(runOptions, [inputName], [input], [outputName]);
        var logits = outputs[0];
        var shape = logits.GetTensorTypeAndShape().Shape;
        if (shape.Length != 3 || shape[0] != 1 || shape[2] != modelVocabularySize)
        {
            throw new InvalidOperationException(
                $"Unexpected logits shape [{string.Join(", ", shape)}]; expected [1, frames, {modelVocabularySize}].");
        }

        var frames = checked((int)shape[1]);
        var logProbs = LogSoftmaxAndMerge(logits.GetTensorDataAsSpan<float>(), frames, modelVocabularySize, groups);
        return new PhonePosteriors(Array.AsReadOnly(symbols), logProbs, frames, FrameSeconds);
    }

    /// <summary>Releases the ONNX session.</summary>
    public void Dispose()
    {
        session.Dispose();
    }

    /// <summary>Scales samples to [-1, 1) and normalizes them to zero mean and unit variance; sums run in index order in double precision.</summary>
    internal static float[] NormalizeWaveform(ReadOnlySpan<short> samples)
    {
        if (samples.Length == 0)
        {
            throw new ArgumentException("The clip has no samples.", nameof(samples));
        }

        var sum = 0.0;
        for (var i = 0; i < samples.Length; i++)
        {
            sum += samples[i] / 32768.0;
        }

        var mean = sum / samples.Length;
        var squares = 0.0;
        for (var i = 0; i < samples.Length; i++)
        {
            var centred = (samples[i] / 32768.0) - mean;
            squares += centred * centred;
        }

        var scale = 1.0 / Math.Sqrt((squares / samples.Length) + NormalizationEpsilon);
        var result = new float[samples.Length];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = (float)(((samples[i] / 32768.0) - mean) * scale);
        }

        return result;
    }

    /// <summary>
    /// Turns raw logits [frames, vocab] into log-probabilities, then merges the columns of each group by adding probabilities
    /// (a log-sum-exp in column order). The result is [frames, groups].
    /// </summary>
    internal static float[] LogSoftmaxAndMerge(ReadOnlySpan<float> logits, int frames, int vocabularySize, int[][] groups)
    {
        var merged = new float[frames * groups.Length];
        var row = new double[vocabularySize];
        for (var frame = 0; frame < frames; frame++)
        {
            var offset = frame * vocabularySize;
            var max = double.NegativeInfinity;
            for (var v = 0; v < vocabularySize; v++)
            {
                row[v] = logits[offset + v];
                if (row[v] > max)
                {
                    max = row[v];
                }
            }

            var total = 0.0;
            for (var v = 0; v < vocabularySize; v++)
            {
                total += Math.Exp(row[v] - max);
            }

            var logNormalizer = max + Math.Log(total);
            for (var g = 0; g < groups.Length; g++)
            {
                var members = groups[g];
                double value;
                if (members.Length == 1)
                {
                    value = row[members[0]] - logNormalizer;
                }
                else
                {
                    var top = double.NegativeInfinity;
                    foreach (var member in members)
                    {
                        top = Math.Max(top, row[member]);
                    }

                    var mass = 0.0;
                    foreach (var member in members)
                    {
                        mass += Math.Exp(row[member] - top);
                    }

                    value = top + Math.Log(mass) - logNormalizer;
                }

                merged[(frame * groups.Length) + g] = (float)value;
            }
        }

        return merged;
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private static IReadOnlyDictionary<string, int> ReadVocabulary(string json)
    {
        using var document = JsonDocument.Parse(json);
        var labels = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            labels.Add(property.Name, property.Value.GetInt32());
        }

        return labels;
    }
}
