using Vocaterra.Game.Core.Language.Learner;

namespace Vocaterra.Game.Core.Reporting;

/// <summary>The bounds of a report (S21): the last ten turns and the last 200 lines of the session log.</summary>
public static class ReportBounds
{
    /// <summary>How many of the newest turns the report lists.</summary>
    public const int RecentTurns = 10;

    /// <summary>How many of the newest session log lines the report holds.</summary>
    public const int LogLines = 200;
}

/// <summary>
/// The game's state at the report, as the report's table shows it (S21). Every field is already a value the game can name; the Markdown only
/// formats it. Positions are in blocks; the angles are in degrees (yaw 0 north, pitch positive up).
/// </summary>
/// <param name="BuildCommit">The commit the build was made from, or <c>unknown</c>.</param>
/// <param name="BundleId">The content bundle's id (<c>game-v1</c>).</param>
/// <param name="BundleHash">The content bundle's hash.</param>
/// <param name="Seed">The world seed.</param>
/// <param name="X">The player's column, in blocks.</param>
/// <param name="FeetY">The height of the player's feet, in blocks.</param>
/// <param name="Z">The player's row, in blocks.</param>
/// <param name="YawDegrees">The view's yaw.</param>
/// <param name="PitchDegrees">The view's pitch.</param>
/// <param name="Objective">The camp objective's summary line.</param>
/// <param name="ObjectiveSteps">Each step of the objective: its text and whether it is done.</param>
/// <param name="Lesson">The lesson in play and its step, or <c>none</c>.</param>
/// <param name="OpenPanel">What was open when F8 was pressed (<c>none (the world)</c>, <c>dialogue with Guía</c>, <c>book</c>...).</param>
/// <param name="Quality">The graphics quality preset's name.</param>
/// <param name="Greyscale">Whether the greyscale A/B switch is on.</param>
/// <param name="Speech">The speech models' state (found, not loaded, off).</param>
/// <param name="Microphone">The microphone's state (found, not found, not used).</param>
/// <param name="Locale">The support-language locale and its bundle's hash (<c>en, hash ...</c>; CONTENT_PLAN C03), or <see langword="null"/> when not known.</param>
public sealed record ReportGameState(
    string BuildCommit,
    string BundleId,
    string BundleHash,
    ulong Seed,
    double X,
    double FeetY,
    double Z,
    double YawDegrees,
    double PitchDegrees,
    string Objective,
    IReadOnlyList<(string Text, bool Done)> ObjectiveSteps,
    string Lesson,
    string OpenPanel,
    string Quality,
    bool Greyscale,
    string Speech,
    string Microphone,
    string? Locale = null);

/// <summary>
/// One report, as the game has it when the player saves it (S21). The Markdown is built from this and nothing else.
/// </summary>
/// <param name="Kind">The kind the player chose.</param>
/// <param name="Text">The player's text, as typed (the title is its first line).</param>
/// <param name="Session">The play session number.</param>
/// <param name="Tick">The simulation tick of the screenshot.</param>
/// <param name="SavedAt">The local date and time the report was saved, formatted by the caller (the game reads the clock; the game layer does not).</param>
/// <param name="ScreenshotFile">The screenshot's file name, relative to the report (the link in the Markdown).</param>
/// <param name="State">The game's state.</param>
/// <param name="RecentTurns">The learner's newest lines, newest first (<c>LearnerRecord.Recent</c>); the report keeps <see cref="ReportBounds.RecentTurns"/>.</param>
/// <param name="LogLines">The session log's lines, oldest first; the report keeps the newest <see cref="ReportBounds.LogLines"/>.</param>
public sealed record ReportSnapshot(
    ReportKind Kind,
    string Text,
    int Session,
    long Tick,
    string SavedAt,
    string ScreenshotFile,
    ReportGameState State,
    IReadOnlyList<InputEntry> RecentTurns,
    IReadOnlyList<string> LogLines);
