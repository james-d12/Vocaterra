using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Language.Learner;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Game.Core.Tests;

// The data the book reads (S20): the verbs' English, the lessons' summaries and step ids, the forms' question flag and last use, and the book item the
// learner starts with.
public sealed class BookDataTests
{
    [Fact]
    public void TheVerbGlossesGiveTheInfinitiveEnglishAndTheIrregularForms()
    {
        var glosses = VerbGlosses.Load(TestGame.Bundle);

        Assert.Equal("to want", glosses.ForLemma("querer")!.English);
        Assert.Equal("has", glosses.ForLemma("tener")!.Third);
        Assert.Equal("had", glosses.ForLemma("tener")!.Past);
        Assert.Null(glosses.ForLemma("querer")!.Third); // the regular rule gives it
        Assert.Null(glosses.ForLemma("hablar_no_existe"));
        Assert.True(glosses.All.Count >= 30);
    }

    [Fact]
    public void SerAndEstarAreGlossedApartByWhatTheyAreAndWhereOrHow()
    {
        var glosses = VerbGlosses.Load(TestGame.Bundle);

        Assert.Equal("to be: what", glosses.ForLemma("ser")!.English);
        Assert.Equal("to be: where or how", glosses.ForLemma("estar")!.English);
    }

    [Fact]
    public void AGlossMustStartWithTo()
    {
        Assert.Throws<InvalidDataException>(() => VerbGlosses.Parse("""{ "schemaVersion": 1, "verbs": [ { "lemma": "querer", "english": "want" } ] }"""));
        Assert.Throws<InvalidDataException>(() => VerbGlosses.Parse("""{ "schemaVersion": 2, "verbs": [] }"""));
        Assert.Throws<InvalidDataException>(() => VerbGlosses.Parse("""{ "schemaVersion": 1, "verbs": [ { "lemma": "querer" } ] }"""));
    }

    [Fact]
    public void TheFirstLessonCarriesItsSummaryAndItsStepIdsInOrder()
    {
        using var session = TestGame.NewSessionWithShippedLessons();
        var row = session.Lessons.Record.Single(static r => r.LessonId == "first-words");

        Assert.StartsWith("You can greet, say yes and no, ask what something is", row.Summary!, StringComparison.Ordinal);
        Assert.Equal("u1-welcome", row.StepIds![0]);
        Assert.Equal(row.StepCount, row.StepIds!.Count);
    }

    [Fact]
    public void TheEnglishTextNoLongerCarriesTheUnusedHoldKey()
    {
        Assert.DoesNotContain("btnHold", EnglishText.UiKeys);
        var english = EnglishText.Load(TestGame.Bundle, PhraseSheet.Load(TestGame.Bundle));
        Assert.Throws<KeyNotFoundException>(() => english.Ui("btnHold"));
    }

    [Fact]
    public void ANewGameStartsWithTheBookItemAndTheItemIsNotPlaceable()
    {
        using var session = TestGame.NewSessionWithShippedLessons();

        Assert.Equal(1, session.Simulation.Inventory.Count(ItemId.Book));
        Assert.Null(BlockDefinition.PlacedBy(ItemId.Book));
        Assert.Equal("book", ItemDefinition.Get(ItemId.Book).Name);
    }

    [Fact]
    public void AFormKeepsWhetherItWasAskedAndItsLastUse()
    {
        var record = new LearnerRecord();
        record.Append(Entry(1, "quieres pan", [Word("quieres", "querer", "2", "sg", "pres", "ind")]));
        record.Append(Entry(2, "¿quieres pan?", [Word("quieres", "querer", "2", "sg", "pres", "ind")]));

        var forms = record.Verbs.Single().Forms;
        Assert.Equal(2, forms.Count); // a question is a form of its own: quieres and ¿quieres?
        var statement = forms.Single(static f => !f.Question);
        var question = forms.Single(static f => f.Question);
        Assert.Equal(1, statement.Times);
        Assert.Equal(1, question.Times);
        Assert.Equal(1, statement.Last!.Entry);
        Assert.Equal(2, question.Last!.Entry);
        Assert.Equal("¿quieres pan?", question.Last.Sentence);
    }

    private static InputEntry Entry(long id, string text, IReadOnlyList<GuideWord> words) =>
        new(id, 1, GuideSession.DefaultStartedAt + id, InputSource.Typed, text, NpcService.GuideAgent, "guide-open", InputPlace.Dialogue, null, "ask_directions", true, false, [], words);

    private static GuideWord Word(string text, string lemma, string person, string number, string tense, string mood) =>
        new(text, new TextSpan(0, text.Length), lemma, "verb", WordRole.Word, person, number, tense, mood, null);
}
