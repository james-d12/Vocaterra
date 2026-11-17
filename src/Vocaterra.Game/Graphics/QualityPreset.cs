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
/// <param name="FarRange">B07: how far the far view's level-of-detail land is drawn in the streamed world, in blocks from the eye (D-game-34 item 21); 0 for none. The bounded valley has no far view.</param>
/// <param name="FarTreeLevels">B07c: how many of the far view's finest levels draw every tree as a box (the levels end at 256, 512, 1,024 and 2,048 blocks): High 4 (every tree to about 2 km), Medium 3 (1 km), Low 2 (512 blocks); beyond, a forest is a raised canopy layer.</param>
/// <param name="FarRealRadius">B07c: how far from the eye workers make the real chunks of the surface for the far view's store: High 1,000 blocks, Medium 760, Low 560 (2.5 voxel distances, where the far view's level 2 ends).</param>
/// <param name="MaxMeshedChunks">B07: the most chunk meshes a streamed world keeps (the nearest ones): a cap on the meshes and so on the memory of the card.</param>
/// <param name="MultiSamples">B22: the samples per pixel of the back buffer (multisample antialiasing, 0 for none): the stair-stepped silhouettes of blocks and the shimmer of far tiles. Chosen when the window opens (the menu's quality row takes effect for it at the next start; `--msaa` overrides).</param>
/// <param name="FarBandScale">B07d: what the far view's level bands are multiplied by (the Far view detail setting, 100% is 1): above 1 the finer levels last further out.</param>
/// <param name="FarTexturedLevels">B07d: how many of the far view's finest levels carry the block atlas's textures (the Far view textures setting, 2 usually).</param>
/// <param name="MaxResidentChunks">B07: the most chunks a streamed world keeps resident (the farthest unchanged ones are dropped past it).</param>
/// <param name="GrassDistance">B18: how far from the eye the decorative grass layer is drawn, in blocks; it thins out over the outer half of it.</param>
/// <param name="Shaders">B17b: whether the level turns the game's own GLSL shaders on (the settings page's Shaders row follows a change of level; every level has them on; the context may still refuse them).</param>
/// <param name="GrassCapPercent">B18: the share of the Grass slider's density this level may reach, in percent: the slider's 100 is this share of a tuft on every grass column of the lushest biome.</param>
public sealed record QualityPreset(
    QualityLevel Level,
    int ViewDistance,
    double MeshBudgetMilliseconds,
    int PlantDistance = 64,
    int PlantKeepPercent = 60,
    bool AmbientOcclusion = true,
    bool Clouds = true,
    int RainDrops = 1200,
    int FarRange = 0,
    int FarTreeLevels = 4,
    int FarRealRadius = 1000,
    int MaxMeshedChunks = 24_000,
    int MaxResidentChunks = 48_000,
    int GrassDistance = 96,
    int GrassCapPercent = 100,
    int MultiSamples = 4,
    double FarBandScale = 1.0,
    int FarTexturedLevels = 2,
    bool Shaders = true)
{
    /// <summary>The lowest view distance a run may be given: three chunks.</summary>
    public const int MinimumViewDistance = 48;

    /// <summary>The preset of a level.</summary>
    public static QualityPreset For(QualityLevel level) => level switch
    {
        QualityLevel.Low => new QualityPreset(QualityLevel.Low, 224, 6, 64, 80, true, true, 2200, FarRange: 4096, FarTreeLevels: 2, FarRealRadius: 560, MaxMeshedChunks: 8_000, MaxResidentChunks: 20_000, GrassDistance: 48, GrassCapPercent: 40),
        QualityLevel.Medium => new QualityPreset(QualityLevel.Medium, 304, 5, 112, 100, true, true, 3400, FarRange: 8192, FarTreeLevels: 3, FarRealRadius: 760, MaxMeshedChunks: 14_000, MaxResidentChunks: 32_000, GrassDistance: 72, GrassCapPercent: 70),
        _ => new QualityPreset(QualityLevel.High, 400, 4, 192, 100, true, true, 5400, FarRange: 16384, FarTreeLevels: 4, FarRealRadius: 1000, MaxMeshedChunks: 24_000, MaxResidentChunks: 48_000, GrassDistance: 96, GrassCapPercent: 100),
    };

    /// <summary>The samples per pixel the window opens with for a quality level (B22).</summary>
    public static int MultiSamplesOf(QualityLevel level) => For(level).MultiSamples;

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

    /// <summary>The same preset with another far range (<c>--far-view</c>); 0 turns the far view off.</summary>
    public QualityPreset WithFarRange(int blocks) => this with { FarRange = Math.Max(0, blocks) };

    /// <summary>The far view's shape for this preset: its range, with the level-of-detail layout every preset shares.</summary>
    public Far.FarViewSettings Far => new(FarRange, BandScale: FarBandScale, TreeLevels: FarTreeLevels, TexturedLevels: FarTexturedLevels, RealRadius: FarRealRadius);

    /// <summary>The name of the level in lower case, as <c>--quality</c> takes it.</summary>
    public string Name => Level.ToString().ToLowerInvariant();
}
