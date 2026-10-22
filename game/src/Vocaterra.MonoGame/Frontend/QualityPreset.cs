namespace Vocaterra.MonoGame.Frontend;

/// <summary>The graphics quality of a run (<c>--quality</c>, S13): how far the world is drawn. Later milestones add the plants, the lighting and the clouds to it.</summary>
public enum QualityLevel
{
    /// <summary>The preset for a machine with software rendering or an old graphics card: the shortest view.</summary>
    Low,

    /// <summary>A middle preset.</summary>
    Medium,

    /// <summary>The preset for a real graphics card; the default.</summary>
    High,
}

/// <summary>
/// What a quality level sets. Only the chunks within <see cref="ViewDistance"/> of the eye are meshed and drawn, the fog closes in at the same
/// distance, and the camera's far plane stops just past it; <see cref="MeshBudgetMilliseconds"/> is how long one frame may spend building chunk
/// meshes (the rest wait for the next frame, nearest first).
/// </summary>
/// <param name="Level">The level.</param>
/// <param name="ViewDistance">How far the world is drawn, in blocks from the eye.</param>
/// <param name="MeshBudgetMilliseconds">The most time one frame spends meshing chunks (an interactive run; a scripted run meshes everything it needs before a screenshot).</param>
public sealed record QualityPreset(QualityLevel Level, int ViewDistance, double MeshBudgetMilliseconds)
{
    /// <summary>The lowest view distance a run may be given: three chunks.</summary>
    public const int MinimumViewDistance = 48;

    /// <summary>The preset of a level.</summary>
    public static QualityPreset For(QualityLevel level) => level switch
    {
        QualityLevel.Low => new QualityPreset(QualityLevel.Low, 224, 6),
        QualityLevel.Medium => new QualityPreset(QualityLevel.Medium, 304, 5),
        _ => new QualityPreset(QualityLevel.High, 400, 4),
    };

    /// <summary>The level named by <c>--quality</c> (<c>low</c>, <c>medium</c> or <c>high</c>), or <see langword="null"/>.</summary>
    public static QualityLevel? Parse(string value) => value switch
    {
        "low" => QualityLevel.Low,
        "medium" => QualityLevel.Medium,
        "high" => QualityLevel.High,
        _ => null,
    };

    /// <summary>The same preset with another view distance (<c>--view-distance</c>).</summary>
    public QualityPreset WithViewDistance(int blocks) => this with { ViewDistance = Math.Max(MinimumViewDistance, blocks) };

    /// <summary>The name of the level in lower case, as <c>--quality</c> takes it.</summary>
    public string Name => Level.ToString().ToLowerInvariant();
}
