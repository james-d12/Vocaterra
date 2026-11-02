using System.Text.RegularExpressions;

namespace Vocaterra.Nlp.Documents.Tests;

/// <summary>
/// Every relative link of every Markdown document of the repository names a file or a folder that exists (it was <c>scripts/check-links.sh</c>, M76). A link is
/// relative when it has no URL scheme and is not a bare <c>#anchor</c>; its target is resolved from the document that holds it, without any <c>#fragment</c> or
/// <c>?query</c>. Links inside fenced code blocks and inline code spans are ignored, and nothing is fetched.
/// </summary>
public sealed partial class MarkdownLinkTests
{
    private static readonly string[] SkippedFolders = ["bin", "obj", "artifacts", ".git", "TestResults", "lexicon-sources"];

    [GeneratedRegex(@"!?\[[^\]]*\]\(([^)\s]+)(?:\s+""[^""]*"")?\)")]
    private static partial Regex Link();

    [GeneratedRegex("`[^`]*`")]
    private static partial Regex CodeSpan();

    [GeneratedRegex("^[a-zA-Z][a-zA-Z0-9+.-]*:")]
    private static partial Regex Scheme();

    [Fact]
    public void EveryRelativeLinkOfTheDocumentsNamesSomethingThatExists()
    {
        var root = RepositoryRoot();
        var broken = new List<string>();
        var checkedLinks = 0;
        foreach (var path in Documents(root, root))
        {
            var fence = false;
            var number = 0;
            foreach (var line in File.ReadLines(path))
            {
                number++;
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    fence = !fence;
                    continue;
                }

                if (fence)
                {
                    continue;
                }

                foreach (Match match in Link().Matches(CodeSpan().Replace(line, string.Empty)))
                {
                    var target = match.Groups[1].Value.Trim('<', '>');
                    if (target.StartsWith('#') || target.StartsWith("//", StringComparison.Ordinal) || Scheme().IsMatch(target))
                    {
                        continue;
                    }

                    target = target.Split('#')[0].Split('?')[0];
                    if (target.Length == 0)
                    {
                        continue;
                    }

                    checkedLinks++;
                    var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, target));
                    if (!File.Exists(resolved) && !Directory.Exists(resolved))
                    {
                        broken.Add($"{Path.GetRelativePath(root, path)}:{number}: {target}");
                    }
                }
            }
        }

        Assert.True(checkedLinks > 0, "no link was checked, so the walk found no documents");
        Assert.True(broken.Count == 0, "broken relative links:\n" + string.Join('\n', broken));
    }

    // The repository root is the folder of Vocaterra.sln above the test assembly.
    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vocaterra.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Vocaterra.sln was not found above " + AppContext.BaseDirectory);
    }

    // Build output, fetched models (third-party Markdown), agent worktrees and gate logs are not the repository's documents.
    private static IEnumerable<string> Documents(string root, string folder)
    {
        var relative = Path.GetRelativePath(root, folder).Replace('\\', '/');
        foreach (var file in Directory.EnumerateFiles(folder, "*.md").Order(StringComparer.Ordinal))
        {
            yield return file;
        }

        foreach (var child in Directory.EnumerateDirectories(folder).Order(StringComparer.Ordinal))
        {
            var name = Path.GetFileName(child);
            if (SkippedFolders.Contains(name, StringComparer.Ordinal)
                || (relative == ".claude" && name is "worktrees" or "gate-logs")
                || (relative == "." && name == "models"))
            {
                continue;
            }

            foreach (var file in Documents(root, child))
            {
                yield return file;
            }
        }
    }
}
