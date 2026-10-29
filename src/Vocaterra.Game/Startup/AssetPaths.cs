namespace Vocaterra.Game.Startup;

/// <summary>Where the run-time assets are: next to the executable, copied from the repository's <c>assets/</c> folder by the project file.</summary>
public static class AssetPaths
{
    /// <summary>The font file relative to the executable (see <c>assets/ATTRIBUTION.md</c> for its licence).</summary>
    public const string FontRelative = "assets/fonts/PixelOperator.ttf";

    /// <summary>The full path of the font file.</summary>
    public static string Font => Path.Combine(AppContext.BaseDirectory, FontRelative);

    /// <summary>The folder of the sound files relative to the executable (see <c>assets/audio/SOURCES.md</c> for each file's source and licence, S24).</summary>
    public const string AudioRelative = "assets/audio";

    /// <summary>The full path of the sound files' folder.</summary>
    public static string Audio => Path.Combine(AppContext.BaseDirectory, AudioRelative);
}
