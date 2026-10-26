using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Session;

/// <summary>Finds and loads the game's content bundle, <c>content/game-v1</c>.</summary>
public static class GameBundle
{
    /// <summary>The bundle folder relative to the repository root.</summary>
    public const string RelativeDirectory = "content/game-v1";

    /// <summary>The environment variable that names a bundle folder and overrides the search.</summary>
    public const string DirectoryVariable = "VOCATERRA_CONTENT_DIR";

    /// <summary>
    /// The bundle folder: the one in <see cref="DirectoryVariable"/> if it is set, otherwise the first <c>content/game-v1</c> with a manifest
    /// found by walking up from the executable's folder and then from the current directory. Returns <see langword="null"/> when there is none.
    /// </summary>
    public static string? Locate()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(DirectoryVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return Directory.Exists(fromEnvironment) ? Path.GetFullPath(fromEnvironment) : null;
        }

        return Search(AppContext.BaseDirectory) ?? Search(Directory.GetCurrentDirectory());
    }

    /// <summary>The bundle folder that <paramref name="directory"/> names, or the one <see cref="Locate"/> finds when it is <see langword="null"/>.</summary>
    /// <exception cref="DirectoryNotFoundException">No bundle folder was found.</exception>
    public static string ResolveDirectory(string? directory = null) => directory ?? Locate()
        ?? throw new DirectoryNotFoundException(
            $"The content bundle {RelativeDirectory} was not found. Run from inside the repository or set {DirectoryVariable}.");

    /// <summary>Loads the bundle from <paramref name="directory"/>, or from <see cref="Locate"/> when it is <see langword="null"/>.</summary>
    /// <exception cref="DirectoryNotFoundException">No bundle folder was found.</exception>
    /// <exception cref="ContentValidationException">The bundle fails the core's validation.</exception>
    public static ContentBundle Load(string? directory = null) => ContentLoader.Load(ResolveDirectory(directory));

    private static string? Search(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "content", "game-v1");
            if (File.Exists(Path.Combine(candidate, "manifest.json")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
