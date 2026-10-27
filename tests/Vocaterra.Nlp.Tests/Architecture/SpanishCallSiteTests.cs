using System.Text.RegularExpressions;

namespace Vocaterra.Nlp.Tests.Architecture;

/// <summary>
/// The grep guard of M33, extended in M34 (ADR 0011, point 8) and shrunk in M53 (ADR 0012): the engine source (<c>src/Vocaterra.Nlp</c>) names no Spanish
/// type. Until M53 the Spanish statics sat in the engine and the allowlist listed the documented compatibility entry points; since M53 they live in
/// <c>src/Vocaterra.Nlp.Es</c>, the engine cannot reference that project, and <see cref="Allowlist"/> is empty. A new entry needs a reason a reviewer accepts.
/// The scan ignores comment lines (including XML documentation) and fails on any hit that the allowlist does not cover. Neutral code reaches a language
/// through <c>bundle.Language</c>.
/// </summary>
public class SpanishCallSiteTests
{
    /// <summary>A Spanish name's pattern: a hit is one line of code that matches it.</summary>
    internal static readonly IReadOnlyDictionary<string, Regex> Patterns = new Dictionary<string, Regex>(StringComparer.Ordinal)
    {
        ["SpanishNumbers"] = new(@"\bSpanishNumbers\b", RegexOptions.CultureInvariant),
        ["SpanishPhoneticKey"] = new(@"\bSpanishPhoneticKey\b", RegexOptions.CultureInvariant),
        ["SpanishG2p"] = new(@"\bSpanishG2p\b", RegexOptions.CultureInvariant),
        ["SpanishTextNormalizer"] = new(@"\bSpanishTextNormalizer\b", RegexOptions.CultureInvariant),
        ["PunctuationFixer"] = new(@"\bPunctuationFixer\b", RegexOptions.CultureInvariant),
        ["NumberAgreement"] = new(@"\bNumberAgreement\b", RegexOptions.CultureInvariant),
        ["WordClasses"] = new(@"\bWordClasses\b", RegexOptions.CultureInvariant),
        ["SpanishOrthography"] = new(@"\bSpanishOrthography\b", RegexOptions.CultureInvariant),
        ["SpanishLanguagePack"] = new(@"\bSpanishLanguagePack\b", RegexOptions.CultureInvariant),
        ["SpanishLexicon"] = new(@"\bSpanishLexicon\b", RegexOptions.CultureInvariant),
        ["SpanishContent"] = new(@"\bSpanishContent\b", RegexOptions.CultureInvariant),
        ["SpanishRepairLines"] = new(@"\bSpanishRepairLines\b", RegexOptions.CultureInvariant),
        ["MorphologyGenerator"] = new(@"\bMorphologyGenerator\b", RegexOptions.CultureInvariant),
        ["SpanishAnalysisRules"] = new(@"\bSpanishAnalysisRules\b", RegexOptions.CultureInvariant),
        ["SpanishAnalysisTables"] = new(@"\bSpanishAnalysisTables\b", RegexOptions.CultureInvariant),
        ["SpanishAgreementChecker"] = new(@"\bSpanishAgreementChecker\b", RegexOptions.CultureInvariant),
        ["SpanishTemplateFunctions"] = new(@"\bSpanishTemplateFunctions\b", RegexOptions.CultureInvariant),
        ["SpanishRecastPolicy"] = new(@"\bSpanishRecastPolicy\b", RegexOptions.CultureInvariant),
        ["EsNamespace"] = new(@"\bVocaterra\.Nlp\.Es\b", RegexOptions.CultureInvariant),
    };

    /// <summary>
    /// One allowed use. <see cref="Path"/> is a file (exact match) or a folder ending in <c>/</c>; a null <see cref="Pattern"/> allows every pattern.
    /// <see cref="Reason"/> says why the use is allowed.
    /// </summary>
    internal sealed record Allow(string Path, string? Pattern, string Reason);

    /// <summary>The allowlist. Empty since M53: the engine has no Spanish type to name. Anything that matches a pattern fails the test.</summary>
    internal static readonly IReadOnlyList<Allow> Allowlist = [];

    /// <summary>One source file: its path relative to the repository root, with forward slashes, and its lines.</summary>
    internal sealed record SourceFile(string Path, IReadOnlyList<string> Lines);

    /// <summary>One line that matches a pattern.</summary>
    internal sealed record Hit(string Path, int Line, string Pattern, string Text);

    /// <summary>The hits of the files that no allowlist entry covers.</summary>
    internal static IReadOnlyList<Hit> Violations(IEnumerable<SourceFile> files, IEnumerable<Allow> allow)
    {
        var allowed = allow.ToArray();
        var result = new List<Hit>();
        foreach (var file in files)
        {
            for (var i = 0; i < file.Lines.Count; i++)
            {
                var line = file.Lines[i];
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var (name, regex) in Patterns)
                {
                    if (regex.IsMatch(line) && !allowed.Any(entry => Covers(entry, file.Path, name)))
                    {
                        result.Add(new Hit(file.Path, i + 1, name, line.Trim()));
                    }
                }
            }
        }

        return result;
    }

    private static bool Covers(Allow entry, string path, string pattern)
    {
        var inPath = entry.Path.EndsWith('/') ? path.StartsWith(entry.Path, StringComparison.Ordinal) : string.Equals(path, entry.Path, StringComparison.Ordinal);
        return inPath && (entry.Pattern is null || string.Equals(entry.Pattern, pattern, StringComparison.Ordinal));
    }

    /// <summary>The engine's source files, read from the repository root.</summary>
    internal static IReadOnlyList<SourceFile> ReadSources(string root)
    {
        var files = new List<SourceFile>();
        var folder = Path.Combine(root, "src", "Vocaterra.Nlp");
        foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.Split('/').Any(static part => part is "bin" or "obj"))
            {
                continue;
            }

            files.Add(new SourceFile(relative, File.ReadAllLines(file)));
        }

        return files;
    }

    /// <summary>The repository root: the nearest folder above the test binaries that holds <c>Vocaterra.sln</c>.</summary>
    internal static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Vocaterra.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("The repository root (Vocaterra.sln) was not found above the test binaries.");
    }

    [Fact]
    public void TheEngineSourceNamesNoSpanishType()
    {
        var sources = ReadSources(RepositoryRoot());
        Assert.NotEmpty(sources); // a vacuous scan would pass for nothing

        var violations = Violations(sources, Allowlist);

        Assert.True(
            violations.Count == 0,
            "The engine names a Spanish type (route it through bundle.Language, or move it into src/Vocaterra.Nlp.Es):\n"
            + string.Join("\n", violations.Select(static hit => $"  {hit.Path}:{hit.Line} [{hit.Pattern}] {hit.Text}")));
    }

    [Fact]
    public void TheAllowlistIsEmpty()
    {
        Assert.Empty(Allowlist);
    }

    [Fact]
    public void TheGuardBitesOnACallOutsideTheAllowlist()
    {
        var neutral = new SourceFile("src/Vocaterra.Nlp/Response/Templates/Example.cs", ["var text = SpanishNumbers.ToWords(2);"]);

        var violations = Violations([neutral], Allowlist);

        Assert.Equal(["SpanishNumbers"], violations.Select(static hit => hit.Pattern));
    }

    [Fact]
    public void TheGuardBitesOnEveryNameOfThePackAndNotOnACommentOrALongerName()
    {
        var files = new[]
        {
            new SourceFile("src/Vocaterra.Nlp/Response/Example.cs", ["var pack = SpanishLanguagePack.Instance;", "var m = new MorphologyGenerator(lexicon);", "using Vocaterra.Nlp.Es.Text;"]),
            new SourceFile("src/Vocaterra.Nlp/Response/Comments.cs", ["// SpanishNumbers.ToWords(2) in a comment", "    /// <see cref=\"SpanishTextNormalizer\"/>", "var x = ISpanishNumbersLike.Value;"]),
        };

        var hits = Violations(files, Allowlist);

        Assert.Equal(["SpanishLanguagePack", "MorphologyGenerator", "EsNamespace"], hits.Select(static hit => hit.Pattern));
        Assert.All(hits, static hit => Assert.Equal("src/Vocaterra.Nlp/Response/Example.cs", hit.Path));
    }

    [Fact]
    public void AnAllowlistEntryCoversOnlyItsOwnPatternAndPath()
    {
        Allow[] allow = [new("src/Vocaterra.Nlp/Example/", "SpanishOrthography", "a test entry")];
        var covered = new SourceFile("src/Vocaterra.Nlp/Example/A.cs", ["var w = SpanishOrthography.IsVowel(c);"]);
        var otherPattern = new SourceFile("src/Vocaterra.Nlp/Example/A.cs", ["var n = SpanishNumbers.ToWords(2);"]);
        var otherPath = new SourceFile("src/Vocaterra.Nlp/Other/A.cs", ["var w = SpanishOrthography.IsVowel(c);"]);

        Assert.Empty(Violations([covered], allow));
        Assert.Equal(["SpanishNumbers"], Violations([otherPattern], allow).Select(static hit => hit.Pattern));
        Assert.Equal(["SpanishOrthography"], Violations([otherPath], allow).Select(static hit => hit.Pattern));
    }
}
