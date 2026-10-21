using System.Globalization;

namespace Vocaterra.Nlp.Cli;

/// <summary>Raised for a malformed command line; the message says what is wrong.</summary>
public sealed class UsageException : Exception
{
    /// <summary>Creates the exception with a message for the user.</summary>
    public UsageException(string message)
        : base(message)
    {
    }
}

/// <summary>A minimal hand-rolled argument parser: <c>--name value</c> options (repeatable) and positional arguments.</summary>
internal sealed class CommandLine
{
    private readonly Dictionary<string, List<string>> _options = new(StringComparer.Ordinal);

    private CommandLine(List<string> positionals)
    {
        Positionals = positionals;
    }

    public IReadOnlyList<string> Positionals { get; }

    public static CommandLine Parse(string[] args, int start, IReadOnlyList<string> valueOptions)
    {
        var positionals = new List<string>();
        var line = new CommandLine(positionals);
        for (var i = start; i < args.Length; i++)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                positionals.Add(arg);
                continue;
            }

            if (!valueOptions.Contains(arg, StringComparer.Ordinal))
            {
                throw new UsageException($"Unknown option '{arg}'.");
            }

            if (i + 1 >= args.Length)
            {
                throw new UsageException($"Option '{arg}' needs a value.");
            }

            if (!line._options.TryGetValue(arg, out var values))
            {
                values = [];
                line._options[arg] = values;
            }

            values.Add(args[++i]);
        }

        return line;
    }

    public string? Value(string name)
    {
        return _options.TryGetValue(name, out var values) ? values[^1] : null;
    }

    public string Required(string name)
    {
        return Value(name) ?? throw new UsageException($"Missing required option '{name}'.");
    }

    public IReadOnlyList<string> Values(string name)
    {
        return _options.TryGetValue(name, out var values) ? values : [];
    }

    public T Number<T>(string name, T fallback, Func<string, IFormatProvider, T> parse)
    {
        var text = Value(name);
        if (text is null)
        {
            return fallback;
        }

        try
        {
            return parse(text, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new UsageException($"Option '{name}' expects a number, got '{text}'.");
        }
    }
}
