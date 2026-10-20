namespace Vocaterra.Nlp.Response.Tests.Templates;

using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Response.Templates;

public class ResponsePlannerTests
{
    private static readonly ResponsePlanner Planner = new(TestSupport.Engine);

    private static DialogueCatalog Catalog()
    {
        static KeyValuePair<string, VariantPool> Pool(string key, params (string LineId, string Text)[] variants)
        {
            return new KeyValuePair<string, VariantPool>(
                key,
                new VariantPool(key, [.. variants.Select(static v => new DialogueTemplate(v.LineId, v.Text))]));
        }

        return new DialogueCatalog(
        [
            Pool("buy_item", ("buy.1", "¡Perfecto!"), ("buy.2", "Claro, {qty:words} {item:plural-if(qty)}.")),
            Pool("greet", ("greet.1", "¡Hola, viajero!")),
            Pool("not_understood", ("nu.1", "No te entiendo.")),
            Pool("custom_intent", ("custom.1", "Respuesta personalizada.")),
            Pool("feedback.recast", ("fb.recast.1", "¿{recast:cap}?")),
            Pool("feedback.hint", ("fb.hint.1", "Cuidado con el verbo.")),
            Pool("feedback.hint.noun.number", ("fb.hint.noun.1", "Piensa en el plural de {item}.")),
            Pool("feedback.explicit_correction", ("fb.explicit.1", "Se dice {expected}, no {actual}. Etiqueta {tag}.")),
            Pool("feedback.slow_repeat", ("fb.slow.1", "Repite despacio: {expected}.")),
        ]);
    }

    private static SlotMap BuySlots()
    {
        return TestSupport.Slots(("item", "espada"), ("qty", "2"));
    }

    [Fact]
    public void CommunicatedVerdictWithoutFeedbackProducesTheReplyOnly()
    {
        var verdict = TestSupport.MakeVerdict("greet");
        var plan = Planner.Plan(verdict, null, Catalog(), new SeededRng(1));
        Assert.Equal([new ResponseLine("greet.1", "¡Hola, viajero!", false)], plan.Lines);
    }

    [Fact]
    public void ReplyTemplatesReadTheVerdictSlots()
    {
        var verdict = TestSupport.MakeVerdict("buy_item", slots: BuySlots());
        var lines = Enumerable.Range(0, 30)
            .Select(i => Planner.Plan(verdict, null, Catalog(), SeededRng.ForInteraction(5, "smith", i)).Lines[0])
            .ToList();
        Assert.Contains(new ResponseLine("buy.1", "¡Perfecto!", false), lines);
        Assert.Contains(new ResponseLine("buy.2", "Claro, dos espadas.", false), lines);
    }

    [Fact]
    public void RecastIsSpokenBeforeTheReply()
    {
        var verdict = TestSupport.MakeVerdict("greet", correctness: 0.7);
        var feedback = new FeedbackRequest("conj.person", "tengo", "tiene", FeedbackKinds.Recast);
        var plan = Planner.Plan(verdict, feedback, Catalog(), new SeededRng(1), recastText: "tienes dos espadas");
        Assert.Equal(
            [new ResponseLine("fb.recast.1", "¿Tienes dos espadas?", true), new ResponseLine("greet.1", "¡Hola, viajero!", false)],
            plan.Lines);
    }

    [Fact]
    public void RecastDefaultsToTheExpectedText()
    {
        var verdict = TestSupport.MakeVerdict("greet");
        var feedback = new FeedbackRequest("conj.person", "tengo dos espadas", "tiene dos espadas", FeedbackKinds.Recast);
        var plan = Planner.Plan(verdict, feedback, Catalog(), new SeededRng(1));
        Assert.Equal("¿Tengo dos espadas?", plan.Lines[0].Text);
    }

    [Fact]
    public void RecastWithoutAPoolFallsBackToTheRecastTextItself()
    {
        var catalog = new DialogueCatalog(Catalog().Pools.Where(static p => p.Key != "feedback.recast"));
        var feedback = new FeedbackRequest("conj.person", "tengo", "tiene", FeedbackKinds.Recast);
        var plan = Planner.Plan(TestSupport.MakeVerdict("greet"), feedback, catalog, new SeededRng(1), recastText: "yo tengo dos espadas");
        Assert.Equal(new ResponseLine("recast", "yo tengo dos espadas", true), plan.Lines[0]);
        Assert.Equal("greet.1", plan.Lines[1].LineId);
    }

    [Fact]
    public void OtherFeedbackFollowsTheReplyAndSeesTheReservedSlots()
    {
        var verdict = TestSupport.MakeVerdict("greet");
        var explicitCorrection = new FeedbackRequest("conj.person", "tengo", "tiene", FeedbackKinds.ExplicitCorrection);
        var plan = Planner.Plan(verdict, explicitCorrection, Catalog(), new SeededRng(1));
        Assert.Equal(
            [new ResponseLine("greet.1", "¡Hola, viajero!", false), new ResponseLine("fb.explicit.1", "Se dice tengo, no tiene. Etiqueta conj.person.", false)],
            plan.Lines);
    }

    [Fact]
    public void TagSpecificFeedbackPoolWinsOverTheKindPool()
    {
        var verdict = TestSupport.MakeVerdict("greet", slots: TestSupport.Slots(("item", "espada")));
        var specific = Planner.Plan(verdict, new FeedbackRequest("noun.number", "espadas", "espada", FeedbackKinds.Hint), Catalog(), new SeededRng(1));
        Assert.Equal("Piensa en el plural de espada.", specific.Lines[1].Text);
        var generic = Planner.Plan(verdict, new FeedbackRequest("conj.person", "tengo", "tiene", FeedbackKinds.Hint), Catalog(), new SeededRng(1));
        Assert.Equal("Cuidado con el verbo.", generic.Lines[1].Text);
    }

    [Fact]
    public void FeedbackWithoutAPoolIsOmitted()
    {
        var plan = Planner.Plan(
            TestSupport.MakeVerdict("greet"),
            new FeedbackRequest("pron.r", "ɾ", "ɹ", FeedbackKinds.ModelPronunciation),
            Catalog(),
            new SeededRng(1));
        Assert.Single(plan.Lines);
    }

    [Fact]
    public void NotCommunicatedVerdictUsesTheNotUnderstoodReplyAndSlowRepeat()
    {
        var verdict = TestSupport.MakeVerdict("buy_item", communicated: false, correctness: 0.1, slots: BuySlots());
        var feedback = new FeedbackRequest("not_communicated", "tengo dos espadas", string.Empty, FeedbackKinds.SlowRepeat);
        var plan = Planner.Plan(verdict, feedback, Catalog(), new SeededRng(1));
        Assert.Equal(
            [new ResponseLine("nu.1", "No te entiendo.", false), new ResponseLine("fb.slow.1", "Repite despacio: tengo dos espadas.", false)],
            plan.Lines);
    }

    [Fact]
    public void UnknownIntentFallsBackToNotUnderstood()
    {
        var plan = Planner.Plan(TestSupport.MakeVerdict("unknown"), null, Catalog(), new SeededRng(1));
        Assert.Equal("nu.1", plan.Lines[0].LineId);
    }

    [Fact]
    public void ExplicitDialogueIntentOverridesTheVerdictIntent()
    {
        var plan = Planner.Plan(TestSupport.MakeVerdict("greet"), null, Catalog(), new SeededRng(1), dialogueIntent: "custom_intent");
        Assert.Equal("custom.1", plan.Lines[0].LineId);
    }

    [Fact]
    public void MissingReplyPoolThrows()
    {
        Assert.Throws<KeyNotFoundException>(() => Planner.Plan(TestSupport.MakeVerdict("sell_item"), null, Catalog(), new SeededRng(1)));
    }

    [Fact]
    public void PlansAreDeterministicPerSeed()
    {
        var verdict = TestSupport.MakeVerdict("buy_item", slots: BuySlots());
        var feedback = new FeedbackRequest("noun.number", "espadas", "espada", FeedbackKinds.Recast);
        var first = Enumerable.Range(0, 20)
            .Select(i => Planner.Plan(verdict, feedback, Catalog(), SeededRng.ForInteraction(9, "smith", i), recastText: "dos espadas"))
            .ToList();
        var second = Enumerable.Range(0, 20)
            .Select(i => Planner.Plan(verdict, feedback, Catalog(), SeededRng.ForInteraction(9, "smith", i), recastText: "dos espadas"))
            .ToList();
        Assert.Equal(first, second);
        Assert.Equal(first[0].GetHashCode(), second[0].GetHashCode());
    }

    [Fact]
    public void ReplyIsDrawnBeforeFeedback()
    {
        var verdict = TestSupport.MakeVerdict("buy_item", slots: BuySlots());
        var feedback = new FeedbackRequest("conj.person", "tengo", "tiene", FeedbackKinds.Hint);
        for (long counter = 0; counter < 10; counter++)
        {
            var rng = SeededRng.ForInteraction(3, "smith", counter);
            var expectedReply = DialogueSelector.Select(Catalog(), "buy_item", SeededRng.ForInteraction(3, "smith", counter));
            var plan = Planner.Plan(verdict, feedback, Catalog(), rng);
            Assert.Equal(expectedReply.LineId, plan.Lines[0].LineId);
        }
    }

    [Fact]
    public void DefaultPlannerUsesTheSeedLexicon()
    {
        var plan = new ResponsePlanner().Plan(
            TestSupport.MakeVerdict("buy_item", slots: BuySlots()),
            null,
            new DialogueCatalog([new("buy_item", new VariantPool("buy_item", [new DialogueTemplate("x.1", "¿Tienes {item:article(indef):plural-if(qty)}?")]))]),
            new SeededRng(1));
        Assert.Equal("¿Tienes unas espadas?", plan.Lines[0].Text);
    }
}
