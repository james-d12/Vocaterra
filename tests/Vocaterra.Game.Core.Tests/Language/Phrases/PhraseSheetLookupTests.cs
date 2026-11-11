using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Language.Phrases;

/// <summary>
/// The run-time phrase sheet (<c>game/phrases.json</c>, S7): what each NPC prompt expects of the learner (the "Esperaba" of the panel's heard-versus-expected
/// line), and the labels of the buttons for the choices the repair ladder offers. That the file agrees with <c>content/PHRASES.md</c> row by row is
/// tested in the host tests (<c>PhraseSheetFileTests</c>).
/// </summary>
public sealed class PhraseSheetLookupTests
{
    private static PhraseSheet Sheet => RouteScene.DataOf(TestGame.Bundle).Phrases;

    [Fact]
    public void ARowWhoseEveryVariantIsInThePluralNamesTheItemAsked()
    {
        // V30: `quiero manzanas` names the item `manzana` (the catalogue's plural), not only a text that says the singular.
        var row = new PhraseRow("quiero-manzanas", "says", "Quiero manzanas.", "I want apples.", "request_item", "baker-open", ["Quiero dos manzanas."], "Quiero manzanas.", []);
        var slots = new Dictionary<string, string> { ["item"] = "manzana" };

        Assert.Equal("quiero-manzanas", PhraseSheet.OfIntent([row], "request_item", slots, TestGame.Nouns));
        Assert.Null(PhraseSheet.OfIntent([row], "request_item", new Dictionary<string, string> { ["item"] = "pan" }, TestGame.Nouns));
    }

    [Fact]
    public void TheBundleHoldsTheHundredAndEightyPhrasesOfTheSheetInOrder()
    {
        var sheet = Sheet;

        Assert.Equal(
            ["que-necesitas", "pedir-agua", "quieres-pan", "si-por-favor", "no-gracias", "donde-esta-el-rio", "hola", "gracias", "adios", "que-tal", "muy-bien", "no-se", "ayuda",
                "me-llamo", "es-madera", "si", "no", "que-es-esto", "quieres-fruta", "de-nada", "otra-vez", "mas-despacio", "necesitas-madera",
                "que-quieres", "quiero-madera", "tienes-fruta", "tengo-fruta", "necesito-agua",
                "que-quiere-luna", "ella-quiere-fruta", "necesitamos-agua", "que-necesita-usted", "necesito-pan", "tengo-botella", "que-quieren-ustedes", "queremos-pan",
                "que-vas-a-comer", "voy-a-comer-pan", "que-vamos-a-comer", "vamos-a-comer-fruta", "que-comiste-ayer", "ayer-comi-fruta", "fuiste-al-rio-ayer", "ayer-fui-al-rio", "bebiste-agua-ayer", "ayer-bebi-agua", "que-vas-a-hacer", "voy-a-ir-al-rio",
                "que-buscas", "donde-esta-la-panaderia", "esta-a-la-izquierda", "donde-esta-la-herreria", "esta-lejos", "esta-cerca", "hay-tres-casas", "hay-un-pozo", "adonde-vas", "voy-a-la-panaderia",
                "que-vas-a-hacer-alli", "voy-a-comprar-pan", "el-letrero", "fuiste-a-la-panaderia", "fui-a-la-panaderia",
                "uno-a-cinco", "seis-a-diez", "tengo-diez-panes", "cuantos-panes", "quiero-dos-panes", "cuanto-cuesta", "cuesta-dos-monedas", "necesito-madera-para-el-horno", "voy-a-buscar-madera",
                "toma-cuatro-monedas", "tengo-cuatro-monedas", "ayer-compre-fruta", "que-compraste", "compre-dos-panes", "compraste-dos-panes", "hay-pan",
                "como-te-llamas", "me-llamo-domingo", "que-haces", "soy-el-herrero", "es-el-herrero", "que-va-a-hacer-domingo", "va-a-hacer-un-hacha", "donde-vives",
                "vivo-en-el-campamento", "quieres-un-hacha", "toma-el-hacha", "trabaja-en-la-herreria", "vive-en-la-casa", "que-hizo-domingo", "hizo-un-hacha", "ayer-trabajo-mucho",
                "mis-manzanas-son-rojas", "como-es-la-manzana", "la-manzana-es-roja", "te-gusta-el-pan", "me-gusta-el-pan", "me-gustan-las-manzanas", "quiero-tres-manzanas-rojas", "mis-plantas-necesitan-agua",
                "voy-a-buscar-agua", "es-grande", "el-pan-es-de-luz", "toma-tres-manzanas", "fui-al-rio", "ayer-me-gusto", "te-gusto-el-pan", "me-gusto-el-pan",
                "el-dia-de-ayer", "que-hizo-luz", "luz-hizo-pan", "que-hizo-mar", "mar-pesco-en-el-rio", "comieron-en-la-plaza", "fuimos-a-la-tienda", "adonde-fueron",
                "fuimos-al-rio", "que-comieron", "ellos-comieron-pan", "adonde-tienes-que-ir", "tengo-que-ir", "le-vendi-pescado", "que-compro-luz", "luz-compro-pescado",
                "que-haras-manana", "manana-comprare-pan", "tendre-pan", "luz-necesita-madera", "buscare-madera", "mar-pescara", "ire-al-rio", "el-dia-de-manana", "que-hara-luz", "luz-hara-pan",
                "tienes-veinte-panes", "quiero-vender-madera", "vendo-madera", "cuanto-es-todo", "es-muy-caro", "mas-barato-que", "puedo-pagar-con-fruta", "manana-vendere-fruta", "compre-tres-manzanas",
                "once-a-quince", "dieciseis-a-veinte", "cuantos-panes-tengo", "te-pago-una-moneda", "tengo-quince-manzanas", "quince-monedas", "es-caro-o-barato", "que-es-mas-barato", "te-compro-fruta", "que-venderas-manana", "que-compraste-en-el-mercado",
        "pon-la-harina", "echa-el-agua", "mezcla", "pon-la-madera", "pon-la-masa", "saca-el-pan", "haz-la-masa", "haz-el-pan-horno", "haz-pan", "voy-a-hacer-pan", "hice-pan", "quiero-harina", "vendo-pan", "hoy-vamos-a-hacer-pan", "que-hiciste", "todavia-no", "toma-aqui-tienes", "quieres-hacer-pan",
                "que-tienes-para-mi", "aqui-tienes-tres-frutas", "aqui-tienes-cinco-bloques", "aqui-tienes-la-madera-y-las-monedas", "aqui-tienes-el-agua", "quiero-una-habitacion", "hay-una-habitacion"],
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
        Assert.Equal("pedir-agua", sheet.Expected("stall-need", "unknown", false, "quiero agua mesa")!.Id);
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
    public void APromptAnNpcWaitsOnWithNoRowsIsAnsweredLikeAnotherNpcPromptThatAcceptsTheSame()
    {
        // V01: the guide's offer took the stall's offer rows by name in the code. Now any prompt an NPC file names (guide-offer: the yes-no answer set, confirm, deny,
        // help, "no sé") that no row names takes the rows of another NPC's prompt that accepts exactly the same answers (stall-offer). The guide's open prompt
        // and her review share an answer set with the villager's but not its intents, so they stay without rows; a lesson's prompt is not an NPC's.
        Assert.Empty(Sheet.PhrasesAt("guide-open"));
        Assert.Empty(Sheet.PhrasesAt("guide-review"));
        Assert.Empty(Sheet.PhrasesAt("lesson-name"));

        // The rule comes from the data: NPCs that waited on lesson-name and lesson-unstuck (one answer set, the same intents) would share lesson-unstuck's rows.
        var npcs = TestGame.Data.Npcs;
        var roster = NpcRoster.Of(
        [
            npcs.Find("guide")! with { Prompts = new Dictionary<string, string>(StringComparer.Ordinal) { ["idle"] = "lesson-name" }, ReviewPrompt = null },
            npcs.Find("villager")! with { Prompts = new Dictionary<string, string>(StringComparer.Ordinal) { ["idle"] = "lesson-unstuck" } },
        ]);
        var sheet = PhraseSheet.Load(TestGame.Bundle, TestGame.Locale, roster);
        Assert.Equal(["otra-vez", "mas-despacio"], sheet.PhrasesAt("lesson-name").Select(static r => r.Id));
        Assert.Empty(sheet.PhrasesAt("guide-offer")); // no NPC waits on it here
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
        var locale = TestGame.Locale.With(new Dictionary<string, string>(StringComparer.Ordinal) { ["phrase.a.meaning"] = "A", ["phrase.a.card"] = "A = A" });
        Assert.Throws<InvalidDataException>(() => PhraseSheet.Parse("{ \"schemaVersion\": 2, \"phrases\": [] }", locale));
        Assert.Throws<InvalidDataException>(() => PhraseSheet.Parse("not json", locale));
        Assert.Throws<InvalidDataException>(() => PhraseSheet.Parse(
            "{ \"schemaVersion\": 1, \"phrases\": [ { \"id\": \"a\", \"says\": \"says\", \"spanish\": \"A\" }, { \"id\": \"a\", \"says\": \"says\", \"spanish\": \"A\" } ] }", locale));
        var one = PhraseSheet.Parse("{ \"schemaVersion\": 1, \"phrases\": [ { \"id\": \"a\", \"says\": \"says\", \"spanish\": \"A\" } ] }", locale);
        Assert.Equal(("A", "A = A"), (one.Rows[0].English, one.Rows[0].Card)); // the meaning and the card are the locale's (C03)
        var noText = Assert.Throws<InvalidDataException>(() => PhraseSheet.Parse("{ \"schemaVersion\": 1, \"phrases\": [ { \"id\": \"b\", \"says\": \"says\", \"spanish\": \"B\" } ] }", locale));
        Assert.Contains("phrase.b.meaning", noText.Message, StringComparison.Ordinal);
    }
}
