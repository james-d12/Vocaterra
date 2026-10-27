using Vocaterra.Nlp.Core.Audio;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Phonology;

namespace Vocaterra.Nlp.Pronunciation;

/// <summary>
/// A deterministic test double for phoneme recognition. It builds posteriors from a phone string and can replay them from
/// <see cref="Recognize"/>. Frames put a log-probability of 0 on the intended symbol and <see cref="FloorLogProb"/> on every other symbol,
/// so rows are not normalized; scorers only use ratios and maxima.
/// </summary>
public sealed class FakePhonemeRecognizer : IPhonemeRecognizer
{
    /// <summary>Model hash reported when none is given.</summary>
    public const string DefaultModelHash = "fake-phoneme-recognizer";

    /// <summary>Log-probability of the intended symbol.</summary>
    public const float TargetLogProb = 0.0f;

    /// <summary>Log-probability of every symbol that is not intended.</summary>
    public const float FloorLogProb = -10.0f;

    /// <summary>Frame length of the generated posteriors in seconds (the wav2vec2 stride).</summary>
    public const double FrameSeconds = 0.02;

    private readonly string[] vocabulary;
    private PhonePosteriors? canned;

    /// <summary>Creates a fake over a phone vocabulary; the blank symbol is added at index 0 and must not be listed.</summary>
    public FakePhonemeRecognizer(IReadOnlyList<string> vocabulary, string modelHash = DefaultModelHash)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);
        ArgumentNullException.ThrowIfNull(modelHash);
        var symbols = new List<string>(vocabulary.Count + 1) { CtcDecoder.BlankSymbol };
        foreach (var phone in vocabulary)
        {
            ArgumentNullException.ThrowIfNull(phone);
            if (symbols.Contains(phone))
            {
                throw new ArgumentException($"The vocabulary lists '{phone}' twice or contains the blank symbol.", nameof(vocabulary));
            }

            symbols.Add(phone);
        }

        this.vocabulary = [.. symbols];
        ModelHash = modelHash;
    }

    /// <summary>A fake over the whole Vocaterra.Nlp.Phonology inventory, including the learner phones.</summary>
    public static FakePhonemeRecognizer ForPhoneInventory()
    {
        return new FakePhonemeRecognizer(PhoneInventory.All);
    }

    /// <inheritdoc />
    public string ModelHash { get; }

    /// <summary>The full vocabulary of the generated posteriors, blank first.</summary>
    public IReadOnlyList<string> Vocabulary => Array.AsReadOnly(vocabulary);

    /// <summary>Sets the posteriors <see cref="Recognize"/> returns for every clip, and returns this fake.</summary>
    public FakePhonemeRecognizer Returns(PhonePosteriors posteriors)
    {
        canned = posteriors ?? throw new ArgumentNullException(nameof(posteriors));
        return this;
    }

    /// <summary>Returns the posteriors set with <see cref="Returns"/>, ignoring the clip; throws when none were set.</summary>
    public PhonePosteriors Recognize(PcmClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        return canned ?? throw new InvalidOperationException("No posteriors were set; call Returns(...) first.");
    }

    /// <summary>
    /// Builds posteriors that spell the phones: each phone holds <paramref name="framesPerPhone"/> frames and
    /// <paramref name="blankFramesBetween"/> blank frames follow it (except after the last phone), with
    /// <paramref name="edgeBlankFrames"/> blank frames before the first and after the last phone. Equal adjacent phones need at least
    /// one blank frame between them to survive CTC collapse. <paramref name="confusions"/> replaces every occurrence of a key phone
    /// by its value (for example <c>ɾ</c> by <c>ɹ</c>), which is how a mispronunciation is injected.
    /// </summary>
    public PhonePosteriors FromPhones(
        IReadOnlyList<string> phones,
        int framesPerPhone = 3,
        int blankFramesBetween = 1,
        IReadOnlyDictionary<string, string>? confusions = null,
        int edgeBlankFrames = 0)
    {
        return Build(phones, framesPerPhone, blankFramesBetween, edgeBlankFrames, confusions, -1, null, 0.0);
    }

    /// <summary>
    /// Like <see cref="FromPhones"/> but the frames of one phone are mumbled: the intended phone keeps probability
    /// <paramref name="targetMass"/> and <paramref name="alternative"/> takes the rest. A target mass below one half makes the alternative win the argmax.
    /// </summary>
    public PhonePosteriors FromMumbled(
        IReadOnlyList<string> phones,
        int mumbledIndex,
        string alternative,
        double targetMass,
        int framesPerPhone = 3,
        int blankFramesBetween = 1,
        int edgeBlankFrames = 0)
    {
        ArgumentNullException.ThrowIfNull(alternative);
        if (!(targetMass > 0.0 && targetMass < 1.0))
        {
            throw new ArgumentOutOfRangeException(nameof(targetMass), targetMass, "Target mass must lie strictly between 0 and 1.");
        }

        return Build(phones, framesPerPhone, blankFramesBetween, edgeBlankFrames, null, mumbledIndex, alternative, targetMass);
    }

    private PhonePosteriors Build(
        IReadOnlyList<string> phones,
        int framesPerPhone,
        int blankFramesBetween,
        int edgeBlankFrames,
        IReadOnlyDictionary<string, string>? confusions,
        int mumbledIndex,
        string? alternative,
        double targetMass)
    {
        ArgumentNullException.ThrowIfNull(phones);
        ArgumentOutOfRangeException.ThrowIfLessThan(framesPerPhone, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(blankFramesBetween);
        ArgumentOutOfRangeException.ThrowIfNegative(edgeBlankFrames);
        if (alternative is not null && (mumbledIndex < 0 || mumbledIndex >= phones.Count))
        {
            throw new ArgumentOutOfRangeException(nameof(mumbledIndex), mumbledIndex, "The mumbled index must name a phone.");
        }

        var rows = new List<float[]>();
        AddBlankFrames(rows, edgeBlankFrames);
        for (var i = 0; i < phones.Count; i++)
        {
            var phone = phones[i];
            if (confusions is not null && confusions.TryGetValue(phone, out var replacement))
            {
                phone = replacement;
            }

            var row = i == mumbledIndex && alternative is not null
                ? MumbledRow(Column(phone), targetMass, Column(alternative))
                : HotRow(Column(phone));
            for (var f = 0; f < framesPerPhone; f++)
            {
                rows.Add(row);
            }

            if (i < phones.Count - 1)
            {
                AddBlankFrames(rows, blankFramesBetween);
            }
        }

        if (phones.Count > 0)
        {
            AddBlankFrames(rows, edgeBlankFrames);
        }

        var logProbs = new float[rows.Count * vocabulary.Length];
        for (var frame = 0; frame < rows.Count; frame++)
        {
            Array.Copy(rows[frame], 0, logProbs, frame * vocabulary.Length, vocabulary.Length);
        }

        return new PhonePosteriors(Array.AsReadOnly(vocabulary), logProbs, rows.Count, FrameSeconds);
    }

    private void AddBlankFrames(List<float[]> rows, int count)
    {
        var blank = HotRow(0);
        for (var i = 0; i < count; i++)
        {
            rows.Add(blank);
        }
    }

    private int Column(string phone)
    {
        var column = Array.IndexOf(vocabulary, phone);
        return column > 0
            ? column
            : throw new ArgumentException($"'{phone}' is not a phone of this fake's vocabulary.", nameof(phone));
    }

    private float[] HotRow(int column)
    {
        var row = new float[vocabulary.Length];
        Array.Fill(row, FloorLogProb);
        row[column] = TargetLogProb;
        return row;
    }

    private float[] MumbledRow(int target, double targetMass, int alternative)
    {
        var row = new float[vocabulary.Length];
        Array.Fill(row, FloorLogProb);
        row[target] = (float)Math.Log(targetMass);
        row[alternative] = (float)Math.Log(1.0 - targetMass);
        return row;
    }
}
