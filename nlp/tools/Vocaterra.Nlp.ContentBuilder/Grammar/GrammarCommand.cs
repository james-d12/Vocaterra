using System.Text.Encodings.Web;
using System.Text.Json;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Nlp.ContentBuilder;

/// <summary>
/// The <c>grammar</c> command: runs the strings of a bundle (and optionally a host's JSONL strings) through a local LanguageTool server, or, with
/// <c>--offline</c>, replays the committed result cache. Findings use the lint's line format (<c>severity check path [ids] message</c>) and its
/// summary line. Learner examples that carry error tags are graded as an oracle: flagged-and-tagged, tagged-but-silent or untagged-but-flagged.
/// </summary>
internal static class GrammarCommand
{
    /// <summary>The address of a local LanguageTool server when <c>--server</c> is not given.</summary>
    public const string DefaultServer = "http://localhost:8081";

    /// <summary>Exit code of an online run with no server: the check was skipped, not passed.</summary>
    public const int ExitNoServer = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Runs the command. The handler, when given, replaces the network (tests use it); exit codes are the ones the usage text lists.</summary>
    public static int Run(ArgumentList arguments, TextWriter output, TextWriter error, HttpMessageHandler? handler)
    {
        var dir = arguments.Positional(0, "bundleDir", exactly: 1);
        var level = arguments.Option("--level") ?? "picky";
        if (level is not ("picky" or "default"))
        {
            throw new UsageException("grammar takes --level picky or --level default.");
        }

        var format = arguments.Option("--format") ?? "text";
        if (format is not ("text" or "json"))
        {
            throw new UsageException("grammar takes --format text or --format json.");
        }

        var stringsOnly = arguments.Flag("--strings-only");
        if (stringsOnly && arguments.Option("--strings") is null)
        {
            throw new UsageException("--strings-only needs --strings <jsonl>.");
        }

        if (!Directory.Exists(dir))
        {
            error.WriteLine($"Bundle directory not found: {dir}");
            return Program.ExitInvalid;
        }

        var fullDir = Path.GetFullPath(dir);
        var folder = Path.Combine(Path.GetDirectoryName(fullDir)!, "grammar");
        var name = Path.GetFileName(fullDir);
        var cachePath = arguments.Option("--cache") ?? Path.Combine(folder, name + "-cache.json");
        var waiversPath = arguments.Option("--waivers") ?? Path.Combine(folder, name + "-waivers.json");
        var server = arguments.Option("--server") ?? DefaultServer;
        var settings = LanguageToolClient.SettingsFor(level);

        ContentBundle bundle;
        try
        {
            // Hashes are not verified, so the check runs while content is being edited; run validate before shipping.
            bundle = ContentLoader.Load(dir, new ContentLoadOptions { VerifyHashes = false });
        }
        catch (ContentValidationException ex)
        {
            error.WriteLine(ex.Message);
            return Program.ExitInvalid;
        }

        try
        {
            var items = Collect(bundle, arguments, stringsOnly);
            var waivers = GrammarWaivers.Load(waiversPath, mustExist: arguments.Option("--waivers") is not null);
            var cache = GrammarCache.Load(cachePath);
            List<(GrammarItem Item, IReadOnlyList<LtMatch>? Matches)> results;
            if (arguments.Flag("--offline"))
            {
                results = ReplayCache(items, cache, settings);
            }
            else
            {
                results = CheckOnline(items, cache, settings, level, server, cachePath, arguments.Flag("--prune"), output, error, handler, out var skipped);
                if (skipped)
                {
                    return ExitNoServer;
                }
            }

            var findings = BuildFindings(results, waivers, Path.GetFileName(waiversPath));
            return Report(findings, format, arguments.Flag("--strict"), output);
        }
        catch (GrammarInputException ex)
        {
            error.WriteLine(ex.Message);
            return Program.ExitInvalid;
        }
    }

    /// <summary>The strings to check: the bundle's, then the JSONL's (its ids must not repeat a bundle id).</summary>
    private static List<GrammarItem> Collect(ContentBundle bundle, ArgumentList arguments, bool stringsOnly)
    {
        var items = stringsOnly ? new List<GrammarItem>() : GrammarStrings.FromBundle(bundle);
        if (arguments.Option("--strings") is not { } path)
        {
            return items;
        }

        if (!File.Exists(path))
        {
            throw new GrammarInputException($"strings file not found: {path}");
        }

        var bundleIds = new HashSet<string>(items.Select(static item => item.Id), StringComparer.Ordinal);
        foreach (var item in GrammarStrings.FromJsonl(path, bundle.TemplateEngine))
        {
            if (bundleIds.Contains(item.Id))
            {
                throw new GrammarInputException($"{Path.GetFileName(path)}: the id '{item.Id}' is also a string of the bundle.");
            }

            items.Add(item);
        }

        return items;
    }

    /// <summary>Checks every string against the server (cached results are reused), updates the cache and returns one result per string.</summary>
    private static List<(GrammarItem Item, IReadOnlyList<LtMatch>? Matches)> CheckOnline(
        List<GrammarItem> items,
        GrammarCache cache,
        string settings,
        string level,
        string server,
        string cachePath,
        bool prune,
        TextWriter output,
        TextWriter error,
        HttpMessageHandler? handler,
        out bool skipped)
    {
        skipped = false;
        using var client = new LanguageToolClient(server, handler);
        try
        {
            var probe = Check(client, "Hola.", level);
            CheckVersion(probe.Version);
        }
        catch (LanguageToolUnreachableException)
        {
            output.WriteLine($"grammar: skipped: no LanguageTool server at {server} (start one with scripts/languagetool.sh start)");
            skipped = true;
            return [];
        }

        var results = new List<(GrammarItem, IReadOnlyList<LtMatch>?)>(items.Count);
        foreach (var item in items)
        {
            var key = GrammarCache.KeyFor(LanguageToolClient.PinnedVersion, settings, item.CheckText);
            if (!cache.TryGet(key, out var entry))
            {
                var checkedText = Check(client, item.CheckText, level);
                entry = new GrammarCacheEntry(key, settings, item.CheckText, checkedText.Matches);
                cache.Add(entry);
            }

            results.Add((item, entry.Matches));
        }

        // Entries are kept unless --prune is given: a run over the bundle alone must not drop the entries of a --strings file checked earlier.
        if (prune)
        {
            cache.Prune();
        }

        var wrote = cache.Save(cachePath, LanguageToolClient.PinnedVersion);
        // The cache note goes to stderr so that --format json keeps stdout a single document.
        error.WriteLine($"cache: {cache.Count} entries, {cache.Added} new, {cache.Pruned} pruned; {(wrote ? "wrote" : "unchanged")} {cachePath}");
        return results;
    }

    /// <summary>Offline: each string's result comes from the cache; a missing entry stays null and becomes a finding.</summary>
    private static List<(GrammarItem Item, IReadOnlyList<LtMatch>? Matches)> ReplayCache(List<GrammarItem> items, GrammarCache cache, string settings)
    {
        var results = new List<(GrammarItem, IReadOnlyList<LtMatch>?)>(items.Count);
        foreach (var item in items)
        {
            var key = GrammarCache.KeyFor(LanguageToolClient.PinnedVersion, settings, item.CheckText);
            results.Add((item, cache.TryGet(key, out var entry) ? entry.Matches : null));
        }

        return results;
    }

    /// <summary>Runs one check, turning a malformed answer into a clear input error.</summary>
    private static (string Version, IReadOnlyList<LtMatch> Matches) Check(LanguageToolClient client, string text, string level)
    {
        try
        {
            return client.Check(text, level);
        }
        catch (JsonException)
        {
            throw new GrammarInputException($"{client.Server} answered something that is not a LanguageTool /v2/check response.");
        }
    }

    private static void CheckVersion(string version)
    {
        if (version != LanguageToolClient.PinnedVersion)
        {
            throw new GrammarInputException(
                $"the server runs LanguageTool '{version}', but the check is pinned to {LanguageToolClient.PinnedVersion} (tools/manifest.json); start the pinned server.");
        }
    }

    /// <summary>A finding before waivers are applied: the rule it came from, if any, decides which waiver can cover it.</summary>
    private sealed record Pending(LintSeverity Severity, string CheckId, GrammarItem Item, string Path, IReadOnlyList<string> Ids, string Message, string? Rule);

    /// <summary>A style or typography match is a fact (info); anything else (agreement, spelling, grammar) is a warning.</summary>
    private static LintSeverity SeverityOf(LtMatch match)
    {
        return match.IssueType is "style" or "typographical" or "whitespace" ? LintSeverity.Info : LintSeverity.Warning;
    }

    private static List<LintFinding> BuildFindings(List<(GrammarItem Item, IReadOnlyList<LtMatch>? Matches)> results, GrammarWaivers waivers, string waiversName)
    {
        var pending = new List<Pending>();
        foreach (var (item, matches) in results)
        {
            if (matches is null)
            {
                pending.Add(new Pending(
                    LintSeverity.Error,
                    "grammar.cache-miss",
                    item,
                    item.Path,
                    item.Ids,
                    "no cached result for this text; run grammar with the server (scripts/languagetool.sh start), then commit the cache",
                    null));
                continue;
            }

            if (item.IsExample)
            {
                AddOracle(pending, item, matches);
                continue;
            }

            foreach (var match in matches)
            {
                pending.Add(new Pending(
                    SeverityOf(match),
                    "grammar." + match.Rule,
                    item,
                    item.Path,
                    item.Ids,
                    Describe(item, match),
                    match.Rule));
            }
        }

        // A slot template renders once per filling; a finding the same in every filling is one finding, reported with the first filling.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var findings = new List<LintFinding>(pending.Count);
        foreach (var finding in pending.Where(finding => seen.Add(finding.CheckId + "\u001f" + finding.Path + "\u001f" + finding.Item.Id + "\u001f" + finding.Message)))
        {
            string? waivedBy = null;
            if (finding.Rule is not null)
            {
                waivedBy = waivers.Find(finding.Rule, finding.Item)?.Reason;
            }

            findings.Add(new LintFinding(finding.Severity, finding.CheckId, finding.Path, finding.Ids, finding.Message, waivedBy));
        }

        foreach (var waiver in waivers.Unused())
        {
            findings.Add(new LintFinding(
                LintSeverity.Warning,
                "grammar.waiver-unused",
                waiversName,
                [waiver.Rule, waiver.Id ?? waiver.Text ?? string.Empty],
                $"the waiver for {waiver.Describe()} matches no finding; remove it or fix its id or text"));
        }

        return [.. findings
            .OrderBy(static f => f.Path, StringComparer.Ordinal)
            .ThenBy(static f => f.CheckId, StringComparer.Ordinal)
            .ThenBy(static f => string.Join("\u001f", f.Ids), StringComparer.Ordinal)
            .ThenBy(static f => f.Message, StringComparer.Ordinal)
            .ThenBy(static f => f.Severity)];
    }

    /// <summary>
    /// The learner-error oracle for one example with expected tags. Flagged-and-tagged and tagged-but-silent are facts (info; a silent server is a blind
    /// spot, never a failure). Untagged-but-flagged is a warning: either the example is an error the author did not tag, or the server is wrong.
    /// </summary>
    private static void AddOracle(List<Pending> pending, GrammarItem item, IReadOnlyList<LtMatch> matches)
    {
        var expected = item.ExpectedErrors ?? [];
        var tagged = expected.Count > 0;
        var flagged = matches.Count > 0;
        var tags = string.Join(", ", expected);
        if (tagged && flagged)
        {
            var mapped = matches.SelectMany(match => RuleTags.For(match.Rule)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
            var note = mapped.Count > 0 && !mapped.Any(tag => expected.Contains(tag)) ? $" (the rule usually means [{string.Join(", ", mapped)}])" : string.Empty;
            pending.Add(new Pending(
                LintSeverity.Info,
                "oracle.flagged-and-tagged",
                item,
                item.Path,
                item.Ids,
                $"example \"{item.Original}\" expects [{tags}]; LanguageTool flags it: {string.Join("; ", matches.Select(match => Describe(item, match)))}{note}",
                null));
            return;
        }

        if (tagged)
        {
            pending.Add(new Pending(
                LintSeverity.Info,
                "oracle.tagged-but-silent",
                item,
                item.Path,
                item.Ids,
                $"example \"{item.Original}\" expects [{tags}]; LanguageTool is silent (a blind spot, not a failure)",
                null));
            return;
        }

        foreach (var match in matches)
        {
            var mapped = RuleTags.For(match.Rule);
            var hint = mapped.Count > 0
                ? $"; the rule usually means [{string.Join(", ", mapped)}]: add those expectations, or fix the example"
                : "; fix the example, or give it its expected error tags";
            pending.Add(new Pending(
                SeverityOf(match),
                "oracle.untagged-but-flagged",
                item,
                item.Path,
                item.Ids,
                $"example \"{item.Original}\" has no error tags but LanguageTool flags it: {Describe(item, match)}{hint}",
                match.Rule));
        }
    }

    /// <summary>One match as text: the rule's message, the span and the first suggestion.</summary>
    private static string Describe(GrammarItem item, LtMatch match)
    {
        var text = item.CheckText;
        var span = match.Offset >= 0 && match.Length > 0 && match.Offset + match.Length <= text.Length
            ? text.Substring(match.Offset, match.Length)
            : string.Empty;
        var suggestion = match.Replacements.Count > 0 ? $" -> \"{match.Replacements[0]}\"" : string.Empty;
        return $"{match.Rule}: {match.Message} (\"{span}\"{suggestion})";
    }

    private static int Report(List<LintFinding> findings, string format, bool strict, TextWriter output)
    {
        var errors = findings.Count(static f => !f.IsWaived && f.Severity == LintSeverity.Error);
        var warnings = findings.Count(static f => !f.IsWaived && f.Severity == LintSeverity.Warning);
        var info = findings.Count(static f => !f.IsWaived && f.Severity == LintSeverity.Info);
        var waived = findings.Count(static f => f.IsWaived);
        var unchecked_ = findings.Count(static f => f.CheckId == "grammar.cache-miss");
        if (format == "json")
        {
            var json = new GrammarJson(
                new GrammarSummaryJson(errors, warnings, info, waived, unchecked_),
                findings.Select(static f => new GrammarFindingJson(
                    f.Severity.ToString().ToLowerInvariant(),
                    f.CheckId,
                    f.Path,
                    f.Ids,
                    f.Message,
                    f.WaivedBy)).ToList());
            output.Write(JsonSerializer.Serialize(json, JsonOptions) + "\n");
        }
        else
        {
            foreach (var finding in findings)
            {
                output.Write(finding.ToLine() + "\n");
            }

            output.Write($"grammar: {errors} errors, {warnings} warnings, {info} info, {waived} waived, {unchecked_} unchecked\n");
        }

        return errors > 0 || (strict && warnings > 0) ? Program.ExitInvalid : Program.ExitOk;
    }

    private sealed record GrammarJson(GrammarSummaryJson Summary, IReadOnlyList<GrammarFindingJson> Findings);

    private sealed record GrammarSummaryJson(int Errors, int Warnings, int Info, int Waived, int Unchecked);

    private sealed record GrammarFindingJson(string Severity, string Check, string Path, IReadOnlyList<string> Ids, string Message, string? WaivedBy);
}

/// <summary>The rule-to-tag hints of the learner-error oracle, read from the embedded <c>rule-tags.json</c>.</summary>
internal static class RuleTags
{
    private static readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<string>>> Map = new(Load);

    /// <summary>The error tags a LanguageTool rule usually explains, or an empty list when the map does not name the rule.</summary>
    public static IReadOnlyList<string> For(string rule)
    {
        return Map.Value.TryGetValue(rule, out var tags) ? tags : [];
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Load()
    {
        using var stream = typeof(RuleTags).Assembly.GetManifestResourceStream("rule-tags.json")
            ?? throw new InvalidOperationException("rule-tags.json is not embedded in the content tool.");
        using var document = JsonDocument.Parse(stream);
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var rule in document.RootElement.GetProperty("rules").EnumerateObject())
        {
            result[rule.Name] = [.. rule.Value.EnumerateArray().Select(static tag => tag.GetString() ?? string.Empty)];
        }

        return result;
    }
}
