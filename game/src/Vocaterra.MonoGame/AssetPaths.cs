namespace Vocaterra.MonoGame;

/// <summary>Where the run-time assets are: next to the executable, copied from the repository's <c>assets/</c> folder by the project file.</summary>
public static class AssetPaths
{
    /// <summary>The font file relative to the executable (see <c>assets/ATTRIBUTION.md</c> for its licence).</summary>
    public const string FontRelative = "assets/fonts/PixelOperator.ttf";

    /// <summary>The full path of the font file.</summary>
    public static string Font => Path.Combine(AppContext.BaseDirectory, FontRelative);
}
