using System.Globalization;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Reporting;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Sound;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Input;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Report;

/// <summary>
/// The bug and feature report (S21, D-game-17), local files only: F8 (or a scripted <c>--report</c>) pauses the game, the next frame is kept in
/// memory as the picture the player saw, and the panel takes the keyboard. Ctrl+Enter (or the Save button) writes the Markdown and the PNG to the
/// reports folder; Esc keeps nothing.
/// </summary>
public sealed class ReportScreen
{
    /// <summary>The folder of the session folder that holds the reports; <c>--report-dir</c> replaces it.</summary>
    public const string ReportsFolder = "reports";

    private static readonly (Keys Key, ReportKey Report)[] MoveKeys = [(Keys.Up, ReportKey.Up), (Keys.Down, ReportKey.Down), (Keys.Home, ReportKey.Home), (Keys.End, ReportKey.End)];

    private readonly VoxelSession _session;
    private readonly ReportKind _kind;
    private readonly string? _reportDirectory;
    private readonly Func<string> _sessionDirectory;
    private readonly Func<string, ReportGameState> _state;
    private readonly Func<GraphicsDevice> _device;
    private readonly UiCanvas _canvas;
    private readonly Func<SpriteFontBase> _font;
    private readonly ToastQueue _toasts;
    private readonly SoundFrontend _sound;
    private readonly TextEditKeys _textEdits;
    private readonly Action<string> _log;
    private readonly Action<string> _error;
    private (Color[] Pixels, int Width, int Height)? _picture;
    private long _tick;
    private int _playSession;
    private (string Text, bool Keep)? _scripted;

    /// <summary>Creates the report of a session.</summary>
    /// <param name="panel">The panel's state, which the layer draws and the game's pause reads.</param>
    /// <param name="session">The session the report describes.</param>
    /// <param name="kind">The kind the panel opens on (<c>--report-kind</c>).</param>
    /// <param name="reportDirectory">Where the reports go (<c>--report-dir</c>), or <see langword="null"/> for the session folder's <see cref="ReportsFolder"/>.</param>
    /// <param name="sessionDirectory">The session folder (its log's last lines go in the report).</param>
    /// <param name="state">The game's state for the report's table, given what was open.</param>
    /// <param name="device">The graphics device the picture is read from and written with.</param>
    /// <param name="canvas">The interface canvas the panel is laid out on.</param>
    /// <param name="font">The panel's font.</param>
    /// <param name="toasts">Where "Report saved" goes.</param>
    /// <param name="sound">Plays the saved cue.</param>
    /// <param name="textEdits">The repeats of the editing keys, shared with the dialogue's text box.</param>
    /// <param name="log">Where a line about the report goes (the console).</param>
    /// <param name="error">Where a report that could not be written is said.</param>
    public ReportScreen(
        ReportPanel panel,
        VoxelSession session,
        ReportKind kind,
        string? reportDirectory,
        Func<string> sessionDirectory,
        Func<string, ReportGameState> state,
        Func<GraphicsDevice> device,
        UiCanvas canvas,
        Func<SpriteFontBase> font,
        ToastQueue toasts,
        SoundFrontend sound,
        TextEditKeys textEdits,
        Action<string> log,
        Action<string> error)
    {
        Panel = panel;
        _session = session;
        _kind = kind;
        _reportDirectory = reportDirectory;
        _sessionDirectory = sessionDirectory;
        _state = state;
        _device = device;
        _canvas = canvas;
        _font = font;
        _toasts = toasts;
        _sound = sound;
        _textEdits = textEdits;
        _log = log;
        _error = error;
    }

    /// <summary>The panel's state, which the layer draws.</summary>
    public ReportPanel Panel { get; }

    /// <summary>F8: the game pauses and the next frame is kept as the picture the player saw.</summary>
    /// <param name="openScreen">What is open now, in the words of the report's table.</param>
    public void Request(string openScreen)
    {
        Panel.Request(openScreen);
        _log($"report: F8 at tick {_session.Simulation.Tick} ({Panel.OpenPanel})");
    }

    /// <summary>A scripted report (<c>--report</c>, <c>--report-open</c>): the panel opens at the next frame and the text is typed there (<see cref="DriveScripted"/>).</summary>
    public void RequestScripted(string text, bool keep, string openScreen)
    {
        Panel.Request(openScreen);
        _scripted = (text, keep);
        _log($"report: scripted, {(keep ? "the panel is left open" : "saved")} ({Panel.OpenPanel})");
    }

    /// <summary>After a frame is drawn: the frame of F8, drawn without the panel, is kept in memory until the report is saved or cancelled, and the panel opens.</summary>
    public void TakePictureIfRequested()
    {
        if (!Panel.Requested)
        {
            return;
        }

        _picture = ScreenshotWriter.Capture(_device());
        _tick = _session.Simulation.Tick;
        _playSession = _session.PlaySession;
        Panel.Show(_kind);
    }

    /// <summary>Once a frame: the scripted report's text, typed once the panel is up; a report that is not left open is saved at once.</summary>
    public void DriveScripted()
    {
        if (_scripted is not { } script || !Panel.IsOpen)
        {
            return;
        }

        _scripted = null;
        Panel.TypeText(script.Text);
        if (!script.Keep)
        {
            Save();
        }
    }

    /// <summary>
    /// The panel's keys and clicks while it is open: Ctrl+Enter saves, Esc cancels (nothing is kept), Tab switches Bug and Feature request, and the
    /// keys edit the text.
    /// </summary>
    public void HandleInput(InputSnapshot input)
    {
        var edits = _textEdits.Step(input);
        if (edits.Backspace)
        {
            _ = Panel.Press(ReportKey.Backspace);
        }

        if (edits.Delete)
        {
            _ = Panel.Press(ReportKey.Delete);
        }

        if (edits.Left)
        {
            _ = Panel.Press(ReportKey.Left);
        }

        if (edits.Right)
        {
            _ = Panel.Press(ReportKey.Right);
        }

        foreach (var (key, reportKey) in MoveKeys)
        {
            if (input.Pressed(key))
            {
                _ = Panel.Press(reportKey);
            }
        }

        var outcome = ReportOutcome.None;
        if (input.Pressed(Keys.Escape))
        {
            outcome = Panel.Press(ReportKey.Escape);
        }
        else if (input.Pressed(Keys.Enter))
        {
            outcome = Panel.Press(input.Ctrl ? ReportKey.Save : ReportKey.Enter);
        }
        else if (input.Pressed(Keys.Tab))
        {
            _ = Panel.Press(ReportKey.Tab);
        }

        if (outcome == ReportOutcome.None && input.LeftClicked
            && ReportLayer.LayoutFor(_canvas.Width, _canvas.Height, _font(), _session.Locale).ButtonAt(input.MouseState.X, input.MouseState.Y) is { } button)
        {
            outcome = Panel.Click(button);
        }

        switch (outcome)
        {
            case ReportOutcome.Save:
                Save();
                break;
            case ReportOutcome.Cancel:
                Cancel();
                break;
        }
    }

    /// <summary>A letter of the text box: every printable character, with the accents of Alt (the control characters are keys).</summary>
    public void Type(char character, bool alt)
    {
        if (!char.IsControl(character))
        {
            Panel.Type(character, alt);
        }
    }

    // Esc: the report is dropped with its picture, and nothing is written.
    private void Cancel()
    {
        Panel.Hide();
        _picture = null;
        _log("report: cancelled; nothing was kept");
    }

    // Ctrl+Enter (or the Save button): the Markdown and the PNG are written to the reports folder, named by the session, the tick and the kind. A
    // write that fails keeps the panel open, with the reason on a toast.
    private void Save()
    {
        if (_picture is not { } shot)
        {
            Panel.Hide();
            return;
        }

        var kind = Panel.Kind;
        var directory = _reportDirectory ?? Path.Combine(_sessionDirectory(), ReportsFolder);
        try
        {
            Directory.CreateDirectory(directory);
            var stem = ReportNaming.Choose(_playSession, _tick, kind, name => File.Exists(Path.Combine(directory, name)));
            var picture = stem + ".png";
            ScreenshotWriter.SaveImage(_device(), shot.Pixels, shot.Width, shot.Height, Path.Combine(directory, picture));
            var report = new ReportSnapshot(
                kind,
                Panel.Text,
                _playSession,
                _tick,
                WallClock.LocalNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                picture,
                _state(Panel.OpenPanel),
                _session.Learner.Recent(ReportBounds.RecentTurns),
                SessionLogTail());
            var markdownPath = Path.GetFullPath(Path.Combine(directory, stem + ".md"));
            File.WriteAllText(markdownPath, ReportMarkdown.Build(report), new System.Text.UTF8Encoding(false));
            Panel.Hide();
            _picture = null;
            _toasts.Show(_session.Locale.Ui("reportSaved", ("path", markdownPath)));
            _sound.Queue(SoundCue.ReportSaved);
            _log($"report saved: {markdownPath} and {picture} ({ReportKinds.Word(kind)}, panel: {report.State.OpenPanel})");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or NotSupportedException)
        {
            _toasts.Show(_session.Locale.Ui("reportNotSaved", ("problem", ex.Message)));
            _error($"report not saved: {ex.Message}");
        }
    }

    // The newest lines of this folder's session log (the file is flushed a line at a time, so it is complete up to the last turn).
    private IReadOnlyList<string> SessionLogTail()
    {
        var path = Path.Combine(_sessionDirectory(), SessionLog.SessionFileName);
        return File.Exists(path) ? File.ReadLines(path).TakeLast(ReportBounds.LogLines).ToList() : Array.Empty<string>();
    }
}
