using Vocaterra.Game.Core.Language.Book;
using Vocaterra.Game.Core.Language.Words;

namespace Vocaterra.Game.Core.Tests.Language.Book;

// The verb-form labels of the book's Verbs page (S20): the English of each reading from a small table over person, number, tense and mood.
public sealed class BookVerbLabelsTests
{
    private static readonly VerbGloss Querer = new("querer", "to want", null, null);
    private static readonly VerbGloss Ser = new("ser", "to be", null, null);
    private static readonly VerbGloss Estar = new("estar", "to be", null, null);
    private static readonly VerbGloss Tener = new("tener", "to have", "has", "had");
    private static readonly VerbGloss Ir = new("ir", "to go", "goes", "went");

    private static string Label(string form, VerbGloss gloss, string person, string number, string tense, string mood, bool question = false) =>
        BookVerbLabels.Label(form, gloss.Lemma, gloss, person, number, tense, mood, question);

    [Theory]
    [InlineData("1", "sg", "I want")]
    [InlineData("2", "sg", "you want")]
    [InlineData("3", "sg", "he/she/it wants")]
    [InlineData("1", "pl", "we want")]
    [InlineData("2", "pl", "you all want")]
    [InlineData("3", "pl", "they want")]
    public void ThePresentIndicativeGivesEachPersonAndNumber(string person, string number, string english)
    {
        Assert.Equal(english, BookVerbLabels.English(Querer.English, Querer.Third, Querer.Past, person, number, "pres", "ind", question: false));
    }

    [Theory]
    [InlineData("1", "sg", "I wanted")]
    [InlineData("3", "sg", "he/she/it wanted")]
    [InlineData("2", "pl", "you all wanted")]
    public void ThePreteriteGivesThePastOfTheVerb(string person, string number, string english)
    {
        Assert.Equal(english, BookVerbLabels.English(Querer.English, Querer.Third, Querer.Past, person, number, "pret", "ind", question: false));
    }

    [Theory]
    [InlineData("impf", "ind", "I used to want")]
    [InlineData("fut", "ind", "I will want")]
    [InlineData("cond", "ind", "I would want")]
    [InlineData("pres", "subj", "that I want")]
    public void TheOtherTensesAndTheSubjunctiveUseTheirOwnFrame(string tense, string mood, string english)
    {
        Assert.Equal(english, BookVerbLabels.English(Querer.English, Querer.Third, Querer.Past, "1", "sg", tense, mood, question: false));
    }

    [Theory]
    [InlineData("2", "sg", "want!")]
    [InlineData("2", "pl", "want! (you all)")]
    [InlineData("1", "pl", "let's want")]
    public void TheImperativeGivesACommandForTheSecondPersonAndALetsForTheFirst(string person, string number, string english)
    {
        Assert.Equal(english, BookVerbLabels.English(Querer.English, Querer.Third, Querer.Past, person, number, null, "imp", question: false));
    }

    [Fact]
    public void AQuestionTurnsTheFrameAroundWithDoOrDoesAndTheSpanishGetsItsMarks()
    {
        Assert.Equal("¿quieres? — do you want?", Label("quieres", Querer, "2", "sg", "pres", "ind", question: true));
        Assert.Equal("¿quiere? — does he/she/it want?", Label("quiere", Querer, "3", "sg", "pres", "ind", question: true));
        Assert.Equal("¿quisiste? — did you want?", Label("quisiste", Querer, "2", "sg", "pret", "ind", question: true));
        Assert.Equal("¿quieres? — will you want?", BookVerbLabels.Label("quieres", "querer", Querer, "2", "sg", "fut", "ind", question: true));
    }

    [Theory]
    [InlineData("soy", "1", "sg", "I am")]
    [InlineData("eres", "2", "sg", "you are")]
    [InlineData("es", "3", "sg", "he/she/it is")]
    [InlineData("somos", "1", "pl", "we are")]
    [InlineData("son", "3", "pl", "they are")]
    public void ToBeHasItsOwnFormsInThePresent(string form, string person, string number, string english)
    {
        Assert.Equal(form + " — " + english, Label(form, Ser, person, number, "pres", "ind"));
    }

    [Fact]
    public void EstaReadsAsHeSheItIsAndTheOtherToBeFormsAsTheirTenses()
    {
        Assert.Equal("está — he/she/it is", Label("está", Estar, "3", "sg", "pres", "ind"));
        Assert.Equal("fue — he/she/it was", Label("fue", Ser, "3", "sg", "pret", "ind"));
        Assert.Equal("estuvimos — we were", Label("estuvimos", Estar, "1", "pl", "pret", "ind"));
        Assert.Equal("¿estás? — are you?", Label("estás", Estar, "2", "sg", "pres", "ind", question: true));
        Assert.Equal("estaba — I used to be", Label("estaba", Estar, "1", "sg", "impf", "ind"));
    }

    [Fact]
    public void SerAndEstarGlossedByWhatTheyAreForStillConjugateAsToBe()
    {
        var serWhat = new VerbGloss("ser", "to be: what", null, null);
        var estarWhere = new VerbGloss("estar", "to be: where or how", null, null);
        Assert.Equal("soy — I am", Label("soy", serWhat, "1", "sg", "pres", "ind"));
        Assert.Equal("está — he/she/it is", Label("está", estarWhere, "3", "sg", "pres", "ind"));
        Assert.Equal("fue — he/she/it was", Label("fue", serWhat, "3", "sg", "pret", "ind"));
        Assert.Equal("I will be", BookVerbLabels.English(serWhat.English, null, null, "1", "sg", "fut", "ind", question: false));
        Assert.Equal("that I be", BookVerbLabels.English(estarWhere.English, null, null, "1", "sg", "pres", "subj", question: false));
        Assert.Equal("ser — to be: what", BookVerbLabels.Label("ser", "ser", serWhat, null, null, null, null, question: false));
        Assert.Equal("estar — to be: where or how", BookVerbLabels.Label("estar", "estar", estarWhere, null, null, null, null, question: false));
    }

    [Fact]
    public void VamosOfAnotherVerbIsLeftAsTheCoreReadsIt()
    {
        var comer = new VerbGloss("comer", "to eat", null, null);
        Assert.Equal("vamos — let's eat", BookVerbLabels.Label("vamos", "comer", comer, "1", "pl", null, "imp", question: false));
    }

    [Fact]
    public void AnIrregularVerbUsesTheThirdPersonAndPastFromItsGloss()
    {
        Assert.Equal("tiene — he/she/it has", Label("tiene", Tener, "3", "sg", "pres", "ind"));
        Assert.Equal("va — he/she/it goes", Label("va", Ir, "3", "sg", "pres", "ind"));
        Assert.Equal("fue — he/she/it went", Label("fue", Ir, "3", "sg", "pret", "ind"));
    }

    [Theory]
    [InlineData("to play", "plays", "played")]
    [InlineData("to try", "tries", "tried")]
    [InlineData("to pass", "passes", "passed")]
    [InlineData("to live", "lives", "lived")]
    public void TheRegularRulesGiveTheThirdPersonAndThePastOfAGlossWithNoOverride(string english, string third, string past)
    {
        Assert.Equal("he/she/it " + third, BookVerbLabels.English(english, null, null, "3", "sg", "pres", "ind", question: false));
        Assert.Equal("he/she/it " + past, BookVerbLabels.English(english, null, null, "3", "sg", "pret", "ind", question: false));
    }

    [Fact]
    public void AnInfinitiveIsLabelledWithItsGlossAndAnUnknownReadingIsTheSpanishAlone()
    {
        Assert.Equal("querer — to want", BookVerbLabels.Label("querer", "querer", Querer, null, null, null, null, question: false));
        Assert.Equal("querido", BookVerbLabels.Label("querido", "querer", Querer, null, null, null, null, question: false));
        Assert.Equal("quiero", BookVerbLabels.Label("quiero", "querer", null, "1", "sg", "pres", "ind", question: false));
        Assert.Null(BookVerbLabels.English("to want", null, null, "1", "sg", "fut", "subj", question: false));
        Assert.Null(BookVerbLabels.Subject(null, "sg"));
        Assert.Equal("I", BookVerbLabels.Subject("1", "sg"));
    }

    [Fact]
    public void TheCommandAndTheSubjectOfEveryPersonAreWrittenOut()
    {
        Assert.Equal("I", BookVerbLabels.Subject("1", "sg"));
        Assert.Equal("you", BookVerbLabels.Subject("2", "sg"));
        Assert.Equal("he/she/it", BookVerbLabels.Subject("3", "sg"));
        Assert.Equal("we", BookVerbLabels.Subject("1", "pl"));
        Assert.Equal("you all", BookVerbLabels.Subject("2", "pl"));
        Assert.Equal("they", BookVerbLabels.Subject("3", "pl"));
    }
}
