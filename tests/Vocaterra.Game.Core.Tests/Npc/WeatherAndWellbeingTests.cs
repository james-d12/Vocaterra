using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Game.Core.Tests.Npc;

/// <summary>
/// The weather and «¿cómo estás?» replies (village-plan V29, the owner's decision of 2026-11-02) and the acknowledgement pools the NPC files declare (the PR #28 review's item 14).
/// </summary>
public sealed class WeatherAndWellbeingTests
{
    private static readonly StoredTimestamp Now = new(GuideSession.DefaultStartedAt + 5);

    private static NpcService NewNpcs() => new(TestGame.Data, new NoFacts(), TestGame.Seed, new StoredTimestamp(GuideSession.DefaultStartedAt));

    private static GuideTurn Free(NpcService npcs, string npc, string line) => npcs.Say(npc, line, new GuideScene(new GuideCell(0, 0, 0)), Now, "hash");

    public static TheoryData<string> Npcs => [.. TestGame.Data.Npcs.Ids];

    [Theory]
    [MemberData(nameof(Npcs))]
    public void EveryNpcAnswersAWeatherStatementByEchoingTheWord(string npc)
    {
        var turn = Free(NewNpcs(), npc, "hace frío");

        Assert.Equal(["Sí, hace frío."], turn.Replies);
        Assert.True(turn.Communicated);
        Assert.Empty(turn.HostEvents);
    }

    [Theory]
    [InlineData("hace mucho calor", "Sí, hace mucho calor.")]
    [InlineData("hace buen tiempo", "Sí, hace buen tiempo.")]
    [InlineData("¡qué frío!", "Sí, hace frío.")]
    [InlineData("llueve", "¡Sí!")]
    [InlineData("nieva", "¡Sí!")]
    [InlineData("está nublado", "¡Sí!")]
    public void TheEchoTakesTheWordOfTheLineAndALineWithNoWordGetsSi(string line, string reply)
    {
        Assert.Equal([reply], Free(NewNpcs(), "baker", line).Replies);
    }

    [Theory]
    [MemberData(nameof(Npcs))]
    public void EveryNpcAnswersWhatTheWeatherIsAndHowTheyAre(string npc)
    {
        Assert.Equal(["Hoy hace sol."], Free(NewNpcs(), npc, "¿qué tiempo hace?").Replies);
        Assert.Equal(["Bien, gracias."], Free(NewNpcs(), npc, "¿cómo estás?").Replies);
    }

    [Theory]
    [MemberData(nameof(Npcs))]
    public void AGreetingStillGetsTheGreetingAndTheWeatherLinesAreNoActions(string npc)
    {
        var greeting = Free(NewNpcs(), npc, "hola");
        var weather = Free(NewNpcs(), npc, "hace frío");

        Assert.NotEqual(["Bien, gracias."], greeting.Replies);
        Assert.Empty(weather.HostEvents);
        Assert.Null(weather.Feedback);
    }

    [Fact]
    public void ACraftQuestionAtTheSmithStaysOffThePromptBecauseAllowingItTakesQuieroHacerUnHachaFromStateWant()
    {
        // V29, item 5: with ask_craft at smith-open the core reads «quiero hacer un hacha» (the want a lesson practises) as the craft question, so the smith keeps «Te entiendo.».
        Assert.Equal(["Te entiendo. Yo hago hachas."], Free(NewNpcs(), "smith", "¿cómo hago un hacha?").Replies);
        Assert.Equal("state_want", Free(NewNpcs(), "smith", "quiero hacer un hacha").Intent);
    }

    [Fact]
    public void EveryNpcDeclaresTheAcknowledgementPoolsItsAgentSaysAndOnlyExistingOnes()
    {
        foreach (var npc in TestGame.Data.Npcs.All)
        {
            var said = TestGame.Bundle.Agents[npc.Id].Definition.States
                .SelectMany(static state => state.Transitions)
                .SelectMany(static transition => transition.Actions)
                .Select(static action => action.VariantPoolId)
                .OfType<string>()
                .Where(static pool => pool.EndsWith("_nice", StringComparison.Ordinal) || pool == "answer_accepted")
                .Distinct(StringComparer.Ordinal);

            Assert.Equal(said.Order(StringComparer.Ordinal), npc.AcknowledgementPools.Order(StringComparer.Ordinal));
            Assert.All(npc.AcknowledgementPools, pool => Assert.True(TestGame.Bundle.Dialogue.Pools.ContainsKey(pool), pool));
        }
    }

    [Fact]
    public void ThePoolOfASlotReplyAndItsValuesAreDeclaredAndEveryValueRendersAndHasAVoice()
    {
        foreach (var npc in TestGame.Data.Npcs.All)
        {
            var (intent, reply) = Assert.Single(npc.SlotReplies);
            Assert.Equal("state_weather", intent);
            var pool = NpcTurnRules.PoolSaidOn(TestGame.Bundle, npc.Id, reply.Event);
            Assert.NotNull(pool);
            var template = Assert.Single(TestGame.Bundle.Dialogue.Pools[pool].Variants);
            foreach (var value in reply.Values)
            {
                var text = TestGame.Bundle.TemplateEngine.Render(template, Vocaterra.Nlp.Core.Verdicts.SlotMap.From(new Dictionary<string, string> { [reply.Slot] = value }));
                Assert.Contains(TestGame.Data.Speech, line => line.Speaker == npc.Id && line.Text == text);
            }
        }
    }

    [Fact]
    public void ADrawAmongTheVariantsIsSeededAndCoversThemAll()
    {
        Assert.True(TestGame.Bundle.Dialogue.TryGetPool("answer_accepted", out var pool));
        var drawn = Enumerable.Range(0, 60).Select(counter => NpcTurnRules.Pick(pool, TestGame.Seed, counter).LineId).ToList();

        Assert.Equal(pool.Variants.Select(static v => v.LineId).Order(StringComparer.Ordinal), drawn.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        Assert.Equal(drawn, Enumerable.Range(0, 60).Select(counter => NpcTurnRules.Pick(pool, TestGame.Seed, counter).LineId));
        Assert.NotEqual(drawn, Enumerable.Range(0, 60).Select(counter => NpcTurnRules.Pick(pool, TestGame.Seed + 1, counter).LineId));
    }

    [Fact]
    public void TheBookCreditsAWeatherLineAsFreeTalkDoesAndTheNounOfItsWeatherWordOnce()
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
        var hot = session.Say("hace mucho calor");

        Assert.Equal(["Sí, hace mucho calor."], hot.Replies);
        var calor = Assert.Single(session.Learner.Nouns, static row => row.Lemma == "calor"); // the lexicon noun the line uses, credited as free talk credits any noun (V20): once, never as an item
        Assert.Equal((1, 1), (calor.Times, calor.TimesCorrect));
        Assert.DoesNotContain(session.Learner.Nouns, static row => row.Lemma is "frío" or "hace");
        Assert.Equal(uses, session.Events.OfType<PhraseUse>().Count()); // no phrase of a lesson is this line
        Assert.Empty(hot.HostEvents);
    }

    private sealed class NoFacts : IGuideFacts
    {
        public GuideNearest? NearestOfNoun(GuideViewpoint from, string lemma) => null;

        public GuideNearest? NearestOfNounJoinedTo(GuideViewpoint from, string lemma, GuideCell body) => null;

        public GuideNearest Toward(GuideViewpoint from, GuideCell cell) => new(cell, 0, string.Empty, 0);

        public IReadOnlyList<string> NounsAt(GuideCell cell) => [];
    }
}
