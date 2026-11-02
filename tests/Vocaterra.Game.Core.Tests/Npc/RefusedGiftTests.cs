using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Decision.Learning;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>V30 (review): a gift the NPC refused is never made, whether or not the agent has a transition for the miss.</summary>
public sealed class RefusedGiftTests
{
    [Theory]
    [InlineData("no_such_miss")] // an agent with no transition for the miss
    [InlineData(NpcHostEvents.WontSell)]
    public void ARefusedGiftLeavesTheTurnsEvents(string miss)
    {
        var bundle = TestGame.Data.Bundle;
        var now = new StoredTimestamp(1_760_000_000);
        var typed = new TypedUtteranceEvaluator(bundle);
        var table = HostEventTable.Load(bundle);
        var context = ConversationContext.Create(TestGame.Seed, "shopkeeper", bundle.Agents["shopkeeper"].InitialState(), now, LearnerModel.New(), null);
        var result = typed.Evaluate("quiero agua, por favor", bundle.Prompts["stall-need"], context);
        var events = result.Decision.Agent!.Actions.Where(static a => a.HostEvent is not null).Select(static a => a.HostEvent!).ToList();
        Assert.Contains(events, e => table.Of("shopkeeper", e).Kind == NpcEffectKind.GiveItem);

        _ = NpcTurnRules.AnswerMisses("shopkeeper", result, table, typed, context.Next(result, now), now, null, false, effect => effect.Kind == NpcEffectKind.GiveItem ? miss : null, events);

        Assert.DoesNotContain(events, e => table.Of("shopkeeper", e).Kind == NpcEffectKind.GiveItem);
    }
}
