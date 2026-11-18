namespace Vocaterra.Game.Core.World;

/// <summary>Which world <see cref="WorldFactory"/> makes.</summary>
public enum WorldMode
{
    /// <summary>The bounded valley the game plays in today: one dense box, built by the scene builder, with the world's place query and its template digest.</summary>
    Bounded,

    /// <summary>The streamed world: a sparse grid whose chunks come from the composite source (the valley's own inside it, the procedural land eased into its edge outside).</summary>
    Streamed,
}
