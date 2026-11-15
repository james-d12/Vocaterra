using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Language.Intent;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>
/// The off-prompt lines in the game (village-plan V27, the owner's answer of 2026-11-02): in free talk a line the NPC's prompt does not take but the core understands
/// (<c>Verdict.OffPromptIntent</c>, M72) gets the NPC's one line, «Te entiendo.» and its topic, and nothing else; it is no failed attempt, and the book credits it.
/// </summary>
public sealed class OffPromptTests
{
    private static readonly StoredTimestamp Now = new(GuideSession.DefaultStartedAt + 5);

    private static NpcService NewNpcs() => new(TestGame.Data, new NoFacts(), TestGame.Seed, new StoredTimestamp(GuideSession.DefaultStartedAt));

    private static GuideTurn Free(NpcService npcs, string npc, string line) => npcs.Say(npc, line, new GuideScene(new GuideCell(0, 0, 0)), Now, "hash");

    [Fact]
    public void EveryNpcHasOneOffPromptLineInItsOwnPoolThatStartsWithTeEntiendo()
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["smith"] = "Te entiendo. Yo hago hachas.",
            ["baker"] = "Te entiendo. ¿Quieres pan?",
            ["farmer"] = "Te entiendo. ¿Quieres manzanas?",
            ["fisher"] = "Te entiendo. ¿Buscas un lugar?",
            ["villager"] = "Te entiendo. ¿Qué buscas?",
            ["guide"] = "Te entiendo. ¿Qué quieres aprender?",
            ["shopkeeper"] = "Te entiendo. ¿Quieres comprar algo?",
            ["innkeeper"] = "Te entiendo. La habitación es gratis.",
        };

        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), TestGame.Data.Npcs.Ids.Order(StringComparer.Ordinal));
        foreach (var (id, text) in expected)
        {
            var pool = NpcTurnRules.OffPromptPoolOf(TestGame.Bundle, id);
            Assert.NotNull(pool);
            var variant = Assert.Single(TestGame.Bundle.Dialogue.Pools[pool].Variants);
            Assert.Equal(text, variant.Text);
        }
    }

    [Theory]
    [InlineData("smith", "me gusta el río", "state_like", "Te entiendo. Yo hago hachas.")]
    [InlineData("fisher", "tengo dos manzanas", "state_have", "Te entiendo. ¿Buscas un lugar?")]
    [InlineData("baker", "el río es grande", "describe", "Te entiendo. ¿Quieres pan?")]
    public void AnOffPromptLineGetsTheNpcsOneLineAndNoRepairNoCorrectionNoNoTeEntendi(string npc, string line, string intent, string reply)
    {
        var turn = Free(NewNpcs(), npc, line);

        Assert.Equal([reply], turn.Replies);
        Assert.Equal(intent, turn.OffPromptIntent);
        Assert.False(turn.Communicated);
        Assert.True(turn.Taken);
        Assert.Null(turn.Feedback);
        Assert.Null(turn.Repair);
        Assert.Null(turn.RepairKind);
        Assert.Null(turn.Choices);
        Assert.False(turn.Uncertain);
        Assert.Empty(turn.HostEvents);
    }

    [Theory]
    [InlineData("baker", "Tienes veinte panes.", "Sí, es verdad.")]
    [InlineData("baker", "Usted tiene muchas manzanas.", "Sí, es verdad.")]
    [InlineData("smith", "Haces hachas.", "Sí, es verdad.")]
    [InlineData("villager", "Eres el herrero.", "Sí, es verdad.")]
    [InlineData("baker", "Tengo veinte panes.", "¡Qué bien!")]
    [InlineData("baker", "Ella tiene veinte panes.", "¡Qué bien!")]
    [InlineData("fisher", "Voy con usted al río.", "¡Qué bien!")]
    public void AStatementAboutTheListenerGetsSiEsVerdadAndOneAboutTheSpeakerOrAThirdPersonKeepsQueBien(string npc, string line, string reply)
    {
        // V28: decided from the core's analysis (a second-person verb, or tú / usted / ustedes not after a preposition), by NpcTurnRules.AboutListener.
        var turn = Free(NewNpcs(), npc, line);

        Assert.Contains(reply, turn.Replies);
        Assert.DoesNotContain(reply == "Sí, es verdad." ? "¡Qué bien!" : "Sí, es verdad.", turn.Replies);
    }

    [Theory]
    [InlineData("baker")]
    [InlineData("smith")]
    [InlineData("fisher")]
    [InlineData("farmer")]
    [InlineData("villager")]
    [InlineData("shopkeeper")]
    public void EveryNpcWhoSaysQueBienHasASiEsVerdadPoolInItsVoice(string npc)
    {
        var prefix = npc == "shopkeeper" ? "shop" : npc;
        var pool = Assert.Single(TestGame.Bundle.Dialogue.Pools.Values, p => p.PoolId == prefix + "_true");
        Assert.Equal("Sí, es verdad.", Assert.Single(pool.Variants).Text);
        Assert.Contains(TestGame.Data.Speech, l => l.LineId == prefix + "_true.1" && l.Speaker == npc);
    }

    // No intent of the catalog takes this line since M78 (the core reads «la tienda está en el pueblo» as describe, so it is no longer the unknown example).
    [Fact]
    public void ALineNoIntentOfTheCatalogTakesIsStillNotUnderstood()
    {
        var turn = Free(NewNpcs(), "farmer", "la tienda cierra mañana");

        Assert.Null(turn.OffPromptIntent);
        Assert.False(turn.Taken);
        Assert.Contains(turn.Replies, static r => r is "No te entendí. ¿Me lo dices otra vez?" or "Perdón, ¿qué dices?");
    }

    [Fact]
    public void AnOffPromptLineIsNoFailedAttemptSoTheLadderStartsAtItsFirstStep()
    {
        var npcs = NewNpcs();

        _ = Free(npcs, "smith", "me gusta el río");
        _ = Free(npcs, "smith", "me gusta el río");
        var turn = Free(npcs, "smith", "xyzzy qwerty");

        Assert.Equal(RepairKind.AskAgain, turn.RepairKind);
        Assert.Null(turn.Choices);
        Assert.Contains(turn.Replies, static r => r is "No te entendí. ¿Me lo dices otra vez?" or "Perdón, ¿qué dices?");
        Assert.DoesNotContain(turn.GuideLines, static l => l.Contains("despacio", StringComparison.Ordinal));
    }

    [Fact]
    public void ALineNotUnderstoodStillClimbsTheLadder()
    {
        var npcs = NewNpcs();

        _ = Free(npcs, "smith", "xyzzy qwerty");
        var turn = Free(npcs, "smith", "xyzzy qwerty");

        Assert.NotEqual(RepairKind.AskAgain, turn.RepairKind); // the control of the test above
    }

    [Fact]
    public void AtALessonStepALineTheStepDoesNotTakeKeepsTheRepairAndItsCountsClimb()
    {
        var npcs = NewNpcs();
        var step = new GuideScene(new GuideCell(0, 0, 0), PromptOverride: "lesson-like-apples");

        var first = npcs.Say("smith", "tengo dos manzanas", step, Now, "hash");
        var second = npcs.Say("smith", "tengo dos manzanas", step, Now, "hash");

        Assert.Null(first.OffPromptIntent);
        Assert.False(first.Taken);
        Assert.Equal(RepairKind.AskAgain, first.RepairKind);
        Assert.NotEqual(RepairKind.AskAgain, second.RepairKind);
        Assert.DoesNotContain(first.Replies, static r => r.StartsWith("Te entiendo", StringComparison.Ordinal));
    }

    [Fact]
    public void TheBookCreditsAnOffPromptLineAsAUseOfItsNounsAndOfAPhraseOfALessonBegun()
    {
        var session = TestGame.NewSessionWithShippedLessons();
        SessionSkip.Apply(session, 10);
        Stage.TeleportToNpc(session, "smith");
        _ = session.OpenTalk("smith");
        for (var i = 0; session.Lessons.Engaged && i < 50; i++)
        {
            _ = session.Lessons.Skip();
        }

        var uses = session.Events.OfType<PhraseUse>().Count();
        var river = session.Say("me gusta el río");

        Assert.Equal("Te entiendo. Yo hago hachas.", Assert.Single(river.Replies));
        var noun = Assert.Single(session.Learner.Nouns, static row => row.Lemma == "río");
        Assert.Equal((1, 1), (noun.Times, noun.TimesCorrect));
        Assert.Equal(uses, session.Events.OfType<PhraseUse>().Count()); // no phrase of a lesson is this line

        var phrase = session.Say("Me gustan las manzanas.");

        Assert.Equal("state_like", phrase.OffPromptIntent);
        Assert.Contains(session.Events.OfType<PhraseUse>().Skip(uses), static u => u.PhraseId == "me-gustan-las-manzanas");
    }

    private sealed class NoFacts : IGuideFacts
    {
        public GuideNearest? NearestOfNoun(GuideViewpoint from, string lemma) => null;

        public GuideNearest? NearestOfNounJoinedTo(GuideViewpoint from, string lemma, GuideCell body) => null;

        public GuideNearest Toward(GuideViewpoint from, GuideCell cell) => new(cell, 0, string.Empty, 0);

        public IReadOnlyList<string> NounsAt(GuideCell cell) => [];
    }
}
