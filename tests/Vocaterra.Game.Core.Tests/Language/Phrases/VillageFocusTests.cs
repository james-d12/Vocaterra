using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.Language.Lessons;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Pipeline;

namespace Vocaterra.Game.Core.Tests.Language.Phrases;

/// <summary>
/// Free talk with the village's NPCs (VILLAGE_PLAN V12b): the phrase a turn is credited with, and the cards the panel shows. V09's scripted runs found the baker's
/// "quiero dos panes, por favor" credited as the stall's <c>pedir-agua</c>: her file had no <c>focus</c>, so no phrase was in play at <c>baker-open</c>, and the
/// fixed mapping (<see cref="PhraseIds.For"/>: a request is the stall's phrase whatever the item) took the turn. Each new NPC's file now names the phrases its
/// prompt practises, and a prompt that is the home of a row of the turn's intent credits that row before the fixed mapping, focus or not.
/// </summary>
public sealed class VillageFocusTests
{
    private static VoxelSession At(string npc, GameData? data = null)
    {
        var session = data is null
            ? TestGame.NewSession()
            : new VoxelSession(data, TestGame.Seed, GuideSession.DefaultStartedAt, TestGame.Route(), CreditRules.FullTyped, TestGame.NoLessons);
        _ = session.Simulation.GrantItems(ItemId.Coin, 10); // the baker sells: a request she can fill
        Stage.TeleportToNpc(session, npc);
        _ = session.OpenTalk(npc);
        return session;
    }

    private static PhraseUse Credit(VoxelSession session, string line)
    {
        var before = session.Events.OfType<PhraseUse>().Count();
        var turn = session.Say(line);
        Assert.True(turn.Communicated, $"'{line}' was not understood ({turn.Intent})");
        return Assert.Single(session.Events.OfType<PhraseUse>().Skip(before));
    }

    // The shipped NPCs with these ones' focus taken away (the files as V10 to V12 shipped them).
    private static GameData WithoutFocus(params string[] ids)
    {
        var npcs = TestGame.Data.Npcs.All
            .Select(npc => ids.Contains(npc.Id, StringComparer.Ordinal) ? npc with { Focus = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal) } : npc)
            .ToList();
        return TestGame.Data.WithNpcs(NpcRoster.Of(npcs));
    }

    [Fact]
    public void FreeTalkAtTheBakerCreditsHerOwnRequestNeverTheStalls()
    {
        var session = At("baker");
        Assert.Equal(["quiero-dos-panes", "cuanto-cuesta", "hay-pan"], session.Support.Cards.Select(static c => c.PhraseId));

        var use = Credit(session, "quiero dos panes, por favor");

        Assert.Equal(("quiero-dos-panes", PhraseUseKind.Supported, "baker", "baker-open"), (use.PhraseId, use.Kind, use.Npc, use.Prompt));
        Assert.DoesNotContain(session.Events.OfType<PhraseUse>(), static u => u.PhraseId == PhraseIds.RequestWater);
        Assert.Equal("cuanto-cuesta", Credit(session, "¿cuánto cuesta?").PhraseId);
        Assert.Equal("hay-pan", Credit(session, "¿hay pan?").PhraseId);
    }

    [Theory]
    [InlineData("smith")]
    [InlineData("farmer")]
    public void FreeTalkAtTheSmithAndTheFarmerCreditsTheirQuestionsAtHomeWithEither(string npc)
    {
        var session = At(npc);
        Assert.Equal(npc == "farmer" ? ["como-te-llamas", "que-haces", "quiero-tres-manzanas-rojas"] : ["como-te-llamas", "que-haces"], session.Support.Cards.Select(static c => c.PhraseId)); // his apples since V13
        session.Objective.Finish(); // after the camp a phrase said away from its home is a transfer: both of them are home to these two

        var name = Credit(session, "¿cómo te llamas?");
        var job = Credit(session, "¿qué haces?");

        Assert.Equal(("como-te-llamas", PhraseUseKind.Supported, npc), (name.PhraseId, name.Kind, name.Npc));
        Assert.Equal(("que-haces", PhraseUseKind.Supported, npc), (job.PhraseId, job.Kind, job.Npc));
    }

    [Theory]
    [InlineData("¿dónde está la panadería?", "donde-esta-la-panaderia")]
    [InlineData("¿dónde está la herrería?", "donde-esta-la-herreria")]
    [InlineData("¿dónde está el río?", "donde-esta-el-rio")] // not one of his: the villager's, by the fixed mapping
    public void TheFishersTwoDirectionsAreToldApartByThePlaceAsked(string line, string phrase)
    {
        var session = At("fisher");
        Assert.Equal(["donde-esta-la-panaderia", "donde-esta-la-herreria"], session.Support.Cards.Select(static c => c.PhraseId));

        Assert.Equal(phrase, Credit(session, line).PhraseId);
    }

    [Theory]
    [InlineData("¿dónde está el río?", "donde-esta-el-rio")] // his own
    [InlineData("¿dónde está la panadería?", "donde-esta-la-panaderia")] // the fisher's, a transfer: V12b credited the villager's river
    [InlineData("¿dónde está la herrería?", "donde-esta-la-herreria")]
    public void AtTheVillagerTheDirectionsAreCreditedByThePlaceAsked(string line, string phrase)
    {
        // V12b's rule for one row (V14): the villager practises ¿Dónde está el río?, and a question about another place is the row practised elsewhere that names it.
        var session = At("villager");
        Assert.Equal(["donde-esta-el-rio"], session.Support.Cards.Select(static c => c.PhraseId));
        session.Objective.Finish();

        var use = Credit(session, line);

        Assert.Equal((phrase, phrase == "donde-esta-el-rio" ? PhraseUseKind.Supported : PhraseUseKind.Transfer), (use.PhraseId, use.Kind));
        var skills = session.Conversation.Records.Select(static r => r.Utterance).OfType<ReplayEnvelope>().Last().Context.ExtraSkills ?? [];
        Assert.Contains("phrase." + phrase, skills); // the core's learner model is told the same phrase (NpcService)
    }

    [Fact]
    public void AtTheVillagerAPlaceNoPractisedRowNamesCreditsNone()
    {
        // As at the fisher, whose two rows name neither: crediting the river's row for the shop would be a guess.
        var session = At("villager");
        var before = session.Events.OfType<PhraseUse>().Count();

        var turn = session.Say("¿dónde está la tienda?");

        Assert.Equal(("ask_directions", "tienda"), (turn.Intent, turn.Slots["place"]));
        Assert.Equal(before, session.Events.OfType<PhraseUse>().Count());
    }

    [Fact]
    public void InALessonsStepThePlaceRuleDoesNotTakeTheStepsPhrase()
    {
        // The lesson's step credits its own phrase, whatever the place (the rule is free talk's): El pueblo's "¿Dónde está la herrería?" step at the fisher's "¿Algo más?".
        var session = VillageLessonTests.AtStep("v1-smithy");
        var use = Credit(session, "¿dónde está el pozo?");
        Assert.Equal("donde-esta-la-herreria", use.PhraseId);
    }

    [Fact]
    public void WithoutAFocusATurnCreditsThePromptsOwnRowNotAnotherNpcs()
    {
        var data = WithoutFocus("baker", "smith");
        var baker = At("baker", data);
        Assert.Empty(baker.Support.Cards); // no phrase in play: no card

        Assert.Equal("quiero-dos-panes", Credit(baker, "quiero dos panes, por favor").PhraseId); // her prompt's own row, not the stall's
        var skills = baker.Conversation.Records.Select(static r => r.Utterance).OfType<ReplayEnvelope>().Last().Context.ExtraSkills ?? [];
        Assert.Contains("phrase.quiero-dos-panes", skills); // the core's learner model is told the same phrase (NpcService)
        Assert.DoesNotContain("phrase." + PhraseIds.RequestWater, skills);
        Assert.Equal("como-te-llamas", Credit(At("smith", data), "¿cómo te llamas?").PhraseId);
    }

    [Fact]
    public void ARequestAtAPromptWithNoRequestOfItsOwnIsStillTheStallsPhrase()
    {
        // The fixed mapping stands where the prompt has no row of the intent: "quiero X, por favor" is the stall's phrase, a transfer at the villager (S8).
        var session = At("villager");
        session.Objective.Finish();

        var use = Credit(session, "quiero pan, por favor");

        Assert.Equal((PhraseIds.RequestWater, PhraseUseKind.Transfer, "villager"), (use.PhraseId, use.Kind, use.Npc));
    }
}
