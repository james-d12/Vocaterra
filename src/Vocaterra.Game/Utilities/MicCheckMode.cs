namespace Vocaterra.Game.Utilities;

/// <summary>Whether the mic check is shown at the start.</summary>
public enum MicCheckMode
{
    /// <summary>The default: shown at the start of an interactive run that loads speech, never in a scripted run.</summary>
    Auto,

    /// <summary><c>--mic-check</c>: shown whatever the run.</summary>
    Force,

    /// <summary><c>--no-mic-check</c>: never shown.</summary>
    Skip,
}
