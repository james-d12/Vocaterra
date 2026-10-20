using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Decision.Scheduling;

/// <summary>
/// Pure-function FSRS-4.5 scheduler (stability, difficulty, retrievability) following the open-spaced-repetition reference.
/// Time arrives only as <see cref="StoredTimestamp"/>; elapsed time is counted in whole days (floor), stability, difficulty
/// and retrievability are rounded to 8 decimals after every update as the reference does, and there are no sub-day learning
/// steps: every rating, including Again, schedules at least one day ahead.
/// </summary>
public static class Fsrs
{
    /// <summary>Exponent of the forgetting curve.</summary>
    public const double Decay = -0.5;

    /// <summary>Scale of the forgetting curve, chosen so that retrievability equals 0.9 after exactly one stability.</summary>
    public const double Factor = 19.0 / 81.0;

    private const long SecondsPerDay = 86_400;
    private const double MinStability = 0.1;
    private const double MinDifficulty = 1.0;
    private const double MaxDifficulty = 10.0;

    /// <summary>Reviews the card with <see cref="FsrsParameters.Default"/>.</summary>
    public static ReviewCard Review(ReviewCard card, Rating rating, StoredTimestamp now)
    {
        return Review(card, rating, now, FsrsParameters.Default);
    }

    /// <summary>
    /// Returns the card after a review at <paramref name="now"/>. A new card takes its initial stability and difficulty from the
    /// weights; a reviewed card is updated from its retrievability after the elapsed whole days. <c>Due</c> is
    /// <c>now + interval days</c> where the interval is the rounded stability-based interval (at least 1, at most the maximum).
    /// </summary>
    public static ReviewCard Review(ReviewCard card, Rating rating, StoredTimestamp now, FsrsParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(parameters);
        if (rating is < Rating.Again or > Rating.Easy)
        {
            throw new ArgumentOutOfRangeException(nameof(rating));
        }

        var w = parameters.Weights;
        if (card.IsNew)
        {
            var stability = InitialStability(w, rating);
            var difficulty = InitialDifficulty(w, rating);
            return Finish(card, stability, difficulty, Interval(stability, parameters), now, lapse: false);
        }

        var elapsedDays = ElapsedDays(card.LastReview, now);
        var retrievability = Retrievability(elapsedDays, card.Stability);
        var nextDifficulty = NextDifficulty(w, card.Difficulty, rating);

        if (rating == Rating.Again)
        {
            var forgotten = NextForgetStability(w, card.Difficulty, card.Stability, retrievability);
            return Finish(card, forgotten, nextDifficulty, Interval(forgotten, parameters), now, lapse: true);
        }

        // As in the reference, the three passing ratings are scheduled together so that hard < good < easy always holds.
        var hardStability = NextRecallStability(w, card.Difficulty, card.Stability, retrievability, Rating.Hard);
        var goodStability = NextRecallStability(w, card.Difficulty, card.Stability, retrievability, Rating.Good);
        var easyStability = NextRecallStability(w, card.Difficulty, card.Stability, retrievability, Rating.Easy);
        var hardInterval = Interval(hardStability, parameters);
        var goodInterval = Interval(goodStability, parameters);
        hardInterval = Math.Min(hardInterval, goodInterval);
        goodInterval = Math.Max(goodInterval, hardInterval + 1);
        var easyInterval = Math.Max(Interval(easyStability, parameters), goodInterval + 1);

        return rating switch
        {
            Rating.Hard => Finish(card, hardStability, nextDifficulty, Math.Min(hardInterval, parameters.MaximumIntervalDays), now, lapse: false),
            Rating.Good => Finish(card, goodStability, nextDifficulty, Math.Min(goodInterval, parameters.MaximumIntervalDays), now, lapse: false),
            _ => Finish(card, easyStability, nextDifficulty, Math.Min(easyInterval, parameters.MaximumIntervalDays), now, lapse: false),
        };
    }

    /// <summary>The probability of recalling the card at <paramref name="now"/>; 0 for a card that was never reviewed.</summary>
    public static double Retrievability(ReviewCard card, StoredTimestamp now)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.IsNew ? 0.0 : Retrievability(ElapsedDays(card.LastReview, now), card.Stability);
    }

    /// <summary>The forgetting curve <c>(1 + FACTOR * t / S) ^ DECAY</c>, rounded to 8 decimals.</summary>
    public static double Retrievability(double elapsedDays, double stability)
    {
        return Round8(Math.Pow(1.0 + (Factor * elapsedDays / stability), Decay));
    }

    /// <summary>The interval in whole days that brings retrievability down to the desired retention, clamped to [1, maximum].</summary>
    public static int Interval(double stability, FsrsParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var modifier = Round8((Math.Pow(parameters.DesiredRetention, 1.0 / Decay) - 1.0) / Factor);
        var days = Math.Round(stability * modifier, MidpointRounding.AwayFromZero);
        return (int)Math.Min(Math.Max(1.0, days), parameters.MaximumIntervalDays);
    }

    private static ReviewCard Finish(ReviewCard card, double stability, double difficulty, int intervalDays, StoredTimestamp now, bool lapse)
    {
        return card with
        {
            Stability = stability,
            Difficulty = difficulty,
            LastReview = now,
            Due = now.AddSeconds(checked(intervalDays * SecondsPerDay)),
            Reps = card.Reps + 1,
            Lapses = card.Lapses + (lapse ? 1 : 0),
        };
    }

    private static long ElapsedDays(StoredTimestamp lastReview, StoredTimestamp now)
    {
        var seconds = lastReview.SecondsUntil(now);
        return seconds <= 0 ? 0 : seconds / SecondsPerDay;
    }

    private static double InitialStability(IReadOnlyList<double> w, Rating rating)
    {
        return Math.Max(w[(int)rating - 1], MinStability);
    }

    private static double InitialDifficulty(IReadOnlyList<double> w, Rating rating)
    {
        return Round8(Math.Min(Math.Max(w[4] - (((int)rating - 3) * w[5]), MinDifficulty), MaxDifficulty));
    }

    private static double NextDifficulty(IReadOnlyList<double> w, double difficulty, Rating rating)
    {
        var shifted = difficulty - (w[6] * ((int)rating - 3));
        var reverted = Round8((w[7] * w[4]) + ((1.0 - w[7]) * shifted));
        return Math.Min(Math.Max(reverted, MinDifficulty), MaxDifficulty);
    }

    private static double NextRecallStability(IReadOnlyList<double> w, double difficulty, double stability, double retrievability, Rating rating)
    {
        var hardPenalty = rating == Rating.Hard ? w[15] : 1.0;
        var easyBonus = rating == Rating.Easy ? w[16] : 1.0;
        return Round8(stability * (1.0
            + (Math.Exp(w[8])
                * (11.0 - difficulty)
                * Math.Pow(stability, -w[9])
                * (Math.Exp((1.0 - retrievability) * w[10]) - 1.0)
                * hardPenalty
                * easyBonus)));
    }

    private static double NextForgetStability(IReadOnlyList<double> w, double difficulty, double stability, double retrievability)
    {
        return Round8(
            w[11]
            * Math.Pow(difficulty, -w[12])
            * (Math.Pow(stability + 1.0, w[13]) - 1.0)
            * Math.Exp((1.0 - retrievability) * w[14]));
    }

    private static double Round8(double value)
    {
        return Math.Round(value, 8, MidpointRounding.AwayFromZero);
    }
}
