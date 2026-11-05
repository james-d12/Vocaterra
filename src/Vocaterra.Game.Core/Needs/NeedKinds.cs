namespace Vocaterra.Game.Core.Needs;

/// <summary>The three drives (N01, design 9.2): the meters that fall in active play and push health and stamina.</summary>
public enum Drive
{
    /// <summary>Hunger (<c>el hambre</c>), restored by eating.</summary>
    Hunger,

    /// <summary>Thirst (<c>la sed</c>), restored by drinking.</summary>
    Thirst,

    /// <summary>Sleep (<c>el sueño</c>), restored by sleeping.</summary>
    Sleep,
}

/// <summary>The five meters by name (<c>--set-need</c> names them).</summary>
public enum NeedMeter
{
    /// <summary>Hunger, 0 to 100.</summary>
    Hunger,

    /// <summary>Thirst, 0 to 100.</summary>
    Thirst,

    /// <summary>Sleep, 0 to 100.</summary>
    Sleep,

    /// <summary>Stamina, 0 to its current maximum.</summary>
    Stamina,

    /// <summary>Health, 0 to 100.</summary>
    Health,
}

/// <summary>What the player is doing during a span of active play, as far as the needs care.</summary>
public enum NeedActivity
{
    /// <summary>Standing still away from a lit fire.</summary>
    Idle,

    /// <summary>Walking.</summary>
    Walking,

    /// <summary>Sprinting (effort: stamina is spent, the drives fall faster).</summary>
    Sprinting,

    /// <summary>Gathering (effort: stamina is spent per gather tick, the drives fall faster).</summary>
    Gathering,

    /// <summary>Standing still within the rest radius of a lit fire: stamina refills and health recovers twice as fast.</summary>
    Resting,
}

/// <summary>
/// The reasons play is not active (design 9.2, "Time"): the needs do not move while any is set. The game sets them each frame; the session's world ticks go on
/// under most of them, which is why the session is told rather than left to guess.
/// </summary>
[Flags]
public enum PlayPause
{
    /// <summary>Play is active.</summary>
    None = 0,

    /// <summary>A conversation panel is open.</summary>
    Conversation = 1,

    /// <summary>A lesson panel is open.</summary>
    Lesson = 2,

    /// <summary>The book is open.</summary>
    Book = 4,

    /// <summary>The inventory is open.</summary>
    Inventory = 8,

    /// <summary>The crafting panel is open.</summary>
    Crafting = 16,

    /// <summary>A station's panel is open (the oven, later).</summary>
    Station = 32,

    /// <summary>The report panel (F8) is open or asked for.</summary>
    Report = 64,

    /// <summary>A menu or help screen is open (the controls, the mic check).</summary>
    Menu = 128,

    /// <summary>The check battery is on.</summary>
    CheckBattery = 256,

    /// <summary>Speech is being recorded.</summary>
    SpeechRecording = 512,

    /// <summary>Speech is being processed (a turn is on the worker).</summary>
    SpeechProcessing = 1024,
}
