using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Prompts;

namespace Vocaterra.Nlp.Pronunciation;

/// <summary>
/// Reject/accept thresholds for phone scores, one <see cref="Margin"/> per <see cref="PhoneClass"/> and <see cref="Difficulty"/>.
/// A phone is low when its margin evaluates to <see cref="MarginDecision.Reject"/>; an <see cref="MarginDecision.Uncertain"/> score is never tagged.
/// Harder prompts are stricter (higher threshold) and tap/trill is the most forgiving class. Every margin uses an epsilon of 0.10.
/// </summary>
/// <remarks>
/// Default thresholds (beginner / intermediate / advanced): vowel .35/.45/.55, tap_trill .25/.35/.45, fricative .30/.40/.50,
/// stop .35/.45/.55, nasal .35/.45/.55, lateral .30/.40/.50, glide .30/.40/.50. These are starting values to be calibrated on the golden learner audio.
/// </remarks>
public sealed class PronunciationThresholds
{
    private const double DefaultEpsilon = 0.10;

    private static readonly double[][] DefaultThresholds =
    [
        [0.35, 0.45, 0.55],
        [0.25, 0.35, 0.45],
        [0.30, 0.40, 0.50],
        [0.35, 0.45, 0.55],
        [0.35, 0.45, 0.55],
        [0.30, 0.40, 0.50],
        [0.30, 0.40, 0.50],
    ];

    private static readonly int DifficultyCount = Enum.GetValues<Difficulty>().Length;

    private readonly Margin[] margins;

    private PronunciationThresholds(Margin[] margins)
    {
        this.margins = margins;
    }

    /// <summary>The shipped thresholds.</summary>
    public static PronunciationThresholds Default { get; } = CreateDefault();

    /// <summary>Returns the margin for a phone class at a difficulty.</summary>
    public Margin Get(PhoneClass phoneClass, Difficulty difficulty)
    {
        return margins[IndexOf(phoneClass, difficulty)];
    }

    /// <summary>Returns the margin that applies to a phone token at a difficulty.</summary>
    public Margin For(string phone, Difficulty difficulty)
    {
        return Get(PhoneClasses.Of(phone), difficulty);
    }

    /// <summary>Evaluates a phone score against the margin for the phone's class and the difficulty.</summary>
    public MarginDecision Evaluate(string phone, Difficulty difficulty, double score)
    {
        return For(phone, difficulty).Evaluate(score);
    }

    /// <summary>Returns a copy with one margin replaced.</summary>
    public PronunciationThresholds With(PhoneClass phoneClass, Difficulty difficulty, Margin margin)
    {
        var copy = (Margin[])margins.Clone();
        copy[IndexOf(phoneClass, difficulty)] = margin;
        return new PronunciationThresholds(copy);
    }

    private static PronunciationThresholds CreateDefault()
    {
        var all = new Margin[PhoneClasses.All.Count * DifficultyCount];
        for (var c = 0; c < PhoneClasses.All.Count; c++)
        {
            for (var d = 0; d < DifficultyCount; d++)
            {
                all[(c * DifficultyCount) + d] = new Margin(DefaultThresholds[c][d], DefaultEpsilon);
            }
        }

        return new PronunciationThresholds(all);
    }

    private static int IndexOf(PhoneClass phoneClass, Difficulty difficulty)
    {
        var c = (int)phoneClass;
        var d = (int)difficulty;
        if (c < 0 || c >= PhoneClasses.All.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(phoneClass), phoneClass, "Unknown phone class.");
        }

        if (d < 0 || d >= DifficultyCount)
        {
            throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Unknown difficulty.");
        }

        return (c * DifficultyCount) + d;
    }
}
