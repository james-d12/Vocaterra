using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Session;

// Food that comes back, and fair gifts (N06, adaptation design 9.6): the berry bushes picked by hand grow back after the next night, and the NPCs' bread and water are given once
// per in-game day each. The rules are game/needs.json's (Regrowth, Gifts); the night is RunDayResets. Both follow the needs: with --needs off there is no food economy, so nothing
// is recorded, nothing grows back and every gift is given (the scripted runs keep their baselines).
public sealed partial class VoxelSession
{
    // The cells of the regrowing blocks gathered since the last night, in the order they were gathered; each holds the gathered block (a plain bush) until the night.
    private readonly List<BlockPos> _picked = [];

    // The keys (GiftRule.Key) of the gifts given since the last night.
    private readonly HashSet<string> _giftsGiven = new(StringComparer.Ordinal);

    /// <summary>The cells of the berry bushes picked and not yet grown back, in the order they were picked.</summary>
    public IReadOnlyList<BlockPos> PickedBushes => _picked;

    /// <summary>The keys of the gifts given today (<c>shopkeeper:bread</c>), ordinal.</summary>
    public IReadOnlyCollection<string> GiftsGivenToday => _giftsGiven.Order(StringComparer.Ordinal).ToList();

    /// <summary>Whether the NPC's item is a gift given already today and no lesson step requires it: it is not given again before a night.</summary>
    /// <param name="npc">The agent id.</param>
    /// <param name="item">The item.</param>
    public bool GiftClosed(string npc, ItemId item) =>
        NeedsEnabled && Data.Needs.GiftOf(npc, Data.Items.Get(item).Name) is { } rule && _giftsGiven.Contains(rule.Key) && !GiftRequired(npc);

    /// <inheritdoc />
    protected override IReadOnlySet<string>? ClosedGifts =>
        NeedsEnabled && _giftsGiven.Count > 0 ? _giftsGiven.Where(key => !GiftRequired(key[..key.IndexOf(':', StringComparison.Ordinal)])).ToHashSet(StringComparer.Ordinal) : null;

    // Whether the lesson under way waits for this NPC to give an item (a do step on item_given at the NPC): the gift is then always given, and it does not count for the day.
    private bool GiftRequired(string npc) =>
        Lessons is { Engaged: true, Current: { Type: LessonStepType.Do, Trigger: { Event: WorldEvents.ItemGiven } trigger } }
        && string.Equals(trigger.Npc, npc, StringComparison.Ordinal);

    // An NPC's gift was made: a gift of the rules counts for the day, unless a lesson step required it.
    private void CountGift(string npc, ItemId item)
    {
        if (NeedsEnabled && Data.Needs.GiftOf(npc, Data.Items.Get(item).Name) is { } rule && !GiftRequired(npc))
        {
            _ = _giftsGiven.Add(rule.Key);
        }
    }

    // A block was gathered by hand: one the rules grow back is remembered until the next night.
    private void NoteGathered(BlockId block, int x, int y, int z)
    {
        if (NeedsEnabled && Data.Needs.Regrowth.Any(r => string.Equals(r.Block, BlockDefinition.Get(block).Name, StringComparison.Ordinal)))
        {
            var cell = new BlockPos(x, y, z);
            if (!_picked.Contains(cell))
            {
                _picked.Add(cell);
            }
        }
    }

    // The night's part of the renewal (RunDayResets): each picked bush still a plain bush is a berry bush again, the gifts are available again. A cell the player has put
    // something else on is let go. With the needs off nothing was recorded.
    internal void RenewAtNight()
    {
        var grid = Scene.Grid;
        foreach (var cell in _picked)
        {
            var current = grid.InBounds(cell.X, cell.Y, cell.Z) ? grid.GetBlock(cell.X, cell.Y, cell.Z) : BlockId.Air;
            if (BlockDefinition.All.FirstOrDefault(d => d.Gatherable && d.BecomesWhenGathered == current && Data.Needs.Regrowth.Any(r => string.Equals(r.Block, d.Name, StringComparison.Ordinal))) is { } grown)
            {
                grid.SetBlock(cell.X, cell.Y, cell.Z, grown.Id);
            }
        }

        _picked.Clear();
        _giftsGiven.Clear();
    }

    /// <summary>The picked bushes for the save, or <see langword="null"/> for none.</summary>
    internal BushSave[]? CaptureBushes() => _picked.Count == 0 ? null : _picked.Select(static c => new BushSave(c.X, c.Y, c.Z)).ToArray();

    /// <summary>The gifts given today for the save, ordinal, or <see langword="null"/> for none.</summary>
    internal string[]? CaptureGifts() => _giftsGiven.Count == 0 ? null : _giftsGiven.Order(StringComparer.Ordinal).ToArray();

    // A save's picked bushes and used gifts back in: the bushes' blocks are in the world the save restored. A save before N06 has neither.
    internal void RestoreRenewal(BushSave[]? bushes, string[]? gifts)
    {
        _picked.Clear();
        foreach (var bush in bushes ?? [])
        {
            var cell = new BlockPos(bush.X, bush.Y, bush.Z);
            if (Scene.Grid.InBounds(cell.X, cell.Y, cell.Z) && !_picked.Contains(cell))
            {
                _picked.Add(cell);
            }
        }

        _giftsGiven.Clear();
        foreach (var gift in gifts ?? [])
        {
            if (Data.Needs.Gifts.Any(g => string.Equals(g.Key, gift, StringComparison.Ordinal)))
            {
                _ = _giftsGiven.Add(gift);
            }
        }
    }
}
