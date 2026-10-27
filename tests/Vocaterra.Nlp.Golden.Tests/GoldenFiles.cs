namespace Vocaterra.Nlp.Golden.Tests;

/// <summary>
/// Where a golden file lives, by language (M35, research 4.8). The Spanish goldens (the bundle's language is <c>es</c>, or absent, which is Spanish) keep
/// their paths, <c>Data/&lt;name&gt;</c>. A golden of another language lives in <c>Data/&lt;lang&gt;/&lt;name&gt;</c>, for example <c>Data/de/</c>, and is recorded
/// with the same <c>VOCATERRA_RECORD_GOLDEN=1</c> variable. The layout is in code only: no file under a <c>Data</c> folder is added by this class, and a
/// language's folder is created when its first golden is recorded.
/// </summary>
internal static class GoldenFiles
{
    /// <summary>The language whose goldens keep the old paths (<c>Data/&lt;name&gt;</c>).</summary>
    public const string SpanishLanguage = "es";

    /// <summary>The golden's path relative to a <c>Data</c> folder: <c>&lt;name&gt;</c> for Spanish, <c>&lt;lang&gt;/&lt;name&gt;</c> for another language.</summary>
    /// <param name="languageId">The primary language subtag of the bundle (<c>es</c>, <c>de</c>); a null or empty id is Spanish.</param>
    /// <param name="fileName">The golden's file name.</param>
    public static string RelativePath(string? languageId, string fileName)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        var language = string.IsNullOrEmpty(languageId) ? SpanishLanguage : languageId;
        if (language.Contains('/', StringComparison.Ordinal) || language.Contains('\\', StringComparison.Ordinal) || language.Contains("..", StringComparison.Ordinal))
        {
            throw new ArgumentException($"'{languageId}' is not a language id.", nameof(languageId));
        }

        return language == SpanishLanguage ? fileName : language + "/" + fileName;
    }

    /// <summary>The golden as it is read: under the test output's <c>Data</c> folder.</summary>
    public static string RunPath(string? languageId, string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Data", RelativePath(languageId, fileName).Replace('/', Path.DirectorySeparatorChar));
    }

    /// <summary>The golden as it is recorded: in the source tree, under <c>tests/Vocaterra.Nlp.Golden.Tests/Data</c>, with its language folder created.</summary>
    public static string SourcePath(string? languageId, string fileName)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var data = Path.Combine(directory.FullName, "tests", "Vocaterra.Nlp.Golden.Tests", "Data");
            if (Directory.Exists(data))
            {
                var path = Path.Combine(data, RelativePath(languageId, fileName).Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                return path;
            }
        }

        throw new DirectoryNotFoundException("Could not find tests/Vocaterra.Nlp.Golden.Tests/Data above the test binaries.");
    }
}
