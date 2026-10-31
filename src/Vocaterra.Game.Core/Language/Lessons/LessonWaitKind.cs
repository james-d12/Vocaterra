namespace Vocaterra.Game.Core.Language.Lessons;

/// <summary>Why a lesson that is not begun waits (V12c): the book's Lessons page says it.</summary>
public enum LessonWaitKind
{
    /// <summary>It is the lesson the objective sends the learner to (the new session's, or the one to go on with): it starts when they talk to its NPC.</summary>
    Next,

    /// <summary>It is unlocked too, but the objective names another: it starts when the learner talks to its NPC.</summary>
    Open,

    /// <summary>Its play session has not come yet (<c>session</c> in <c>game/lessons.json</c>).</summary>
    Session,

    /// <summary>The lesson it comes after is not done yet (<c>after</c> in <c>game/lessons.json</c>).</summary>
    After,
}
