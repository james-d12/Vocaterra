namespace Vocaterra.Game.Core.Activities;

/// <summary>
/// The sound of the station's instruction rows (W07, design 10.8: each row plays through <see cref="Sound.NpcVoice"/>, the slow file for <c>Más despacio</c>). The panel asks
/// whether a row can be heard before it decides to show the Spanish card in its place (decision 19: with no audio device, with <c>--mute</c> or with no recording the card
/// shows at the audio-only rung and the act is reading evidence).
/// </summary>
public interface IInstructionAudio
{
    /// <summary>Whether the row can be heard right now: a sound device, not muted, and a recording of the row.</summary>
    /// <param name="rowId">The does row's phrase id.</param>
    bool CanPlay(string rowId);

    /// <summary>Plays the row, replacing what plays; returns whether a clip was started.</summary>
    /// <param name="rowId">The does row's phrase id.</param>
    /// <param name="slow">Whether to play the slow recording.</param>
    bool Play(string rowId, bool slow);
}

/// <summary>An <see cref="IInstructionAudio"/> that never plays (a run with no voice, and the tests that do not care).</summary>
public sealed class SilentInstructionAudio : IInstructionAudio
{
    /// <summary>The one instance.</summary>
    public static SilentInstructionAudio Instance { get; } = new();

    /// <inheritdoc />
    public bool CanPlay(string rowId) => false;

    /// <inheritdoc />
    public bool Play(string rowId, bool slow) => false;
}
