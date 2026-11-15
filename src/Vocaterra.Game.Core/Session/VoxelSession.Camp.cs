using System.Globalization;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Land;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Layout;

namespace Vocaterra.Game.Core.Session;

// The camp (Y12, D-game-32): the camp kit the mayor sold with the plot sets up a tent, a bedroll and a fire ring as one piece inside the owned plot. The shape is the scene layout's `camp`
// stamp (CampPlan); the blocks are set directly, never through TryPlace (which refuses everything but planks and torches on protected ground and knows no camp). The bedroll and the lit ring
// become sleep places made in play, saved in systems.sleep with the camp's position; gathering any block of the camp packs the whole camp back into the kit.
public sealed partial class VoxelSession
{
    /// <summary>The id of the sleep place at the camp's bedroll (Y12).</summary>
    public const string CampBedPlace = "camp.bed";

    // The ui key of the word the arrow shows at the camp's places (ui.campBeacon: «campamento»), as the plot's arrow shows ui.plotBeacon.
    private const string CampBeaconKey = "campBeacon";

    /// <summary>The id of the sleep place at the camp's fire ring (Y12); usable when the ring is lit, as every fire is.</summary>
    public const string CampFirePlace = "camp.fire";

    /// <summary>The detail of the world events at the camp's ring (<c>torch_at_ring</c>, <c>fire_lit</c>): the camp objective's step <c>fire</c> waits for it.</summary>
    public const string CampFireName = "camp.fire";

    // The ui keys of the camp's lines (content/locale/en/ui.json).
    private const string CampOutsideHint = "campOutside";
    private const string CampNotLevelHint = "campNotLevel";
    private const string CampBlockedHint = "campBlocked";
    private const string CampPlayerHint = "campPlayer";
    private const string CampNoWakeHint = "campNoWake";
    private const string CampHaveHint = "campHave";
    private const string CampPlacedHint = "campPlaced";
    private const string CampPackedHint = "campPacked";
    private const string CampPackFullHint = "campPackFull";
    private const string CampKitBackHint = "campKitBack";

    // The sleep places' ranges: the bedroll, as the inn's bed; the fire, across the ring and the door, a few blocks up and down.
    private const int CampBedRadius = 2;
    private const int CampBedHeight = 2;
    private const int CampFireRadius = 4;
    private const int CampFireHeight = 3;

    private bool _campKitOwed;
    private bool _campFireLit;

    /// <summary>What using the camp kit on the ground did (Y12).</summary>
    public enum CampResult
    {
        /// <summary>Nothing: no kit in the bag, the player is dead, the world has no camp stamp, or the cell is outside the world.</summary>
        NotApplied,

        /// <summary>The camp was set up.</summary>
        Placed,

        /// <summary>The camp cannot stand there; the hint says why and nothing changed.</summary>
        Refused,
    }

    /// <summary>The camp the player has set up (Y12), or <see langword="null"/>. One camp at most: the kit is one item.</summary>
    public CampPlan? Camp { get; private set; }

    /// <summary>Whether the camp's fire ring burns now: a camp stands and a torch is on its ring (read from the world).</summary>
    public bool CampFireIsLit => Camp is not null && Fires.IsLit(Camp.Fire);

    /// <summary>The camp's fire ring middle's column when it burns, for the sound beds (the ring's fire bed follows it as it follows Luna's), or <see langword="null"/>.</summary>
    public (int X, int Z)? LitCampFire => CampFireIsLit ? (Camp!.Fire.X, Camp.Fire.Z) : null;

    // The camp stamp of the bundle, or null when the scene layout has none.
    private StampSpec? CampShape => Data.Scene.Stamps.TryGetValue(CampPlan.StampName, out var shape) ? shape : null;

    /// <summary>
    /// The camp kit's use on the ground block the crosshair is on (Y12): sets up the camp with its fire ring's middle on that column, its front facing the player. Every cell is checked before
    /// anything changes (inside the owned plot, on ground at one height, free, clear of the player, a cell to wake on): a refusal puts its reason on the hint line and changes nothing. On success the
    /// blocks are set (undergrowth in the way goes), the kit is taken, the fire rings are scanned again, the bedroll and the ring become sleep places and <c>camp_placed</c> is raised.
    /// </summary>
    /// <param name="ground">The block aimed at.</param>
    public CampResult UseCampKit(BlockPos ground) => UseCampKit(ground, FacingTheCamp());

    /// <summary>
    /// <see cref="UseCampKit(BlockPos)"/> with the side the camp's front faces given (the player's own is the side opposite to where they look: the camp faces them).
    /// </summary>
    /// <param name="ground">The block aimed at.</param>
    /// <param name="facing">The side the camp's front faces.</param>
    public CampResult UseCampKit(BlockPos ground, Direction facing)
    {
        if (Needs.IsDead || !Scene.Grid.InBounds(ground.X, ground.Y, ground.Z) || Simulation.Inventory.Count(ItemId.CampKit) < 1 || CampShape is not { } shape)
        {
            return CampResult.NotApplied;
        }

        if (Camp is not null)
        {
            _hints.Add(Locale.Ui(CampHaveHint));
            return CampResult.Refused;
        }

        var grid = Scene.Grid;
        var groundY = CampPlan.GroundOf(grid, ground.X, ground.Z);
        if (groundY < 0)
        {
            _hints.Add(Locale.Ui(CampBlockedHint));
            return CampResult.Refused;
        }

        var plan = CampPlan.Around(shape, ground.X, ground.Z, groundY, facing);
        var fault = plan.Check(grid, IsOwned);
        if (fault == CampFault.None && plan.Blocks.Any(b => BlockDefinition.IsSolid(b.Block) && Simulation.OverlapsPlayer(b.Cell)))
        {
            _hints.Add(Locale.Ui(CampPlayerHint));
            return CampResult.Refused;
        }

        if (fault == CampFault.None && !plan.HasWake(grid))
        {
            fault = CampFault.NoWake;
        }

        if (fault != CampFault.None)
        {
            _hints.Add(Locale.Ui(fault switch
            {
                CampFault.NotOwned => CampOutsideHint,
                CampFault.NotLevel => CampNotLevelHint,
                CampFault.Blocked => CampBlockedHint,
                _ => CampNoWakeHint,
            }));
            return CampResult.Refused;
        }

        plan.Place(grid);
        _ = Simulation.TakeItems(ItemId.CampKit, 1);
        Camp = plan;
        Land.Camp = plan;
        Fires.Rescan();
        AddCampPlaces(plan);
        _campFireLit = CampFireIsLit;
        _hints.Add(Locale.Ui(CampPlacedHint));
        LogWorld(WorldEvents.CampPlaced, null, string.Create(CultureInfo.InvariantCulture, $"{plan.Fire.X},{plan.Fire.Y},{plan.Fire.Z}"));
        return CampResult.Placed;
    }

    // The side the camp's front faces for a player who looks along their yaw: opposite to the way they look, snapped to the four axes (a tie goes to the north-south axis).
    private Direction FacingTheCamp()
    {
        var yaw = Simulation.Player.Yaw;
        var east = Mathematics.Angles.Sin(yaw);
        var south = -Mathematics.Angles.Cos(yaw);
        var looking = Math.Abs(east) > Math.Abs(south) ? (east > 0 ? Direction.East : Direction.West) : (south > 0 ? Direction.South : Direction.North);
        return looking switch
        {
            Direction.North => Direction.South,
            Direction.South => Direction.North,
            Direction.East => Direction.West,
            _ => Direction.East,
        };
    }

    private void AddCampPlaces(CampPlan plan)
    {
        SleepPlaces.Add(new SleepPlace(CampBedPlace, null, plan.Bed, SleepPlaceSpec.KindBed, CampBedRadius, CampBedHeight, SleepPlaceSpec.AccessAlways, plan.Wake));
        SleepPlaces.Add(new SleepPlace(CampFirePlace, null, plan.Fire, SleepPlaceSpec.KindFire, CampFireRadius, CampFireHeight, SleepPlaceSpec.AccessAlways, plan.Wake));
    }

    // Whether a place is one of the camp's.
    internal static bool IsCampPlace(string id) => id is CampBedPlace or CampFirePlace;

    // A torch was placed (BlockPlaced): at the camp's ring it lights the camp's fire, which raises the ring's torch event and, the first time, fire_lit with the camp's ring as its detail.
    private bool TorchAtCampRing(BlockPos cell)
    {
        if (Camp is not { } camp || !AtRing(camp.RingStones, cell))
        {
            return false;
        }

        LogWorld(WorldEvents.TorchAtRing, null, CampFireName);
        if (!_campFireLit)
        {
            _campFireLit = true;
            LogWorld(WorldEvents.FireLit, null, CampFireName);
        }

        return true;
    }

    /// <summary>
    /// Packs the camp up (Y12): every block it set goes, the bushes planted beside it stay, the camp kit goes back into the bag (a torch on the ring comes back too when the bag has room) and the camp's
    /// two sleep places are dropped; the place last slept falls back to the inn's bed when it was the camp's and lodging is given, otherwise to Luna's fire. A full bag refuses, with a line, and
    /// changes nothing. The kit can be set up again.
    /// </summary>
    /// <returns>Whether the camp was packed.</returns>
    public bool PackCamp()
    {
        if (Camp is not { } camp || Needs.IsDead)
        {
            return false;
        }

        var bag = Simulation.Inventory;
        if (!bag.CanAdd(ItemId.CampKit, 1))
        {
            _hints.Add(Locale.Ui(CampPackFullHint));
            return false;
        }

        var grid = Scene.Grid;
        foreach (var (cell, block) in camp.Blocks)
        {
            if (grid.GetBlock(cell.X, cell.Y, cell.Z) == block)
            {
                grid.SetBlock(cell.X, cell.Y, cell.Z, BlockId.Air);
            }
        }

        _ = Simulation.GrantItems(ItemId.CampKit, 1);
        ReturnRingTorches(camp);
        DropCamp();
        Fires.Rescan();
        _hints.Add(Locale.Ui(CampPackedHint));
        return true;
    }

    // The torches left on a ring that is gone go back to the bag while it has room (a torch is not gatherable by hand, so it would hang in the air).
    private void ReturnRingTorches(CampPlan camp)
    {
        var grid = Scene.Grid;
        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dz = -1; dz <= 1; dz++)
            {
                for (var dy = 0; dy <= 1; dy++)
                {
                    var (x, y, z) = (camp.Fire.X + dx, camp.Fire.Y + dy, camp.Fire.Z + dz);
                    if (grid.InBounds(x, y, z) && grid.GetBlock(x, y, z) == BlockId.Torch && Simulation.Inventory.CanAdd(ItemId.Torch, 1))
                    {
                        grid.SetBlock(x, y, z, BlockId.Air);
                        _ = Simulation.GrantItems(ItemId.Torch, 1);
                    }
                }
            }
        }
    }

    // The camp is no more (packed up, or its blocks were gone at a load): its places go, the camp is forgotten, and the place last slept falls back.
    private void DropCamp()
    {
        _ = SleepPlaces.Remove(CampBedPlace);
        _ = SleepPlaces.Remove(CampFirePlace);
        Camp = null;
        Land.Camp = null;
        _campFireLit = false;
        if (LastSleepPlace is { } last && IsCampPlace(last.Id))
        {
            LastSleepPlace = LodgingGiven ? new LastSleepSave(InnBedPlace) : null; // else a death wakes at Luna's fire, which death.wakeAt names
        }
    }

    // The id of the inn's sleep place (game/needs.json), the fallback of a death after the camp was packed.
    private string InnBedPlace => Data.Needs.SleepPlaces.FirstOrDefault(static p => p.Access == SleepPlaceSpec.AccessLodging && p.Kind == SleepPlaceSpec.KindBed)?.Id ?? "inn.bed";

    // Once a tick: a camp kit a load could not give back (a full bag) comes when there is room.
    internal void ReturnOwedKit()
    {
        if (_campKitOwed && Simulation.Inventory.CanAdd(ItemId.CampKit, 1))
        {
            _ = Simulation.GrantItems(ItemId.CampKit, 1);
            _campKitOwed = false;
            _hints.Add(Locale.Ui(CampKitBackHint));
        }
    }

    /// <summary>The camp's part of the sleep entry of a save (Y12), or <see langword="null"/> when no camp stands.</summary>
    internal CampSave? CaptureCamp() => Camp?.ToSave();

    /// <summary>Whether the camp kit is still owed to the bag (a camp lost at a load, a bag that was full), for the save.</summary>
    internal bool CampKitOwed => _campKitOwed;

    // A load puts the camp back (Y12): a camp whose blocks all stand is back with its places (RestoreSleep put them); one whose blocks are gone loses its places and gives the kit back, once.
    private void RestoreCamp(CampSave? save, bool kitOwed)
    {
        _campKitOwed = kitOwed;
        Camp = null;
        Land.Camp = null;
        _campFireLit = false;
        if (save is not null)
        {
            if (CampShape is { } shape && save.ToDirection() is { } facing)
            {
                var plan = new CampPlan(shape, facing, save.X, save.Z, save.Ground);
                if (plan.Stands(Scene.Grid))
                {
                    Camp = plan;
                    Land.Camp = plan;
                    if (SleepPlaces.Find(CampBedPlace) is null || SleepPlaces.Find(CampFirePlace) is null)
                    {
                        AddCampPlaces(plan);
                    }

                    _campFireLit = CampFireIsLit;
                }
            }

            if (Camp is null)
            {
                _campKitOwed = true; // the blocks are gone: the kit comes back once, now or when the bag has room
            }
        }

        if (Camp is null)
        {
            DropCamp();
        }

        Fires.Rescan(); // the owed kit is given after the load's digest check (SaveGame.Restore) or the next tick, never before: the bag is part of the digest
    }
}
