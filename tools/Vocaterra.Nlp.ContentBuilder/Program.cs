using System.Globalization;
using System.Text;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Nlp.ContentBuilder;

/// <summary>
/// Entry point of the build-time content tool. Commands: <c>validate</c>, <c>hash</c>, <c>pack</c>, <c>verify-pack</c>, <c>cefr-check</c>, <c>lint</c>,
/// <c>import-lexicon</c>, <c>lexicon-report</c>.
/// Exit codes: 0 success, 1 the content or input is invalid (for <c>lint</c>: it found errors, or warnings with <c>--strict</c>), 2 the command line is wrong.
/// </summary>
public static class Program
{
    /// <summary>Exit code for success.</summary>
    public const int ExitOk = 0;

    /// <summary>Exit code for invalid content or input.</summary>
    public const int ExitInvalid = 1;

    /// <summary>Exit code for a malformed command line.</summary>
    public const int ExitUsage = 2;

    private const string Usage =
        """
        Vocaterra.Nlp.ContentBuilder - build-time content pipeline

        Usage:
          validate <bundleDir> [--strict]                    verify hashes and cross-references; exit 1 with the problem list; then run
                                                             the lint and print its errors and warnings (they fail the command only
                                                             with --strict)
          hash <bundleDir> [--bundle-id ID] [--content-version V]
                                                             rewrite manifest.json from the files on disk
          pack <bundleDir> <out.zip>                         validate the bundle, then write it as a zip and <out.zip>.sha256 holding the
                                                             bundle hash; nothing is written for an invalid bundle
          verify-pack <zip>                                  extract the archive to a temporary folder, validate it there and compare its
                                                             bundle hash with <zip>.sha256
          cefr-check <bundleDir> --level a1|a2               list dialogue-template lemmas missing from the CEFR list
          lint <bundleDir> [--strict] [--level a1|a2] [--fail-on-level] [--json] [--prompts a,b] [--host-pools p*,q]
                                                             check the content for mistakes the validator cannot see: example coverage,
                                                             confusion between intents, grammar hazards, unreachable content, CEFR level
                                                             (with --level), example error expectations; one finding per line, then a
                                                             summary; exit 1 on errors (and on warnings with --strict); --fail-on-level
                                                             makes lemmas above --level errors; --json prints the report as JSON;
                                                             --prompts measures reachability from those prompts only; --host-pools names
                                                             pools the host voices itself (exact ids, or a prefix ending in *); hashes are
                                                             not verified, so run it before hash
          grammar <bundleDir> [--strings <jsonl> [--strings-only]] [--server URL] [--offline] [--cache FILE] [--waivers FILE]
                  [--level picky|default] [--format text|json] [--strict] [--prune]
                                                             check the Spanish strings (dialogue lines, prompts, answer sets, intent examples,
                                                             slot templates rendered from sampled fillings, and the host's JSONL strings) with a
                                                             local LanguageTool server (default http://localhost:8081, plain 'es'); results are
                                                             cached in <bundle's folder>/grammar/<name>-cache.json and waivers are read from
                                                             <name>-waivers.json beside it; --offline replays the cache only (a missing entry is an
                                                             error); learner examples with expected error tags are graded as an oracle; findings
                                                             print like lint; exit 1 on errors (and warnings with --strict), 3 when no server
                                                             answers in online mode (skipped, not passed); online runs keep the cache's other
                                                             entries unless --prune drops those this run did not use; docs/nlp/API.md has the JSONL format
          import-lexicon <kaikki.jsonl> <out.json> [--max N] [--frequency <es_50k.txt> [--top N]] [--hand-authored <dir|a.json,b.json>]
                                                             [--sidecar <out.source.json> [--sources <sources.json>]]
                                                             convert wiktextract Spanish JSONL into a lexicon file; with a frequency
                                                             list and --top, keep the N most frequent lemmas (lemma frequency = summed
                                                             counts of its forms in the list); --hand-authored names lexicon files (or a
                                                             folder, minus imported files) whose lemmas the import leaves alone, verbs
                                                             excepted; the sidecar records URLs, retrieval date,
                                                             SHA-256, line counts, parameters and the importer version
          lexicon-report <bundleDir>                         list each lexicon file of the bundle, how the files overlap and what the
                                                             merged lexicon holds
        """;

    /// <summary>Process entry point.</summary>
    public static int Main(string[] args)
    {
        return Run(args);
    }

    /// <summary>Runs the tool on the console streams.</summary>
    public static int Run(string[] args)
    {
        return Run(args, Console.Out, Console.Error);
    }

    /// <summary>
    /// Runs the tool, writing normal output to <paramref name="output"/> and problems and usage errors to <paramref name="error"/>. The HTTP handler
    /// is used only by <c>grammar</c>, in place of the network (tests pass a stub).
    /// </summary>
    public static int Run(string[] args, TextWriter output, TextWriter error, HttpMessageHandler? httpHandler = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (args.Length == 0)
        {
            error.WriteLine(Usage);
            return ExitUsage;
        }

        var command = args[0];
        if (command is "help" or "--help" or "-h")
        {
            output.WriteLine(Usage);
            return ExitOk;
        }

        try
        {
            return command switch
            {
                "validate" => Validate(new ArgumentList(args, 1, [], ["--strict"]), output, error),
                "hash" => Hash(new ArgumentList(args, 1, ["--bundle-id", "--content-version"]), output),
                "pack" => Pack(new ArgumentList(args, 1, []), output, error),
                "verify-pack" => VerifyPack(new ArgumentList(args, 1, []), output, error),
                "lint" => Lint(new ArgumentList(args, 1, ["--level", "--prompts", "--host-pools"], ["--strict", "--fail-on-level", "--json"]), output, error),
                "cefr-check" => CefrCheck(new ArgumentList(args, 1, ["--level"]), output, error),
                "grammar" => GrammarCommand.Run(
                    new ArgumentList(args, 1, ["--strings", "--server", "--cache", "--waivers", "--level", "--format"], ["--offline", "--strict", "--strings-only", "--prune"]),
                    output,
                    error,
                    httpHandler),
                "import-lexicon" => ImportLexicon(new ArgumentList(args, 1, ["--max", "--frequency", "--top", "--sidecar", "--sources", "--hand-authored"]), output, error),
                "lexicon-report" => LexiconReportCommand(new ArgumentList(args, 1, []), output, error),
                _ => throw new UsageException($"Unknown command '{command}'."),
            };
        }
        catch (UsageException ex)
        {
            error.WriteLine(ex.Message);
            error.WriteLine(Usage);
            return ExitUsage;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException)
        {
            error.WriteLine(ex.Message);
            return ExitInvalid;
        }
    }

    private static int Validate(ArgumentList arguments, TextWriter output, TextWriter error)
    {
        var dir = arguments.Positional(0, "bundleDir", exactly: 1);
        try
        {
            var bundle = ContentLoader.Load(dir);
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"OK: bundle '{bundle.Manifest.BundleId}' {bundle.Manifest.ContentVersion}, {bundle.Manifest.Files.Count} files, hash {bundle.ContentHash}"));
            var strict = arguments.Flag("--strict");
            var report = ContentLinter.Lint(bundle);
            output.Write(report.Format(LintSeverity.Warning));
            if (strict && !report.Passes(strict: true))
            {
                error.WriteLine("validate --strict: the lint found errors or warnings (run 'lint' for the full report).");
                return ExitInvalid;
            }

            return ExitOk;
        }
        catch (ContentValidationException ex)
        {
            error.WriteLine(ex.Message);
            return ExitInvalid;
        }
    }

    private static int Lint(ArgumentList arguments, TextWriter output, TextWriter error)
    {
        var dir = arguments.Positional(0, "bundleDir", exactly: 1);
        var level = arguments.Option("--level")?.ToLowerInvariant();
        if (level is not (null or "a1" or "a2"))
        {
            throw new UsageException("lint takes --level a1 or --level a2.");
        }

        if (arguments.Flag("--fail-on-level") && level is null)
        {
            throw new UsageException("--fail-on-level needs --level a1|a2.");
        }

        ContentBundle bundle;
        try
        {
            // Hashes are not verified so the lint can run while content is being edited, before hash; run validate before shipping.
            bundle = ContentLoader.Load(dir, new ContentLoadOptions { VerifyHashes = false });
        }
        catch (ContentValidationException ex)
        {
            error.WriteLine(ex.Message);
            return ExitInvalid;
        }

        LintReport report;
        try
        {
            report = ContentLinter.Lint(bundle, new LintOptions
            {
                Level = level,
                FailOnLevel = arguments.Flag("--fail-on-level"),
                RootPrompts = SplitList(arguments.Option("--prompts")),
                HostPools = SplitList(arguments.Option("--host-pools")),
            });
        }
        catch (ArgumentException ex)
        {
            error.WriteLine(ex.Message);
            return ExitInvalid;
        }

        output.Write(arguments.Flag("--json") ? report.ToJson() + "\n" : report.Format());
        return report.Passes(arguments.Flag("--strict")) ? ExitOk : ExitInvalid;
    }

    private static string[]? SplitList(string? value)
    {
        return value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static int Hash(ArgumentList arguments, TextWriter output)
    {
        var dir = arguments.Positional(0, "bundleDir", exactly: 1);
        var manifest = ContentManifest.Compute(dir, arguments.Option("--bundle-id"), arguments.Option("--content-version"));
        File.WriteAllText(Path.Combine(dir, ContentPaths.Manifest), manifest.ToJson(), new UTF8Encoding(false));
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"wrote {ContentPaths.Manifest}: {manifest.Files.Count} files, hash {manifest.BundleHash}"));
        return ExitOk;
    }

    private static int Pack(ArgumentList arguments, TextWriter output, TextWriter error)
    {
        var dir = arguments.Positional(0, "bundleDir", exactly: 2);
        var archive = arguments.Positional(1, "out.zip", exactly: 2);
        try
        {
            var (hash, files) = BundlePack.Pack(dir, archive);
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"packed {files} files into {archive}, bundle hash {hash}"));
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"wrote {archive}{BundlePack.SidecarSuffix}"));
            return ExitOk;
        }
        catch (ContentValidationException ex)
        {
            error.WriteLine(ex.Message);
            return ExitInvalid;
        }
    }

    private static int VerifyPack(ArgumentList arguments, TextWriter output, TextWriter error)
    {
        var archive = arguments.Positional(0, "zip", exactly: 1);
        try
        {
            var (hash, files) = BundlePack.Verify(archive);
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"OK: {archive} holds {files} entries, bundle hash {hash}"));
            return ExitOk;
        }
        catch (Exception ex) when (ex is ContentValidationException or BundlePackException)
        {
            error.WriteLine(ex.Message);
            return ExitInvalid;
        }
    }

    private static int CefrCheck(ArgumentList arguments, TextWriter output, TextWriter error)
    {
        var dir = arguments.Positional(0, "bundleDir", exactly: 1);
        var level = arguments.Option("--level")?.ToLowerInvariant();
        if (level is not ("a1" or "a2"))
        {
            throw new UsageException("cefr-check needs --level a1 or --level a2.");
        }

        ContentBundle bundle;
        try
        {
            // Hashes are not verified so the check can run while content is being edited; run validate before shipping.
            bundle = ContentLoader.Load(dir, new ContentLoadOptions { VerifyHashes = false });
        }
        catch (ContentValidationException ex)
        {
            error.WriteLine(ex.Message);
            return ExitInvalid;
        }

        output.Write(CefrChecker.Format(CefrChecker.Check(bundle, level)));
        return ExitOk;
    }

    private static int ImportLexicon(ArgumentList arguments, TextWriter output, TextWriter error)
    {
        var input = arguments.Positional(0, "kaikki.jsonl", exactly: 2);
        var outputPath = arguments.Positional(1, "out.json", exactly: 2);
        int? max = null;
        if (arguments.Option("--max") is { } rawMax)
        {
            if (!int.TryParse(rawMax, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < 1)
            {
                throw new UsageException("--max needs a positive whole number.");
            }

            max = parsed;
        }

        int? top = null;
        if (arguments.Option("--top") is { } rawTop)
        {
            if (!int.TryParse(rawTop, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < 1)
            {
                throw new UsageException("--top needs a positive whole number.");
            }

            top = parsed;
        }

        var frequencyPath = arguments.Option("--frequency");
        var sidecarPath = arguments.Option("--sidecar");
        if (top is not null && frequencyPath is null)
        {
            throw new UsageException("--top needs --frequency <es_50k.txt>.");
        }

        if (arguments.Option("--sources") is not null && sidecarPath is null)
        {
            throw new UsageException("--sources only makes sense with --sidecar.");
        }

        if (!File.Exists(input))
        {
            error.WriteLine($"Input file not found: {input}");
            return ExitInvalid;
        }

        if (frequencyPath is not null && !File.Exists(frequencyPath))
        {
            error.WriteLine($"Frequency list not found: {frequencyPath}");
            return ExitInvalid;
        }

        var frequency = frequencyPath is null ? null : FrequencyList.ReadFile(frequencyPath);
        var handAuthored = arguments.Option("--hand-authored") is { } rawHand ? HandAuthoredLemmas.Read(rawHand) : null;
        var options = new KaikkiImportOptions { MaxEntries = max, Frequency = frequency, Top = top, HandAuthored = handAuthored };
        var result = KaikkiImporter.ImportFile(input, options);

        // The sidecar is built before anything is written, so a source file that differs from sources.json leaves no half-finished pair behind.
        string? sidecarJson = null;
        if (sidecarPath is not null)
        {
            var sourcesPath = arguments.Option("--sources") ?? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(input))!, SourcesFile.FileName);
            var records = SourcesFile.Read(sourcesPath);
            (FrequencyList, string)? withPath = frequency is null ? null : (frequency, frequencyPath!);
            sidecarJson = LexiconSidecarBuilder.Build(result, options, input, withPath, records, Path.GetFileName(outputPath), handAuthored?.FileNames).ToJson();
        }

        File.WriteAllText(outputPath, KaikkiImporter.ToJson(result.Entries), new UTF8Encoding(false));
        output.Write(result.Summary());
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"wrote {outputPath}"));
        if (sidecarPath is not null)
        {
            File.WriteAllText(sidecarPath, sidecarJson!, new UTF8Encoding(false));
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"wrote {sidecarPath}"));
        }

        return ExitOk;
    }

    private static int LexiconReportCommand(ArgumentList arguments, TextWriter output, TextWriter error)
    {
        var dir = arguments.Positional(0, "bundleDir", exactly: 1);
        if (!Directory.Exists(dir))
        {
            error.WriteLine($"Bundle directory not found: {dir}");
            return ExitInvalid;
        }

        output.Write(LexiconReport.Format(LexiconReport.Build(dir)));
        return ExitOk;
    }
}

/// <summary>A malformed command line.</summary>
internal sealed class UsageException : Exception
{
    public UsageException(string message)
        : base(message)
    {
    }
}

/// <summary>Positional arguments and <c>--name value</c> or <c>--name=value</c> options of one command.</summary>
internal sealed class ArgumentList
{
    private readonly List<string> _positional = [];
    private readonly Dictionary<string, string> _options = new(StringComparer.Ordinal);
    private readonly HashSet<string> _flags = new(StringComparer.Ordinal);

    public ArgumentList(string[] args, int start, string[] knownOptions, string[]? knownFlags = null)
    {
        for (var i = start; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                _positional.Add(arg);
                continue;
            }

            if (knownFlags is not null && Array.IndexOf(knownFlags, arg) >= 0)
            {
                if (!_flags.Add(arg))
                {
                    throw new UsageException($"Option '{arg}' is given twice.");
                }

                continue;
            }

            var equals = arg.IndexOf('=', StringComparison.Ordinal);
            var name = equals < 0 ? arg : arg[..equals];
            if (Array.IndexOf(knownOptions, name) < 0)
            {
                throw new UsageException($"Unknown option '{name}'.");
            }

            string value;
            if (equals >= 0)
            {
                value = arg[(equals + 1)..];
            }
            else if (i + 1 < args.Length)
            {
                value = args[++i];
            }
            else
            {
                throw new UsageException($"Option '{name}' needs a value.");
            }

            if (!_options.TryAdd(name, value))
            {
                throw new UsageException($"Option '{name}' is given twice.");
            }
        }
    }

    /// <summary>Returns the positional argument at the index; the command takes exactly <paramref name="exactly"/> of them.</summary>
    public string Positional(int index, string name, int exactly)
    {
        if (_positional.Count != exactly)
        {
            throw new UsageException(string.Create(CultureInfo.InvariantCulture, $"Expected {exactly} argument(s) but got {_positional.Count}."));
        }

        return _positional[index].Length > 0 ? _positional[index] : throw new UsageException($"Argument <{name}> must not be empty.");
    }

    public bool Flag(string name)
    {
        return _flags.Contains(name);
    }

    public string? Option(string name)
    {
        return _options.GetValueOrDefault(name);
    }
}
