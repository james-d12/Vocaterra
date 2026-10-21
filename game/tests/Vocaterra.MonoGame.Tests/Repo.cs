namespace Vocaterra.MonoGame.Tests;

/// <summary>Finds files in the checkout from the test's output folder (the tests read the committed font and the project's sources).</summary>
internal static class Repo
{
    public static string Root { get; } = FindRoot();

    public static string Path(params string[] parts) => System.IO.Path.Combine([Root, .. parts]);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "Vocaterra.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Vocaterra.sln was not found above the test output folder.");
    }
}
