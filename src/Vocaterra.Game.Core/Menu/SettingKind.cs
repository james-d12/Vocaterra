namespace Vocaterra.Game.Core.Menu;

/// <summary>How the menu changes a setting.</summary>
public enum SettingKind
{
    /// <summary>A number between two ends, moved in steps (Left and Right, or the mouse on the track).</summary>
    Slider,

    /// <summary>One of a few named values, cycled.</summary>
    Choice,

    /// <summary>On or off.</summary>
    Toggle,
}
