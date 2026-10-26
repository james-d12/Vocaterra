namespace Vocaterra.Game.Utilities;

/// <summary>Finds the scene layout file of a 3D run: the one named with <c>--scene</c>, or the bundle's own (<c>game/scene.json</c>, S3).</summary>
public static class SceneLocator
{
    /// <summary>The scene file's place inside a bundle folder (the prototype route, hand-authored).</summary>
    public const string BundleRelativePath = "game/scene.json";

    /// <summary>The path of the layout file to load.</summary>
    /// <param name="explicitFile">The file named with <c>--scene</c>, or <see langword="null"/>.</param>
    /// <param name="bundleDirectory">The content bundle folder (<c>content/game-v1</c>).</param>
    /// <exception cref="FileNotFoundException">The named file, or the bundle's scene, does not exist.</exception>
    public static string Resolve(string? explicitFile, string bundleDirectory)
    {
        ArgumentNullException.ThrowIfNull(bundleDirectory);
        if (explicitFile is not null)
        {
            return File.Exists(explicitFile) ? explicitFile : throw new FileNotFoundException($"The scene file '{explicitFile}' does not exist.");
        }

        var inBundle = Path.Combine(bundleDirectory, BundleRelativePath);
        return File.Exists(inBundle) ? inBundle : throw new FileNotFoundException($"No scene layout found: looked for {inBundle}. Name one with --scene.");
    }
}
