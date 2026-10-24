namespace Vocaterra.Game.Core.Reporting;

/// <summary>What a player's report is about (S21, D-game-17): a bug, or a feature request.</summary>
public enum ReportKind
{
    /// <summary>Something is wrong in the game.</summary>
    Bug,

    /// <summary>Something the player would like the game to do.</summary>
    Feature,
}

/// <summary>The names of a <see cref="ReportKind"/>: the word in a file name and the English label on screen.</summary>
public static class ReportKinds
{
    /// <summary>The word of the kind in a report's file name (<c>bug</c> or <c>feature</c>), and the <c>--report-kind</c> value.</summary>
    /// <param name="kind">The kind.</param>
    public static string Word(ReportKind kind) => kind switch
    {
        ReportKind.Bug => "bug",
        ReportKind.Feature => "feature",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown report kind."),
    };

    /// <summary>The English label of the kind on screen and in the report (<c>Bug</c> or <c>Feature request</c>).</summary>
    /// <param name="kind">The kind.</param>
    public static string Label(ReportKind kind) => kind switch
    {
        ReportKind.Bug => "Bug",
        ReportKind.Feature => "Feature request",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown report kind."),
    };

    /// <summary>Reads a kind word (<c>bug</c> or <c>feature</c>, in any case).</summary>
    /// <param name="word">The word.</param>
    /// <param name="kind">The kind, when the word is one.</param>
    public static bool TryParse(string? word, out ReportKind kind)
    {
        switch (word?.Trim().ToLowerInvariant())
        {
            case "bug":
                kind = ReportKind.Bug;
                return true;
            case "feature":
                kind = ReportKind.Feature;
                return true;
            default:
                kind = ReportKind.Bug;
                return false;
        }
    }
}
