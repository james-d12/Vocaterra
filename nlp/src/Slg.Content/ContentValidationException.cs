using System.Globalization;

namespace Slg.Content;

/// <summary>Raised when a content bundle is missing files, fails hash verification or breaks a cross-reference; lists every problem found.</summary>
public sealed class ContentValidationException : Exception
{
    /// <summary>Creates an exception from the problems found; the message lists them all, one per line.</summary>
    public ContentValidationException(IEnumerable<string> problems)
        : this(Freeze(problems))
    {
    }

    private ContentValidationException(IReadOnlyList<string> problems)
        : base(BuildMessage(problems))
    {
        Problems = problems;
    }

    /// <summary>Every problem found, in the order the loader found them.</summary>
    public IReadOnlyList<string> Problems { get; }

    private static IReadOnlyList<string> Freeze(IEnumerable<string> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        return problems.ToArray();
    }

    private static string BuildMessage(IReadOnlyList<string> problems)
    {
        var header = string.Create(CultureInfo.InvariantCulture, $"Content bundle is invalid ({problems.Count} problem(s)):");
        return header + string.Concat(problems.Select(static problem => "\n  - " + problem));
    }
}
