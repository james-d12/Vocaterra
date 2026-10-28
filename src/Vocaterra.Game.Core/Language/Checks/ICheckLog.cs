namespace Vocaterra.Game.Core.Language.Checks;

/// <summary>Where a check battery's start, answers and end are written (the game's session log, S11), without the log's file or clock.</summary>
public interface ICheckLog
{
    /// <summary>The start of a check battery: its kind, its session and its number of items.</summary>
    /// <param name="check">The check kind (<c>pre</c>...).</param>
    /// <param name="session">The play session number.</param>
    /// <param name="items">The number of items in the battery.</param>
    void CheckStart(string check, int session, int items);

    /// <summary>The end of a check battery: how many items were answered, and how many were skipped (Esc).</summary>
    /// <param name="check">The check kind.</param>
    /// <param name="answered">The items answered.</param>
    /// <param name="skipped">The items left unanswered.</param>
    void CheckEnd(string check, int answered, int skipped);

    /// <summary>One answer to a check item (measurement only: no progress, no learner model, no world).</summary>
    /// <param name="result">The answer.</param>
    /// <param name="check">The check kind.</param>
    /// <param name="session">The play session number.</param>
    /// <param name="tick">The simulation tick (the world is still at the start for a pre-check).</param>
    void CheckResult(CheckResult result, string check, int session, long tick);
}
