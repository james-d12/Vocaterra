namespace Vocaterra.Game.Tests;

/// <summary>
/// The run-time phrase sheet (<c>game/phrases.json</c>, S7): what each NPC prompt expects of the learner (the "Esperaba" of the panel's heard-versus-expected
/// line), and the labels of the buttons for the choices the repair ladder offers. That the file agrees with <c>content/PHRASES.md</c> row by row is
/// tested in the host tests (<c>PhraseSheetFileTests</c>).
/// </summary>
public sealed class PhraseSheetLookupTests
{
    private static PhraseSheet Sheet => PhraseSheet.Load(TestGame.Bundle);

    [Fact]
    public void TheBundleHoldsTheTwentyOnePhrasesOfTheSheetInOrder()
    {
        var sheet = Sheet;

        Assert.Equal(
            ["que-necesitas", "pedir-agua", "quieres-pan", "si-por-favor", "no-gracias", "donde-esta-el-rio", "hola", "gracias", "adios", "no-se", "ayuda",
                "me-llamo", "es-madera", "si", "no", "que-es-esto", "quieres-fruta", "de-nada", "otra-vez", "mas-despacio", "necesitas-madera"],
            sheet.Rows.Select(static r => r.Id));
        Assert.Equal("Quiero agua, por favor.", sheet.Find("pedir-agua")!.CardPhrase);
        Assert.Equal("¿Dónde está el río?", sheet.Find("donde-esta-el-rio")!.CardPhrase);
        Assert.Null(sheet.Find("no-such-phrase"));
    }

    [Fact]
    public void TheStallsQuestionExpectsTheRequestForWaterWhateverWasHeard()
    {
        var sheet = Sheet;

        // Understood: the row of the intent that matched.
        Assert.Equal("pedir-agua", sheet.Expected("stall-need", "request_item", true, "quiero agua")!.Id);

        // Not understood: the prompt's only learner phrase (help and "no sé" are not what the prompt waits for).
        Assert.Equal("pedir-agua", sheet.Expected("stall-need", "unknown", false, "agua agua mesa")!.Id);
        Assert.Equal("pedir-agua", sheet.Expected("stall-need", "ask_help", true, "ayuda")!.Id);
        Assert.Equal("pedir-agua", sheet.Expected("stall-need", "dont_know", true, "no sé")!.Id);
    }

    [Fact]
    public void AnOfferHasTwoPhrasesAndTheMatchedIntentPicksOneAndTheClosestOneWhenNothingMatched()
    {
        var sheet = Sheet;

        Assert.Equal("si-por-favor", sheet.Expected("stall-offer", "confirm", true, "claro")!.Id);
        Assert.Equal("no-gracias", sheet.Expected("stall-offer", "deny", true, "no gracias")!.Id);
        Assert.Equal("no-gracias", sheet.Expected("stall-offer", "unknown", false, "no gracia")!.Id);
        Assert.Equal("si-por-favor", sheet.Expected("stall-offer", "unknown", false, "si por favo")!.Id);
        Assert.Equal("si-por-favor", sheet.Expected("stall-offer", "unknown", false, string.Empty)!.Id); // nothing heard: the first row

        // The guide's offer at the camp is answered with the same two phrases.
        Assert.Equal("no-gracias", sheet.Expected("guide-offer", "deny", true, "no")!.Id);
        Assert.Equal(["si-por-favor", "no-gracias"], sheet.PhrasesAt("guide-offer").Select(static r => r.Id));
    }

    [Fact]
    public void TheVillagersOpenPromptTakesTheRowOfTheIntentThatMatchedAndElseTheClosestRow()
    {
        var sheet = Sheet;

        Assert.Equal("donde-esta-el-rio", sheet.Expected("villager-open", "ask_directions", true, "dónde está el río")!.Id);
        Assert.Equal("hola", sheet.Expected("villager-open", "greet", true, "buenos días")!.Id);
        Assert.Equal("hola", sheet.Expected("villager-open", "unknown", false, "gola")!.Id);
        Assert.Equal("adios", sheet.Expected("villager-open", "unknown", false, "adiós")!.Id);
        Assert.Equal("donde-esta-el-rio", sheet.Expected("villager-open", "unknown", false, "donde esta rio")!.Id);
    }

    [Fact]
    public void TheGuidesOpenPromptHasNoRowsOfItsOwnSoOnlyAnIntentThatIsInTheSheetGivesAnExpectedPhrase()
    {
        var sheet = Sheet;

        Assert.Equal("donde-esta-el-rio", sheet.Expected("guide-open", "ask_directions", true, "dónde está el río")!.Id);
        Assert.Null(sheet.Expected("guide-open", "ask_craft", true, "cómo hago una antorcha"));
        Assert.Null(sheet.Expected("guide-open", "unknown", false, "mesa"));
    }

    [Fact]
    public void ChoicesBecomeButtonsLabelledWithTheSheetsPhraseAndTwoPhrasesOfOneRowAreOneButton()
    {
        var sheet = Sheet;

        // What the core offers at the stall's question: the first three of the answer set's phrases in ordinal order.
        var options = sheet.OptionsFor(["agua por favor", "ayuda", "ayuda por favor"]);

        Assert.Equal(["Quiero agua, por favor.", "Ayuda."], options.Select(static o => o.Label));
        Assert.Equal(["agua por favor", "ayuda"], options.Select(static o => o.Line)); // the choice sends the phrase the NPC offered

        var offer = sheet.OptionsFor(["ayuda", "ayuda por favor", "claro"]);
        Assert.Equal(["Ayuda.", "Sí, por favor."], offer.Select(static o => o.Label));

        var unknown = sheet.OptionsFor(["mesa por favor"]);
        Assert.Equal("Mesa por favor.", unknown.Single().Label); // a phrase that is no row: itself, capitalized
    }

    [Fact]
    public void ThePlainFormIgnoresCaseAccentsAndPunctuation()
    {
        Assert.Equal("donde esta el rio", PhraseSheet.Plain("¿Dónde está el río?"));
        Assert.Equal("si por favor", PhraseSheet.Plain("Sí, por favor."));
        Assert.Equal(string.Empty, PhraseSheet.Plain(" ¿? "));
    }

    [Fact]
    public void AMalformedSheetIsRefusedWithAMessage()
    {
        Assert.Throws<InvalidDataException>(() => PhraseSheet.Parse("{ \"schemaVersion\": 2, \"phrases\": [] }"));
        Assert.Throws<InvalidDataException>(() => PhraseSheet.Parse("not json"));
        Assert.Throws<InvalidDataException>(() => PhraseSheet.Parse(
            "{ \"schemaVersion\": 1, \"phrases\": [ { \"id\": \"a\", \"says\": \"says\", \"spanish\": \"A\", \"english\": \"A\", \"card\": \"A = A\" }, { \"id\": \"a\", \"says\": \"says\", \"spanish\": \"A\", \"english\": \"A\", \"card\": \"A = A\" } ] }"));
    }
}
