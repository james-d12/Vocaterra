using Vocaterra.Game;
using Vocaterra.MonoGame.Graphics;
using Vocaterra.MonoGame.UI;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.MonoGame.Utilities;

/// <summary>
/// What the scripted run asks of the game around it: open and close the dialogue panel on an NPC, and save a screenshot. The game supplies
/// these (the panel and the back buffer are its), so the script itself stays free of the engine and is tested without a window.
/// </summary>
/// <param name="OpenTalk">Opens the panel on an NPC (the same call the E key makes).</param>
/// <param name="CloseTalk">Closes the panel (the same call the Esc key makes).</param>
/// <param name="TakeShot">Asks for a screenshot of the next drawn frame.</param>
/// <param name="ShotPending">Whether the last requested screenshot has not been saved yet.</param>
/// <param name="Lesson">Carries out a lesson step of the script (S16): the kind and its value (the lesson's id, or the option). The game supplies the lesson front-end's call.</param>
/// <param name="Report">Opens the report panel (S21) and types the text in it; the flag says whether the panel stays open (<c>--report-open</c>) or the text is saved (<c>--report</c>).</param>
/// <param name="ReportBusy">Whether the report panel is up or on its way (S21): a saved report closes it, and the script waits for that.</param>
/// <param name="BookPlay">Opens the book on the target's page and plays its recording, normal or slow (S20b, <c>--book-play</c>).</param>
/// <param name="BookPractise">Opens the book on its Lessons page and presses "Practise again" on the lesson (S26, <c>--book-practise</c>).</param>
public sealed record ScenarioHooks(
    Action<string> OpenTalk,
    Action CloseTalk,
    Action<string> TakeShot,
    Func<bool> ShotPending,
    Action<ScriptStepKind, string>? Lesson = null,
    Action<string, bool>? Report = null,
    Func<bool>? ReportBusy = null,
    Action<string, bool>? BookPlay = null,
    Action<string>? BookPractise = null);

/// <summary>
/// The scripted run of the 3D game (<c>--screenshot</c> with steps): the body's steps (<see cref="VoxelScript"/>), the talk steps (S6) and the
/// screenshots, one after the other in the order given. A <c>--talk</c> walks to the NPC and stops at the stand-off distance
/// (<see cref="NpcStandOff"/>, about two blocks, with a clear line to the NPC) facing it, then opens the panel; a <c>--say</c> types the line into
/// the panel, sends it and waits for the NPC's turn to be applied (with no panel open it first talks to the guide, as the S3 run did); walking on
/// or talking to someone else closes the panel, as pressing Esc would. It drives the same panel, the same worker and the same session the player
/// does; the frame calls <see cref="Fill"/> once per tick for the body's commands.
/// </summary>
/// <remarks>The script is a state machine over ticks: nothing reads the clock or draws a random number.</remarks>
public sealed class ScenarioScript
{
    /// <summary>The ticks the script waits after arriving before it opens the panel (the view settles).</summary>
    public const int SettleTicks = 20;

    /// <summary>The ticks between one line's reply and the next step.</summary>
    public const int GapTicks = 6;

    private readonly VoxelSession _session;
    private readonly LookControl _look;
    private readonly DialogueModel _dialogue;
    private readonly IReadOnlyList<ScriptStep> _steps;
    private readonly Func<string, ItemId?> _resolveItem;
    private readonly ScenarioHooks _hooks;
    private readonly Action<string> _log;
    private readonly bool _holdsCheckAnswers;
    private readonly List<VoxelCommand> _scratch = [];
    private Phase _phase = Phase.Idle;
    private VoxelScript? _walk;
    private string _npc = string.Empty;
    private string _line = string.Empty;
    private int _index;
    private int _wait;
    private string _lessonId = string.Empty;

    /// <summary>Creates the script over a started session and its panel.</summary>
    /// <param name="session">The 3D game, with its worker started.</param>
    /// <param name="look">The look the script turns (the front-end's own).</param>
    /// <param name="dialogue">The panel the lines are typed into.</param>
    /// <param name="steps">The steps in order.</param>
    /// <param name="resolveItem">Maps an item as typed (a noun or a key) to the item, for the place and craft steps.</param>
    /// <param name="hooks">What the script asks of the game around it.</param>
    /// <param name="log">Where the steps report (the console).</param>
    /// <param name="holdsCheckAnswers">
    /// Whether the script stops at the answers of a check battery (<c>--pick</c>, and a <c>--say</c> or <c>--speak</c> with no panel open) instead of
    /// taking them as world steps: in a playtest the post-check takes them from the same step list (S12).
    /// </param>
    public ScenarioScript(
        VoxelSession session,
        LookControl look,
        DialogueModel dialogue,
        IReadOnlyList<ScriptStep> steps,
        Func<string, ItemId?> resolveItem,
        ScenarioHooks hooks,
        Action<string> log,
        bool holdsCheckAnswers = false)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(resolveItem);
        ArgumentNullException.ThrowIfNull(hooks);
        ArgumentNullException.ThrowIfNull(log);
        _session = session;
        _look = look;
        _dialogue = dialogue;
        _steps = steps;
        _resolveItem = resolveItem;
        _hooks = hooks;
        _log = log;
        _holdsCheckAnswers = holdsCheckAnswers;
    }

    /// <summary>
    /// Starts the world's steps at this index: the steps before it were the check battery's (<see cref="CheckScript"/>, S11), and the script begins
    /// with the first step after them. Call it once, before the first <see cref="Fill"/>.
    /// </summary>
    /// <param name="index">The index of the first world step.</param>
    public void SkipTo(int index)
    {
        if (index < 0 || index > _steps.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The step is not in the script.");
        }

        _index = index;
    }

    /// <summary>Whether every step has been done and its effect applied.</summary>
    public bool IsDone => _phase == Phase.Idle && _index >= _steps.Count;

    /// <summary>The next step the script has not started (the playtest's post-check takes the script from here, S12).</summary>
    public int Index => _index;

    /// <summary>Whether no step is half done (no walk, line or wait in progress), so the script can hand the steps on (S12).</summary>
    public bool AtStepBoundary => _phase == Phase.Idle;

    /// <summary>The line being said (empty when none).</summary>
    public string CurrentLine => _line;

    /// <summary>
    /// Advances the script by one tick and adds the body's commands for it. Call it once per tick, before the simulation steps, and only while
    /// the script is not done.
    /// </summary>
    public void Fill(List<VoxelCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        while (true)
        {
            switch (_phase)
            {
                case Phase.Idle:
                    if (_index >= _steps.Count)
                    {
                        return;
                    }

                    if (_session.KeyWordsPending && _steps[_index].Kind is ScriptStepKind.Say or ScriptStepKind.Speak or ScriptStepKind.Wav)
                    {
                        return; // the stall's key-word cards stand alone first (S8): the line waits for the shopkeeper's question
                    }

                    if (_holdsCheckAnswers && IsCheckAnswer(_steps[_index]))
                    {
                        return; // a playtest: the post-check's answers are taken by the battery once it starts (S12), not by the world
                    }

                    Begin();
                    continue;
                case Phase.Moving:
                case Phase.Walking:
                    if (Advance(commands))
                    {
                        return;
                    }

                    continue;
                case Phase.Settling:
                    if (--_wait <= 0)
                    {
                        _hooks.OpenTalk(_npc);
                        _phase = Phase.Idle;
                        continue;
                    }

                    return;
                case Phase.Waiting:
                    if (_session.PendingTurns == 0 && !_dialogue.SubmitRequested && _dialogue.PendingSpeech is null)
                    {
                        _phase = Phase.Gap;
                        _wait = GapTicks;
                    }

                    return;
                case Phase.Gap:
                    if (--_wait <= 0)
                    {
                        _phase = Phase.Idle;
                        continue;
                    }

                    return;
                case Phase.Lesson:
                    if (LessonTick())
                    {
                        return;
                    }

                    continue;
                case Phase.ReportBusy:
                    if (_hooks.ReportBusy?.Invoke() ?? false)
                    {
                        return; // the report panel is up or on its way: the script waits until the report is saved
                    }

                    _phase = Phase.Idle;
                    continue;
                default: // Phase.Shot
                    if (_hooks.ShotPending())
                    {
                        return;
                    }

                    _phase = Phase.Idle;
                    continue;
            }
        }
    }

    // Takes the next step: a run of body steps becomes one VoxelScript, a talk walks to the NPC, a say types its line.
    // A step the check battery answers when a playtest holds the script: a pick, or a line with no panel open (the world would walk to the guide
    // for it). A line with the panel open is the world's, as before.
    private bool IsCheckAnswer(ScriptStep step) => step.Kind switch
    {
        ScriptStepKind.Pick => true,
        ScriptStepKind.Say or ScriptStepKind.Speak => !_dialogue.IsOpen,
        _ => false,
    };

    private void Begin()
    {
        var step = _steps[_index];
        switch (step.Kind)
        {
            case ScriptStepKind.Move:
                CloseIfOpen();
                var run = new List<VoxelStep>();
                while (_index < _steps.Count && _steps[_index].Kind == ScriptStepKind.Move)
                {
                    run.Add(_steps[_index].Step!);
                    _index++;
                }

                _walk = new VoxelScript(_session.Simulation, _look, run, _resolveItem, _log);
                _phase = Phase.Moving;
                break;
            case ScriptStepKind.Talk:
                StartTalk(step.Value);
                _index++;
                break;
            case ScriptStepKind.Say:
                if (!_dialogue.IsOpen)
                {
                    StartTalk(NpcService.GuideAgent); // a line with nobody to say it to: the guide, as in S3 (the step itself is taken next)
                    break;
                }

                _line = step.Value;
                _ = _dialogue.Entry.Insert(_line);
                _dialogue.RequestSubmit();
                _index++;
                _phase = Phase.Waiting;
                break;
            case ScriptStepKind.Speak:
            case ScriptStepKind.Wav:
                if (!_dialogue.IsOpen)
                {
                    StartTalk(NpcService.GuideAgent); // nobody to say it to: the guide, as for --say
                    break;
                }

                // The scripted spoken path (S7): the line is synthesized by Kokoro (or the WAV read) on the worker and recognized by Vosk as if the
                // player had said it into the microphone; the turn is the one a spoken line makes.
                _line = step.Value;
                _dialogue.RequestSpeech(step.Kind == ScriptStepKind.Speak ? TurnSource.Speak : TurnSource.Wav, step.Value);
                _index++;
                _phase = Phase.Waiting;
                break;
            case ScriptStepKind.Leave:
                CloseIfOpen();
                _index++;
                break;
            case ScriptStepKind.LessonStart:
                if (!_dialogue.IsOpen)
                {
                    // The lesson is attached to an NPC: walk up to it and open the panel first (the step itself is taken next).
                    StartTalk(_session.Lessons.Book.Find(step.Value)?.Npc ?? NpcService.GuideAgent);
                    break;
                }

                _hooks.Lesson?.Invoke(step.Kind, step.Value);
                _index++;
                _phase = Phase.Gap;
                _wait = GapTicks;
                break;
            case ScriptStepKind.LessonContinue:
            case ScriptStepKind.LessonSkip:
            case ScriptStepKind.LessonHelp:
            case ScriptStepKind.LessonPick:
                _hooks.Lesson?.Invoke(step.Kind, step.Value);
                _index++;
                _phase = Phase.Gap;
                _wait = GapTicks;
                break;
            case ScriptStepKind.LessonRun:
                if (_session.Lessons.Book.Find(step.Value) is not { } runLesson)
                {
                    _log($"lesson-run: there is no lesson '{step.Value}'");
                    _index++;
                    break;
                }

                if (_session.Lessons.StatusOf(runLesson.Id) == LessonStatus.Completed && !string.Equals(_session.Lessons.ReplayPending, runLesson.Id, StringComparison.Ordinal))
                {
                    _index++; // done (the step is taken again after each interruption: a line sent, a walk to the fruit); a lesson asked to be practised again runs again (S26)
                    break;
                }

                if (!_dialogue.IsOpen)
                {
                    StartTalk(_session.Lessons.TalkTarget ?? runLesson.Npc); // walk up to the NPC (the step's own, or the one a do step waits for, S26) and open the panel; the step itself is taken next
                    break;
                }

                _lessonId = runLesson.Id;
                _hooks.Lesson?.Invoke(ScriptStepKind.LessonStart, runLesson.Id); // an auto-start lesson is engaged already: this changes nothing then
                _phase = Phase.Lesson;
                break;
            case ScriptStepKind.Report:
                _hooks.Report?.Invoke(step.Value, false); // opens the panel and types the text; it is saved at the next frame
                _index++;
                _phase = Phase.ReportBusy;
                break;
            case ScriptStepKind.ReportOpen:
                // Leaves the panel open for the screenshot. A paused world takes no further steps, so this is the script's last step (LaunchOptions checks it).
                _hooks.Report?.Invoke(step.Value, true);
                _index++;
                break;
            case ScriptStepKind.BookPlay:
            case ScriptStepKind.BookPlaySlow:
                _hooks.BookPlay?.Invoke(step.Value, step.Kind == ScriptStepKind.BookPlaySlow);
                _index++;
                break;
            case ScriptStepKind.BookPractise:
                _hooks.BookPractise?.Invoke(step.Value);
                _index++;
                break;
            case ScriptStepKind.Pick:
                _log($"pick {step.Value}: a check step; the check battery takes it before the world (ignored here)");
                _index++;
                break;
            case ScriptStepKind.Wait:
                _wait = int.Parse(step.Value, System.Globalization.CultureInfo.InvariantCulture) * Vocaterra.Simulation.Voxel.VoxelSimulation.TicksPerSecond;
                _index++;
                _phase = Phase.Gap;
                break;
            case ScriptStepKind.Skip:
                var skipped = int.Parse(step.Value, System.Globalization.CultureInfo.InvariantCulture);
                _session.SkipTime(skipped);
                _log($"skip: the stored time moves on {skipped} s (now {_session.StoredTime.UnixSeconds})");
                _index++;
                break;
            default: // Shot
                _hooks.TakeShot(step.Value);
                _index++;
                _phase = Phase.Shot;
                break;
        }
    }

    // One tick of a lesson run (S17): the learner who gets everything right does the step in progress. Returns true when the tick was taken (the script waits), false
    // when the script should look again at once (the panel is closed, or the lesson ended).
    private bool LessonTick()
    {
        var runner = _session.Lessons;
        if (runner.StatusOf(_lessonId) == LessonStatus.Completed)
        {
            _index++;
            _phase = Phase.Gap;
            _wait = GapTicks;
            return true;
        }

        if (!runner.Engaged || !_dialogue.IsOpen || runner.Current is not { } step)
        {
            _phase = Phase.Idle; // the panel was closed for a do step: the run walks back to the NPC and takes up where the lesson stopped
            return false;
        }

        if (_session.PendingTurns > 0 || _dialogue.SubmitRequested || _dialogue.PendingSpeech is not null)
        {
            return true;
        }

        switch (step.Type)
        {
            case LessonStepType.Listen:
                _hooks.Lesson?.Invoke(ScriptStepKind.LessonPick, (step.Correct + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
                _hooks.Lesson?.Invoke(ScriptStepKind.LessonContinue, string.Empty);
                break;
            case LessonStepType.Repeat:
            case LessonStepType.Answer:
                _line = _session.Phrases.Find(step.Phrase!)!.Spanish.TrimEnd('.');
                _ = _dialogue.Entry.Insert(_line);
                _dialogue.RequestSubmit();
                _phase = Phase.Waiting;
                return true;
            case LessonStepType.Do:
                CloseIfOpen();
                if (string.Equals(step.Event, WorldEvents.TalkOpened, StringComparison.Ordinal))
                {
                    _phase = Phase.Idle; // the step waits for the panel to open on another NPC (S26): the run walks up to that NPC next (LessonRun), not to the beacon
                    return true;
                }

                if (_session.Pointer is { } target)
                {
                    _walk = new VoxelScript(
                        _session.Simulation,
                        _look,
                        [VoxelStep.WalkTo(target.Target.X, target.Target.Z), VoxelStep.Interact(target.Target.X, target.Target.Y, target.Target.Z)],
                        static _ => null,
                        _log);
                    _phase = Phase.Moving;
                }
                else
                {
                    _log($"lesson-run: the step '{step.Id}' has nothing to walk to; it is skipped");
                    _hooks.Lesson?.Invoke(ScriptStepKind.LessonSkip, string.Empty);
                }

                return true;
            default:
                _hooks.Lesson?.Invoke(ScriptStepKind.LessonContinue, string.Empty);
                break;
        }

        _phase = Phase.Gap;
        _wait = GapTicks;
        return true;
    }

    // Walks to the stand-off cell of the NPC, then looks at it and opens the panel.
    private void StartTalk(string npc)
    {
        CloseIfOpen();
        _npc = npc;
        var scene = _session.Scene;
        var feet = scene.Npc(npc).Feet;
        var from = _session.Simulation.Player.FeetBlock;
        var stand = NpcStandOff.Choose(scene.Grid, feet, from) ?? new BlockPos(feet.X + NpcStandOff.Blocks, feet.Y, feet.Z);
        _walk = new VoxelScript(_session.Simulation, _look, [VoxelStep.WalkTo(stand.X, stand.Z)], static _ => null, _log);
        _phase = Phase.Walking;
    }

    private void CloseIfOpen()
    {
        if (_dialogue.IsOpen)
        {
            _hooks.CloseTalk();
        }
    }

    // One tick of the running walk. Returns true when it produced commands (the tick is taken); false when it finished with none (go on).
    private bool Advance(List<VoxelCommand> commands)
    {
        var walk = _walk!;
        walk.Fill(_scratch);
        commands.AddRange(_scratch);
        var finished = walk.IsDone;
        if (finished)
        {
            _walk = null;
            if (_phase == Phase.Walking)
            {
                FaceNpc();
                _phase = Phase.Settling;
                _wait = SettleTicks;
            }
            else
            {
                _phase = Phase.Idle;
            }
        }

        return _scratch.Count > 0 || !finished;
    }

    // Turns the view to the NPC's chest, the point the crosshair aims at (FigureAim).
    private void FaceNpc()
    {
        var feet = _session.Scene.Npc(_npc).Feet;
        var body = _session.Simulation.Player;
        const double U = VoxelPhysics.UnitsPerBlock;
        _look.LookAt(body.EyeX / U, body.EyeY / U, body.EyeZ / U, feet.X + 0.5, feet.Y + 1.2, feet.Z + 0.5);
    }

    private enum Phase
    {
        Idle,
        Moving,
        Walking,
        Settling,
        Waiting,
        Gap,
        Lesson,
        ReportBusy,
        Shot,
    }
}
