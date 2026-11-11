using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Session;

// Death and the belongings chest (N04, adaptation design 9.7). At zero health, in active play, the player is back at their own fire at once with the waking values, and what the
// bag held, but the items that stack to one, stays in a chest where they fell until the next night. A death is not a night: the day, the sitting and the day's resets do not move,
// and nothing of the learner's is touched. The game fades the screen on DeathCount.
public sealed partial class VoxelSession
{
    /// <summary>The ui key of the line after a death by hunger.</summary>
    public const string DiedHungerHint = "diedHunger";

    /// <summary>The ui key of the line after a death by thirst.</summary>
    public const string DiedThirstHint = "diedThirst";

    /// <summary>The ui key of the line after a death by drowning (water-plan H11).</summary>
    public const string DiedDrownedHint = "diedDrowned";

    /// <summary>The ui key of the line after a faint from drowning under the relaxed-needs setting.</summary>
    public const string FaintedDrownedHint = "faintedDrowned";

    /// <summary>The detail of <see cref="WorldEvents.PlayerDied"/> for a death by drowning (the cause the water rules raise).</summary>
    public const string DrownedCause = "player_drowned";

    /// <summary>The ui key of the line after a faint from hunger under the relaxed-needs setting (the bag is kept, no chest).</summary>
    public const string FaintedHungerHint = "faintedHunger";

    /// <summary>The ui key of the line after a faint from thirst under the relaxed-needs setting.</summary>
    public const string FaintedThirstHint = "faintedThirst";

    /// <summary>The ui key of the line when a chest keeps what the bag could not take back.</summary>
    public const string ChestRestHint = "chestRest";

    /// <summary>The ui key of the English of the beacon's label on the chest (the label itself is the Spanish <see cref="BelongingsHud"/>).</summary>
    public const string BelongingsLabel = "beaconBelongings";

    /// <summary>The key in <c>hudLines</c> (game/words.json) of the beacon's label on the chest, «tus cosas» (N05).</summary>
    public const string BelongingsHud = "hud.belongings";

    // How far (blocks across) the chest looks for free ground, and how far above and below the fall; and how far from the fire's middle the player wakes.
    private const int ChestSearchBlocks = 24;
    private const int ChestSearchHeight = 4;
    private const int WakeNearSquared = 4;
    private const int WakeFarSquared = 25;
    private const int WakeWalkSteps = 12;

    private BelongingsChest? _chest;

    /// <summary>How many times the player has died in this run. The game fades the screen when it changes.</summary>
    public int DeathCount { get; private set; }

    /// <summary>The cause line of the last death or faint (<c>You fainted from thirst and woke by your fire...</c>), or <see langword="null"/> before one: the death screen shows it (N05).</summary>
    public string? DeathLine { get; private set; }

    /// <summary>The chest a death left standing, or <see langword="null"/>: it goes with the next night.</summary>
    public BelongingsChest? Chest => _chest;

    /// <summary>Whether the crosshair is on the belongings chest within reach: E takes back what the bag can hold.</summary>
    public bool AimedAtChest => _chest is { } chest && Simulation.Target is { Id: BlockId.Chest } hit && hit.Block == chest.Cell;

    /// <summary>The chest and its contents for the save (<see langword="null"/> for none).</summary>
    internal ChestSave? CaptureChest() =>
        _chest is { } chest ? new ChestSave(chest.Cell.X, chest.Cell.Y, chest.Cell.Z, chest.Items.Select(static i => new SlotSave(i.Item, i.Count)).ToArray()) : null;

    // A save's chest back in: only the state, the block is in the world the save restored. A load never takes a chest away.
    internal void RestoreChest(ChestSave? save)
    {
        _chest = null;
        if (save is null)
        {
            return;
        }

        var chest = new BelongingsChest(new BlockPos(save.X, save.Y, save.Z));
        foreach (var stack in save.Stacks)
        {
            chest.Add(stack.Item, stack.Count);
        }

        _chest = chest;
    }

    // Called after a tick of the needs: at zero health the player dies (only in active play: StepNeeds returns before it otherwise).
    private void DieIfDead()
    {
        if (!Needs.IsDead)
        {
            return;
        }

        var drive = Needs.DeathCause ?? (Needs.Value(NeedMeter.Thirst) <= Needs.Value(NeedMeter.Hunger) ? Drive.Thirst : Drive.Hunger);
        var drowned = Simulation.Breath.IsEmpty; // the last breath is gone: the water took the last health (water-plan 4.4)
        var cause = drowned ? DrownedCause : Data.Needs.Of(drive).Detail;
        var feet = Simulation.Player.FeetBlock;
        LogWorld(WorldEvents.PlayerDied, null, cause);
        DeathCount++;

        var relaxed = RelaxedNeeds;
        if (!relaxed)
        {
            LeaveBelongings(feet); // the relaxed setting faints instead (N05): the bag is kept and no chest is left
        }

        Needs.Wake();
        SettleNeedStages();
        WakeAtSleepPlace();
        LogWorld(WorldEvents.PlayerWoke, null, relaxed ? "faint" : "death");
        DeathLine = Locale.Ui(drowned ? (relaxed ? FaintedDrownedHint : DiedDrownedHint)
            : relaxed ? (drive == Drive.Thirst ? FaintedThirstHint : FaintedHungerHint) : (drive == Drive.Thirst ? DiedThirstHint : DiedHungerHint));
        _hints.Add(DeathLine);
        if (_chest is { } chest && Pointer is null)
        {
            var label = Data.Words.HudLine(BelongingsHud) ?? Locale.Ui(BelongingsLabel); // the beacon's labels are Spanish, as the camp's and the river's are (N05)
            Pointer = new PointerMarker(label, new GuideCell(chest.Cell.X, chest.Cell.Y, chest.Cell.Z));
            LogWorld(WorldEvents.BeaconSet, null, $"{label} at {chest.Cell.X},{chest.Cell.Y},{chest.Cell.Z}");
        }
    }

    // Everything in the bag but the items that stack to one goes into a chest on the nearest free ground cell. A second death before the night moves the first chest's contents into
    // the new one (one chest at a time); a bag with nothing to leave leaves any chest as it was.
    private void LeaveBelongings(BlockPos feet)
    {
        var bag = Simulation.Inventory;
        var left = new List<(ItemId Item, int Count)>();
        for (var slot = 0; slot < Inventory.SlotCount; slot++)
        {
            var stack = bag.Slots[slot];
            if (!stack.IsEmpty && ItemDefinition.Get(stack.Item).MaxStack > 1)
            {
                left.Add((stack.Item, stack.Count));
                bag.SetSlot(slot, ItemStack.Empty);
            }
        }

        if (left.Count == 0)
        {
            return;
        }

        var old = _chest;
        RemoveChestBlock();
        var cell = NearestFreeGround(feet, ChestSearchBlocks * ChestSearchBlocks, 0, ChestSearchHeight, avoid: null, reach: null) ?? feet;
        Scene.Grid.SetBlock(cell.X, cell.Y, cell.Z, BlockId.Chest);
        var chest = new BelongingsChest(cell);
        if (old is not null)
        {
            foreach (var (item, count) in old.Items)
            {
                chest.Add(item, count);
            }
        }

        foreach (var (item, count) in left)
        {
            chest.Add(item, count);
        }

        _chest = chest;
    }

    private void RemoveChestBlock()
    {
        if (_chest is { } chest && Scene.Grid.InBounds(chest.Cell.X, chest.Cell.Y, chest.Cell.Z) && Scene.Grid.GetBlock(chest.Cell.X, chest.Cell.Y, chest.Cell.Z) == BlockId.Chest)
        {
            Scene.Grid.SetBlock(chest.Cell.X, chest.Cell.Y, chest.Cell.Z, BlockId.Air);
        }

        if (_chest is { } gone && Pointer is { Quiz: false } pointer && pointer.Target.ToBlock() == gone.Cell)
        {
            DismissPointer();
        }

        _chest = null;
    }

    // The player wakes at the place last slept at, or else the sleep place death.wakeAt names (the mountain fire's camp; the ring is built and cold until its torch is put on): the nearest free ground two to five blocks
    // from the place's cell (WakeNearSquared, WakeFarSquared) that is reached on foot from it (Y05a), which is within the rest radius of a ring's middle. A scene without the place leaves the player where they are.
    private void WakeAtSleepPlace()
    {
        var last = LastSleepPlace is { } slept ? SleepPlaces.Find(slept.Id) : null; // Y02: the place last slept at, while it is still a place; else the one death.wakeAt names
        if ((last ?? SleepPlaces.WakeAt) is not { } place)
        {
            return;
        }

        // Y05a: the cell is one the player walks to from the place (a bounded walk, so not through a wall), two to five blocks across from it as before (a fire's ring, a bed's side).
        // No standing room at the place, or no cell reached, falls back to the straight search.
        var reach = SleepWalk.StartFor(Scene.Grid, place.Cell) is { } start ? SleepWalk.ReachFrom(Scene.Grid, start, WakeWalkSteps) : null;
        var cell = (reach is null ? null : NearestFreeGround(place.Cell, WakeFarSquared, WakeNearSquared, 3, _chest?.Cell, reach))
            ?? NearestFreeGround(place.Cell, WakeFarSquared, WakeNearSquared, 3, _chest?.Cell, null);
        if (cell is { } stand)
        {
            Simulation.TeleportTo(stand);
        }
    }

    /// <summary>
    /// A chest taken from: E at the chest puts what the bag can hold back in it, kind by kind. The rest stays, with a line. An emptied chest goes. Raises
    /// <see cref="WorldEvents.BelongingsRecovered"/> (detail <c>all</c> or <c>some</c>) when anything came back.
    /// </summary>
    public NeedActionResult TakeBelongings()
    {
        if (!AimedAtChest || _chest is not { } chest)
        {
            return NeedActionResult.Nothing;
        }

        var taken = chest.TakeInto(Simulation.Inventory);
        if (taken == 0)
        {
            _hints.Add(Locale.Ui(ChestRestHint));
            return NeedActionResult.Kept;
        }

        if (chest.IsEmpty)
        {
            RemoveChestBlock();
            LogWorld(WorldEvents.BelongingsRecovered, null, "all");
        }
        else
        {
            _hints.Add(Locale.Ui(ChestRestHint));
            LogWorld(WorldEvents.BelongingsRecovered, null, "some");
        }

        return NeedActionResult.Done;
    }

    // A night takes the chest with whatever is left in it (a load never does).
    private void RemoveChestAtNight() => RemoveChestBlock();

    // The nearest cell to a middle (by distance across, then height, then x, then z) between two squared distances across that is free ground: air with a solid, dry, bare block
    // under it (not leaves, not a fire ring's stone or a torch) and room for a body (a second cell free over it). Never the avoided cell and never a ring's middle.
    private BlockPos? NearestFreeGround(BlockPos middle, int maxSquared, int minSquared, int height, BlockPos? avoid, Dictionary<BlockPos, int>? reach)
    {
        var grid = Scene.Grid;
        var radius = (int)Math.Ceiling(Math.Sqrt(maxSquared));
        BlockPos? best = null;
        (long D, int Dy, int X, int Z) bestKey = default;
        var middles = Fires.Middles;
        for (var x = middle.X - radius; x <= middle.X + radius; x++)
        {
            for (var z = middle.Z - radius; z <= middle.Z + radius; z++)
            {
                long dx = x - middle.X;
                long dz = z - middle.Z;
                var d = (dx * dx) + (dz * dz);
                if (d > maxSquared || d < minSquared)
                {
                    continue;
                }

                for (var y = middle.Y - height; y <= middle.Y + height; y++)
                {
                    var cell = new BlockPos(x, y, z);
                    if (!IsFreeGround(grid, cell) || cell == avoid || middles.Contains(cell) || (reach is not null && !reach.ContainsKey(cell)))
                    {
                        continue;
                    }

                    var key = (d, Math.Abs(y - middle.Y), x, z);
                    if (best is null || key.CompareTo(bestKey) < 0)
                    {
                        best = cell;
                        bestKey = key;
                    }
                }
            }
        }

        return best;
    }

    private static bool IsFreeGround(VoxelGrid grid, BlockPos cell)
    {
        if (!grid.InBounds(cell.X, cell.Y - 1, cell.Z) || !grid.InBounds(cell.X, cell.Y + 1, cell.Z))
        {
            return false;
        }

        var below = grid.GetBlock(cell.X, cell.Y - 1, cell.Z);
        if (!BlockDefinition.IsSolid(below) || BlockDefinition.IsTall(below) || BlockDefinition.IsLeaves(below) || below is BlockId.Water or BlockId.FireRing or BlockId.Torch or BlockId.PostTorch or BlockId.Chest)
        {
            return false;
        }

        return grid.GetBlock(cell.X, cell.Y, cell.Z) == BlockId.Air && !grid.IsSolid(cell.X, cell.Y + 1, cell.Z);
    }
}
