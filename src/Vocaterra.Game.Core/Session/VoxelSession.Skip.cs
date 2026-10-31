using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Session;

// The test aid --skip-to-session (V12c, SessionSkip): the 3D session's part of putting a new game where a learner who played the earlier sessions would stand.
public sealed partial class VoxelSession
{
    /// <summary>
    /// Session 1's ending as the objectives describe it (V12c, <see cref="SessionSkip"/>): every step of the objectives the ending waits for that a save counts is counted to
    /// its target, a torch stands on the rim of the player's own fire ring (the world edit that lights it, as the spare torch placed there), and the stage follows from the
    /// world and the lessons as a load works it out (<see cref="RestoreStage"/>): the guide met, the fire lit, the session complete, and the completion hooks put back without
    /// acting (no spare torch, no arrow). The lessons of session 1 are counted first, so the guide counts as met.
    /// </summary>
    /// <exception cref="InvalidOperationException">The scene has no free cell on the rim of the player's fire ring.</exception>
    internal void SkipToEnding()
    {
        foreach (var objective in Objectives.Where(static o => o.Spec.EndsSession))
        {
            foreach (var step in objective.Spec.Steps.Where(static s => s.Restore is null))
            {
                objective.Set(step.Id, step.Target);
            }
        }

        if (HasPlayerFireRing && !ScanPlayerFireRing())
        {
            var rim = _playerFireRing.Select(static stone => (BlockPos?)new BlockPos(stone.X, stone.Y + 1, stone.Z)).FirstOrDefault(cell => Scene.Grid.GetBlock(cell!.Value.X, cell.Value.Y, cell.Value.Z) == BlockId.Air)
                ?? throw new InvalidOperationException("The player's fire ring has no free cell on its rim for the torch.");
            _ = Scene.Grid.SetBlock(rim.X, rim.Y, rim.Z, BlockId.Torch);
        }

        RestoreStage();
    }

    /// <summary>
    /// Delivers an order as a learner who gathered its goods would (V12c, <see cref="SessionSkip"/>): the goods come into the bag, and the NPC takes them and pays as when
    /// the player walks up with them (<see cref="GuideSession.DeliverWithoutTalk"/>).
    /// </summary>
    /// <param name="order">The order (an objective with <c>deliverTo</c>).</param>
    /// <exception cref="InvalidOperationException">The bag cannot hold the goods, or the NPC does not take the delivery.</exception>
    internal void SkipDeliver(Objective order)
    {
        foreach (var goods in order.Spec.Goods)
        {
            if (Simulation.GrantItems(goods.Item, goods.Count) > 0)
            {
                throw new InvalidOperationException($"The bag cannot hold the goods of the order '{order.Id}'.");
            }
        }

        if (!DeliverWithoutTalk(order.Spec.DeliverTo!, order.Id))
        {
            throw new InvalidOperationException($"{order.Spec.DeliverTo} does not take the order '{order.Id}'.");
        }
    }

    /// <summary>A purchase a lesson makes (V12c, <see cref="SessionSkip"/>): the NPC sells as a sale at its stall does, the coins for the items, or nothing changes hands.</summary>
    /// <param name="purchase">The purchase.</param>
    internal void SkipBuy(SessionSkip.Purchase purchase) => Sell(purchase.Npc, purchase.Item, purchase.Count, purchase.Count * purchase.Price, "request");

    /// <summary>
    /// Puts back the word records and the carded nouns of before the skip and drops the word cards it queued (V12c): an item an NPC gave in a session that was not played
    /// was not met, so no word moves (the skip fakes no practice) and its card shows when the learner first meets it.
    /// </summary>
    /// <param name="words">The word records before the skip.</param>
    /// <param name="carded">The carded nouns before the skip.</param>
    internal void ForgetSkippedWords(IEnumerable<Language.Words.WordRecord> words, IEnumerable<string> carded)
    {
        Words.Restore(words);
        RestoreCarded(carded);
        _newCards.Clear();
    }
}
