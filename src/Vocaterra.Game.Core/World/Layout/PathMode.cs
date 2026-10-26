namespace Vocaterra.Game.Core.World.Layout;

/// <summary>How a path takes the ground.</summary>
public enum PathMode
{
    /// <summary>The path's height changes evenly from the height of its first site to the height of its last (a switchback trail down a mountainside).</summary>
    Grade,

    /// <summary>The path follows the terrain's own height, smoothed so that it never steps more than one block (a road over the valley floor).</summary>
    Follow,
}

