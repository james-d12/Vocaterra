using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>
/// W03: the fail-forward is the NPC file's <c>patience</c> rule, read by <see cref="NpcService"/> for any NPC at the prompt the rule names. The stall's own tests
/// (<see cref="NpcServiceTests"/>) pin the shipped rule; these give the same stall other numbers, and take the rule away.
/// </summary>
public sealed class NpcPatienceTests
{
    private static readonly NpcPatienceRule Shipped = new(4, 2, "shopkeeper.give", "stall-need", true);

    private static VoxelSession AtStall(NpcPatienceRule? rule)
    {
        var shopkeeper = TestGame.Data.Npcs.Find("shopkeeper")! with { Patience = rule };
        var roster = NpcRoster.Of([.. TestGame.Data.Npcs.All.Select(npc => npc.Id == "shopkeeper" ? shopkeeper : npc)]);
        var session = new VoxelSession(TestGame.Data.WithNpcs(roster), TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), CreditRules.FullTyped, TestGame.NoLessons);
        Stage.TeleportToNpc(session, "shopkeeper");
        _ = session.OpenTalk("shopkeeper");
        return session;
    }

    [Fact]
    public void TheShopkeepersRuleIsTheShippedDataAndNoOtherNpcHasOne()
    {
        Assert.Equal(Shipped, TestGame.Data.Npcs.Find("shopkeeper")!.Patience);
        Assert.All(TestGame.Data.Npcs.All.Where(static npc => npc.Id != "shopkeeper"), static npc => Assert.Null(npc.Patience));
    }

    [Fact]
    public void TheRulesMissesAreTheDatasNotACodeConstant()
    {
        var session = AtStall(Shipped with { Misses = 2, AfterHelp = 2 });

        Assert.False(session.Say("zzz qqq").FailForward);
        var second = session.Say("zzz qqq");

        Assert.True(second.FailForward);
        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Bottle));
    }

    [Fact]
    public void TheRulesAfterHelpShortensTheWaitOnlyOnceHelpWasAsked()
    {
        var plain = AtStall(Shipped with { AfterHelp = 1 });
        Assert.False(plain.Say("zzz qqq").FailForward); // one miss, no help asked: the four are still the count

        var helped = AtStall(Shipped with { AfterHelp = 1 });
        var turn = helped.Say("ayuda");

        Assert.True(turn.FailForward); // the help line is the first miss and already enough
        Assert.Contains("shopkeeper.give", helped.Conversation.Records.Select(static r => r.HostTurn?.HostEvent));
    }

    [Fact]
    public void AnNpcWithNoRuleNeverFailsForward()
    {
        var session = AtStall(null);
        GuideTurn last = null!;
        for (var i = 0; i < 8; i++)
        {
            last = session.Say(i % 2 == 0 ? "zzz qqq" : "ayuda");
            Assert.False(last.FailForward);
        }

        Assert.Equal(0, session.Simulation.Inventory.Count(ItemId.Bottle));
        Assert.DoesNotContain(session.Conversation.Records, static r => r.HostTurn?.HostEvent == "shopkeeper.give");
    }

    [Fact]
    public void ARuleAtAnotherPromptIsNotCountedAtTheStallsNeed()
    {
        var session = AtStall(Shipped with { Prompt = "stall-offer" });

        for (var i = 0; i < 6; i++)
        {
            Assert.False(session.Say("zzz qqq").FailForward);
        }
    }

    [Fact]
    public void AOnceRuleFiresOnlyWhileTheBottleIsNotGivenAndASkippedBottleHoldsIt()
    {
        var once = AtStall(Shipped with { Misses = 1, AfterHelp = 1 });
        once.Conversation.SkipShopkeeperBottle(); // the bottle was given before (a skipped session, or a save)
        Assert.False(once.Say("zzz qqq").FailForward);

        var repeating = AtStall(Shipped with { Misses = 1, AfterHelp = 1, Once = false });
        repeating.Conversation.SkipShopkeeperBottle();
        Assert.True(repeating.Say("zzz qqq").FailForward);
    }

    [Fact]
    public void ASaveKeepsTheBottleGivenAndTheRuleIsNotSaved()
    {
        var session = AtStall(Shipped);
        var turn = session.Say("quiero agua, por favor");
        Assert.Contains("stall.give_water", turn.HostEvents);
        session.EndTalk();

        var text = SaveGame.Save(session, TestGame.Route());
        var loaded = SaveGame.Load(text, TestGame.Data, TestGame.Route, credit: CreditRules.FullTyped, bumpSession: true);

        Assert.True(loaded.Conversation.CaptureAgent("shopkeeper").BottleGiven);
        Assert.DoesNotContain("atience", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheValidatorRejectsEachBrokenRule()
    {
        var shopkeeper = TestGame.Data.Npcs.Find("shopkeeper")!;
        string[] Problems(NpcPatienceRule rule) =>
            [.. GameDataValidator.Validate(TestGame.Data.WithNpcs(NpcRoster.Of([.. TestGame.Data.Npcs.All.Select(npc => npc.Id == "shopkeeper" ? shopkeeper with { Patience = rule } : npc)])))];

        Assert.Empty(Problems(Shipped));
        Assert.Contains("game/npcs/shopkeeper.json: the patience's misses (0) and afterHelp (2) must both be 1 or more.", Problems(Shipped with { Misses = 0 }));
        Assert.Contains("game/npcs/shopkeeper.json: the patience's misses (4) and afterHelp (0) must both be 1 or more.", Problems(Shipped with { AfterHelp = 0 }));
        Assert.Contains("game/npcs/shopkeeper.json: the patience's afterHelp (5) is more than its misses (4); help can only shorten the wait.", Problems(Shipped with { AfterHelp = 5 }));
        Assert.Contains("game/npcs/shopkeeper.json: the patience's event 'shopkeeper.nope' is taken by no state of agents/shopkeeper.json (onHostEvent), so the NPC could not fail forward.", Problems(Shipped with { Event = "shopkeeper.nope" }));
        Assert.Contains("game/npcs/shopkeeper.json: the patience applies at 'villager-open', which is not one of the NPC's prompts.", Problems(Shipped with { Prompt = "villager-open" }));
    }
}
