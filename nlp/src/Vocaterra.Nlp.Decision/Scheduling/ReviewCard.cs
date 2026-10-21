using System.Text.Json.Serialization;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Decision.Scheduling;

/// <summary>The FSRS memory state of one content item; a card with zero reps has not been reviewed yet.</summary>
public sealed record ReviewCard(
    string ItemId,
    double Stability,
    double Difficulty,
    StoredTimestamp LastReview,
    StoredTimestamp Due,
    int Reps,
    int Lapses)
{
    /// <summary>True until the first review.</summary>
    [JsonIgnore]
    public bool IsNew => Reps <= 0 || Stability <= 0.0;

    /// <summary>Creates a never-reviewed card that is due immediately.</summary>
    public static ReviewCard New(string itemId, StoredTimestamp now)
    {
        ArgumentNullException.ThrowIfNull(itemId);
        return new ReviewCard(itemId, 0.0, 0.0, now, now, 0, 0);
    }
}
