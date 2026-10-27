using System.Text.RegularExpressions;

namespace Vocaterra.Nlp.Tests.Architecture;

/// <summary>
/// The grep guard of M33, extended in M34 (ADR 0011, point 8): the Spanish static entry points (text side and, since M34, analysis side) are called only from the Spanish pack, from their own files
/// and from the documented compatibility entry points. Neutral code reaches the Spanish behaviour through <c>bundle.Language</c>. The scan reads the
/// source of <c>src/Vocaterra.Nlp</c> and <c>tools/</c> (not the tests), ignores comment lines, and fails on any hit that the allowlist does not cover.
/// </summary>
public class SpanishCallSiteTests
{
    /// <summary>A Spanish static's call pattern, by name: a hit is one line that matches it.</summary>
    internal static readonly IReadOnlyDictionary<string, Regex> Patterns = new Dictionary<string, Regex>(StringComparer.Ordinal)
    {
        ["SpanishNumbers"] = new(@"\bSpanishNumbers\.", RegexOptions.CultureInvariant),
        ["SpanishPhoneticKey"] = new(@"\bSpanishPhoneticKey\.", RegexOptions.CultureInvariant),
        ["SpanishG2p"] = new(@"\bSpanishG2p\.", RegexOptions.CultureInvariant),
        ["TextNormalizer"] = new(@"\bTextNormalizer\.", RegexOptions.CultureInvariant),
        ["PunctuationFixer"] = new(@"\bPunctuationFixer\.", RegexOptions.CultureInvariant),
        ["NumberAgreement"] = new(@"\bNumberAgreement\.", RegexOptions.CultureInvariant),
        ["Accents"] = new(@"\bAccents\.", RegexOptions.CultureInvariant),
        ["AccentRestoration"] = new(@"\bAccentRestoration\.", RegexOptions.CultureInvariant),
        ["TextFolding"] = new(@"\bTextFolding\.", RegexOptions.CultureInvariant),
        ["WordClasses"] = new(@"\bWordClasses\.", RegexOptions.CultureInvariant),
        ["SpanishOrthography"] = new(@"\bSpanishOrthography\.", RegexOptions.CultureInvariant),
        ["SpanishLanguagePack"] = new(@"\bSpanishLanguagePack\.", RegexOptions.CultureInvariant),
        ["MorphologyGenerator"] = new(@"\bnew MorphologyGenerator\(", RegexOptions.CultureInvariant),
        ["SpanishAnalysisTables"] = new(@"\bSpanishAnalysisTables\.", RegexOptions.CultureInvariant),
    };

    /// <summary>
    /// One allowed use. <see cref="Path"/> is a file (exact match) or a folder ending in <c>/</c>; a null <see cref="Pattern"/> allows every pattern.
    /// <see cref="Reason"/> says why the use is allowed.
    /// </summary>
    internal sealed record Allow(string Path, string? Pattern, string Reason);

    /// <summary>The allowlist. Anything else that matches a pattern fails the test.</summary>
    internal static readonly IReadOnlyList<Allow> Allowlist =
    [
        new("src/Vocaterra.Nlp/Language/Packs/", null, "The Spanish pack and the Spanish implementations it owns (SpanishLanguagePack, SpanishRecastPolicy, PunctuationFixer, NumberAgreement)."),
        new("src/Vocaterra.Nlp/Language/Normalization/TextNormalizer.cs", null, "The Spanish static normalizer: its own file."),
        new("src/Vocaterra.Nlp/Language/Normalization/SpanishNumbers.cs", null, "The Spanish static number words: its own file (it uses Accents.Strip)."),
        new("src/Vocaterra.Nlp/Language/Normalization/SpanishPhoneticKey.cs", null, "The Spanish static phonetic key: its own file (it uses Accents.Strip)."),
        new("src/Vocaterra.Nlp/Response/Templates/RecastBuilder.cs", "SpanishLanguagePack", "Documented Spanish compatibility entry points RecastBuilder.Build(text, analysis[, lexicon]); the pack overload is the engine's."),
        new("src/Vocaterra.Nlp/Response/Speech/SpeechRequests.cs", "SpanishLanguagePack", "Documented Spanish compatibility entry point SpeechRequests.Create(text, voice[, dialect])."),
        new("src/Vocaterra.Nlp/Response/Templates/TemplateEngine.cs", "SpanishLanguagePack", "TemplateEngine.CreateDefault, the Spanish seed engine (a bundle's engine is bundle.TemplateEngine)."),
        // M34 routed the analysis rules, the slot grammar, the intent catalog and the lexicon's fold through the pack (IAnalysisRules, ILexiconFolding, LexiconProfile). What is left:
        new("src/Vocaterra.Nlp/Language/Morphology/MorphologyGenerator.cs", "TextFolding", "The Spanish IMorphology implementation: its own file, a Spanish static class (it moves into the Spanish pack project in M53)."),
        new("src/Vocaterra.Nlp/Language/Morphology/MorphologyGenerator.cs", "WordClasses", "The Spanish IMorphology implementation (el before a stressed feminine noun); moves with the pack in M53."),
        new("src/Vocaterra.Nlp/Language/Morphology/MorphologyGenerator.cs", "SpanishOrthography", "The Spanish IMorphology implementation (Spanish written accents and plurals); moves with the pack in M53."),
        new("src/Vocaterra.Nlp/Language/Lexicon/Lexicon.cs", "SpanishLanguagePack", "The seed lexicon is Spanish (an embedded resource), so Lexicon.Seed, Lexicon.Empty and the overloads without a folding default to the Spanish folding. A bundle's lexicon is built with its pack's folding (FromEntries(entries, folding, profile))."),
    ];

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

    /// <summary>The engine's and the tools' source files, read from the repository root.</summary>
    internal static IReadOnlyList<SourceFile> ReadSources(string root)
    {
        var files = new List<SourceFile>();
        var folders = new List<string> { Path.Combine(root, "src", "Vocaterra.Nlp") };
        folders.AddRange(Directory.EnumerateDirectories(Path.Combine(root, "tools"), "Vocaterra.Nlp.*"));
        foreach (var full in folders)
        {
            foreach (var file in Directory.EnumerateFiles(full, "*.cs", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                if (relative.Split('/').Any(static part => part is "bin" or "obj"))
                {
                    continue;
                }

                files.Add(new SourceFile(relative, File.ReadAllLines(file)));
            }
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
    public void NoSpanishStaticIsCalledOutsideTheAllowlist()
    {
        var sources = ReadSources(RepositoryRoot());
        Assert.NotEmpty(sources); // a vacuous scan would pass for nothing

        var violations = Violations(sources, Allowlist);

        Assert.True(
            violations.Count == 0,
            "Spanish statics are called outside the pack and the allowlist (route them through bundle.Language):\n"
            + string.Join("\n", violations.Select(static hit => $"  {hit.Path}:{hit.Line} [{hit.Pattern}] {hit.Text}")));
    }

    [Fact]
    public void TheGuardBitesOnACallOutsideTheAllowlist()
    {
        var neutral = new SourceFile("src/Vocaterra.Nlp/Response/Templates/Example.cs", ["var text = SpanishNumbers.ToWords(2);"]);

        var violations = Violations([neutral], Allowlist);

        Assert.Equal(["SpanishNumbers"], violations.Select(static hit => hit.Pattern));
    }

    [Fact]
    public void AComment_AndTheCompatibilityEntryPoints_AreNotViolations()
    {
        var files = new[]
        {
            new SourceFile("src/Vocaterra.Nlp/Response/Templates/Example.cs", ["// SpanishNumbers.ToWords(2) in a comment", "    /// TextNormalizer.Normalize(x)"]),
            new SourceFile("src/Vocaterra.Nlp/Response/Speech/SpeechRequests.cs", ["return Create(text, voiceId, SpanishLanguagePack.Instance, dialect);"]),
            new SourceFile("src/Vocaterra.Nlp/Language/Packs/SpanishLanguagePack.cs", ["public string Fix(string text) => PunctuationFixer.Fix(text);"]),
        };

        Assert.Empty(Violations(files, Allowlist));
    }

    [Fact]
    public void AnAllowlistEntryCoversOnlyItsOwnPattern()
    {
        var morphology = new SourceFile("src/Vocaterra.Nlp/Language/Morphology/MorphologyGenerator.cs", ["var w = SpanishOrthography.IsVowel(c);"]);
        var numbers = new SourceFile("src/Vocaterra.Nlp/Language/Morphology/MorphologyGenerator.cs", ["var n = SpanishNumbers.ToWords(2);"]);

        Assert.Empty(Violations([morphology], Allowlist));
        Assert.Equal(["SpanishNumbers"], Violations([numbers], Allowlist).Select(static hit => hit.Pattern));
    }
}
