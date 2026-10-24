using System.Globalization;
using System.Text;

namespace Vocaterra.Game.Reporting;

/// <summary>
/// What a crash report holds (X05, R04). Every value is text the caller has already read: the game layer reads no clock and no engine, so the
/// front-end formats the time, reads the state it can read and tails the log before it calls <see cref="CrashReport.Build"/>.
/// </summary>
/// <param name="Stopped">Whether the game stopped (a crash on the game loop, or an unhandled exception), or went on (an unobserved task exception).</param>
/// <param name="SavedAt">The local date and time of the report, formatted by the caller (<c>2026-10-23 10:15:00</c>).</param>
/// <param name="BuildCommit">The commit the build was made from, or <c>unknown</c>.</param>
/// <param name="Where">Where the exception came out (<c>the game loop</c>, <c>an unhandled exception</c>, <c>an unobserved task exception</c>).</param>
/// <param name="Session">The play session, or <see langword="null"/> when the state could not be read.</param>
/// <param name="Tick">The simulation tick, or <see langword="null"/> when the state could not be read.</param>
/// <param name="ExceptionType">The exception's type name.</param>
/// <param name="Message">The exception's message.</param>
/// <param name="Stack">The exception's text with its stack trace and inner exceptions (<c>ToString()</c>), or the stack trace alone.</param>
/// <param name="State">The game's state as name and value rows, in the order to show them; empty when nothing could be read.</param>
/// <param name="StateNote">Why the state is missing or partial (<c>not read: ...</c>), or <see langword="null"/>.</param>
/// <param name="SaveNote">What the last-chance save did (<c>written: crash.save</c>, or why it was not written).</param>
/// <param name="LogLines">The session log's lines, oldest first; the report keeps the newest <see cref="ReportBounds.LogLines"/>.</param>
public sealed record CrashInput(
    bool Stopped,
    string SavedAt,
    string BuildCommit,
    string Where,
    int? Session,
    long? Tick,
    string ExceptionType,
    string Message,
    string Stack,
    IReadOnlyList<(string Item, string Value)> State,
    string? StateNote,
    string SaveNote,
    IReadOnlyList<string> LogLines);

/// <summary>
/// The Markdown of a crash report (X05, R04), in the format of the F8 reports (S21): a title, the kind, the time and the session; the exception
/// and its stack in fences; the game state as a table; the last-chance save; and the newest <see cref="ReportBounds.LogLines"/> lines of the session
/// log. Pure text: the same input always gives the same Markdown (a golden test pins it).
/// </summary>
public static class CrashReport
{
    /// <summary>The title of a crash report: the exception's type, without its namespace.</summary>
    /// <param name="exceptionType">The exception's type name.</param>
    public static string Title(string exceptionType)
    {
        ArgumentNullException.ThrowIfNull(exceptionType);
        var name = exceptionType.Trim();
        var dot = name.LastIndexOf('.');
        if (dot >= 0 && dot < name.Length - 1)
        {
            name = name[(dot + 1)..];
        }

        return name.Length == 0 ? "Unknown exception" : name;
    }

    /// <summary>Builds the Markdown of a crash report.</summary>
    /// <param name="crash">The crash.</param>
    public static string Build(CrashInput crash)
    {
        ArgumentNullException.ThrowIfNull(crash);
        var builder = new StringBuilder();
        builder.Append("# Crash report: ").Append(Title(crash.ExceptionType)).Append("\n\n");
        builder.Append("- **Kind:** ").Append(crash.Stopped ? "Crash (the game stopped)" : "Unobserved task exception (the game kept running)").Append('\n');
        builder.Append("- **Saved:** ").Append(crash.SavedAt).Append(" (local time)\n");
        builder.Append("- **Session:** ").Append(crash.Session is { } session ? session.ToString(CultureInfo.InvariantCulture) : "unknown")
            .Append(", tick ").Append(crash.Tick is { } tick ? tick.ToString(CultureInfo.InvariantCulture) : "unknown").Append('\n');
        builder.Append("- **Build commit:** ").Append(crash.BuildCommit).Append('\n');
        builder.Append("- **Where:** ").Append(crash.Where).Append('\n');
        builder.Append('\n');

        builder.Append("## Exception\n\n");
        builder.Append("**").Append(crash.ExceptionType).Append("**: ").Append(OneLine(crash.Message)).Append("\n\n");
        var stack = crash.Stack.TrimEnd();
        var stackFence = Fence(stack);
        builder.Append(stackFence).Append("text\n").Append(stack).Append('\n').Append(stackFence).Append("\n\n");

        builder.Append("## Game state\n\n");
        if (crash.State.Count == 0)
        {
            builder.Append("The game state could not be read.\n\n");
        }
        else
        {
            builder.Append("| Item | Value |\n| --- | --- |\n");
            foreach (var (item, value) in crash.State)
            {
                builder.Append("| ").Append(Cell(item)).Append(" | ").Append(Cell(value)).Append(" |\n");
            }

            builder.Append('\n');
        }

        if (!string.IsNullOrWhiteSpace(crash.StateNote))
        {
            builder.Append(OneLine(crash.StateNote)).Append("\n\n");
        }

        builder.Append("## Last-chance save\n\n").Append(OneLine(crash.SaveNote)).Append("\n\n");

        var lines = crash.LogLines.TakeLast(ReportBounds.LogLines).ToList();
        var fence = Fence(lines);
        builder.Append("## Session log (the newest ").Append(ReportBounds.LogLines.ToString(CultureInfo.InvariantCulture)).Append(" lines at most)\n\n");
        builder.Append(fence).Append("jsonl\n");
        foreach (var line in lines)
        {
            builder.Append(line.TrimEnd('\r', '\n')).Append('\n');
        }

        builder.Append(fence).Append('\n');
        return builder.ToString();
    }

    // A fence longer than any run of backticks in the text, so a stack or a log line can hold a fence of its own.
    private static string Fence(string text) => new('`', Math.Max(3, LongestBacktickRun(text) + 1));

    private static string Fence(IEnumerable<string> lines) => new('`', Math.Max(3, lines.Select(LongestBacktickRun).DefaultIfEmpty(0).Max() + 1));

    private static int LongestBacktickRun(string text)
    {
        var longest = 0;
        var run = 0;
        foreach (var c in text)
        {
            run = c == '`' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        return longest;
    }

    private static string OneLine(string text) => text.Replace("\r", " ").Replace("\n", " ").Trim();

    private static string Cell(string value) => OneLine(value).Replace("|", "\\|");
}
