using Vocaterra.Game.Core.Language.Book;
using Vocaterra.Game.Core.Language.Learner;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Es;

namespace Vocaterra.Game.Core.Tests.Language.Book;

// The book's conjugation tables (S28, D-game-19) for a fixed record: which cells are highlighted (used, with a count), which are plain (taught, not used), which are
// empty (not taught) and which columns are hidden; the future (ir a) built from ir; the simple future, the fourth column (V16); and the game's reading of "vamos".
public sealed class ConjugationTableTests
{
    private static readonly Lazy<VerbGlosses> Glosses = new(() => TestGame.Data.Glosses);

    private static WordUse Use(long entry) => new(entry, 1, GuideSession.DefaultStartedAt + entry, "x");

    [Fact]
    public void TheTableCountsEstaraAsTheThirdPersonPresentOfEstar()
    {
        // The core reads "está" as the tú imperative of estar; it is the he/she/it present, so it fills that cell (X03).
        var verb = Verb("estar", Form("está", "2", "sg", null, "imp", 1));
        var table = ConjugationTable.Build(verb, [], [], TestGame.Bundle.SpanishMorphology(), null, TestGame.Data.Words);
        Assert.Equal(new ConjugationCell("está", CellState.Used, 1), At(table, "3", "sg", "pres"));
        Assert.Equal(new ConjugationCell(null, CellState.Locked, 0), At(table, "2", "sg", "pres")); // tú is not counted
    }

    private static FormRow Form(string text, string? person, string? number, string? tense, string? mood, int times, bool question = false) =>
        new(text, person, number, tense, mood, times, times, question);

    private static WordRow Verb(string lemma, params FormRow[] forms) =>
        new(lemma, "verb", null, forms.Sum(static f => f.Times), forms.Sum(static f => f.Times), forms, Use(1), Use(1));

    private static GuideWord Word(string text, int start, string? lemma, string? person = null, string? number = null, string? tense = null, string? mood = null) =>
        new(text, new TextSpan(start, text.Length), lemma, "verb", WordRole.Word, person, number, tense, mood);

    private static InputEntry Entry(long id, string text, IReadOnlyList<GuideWord> words, InputSource source = InputSource.Typed, InputPlace place = InputPlace.Lesson, bool held = false) =>
        new(id, 1, GuideSession.DefaultStartedAt + id, source, text, "villager", "p", place, "ayer-y-manana/f2-we", "state_action", true, held, [], words);

    // "voy a comer pan": the core reads voy as ir, 1 sg, present; "a" is a preposition; comer is the infinitive.
    private static InputEntry VoyAComer(long id, InputSource source = InputSource.Typed, InputPlace place = InputPlace.Lesson, bool held = false) =>
        Entry(id, "voy a comer pan", [Word("voy", 0, "ir", "1", "sg", "pres", "ind"), Word("a", 4, "a"), Word("comer", 6, "comer")], source, place, held);

    // "vamos a comer fruta": the core reads vamos as an imperative of ir, 1 pl, with no tense (the S27 quirk: the word record shows "vamos (1 pl imp)").
    private static InputEntry VamosAComer(long id) =>
        Entry(id, "vamos a comer fruta", [Word("vamos", 0, "ir", "1", "pl", null, "imp"), Word("a", 6, "a"), Word("comer", 8, "comer")]);

    private static TaughtVerbForm Taught(string lemma, string person, string number, string tense) => new(lemma, person, number, tense);

    // What the lessons teach comer in session 3 (lessons.json): the future (ir a) for yo, tú, nosotros and ellos, and the preterite for yo and tú.
    private static readonly IReadOnlyList<TaughtVerbForm> TaughtComer =
    [
        Taught("comer", "1", "sg", "ir-a"), Taught("comer", "2", "sg", "ir-a"), Taught("comer", "1", "pl", "ir-a"), Taught("comer", "3", "pl", "ir-a"),
        Taught("comer", "1", "sg", "pret"), Taught("comer", "2", "sg", "pret"),
    ];

    private static ConjugationCell At(ConjugationTable table, string person, string number, string tense)
    {
        var row = table.Rows.Single(r => r.Person == person && r.Number == number);
        var column = table.Columns.ToList().FindIndex(c => c.Tense == tense);
        Assert.True(column >= 0, $"the {tense} column is not shown");
        return row.Cells[column];
    }

    [Fact]
    public void TheRowsAreYoTuElNosotrosAndUstedesAndThereIsNoVosotrosRow()
    {
        var table = ConjugationTable.Build(Verb("querer", Form("quiero", "1", "sg", "pres", "ind", 1)), [], [], TestGame.Bundle.SpanishMorphology(), Glosses.Value.ForLemma("querer"), TestGame.Data.Words);

        Assert.Equal(["yo", "tú", "él/ella/usted", "nosotros", "ustedes/ellos"], table.Rows.Select(static r => r.Label));
        Assert.Equal(["1 sg", "2 sg", "3 sg", "1 pl", "3 pl"], table.Rows.Select(static r => $"{r.Person} {r.Number}"));
        Assert.Equal("to want", table.Gloss);
    }

    [Fact]
    public void AUsedFormIsHighlightedWithItsCountATaughtFormIsPlainAndTheRestIsEmpty()
    {
        var querer = Verb("querer", Form("quiero", "1", "sg", "pres", "ind", 3), Form("quieres", "2", "sg", "pres", "ind", 1, question: true));
        var taught = new[] { Taught("querer", "1", "sg", "pres"), Taught("querer", "2", "sg", "pres"), Taught("querer", "3", "sg", "pres") };
        var table = ConjugationTable.Build(querer, [], taught, TestGame.Bundle.SpanishMorphology(), null, TestGame.Data.Words);

        var yo = At(table, "1", "sg", "pres");
        Assert.Equal(new ConjugationCell("quiero", CellState.Used, 3), yo);
        Assert.Equal(new ConjugationCell("quieres", CellState.Used, 1), At(table, "2", "sg", "pres")); // a question is the same cell (the counts are summed)
        Assert.Equal(new ConjugationCell("quiere", CellState.Taught, 0), At(table, "3", "sg", "pres")); // taught, not said: shown plainly
        Assert.Equal(new ConjugationCell(null, CellState.Locked, 0), At(table, "1", "pl", "pres")); // not taught, not said: nothing to show yet
    }

    [Fact]
    public void ATenseNoLessonHasTaughtAndTheLearnerHasNotUsedIsHiddenUntilItsLesson()
    {
        var querer = Verb("querer", Form("quiero", "1", "sg", "pres", "ind", 1));
        var first = new[] { Taught("querer", "1", "sg", "pres") };

        var early = ConjugationTable.Build(querer, [], first, TestGame.Bundle.SpanishMorphology(), null, TestGame.Data.Words);
        Assert.Equal(["pres"], early.Columns.Select(static c => c.Tense));

        // The lesson that teaches the past starts: the column appears (with the taught forms plain), still not the future.
        var later = ConjugationTable.Build(querer, [], [.. first, Taught("querer", "1", "sg", "pret")], TestGame.Bundle.SpanishMorphology(), null, TestGame.Data.Words);
        Assert.Equal(["pres", "pret"], later.Columns.Select(static c => c.Tense));
        Assert.Equal(new ConjugationCell("quise", CellState.Taught, 0), At(later, "1", "sg", "pret"));
        Assert.Equal(["present", "past"], later.Columns.Select(static c => TestGame.Data.Locale.Ui(c.TitleId)));
    }

    [Fact]
    public void AFormTheLearnerUsedShowsItsColumnEvenWhenNoLessonTaughtIt()
    {
        var table = ConjugationTable.Build(Verb("ver", Form("vi", "1", "sg", "pret", "ind", 1)), [], [], TestGame.Bundle.SpanishMorphology(), null, TestGame.Data.Words);

        Assert.Equal(["pret"], table.Columns.Select(static c => c.Tense)); // what the learner said is never hidden from them
        Assert.Equal(CellState.Used, At(table, "1", "sg", "pret").State);
    }

    [Fact]
    public void AVerbWithNoFiniteFormYetStillGetsThePresentColumn()
    {
        var table = ConjugationTable.Build(Verb("comer", Form("comer", null, null, null, null, 2)), [], [], TestGame.Bundle.SpanishMorphology(), null, TestGame.Data.Words);

        Assert.Equal(["pres"], table.Columns.Select(static c => c.Tense));
        Assert.All(table.Rows, static r => Assert.Equal(CellState.Locked, r.Cells[0].State));
    }

    [Fact]
    public void TheFutureColumnIsBuiltFromIrsPresentAPlusTheInfinitive()
    {
        var comer = Verb("comer", Form("comer", null, null, null, null, 2));
        var table = ConjugationTable.Build(comer, [VoyAComer(1), VoyAComer(2)], TaughtComer, TestGame.Bundle.SpanishMorphology(), Glosses.Value.ForLemma("comer"), TestGame.Data.Words);

        Assert.Equal(["ir-a", "pret"], table.Columns.Select(static c => c.Tense).OrderBy(static t => t, StringComparer.Ordinal));
        var future = table.Columns.ToList().FindIndex(static c => c.Tense == "ir-a");
        Assert.Equal("future (ir a)", TestGame.Data.Locale.Ui(table.Columns[future].TitleId));
        Assert.Equal(new ConjugationCell("voy a comer", CellState.Used, 2), At(table, "1", "sg", "ir-a"));
        Assert.Equal(new ConjugationCell("vas a comer", CellState.Taught, 0), At(table, "2", "sg", "ir-a"));
        Assert.Equal(new ConjugationCell(null, CellState.Locked, 0), At(table, "3", "sg", "ir-a"));
        Assert.Equal(new ConjugationCell("van a comer", CellState.Taught, 0), At(table, "3", "pl", "ir-a"));
        Assert.Equal(new ConjugationCell("comí", CellState.Taught, 0), At(table, "1", "sg", "pret"));
        Assert.Equal(new ConjugationCell("comiste", CellState.Taught, 0), At(table, "2", "sg", "pret"));
    }

    [Fact]
    public void TheSimpleFutureIsTheFourthColumnShownOnceALessonTeachesOneOfItsForms()
    {
        // V16: the column after the future (ir a), hidden like the others until a lesson that is completed or under way teaches a form of it (session 9 is the first).
        var comprar = Verb("comprar", Form("compro", "1", "sg", "pres", "ind", 1));
        var before = new[] { Taught("comprar", "1", "sg", "pres"), Taught("comprar", "1", "sg", "pret"), Taught("comprar", "1", "sg", "ir-a") };

        var early = ConjugationTable.Build(comprar, [], before, TestGame.Bundle.SpanishMorphology(), null, TestGame.Data.Words);
        Assert.Equal(["pres", "pret", "ir-a"], early.Columns.Select(static c => c.Tense));

        var later = ConjugationTable.Build(comprar, [], [.. before, Taught("comprar", "1", "sg", "fut"), Taught("comprar", "3", "sg", "fut")], TestGame.Bundle.SpanishMorphology(), null, TestGame.Data.Words);
        Assert.Equal(["pres", "pret", "ir-a", "fut"], later.Columns.Select(static c => c.Tense));
        Assert.Equal(["present", "past", "future (ir a)", "future"], later.Columns.Select(static c => TestGame.Data.Locale.Ui(c.TitleId)));
        Assert.Equal(new ConjugationCell("compraré", CellState.Taught, 0), At(later, "1", "sg", "fut"));
        Assert.Equal(new ConjugationCell("comprará", CellState.Taught, 0), At(later, "3", "sg", "fut"));
        Assert.Equal(new ConjugationCell(null, CellState.Locked, 0), At(later, "2", "sg", "fut"));
        Assert.Equal(ConjugationTable.AllColumns.Take(4), later.Columns); // the fifth, tener que (V21b), waits for its own lesson
    }

    [Fact]
    public void TenerQueIsTheFifthColumnShownOnceTheLessonThatTeachesItIsDone()
    {
        // V21b: «tengo que ir al río» (session 8's tener que) has its column beside the future (ir a), built from tener's present and the infinitive.
        var ir = Verb("ir", Form("voy", "1", "sg", "pres", "ind", 1));
        var morphology = TestGame.Bundle.SpanishMorphology();

        var before = TestGame.NewSessionWithShippedLessons();
        SessionSkip.Apply(before, 8); // Ayer en el pueblo is next, not begun
        var early = ConjugationTable.Build(ir, [], BookPages.TaughtForms(before), morphology, null, TestGame.Data.Words);
        Assert.DoesNotContain("tener-que", early.Columns.Select(static c => c.Tense));

        var after = TestGame.NewSessionWithShippedLessons();
        SessionSkip.Apply(after, 9); // Ayer en el pueblo is done
        var table = ConjugationTable.Build(ir, [], BookPages.TaughtForms(after), morphology, null, TestGame.Data.Words);
        Assert.Contains("tener-que", table.Columns.Select(static c => c.Tense));
        Assert.Equal("must (tener que)", TestGame.Data.Locale.Ui(table.Columns.Single(static c => c.Tense == "tener-que").TitleId));
        Assert.Equal(new ConjugationCell("tengo que ir", CellState.Taught, 0), At(table, "1", "sg", "tener-que"));
        Assert.Equal(new ConjugationCell(null, CellState.Locked, 0), At(table, "2", "sg", "tener-que"));

        // Said, it is counted: «tengo que ir al río» uses the form.
        var said = Entry(1, "tengo que ir al río", [Word("tengo", 0, "tener", "1", "sg", "pres", "ind"), Word("que", 6, "que"), Word("ir", 10, "ir"), Word("al", 13, "a"), Word("río", 16, "río")]);
        var used = ConjugationTable.Build(ir, [said], [], morphology, null, TestGame.Data.Words);
        Assert.Equal(new ConjugationCell("tengo que ir", CellState.Used, 1), At(used, "1", "sg", "tener-que"));
    }

    [Fact]
    public void ASimpleFutureTheLearnerSaidIsCountedFromTheWordRecordAndShowsItsColumn()
    {
        // As the present and the past: the word record's forms, by the core's reading (compraré: comprar, 1 sg, fut), whether or not a lesson taught them yet.
        var comprar = Verb("comprar", Form("compraré", "1", "sg", "fut", "ind", 2), Form("comprarás", "2", "sg", "fut", "ind", 1, question: true), Form("compré", "1", "sg", "pret", "ind", 1));
        var table = ConjugationTable.Build(comprar, [], [Taught("comprar", "3", "sg", "fut")], TestGame.Bundle.SpanishMorphology(), null, TestGame.Data.Words);

        Assert.Equal(["pret", "fut"], table.Columns.Select(static c => c.Tense));
        Assert.Equal(new ConjugationCell("compraré", CellState.Used, 2), At(table, "1", "sg", "fut"));
        Assert.Equal(new ConjugationCell("comprarás", CellState.Used, 1), At(table, "2", "sg", "fut"));
        Assert.Equal(new ConjugationCell("comprará", CellState.Taught, 0), At(table, "3", "sg", "fut"));
        Assert.Equal(new ConjugationCell("compré", CellState.Used, 1), At(table, "1", "sg", "pret"));
        Assert.Equal(new ConjugationCell(null, CellState.Locked, 0), At(table, "1", "pl", "fut"));

        // ir's future is its own word (iré), never the future (ir a) of ir.
        var ir = ConjugationTable.Build(Verb("ir", Form("iré", "1", "sg", "fut", "ind", 1)), [], [], TestGame.Bundle.SpanishMorphology(), null, TestGame.Data.Words);
        Assert.Equal(["fut"], ir.Columns.Select(static c => c.Tense));
        Assert.Equal(new ConjugationCell("iré", CellState.Used, 1), At(ir, "1", "sg", "fut"));
    }

    [Fact]
    public void VamosAComerCountsAsTheNosotrosCellOfTheFutureWhateverTheCoreMadeOfVamos()
    {
        var comer = Verb("comer", Form("comer", null, null, null, null, 1));
        var table = ConjugationTable.Build(comer, [VamosAComer(1)], TaughtComer, TestGame.Bundle.SpanishMorphology(), null, TestGame.Data.Words);

        Assert.Equal(new ConjugationCell("vamos a comer", CellState.Used, 1), At(table, "1", "pl", "ir-a"));
    }

    [Fact]
    public void VamosIsIrInThePresentForNosotrosWhetherItIsBareOrBeforeAAndAnInfinitive()
    {
        // The word record shows both as "vamos (1 pl imp)": no tense, an imperative. The book counts them as ir, present, 1 pl.
        var viaImperative = Verb("ir", Form("voy", "1", "sg", "pres", "ind", 1), Form("vamos", "1", "pl", null, "imp", 2));
        var table = ConjugationTable.Build(viaImperative, [VamosAComer(1)], [Taught("ir", "1", "sg", "pres")], TestGame.Bundle.SpanishMorphology(), Glosses.Value.ForLemma("ir"), TestGame.Data.Words);
        Assert.Equal(new ConjugationCell("vamos", CellState.Used, 2), At(table, "1", "pl", "pres"));
        Assert.Equal(new ConjugationCell("voy", CellState.Used, 1), At(table, "1", "sg", "pres"));

        // A bare "vamos" whose reading has no person at all is still ir's nosotros form, by its text.
        var bare = Verb("ir", Form("vamos", null, null, null, "imp", 1));
        Assert.Equal(new ConjugationCell("vamos", CellState.Used, 1), At(ConjugationTable.Build(bare, [], [], TestGame.Bundle.SpanishMorphology(), null, TestGame.Data.Words), "1", "pl", "pres"));

        // The text rule is for ir alone: "vamos" under another verb is not placed.
        var other = Verb("comer", Form("vamos", "1", "pl", null, "imp", 1));
        Assert.Equal(CellState.Locked, At(ConjugationTable.Build(other, [], [], TestGame.Bundle.SpanishMorphology(), null, TestGame.Data.Words), "1", "pl", "pres").State);
    }

    [Fact]
    public void ARealImperativeOrASubjunctiveFormIsNotPlacedInAnIndicativeCell()
    {
        var table = ConjugationTable.Build(Verb("querer", Form("quiera", "1", "sg", "pres", "subj", 4), Form("quiero", "1", "sg", "pres", "ind", 1)), [], [], TestGame.Bundle.SpanishMorphology(), null, TestGame.Data.Words);

        Assert.Equal(new ConjugationCell("quiero", CellState.Used, 1), At(table, "1", "sg", "pres"));
    }

    [Fact]
    public void LinesTheRecordDoesNotCountAreNotCountedInTheFuture()
    {
        var comer = Verb("comer", Form("comer", null, null, null, null, 1));
        var history = new[]
        {
            VoyAComer(1, InputSource.Button), // a repair button
            VoyAComer(2, place: InputPlace.Check), // a check answer
            VoyAComer(3, held: true), // an unsure recognition
            VoyAComer(4),
        };

        var table = ConjugationTable.Build(comer, history, TaughtComer, TestGame.Bundle.SpanishMorphology(), null, TestGame.Data.Words);

        Assert.Equal(1, At(table, "1", "sg", "ir-a").Uses);
    }

    [Fact]
    public void AnAInAnotherPlaceOrAnotherVerbAfterItIsNotTheFuture()
    {
        var comer = Verb("comer", Form("comer", null, null, null, null, 1));
        var history = new[]
        {
            Entry(1, "voy al río", [Word("voy", 0, "ir", "1", "sg", "pres", "ind"), Word("al", 4, "a"), Word("río", 7, "río")]),
            Entry(2, "voy a beber agua", [Word("voy", 0, "ir", "1", "sg", "pres", "ind"), Word("a", 4, "a"), Word("beber", 6, "beber")]),
        };

        var table = ConjugationTable.Build(comer, history, TaughtComer, TestGame.Bundle.SpanishMorphology(), null, TestGame.Data.Words);

        Assert.Equal(0, At(table, "1", "sg", "ir-a").Uses);
        Assert.Equal(CellState.Taught, At(table, "1", "sg", "ir-a").State);
    }

    [Fact]
    public void EveryFormComesFromTheCoresMorphologyAndTheShippedLessonsDeclareOnlyFormsItKnows()
    {
        var morphology = TestGame.Bundle.SpanishMorphology();
        var session = RouteScene.NewSessionWithShippedLessons(TestGame.Bundle, TestGame.Nouns);
        foreach (var lesson in session.Lessons.Book.Lessons)
        {
            foreach (var form in lesson.TaughtVerbs)
            {
                var table = ConjugationTable.Build(Verb(form.Lemma, Form(form.Lemma, null, null, null, null, 1)), [], [form], morphology, null, TestGame.Data.Words);
                var cell = At(table, form.Person, form.Number, form.Tense);
                Assert.Equal(CellState.Taught, cell.State);
                Assert.Equal(form.Text(morphology, TestGame.Data.Words.NearFuture, TestGame.Data.Words.Obligation), cell.Text);
            }
        }
    }
}
