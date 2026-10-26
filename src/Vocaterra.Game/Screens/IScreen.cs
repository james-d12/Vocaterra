using Vocaterra.Game.Input;

namespace Vocaterra.Game.Screens;

/// <summary>A screen over the world that takes the keyboard while it is open: a check, a panel, the book, the help.</summary>
public interface IScreen
{
    /// <summary>What the screen is called in a report's table of the game's state (S21).</summary>
    string Name { get; }

    /// <summary>Whether the screen is on screen and takes the keyboard.</summary>
    bool IsOpen { get; }

    /// <summary>Whether, while it is open, the mouse is free and the world takes no walking, looking, gathering or hotbar input.</summary>
    bool FreesMouse { get; }

    /// <summary>The frame's keys (and, for some, the wheel) while the screen is the top one.</summary>
    void HandleInput(InputSnapshot input);
}
