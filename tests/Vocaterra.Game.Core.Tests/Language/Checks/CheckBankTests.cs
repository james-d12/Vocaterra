using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Language.Checks;

/// <summary>
/// The check battery's items (<c>game/checks.json</c>, S11): what is in the bundle, that a bad item is refused when it is read, and since VILLAGE_PLAN V07 the
/// lesson each belongs to.
/// </summary>
public sealed class CheckBankTests
{
    private static IReadOnlyList<CheckItem> Bank => RouteScene.DataOf(TestGame.Bundle).Checks;

    private static PhraseSheet Sheet => RouteScene.DataOf(TestGame.Bundle).Phrases;

    // One comprehension item and one production item, valid on their own; the tests break one thing at a time.
    private const string Comprehension =
        """{"id":"hear.x","kind":"comprehension","phrase":"pedir-agua","lesson":null,"line":"phrase.pedir-agua","spanish":"Quiero agua, por favor.","question":"?","options":[{"label":"a"},{"label":"b"},{"label":"c"}],"correct":0}""";

    private const string Production =
        """{"id":"say.x","kind":"production","phrase":"pedir-agua","lesson":null,"situation":"You want water.","prompt":"stall-need","intent":"request_item"}""";

    private static string Battery(params string[] items) => """{"schemaVersion":2,"items":[""" + string.Join(",", items) + "]}";

    [Fact]
    public void TheBundleHoldsFortyComprehensionItemsAndThirtyNineProductionItems()
    {
        // The pilot's 31 (sixteen heard, fifteen said) and, since VILLAGE_PLAN V10, El pueblo's eight (four of each), since V11 Números y pan's eight, since V12 Gente
        // del pueblo's eight, since V13 ¿Cómo es?'s eight, since V14 Ayer en el pueblo's eight and since V17 Mañana en el pueblo's eight (four of
        // each).
        var bank = Bank;

        Assert.Equal(79, bank.Count);
        Assert.Equal(40, bank.Count(static i => i.Kind == CheckItemKind.Comprehension));
        Assert.Equal(
            ["say.pedir-agua", "say.si-por-favor", "say.no-gracias", "say.donde-esta-el-rio", "say.si", "say.no", "say.que-es-esto", "say.otra-vez", "say.mas-despacio",
                "say.quiero-madera", "say.tengo-fruta", "say.ella-quiere-fruta", "say.necesitamos-agua", "say.voy-a-comer-pan", "say.ayer-comi-fruta",
                "say.donde-esta-la-panaderia", "say.esta-lejos", "say.voy-a-comprar-pan", "say.fui-a-la-panaderia",
                "say.quiero-dos-panes", "say.cuanto-cuesta", "say.voy-a-buscar-madera", "say.compre-dos-panes",
                "say.como-te-llamas", "say.que-haces", "say.va-a-hacer-un-hacha", "say.hizo-un-hacha",
                "say.la-manzana-es-roja", "say.me-gustan-las-manzanas", "say.quiero-tres-manzanas-rojas", "say.me-gusto-el-pan",
                "say.luz-hizo-pan", "say.fuimos-al-rio", "say.ellos-comieron-pan", "say.tengo-que-ir",
                "say.manana-comprare-pan", "say.buscare-madera", "say.ire-al-rio", "say.luz-hara-pan"],
            bank.Where(static i => i.Kind == CheckItemKind.Production).Select(static i => i.Id));
        Assert.Equal(
            ["request_item", "confirm", "deny", "ask_directions", "confirm", "deny", "ask_what", "ask_repeat", "ask_slower", "state_want", "state_have", "state_want", "state_want", "state_action", "state_action",
                "ask_directions", "ask_far", "state_action", "state_action", "request_item", "ask_price", "state_action", "state_action", "ask_name", "ask_activity", "state_action", "state_action",
                "describe", "state_like", "request_item", "state_like", "state_action", "state_action", "state_action", "state_obligation",
                "state_action", "state_action", "state_action", "state_action"],
            bank.Where(static i => i.Kind == CheckItemKind.Production).Select(static i => i.Intent));
    }

    [Fact]
    public void EveryItemBelongsToALessonOrIsOneOfTheFreeRunsEight()
    {
        // VILLAGE_PLAN V07: read from each item's phrase and the lessons' steps. Eight belong to no lesson: the stall's exchange and the villager's directions of the
        // first session's free run (que-necesitas, quieres-pan, pedir-agua, no-gracias, donde-esta-el-rio), which no lesson's step practises.
        var bank = RouteScene.DataOf(TestGame.Bundle).Checks;
        Assert.Equal(3, bank.CoreSessions);
        Assert.Equal(
            ["hear.que-necesitas", "hear.quieres-pan", "hear.pedir-agua", "hear.no-gracias", "hear.donde-esta-el-rio", "say.pedir-agua", "say.no-gracias", "say.donde-esta-el-rio"],
            bank.Where(static i => i.Lesson is null).Select(static i => i.Id));
        Assert.Equal(16, bank.Count(static i => i.Lesson == "first-words"));
        Assert.Equal(["hear.que-quieren-ustedes", "say.ella-quiere-fruta", "say.necesitamos-agua"], bank.Where(static i => i.Lesson == "personas").Select(static i => i.Id));
        Assert.Equal(["hear.que-vas-a-comer", "hear.que-comiste-ayer", "say.voy-a-comer-pan", "say.ayer-comi-fruta"], bank.Where(static i => i.Lesson == "ayer-y-manana").Select(static i => i.Id));
        Assert.Equal(
            ["hear.que-buscas", "hear.esta-a-la-izquierda", "hear.hay-tres-casas", "hear.fuiste-a-la-panaderia", "say.donde-esta-la-panaderia", "say.esta-lejos", "say.voy-a-comprar-pan", "say.fui-a-la-panaderia"],
            bank.Where(static i => i.Lesson == "el-pueblo").Select(static i => i.Id));
        Assert.Equal(
            ["hear.tengo-diez-panes", "hear.cuantos-panes", "hear.cuesta-dos-monedas", "hear.que-compraste", "say.quiero-dos-panes", "say.cuanto-cuesta", "say.voy-a-buscar-madera", "say.compre-dos-panes"],
            bank.Where(static i => i.Lesson == "numeros-y-pan").Select(static i => i.Id));
        Assert.Equal(
            ["hear.soy-el-herrero", "hear.quieres-un-hacha", "hear.que-va-a-hacer-domingo", "hear.ayer-trabajo-mucho", "say.como-te-llamas", "say.que-haces", "say.va-a-hacer-un-hacha", "say.hizo-un-hacha"],
            bank.Where(static i => i.Lesson == "gente-del-pueblo").Select(static i => i.Id));
        Assert.Equal(
            ["hear.mis-manzanas-son-rojas", "hear.te-gusta-el-pan", "hear.mis-plantas-necesitan-agua", "hear.el-pan-es-de-luz", "say.la-manzana-es-roja", "say.me-gustan-las-manzanas", "say.quiero-tres-manzanas-rojas", "say.me-gusto-el-pan"],
            bank.Where(static i => i.Lesson == "como-es").Select(static i => i.Id));
        Assert.Equal(
            ["hear.el-dia-de-ayer", "hear.comieron-en-la-plaza", "hear.adonde-fueron", "hear.le-vendi-pescado", "say.luz-hizo-pan", "say.fuimos-al-rio", "say.ellos-comieron-pan", "say.tengo-que-ir"],
            bank.Where(static i => i.Lesson == "ayer-en-el-pueblo").Select(static i => i.Id));
        Assert.Equal(
            ["hear.que-haras-manana", "hear.tendre-pan", "hear.mar-pescara", "hear.el-dia-de-manana", "say.manana-comprare-pan", "say.buscare-madera", "say.ire-al-rio", "say.luz-hara-pan"],
            bank.Where(static i => i.Lesson == "manana-en-el-pueblo").Select(static i => i.Id));

        // An item of a lesson checks a phrase or a line of that lesson's steps.
        var lessons = RouteScene.DataOf(TestGame.Bundle).Lessons;
        foreach (var item in bank.Where(static i => i.Lesson is not null))
        {
            var steps = lessons.Find(item.Lesson!)!.Steps;
            Assert.True(steps.Any(s => s.Phrase == item.Phrase || (item.Line.Length > 0 && s.Line?.LineId == item.Line)), $"{item.Id} checks nothing of {item.Lesson}");
        }
    }

    [Fact]
    public void AnItemMustSayItsLessonAndTheLessonMustExist()
    {
        Assert.Throws<InvalidDataException>(() => Parse(Battery(Production.Replace("\"lesson\":null,", string.Empty, StringComparison.Ordinal))));
        Assert.Equal("first-words", Parse(Battery(Production.Replace("\"lesson\":null", "\"lesson\":\"first-words\"", StringComparison.Ordinal)))[0].Lesson);
        Assert.Empty(Problems(Battery(Production.Replace("\"lesson\":null", "\"lesson\":\"first-words\"", StringComparison.Ordinal))));

        var problems = Problems(Battery(Production.Replace("\"lesson\":null", "\"lesson\":\"no-such-lesson\"", StringComparison.Ordinal)));

        Assert.Contains("game/checks.json: item 'say.x' belongs to the lesson 'no-such-lesson', which is not a lesson (first-words, personas, ayer-y-manana, el-pueblo, numeros-y-pan, gente-del-pueblo, como-es, ayer-en-el-pueblo, manana-en-el-pueblo).", problems);
    }

    [Fact]
    public void EveryComprehensionItemHasThreeOptionsAndAnAnswerAmongThem()
    {
        foreach (var item in Bank.Where(static i => i.Kind == CheckItemKind.Comprehension))
        {
            Assert.Equal(3, item.Options.Count);
            Assert.InRange(item.Correct, 0, 2);
            Assert.All(item.Options, static option => Assert.False(string.IsNullOrWhiteSpace(option.Label)));
        }
    }

    [Fact]
    public void EveryComprehensionLineIsAPrerenderedSpeechLine()
    {
        var lines = SpeechLines.Enumerate(TestGame.Bundle, TestGame.Nouns, Sheet, TestGame.Data.Npcs).Select(static l => l.LineId).ToHashSet(StringComparer.Ordinal);

        foreach (var item in Bank.Where(static i => i.Kind == CheckItemKind.Comprehension))
        {
            Assert.Contains(item.Line, lines);
        }
    }

    [Fact]
    public void EveryProductionItemIsGradedAtItsRowsPrompt()
    {
        var sheet = Sheet;

        foreach (var item in Bank.Where(static i => i.Kind == CheckItemKind.Production))
        {
            Assert.Equal(sheet.Find(item.Phrase)!.Prompt, item.Prompt);
            Assert.True(sheet.Find(item.Phrase)!.IsLearnerPhrase);
        }
    }

    [Fact]
    public void AnItemThatNamesNoPhraseOfTheSheetIsRefused()
    {
        var json = Battery(Comprehension.Replace("\"pedir-agua\"", "\"no-such-phrase\"", StringComparison.Ordinal));

        Assert.Contains(Problems(json), static p => p.Contains("no-such-phrase", StringComparison.Ordinal));
    }

    [Fact]
    public void AComprehensionAnswerOutsideItsOptionsIsRefused()
    {
        var json = Battery(Comprehension.Replace("\"correct\":0", "\"correct\":3", StringComparison.Ordinal));

        Assert.Throws<InvalidDataException>(() => Parse(json));
    }

    [Fact]
    public void AComprehensionItemNeedsThreeOptions()
    {
        var json = Battery(Comprehension.Replace(",{\"label\":\"c\"}", string.Empty, StringComparison.Ordinal));

        Assert.Throws<InvalidDataException>(() => Parse(json));
    }

    [Fact]
    public void AProductionItemIsGradedAtAPromptTheBundleHas()
    {
        var json = Battery(Production.Replace("\"stall-need\"", "\"no-such-prompt\"", StringComparison.Ordinal));

        Assert.Contains(Problems(json), static p => p.Contains("no-such-prompt", StringComparison.Ordinal));
    }

    [Fact]
    public void AProductionItemOnAPhraseTheLearnerDoesNotSayIsRefused()
    {
        var json = Battery(Production.Replace("\"pedir-agua\"", "\"que-necesitas\"", StringComparison.Ordinal));

        Assert.Contains(Problems(json), static p => p.Contains("a phrase the learner does not say", StringComparison.Ordinal));
    }

    [Fact]
    public void AComprehensionItemThatPlaysNoSpeechLineIsRefused()
    {
        var json = Battery(Comprehension.Replace("\"phrase.pedir-agua\"", "\"phrase.no-such-line\"", StringComparison.Ordinal));

        Assert.Contains(Problems(json), static p => p.Contains("phrase.no-such-line", StringComparison.Ordinal));
    }

    [Fact]
    public void AValidBatteryHasNoProblemsWithTheBundle()
    {
        Assert.Empty(Problems(Battery(Comprehension, Production)));
    }

    // The references of the items are checked against the other files by the validator (CONTENT_PLAN C02), as the game's load does.
    // The shipped battery's locale strings are orphans once a fixture replaces the battery, so those are left out.
    private static IReadOnlyList<string> Problems(string json) =>
        GameDataValidator.Validate(TestGame.Data.WithChecks(Parse(json))).Where(static p => !p.EndsWith("(an orphan).", StringComparison.Ordinal)).ToList();

    // The fixtures keep their text inline; it is split into locale strings as the shipped battery's is (C03).
    private static CheckBank Parse(string json)
    {
        var (file, strings) = LocaleFixture.SplitChecks(json);
        return CheckBank.Parse(file, TestGame.Locale.With(strings));
    }

    [Fact]
    public void TwoItemsWithOneIdAreRefused()
    {
        var json = Battery(Production, Production);

        Assert.Throws<InvalidDataException>(() => Parse(json));
    }

    [Fact]
    public void AnEmptyBatteryAndAnUnknownSchemaAreRefused()
    {
        Assert.Throws<InvalidDataException>(() => Parse("""{"schemaVersion":2,"items":[]}"""));
        Assert.Throws<InvalidDataException>(() => Parse(Battery(Production).Replace("\"schemaVersion\":2", "\"schemaVersion\":1", StringComparison.Ordinal))); // before V07's lessons
        Assert.Throws<InvalidDataException>(() => Parse(Battery(Production).Replace("\"schemaVersion\":2", "\"schemaVersion\":3", StringComparison.Ordinal)));
        Assert.Throws<InvalidDataException>(() => Parse(Battery(Production).Replace("\"schemaVersion\":2,", "\"schemaVersion\":2,\"coreSessions\":0,", StringComparison.Ordinal)));
        Assert.Throws<InvalidDataException>(() => CheckBank.Parse("not json", TestGame.Locale));
    }

    [Fact]
    public void AValidBatteryParses()
    {
        var items = Parse(Battery(Comprehension, Production));

        Assert.Equal(["hear.x", "say.x"], items.Select(static i => i.Id));
        Assert.Equal("Quiero agua, por favor.", items[0].Spanish);
        Assert.Equal("request_item", items[1].Intent);
    }
}
