using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Npc;

namespace Vocaterra.Game.Core.Session;

// The test aid --skip-to-session (V12c, SessionSkip): the 3D session's part of putting a new game where a learner who played the earlier sessions would stand.
public sealed partial class VoxelSession
{
    /// <summary>
    /// Session 1's ending as the objectives describe it (V12c, <see cref="SessionSkip"/>): every step of the objectives the ending waits for that a save counts is counted to
    /// its target, lodging is given at the inn (<see cref="GrantLodging"/>, Y07: the bed is usable), and the stage follows from the
    /// world and the lessons as a load works it out (<see cref="RestoreStage"/>): the guide met, the session complete, and the completion hooks put back without
    /// acting (no arrow, no line). The lessons of session 1 are counted first, so the guide counts as met. A step that counts a bottle filled (the camp's water) leaves
    /// that many bottles of water in the bag (V13): the shopkeeper gave the bottle and the river filled it, and nothing in the game empties a bottle, so a learner who played
    /// the session carries it (the farmer's order of session 7 takes it). The shopkeeper counts it as given and his key words as taught (V09).
    /// </summary>
    /// <exception cref="InvalidOperationException">The bag cannot hold the bottles.</exception>
    internal void SkipToEnding()
    {
        foreach (var objective in Objectives.Where(static o => o.Spec.EndsSession))
        {
            foreach (var step in objective.Spec.Steps.Where(static s => s.Restore is null))
            {
                objective.Set(step.Id, step.Target);
                if (string.Equals(step.Trigger.Event, WorldEvents.BottleFilled, StringComparison.Ordinal))
                {
                    if (Simulation.GrantItems(ItemId.WaterBottle, step.Target) > 0)
                    {
                        throw new InvalidOperationException("The bag cannot hold the bottle of water the camp's step filled.");
                    }

                    // The shopkeeper gave that bottle, at a first visit that taught his key words (V09): without his part a later visit failed forward with a
                    // second bottle and a use of his phrase the learner never made, and his key words stood alone again.
                    MarkKeyWordsTaught(NpcService.ShopkeeperAgent);
                    Conversation.SkipShopkeeperBottle();
                }
            }
        }

        GrantLodging(); // the room at the inn, raising lodging_granted as a real grant does: the bed is usable and inn-room counts

        RestoreStage();
    }

    /// <summary>
    /// Delivers an order as a learner who gathered its goods would (V12c, <see cref="SessionSkip"/>): what the bag lacks of the goods comes in, as the play gathered it (the
    /// wood, the fruit), and the NPC takes them and pays as when the player walks up with them (<see cref="GuideSession.DeliverWithoutTalk"/>). Coins are never gathered: only
    /// an NPC gives them, so the coins an order asks for are those the earlier sessions left in the bag (V13: the axe's two, which leave none for session 7), and goods the bag
    /// holds already are the ones taken (the camp's bottle of water, for the farmer).
    /// </summary>
    /// <param name="order">The order (an objective with <c>deliverTo</c>).</param>
    /// <exception cref="InvalidOperationException">The bag cannot hold the goods, the earlier sessions leave too few coins for them, or the NPC does not take the delivery.</exception>
    internal void SkipDeliver(Objective order)
    {
        foreach (var goods in order.Spec.Goods)
        {
            var missing = goods.Count - Simulation.Inventory.Count(goods.Item);
            if (missing <= 0)
            {
                continue;
            }

            if (goods.Item == ItemId.Coin)
            {
                throw new InvalidOperationException($"The sessions before leave {Simulation.Inventory.Count(ItemId.Coin)} coins, and the order '{order.Id}' asks for {goods.Count}.");
            }

            if (Simulation.GrantItems(goods.Item, missing) > 0)
            {
                throw new InvalidOperationException($"The bag cannot hold the goods of the order '{order.Id}'.");
            }
        }

        if (!DeliverWithoutTalk(order.Spec.DeliverTo!, order.Id))
        {
            throw new InvalidOperationException($"{order.Spec.DeliverTo} does not take the order '{order.Id}'.");
        }
    }

    /// <summary>A purchase a lesson makes (V12c, <see cref="SessionSkip"/>): the NPC sells as a sale at its stall does, the coins for the items.</summary>
    /// <param name="purchase">The purchase.</param>
    /// <exception cref="InvalidOperationException">
    /// The purchase cannot be made: the sessions before leave too few coins for it, or the bag has no room for the items (V19, the review's risk: the skip went on quietly,
    /// and only a later order's coins caught the shortfall).
    /// </exception>
    internal void SkipBuy(SessionSkip.Purchase purchase)
    {
        var cost = (int)Math.Min((long)purchase.Count * purchase.Price, int.MaxValue);
        if (!Sell(purchase.Npc, purchase.Item, purchase.Count, cost, "request"))
        {
            var item = Nouns.ForItem(purchase.Item)?.Lemma ?? ItemDefinition.Get(purchase.Item).Name;
            var coins = Simulation.Inventory.Count(ItemId.Coin);
            if (coins < cost)
            {
                throw new InvalidOperationException($"The skip cannot make session {Day}'s purchase of {purchase.Count} {item} from {purchase.Npc}: the sessions before leave {coins} coins, and it costs {cost}.");
            }

            throw new InvalidOperationException($"The skip cannot make session {Day}'s purchase of {purchase.Count} {item} from {purchase.Npc}: the bag has no room for them.");
        }
    }

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
