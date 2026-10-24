using Vocaterra.Game.Reports;
using Vocaterra.MonoGame.UI;
using Vocaterra.MonoGame.Utilities;

namespace Vocaterra.MonoGame.Tests;

/// <summary>
/// The bug and feature report panel of S21 as plain logic: the keys (Esc cancels and keeps nothing, Tab switches the kind, Ctrl+Enter saves, Enter
/// is a new line), the clicks, the editing of the multi-line text, the Alt accents, the pause (no tick passes while the panel is open), the
/// geometry the mouse uses, and the launch options of the scripted report.
/// </summary>
public sealed class ReportPanelTests
{
    [Fact]
    public void EscCancelsAndKeepsNothing()
    {
        var panel = Shown();
        panel.TypeText("The bridge is missing");
        Assert.Equal(ReportOutcome.Cancel, panel.Press(ReportKey.Escape));
        panel.Hide(); // the game's answer to Cancel: the picture is dropped with the text
        Assert.False(panel.IsOpen);
        Assert.False(panel.Paused);
        Assert.Equal(string.Empty, panel.Text);
        Assert.Equal(ReportOutcome.None, panel.Press(ReportKey.Escape)); // a closed panel takes no key
    }

    [Fact]
    public void TabSwitchesBetweenBugAndFeatureRequest()
    {
        var panel = Shown();
        Assert.Equal(ReportKind.Bug, panel.Kind);
        Assert.Equal(ReportOutcome.None, panel.Press(ReportKey.Tab));
        Assert.Equal(ReportKind.Feature, panel.Kind);
        _ = panel.Press(ReportKey.Tab);
        Assert.Equal(ReportKind.Bug, panel.Kind);
    }

    [Fact]
    public void ASaveNeedsSomeTextAndSaysSoOtherwise()
    {
        var panel = Shown();
        Assert.Equal(ReportOutcome.None, panel.Press(ReportKey.Save));
        Assert.Equal(ReportPanel.BlankNotice, panel.Notice);
        panel.TypeText("   ");
        Assert.Equal(ReportOutcome.None, panel.Press(ReportKey.Save));
        panel.TypeText("a");
        Assert.Null(panel.Notice);
        Assert.Equal(ReportOutcome.Save, panel.Press(ReportKey.Save));
        Assert.Equal("a", panel.Text);
    }

    [Fact]
    public void ClicksPickTheKindSaveAndCancel()
    {
        var panel = Shown();
        Assert.Equal(ReportOutcome.None, panel.Click(ReportButton.Feature));
        Assert.Equal(ReportKind.Feature, panel.Kind);
        _ = panel.Click(ReportButton.Bug);
        Assert.Equal(ReportKind.Bug, panel.Kind);
        panel.TypeText("x");
        Assert.Equal(ReportOutcome.Save, panel.Click(ReportButton.Save));
        Assert.Equal(ReportOutcome.Cancel, panel.Click(ReportButton.Cancel));
    }

    [Fact]
    public void EnterStartsANewLineAndTheArrowsMoveByLine()
    {
        var panel = Shown();
        panel.TypeText("abc");
        _ = panel.Press(ReportKey.Enter);
        panel.TypeText("de");
        Assert.Equal("abc\nde", panel.Entry.Text);
        _ = panel.Press(ReportKey.Up);
        Assert.Equal(2, panel.Entry.Caret); // the same column (2) on the first line
        _ = panel.Press(ReportKey.Down);
        Assert.Equal(6, panel.Entry.Caret); // the end of the second
        _ = panel.Press(ReportKey.Home);
        Assert.Equal(4, panel.Entry.Caret);
        _ = panel.Press(ReportKey.End);
        Assert.Equal(6, panel.Entry.Caret);
    }

    [Fact]
    public void BackspaceAndDeleteEditAroundTheCaret()
    {
        var panel = Shown();
        panel.TypeText("abcd");
        _ = panel.Press(ReportKey.Left);
        _ = panel.Press(ReportKey.Backspace);
        Assert.Equal("abd", panel.Entry.Text);
        _ = panel.Press(ReportKey.Delete);
        Assert.Equal("ab", panel.Entry.Text);
        _ = panel.Press(ReportKey.Right); // at the end: nothing to move to
        Assert.Equal(2, panel.Entry.Caret);
    }

    [Fact]
    public void AltAndAVowelTypesTheAccentedLetter()
    {
        var panel = Shown();
        panel.Type('a', alt: true);
        panel.Type('E', alt: true);
        panel.Type('n', alt: true);
        panel.Type('/', alt: true);
        panel.Type('1', alt: true);
        panel.Type('a', alt: false);
        Assert.Equal("áÉñ¿¡a", panel.Entry.Text);
    }

    [Fact]
    public void TheTextIsCappedAtItsMaximum()
    {
        var entry = new ReportEntry(maxLength: 3);
        Assert.Equal(3, entry.Insert("abcdef"));
        Assert.Equal("abc", entry.Text);
        Assert.False(entry.Insert('d'));
    }

    [Fact]
    public void ANewLineIsKeptButAControlCharacterIsNot()
    {
        var entry = new ReportEntry();
        Assert.True(entry.Insert('\n'));
        Assert.False(entry.Insert('\t'));
        Assert.False(entry.Insert('\r'));
        Assert.Equal("\n", entry.Text);
    }

    [Fact]
    public void NoTickPassesWhileThePanelIsOpen()
    {
        var clock = new TickAccumulator(60, maxStepsPerFrame: 100);
        var panel = new ReportPanel();
        Assert.Equal(60, panel.Gate(clock.Advance(1.0))); // the world runs as usual with no panel

        panel.Request("none (the world)"); // F8: paused from here on
        Assert.Equal(0, panel.Gate(clock.Advance(1.0)));
        panel.Show(ReportKind.Bug); // the panel is up: still paused
        Assert.Equal(0, panel.Gate(clock.Advance(1.0)));
        panel.Hide();
        Assert.Equal(60, panel.Gate(clock.Advance(1.0)));
    }

    [Fact]
    public void ARequestPausesBeforeThePanelIsDrawn()
    {
        var panel = new ReportPanel();
        panel.Request("book");
        Assert.True(panel.Paused);
        Assert.True(panel.Requested);
        Assert.False(panel.IsOpen);
        Assert.Equal("book", panel.OpenPanel);
        panel.Show(ReportKind.Feature);
        Assert.True(panel.IsOpen);
        Assert.False(panel.Requested);
        Assert.Equal(ReportKind.Feature, panel.Kind);
    }

    [Fact]
    public void TheButtonsAreFoundWhereTheyAreDrawn()
    {
        var layout = new ReportLayout(1280, 720);
        Assert.Equal(ReportButton.Bug, layout.ButtonAt(layout.BugButton.X + 5, layout.BugButton.Y + 5));
        Assert.Equal(ReportButton.Feature, layout.ButtonAt(layout.FeatureButton.X + 5, layout.FeatureButton.Y + 5));
        Assert.Equal(ReportButton.Save, layout.ButtonAt(layout.SaveButton.X + 5, layout.SaveButton.Y + 5));
        Assert.Equal(ReportButton.Cancel, layout.ButtonAt(layout.CancelButton.X + 5, layout.CancelButton.Y + 5));
        Assert.Null(layout.ButtonAt(0, 0));
        Assert.True(layout.Panel.Contains(640, 360));
    }

    [Fact]
    public void TheTextRowsBreakAtASpaceAndKeepTheNewLines()
    {
        const string text = "aaa bbb ccc\ndd";
        var rows = ReportWrap.Rows(text, 7, static s => s.Length);
        Assert.All(rows, row => Assert.True(row.Length <= 7));
        Assert.Equal(12, rows[^1].Start); // the second line starts after the new line
        Assert.Equal("dd", text.Substring(rows[^1].Start, rows[^1].Length));
        Assert.Equal(rows.Count - 1, ReportWrap.RowOf(rows, 13));
        Assert.Equal(0, ReportWrap.RowOf(rows, 0));
    }

    [Fact]
    public void AnEmptyTextHasOneRow()
    {
        var rows = ReportWrap.Rows(string.Empty, 100, static s => s.Length);
        Assert.Equal((0, 0), Assert.Single(rows));
    }

    [Fact]
    public void AccentsAreTypedOnlyWithAlt()
    {
        Assert.Equal('é', AccentShortcuts.ForTyped('e', alt: true));
        Assert.Equal('e', AccentShortcuts.ForTyped('e', alt: false));
        Assert.Equal('x', AccentShortcuts.ForTyped('x', alt: true));
    }

    [Fact]
    public void TheScriptedReportOptionsParse()
    {
        var args = new[] { "--screenshot", "panel.png", "--report", "The bridge is missing", "--report-kind", "feature", "--report-dir", "reports-out" };
        Assert.True(LaunchOptions.TryParse(args, out var options, out var error), error);
        var step = Assert.Single(options.Script);
        Assert.Equal(ScriptStepKind.Report, step.Kind);
        Assert.Equal("The bridge is missing", step.Value);
        Assert.Equal(ReportKind.Feature, options.ReportKind);
        Assert.Equal("reports-out", options.ReportDir);
    }

    [Fact]
    public void AReportOptionNeedsTheScript()
    {
        Assert.False(LaunchOptions.TryParse(["--report", "x"], out _, out var error));
        Assert.Contains("--report", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportOpenMustBeTheLastStepAndTheKindMustBeKnown()
    {
        Assert.False(LaunchOptions.TryParse(["--screenshot", "a.png", "--report-open", "x", "--shot", "b.png"], out _, out var order));
        Assert.Contains("last step", order, StringComparison.Ordinal);
        Assert.True(LaunchOptions.TryParse(["--screenshot", "a.png", "--shot", "b.png", "--report-open", "x"], out _, out var fine), fine);
        Assert.False(LaunchOptions.TryParse(["--screenshot", "a.png", "--report-kind", "idea"], out _, out var kind));
        Assert.Contains("bug or feature", kind, StringComparison.Ordinal);
    }

    [Fact]
    public void TheBuildCommitIsStampedOrFromTheEnvironmentOrUnknown()
    {
        var plain = typeof(string).Assembly; // carries no BuildCommit metadata
        Assert.Equal("abc1234", BuildCommit.Read(plain, _ => "abc1234\n"));
        Assert.Equal(BuildCommit.Unknown, BuildCommit.Read(plain, _ => null));
        Assert.Equal(BuildCommit.Unknown, BuildCommit.Read(plain, _ => "  "));
    }

    private static ReportPanel Shown()
    {
        var panel = new ReportPanel();
        panel.Request("none (the world)");
        panel.Show(ReportKind.Bug);
        return panel;
    }
}
