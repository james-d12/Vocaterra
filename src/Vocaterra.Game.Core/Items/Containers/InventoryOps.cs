namespace Vocaterra.Game.Core.Items.Containers;

/// <summary>
/// The moves by hand over any two containers (I02, inventory-plan 3.9): left click, right click, shift-click, sort. Every move works through <see cref="ISlotContainer"/> and the <see cref="HeldStack"/> only, so a chest (W24) needs
/// no edit here. Each move is all or nothing: a refused one leaves every container and the held stack as they were and returns a <see cref="MoveResult"/> the panel can show. Everything is integer arithmetic, so the same moves
/// give the same contents on every machine.
/// </summary>
public sealed class InventoryOps
{
    private readonly SlotContainers _containers;
    private readonly HeldStack _held;
    private readonly IItemCatalogue _items;
    private readonly Dictionary<ItemId, int> _order = [];

    /// <summary>Creates the moves over a set of containers and the cursor.</summary>
    /// <param name="containers">The containers a move can name; the bag (<see cref="ContainerIds.Bag"/>) is where quick moves and returns go.</param>
    /// <param name="held">The held stack.</param>
    /// <param name="items">The item catalogue: stack limits and the sort order.</param>
    public InventoryOps(SlotContainers containers, HeldStack held, IItemCatalogue items)
    {
        ArgumentNullException.ThrowIfNull(containers);
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(items);
        _containers = containers;
        _held = held;
        _items = items;
        for (var i = 0; i < items.All.Count; i++)
        {
            _order[items.All[i].Id] = i;
        }
    }

    /// <summary>The held stack.</summary>
    public HeldStack Held => _held;

    /// <summary>Does <paramref name="action"/> at a container and slot.</summary>
    /// <param name="action">The move.</param>
    /// <param name="container">The container's id (ignored by sort and return).</param>
    /// <param name="slot">The slot (ignored by sort and return).</param>
    public MoveResult Apply(MoveAction action, int container, int slot) => action switch
    {
        MoveAction.LeftClick => LeftClick(container, slot),
        MoveAction.RightClick => RightClick(container, slot),
        MoveAction.QuickMove => QuickMove(container, slot),
        MoveAction.Sort => Sort(),
        MoveAction.ReturnHeld => ReturnHeld(),
        _ => MoveResult.Refused(MoveReason.NoSuchAction),
    };

    /// <summary>Left click: pick up the whole stack, put the held one down, swap with what is there, or merge into a stack of the same item (the rest stays held). A trash slot takes the held stack and the old stack there is gone.</summary>
    /// <param name="container">The container's id.</param>
    /// <param name="slot">The slot.</param>
    public MoveResult LeftClick(int container, int slot)
    {
        if (!Locate(container, slot, out var c, out var failure))
        {
            return failure;
        }

        var there = c.Get(slot);
        var held = _held.Stack;
        if (there.IsEmpty && held.IsEmpty)
        {
            return MoveResult.Refused(MoveReason.NothingToMove);
        }

        var tx = new MoveTransaction();
        if (held.IsEmpty)
        {
            return PickUp(tx, c, slot, there, there.Count);
        }

        if (there.IsEmpty || c.Discards)
        {
            var put = tx.Put(c, slot, held);
            if (!put.IsDone)
            {
                return put;
            }

            _held.Put(0, ItemStack.Empty);
            return MoveResult.Done;
        }

        var limit = _items.Get(held.Item).MaxStack;
        if (there.CanMergeWith(held) && there.Count < limit)
        {
            var moved = Math.Min(limit - there.Count, held.Count);
            var merged = tx.Put(c, slot, there.WithCount(there.Count + moved));
            if (!merged.IsDone)
            {
                return merged;
            }

            _held.Put(0, held.WithCount(held.Count - moved));
            return MoveResult.Done;
        }

        var swap = tx.Put(c, slot, held);
        if (!swap.IsDone)
        {
            return swap;
        }

        _held.Put(0, there);
        _held.SetOrigin(c.Id, slot);
        return MoveResult.Done;
    }

    /// <summary>Right click: pick up the larger half (one of one); with a held stack, put one down into an empty slot or a stack of the same item.</summary>
    /// <param name="container">The container's id.</param>
    /// <param name="slot">The slot.</param>
    public MoveResult RightClick(int container, int slot)
    {
        if (!Locate(container, slot, out var c, out var failure))
        {
            return failure;
        }

        var there = c.Get(slot);
        var held = _held.Stack;
        var tx = new MoveTransaction();
        if (held.IsEmpty)
        {
            return there.IsEmpty ? MoveResult.Refused(MoveReason.NothingToMove) : PickUp(tx, c, slot, there, (there.Count + 1) / 2);
        }

        if (!there.IsEmpty)
        {
            if (!there.CanMergeWith(held))
            {
                return MoveResult.Refused(MoveReason.DifferentItem);
            }

            if (there.Count >= _items.Get(there.Item).MaxStack)
            {
                return MoveResult.Refused(MoveReason.SlotFull);
            }
        }

        var put = tx.Put(c, slot, held.WithCount(there.IsEmpty ? 1 : there.Count + 1));
        if (!put.IsDone)
        {
            return put;
        }

        _held.Put(0, held.WithCount(held.Count - 1));
        return MoveResult.Done;
    }

    /// <summary>
    /// Shift-click: a stack of the bag that a container claims (a wearable) goes to that container's slot, the old stack there taking its place; any other stack of the bag goes from the hotbar to the pack or from the pack to the
    /// hotbar, merging into stacks of the same item first. A stack in any other container goes to the bag. What does not fit stays where it was; nothing fitting at all is <see cref="MoveReason.NoRoom"/>.
    /// </summary>
    /// <param name="container">The container's id.</param>
    /// <param name="slot">The slot.</param>
    public MoveResult QuickMove(int container, int slot)
    {
        if (!Locate(container, slot, out var c, out var failure))
        {
            return failure;
        }

        var stack = c.Get(slot);
        if (stack.IsEmpty)
        {
            return MoveResult.Refused(MoveReason.NothingToMove);
        }

        if (_containers.Find(ContainerIds.Bag) is not { } bag)
        {
            return MoveResult.Refused(MoveReason.NoSuchContainer);
        }

        var tx = new MoveTransaction();
        if (c.Id != ContainerIds.Bag)
        {
            return Relocate(tx, c, slot, stack, bag, 0, int.MaxValue);
        }

        foreach (var candidate in _containers.All)
        {
            if (candidate is IQuickTarget target && target.Claims(stack, out var into, out var refusal))
            {
                if (into < 0)
                {
                    return refusal;
                }

                var old = target.Get(into);
                var put = tx.Put(target, into, stack);
                if (!put.IsDone)
                {
                    return put;
                }

                _ = tx.Put(bag, slot, old); // the worn item takes the place the new one left
                return MoveResult.Done;
            }
        }

        return slot < Inventory.HotbarSize
            ? Relocate(tx, c, slot, stack, bag, Inventory.HotbarSize, int.MaxValue)
            : Relocate(tx, c, slot, stack, bag, 0, Inventory.HotbarSize);
    }

    /// <summary>Sorts the pack (the bag's slots after the hotbar, which is left alone): same-item stacks merged to their limit, then in catalogue order, then count descending. Always done; the bag may be all empty.</summary>
    public MoveResult Sort()
    {
        if (_containers.Find(ContainerIds.Bag) is not { } bag)
        {
            return MoveResult.Refused(MoveReason.NoSuchContainer);
        }

        var totals = new SortedDictionary<(int Order, int Wear), (ItemId Item, int Count)>();
        for (var i = Inventory.HotbarSize; i < bag.SlotCount; i++)
        {
            var stack = bag.Get(i);
            if (stack.IsEmpty)
            {
                continue;
            }

            var key = (_order.GetValueOrDefault(stack.Item, int.MaxValue - (int)stack.Item), stack.Wear); // stacks of one wear merge; a worn item keeps its wear (I07)
            totals[key] = totals.TryGetValue(key, out var seen) ? (stack.Item, seen.Count + stack.Count) : (stack.Item, stack.Count);
        }

        var sorted = new List<ItemStack>();
        foreach (var ((_, wear), (item, count)) in totals)
        {
            var limit = _items.Get(item).MaxStack;
            for (var left = count; left > 0; left -= Math.Min(left, limit))
            {
                sorted.Add(new ItemStack(item, Math.Min(left, limit), wear));
            }
        }

        for (var i = Inventory.HotbarSize; i < bag.SlotCount; i++)
        {
            var next = i - Inventory.HotbarSize;
            bag.Put(i, next < sorted.Count ? sorted[next] : ItemStack.Empty);
        }

        return MoveResult.Done;
    }

    /// <summary>
    /// Returns the held stack: to the slot it came from when that is empty and takes it, else into the bag (merging, then the first free slot). What does not fit stays held (the bag is full), so the stack is never lost.
    /// </summary>
    /// <returns>Done when nothing is held any more, <see cref="MoveReason.NoRoom"/> when some of it still is. Holding nothing is Done.</returns>
    public MoveResult ReturnHeld()
    {
        var held = _held.Stack;
        if (held.IsEmpty)
        {
            return MoveResult.Done;
        }

        var tx = new MoveTransaction();
        if (_containers.Find(_held.OriginContainer) is { } origin && (uint)_held.OriginSlot < (uint)origin.SlotCount && origin.Get(_held.OriginSlot).IsEmpty && origin.CanPut(_held.OriginSlot, held).IsDone)
        {
            _ = tx.Put(origin, _held.OriginSlot, held);
            _held.Put(0, ItemStack.Empty);
            return MoveResult.Done;
        }

        if (_containers.Find(ContainerIds.Bag) is not { } bag)
        {
            return MoveResult.Refused(MoveReason.NoSuchContainer);
        }

        var rest = PlaceInto(tx, bag, 0, int.MaxValue, held, out var failure);
        if (!failure.IsDone)
        {
            tx.Undo();
            return failure;
        }

        _held.Put(0, rest);
        return rest.IsEmpty ? MoveResult.Done : MoveResult.Refused(MoveReason.NoRoom);
    }

    private MoveResult PickUp(MoveTransaction tx, ISlotContainer c, int slot, ItemStack there, int take)
    {
        var left = there.Count - take;
        var lifted = tx.Put(c, slot, there.WithCount(left));
        if (!lifted.IsDone)
        {
            return lifted;
        }

        _held.Put(0, there.WithCount(take));
        _held.SetOrigin(c.Id, slot);
        return MoveResult.Done;
    }

    // Takes the stack out of its slot and places it in [from, to) of the destination, merging first; what does not fit goes back to the source slot.
    private MoveResult Relocate(MoveTransaction tx, ISlotContainer source, int slot, ItemStack stack, ISlotContainer destination, int from, int to)
    {
        var taken = tx.Put(source, slot, ItemStack.Empty);
        if (!taken.IsDone)
        {
            return taken;
        }

        var rest = PlaceInto(tx, destination, from, to, stack, out var failure);
        if (!failure.IsDone)
        {
            tx.Undo();
            return failure;
        }

        if (rest.Count == stack.Count)
        {
            tx.Undo();
            return MoveResult.Refused(MoveReason.NoRoom);
        }

        if (!rest.IsEmpty)
        {
            var back = tx.Put(source, slot, rest);
            if (!back.IsDone)
            {
                tx.Undo();
                return back;
            }
        }

        return MoveResult.Done;
    }

    // Puts as much of the stack as fits into slots [from, to) of the container: onto stacks of the same item first (lowest slot first), then into empty slots. Returns what is left.
    private ItemStack PlaceInto(MoveTransaction tx, ISlotContainer container, int from, int to, ItemStack stack, out MoveResult failure)
    {
        failure = MoveResult.Done;
        var limit = _items.Get(stack.Item).MaxStack;
        var left = stack.Count;
        var end = Math.Min(to, container.SlotCount);
        for (var pass = 0; pass < 2 && left > 0; pass++)
        {
            for (var i = from; i < end && left > 0; i++)
            {
                var there = container.Get(i);
                if (pass == 0 ? !there.CanMergeWith(stack) || there.Count >= limit : !there.IsEmpty)
                {
                    continue;
                }

                var have = pass == 0 ? there.Count : 0;
                var add = Math.Min(left, limit - have);
                var put = tx.Put(container, i, stack.WithCount(have + add));
                if (!put.IsDone)
                {
                    if (pass == 1)
                    {
                        continue; // this empty slot refuses (a chest's rule): try the next
                    }

                    failure = put;
                    return stack.WithCount(left);
                }

                left -= add;
            }
        }

        return stack.WithCount(left);
    }

    private bool Locate(int container, int slot, out ISlotContainer found, out MoveResult failure)
    {
        failure = MoveResult.Done;
        found = null!;
        if (container == ContainerIds.Held || _containers.Find(container) is not { } c)
        {
            failure = MoveResult.Refused(MoveReason.NoSuchContainer);
            return false;
        }

        if ((uint)slot >= (uint)c.SlotCount)
        {
            failure = MoveResult.Refused(MoveReason.NoSuchSlot);
            return false;
        }

        found = c;
        return true;
    }
}
