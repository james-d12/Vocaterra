namespace Vocaterra.Game.Core.Menu;

/// <summary>The buttons of the menu's main page, in order.</summary>
public enum MenuItem
{
    /// <summary>Back to the game.</summary>
    Resume,

    /// <summary>The settings page.</summary>
    Settings,

    /// <summary>The controls page.</summary>
    Controls,

    /// <summary>Save the world, close it and return to the title screen (Q01).</summary>
    MainMenu,

    /// <summary>Save, then close the game.</summary>
    Exit,
}
