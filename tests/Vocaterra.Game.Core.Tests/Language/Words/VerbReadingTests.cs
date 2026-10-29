using Vocaterra.Game.Core.Language.Book;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Es;

namespace Vocaterra.Game.Core.Tests.Language.Words;

// The game's reading of a used form (X03): an imperative reading that is the indicative present of its verb is that present, for the labels and the tables alike.
public sealed class VerbReadingTests
{
    private static readonly Lazy<ContentBundle> Bundle = new(() => GameBundle.Load());
    private static readonly Lazy<VerbGlosses> Glosses = new(() => RouteScene.DataOf(Bundle.Value).Glosses);

    [Theory]
    [InlineData("está", "estar", "2", "sg", "3", "sg")] // the core's tú imperative; estar's he/she/it present
    [InlineData("vamos", "ir", "1", "pl", "1", "pl")] // "let's go" to the core; ir's we present
    [InlineData("come", "comer", "2", "sg", "3", "sg")] // a regular verb: eat, he/she/it present
    public void AnImperativeThatIsAnIndicativePresentReadsAsThatPresent(string text, string lemma, string person, string number, string expectedPerson, string expectedNumber)
    {
        var reading = VerbReading.Normalize(lemma, text, person, number, null, "imp", Bundle.Value.SpanishMorphology());
        Assert.Equal((expectedPerson, expectedNumber, "pres", "ind"), reading);
    }

    [Fact]
    public void TheLabelsOfThoseReadingsAreTheIndicative()
    {
        var estar = Glosses.Value.ForLemma("estar");
        var comer = Glosses.Value.ForLemma("comer");
        var ir = Glosses.Value.ForLemma("ir");
        Assert.Equal("está — he/she/it is", Label("está", "estar", estar, Bundle.Value.SpanishMorphology(), "2", "sg", "imp"));
        Assert.Equal("vamos — we go", Label("vamos", "ir", ir, Bundle.Value.SpanishMorphology(), "1", "pl", "imp"));
        Assert.Equal("come — he/she/it eats", Label("come", "comer", comer, Bundle.Value.SpanishMorphology(), "2", "sg", "imp"));
        Assert.Equal("¿está? — is he/she/it?", Label("está", "estar", estar, Bundle.Value.SpanishMorphology(), "2", "sg", "imp", question: true));
        Assert.Equal("vamos — we go", Label("vamos", "ir", ir, Bundle.Value.SpanishMorphology(), null, null, null));
    }

    [Fact]
    public void ACommandWithNoIndicativeTwinKeepsItsImperativeLabel()
    {
        // "ven" is not a present of venir (vengo, vienes, viene...), so it stays the command the core read.
        var reading = VerbReading.Normalize("venir", "ven", "2", "sg", null, "imp", Bundle.Value.SpanishMorphology());
        Assert.Equal(("2", "sg", null, "imp"), reading);
        Assert.Equal("ven — come!", BookVerbLabels.Label("ven", "venir", new VerbGloss("venir", "to come", "come", "comes", "came"), "2", "sg", null, "imp", question: false));
    }

    [Fact]
    public void AFiniteIndicativeAndAnInfinitiveAreLeftAsTheCoreReadThem()
    {
        Assert.Equal(("1", "sg", "pres", "ind"), VerbReading.Normalize("querer", "quiero", "1", "sg", "pres", "ind", Bundle.Value.SpanishMorphology()));
        Assert.Equal((null, null, null, null), VerbReading.Normalize("comer", "comer", null, null, null, null, Bundle.Value.SpanishMorphology()));
    }

    private static string Label(string form, string lemma, VerbGloss? gloss, Vocaterra.Nlp.Es.Morphology.MorphologyGenerator morphology, string? person, string? number, string? mood, bool question = false)
    {
        var (p, n, t, m) = VerbReading.Normalize(lemma, form, person, number, null, mood, morphology);
        return BookVerbLabels.Label(form, lemma, gloss, p, n, t, m, question);
    }
}
