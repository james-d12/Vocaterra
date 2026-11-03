using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Simulation;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Navigation;

/// <summary>
/// Where the beacon stands: on a named point at the start (<c>--beacon</c>, or the guide's camp for a new game, S13), on the session's arrow while
/// there is one (S3), and nowhere once the player has reached it. An arrival is a world event (<see cref="WorldEvents.BeaconReached"/>, village-plan V02): the
/// session logs it for its own arrow (<see cref="VoxelSession.Step"/>, whose reach takes the arrow down first), and this class for a beacon it placed itself.
/// </summary>
public sealed class BeaconDirector
{
    /// <summary>The point the beacon stands on at the start of a new game: the guide's camp, at the foot of the trail from the player's own camp (S13; <c>game/session.json</c>'s <c>beaconStart</c>, C07).</summary>
    public string StartPoint => _session.Data.Objectives.BeaconStart;

    private readonly GuideSession _session;
    private readonly Action<string> _log;
    private readonly Action<string> _error;
    private PointerMarker? _shownPointer;

    /// <summary>Creates the director of a session's beacon.</summary>
    /// <param name="session">The session whose arrow the beacon shows.</param>
    /// <param name="log">Where a line about the beacon goes (the console).</param>
    /// <param name="error">Where a point that is not in the scene is reported.</param>
    public BeaconDirector(GuideSession session, Action<string> log, Action<string> error)
    {
        _session = session;
        _log = log;
        _error = error;
    }

    /// <summary>The beacon: a column stands on its block and an arrow points to it from the screen edge.</summary>
    public Beacon Beacon { get; } = new();

    /// <summary>
    /// Puts the beacon at the start: on the point <paramref name="requested"/> names (<c>none</c> for no beacon), or for a new game with no
    /// request on <see cref="StartPoint"/> (the walk down the trail, until the opening of S17 says it in words).
    /// </summary>
    /// <param name="scene">The scene whose named points the beacon can stand on.</param>
    /// <param name="requested">The point of <c>--beacon</c>, or <see langword="null"/>.</param>
    /// <param name="loaded">Whether the game was loaded from a save.</param>
    public void PlaceAtStart(Scene scene, string? requested, bool loaded)
    {
        if (requested is { } point && !string.Equals(point, "none", StringComparison.Ordinal))
        {
            if (Beacon.SetPoint(scene, point, scene.LabelOf(point)))
            {
                _log($"beacon: {point} at {Beacon.Target!.Value.X},{Beacon.Target.Value.Y},{Beacon.Target.Value.Z}");
            }
            else
            {
                _error($"beacon: the scene has no point '{point}' (it has {string.Join(", ", scene.Points.Keys)})");
            }
        }

        if (requested is null && !loaded && Beacon.SetPoint(scene, StartPoint, scene.LabelOf(StartPoint)))
        {
            _log($"beacon: {StartPoint} at {Beacon.Target!.Value.X},{Beacon.Target.Value.Y},{Beacon.Target.Value.Z} (the walk down the trail)");
        }
    }

    /// <summary>
    /// A new play session began in the run (V12c, a sleep at the fire): the beacon is as a load starts it, with no start point of its own; the next <see cref="Sync"/> stands
    /// it on the session's arrow, if there is one.
    /// </summary>
    public void Restart()
    {
        Beacon.Clear();
        _shownPointer = null;
    }

    /// <summary>
    /// Once a frame: the arrow of the session is the beacon. A new arrow stands the column on its block (a review question shows no name), and
    /// when the beacon's own reach takes it down, the arrow goes too.
    /// </summary>
    public void Sync()
    {
        var pointer = _session.Pointer;
        if (!ReferenceEquals(pointer, _shownPointer))
        {
            _shownPointer = pointer;
            if (pointer is { } arrow)
            {
                Beacon.Set(arrow.Target.ToBlock(), arrow.Quiz ? _session.Data.Words.Unseen : arrow.Place, arrow.Point);
            }
            else
            {
                Beacon.Clear();
            }
        }

        if (pointer is not null && !Beacon.IsSet)
        {
            _session.DismissPointer();
            _shownPointer = null;
        }
    }

    /// <summary>
    /// Once a frame, last: the beacon comes down when the player stands near it. When it was a beacon this class placed (the start, <c>--beacon</c>) the arrival is
    /// logged as <see cref="WorldEvents.BeaconReached"/> with the point's name (village-plan V02); the session's own arrow logs its arrival itself.
    /// </summary>
    public void ClearIfReached(VoxelSimulation sim)
    {
        var label = Beacon.Label;
        var point = Beacon.Name ?? label;
        var own = _shownPointer is null;
        if (Beacon.ClearIfReached(sim.Player.CenterX / (double)VoxelPhysics.UnitsPerBlock, sim.Player.CenterZ / (double)VoxelPhysics.UnitsPerBlock))
        {
            _log($"beacon: reached {label} at tick {sim.Tick}");
            if (own && point is not null)
            {
                _session.NoteBeaconReached(point);
            }
        }
    }
}
