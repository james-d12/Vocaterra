namespace Vocaterra.Game.Core.Menu;

/// <summary>
/// The testing section's controls, each over something the front-end already has (a flag of the command line or a function key): the debug overlay (F3, <c>--dev</c>), the needs running
/// (<c>--needs on|off</c>), a refill of the needs, every sound but the voices off (<c>--no-sounds</c>), the voices off (<c>--mute</c>), the camera over the shoulder (F5, <c>--third-person</c>)
/// and a save now (F6). None of them is kept in a save: a test aid must not outlive the test.
/// </summary>
/// <param name="DebugOverlay">Whether the debug overlay and the dialogue's developer note show; <see langword="null"/> where the run has none (a playtest).</param>
/// <param name="SetDebugOverlay">Shows or hides them.</param>
/// <param name="NeedsRunning">Whether the needs run in active play.</param>
/// <param name="SetNeedsRunning">Runs or freezes the needs.</param>
/// <param name="RefillNeeds">Sets every need to full.</param>
/// <param name="SoundsOff">Whether every sound but the NPCs' voices is off.</param>
/// <param name="SetSoundsOff">Turns them off or on.</param>
/// <param name="VoicesMuted">Whether the NPCs' voices are off.</param>
/// <param name="SetVoicesMuted">Turns them off or on.</param>
/// <param name="ShoulderView">Whether the camera is over the shoulder.</param>
/// <param name="SetShoulderView">Puts the camera over the shoulder or back in first person.</param>
/// <param name="SaveNow">Saves the game as F6 does.</param>
public sealed record TestingHooks(
    Func<bool>? DebugOverlay,
    Action<bool>? SetDebugOverlay,
    Func<bool> NeedsRunning,
    Action<bool> SetNeedsRunning,
    Action RefillNeeds,
    Func<bool> SoundsOff,
    Action<bool> SetSoundsOff,
    Func<bool> VoicesMuted,
    Action<bool> SetVoicesMuted,
    Func<bool> ShoulderView,
    Action<bool> SetShoulderView,
    Action SaveNow);
