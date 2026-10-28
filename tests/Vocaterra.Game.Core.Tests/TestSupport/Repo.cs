namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>Finds files in the checkout from the test's output folder (the tests read the committed font and the project's sources).</summary>
internal static class Repo
{
    public static string Root { get; } = FindRoot();

    public static string Path(params string[] parts) => System.IO.Path.Combine([Root, .. parts]);

    private static string FindRoot()
    {
        // The repository root: the first ancestor of the test output that holds content/game-v1.
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, "content", "game-v1", "manifest.json")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("content/game-v1 was not found above the test output folder.");
    }
}
