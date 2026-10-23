namespace Vocaterra.Nlp.Testing;

/// <summary>
/// Finds the repository's one content bundle, <c>content/game-v1</c>, by walking up from the test assembly's folder (as the game's
/// <c>GameBundle.Locate</c> does). The bundle is 38 MB, mostly <c>speech/*.wav</c>, so no test project copies it to its output folder.
/// One source file, linked into every core test project that reads the bundle (docs/nlp/adr/0009-core-and-game-boundary.md, R02).
/// </summary>
internal static class GameBundleLocator
{
    /// <summary>The bundle folder of the repository the tests run from.</summary>
    public static string Directory { get; } = Find();

    private static string Find()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "content", "game-v1");
            if (File.Exists(Path.Combine(candidate, "manifest.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("content/game-v1/manifest.json was not found above " + AppContext.BaseDirectory + ".");
    }
}
