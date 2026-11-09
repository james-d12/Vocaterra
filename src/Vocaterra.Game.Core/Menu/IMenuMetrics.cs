namespace Vocaterra.Game.Core.Menu;

/// <summary>What the menu needs to know of how it is drawn: what is under the pointer and how much of a long page fits. The front-end's layout answers it for the canvas as it is now.</summary>
public interface IMenuMetrics
{
    /// <summary>How many rows of the settings page are shown at once.</summary>
    int SettingsCapacity { get; }

    /// <summary>How far the controls page can scroll, in lines (0 when it all fits).</summary>
    int ControlsMaxScroll { get; }

    /// <summary>What is under a point of the canvas on this page, or <see langword="null"/>.</summary>
    /// <param name="page">The page shown.</param>
    /// <param name="settingsScroll">The first settings row shown.</param>
    /// <param name="x">The canvas x.</param>
    /// <param name="y">The canvas y.</param>
    MenuHit? HitAt(MenuPage page, int settingsScroll, int x, int y);

    /// <summary>How far along a settings row's track an x of the canvas is, 0 to 1 (clamped).</summary>
    /// <param name="settingsScroll">The first settings row shown.</param>
    /// <param name="row">The settings row.</param>
    /// <param name="x">The canvas x.</param>
    double TrackFraction(int settingsScroll, int row, int x);
}
