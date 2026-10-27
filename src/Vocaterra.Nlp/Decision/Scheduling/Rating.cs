namespace Vocaterra.Nlp.Decision.Scheduling;

/// <summary>How well a review went; the numeric values are the FSRS grades 1 to 4.</summary>
public enum Rating
{
    /// <summary>The learner failed to recall the item.</summary>
    Again = 1,

    /// <summary>Recalled with serious difficulty.</summary>
    Hard = 2,

    /// <summary>Recalled correctly.</summary>
    Good = 3,

    /// <summary>Recalled effortlessly.</summary>
    Easy = 4,
}
