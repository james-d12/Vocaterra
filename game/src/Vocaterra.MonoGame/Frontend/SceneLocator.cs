namespace Vocaterra.MonoGame.Frontend;

/// <summary>Finds the scene layout file of a 3D run: the one named with <c>--scene</c>, the bundle's own, or the draft route beside the bundle.</summary>
public static class SceneLocator
{
    /// <summary>The scene file's place inside a bundle folder (S3 moves the route there).</summary>
    public const string BundleRelativePath = "game/scene.json";

    /// <summary>The draft route's place relative to the folder that holds the bundle (<c>content/scenes/route-draft.json</c>).</summary>
    public const string DraftRelativePath = "scenes/route-draft.json";

    /// <summary>The path of the layout file to load.</summary>
    /// <param name="explicitFile">The file named with <c>--scene</c>, or <see langword="null"/>.</param>
    /// <param name="bundleDirectory">The content bundle folder (<c>content/game-v1</c>).</param>
    /// <exception cref="FileNotFoundException">No layout file exists at any of the places.</exception>
    public static string Resolve(string? explicitFile, string bundleDirectory)
    {
        ArgumentNullException.ThrowIfNull(bundleDirectory);
        if (explicitFile is not null)
        {
            return File.Exists(explicitFile) ? explicitFile : throw new FileNotFoundException($"The scene file '{explicitFile}' does not exist.");
        }

        var inBundle = Path.Combine(bundleDirectory, BundleRelativePath);
        if (File.Exists(inBundle))
        {
            return inBundle;
        }

        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(bundleDirectory)));
        var draft = parent is null ? null : Path.Combine(parent, DraftRelativePath);
        return draft is not null && File.Exists(draft)
            ? draft
            : throw new FileNotFoundException($"No scene layout found: looked for {inBundle} and {draft ?? DraftRelativePath}. Name one with --scene.");
    }
}
