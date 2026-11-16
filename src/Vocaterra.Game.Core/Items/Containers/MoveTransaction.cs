namespace Vocaterra.Game.Core.Items.Containers;

/// <summary>The puts of one move, made one by one and undone together when a later one is refused, so a move is all or nothing over any containers (I02). Each put is judged by its container against the state the earlier ones made.</summary>
internal sealed class MoveTransaction
{
    private readonly List<(ISlotContainer Container, int Slot, ItemStack Old)> _made = [];

    /// <summary>Puts <paramref name="stack"/> in the slot when the container takes it; records the old stack for <see cref="Undo"/>.</summary>
    public MoveResult Put(ISlotContainer container, int slot, ItemStack stack)
    {
        var verdict = container.CanPut(slot, stack);
        if (!verdict.IsDone)
        {
            return verdict;
        }

        _made.Add((container, slot, container.Get(slot)));
        container.Put(slot, stack);
        return MoveResult.Done;
    }

    /// <summary>Puts every old stack back, last put first.</summary>
    public void Undo()
    {
        for (var i = _made.Count - 1; i >= 0; i--)
        {
            var (container, slot, old) = _made[i];
            container.Put(slot, old);
        }

        _made.Clear();
    }
}
