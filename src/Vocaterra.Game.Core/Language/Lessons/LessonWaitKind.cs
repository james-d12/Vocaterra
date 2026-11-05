namespace Vocaterra.Game.Core.Language.Lessons;

/// <summary>Why a lesson that is not begun waits (V12c, L01): the book's Lessons page says it.</summary>
public enum LessonWaitKind
{
    /// <summary>It is the introduction, the one lesson that starts by itself: it starts when the learner first talks to its NPC.</summary>
    Next,

    /// <summary>The class is on offer (L01): its teacher is met and the lesson it comes after is done; it starts when the learner asks, or says Sí to the teacher's offer.</summary>
    Open,

    /// <summary>Its teacher is not met yet: the player has not talked to them.</summary>
    Meet,

    /// <summary>The lesson it comes after is not done yet (<c>after</c> in <c>game/lessons.json</c>).</summary>
    After,
}
