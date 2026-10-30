using Vocaterra.Nlp.Content;

namespace Vocaterra.Nlp.Testing;

/// <summary>
/// Finds the repository's one content bundle, <c>content/game-v1</c>, by walking up from the test assembly's folder (as the game's
/// <c>GameBundle.Locate</c> does), and loads it once per test process. The bundle is 38 MB, mostly <c>speech/*.wav</c>, so no test project
/// copies it to its output folder, and a load reads, hashes and parses all of it. One source file, linked into every core test project that
/// reads the bundle (docs/nlp/adr/0009-core-and-game-boundary.md, R02).
/// </summary>
internal static class GameBundleLocator
{
    /// <summary>The bundle folder of the repository the tests run from.</summary>
    public static string Directory { get; } = Find();

    // Declared after Directory, so that the nullable analysis of the initializers sees it set.
    private static readonly Lazy<ContentBundle> Loaded = new(() => SpanishContent.Load(Directory));

    /// <summary>
    /// The bundle, loaded on first use with every hash verified (the loader's default) and the Spanish pack registered, and shared by every test of the
    /// process. A test that needs other load options, or a damaged copy, loads its own (<c>TempBundle</c>).
    /// </summary>
    public static ContentBundle Bundle => Loaded.Value;

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
