using System.Globalization;
using System.Text;
using Vocaterra.Game.Language.Learner;

namespace Vocaterra.Game.Reporting;

/// <summary>
/// The Markdown of a report (S21, D-game-17): a title (the first line of the player's text) and the kind, the text, the linked screenshot, the
/// game state as a table, the newest <see cref="ReportBounds.RecentTurns"/> turns, and the newest <see cref="ReportBounds.LogLines"/> lines of the
/// session log in a fenced block. Pure text: the same snapshot always gives the same Markdown (the golden test pins it).
/// </summary>
public static class ReportMarkdown
{
    /// <summary>The longest title, in characters, before it is cut with an ellipsis.</summary>
    public const int MaxTitleLength = 80;

    /// <summary>The title of a report with no text: a saved report is never blank, so this only guards the builder.</summary>
    public const string UntitledTitle = "Untitled report";

    /// <summary>Builds the Markdown of a report.</summary>
    /// <param name="report">The report.</param>
    public static string Build(ReportSnapshot report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var state = report.State;
        var builder = new StringBuilder();
        builder.Append("# ").Append(Title(report.Text)).Append('\n').Append('\n');
        builder.Append("- **Kind:** ").Append(ReportKinds.Label(report.Kind)).Append('\n');
        builder.Append("- **Saved:** ").Append(report.SavedAt).Append(" (local time)\n");
        builder.Append("- **Session:** ").Append(report.Session.ToString(CultureInfo.InvariantCulture))
            .Append(", tick ").Append(report.Tick.ToString(CultureInfo.InvariantCulture)).Append('\n');
        builder.Append('\n');

        builder.Append("## Player's text\n\n").Append(report.Text.TrimEnd()).Append("\n\n");
        builder.Append("## Screenshot\n\n").Append("![The screen when the report was opened](").Append(report.ScreenshotFile).Append(")\n\n");

        builder.Append("## Game state\n\n| Item | Value |\n| --- | --- |\n");
        Row(builder, "Build commit", state.BuildCommit);
        Row(builder, "Bundle", $"{state.BundleId}, hash {state.BundleHash}");
        Row(builder, "Seed", state.Seed.ToString(CultureInfo.InvariantCulture));
        Row(builder, "Position", string.Create(CultureInfo.InvariantCulture, $"column {state.X:0.0}, row {state.Z:0.0}, feet at height {state.FeetY:0.0} (blocks)"));
        Row(builder, "View", string.Create(CultureInfo.InvariantCulture, $"yaw {state.YawDegrees:0} degrees, pitch {state.PitchDegrees:0} degrees"));
        Row(builder, "Objective", state.Objective);
        Row(builder, "Objective steps", state.ObjectiveSteps.Count == 0
            ? "none"
            : string.Join("; ", state.ObjectiveSteps.Select(static step => $"{step.Text} ({(step.Done ? "done" : "not done")})")));
        Row(builder, "Lesson", state.Lesson);
        Row(builder, "Open panel", state.OpenPanel);
        Row(builder, "Quality", state.Quality);
        Row(builder, "Greyscale", state.Greyscale ? "on" : "off");
        Row(builder, "Speech", state.Speech);
        Row(builder, "Microphone", state.Microphone);
        builder.Append('\n');

        // The newest turns (LearnerRecord.Recent is newest first); shown oldest first, so the table reads as the play went.
        var turns = report.RecentTurns.Take(ReportBounds.RecentTurns).Reverse().ToList();
        builder.Append("## Recent turns\n\n");
        if (turns.Count == 0)
        {
            builder.Append("No line was said or typed in this session yet.\n\n");
        }
        else
        {
            builder.Append("The newest lines, up to ").Append(ReportBounds.RecentTurns.ToString(CultureInfo.InvariantCulture))
                .Append(", oldest first.\n\n");
            builder.Append("| # | Text | Voice or typed | Where | Understood | Error tags |\n| --- | --- | --- | --- | --- | --- |\n");
            foreach (var turn in turns)
            {
                builder.Append("| ").Append(turn.Id.ToString(CultureInfo.InvariantCulture))
                    .Append(" | ").Append(Cell(turn.Text))
                    .Append(" | ").Append(SourceWord(turn.Source))
                    .Append(" | ").Append(Cell(Where(turn)))
                    .Append(" | ").Append(Understood(turn))
                    .Append(" | ").Append(Cell(turn.Errors.Count == 0 ? "none" : string.Join(", ", turn.Errors.Select(static e => e.Tag))))
                    .Append(" |\n");
            }

            builder.Append('\n');
        }

        // The newest log lines, in a fence longer than any run of backticks in them (a player's text can hold a fence of its own).
        var lines = report.LogLines.TakeLast(ReportBounds.LogLines).ToList();
        var fence = new string('`', Math.Max(3, LongestBacktickRun(lines) + 1));
        builder.Append("## Session log (the newest ").Append(ReportBounds.LogLines.ToString(CultureInfo.InvariantCulture)).Append(" lines at most)\n\n");
        builder.Append(fence).Append("jsonl\n");
        foreach (var line in lines)
        {
            builder.Append(line.TrimEnd('\r', '\n')).Append('\n');
        }

        builder.Append(fence).Append('\n');
        return builder.ToString();
    }

    /// <summary>The title of a report: the first non-blank line of its text, cut to <see cref="MaxTitleLength"/>.</summary>
    /// <param name="text">The player's text.</param>
    public static string Title(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var first = text.Split('\n').Select(static line => line.Trim()).FirstOrDefault(static line => line.Length > 0);
        if (first is null)
        {
            return UntitledTitle;
        }

        return first.Length <= MaxTitleLength ? first : first[..(MaxTitleLength - 1)] + "…";
    }

    private static void Row(StringBuilder builder, string item, string value) =>
        builder.Append("| ").Append(item).Append(" | ").Append(Cell(value)).Append(" |\n");

    // A table cell: one line, and the pipe escaped.
    private static string Cell(string value) => value.Replace("\r", " ").Replace("\n", " ").Replace("|", "\\|").Trim();

    private static string SourceWord(InputSource source) => source switch
    {
        InputSource.Voice => "voice",
        InputSource.Typed => "typed",
        InputSource.Button => "button",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown input source."),
    };

    private static string Where(InputEntry turn) => turn.Place switch
    {
        InputPlace.Dialogue => $"talk with {turn.Npc ?? "an NPC"}",
        InputPlace.Lesson => $"lesson {turn.Unit ?? string.Empty}".TrimEnd(),
        InputPlace.Check => $"check item {turn.Unit ?? string.Empty}".TrimEnd(),
        _ => throw new ArgumentOutOfRangeException(nameof(turn), turn.Place, "Unknown place."),
    };

    private static string Understood(InputEntry turn)
    {
        var word = turn.Communicated ? "yes" : "no";
        return turn.Held ? $"{word} (held back as unsure)" : word;
    }

    private static int LongestBacktickRun(IEnumerable<string> lines)
    {
        var longest = 0;
        foreach (var line in lines)
        {
            var run = 0;
            foreach (var c in line)
            {
                run = c == '`' ? run + 1 : 0;
                longest = Math.Max(longest, run);
            }
        }

        return longest;
    }
}
