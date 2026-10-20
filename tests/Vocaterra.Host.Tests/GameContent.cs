using Vocaterra.Nlp.Content;

namespace Vocaterra.Host.Tests;

/// <summary>The game's content bundle in this repository, loaded once for the whole test run.</summary>
internal static class GameContent
{
    private static readonly Lazy<ContentBundle> LoadedBundle = new(() => ContentLoader.Load(BundleDirectory()));

    /// <summary>The bundle, loaded and validated by the core's loader.</summary>
    public static ContentBundle Bundle => LoadedBundle.Value;

    /// <summary>The folder <c>content/game-v1</c> of the repository, found by walking up from the test assembly to <c>Vocaterra.sln</c>.</summary>
    public static string BundleDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Vocaterra.sln")))
        {
            directory = directory.Parent;
        }

        return directory is null
            ? throw new InvalidOperationException("Vocaterra.sln was not found above the test assembly.")
            : Path.Combine(directory.FullName, "content", "game-v1");
    }
}
