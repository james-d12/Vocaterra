using Vocaterra.Game.Core.Menu;

namespace Vocaterra.Game.Graphics;

/// <summary>
/// What a quality level sets. Only the chunks within <see cref="ViewDistance"/> of the eye are meshed and drawn, the fog closes in at the same
/// distance, and the camera's far plane stops just past it; <see cref="MeshBudgetMilliseconds"/> is how long one frame may spend building chunk
/// meshes (the rest wait for the next frame, nearest first).
/// </summary>
/// <param name="Level">The level.</param>
/// <param name="ViewDistance">How far the world is drawn, in blocks from the eye.</param>
/// <param name="MeshBudgetMilliseconds">The most time one frame spends meshing chunks (an interactive run; a scripted run meshes everything it needs before a screenshot).</param>
/// <param name="PlantDistance">S14: how far from the eye the undergrowth (tall grass, ferns, reeds, flowers) is drawn, in blocks.</param>
/// <param name="PlantKeepPercent">S14: the percent of the undergrowth that is built at all (a stable pick by position), 100 for every plant.</param>
/// <param name="AmbientOcclusion">S14: whether the cube corners are darkened by the blocks round them.</param>
/// <param name="Clouds">S14: whether the flat clouds are drawn.</param>
/// <param name="RainDrops">E06: the most drops the rain draws at full intensity (a style takes its share of it, <c>PrecipitationStyle.CountShare</c>); the cost is four vertices a drop.</param>
public sealed record QualityPreset(
    QualityLevel Level,
    int ViewDistance,
    double MeshBudgetMilliseconds,
    int PlantDistance = 64,
    int PlantKeepPercent = 60,
    bool AmbientOcclusion = true,
    bool Clouds = true,
    int RainDrops = 1200)
{
    /// <summary>The lowest view distance a run may be given: three chunks.</summary>
    public const int MinimumViewDistance = 48;

    /// <summary>The preset of a level.</summary>
    public static QualityPreset For(QualityLevel level) => level switch
    {
        QualityLevel.Low => new QualityPreset(QualityLevel.Low, 224, 6, 64, 80, true, true, 2200),
        QualityLevel.Medium => new QualityPreset(QualityLevel.Medium, 304, 5, 112, 100, true, true, 3400),
        _ => new QualityPreset(QualityLevel.High, 400, 4, 192, 100, true, true, 5400),
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
