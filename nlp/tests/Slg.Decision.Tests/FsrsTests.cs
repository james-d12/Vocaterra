using Slg.Core.Determinism;
using Slg.Decision.Scheduling;

namespace Slg.Decision.Tests;

/// <summary>Tests for the FSRS-4.5 scheduler and the verdict-to-rating mapping.</summary>
public class FsrsTests
{
    private const long Day = 86_400;
    private const double Tolerance = 1e-8;
    private static readonly StoredTimestamp T0 = new(1_700_000_000);

    private static StoredTimestamp Days(long days)
    {
        return T0.AddSeconds(days * Day);
    }

    [Fact]
    public void DefaultParametersAreTheFsrs45Weights()
    {
        var p = FsrsParameters.Default;

        Assert.Equal(17, p.Weights.Count);
        Assert.Equal(0.5701, p.Weights[0]);
        Assert.Equal(4.1386, p.Weights[2]);
        Assert.Equal(2.8237, p.Weights[16]);
        Assert.Equal(0.9, p.DesiredRetention);
        Assert.Equal(36500, p.MaximumIntervalDays);
    }

    [Fact]
    public void ParametersAreValidated()
    {
        var weights = FsrsParameters.Default.Weights;

        Assert.Throws<ArgumentException>(() => new FsrsParameters(weights.Take(16).ToArray(), 0.9, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FsrsParameters(weights, 1.0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FsrsParameters(weights, 0.0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FsrsParameters(weights, 0.9, 0));
        Assert.Throws<ArgumentException>(() => new FsrsParameters([.. weights.Take(16), double.NaN], 0.9, 100));
        Assert.Equal(FsrsParameters.Default, new FsrsParameters(weights, 0.9, 36500));
    }

    [Fact]
    public void GoodOnNewCardGivesInitialStabilityAndRoundedIntervalDue()
    {
        var card = Fsrs.Review(ReviewCard.New("hola", T0), Rating.Good, T0);

        // Hand check: stability = w[2] = 4.1386; difficulty = w[4] = 5.1443 (D0(3)); interval = round(4.1386 * 1.0) = 4 days.
        Assert.Equal("hola", card.ItemId);
        Assert.Equal(4.1386, card.Stability);
        Assert.Equal(5.1443, card.Difficulty);
        Assert.Equal(T0, card.LastReview);
        Assert.Equal(new StoredTimestamp(T0.UnixSeconds + (4 * Day)), card.Due);
        Assert.Equal(1, card.Reps);
        Assert.Equal(0, card.Lapses);
    }

    [Theory]
    [InlineData(Rating.Again, 0.5701, 7.5455, 1)]
    [InlineData(Rating.Hard, 1.4436, 6.3449, 1)]
    [InlineData(Rating.Good, 4.1386, 5.1443, 4)]
    [InlineData(Rating.Easy, 10.9355, 3.9437, 11)]
    public void FirstReviewUsesInitialStabilityAndDifficultyPerRating(Rating rating, double stability, double difficulty, int days)
    {
        var card = Fsrs.Review(ReviewCard.New("x", T0), rating, T0);

        Assert.Equal(stability, card.Stability, Tolerance);
        Assert.Equal(difficulty, card.Difficulty, Tolerance);
        Assert.Equal(T0.UnixSeconds + (days * Day), card.Due.UnixSeconds);
        Assert.Equal(0, card.Lapses);
    }

    [Fact]
    public void RetrievabilityIsNinetyPercentAfterOneStability()
    {
        Assert.Equal(0.9, Fsrs.Retrievability(4.1386, 4.1386), Tolerance);
        Assert.Equal(1.0, Fsrs.Retrievability(0.0, 4.1386), Tolerance);
    }

    [Fact]
    public void RetrievabilityOfCardUsesWholeElapsedDays()
    {
        var card = Fsrs.Review(ReviewCard.New("x", T0), Rating.Good, T0);

        Assert.Equal(0.0, Fsrs.Retrievability(ReviewCard.New("x", T0), Days(3)));
        Assert.Equal(Fsrs.Retrievability(2.0, 4.1386), Fsrs.Retrievability(card, T0.AddSeconds((2 * Day) + 86_399)));
        Assert.Equal(1.0, Fsrs.Retrievability(card, T0.AddSeconds(-500)), Tolerance);
    }

    [Fact]
    public void IntervalEqualsRoundedStabilityAtNinetyPercentRetention()
    {
        Assert.Equal(1, Fsrs.Interval(0.3, FsrsParameters.Default));
        Assert.Equal(5, Fsrs.Interval(4.5, FsrsParameters.Default));
        Assert.Equal(4, Fsrs.Interval(4.4999, FsrsParameters.Default));
        Assert.Equal(36500, Fsrs.Interval(1e9, FsrsParameters.Default));
    }

    [Fact]
    public void LowerDesiredRetentionStretchesTheInterval()
    {
        var relaxed = new FsrsParameters(FsrsParameters.Default.Weights, 0.8, 36500);

        // (0.8^-2 - 1) / (19/81) = 2.39802632 days per day of stability.
        Assert.Equal(24, Fsrs.Interval(10.0, relaxed));
    }

    [Fact]
    public void MaximumIntervalClampsTheSchedule()
    {
        var capped = new FsrsParameters(FsrsParameters.Default.Weights, 0.9, 7);

        var card = Fsrs.Review(ReviewCard.New("x", T0), Rating.Easy, T0, capped);

        Assert.Equal(T0.UnixSeconds + (7 * Day), card.Due.UnixSeconds);
    }

    [Fact]
    public void FixedHistoryMatchesReferenceImplementation()
    {
        // Constants produced by an independent line-by-line port of the ts-fsrs 3.5.7 algorithm (FSRS-4.5).
        var card = ReviewCard.New("x", T0);
        var steps = new (long Day, Rating Rating, double Stability, double Difficulty, long IntervalDays, int Reps, int Lapses)[]
        {
            (0, Rating.Good, 4.1386, 5.1443, 4, 1, 0),
            (4, Rating.Good, 14.73612508, 5.1443, 15, 2, 0),
            (13, Rating.Hard, 19.3053531, 5.97577026, 19, 3, 0),
            (33, Rating.Again, 3.44293363, 7.60861156, 3, 4, 1),
            (34, Rating.Good, 5.13269345, 7.51940348, 5, 5, 1),
            (37, Rating.Easy, 18.61778373, 6.60195447, 19, 6, 1),
        };

        foreach (var step in steps)
        {
            var now = Days(step.Day);
            card = Fsrs.Review(card, step.Rating, now);

            Assert.Equal(step.Stability, card.Stability, Tolerance);
            Assert.Equal(step.Difficulty, card.Difficulty, Tolerance);
            Assert.Equal(now, card.LastReview);
            Assert.Equal(now.UnixSeconds + (step.IntervalDays * Day), card.Due.UnixSeconds);
            Assert.Equal(step.Reps, card.Reps);
            Assert.Equal(step.Lapses, card.Lapses);
        }
    }

    [Fact]
    public void ReviewOnTheSameDayKeepsStabilityOnSuccess()
    {
        var first = Fsrs.Review(ReviewCard.New("x", T0), Rating.Good, T0);

        var again = Fsrs.Review(first, Rating.Good, T0.AddSeconds(3600));

        // Retrievability is 1 so the recall term vanishes; the earlier interval is kept at least one day ahead.
        Assert.Equal(first.Stability, again.Stability, Tolerance);
        Assert.True(again.Due > T0.AddSeconds(3600));
    }

    [Fact]
    public void ForgettingLowersStabilityAndCountsALapse()
    {
        var first = Fsrs.Review(ReviewCard.New("x", T0), Rating.Easy, T0);

        var lapsed = Fsrs.Review(first, Rating.Again, Days(11));

        Assert.True(lapsed.Stability < first.Stability);
        Assert.True(lapsed.Difficulty > first.Difficulty);
        Assert.Equal(1, lapsed.Lapses);
        Assert.Equal(2, lapsed.Reps);
        Assert.True(lapsed.Due >= Days(12));
    }

    [Fact]
    public void HardGoodEasyIntervalsAreStrictlyOrdered()
    {
        var learned = Fsrs.Review(ReviewCard.New("x", T0), Rating.Good, T0);
        var now = Days(4);

        var hard = Fsrs.Review(learned, Rating.Hard, now);
        var good = Fsrs.Review(learned, Rating.Good, now);
        var easy = Fsrs.Review(learned, Rating.Easy, now);

        Assert.True(hard.Due < good.Due);
        Assert.True(good.Due < easy.Due);
        Assert.True(hard.Stability < good.Stability);
        Assert.True(good.Stability < easy.Stability);
    }

    [Fact]
    public void DifficultyStaysWithinOneToTen()
    {
        var card = Fsrs.Review(ReviewCard.New("x", T0), Rating.Again, T0);
        for (var i = 1; i <= 60; i++)
        {
            card = Fsrs.Review(card, Rating.Again, Days(i));
            Assert.InRange(card.Difficulty, 1.0, 10.0);
        }

        for (var i = 61; i <= 160; i++)
        {
            card = Fsrs.Review(card, Rating.Easy, Days(i));
            Assert.InRange(card.Difficulty, 1.0, 10.0);
        }
    }

    [Fact]
    public void ReviewIsDeterministicAndDoesNotMutateTheInputCard()
    {
        var learned = Fsrs.Review(ReviewCard.New("x", T0), Rating.Good, T0);
        var snapshot = CanonicalJson.Serialize(learned);

        var a = Fsrs.Review(learned, Rating.Hard, Days(6));
        var b = Fsrs.Review(learned, Rating.Hard, Days(6));

        Assert.Equal(a, b);
        Assert.Equal(snapshot, CanonicalJson.Serialize(learned));
        Assert.Equal(a, CanonicalJson.Deserialize<ReviewCard>(CanonicalJson.Serialize(a)));
    }

    [Fact]
    public void InvalidRatingIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Fsrs.Review(ReviewCard.New("x", T0), (Rating)0, T0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Fsrs.Review(ReviewCard.New("x", T0), (Rating)5, T0));
    }

    [Fact]
    public void NewCardIsDueImmediately()
    {
        var card = ReviewCard.New("x", T0);

        Assert.True(card.IsNew);
        Assert.Equal(T0, card.Due);
        Assert.False(Fsrs.Review(card, Rating.Hard, T0).IsNew);
    }
}

/// <summary>Tests for the verdict-to-rating mapping.</summary>
public class RatingMapperTests
{
    [Fact]
    public void NotCommunicatedIsAgain()
    {
        Assert.Equal(Rating.Again, RatingMapper.FromVerdict(TestVerdicts.Make("x", false, 1.0)));
    }

    [Theory]
    [InlineData(0.0, Rating.Hard)]
    [InlineData(0.3, Rating.Hard)]
    [InlineData(0.47, Rating.Hard)]
    [InlineData(0.5, Rating.Hard)]
    [InlineData(0.51, Rating.Hard)]
    [InlineData(0.53, Rating.Good)]
    [InlineData(0.7, Rating.Good)]
    [InlineData(0.87, Rating.Good)]
    [InlineData(0.9, Rating.Good)]
    [InlineData(0.91, Rating.Good)]
    [InlineData(0.93, Rating.Easy)]
    [InlineData(1.0, Rating.Easy)]
    public void CorrectnessMapsThroughMarginsAndUncertainRoundsDown(double correctness, Rating expected)
    {
        Assert.Equal(expected, RatingMapper.FromVerdict(TestVerdicts.Make("x", true, correctness)));
    }

    [Fact]
    public void EpsilonComesFromTheThresholds()
    {
        var tight = new RatingThresholds(0.5, 0.9, 0.0);

        // With no band a score of exactly 0.9 is Easy and 0.5 is Good, as the plain comparison says.
        Assert.Equal(Rating.Easy, RatingMapper.FromVerdict(TestVerdicts.Make("x", true, 0.9), tight));
        Assert.Equal(Rating.Good, RatingMapper.FromVerdict(TestVerdicts.Make("x", true, 0.5), tight));
        Assert.Equal(Rating.Hard, RatingMapper.FromVerdict(TestVerdicts.Make("x", true, 0.4999), tight));
        Assert.Equal(Rating.Good, RatingMapper.FromVerdict(TestVerdicts.Make("x", true, 0.8999), tight));
    }

    [Fact]
    public void InvalidEpsilonIsRejected()
    {
        var bad = new RatingThresholds(0.5, 0.9, -0.1);

        Assert.Throws<ArgumentOutOfRangeException>(() => RatingMapper.FromVerdict(TestVerdicts.Make("x", true, 0.7), bad));
    }
}
