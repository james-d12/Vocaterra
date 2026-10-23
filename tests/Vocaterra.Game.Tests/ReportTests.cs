using Vocaterra.Game.Reports;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Game.Tests;

/// <summary>
/// The in-game report of S21 (D-game-17) as plain logic: the file names and their counter, the Markdown for a fixed state (a golden), and the bounds
/// (the newest 10 turns and 200 log lines). Nothing here reads a clock: the save time is given.
/// </summary>
public sealed class ReportTests
{
    private const string FixedSavedAt = "2026-10-22 10:01:02";

    // The golden: the Markdown of a fixed report (the first line is the title, the turns are shown oldest first, the log is in a fence).
    private const string Golden = """
        # The bridge is missing

        - **Kind:** Bug
        - **Saved:** 2026-10-22 10:01:02 (local time)
        - **Session:** 2, tick 4681

        ## Player's text

        The bridge is missing
        It should be by the river.

        ## Screenshot

        ![The screen when the report was opened](report-s2-t4681-bug.png)

        ## Game state

        | Item | Value |
        | --- | --- |
        | Build commit | abc1234 |
        | Bundle | game-v1, hash 0123abcd |
        | Seed | 42 |
        | Position | column 230.5, row 221.5, feet at height 12.0 (blocks) |
        | View | yaw 90 degrees, pitch -5 degrees |
        | Objective | Campamento: Wood 2/5 · Berries 0/3 |
        | Objective steps | Wood 2/5 (not done); Berries 0/3 (not done) |
        | Lesson | first-words, step 3 of 9 |
        | Open panel | none (the world) |
        | Quality | high |
        | Greyscale | off |
        | Speech | models found, speech ready |
        | Microphone | found |

        ## Recent turns

        The newest lines, up to 10, oldest first.

        | # | Text | Voice or typed | Where | Understood | Error tags |
        | --- | --- | --- | --- | --- | --- |
        | 1 | ¿Dónde \| está? | voice | lesson first-words/answer | no (held back as unsure) | gender |
        | 2 | agua | typed | talk with guide | yes | none |

        ## Session log (the newest 200 lines at most)

        ```jsonl
        {"type":"session_start"}
        {"type":"turn"}
        ```

        """;

    [Fact]
    public void TheMarkdownOfAFixedReportIsTheGolden()
    {
        Assert.Equal(Golden.ReplaceLineEndings("\n"), ReportMarkdown.Build(FixedReport()).ReplaceLineEndings("\n"));
    }

    [Fact]
    public void TheTitleIsTheFirstNonBlankLineAndIsCut()
    {
        Assert.Equal("The bridge is missing", ReportMarkdown.Title("\n  The bridge is missing  \nmore"));
        Assert.Equal(ReportMarkdown.UntitledTitle, ReportMarkdown.Title("  \n  "));
        var longText = new string('a', 200);
        Assert.Equal(ReportMarkdown.MaxTitleLength, ReportMarkdown.Title(longText).Length);
        Assert.EndsWith("…", ReportMarkdown.Title(longText), StringComparison.Ordinal);
    }

    [Fact]
    public void ARecentTurnsTableHoldsTheNewestTenTurnsAndNoMore()
    {
        var turns = Enumerable.Range(1, 25).Select(id => Turn(id)).Reverse().ToList(); // newest first, as LearnerRecord.Recent gives them
        var markdown = ReportMarkdown.Build(FixedReport() with { RecentTurns = turns });
        var rows = markdown.Split('\n').Where(line => line.StartsWith("| ", StringComparison.Ordinal) && char.IsDigit(line[2])).ToList();
        Assert.Equal(10, rows.Count);
        Assert.StartsWith("| 16 |", rows[0], StringComparison.Ordinal); // the newest ten, oldest of them first
        Assert.StartsWith("| 25 |", rows[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void TheSessionLogHoldsTheNewestTwoHundredLinesInOneFence()
    {
        var lines = Enumerable.Range(1, 500).Select(i => $"{{\"line\":{i}}}").ToList();
        var markdown = ReportMarkdown.Build(FixedReport() with { LogLines = lines });
        var fenced = markdown[(markdown.IndexOf("```jsonl", StringComparison.Ordinal) + "```jsonl\n".Length)..];
        fenced = fenced[..fenced.IndexOf("\n```", StringComparison.Ordinal)];
        var shown = fenced.Split('\n');
        Assert.Equal(ReportBounds.LogLines, shown.Length);
        Assert.Equal("{\"line\":301}", shown[0]);
        Assert.Equal("{\"line\":500}", shown[^1]);
    }

    [Fact]
    public void AFenceInThePlayersLogIsNeverClosedEarly()
    {
        var markdown = ReportMarkdown.Build(FixedReport() with { LogLines = ["{\"text\":\"```\"}"] });
        Assert.Contains("````jsonl\n{\"text\":\"```\"}\n````\n", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void AStatePipeOrNewLineDoesNotBreakTheTable()
    {
        var state = FixedReport().State with { Objective = "a | b\nc" };
        var markdown = ReportMarkdown.Build(FixedReport() with { State = state });
        Assert.Contains("| Objective | a \\| b c |", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNamesFollowTheSessionTheTickAndTheKind()
    {
        Assert.Equal("report-s1-t4681-bug", ReportNaming.Stem(1, 4681, ReportKind.Bug));
        Assert.Equal("report-s3-t120-feature", ReportNaming.Stem(3, 120, ReportKind.Feature));
        Assert.Equal("report-s1-t4681-bug-2", ReportNaming.Stem(1, 4681, ReportKind.Bug, 2));
    }

    [Fact]
    public void ACounterIsAddedOnlyWhenTheNameIsTaken()
    {
        Assert.Equal("report-s1-t4681-bug", ReportNaming.Choose(1, 4681, ReportKind.Bug, _ => false));
        Assert.Equal("report-s1-t4681-bug-2", ReportNaming.Choose(1, 4681, ReportKind.Bug, name => name == "report-s1-t4681-bug.md"));
        Assert.Equal("report-s1-t4681-bug-2", ReportNaming.Choose(1, 4681, ReportKind.Bug, name => name == "report-s1-t4681-bug.png"));
        var taken = new HashSet<string> { "report-s1-t4681-bug.md", "report-s1-t4681-bug-2.png", "report-s1-t4681-bug-3.md" };
        Assert.Equal("report-s1-t4681-bug-4", ReportNaming.Choose(1, 4681, ReportKind.Bug, taken.Contains));
        Assert.Equal("report-s1-t4681-feature", ReportNaming.Choose(1, 4681, ReportKind.Feature, taken.Contains));
    }

    [Fact]
    public void TheNamingGivesUpAfterTheLastCounter()
    {
        Assert.Throws<InvalidOperationException>(() => ReportNaming.Choose(1, 1, ReportKind.Bug, _ => true));
    }

    [Fact]
    public void TheKindsAreNamedInEnglishAndReadFromTheCommandLineWord()
    {
        Assert.Equal("bug", ReportKinds.Word(ReportKind.Bug));
        Assert.Equal("feature", ReportKinds.Word(ReportKind.Feature));
        Assert.Equal("Bug", ReportKinds.Label(ReportKind.Bug));
        Assert.Equal("Feature request", ReportKinds.Label(ReportKind.Feature));
        Assert.True(ReportKinds.TryParse("Feature", out var kind));
        Assert.Equal(ReportKind.Feature, kind);
        Assert.False(ReportKinds.TryParse("idea", out _));
    }

    private static ReportSnapshot FixedReport() => new(
        ReportKind.Bug,
        "The bridge is missing\nIt should be by the river.",
        2,
        4681,
        FixedSavedAt,
        "report-s2-t4681-bug.png",
        new ReportGameState(
            BuildCommit: "abc1234",
            BundleId: "game-v1",
            BundleHash: "0123abcd",
            Seed: 42,
            X: 230.5,
            FeetY: 12.0,
            Z: 221.5,
            YawDegrees: 90.0,
            PitchDegrees: -5.0,
            Objective: "Campamento: Wood 2/5 · Berries 0/3",
            ObjectiveSteps: [("Wood 2/5", false), ("Berries 0/3", false)],
            Lesson: "first-words, step 3 of 9",
            OpenPanel: "none (the world)",
            Quality: "high",
            Greyscale: false,
            Speech: "models found, speech ready",
            Microphone: "found"),
        [
            new InputEntry(2, 2, 0, InputSource.Typed, "agua", "guide", "guide-open", InputPlace.Dialogue, null, "ask_water", true, false, [], []),
            new InputEntry(
                1,
                2,
                0,
                InputSource.Voice,
                "¿Dónde | está?",
                null,
                "first-words",
                InputPlace.Lesson,
                "first-words/answer",
                "ask_where",
                false,
                true,
                [new GuideError("gender", "el", "la", new TextSpan(0, 2))],
                []),
        ],
        ["{\"type\":\"session_start\"}", "{\"type\":\"turn\"}"]);

    private static InputEntry Turn(int id) => new(id, 1, 0, InputSource.Typed, $"line {id}", "guide", "guide-open", InputPlace.Dialogue, null, "other", true, false, [], []);
}
