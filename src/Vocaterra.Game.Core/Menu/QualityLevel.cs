namespace Vocaterra.Game.Core.Menu;

/// <summary>The graphics quality of a run (<c>--quality</c>, S13 and S14, and the menu's setting): how far the world is drawn, how much undergrowth is drawn, whether the corners are shaded and whether clouds are drawn. The front-end maps a level to its preset.</summary>
public enum QualityLevel
{
    /// <summary>The preset for a machine with software rendering or an old graphics card: the shortest view.</summary>
    Low,

    /// <summary>A middle preset.</summary>
    Medium,

    /// <summary>The preset for a real graphics card; the default.</summary>
    High,
}
