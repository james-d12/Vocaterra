using Vocaterra.Game.Core.Needs;

namespace Vocaterra.Game.Core.Menu;

/// <summary>
/// The testing section's controls, each over something the front-end already has (a flag of the command line or a function key): the debug overlay (F3, <c>--dev</c>), the needs
/// running (<c>--needs on|off</c>) and each need's value (<c>--set-need</c>), a refill of the needs, every sound but the voices off (<c>--no-sounds</c>), the voices off (<c>--mute</c>),
/// the camera over the shoulder (F5, <c>--third-person</c>), dusk lighting (<c>--dusk</c>), the greyscale view (<c>--greyscale</c>), every noun counted as known in the view
/// (<c>--assume-known</c>, but only the view), the first-time tips (<c>--tips</c>), the beacon (<c>--beacon</c>), a teleport to a named point (<c>--teleport</c>), the typed-answer credit
/// (<c>--typed-credit</c>), the autosave (<c>--no-autosave</c>) and a save now (F6). None of them is kept in a save: a test aid must not outlive the test. A row appears when its hooks
/// are given, so a front-end (or a test) offers only what it has.
/// </summary>
public sealed record TestingHooks
{
    /// <summary>Whether the debug overlay and the dialogue's developer note show; <see langword="null"/> where the run has none (a playtest).</summary>
    public Func<bool>? DebugOverlay { get; init; }

    /// <summary>Shows or hides them.</summary>
    public Action<bool>? SetDebugOverlay { get; init; }

    /// <summary>Whether the needs run in active play.</summary>
    public required Func<bool> NeedsRunning { get; init; }

    /// <summary>Runs or freezes the needs.</summary>
    public required Action<bool> SetNeedsRunning { get; init; }

    /// <summary>A need's value now, 0 to 100.</summary>
    public Func<NeedMeter, int>? NeedValue { get; init; }

    /// <summary>Sets a need (stamina stops at its maximum).</summary>
    public Action<NeedMeter, int>? SetNeed { get; init; }

    /// <summary>Sets every need to full.</summary>
    public required Action RefillNeeds { get; init; }

    /// <summary>Whether every sound but the NPCs' voices is off.</summary>
    public required Func<bool> SoundsOff { get; init; }

    /// <summary>Turns them off or on.</summary>
    public required Action<bool> SetSoundsOff { get; init; }

    /// <summary>Whether the NPCs' voices are off.</summary>
    public required Func<bool> VoicesMuted { get; init; }

    /// <summary>Turns them off or on.</summary>
    public required Action<bool> SetVoicesMuted { get; init; }

    /// <summary>Whether the camera is over the shoulder.</summary>
    public required Func<bool> ShoulderView { get; init; }

    /// <summary>Puts the camera over the shoulder or back in first person.</summary>
    public required Action<bool> SetShoulderView { get; init; }

    /// <summary>Whether the lighting is the dusk of <c>--dusk</c>.</summary>
    public Func<bool>? Dusk { get; init; }

    /// <summary>Switches dusk lighting (the world is meshed again, the sky and the ambient beds follow).</summary>
    public Action<bool>? SetDusk { get; init; }

    /// <summary>Whether the playtest A/B view is on: blocks whose noun is not known yet are grey.</summary>
    public Func<bool>? Greyscale { get; init; }

    /// <summary>Switches the greyscale view.</summary>
    public Action<bool>? SetGreyscale { get; init; }

    /// <summary>Whether every noun counts as known in the view (nothing is grey). The learner's progress is not touched.</summary>
    public Func<bool>? AssumeAllKnown { get; init; }

    /// <summary>Switches it.</summary>
    public Action<bool>? SetAssumeAllKnown { get; init; }

    /// <summary>Whether the first-time tips are drawn.</summary>
    public Func<bool>? TipsShown { get; init; }

    /// <summary>Shows or hides them.</summary>
    public Action<bool>? SetTipsShown { get; init; }

    /// <summary>The scene's named points (the ones <c>--beacon</c> and the teleport name), sorted; empty or <see langword="null"/> for none.</summary>
    public IReadOnlyList<string>? Places { get; init; }

    /// <summary>The name of the point the beacon stands on, or <see langword="null"/> when there is no beacon (or it stands on something that is not a named point).</summary>
    public Func<string?>? BeaconPlace { get; init; }

    /// <summary>Stands the beacon on a named point, or takes it down for <see langword="null"/>.</summary>
    public Action<string?>? SetBeacon { get; init; }

    /// <summary>Puts the player on the ground of a named point.</summary>
    public Action<string>? TeleportTo { get; init; }

    /// <summary>Whether a correct typed answer earns full credit (<c>--typed-credit full</c>) rather than half.</summary>
    public Func<bool>? FullTypedCredit { get; init; }

    /// <summary>Sets the typed-answer credit.</summary>
    public Action<bool>? SetFullTypedCredit { get; init; }

    /// <summary>Whether the autosave writes.</summary>
    public Func<bool>? AutosaveOn { get; init; }

    /// <summary>Switches the autosave.</summary>
    public Action<bool>? SetAutosaveOn { get; init; }

    /// <summary>Saves the game as F6 does.</summary>
    public required Action SaveNow { get; init; }
}
