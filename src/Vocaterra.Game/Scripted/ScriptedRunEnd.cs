using System.Globalization;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Scripted;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Graphics;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Startup;

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

    private readonly LaunchOptions _options;
    private readonly VoxelSession _session;
    private readonly CheckBattery _checks;
    private readonly ScenarioScript? _scenario;
    private readonly DialogueModel _dialogue;
    private readonly MicCheckModel? _micCheck;
    private readonly ShotRequest _shots;
    private readonly (Action Book, Action Inventory, Action Crafting) _openAtEnd;
    private readonly Action _exit;
    private readonly Action<string> _console;
    private int _settled;
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
        Action<string> console)
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
    }

    /// <summary>The process exit code: 0 unless a picture could not be saved.</summary>
    public int ExitCode { get; private set; }

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
    public void AfterDraw(GraphicsDevice device, int width, int height, WindowSizes sizes, FrameTimings timings, VoxelRenderer renderer, QualityPreset preset)
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
        if (mayShoot && _shots.Take() is { } shot)
        {
            var saved = ScreenshotWriter.TrySave(device, shot);
            _console($"shot: {shot} ({(saved ? "saved" : "FAILED")}) at tick {sim.Tick}, {_session.Objective.Summary}");
            ExitCode = saved ? ExitCode : 1;
        }

        if (!mayShoot || _options.ScreenshotPath is not { } path || !ReadyForPicture())
        {
            return;
        }

        ExitCode = ScreenshotWriter.TrySave(device, path) ? 0 : 1;
        _console($"scene draw (voxel world, layers, flush): {timings.DrawSummary()}");
        _console($"frame time (end of one draw to the next, no vsync): {timings.FrameSummary()}");
        _console($"voxel: {renderer.Stats.Summary()}; {renderer.DrawnChunks} chunks drawn this frame, {renderer.DrawnFigureQuads} figure quads, {renderer.DrawnPlantQuads} plant quads (within {preset.PlantDistance} blocks, {preset.PlantKeepPercent}% built), occlusion {(preset.AmbientOcclusion ? "on" : "off")}, clouds {(preset.Clouds ? "on" : "off")}");
        var body = sim.Player;
        _console(string.Create(CultureInfo.InvariantCulture, $"player: ({body.CenterX / (double)VoxelPhysics.UnitsPerBlock:0.00}, {body.FeetY / (double)VoxelPhysics.UnitsPerBlock:0.00}, {body.CenterZ / (double)VoxelPhysics.UnitsPerBlock:0.00}), tick {sim.Tick}, wood {sim.Inventory.Count(ItemId.Wood)}, berries {sim.Inventory.Count(ItemId.Berry)}, bottle {sim.Inventory.Count(ItemId.Bottle)}, water {sim.Inventory.Count(ItemId.WaterBottle)}, bread {sim.Inventory.Count(ItemId.Bread)}"));
        _console($"objective: {_session.Objective.Summary}");
        _exit();
    }

    // The final picture waits for the frames asked for, a settled run, the models, no turn on the worker and none about to be sent, and a mic check
    // that is not waiting for its recording; a battery that rests is ready too, and a runaway run takes its picture anyway.
    private bool ReadyForPicture()
    {
        var tick = _session.Simulation.Tick;
        return (tick >= _options.Frames && _settled >= SettleTicks && _session.Worker!.IsReady && _session.PendingTurns == 0 && !_dialogue.SubmitRequested && _dialogue.PendingSpeech is null
                && _micCheck is not { Phase: MicCheckPhase.Ready or MicCheckPhase.Listening or MicCheckPhase.Thinking })
            || tick > RunawayTicks
            || (_checks.RestsForShot && _settled >= SettleTicks);
    }
}
