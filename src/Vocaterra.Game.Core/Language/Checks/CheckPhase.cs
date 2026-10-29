namespace Vocaterra.Game.Core.Language.Checks;

/// <summary>Where the check battery stands: an item is showing, a production answer is with the worker, or the battery is done.</summary>
public enum CheckPhase
{
    /// <summary>The current item is on the screen and waits for the learner.</summary>
    Showing,

    /// <summary>A production answer went to the worker; the core is grading it.</summary>
    Thinking,

    /// <summary>Every item is answered or skipped.</summary>
    Done,
}
