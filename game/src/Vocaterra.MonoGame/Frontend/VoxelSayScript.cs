using Vocaterra.Game;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// The <c>--say</c> script of a 3D run (S3): walk to the guide and face her, open the panel (asking the review question when one is due), type
/// each line, send it, wait for the guide's turn to be applied, and go on to the next line. It drives the same panel and the same worker as the
/// player does; the frame calls <see cref="Fill"/> once per tick for the body's commands, and the panel's submission is the turn pump's, as for
/// typed lines.
/// </summary>
/// <remarks>
/// The walk is a <see cref="VoxelScript"/> with two steps (walk to a block two blocks east of the guide, then look at her), so the walk's
/// stuck handling is the one the <c>--walk-to</c> step has. The script is a state machine over ticks: nothing reads the clock.
/// </remarks>
public sealed class VoxelSayScript
{
    /// <summary>The ticks the script waits after arriving before it opens the panel (the view settles).</summary>
    public const int SettleTicks = 20;

    /// <summary>The ticks between one line's reply and the next line.</summary>
    public const int GapTicks = 6;

    /// <summary>How far east of the guide's feet the player stands (the camp keeps that clear).</summary>
    public const int StandBlocks = 2;

    private readonly VoxelSession _session;
    private readonly DialogueModel _dialogue;
    private readonly IReadOnlyList<string> _lines;
    private readonly VoxelScript _walk;
    private Phase _phase = Phase.Walking;
    private int _wait;
    private int _next;

    /// <summary>Creates the script for the lines, over a started session and its panel.</summary>
    /// <param name="session">The 3D game, with its worker started.</param>
    /// <param name="look">The look the script turns (the front-end's own).</param>
    /// <param name="dialogue">The panel the lines are typed into.</param>
    /// <param name="lines">The lines the player says, in order.</param>
    /// <param name="log">Where the walk reports its steps (the console).</param>
    public VoxelSayScript(VoxelSession session, LookControl look, DialogueModel dialogue, IReadOnlyList<string> lines, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(log);
        _session = session;
        _dialogue = dialogue;
        _lines = lines;
        var guide = session.Scene.Npc("guide").Feet;
        var standX = guide.X + StandBlocks;
        var standZ = guide.Z;

        // Yaw 0 is north (-z) and 90 is east (+x): the bearing from the stand to the guide's feet, in degrees.
        var yaw = Math.Atan2(guide.X - standX, -(guide.Z - standZ)) * 180.0 / Math.PI;
        _walk = new VoxelScript(
            session.Simulation,
            look,
            [VoxelStep.WalkTo(standX, standZ), VoxelStep.Look(yaw, 0)],
            static _ => null,
            log);
        if (lines.Count == 0)
        {
            _phase = Phase.Done;
        }
    }

    /// <summary>Whether every line has been said and its reply applied.</summary>
    public bool IsDone => _phase == Phase.Done;

    /// <summary>The line being said (empty when none).</summary>
    public string CurrentLine => _next > 0 && _next <= _lines.Count ? _lines[_next - 1] : string.Empty;

    /// <summary>
    /// Advances the script by one tick and adds the body's commands for it. Call it once per tick, before the simulation steps, and only while
    /// the script is not done.
    /// </summary>
    public void Fill(List<VoxelCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        switch (_phase)
        {
            case Phase.Walking:
                _walk.Fill(commands);
                if (_walk.IsDone)
                {
                    _phase = Phase.Settling;
                    _wait = SettleTicks;
                }

                break;
            case Phase.Settling:
                if (--_wait <= 0)
                {
                    _dialogue.Open(_session.BeginTalk()?.Question);
                    _phase = Phase.Typing;
                }

                break;
            case Phase.Typing:
                _ = _dialogue.Entry.Insert(_lines[_next]);
                _dialogue.RequestSubmit();
                _next++;
                _phase = Phase.Waiting;
                break;
            case Phase.Waiting:
                if (_session.PendingTurns == 0 && !_dialogue.SubmitRequested)
                {
                    _phase = Phase.Gap;
                    _wait = GapTicks;
                }

                break;
            case Phase.Gap:
                if (--_wait <= 0)
                {
                    _phase = _next < _lines.Count ? Phase.Typing : Phase.Done;
                }

                break;
        }
    }

    private enum Phase
    {
        Walking,
        Settling,
        Typing,
        Waiting,
        Gap,
        Done,
    }
}
