using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Nlp.Response.Tests.Templates;

/// <summary>Host slots in response planning: the same formatters as verdict slots, the verdict wins on a clash, and slot kinds for validation.</summary>
public class HostSlotTests
{
    private static readonly ResponsePlanner Planner = new(TestSupport.Engine);

    private static DialogueCatalog Catalog()
    {
        return new DialogueCatalog(
        [
            new("greet", new VariantPool("greet", [new DialogueTemplate("greet.1", "Mira {target:article(def)}, {direction}, a {distance:words(m)} metros.")])),
            new("not_understood", new VariantPool("not_understood", [new DialogueTemplate("nu.1", "No te entiendo.")])),
            new("feedback.hint", new VariantPool("feedback.hint", [new DialogueTemplate("fb.1", "Piensa en {item:article(def)} y en {target:article(indef)}.")])),
        ]);
    }

    private static Verdict GreetVerdict(SlotMap? slots = null)
    {
        return TestSupport.MakeVerdict("greet", slots: slots);
    }

    [Fact]
    public void AHostSlotGoesThroughArticleAndWordsFormatters()
    {
        var plan = Planner.Plan(
            GreetVerdict(),
            null,
            Catalog(),
            SeededRng.ForInteraction(1, "a", 0),
            hostSlots: TestSupport.Slots(("target", "árbol"), ("direction", "a la izquierda"), ("distance", "21")));

        Assert.Equal("Mira el árbol, a la izquierda, a veintiún metros.", plan.Lines[0].Text);
    }

    [Fact]
    public void AFeedbackTemplateSeesHostSlotsAndVerdictSlots_AndTheVerdictWinsOnAClash()
    {
        var verdict = GreetVerdict(TestSupport.Slots(("item", "espada"), ("target", "casa")));

        var plan = Planner.Plan(
            verdict,
            new FeedbackRequest("noun.number", "espadas", "espada", FeedbackKinds.Hint),
            Catalog(),
            SeededRng.ForInteraction(1, "a", 0),
            hostSlots: TestSupport.Slots(("target", "árbol"), ("direction", "al norte"), ("distance", "1"), ("item", "martillo")));

        Assert.Equal("Mira la casa, al norte, a un metros.", plan.Lines[0].Text);
        Assert.Equal("Piensa en la espada y en una casa.", plan.Lines[1].Text);
    }

    [Fact]
    public void AMissingHostSlotFailsLikeAMissingVerdictSlot()
    {
        var withoutHost = Assert.Throws<TemplateException>(() =>
            Planner.Plan(GreetVerdict(), null, Catalog(), SeededRng.ForInteraction(1, "a", 0)));
        var withPartialHost = Assert.Throws<TemplateException>(() =>
            Planner.Plan(GreetVerdict(), null, Catalog(), SeededRng.ForInteraction(1, "a", 0), hostSlots: TestSupport.Slots(("target", "árbol"))));

        Assert.Contains("target", withoutHost.Placeholder, StringComparison.Ordinal);
        Assert.Contains("direction", withPartialHost.Placeholder, StringComparison.Ordinal);
    }

    [Fact]
    public void WithFallbackKeepsOwnValuesAndAddsTheRest()
    {
        var own = TestSupport.Slots(("a", "1"), ("c", "3"));
        var fallback = TestSupport.Slots(("a", "x"), ("b", "2"));

        var merged = own.WithFallback(fallback);

        Assert.Equal(TestSupport.Slots(("a", "1"), ("b", "2"), ("c", "3")), merged);
        Assert.Same(own, own.WithFallback(SlotMap.Empty));
    }

    [Fact]
    public void SlotKindsTellNumbersAndVerbsFromText()
    {
        var kinds = TemplateEngine.SlotKinds(TestSupport.Template("{distance:words(m)} {qty:words(item)} {item:plural-if(qty)} {n:plural-if(c)} {v:conj(1,sg,pres)} {name}"));

        Assert.Equal(TemplateSlotKind.Number, kinds["distance"]);
        Assert.Equal(TemplateSlotKind.Number, kinds["qty"]);
        Assert.Equal(TemplateSlotKind.Number, kinds["c"]);
        Assert.Equal(TemplateSlotKind.Verb, kinds["v"]);
        Assert.Equal(TemplateSlotKind.Text, kinds["item"]);
        Assert.False(kinds.ContainsKey("name"));
    }
}
