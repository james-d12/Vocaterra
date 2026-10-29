namespace Vocaterra.Game.Core.Language.Lessons;

/// <summary>What a button of the lesson strip does (S16). The strip's mouse hit-test, its keys and the code that carries them out all use these.</summary>
public enum LessonAction
{
    /// <summary><c>L</c>: starts the lesson on offer, or takes the paused one up again.</summary>
    Start,

    /// <summary><c>Enter</c>: ends the step that waits for it (explain, model, recap, a picked listen step).</summary>
    Continue,

    /// <summary><c>K</c>: skips the step.</summary>
    Skip,

    /// <summary><c>H</c>: the step's help path (the panel's Ayuda button does the same while a lesson is engaged).</summary>
    Help,

    /// <summary><c>1</c>: the first option of a listen step.</summary>
    Option1,

    /// <summary><c>2</c>: the second option of a listen step.</summary>
    Option2,

    /// <summary><c>3</c>: the third option of a listen step.</summary>
    Option3,

    /// <summary><c>4</c>: the fourth option of a listen step.</summary>
    Option4,

    /// <summary><c>M</c>: the next page of the step's English, when it is too long for the strip at this size (X08).</summary>
    More,
}
