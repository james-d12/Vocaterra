using System.Globalization;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Reporting;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Startup;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Scripted;

/// <summary>The window's sizes a screenshot checks before it is taken (<see cref="ScreenshotGate"/>): what the game asked for and what the window has.</summary>
/// <param name="PreferredWidth">The back buffer's width the game asked for.</param>
/// <param name="PreferredHeight">The back buffer's height the game asked for.</param>
/// <param name="ClientWidth">The window's client width now.</param>
/// <param name="ClientHeight">The window's client height now.</param>
public readonly record struct WindowSizes(int PreferredWidth, int PreferredHeight, int ClientWidth, int ClientHeight);

/// <summary>
/// The end of a scripted (<c>--screenshot</c>) run: it settles once the world's script is done and nothing waits (or a check battery rests or
/// shows its thank-you screen), opens the panels the run asked for at the end (<c>--journal</c>, <c>--inventory</c>, <c>--crafting</c>), takes
/// the <c>--shot</c> pictures along the way and, once the window has its size, the final picture with the run's summary, and exits.
/// </summary>
public sealed class ScriptedRunEnd
{
    /// <summary>The ticks a run must stay settled before its final picture.</summary>
    public const int SettleTicks = 30;

    /// <summary>The tick at which a run that never settles takes its picture anyway (five minutes of game time).</summary>
    public const int RunawayTicks = 60 * 60 * 5;

    /// <summary>The longest, in milliseconds of the wall clock, that a settled picture waits for the far tiles to reach the card.</summary>
    public const double UploadWaitLimitMilliseconds = 120_000;

    private readonly LaunchOptions _options;
    private readonly VoxelSession _session;
    private readonly CheckBattery _checks;
    private readonly ScenarioScript? _scenario;
    private readonly DialogueModel _dialogue;
    private readonly MicCheckModel? _micCheck;
    private readonly ShotRequest _shots;
    private readonly (Action Book, Action Inventory, Action Crafting) _openAtEnd;
    private readonly Action<MenuPage, int>? _openMenu;
    private readonly Action _exit;
    private readonly Action<string> _console;
    private readonly Action<long, string>? _logRunaway;
    private bool _runaway;
    private int _settled;
    private double? _uploadWaitStart;
    private bool _shown;
    private int _sizeFrames;
    private int _framesAtSize;
    private bool _sizeWarned;

    /// <summary>Creates the end of a run.</summary>
    /// <param name="options">The run's options: the screenshot, the frames, the panels at the end.</param>
    /// <param name="session">The session.</param>
    /// <param name="checks">The check battery.</param>
    /// <param name="scenario">The world's script, or <see langword="null"/>.</param>
    /// <param name="dialogue">The dialogue panel (a line about to be sent is waited for).</param>
    /// <param name="micCheck">The mic check, or <see langword="null"/> (one that waits for a recording is waited for).</param>
    /// <param name="shots">The <c>--shot</c> steps' waiting picture.</param>
    /// <param name="openAtEnd">Open the book, the inventory and the crafting panel.</param>
    /// <param name="exit">Ends the game.</param>
    /// <param name="console">Where the run's lines go.</param>
    /// <param name="logRunaway">Where a run cut short at <see cref="RunawayTicks"/> says so besides the console (the session log: the tick and the line), or <see langword="null"/>.</param>
    public ScriptedRunEnd(
        LaunchOptions options,
        VoxelSession session,
        CheckBattery checks,
        ScenarioScript? scenario,
        DialogueModel dialogue,
        MicCheckModel? micCheck,
        ShotRequest shots,
        (Action Book, Action Inventory, Action Crafting) openAtEnd,
        Action exit,
        Action<string> console,
        Action<long, string>? logRunaway = null,
        Action<MenuPage, int>? openMenu = null)
    {
        _options = options;
        _session = session;
        _checks = checks;
        _scenario = scenario;
        _dialogue = dialogue;
        _micCheck = micCheck;
        _shots = shots;
        _openAtEnd = openAtEnd;
        _exit = exit;
        _console = console;
        _logRunaway = logRunaway;
        _openMenu = openMenu;
    }

    /// <summary>The exit code of a run cut short at <see cref="RunawayTicks"/> whose picture was saved: the script did not finish, so the capture is not the one asked for (V26).</summary>
    public const int RunawayExitCode = 2;

    /// <summary>The process exit code: 0 unless a picture could not be saved (1), or the run was cut short at <see cref="RunawayTicks"/> (<see cref="RunawayExitCode"/>).</summary>
    public int ExitCode { get; private set; }

    /// <summary>The exit code after one more picture: the worst code wins, so a mid-run shot that failed (1) is not wiped by a final picture that saved (V30).</summary>
    public static int WorstExit(int current, bool saved, bool runaway) =>
        Math.Max(current, saved ? (runaway ? RunawayExitCode : 0) : 1);

    /// <summary>
    /// Once a frame, after the world: counts the frames the run has been settled, and opens the panels asked for at the end once the world's script
    /// is done. Settled: the world's script is done (or there is none) with no battery on screen, a battery that ends the run has shown its end,
    /// or a battery's script rests. A pre-check is not an end: the world's script after it still has steps to take (S12).
    /// </summary>
    public void AfterUpdate()
    {
        var scriptDone = _scenario is null || _scenario.IsDone;
        if (_checks.Screen is null && scriptDone || _checks.Done && _checks.EndsRun || _checks.ScriptRests && _checks.Screen is { IsDone: false })
        {
            _settled++;
        }

        if (_checks.Screen is null && scriptDone && !_shown)
        {
            _shown = true;
            if (_options.Journal)
            {
                _openAtEnd.Book();
            }

            if (_options.ShowInventory)
            {
                _openAtEnd.Inventory();
            }

            if (_options.ShowCrafting)
            {
                _openAtEnd.Crafting();
            }

            if (_options.ShowMenu is { } page)
            {
                _openMenu?.Invoke(page, _options.MenuScroll);
            }
        }
    }

    /// <summary>
    /// After a frame is drawn: a picture is taken at the size the game asked for (a window's first frames can be drawn before its resize), first a
    /// waiting <c>--shot</c>, then the final picture once the run has settled, with the run's summary, and the game exits.
    /// </summary>
    /// <param name="device">The graphics device the picture is read from.</param>
    /// <param name="width">The back buffer's width.</param>
    /// <param name="height">The back buffer's height.</param>
    /// <param name="sizes">What the game asked for and what the window has.</param>
    /// <param name="timings">The frames' timings, for the summary.</param>
    /// <param name="renderer">The renderer, for the summary.</param>
    /// <param name="preset">The quality preset, for the summary.</param>
    /// <param name="streaming">The streamed world's host, for its counts (B07), or <see langword="null"/> in the bounded valley.</param>
    public void AfterDraw(GraphicsDevice device, int width, int height, WindowSizes sizes, FrameTimings timings, VoxelRenderer renderer, QualityPreset preset, StreamedWorldHost? streaming = null, (BusyFrameStats Streaming, BusyFrameStats Remesh)? phases = null, (long Bytes, long Frames, double ProcessMegabytesPerSecond)? allocations = null)
    {
        var sim = _session.Simulation;
        var sized = !_options.IsScreenshotRun || ScreenshotGate.IsFinalSize(width, height, sizes.PreferredWidth, sizes.PreferredHeight, sizes.ClientWidth, sizes.ClientHeight);
        if (!sized)
        {
            _sizeFrames++;
            if (!_sizeWarned && ScreenshotGate.MayShoot(false, _sizeFrames))
            {
                _sizeWarned = true;
                _console($"screenshot: the window stayed {width} x {height} (wanted {sizes.PreferredWidth} x {sizes.PreferredHeight}) for {_sizeFrames} frames; the picture is taken at this size");
            }
        }

        _framesAtSize = sized ? _framesAtSize + 1 : 0;
        var mayShoot = ScreenshotGate.MayShoot(sized, _sizeFrames, _framesAtSize);
        // A --shot waits as the final picture does until every far tile it lists is on the card (B07f: the shot of a step after a teleport was taken in the first frame, before the card had the far land, and showed a void under a high eye).
        if (mayShoot && FarOnTheCard(streaming) && _shots.Take() is { } shot)
        {
            var saved = ScreenshotWriter.TrySave(device, shot);
            _console($"shot: {shot} ({(saved ? "saved" : "FAILED")}) at tick {sim.Tick}, {_session.Objective.Summary}");
            ExitCode = WorstExit(ExitCode, saved, false);
        }

        if (!mayShoot || _options.ScreenshotPath is not { } path || !ReadyForPicture(streaming))
        {
            return;
        }

        ExitCode = WorstExit(ExitCode, ScreenshotWriter.TrySave(device, path), _runaway);
        _console($"scene draw (voxel world, layers, flush): {timings.DrawSummary()}");
        _console($"frame time (end of one draw to the next, no vsync): {timings.FrameSummary()}");
        if (streaming is not null && _options.StreamStats)
        {
            _console(StreamLine(renderer, streaming, timings, phases, allocations));
        }

        if (_options.StreamStats)
        {
            var chunks = renderer.ChunkPassTimes;
            _console(string.Create(CultureInfo.InvariantCulture, $"chunk passes ({(renderer.GlPass is null ? "effects" : "GLSL")}, processor time to submit): {chunks.Count} frames, median {chunks.Median:0.00} ms, p95 {chunks.Percentile(95):0.00} ms, p99 {chunks.Percentile(99):0.00} ms, longest {chunks.Longest:0.00} ms"));
            if (renderer.CardTimes is { } card)
            {
                _console(string.Create(CultureInfo.InvariantCulture, $"chunk passes ({(renderer.GlPass is null ? "effects" : "GLSL")}, the card's time, GL timer query): {card.Count} frames, median {card.Median:0.00} ms, p95 {card.Percentile(95):0.00} ms, p99 {card.Percentile(99):0.00} ms, longest {card.Longest:0.00} ms"));
            }
        }

        if (renderer.GlPass is { } glPass && (_options.StreamStats || glPass.Errors.Count > 0)) // quiet by default: the compared output of a run does not hold it
        {
            var (errors, first) = glPass.Errors;
            _console($"gl: the last frame's scope drew {glPass.LastPass.Quads} quads in {glPass.LastPass.DrawCalls} draw calls with {glPass.LastPass.StateChanges} state sets; {errors} GL error(s) cleared{(errors > 0 ? $" (first {first})" : string.Empty)}");
        }

        // A streamed world's counts of chunks and meshes depend on how it was loaded, not on what the run did, so they have a line of their own (--stream-stats); the compared line keeps what the picture shows.
        _console($"voxel: {(renderer.IsStreamed ? string.Empty : renderer.Stats.Summary() + "; ")}{renderer.DrawnChunks} chunks drawn this frame, {renderer.DrawnFigureQuads} figure quads, {renderer.DrawnPlantQuads} plant quads (within {preset.PlantDistance} blocks, {preset.PlantKeepPercent}% built), occlusion {(preset.AmbientOcclusion ? "on" : "off")}, clouds {(preset.Clouds ? "on" : "off")}{(renderer.GrassPercent > 0 ? $", grass {renderer.DrawnGrassQuads} quads (slider {renderer.GrassPercent}%, within {preset.GrassDistance} blocks, cap {preset.GrassCapPercent}%; {renderer.GrassBuilds.Built} layers built in {renderer.GrassBuilds.Milliseconds:0} ms, {renderer.GrassQuadsBuilt} quads held)" : string.Empty)}{(renderer.DrawnRainVertices > 0 ? $", rain {renderer.DrawnRainVertices} vertices (budget {preset.RainDrops} drops)" : string.Empty)}");
        var body = sim.Player;
        _console(string.Create(CultureInfo.InvariantCulture, $"player: ({body.CenterX / (double)VoxelPhysics.UnitsPerBlock:0.00}, {body.FeetY / (double)VoxelPhysics.UnitsPerBlock:0.00}, {body.CenterZ / (double)VoxelPhysics.UnitsPerBlock:0.00}), tick {sim.Tick}, wood {sim.Inventory.Count(ItemId.Wood)}, berries {sim.Inventory.Count(ItemId.Berry)}, bottle {sim.Inventory.Count(ItemId.Bottle)}, water {sim.Inventory.Count(ItemId.WaterBottle)}, bread {sim.Inventory.Count(ItemId.Bread)}"));
        _console($"objective: {_session.Objective.Summary}");
        _exit();
    }

    private static string StreamLine(VoxelRenderer renderer, StreamedWorldHost streaming, FrameTimings timings, (BusyFrameStats Streaming, BusyFrameStats Remesh)? phases, (long Bytes, long Frames, double ProcessMegabytesPerSecond)? allocations)
    {
        var residency = streaming.Residency.Stats;
        var far = streaming.Far.Stats;
        var allocation = allocations is { Frames: > 0 } a ? $"; allocation: streaming and meshing phases {a.Bytes / 1024.0 / a.Frames:0.0} KB a frame on the game thread ({a.Bytes / (1024.0 * 1024.0):0.0} MB in {a.Frames} frames), whole process {a.ProcessMegabytesPerSecond:0.00} MB/s, collections gen 0/1/2: {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}, {GC.GetTotalPauseDuration().TotalMilliseconds:0} ms paused" : string.Empty;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"stream: {renderer.Stats.Summary()}; resident {residency.Resident} chunks ({residency.PlannedColumns} columns planned, {residency.Installed} installed, {residency.Evicted} evicted, {residency.Dropped} dropped, {residency.Plans} plans, {residency.GenerateMilliseconds:0} ms generating); far view to {renderer.FarRange} blocks: {far.Cached} tiles cached ({streaming.Far.Failed} failed, {streaming.Far.Unuploaded} not on the card), {far.Drawn} listed, {renderer.DrawnFarTiles} drawn, {far.Triangles} triangles, {far.Built} built in {far.BuildMilliseconds:0} ms (longest {far.LongestBuildMilliseconds:0.0} ms), {far.Bytes / (1024.0 * 1024.0):0.0} MB; by level [{streaming.Far.LevelSummary}]; lod: {streaming.LodSections} sections ({streaming.LodBytes / (1024.0 * 1024.0):0.0} MB packed), ring {streaming.Ring.Done} footprints real ({streaming.Ring.Pending} pending), {streaming.EditedChunksTaken} edited chunks taken in; store [{streaming.LodStoreSummary}]; settles [{string.Join(", ", streaming.SettleMilliseconds.Select(static ms => ms.ToString("0", System.Globalization.CultureInfo.InvariantCulture)))}] ms; process {Environment.WorkingSet / (1024.0 * 1024.0):0} MB working set, {GC.GetTotalMemory(false) / (1024.0 * 1024.0):0} MB managed; {streaming.Pool.Workers} worker thread(s), {streaming.Pool.Completed} jobs; frames p50 {timings.Frames.Median:0.0} ms, p95 {timings.Frames.Percentile(95):0.0} ms, p99 {timings.Frames.Percentile(99):0.0} ms, {timings.Frames.CountOver(16.7)} over 16.7 ms and {timings.Frames.CountOver(33.4)} over 33.4 ms of {timings.Frames.Count}; worst frames (tick:ms) [{timings.WorstFrames()}]; streaming update p50 {phases?.Streaming.Median:0.00} p99 {phases?.Streaming.Percentile(99):0.0} max {phases?.Streaming.Longest:0.0} ms, remesh p50 {phases?.Remesh.Median:0.00} p99 {phases?.Remesh.Percentile(99):0.0} max {phases?.Remesh.Longest:0.0} ms{allocation}; {renderer.MeshingSummary}; {renderer.ChunksGeneratedByReads} chunks generated by reads on the game thread");
    }

    // The final picture waits for the frames asked for, a settled run, the models, no turn on the worker and none about to be sent, and a mic check
    // that is not waiting for its recording; a battery that rests is ready too, and a runaway run takes its picture anyway.
    // A settled picture waits until every far tile it lists is on the card, but never for ever: after UploadWaitLimitMilliseconds of the wall clock it is taken with what is there (the run's stream line says how many were not).
    private bool FarOnTheCard(StreamedWorldHost? streaming)
    {
        if (streaming is null || streaming.Far.Unuploaded == 0)
        {
            _uploadWaitStart = null;
            return true;
        }

        _uploadWaitStart ??= MonotonicClock.Milliseconds();
        return MonotonicClock.Milliseconds() - _uploadWaitStart > UploadWaitLimitMilliseconds;
    }

    private bool ReadyForPicture(StreamedWorldHost? streaming)
    {
        var tick = _session.Simulation.Tick;
        return (tick >= _options.Frames && _settled >= SettleTicks && _session.Worker!.IsReady && _session.PendingTurns == 0 && !_dialogue.SubmitRequested && _dialogue.PendingSpeech is null
                && _micCheck is not { Phase: MicCheckPhase.Ready or MicCheckPhase.Listening or MicCheckPhase.Thinking }
                && FarOnTheCard(streaming)) // a settled picture waits until every far tile it lists is on the card
            || (_checks.RestsForShot && _settled >= SettleTicks)
            || RunawayAt(tick);
    }

    // A run that never settles takes its picture anyway at RunawayTicks, and says so once (V26): it had dropped the steps after the tick without a word.
    private bool RunawayAt(long tick)
    {
        if (tick <= RunawayTicks)
        {
            return false;
        }

        if (!_runaway)
        {
            _runaway = true;
            var line = RunawayLine(tick, _scenario?.StepsLeft ?? 0, _scenario?.NextStep, _scenario is { AtStepBoundary: false });
            _console(line);
            _logRunaway?.Invoke(tick, line);
        }

        return true;
    }

    /// <summary>The line a run cut short at <see cref="RunawayTicks"/> prints and logs: the tick, how many steps were left and the first of them.</summary>
    /// <param name="tick">The tick the run stopped at.</param>
    /// <param name="stepsLeft">The steps the script had not started.</param>
    /// <param name="next">The first of them, or <see langword="null"/> when none was left.</param>
    /// <param name="stepInProgress">Whether a step had started and was not done (a walk that never arrived, a line not yet answered).</param>
    public static string RunawayLine(long tick, int stepsLeft, ScriptStep? next, bool stepInProgress)
    {
        var left = stepsLeft == 0
            ? "no step was left to start"
            : string.Create(CultureInfo.InvariantCulture, $"{stepsLeft} step{(stepsLeft == 1 ? string.Empty : "s")} not started, the first: {Describe(next)}");
        return string.Create(CultureInfo.InvariantCulture, $"scripted run stopped at tick {tick} (the limit of {RunawayTicks}, five minutes of play): {left}{(stepInProgress ? "; a step was in progress" : string.Empty)}; exit {RunawayExitCode}");
    }

    private static string Describe(ScriptStep? step) =>
        step is null ? "none" : step.Kind == ScriptStepKind.Move ? $"{step.Step}" : string.IsNullOrEmpty(step.Value) ? $"{step.Kind}" : $"{step.Kind} \"{step.Value}\"";
}
