using Vocaterra.Nlp.Content;

namespace Vocaterra.Host.Tests;

/// <summary>The game's content bundle in this repository, loaded once for the whole test run.</summary>
internal static class GameContent
{
    private static readonly Lazy<ContentBundle> LoadedBundle = new(() => ContentLoader.Load(BundleDirectory()));

    /// <summary>The bundle, loaded and validated by the core's loader.</summary>
    public static ContentBundle Bundle => LoadedBundle.Value;

    /// <summary>The folder <c>content/game-v1</c> of the game (<c>game/</c> in the repository), found by walking up from the test assembly.</summary>
    public static string BundleDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "content", "game-v1");
            if (File.Exists(Path.Combine(candidate, "manifest.json")))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("content/game-v1 was not found above the test assembly.");
    }
}
