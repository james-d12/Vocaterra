using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Content;

/// <summary>How serious a lint finding is. An error is a defect in the content, a warning is a hazard worth a look, info is a fact worth knowing.</summary>
public enum LintSeverity
{
    /// <summary>A fact, such as an overlap that no prompt can show; never fails anything.</summary>
    Info,

    /// <summary>A hazard; fails only in strict mode.</summary>
    Warning,

    /// <summary>A defect in the content; fails the lint.</summary>
    Error,
}

/// <summary>One lint finding: what was found, where, and which ids it involves.</summary>
/// <param name="Severity">The severity (unchanged when the finding is waived).</param>
/// <param name="CheckId">The check that produced it, for example <c>coverage.uncovered</c> or <c>confusion.prompt</c>.</param>
/// <param name="Path">The bundle-relative path of the file to change, for example <c>intents/intents.json</c>.</param>
/// <param name="Ids">The ids involved (intents, prompts, pools, states, line ids), in the order the check names them.</param>
/// <param name="Message">What was found.</param>
/// <param name="WaivedBy">The waiver that covers the finding, such as <c>cast_spell lint.allowOverlap thank</c>; null when it is not waived.</param>
/// <param name="Suggestion">A hint the author may use, such as a suggested grammar; null when there is none.</param>
public sealed record LintFinding(
    LintSeverity Severity,
    string CheckId,
    string Path,
    IReadOnlyList<string> Ids,
    string Message,
    string? WaivedBy = null,
    string? Suggestion = null)
{
    /// <summary>True when a waiver covers the finding: it is reported but counts as neither error nor warning.</summary>
    [JsonIgnore]
    public bool IsWaived => WaivedBy is not null;

    /// <summary>Structural equality including the ids.</summary>
    public bool Equals(LintFinding? other)
    {
        return other is not null
            && Severity == other.Severity
            && string.Equals(CheckId, other.CheckId, StringComparison.Ordinal)
            && string.Equals(Path, other.Path, StringComparison.Ordinal)
            && Ids.SequenceEqual(other.Ids)
            && string.Equals(Message, other.Message, StringComparison.Ordinal)
            && string.Equals(WaivedBy, other.WaivedBy, StringComparison.Ordinal)
            && string.Equals(Suggestion, other.Suggestion, StringComparison.Ordinal);
    }

    /// <summary>Hash consistent with <see cref="Equals(LintFinding)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Severity);
        hash.Add(CheckId, StringComparer.Ordinal);
        hash.Add(Path, StringComparer.Ordinal);
        foreach (var id in Ids)
        {
            hash.Add(id, StringComparer.Ordinal);
        }

        hash.Add(Message, StringComparer.Ordinal);
        hash.Add(WaivedBy, StringComparer.Ordinal);
        hash.Add(Suggestion, StringComparer.Ordinal);
        return hash.ToHashCode();
    }

    /// <summary>
    /// The finding as one line: <c>severity check path [ids] message</c>, where a waived finding starts with <c>waived</c> instead of its severity and ends
    /// with the waiver. A suggestion is appended to the message.
    /// </summary>
    public string ToLine()
    {
        var builder = new StringBuilder();
        builder.Append(IsWaived ? "waived" : Severity.ToString().ToLowerInvariant())
            .Append(' ').Append(CheckId)
            .Append(' ').Append(Path)
            .Append(" [").Append(string.Join(", ", Ids)).Append("] ")
            .Append(Message);
        if (Suggestion is not null)
        {
            builder.Append(" Suggested grammar (a hint, check it): ").Append(Suggestion);
        }

        if (IsWaived)
        {
            builder.Append(" (was ").Append(Severity.ToString().ToLowerInvariant()).Append("; waiver: ").Append(WaivedBy).Append(')');
        }

        return builder.ToString();
    }
}

/// <summary>Options of <see cref="ContentLinter"/>.</summary>
public sealed record LintOptions
{
    /// <summary>The options <c>validate</c> uses: no CEFR check.</summary>
    public static LintOptions Default { get; } = new();

    /// <summary>The CEFR level to check dialogue, prompt text and answer-set canonicals against (<c>a1</c> or <c>a2</c>); null skips the CEFR check.</summary>
    public string? Level { get; init; }

    /// <summary>When true, a lemma above <see cref="Level"/> is an error instead of a warning.</summary>
    public bool FailOnLevel { get; init; }

    /// <summary>
    /// The ids of the prompts the host starts conversations with, or null for every prompt of the bundle. Reachability (intents, answer sets, agents and
    /// pools no prompt reaches) is measured from these prompts only, so a game that uses two of a bundle's prompts can ask what the other prompts' content
    /// costs. An id the bundle does not have is an error.
    /// </summary>
    public IReadOnlyList<string>? RootPrompts { get; init; }

    /// <summary>
    /// Dialogue pools the host voices itself, without an agent action: exact pool ids, or a prefix ending in <c>*</c> (<c>guide.*</c>). They count as reachable.
    /// </summary>
    public IReadOnlyList<string>? HostPools { get; init; }
}

/// <summary>The result of linting a bundle: every finding in ordinal order (path, check, ids, message).</summary>
/// <param name="Findings">The findings; waived ones included.</param>
public sealed record LintReport(IReadOnlyList<LintFinding> Findings)
{
    /// <summary>Number of unwaived errors.</summary>
    [JsonIgnore]
    public int Errors => Findings.Count(static f => !f.IsWaived && f.Severity == LintSeverity.Error);

    /// <summary>Number of unwaived warnings.</summary>
    [JsonIgnore]
    public int Warnings => Findings.Count(static f => !f.IsWaived && f.Severity == LintSeverity.Warning);

    /// <summary>Number of unwaived info findings.</summary>
    [JsonIgnore]
    public int Infos => Findings.Count(static f => !f.IsWaived && f.Severity == LintSeverity.Info);

    /// <summary>Number of waived findings, whatever their severity.</summary>
    [JsonIgnore]
    public int Waived => Findings.Count(static f => f.IsWaived);

    /// <summary>True when the lint passes: no error, and in strict mode no warning either. Info and waived findings never fail it.</summary>
    public bool Passes(bool strict)
    {
        return Errors == 0 && (!strict || Warnings == 0);
    }

    /// <summary>The findings of at least the given severity that are not waived, in report order.</summary>
    public IEnumerable<LintFinding> Active(LintSeverity minimum)
    {
        return Findings.Where(f => !f.IsWaived && f.Severity >= minimum);
    }

    /// <summary>The summary line: <c>lint: 0 errors, 2 warnings, 5 info, 1 waived</c>.</summary>
    public string SummaryLine()
    {
        return string.Create(CultureInfo.InvariantCulture, $"lint: {Errors} errors, {Warnings} warnings, {Infos} info, {Waived} waived");
    }

    /// <summary>One line per finding in report order, then the summary line, each ended by a newline.</summary>
    public string Format()
    {
        return Format(LintSeverity.Info);
    }

    /// <summary>
    /// Like <see cref="Format()"/> but leaves out the findings whose severity is below <paramref name="minimum"/> (a waived finding counts with the severity it
    /// had before it was waived). The summary line always counts everything.
    /// </summary>
    public string Format(LintSeverity minimum)
    {
        var builder = new StringBuilder();
        foreach (var finding in Findings.Where(f => f.Severity >= minimum))
        {
            builder.Append(finding.ToLine()).Append('\n');
        }

        builder.Append(SummaryLine()).Append('\n');
        return builder.ToString();
    }

    /// <summary>The report as canonical JSON: <c>{"summary":{...},"findings":[...]}</c>, findings in report order.</summary>
    public string ToJson()
    {
        return CanonicalJson.Serialize(new LintReportJson(new LintSummaryJson(Errors, Warnings, Infos, Waived), Findings));
    }

    /// <summary>Structural equality including the findings.</summary>
    public bool Equals(LintReport? other)
    {
        return other is not null && Findings.SequenceEqual(other.Findings);
    }

    /// <summary>Hash consistent with <see cref="Equals(LintReport)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        foreach (var finding in Findings)
        {
            hash.Add(finding);
        }

        return hash.ToHashCode();
    }
}

internal sealed record LintSummaryJson(int Errors, int Warnings, int Info, int Waived);

internal sealed record LintReportJson(LintSummaryJson Summary, IReadOnlyList<LintFinding> Findings);
