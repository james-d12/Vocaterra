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
        Assert.Equal(["Hoy hace sol."], Free(NewNpcs(), npc, "¿qué tiempo hace?").Replies); // a world with no weather (E07: no key in the scene) keeps the plain answer of the transition
        Assert.Equal([npc == "villager" ? "Bien, gracias. ¿Y tú?" : "Bien, gracias."], Free(NewNpcs(), npc, "¿cómo estás?").Replies); // PR #48: the villager, whose prompt practises «Muy bien.», asks back
    }

    /// <summary>E07: the held weather, the hour that makes it day or night, and the line every NPC answers with (the first kind by day and its night key, then the other two).</summary>
    public static TheoryData<string, double, string> HeldWeathers => new()
    {
        { "sunny", 12, "Hoy hace sol." },
        { "sunny", 23, "Hace buen tiempo." },
        { "cloudy", 12, "Hoy está nublado." },
        { "cloudy", 23, "Hoy está nublado." },
        { "rain", 12, "Hoy llueve." },
        { "rain", 23, "Hoy llueve." },
    };

    [Theory]
    [MemberData(nameof(HeldWeathers))]
    public void EveryNpcAnswersWhatTheWeatherIsWithTheWeatherTheWorldHasByDayAndAtNight(string kind, double hour, string line)
    {
        foreach (var npc in TestGame.Data.Npcs.Ids)
        {
            var session = TestGame.NewSession();
            session.Simulation.ForceWeather(kind);
            session.Simulation.FreezeClock(hour);
            Stage.TeleportToNpc(session, npc);
            _ = session.OpenTalk(npc);

            var turn = session.Say("¿qué tiempo hace?");

            Assert.Equal("ask_weather", turn.Intent);
            Assert.Equal([line], turn.Replies);
            Assert.Empty(turn.HostEvents);
        }
    }

    [Fact]
    public void TheAnswerChangesWhenTheWeatherChangesBetweenTwoTurnsOfOneConversation()
    {
        var session = TestGame.NewSession();
        session.Simulation.FreezeClock(12);
        Stage.TeleportToNpc(session, "baker");
        _ = session.OpenTalk("baker");

        session.Simulation.ForceWeather("sunny");
        var first = session.Say("¿qué tiempo hace?");
        session.Simulation.ForceWeather("rain");
        var second = session.Say("¿qué tiempo hace?");
        session.Simulation.FreezeClock(23);
        session.Simulation.ForceWeather("sunny");
        var third = session.Say("¿qué tiempo hace?");

        Assert.Equal(["Hoy hace sol."], first.Replies);
        Assert.Equal(["Hoy llueve."], second.Replies);
        Assert.Equal(["Hace buen tiempo."], third.Replies);
    }

    [Fact]
    public void ANeighbourOfTheQuestionIsAnsweredAsItWasWhateverTheWeather()
    {
        var session = TestGame.NewSession();
        session.Simulation.ForceWeather("rain");
        session.Simulation.FreezeClock(12);
        Stage.TeleportToNpc(session, "baker");
        _ = session.OpenTalk("baker");

        Assert.Equal(["Sí, hace sol."], session.Say("hace sol").Replies); // the statement is echoed, not checked against the weather (state_weather stays as V29 made it)
        Assert.Equal(["¡Sí!"], session.Say("llueve").Replies);
        Assert.Equal(["Bien, gracias."], session.Say("¿cómo estás?").Replies);
        Assert.Equal(["Hoy llueve."], session.Say("¿qué tiempo hace hoy?").Replies);
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
