using System.Globalization;
using System.Text;
using Slg.Content;

namespace Slg.ContentBuilder;

/// <summary>
/// Entry point of the build-time content tool. Commands: <c>validate</c>, <c>hash</c>, <c>cefr-check</c>, <c>import-lexicon</c>.
/// Exit codes: 0 success, 1 the content or input is invalid, 2 the command line is wrong.
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
        Slg.ContentBuilder - build-time content pipeline

        Usage:
          validate <bundleDir>                               verify hashes and cross-references; exit 1 with the problem list
          hash <bundleDir> [--bundle-id ID] [--content-version V]
                                                             rewrite manifest.json from the files on disk
          cefr-check <bundleDir> --level a1|a2               list dialogue-template lemmas missing from the CEFR list
          import-lexicon <kaikki.jsonl> <out.json> [--max N] convert wiktextract Spanish JSONL into a lexicon file
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

    /// <summary>Runs the tool, writing normal output to <paramref name="output"/> and problems and usage errors to <paramref name="error"/>.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
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
                "validate" => Validate(new ArgumentList(args, 1, []), output, error),
                "hash" => Hash(new ArgumentList(args, 1, ["--bundle-id", "--content-version"]), output),
                "cefr-check" => CefrCheck(new ArgumentList(args, 1, ["--level"]), output, error),
                "import-lexicon" => ImportLexicon(new ArgumentList(args, 1, ["--max"]), output, error),
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
            return ExitOk;
        }
        catch (ContentValidationException ex)
        {
            error.WriteLine(ex.Message);
            return ExitInvalid;
        }
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

        if (!File.Exists(input))
        {
            error.WriteLine($"Input file not found: {input}");
            return ExitInvalid;
        }

        var result = KaikkiImporter.ImportFile(input, max);
        File.WriteAllText(outputPath, KaikkiImporter.ToJson(result.Entries), new UTF8Encoding(false));
        output.Write(result.Summary());
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"wrote {outputPath}"));
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

    public ArgumentList(string[] args, int start, string[] knownOptions)
    {
        for (var i = start; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                _positional.Add(arg);
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

    public string? Option(string name)
    {
        return _options.GetValueOrDefault(name);
    }
}
