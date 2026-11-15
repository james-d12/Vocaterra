using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Session;

/// <summary>A place the player can sleep at: where it stands, what it is, how near the player must be, and what it asks.</summary>
/// <param name="Id">The place's id (<c>guide.fire</c>).</param>
/// <param name="Point">The scene point it was made at (<c>firering</c>), or <see langword="null"/> for a place made in play at a cell.</param>
/// <param name="Cell">The cell it stands at.</param>
/// <param name="Kind"><see cref="SleepPlaceSpec.KindFire"/> or <see cref="SleepPlaceSpec.KindBed"/>.</param>
/// <param name="RadiusBlocks">How far across, in blocks, of the cell the player may stand to sleep there.</param>
/// <param name="HeightBlocks">How far in height, in blocks, of the cell.</param>
/// <param name="Access"><see cref="SleepPlaceSpec.AccessAlways"/> or <see cref="SleepPlaceSpec.AccessLodging"/>.</param>
/// <param name="Wake">The cell the player wakes on after a death, for a place that names one (the camp's, Y12), or <see langword="null"/> to wake on the nearest free ground.</param>
public sealed record SleepPlace(string Id, string? Point, BlockPos Cell, string Kind, int RadiusBlocks, int HeightBlocks, string Access, BlockPos? Wake = null);

/// <summary>
/// The places the player can sleep at (Y01): the authored ones (<see cref="NeedsData.SleepPlaces"/>) resolved against the scene's points, and the ones added and removed while the game
/// runs. A place whose point the scene lacks is left out. A fire is usable while a lit ring stands at it, read from the world when asked; a bed when its access is met (<c>lodging</c>
/// is met by the flag the session gives). Sleeping, the prompt, the closing card, the arrow to bed and the waking after a death all ask this list. It reads the world and holds no clock.
/// </summary>
public sealed class SleepPlaces
{
    private readonly FireRings _fires;
    private readonly VoxelGrid _grid;
    private readonly Func<bool> _lodgingGiven;
    private readonly string _wakeAt;
    private readonly List<SleepPlace> _places = [];
    private readonly HashSet<string> _madeInPlay = new(StringComparer.Ordinal);

    // The walks of IsAt, by place id, for one feet cell and one state of the world: the prompt, the card and the rest rule ask several times a frame, the walk is done once.
    private readonly Dictionary<string, bool> _walks = new(StringComparer.Ordinal);
    private BlockPos _walkFeet;
    private long _walkEdits = -1;

    /// <summary>Resolves the authored places against a scene.</summary>
    /// <param name="specs">The authored places.</param>
    /// <param name="wakeAt">The id of the place the player wakes at after a death.</param>
    /// <param name="scene">The scene whose points the places stand at.</param>
    /// <param name="fires">The world's fire rings (shared with the rest rule).</param>
    /// <param name="lodgingGiven">Whether a place with the access <c>lodging</c> is met now.</param>
    public SleepPlaces(IReadOnlyList<SleepPlaceSpec> specs, string wakeAt, Scene scene, FireRings fires, Func<bool> lodgingGiven)
    {
        ArgumentNullException.ThrowIfNull(specs);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(fires);
        ArgumentNullException.ThrowIfNull(lodgingGiven);
        _fires = fires;
        _grid = scene.Grid;
        _lodgingGiven = lodgingGiven;
        _wakeAt = wakeAt;
        foreach (var spec in specs)
        {
            if (scene.Points.TryGetValue(spec.Point, out var cell))
            {
                _places.Add(new SleepPlace(spec.Id, spec.Point, cell, spec.Kind, spec.RadiusBlocks, spec.HeightBlocks, spec.Access));
            }
        }
    }

    /// <summary>Every place, the authored ones first, then those made in play in the order made.</summary>
    public IReadOnlyList<SleepPlace> All => _places;

    /// <summary>Adds a place made while the game runs (a place with this id is replaced).</summary>
    /// <param name="place">The place.</param>
    public void Add(SleepPlace place)
    {
        ArgumentNullException.ThrowIfNull(place);
        Remove(place.Id);
        _places.Add(place);
        _madeInPlay.Add(place.Id);
        _walks.Clear();
    }

    /// <summary>The places made in play (<see cref="Add"/>), in the order made: what the save keeps, since the authored ones are the bundle's.</summary>
    public IReadOnlyList<SleepPlace> MadeInPlay => _places.Where(p => _madeInPlay.Contains(p.Id)).ToArray();

    /// <summary>Replaces the places made in play with these (a load): the authored places stay, and a place whose id an authored place has is left out.</summary>
    /// <param name="places">The places.</param>
    public void RestoreMadeInPlay(IEnumerable<SleepPlace> places)
    {
        ArgumentNullException.ThrowIfNull(places);
        _places.RemoveAll(p => _madeInPlay.Contains(p.Id));
        _madeInPlay.Clear();
        _walks.Clear();
        foreach (var place in places)
        {
            if (Find(place.Id) is null)
            {
                Add(place);
            }
        }
    }

    /// <summary>Takes a place away; false when there is none with this id.</summary>
    /// <param name="id">The place's id.</param>
    public bool Remove(string id)
    {
        _madeInPlay.Remove(id);
        _walks.Clear();
        return _places.RemoveAll(p => string.Equals(p.Id, id, StringComparison.Ordinal)) > 0;
    }

    /// <summary>The place with this id, or <see langword="null"/>.</summary>
    /// <param name="id">The place's id.</param>
    public SleepPlace? Find(string id) => _places.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));

    /// <summary>Whether the place can be slept at now: a fire when a lit ring stands at it (read from the world), a bed when its access is met.</summary>
    /// <param name="place">The place.</param>
    public bool IsUsable(SleepPlace place)
    {
        ArgumentNullException.ThrowIfNull(place);
        return place.Kind == SleepPlaceSpec.KindFire ? RingLitAt(place.Cell) : AccessMet(place);
    }

    /// <summary>Whether any place is usable now.</summary>
    public bool AnyUsable => _places.Any(IsUsable);

    /// <summary>The usable place the player stands at (within its reach across and in height), the nearest when several, or <see langword="null"/>.</summary>
    /// <param name="feet">The block the player's feet are in.</param>
    public SleepPlace? UsableAt(BlockPos feet) =>
        _places.Where(p => IsUsable(p) && IsAt(p, feet)).OrderBy(p => SquaredAcross(p.Cell, feet)).FirstOrDefault();

    /// <summary>The bed the player stands at whose access is not met (Y05: the inn's bed before lodging), the nearest when several, or <see langword="null"/>. A fire is never locked, only unlit.</summary>
    /// <param name="feet">The block the player's feet are in.</param>
    public SleepPlace? LockedBedAt(BlockPos feet) =>
        _places.Where(p => p.Kind != SleepPlaceSpec.KindFire && !IsUsable(p) && IsAt(p, feet)).OrderBy(p => SquaredAcross(p.Cell, feet)).FirstOrDefault();

    /// <summary>The usable place nearest the feet (across, then in height), or <see langword="null"/> when none is usable: where the arrow goes.</summary>
    /// <param name="feet">The block the player's feet are in.</param>
    public SleepPlace? NearestUsable(BlockPos feet) =>
        _places.Where(IsUsable).OrderBy(p => SquaredAcross(p.Cell, feet)).ThenBy(p => Math.Abs(p.Cell.Y - feet.Y)).FirstOrDefault();

    /// <summary>The place the player wakes at after a death (<see cref="DeathSpec.WakeAt"/>), usable or not, or <see langword="null"/> when the scene has none by that id.</summary>
    public SleepPlace? WakeAt => Find(_wakeAt);

    /// <summary>Whether the feet are within the place's reach across and in height, usable or not.</summary>
    /// <param name="place">The place.</param>
    /// <param name="feet">The block the player's feet are in.</param>
    public static bool IsWithinReach(SleepPlace place, BlockPos feet)
    {
        ArgumentNullException.ThrowIfNull(place);
        return Within(place, feet);
    }

    /// <summary>The most steps on foot from a place's cell at which the player still counts as at it: the reach across and a few blocks over (a wall is longer than that).</summary>
    /// <param name="place">The place.</param>
    public static int WalkStepsOf(SleepPlace place)
    {
        ArgumentNullException.ThrowIfNull(place);
        return place.RadiusBlocks + 3;
    }

    /// <summary>
    /// Whether the player is at the place: within its reach across and in height (a radius test, first and cheap) and then reachable on foot from the place's cell within
    /// <see cref="WalkStepsOf"/> steps, so a wall between them says no. A place whose cell has no standing room in its column keeps the radius test alone. The walk is kept until the feet
    /// move or the world is edited (<see cref="VoxelGrid.EditCount"/>), so asking again in the same frame costs nothing.
    /// </summary>
    /// <param name="place">The place.</param>
    /// <param name="feet">The block the player's feet are in.</param>
    public bool IsAt(SleepPlace place, BlockPos feet)
    {
        ArgumentNullException.ThrowIfNull(place);
        if (!Within(place, feet))
        {
            return false;
        }

        if (_walkEdits != _grid.EditCount || _walkFeet != feet)
        {
            _walks.Clear();
            _walkEdits = _grid.EditCount;
            _walkFeet = feet;
        }

        if (!_walks.TryGetValue(place.Id, out var at))
        {
            at = SleepWalk.StartFor(_grid, place.Cell) is not { } start || SleepWalk.Reaches(_grid, start, feet, WalkStepsOf(place));
            _walks[place.Id] = at;
        }

        return at;
    }

    private static bool Within(SleepPlace place, BlockPos feet) =>
        SquaredAcross(place.Cell, feet) <= (long)place.RadiusBlocks * place.RadiusBlocks && Math.Abs(place.Cell.Y - feet.Y) <= place.HeightBlocks;

    private static long SquaredAcross(BlockPos a, BlockPos b)
    {
        long dx = a.X - b.X;
        long dz = a.Z - b.Z;
        return (dx * dx) + (dz * dz);
    }

    private bool AccessMet(SleepPlace place) => place.Access == SleepPlaceSpec.AccessAlways || (place.Access == SleepPlaceSpec.AccessLodging && _lodgingGiven());

    // A lit ring stands at the place: a ring whose middle is within a block across of the cell and two in height (the point is at the ring's middle, on the ground beside its stones).
    private bool RingLitAt(BlockPos cell)
    {
        foreach (var middle in _fires.Middles)
        {
            if (Math.Abs(middle.X - cell.X) <= 1 && Math.Abs(middle.Z - cell.Z) <= 1 && Math.Abs(middle.Y - cell.Y) <= 2 && _fires.IsLit(middle))
            {
                return true;
            }
        }

        return false;
    }
}
