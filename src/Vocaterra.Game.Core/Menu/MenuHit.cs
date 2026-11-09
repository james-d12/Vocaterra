namespace Vocaterra.Game.Core.Menu;

/// <summary>What is under the pointer on the menu's page: a button of the main page, a row of the settings, or the Back button.</summary>
/// <param name="Index">The button (a <see cref="MenuItem"/>) on the main page, or the row's index in the settings list; -1 for the Back button.</param>
/// <param name="OnTrack">Whether the pointer is on a slider row's track, so a click sets the value there.</param>
public readonly record struct MenuHit(int Index, bool OnTrack = false)
{
    /// <summary>The Back button.</summary>
    public static MenuHit Back { get; } = new(-1);

    /// <summary>Whether this is the Back button.</summary>
    public bool IsBack => Index < 0;
}
