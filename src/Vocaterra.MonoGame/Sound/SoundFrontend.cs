using Vocaterra.Game;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.MonoGame.Sound;

/// <summary>Which panels are open, as the sound side watches them (S24): the sound for a panel opening or closing, and the book's page turn.</summary>
/// <param name="Inventory">Whether the inventory is open.</param>
/// <param name="Crafting">Whether the crafting panel is open.</param>
/// <param name="Book">Whether the book is open.</param>
/// <param name="BookPage">The book's page (0 when closed).</param>
/// <param name="Dialogue">Whether the dialogue panel is open.</param>
public readonly record struct PanelState(bool Inventory, bool Crafting, bool Book, int BookPage, bool Dialogue);

/// <summary>
/// A recording the script makes without a microphone (S24): a scripted spoken line (<c>--wav</c>, <c>--speak</c>) and the scripted mic check stand for a push-to-talk. It
/// runs the same sequence a real one does (the start beep, the silent window, the stop beep) for a fixed number of ticks, so the sound log of a scripted run shows the
/// microphone rule at work. It never delays the line itself.
/// </summary>
public sealed class ScriptedRecording
{
    /// <summary>How many ticks a scripted recording lasts (0.4 s).</summary>
    public const int Ticks = 24;

    private readonly ITalkSounds _sounds;
    private int _left;

    /// <summary>Creates the scripted recording over the sound side.</summary>
    /// <param name="sounds">Where the start, the window and the stop are told.</param>
    public ScriptedRecording(ITalkSounds sounds) => _sounds = sounds ?? throw new ArgumentNullException(nameof(sounds));

    /// <summary>Whether the window is on.</summary>
    public bool Active => _left > 0;

    /// <summary>Starts a recording window (the start beep, then silence).</summary>
    public void Begin()
    {
        if (_left > 0)
        {
            return;
        }

        _ = _sounds.Starting();
        _sounds.Started();
        _left = Ticks;
    }

    /// <summary>One tick: the window closes (and the stop beep plays) when its ticks are up.</summary>
    public void Tick()
    {
        if (_left > 0 && --_left == 0)
        {
            _sounds.Stopped();
        }
    }
}

/// <summary>
/// The game's sound side (S24): the mixer, the event-to-cue mapping, and the cues of one frame gathered and folded before they play. The game feeds it the simulation's
/// events, the session's game events, the finished turns and the panels' state; <see cref="Flush"/> plays what the frame called for. It is engine free: the output is
/// whatever the mixer was given (the null output in a scripted run).
/// </summary>
public sealed class SoundFrontend
{
    private readonly List<SoundCue> _frame = [];
    private readonly Footsteps _steps = new();
    private readonly AmbientSound? _ambient;
    private PanelState _panels;
    private int _eventCursor;
    private bool _skipPageTurn;

    /// <summary>Creates the sound side.</summary>
    /// <param name="mixer">The mixer.</param>
    /// <param name="mapper">The event-to-cue mapping.</param>
    /// <param name="ambient">The ambient beds and their scheduled sounds (S25), or <see langword="null"/> for none.</param>
    public SoundFrontend(SoundMixer mixer, SoundEventMapper mapper, AmbientSound? ambient = null)
    {
        Mixer = mixer ?? throw new ArgumentNullException(nameof(mixer));
        Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        Scripted = new ScriptedRecording(mixer);
        _ambient = ambient;
    }

    /// <summary>The mixer.</summary>
    public SoundMixer Mixer { get; }

    /// <summary>The event-to-cue mapping.</summary>
    public SoundEventMapper Mapper { get; }

    /// <summary>The scripted recording window (a scripted spoken line, the scripted mic check).</summary>
    public ScriptedRecording Scripted { get; }

    /// <summary>Starts the game events' cursor at this many events (the events a loaded game already holds do not sound again).</summary>
    /// <param name="count">The number of events already there.</param>
    public void SkipEvents(int count) => _eventCursor = count;

    /// <summary>Queues a cue for this frame.</summary>
    /// <param name="cue">The cue.</param>
    public void Queue(SoundCue cue) => _frame.Add(cue);

    /// <summary>Queues a button's click, or the tab's tick when the tab also turns the book (so the page does not sound twice).</summary>
    /// <param name="cue">The click or the tab cue.</param>
    public void QueueClick(SoundCue cue)
    {
        _frame.Add(cue);
        if (cue == SoundCue.UiTab)
        {
            _skipPageTurn = true;
        }
    }

    /// <summary>Queues the cues of the simulation's events of the last tick.</summary>
    /// <param name="events">The events.</param>
    /// <param name="blockAt">Reads the block at a cell.</param>
    public void ObserveSimulation(IReadOnlyList<SimulationEvent> events, Func<int, int, int, BlockId> blockAt)
    {
        ArgumentNullException.ThrowIfNull(events);
        foreach (var e in events)
        {
            _frame.AddRange(Mapper.ForSimulation(e, blockAt));
        }
    }

    /// <summary>
    /// Once per tick (S25): the footsteps of the player's movement (a step on the surface underfoot, a splash in water, a jump and a landing), queued for this
    /// frame, and the ambient beds and their scheduled sounds for the player's place.
    /// </summary>
    /// <param name="tick">The tick.</param>
    /// <param name="events">The events of the tick.</param>
    /// <param name="player">The player's body.</param>
    /// <param name="blockAt">Reads the block at a cell.</param>
    /// <param name="fireLit">Whether the player's own fire is lit.</param>
    public void ObserveWorld(long tick, IReadOnlyList<SimulationEvent> events, PlayerBody3D player, Func<int, int, int, BlockId> blockAt, bool fireLit)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(player);
        var jumped = events.Any(static e => e.Kind == SimulationEventKind.PlayerJumped);
        var landed = events.Any(static e => e.Kind == SimulationEventKind.PlayerLanded);
        _frame.AddRange(_steps.Observe(Footsteps.StateOf(player, blockAt), jumped, landed));
        _ambient?.Observe(tick, new Listener(player.CenterX / (double)VoxelPhysics.UnitsPerBlock, player.MinY / (double)VoxelPhysics.UnitsPerBlock, player.CenterZ / (double)VoxelPhysics.UnitsPerBlock, player.Yaw), fireLit);
    }

    /// <summary>Queues the cues of the session's game events that came since the last call.</summary>
    /// <param name="events">The session's events (the list only grows).</param>
    public void ObserveGameEvents(IReadOnlyList<GameEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        for (; _eventCursor < events.Count; _eventCursor++)
        {
            _frame.AddRange(Mapper.ForGameEvent(events[_eventCursor]));
        }
    }

    /// <summary>Queues the cues of a finished turn.</summary>
    /// <param name="turn">The turn, or <see langword="null"/> when it failed.</param>
    /// <param name="words">The turn's word progress, or <see langword="null"/>.</param>
    /// <param name="isCheck">Whether it answered a check item.</param>
    public void ObserveTurn(GuideTurn? turn, WordTurnResult? words, bool isCheck = false) => _frame.AddRange(Mapper.ForTurn(turn, words, isCheck));

    /// <summary>Compares the panels with the last frame's and queues the open, close and page-turn cues.</summary>
    /// <param name="now">The panels now.</param>
    public void ObservePanels(PanelState now)
    {
        var before = _panels;
        _panels = now;
        Edge(before.Inventory, now.Inventory, SoundCue.InventoryOpen, SoundCue.InventoryClose);
        Edge(before.Crafting, now.Crafting, SoundCue.CraftingOpen, SoundCue.CraftingClose);
        Edge(before.Book, now.Book, SoundCue.BookOpen, SoundCue.BookClose);
        Edge(before.Dialogue, now.Dialogue, SoundCue.DialogueOpen, SoundCue.DialogueClose);
        if (before.Book && now.Book && before.BookPage != now.BookPage)
        {
            if (!_skipPageTurn)
            {
                _frame.Add(SoundCue.BookPage);
            }
        }

        _skipPageTurn = false;
    }

    /// <summary>Plays the frame's cues, folded (a duplicate once, the progress cues not piled up), then clears the frame.</summary>
    /// <param name="tick">The tick the log's lines are stamped with.</param>
    public void Flush(long tick)
    {
        Mixer.SetTick(tick);
        if (_frame.Count == 0)
        {
            return;
        }

        foreach (var cue in SoundEventMapper.Collapse(_frame))
        {
            _ = Mixer.Play(cue);
        }

        _frame.Clear();
    }

    private void Edge(bool before, bool now, SoundCue opened, SoundCue closed)
    {
        if (before != now)
        {
            _frame.Add(now ? opened : closed);
        }
    }
}
