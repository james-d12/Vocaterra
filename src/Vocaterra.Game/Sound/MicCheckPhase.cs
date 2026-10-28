namespace Vocaterra.Game.Sound;

/// <summary>Where the mic check is.</summary>
public enum MicCheckPhase
{
    /// <summary>Waiting for the player to hold the talk key and say "hola".</summary>
    Ready,

    /// <summary>The key is held and the microphone records: the level meter moves.</summary>
    Listening,

    /// <summary>The clip is with the recognizer.</summary>
    Thinking,

    /// <summary>The recognizer heard "hola".</summary>
    Passed,

    /// <summary>The recognizer did not hear "hola": try again or skip.</summary>
    Failed,

    /// <summary>The screen is gone (continued after a pass, or skipped).</summary>
    Done,
}
