using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;

namespace Vocaterra.Game.Core.Tests;

/// <summary>The check battery's items (<c>game/checks.json</c>, S11): what is in the bundle, and that a bad item is refused when it is read.</summary>
public sealed class CheckBankTests
{
    private static IReadOnlyList<CheckItem> Bank => CheckBank.Load(TestGame.Bundle);

    private static PhraseSheet Sheet => PhraseSheet.Load(TestGame.Bundle);

    private static IReadOnlySet<string> Prompts => TestGame.Bundle.Prompts.Keys.ToHashSet(StringComparer.Ordinal);

    // One comprehension item and one production item, valid on their own; the tests break one thing at a time.
    private const string Comprehension =
        """{"id":"hear.x","kind":"comprehension","phrase":"pedir-agua","line":"phrase.pedir-agua","spanish":"Quiero agua, por favor.","question":"?","options":[{"label":"a"},{"label":"b"},{"label":"c"}],"correct":0}""";

    private const string Production =
        """{"id":"say.x","kind":"production","phrase":"pedir-agua","situation":"You want water.","prompt":"stall-need","intent":"request_item"}""";

    private static string Battery(params string[] items) => """{"schemaVersion":1,"items":[""" + string.Join(",", items) + "]}";

    [Fact]
    public void TheBundleHoldsSixteenComprehensionItemsAndFifteenProductionItems()
    {
        var bank = Bank;

        Assert.Equal(31, bank.Count);
        Assert.Equal(16, bank.Count(static i => i.Kind == CheckItemKind.Comprehension));
        Assert.Equal(
            ["say.pedir-agua", "say.si-por-favor", "say.no-gracias", "say.donde-esta-el-rio", "say.si", "say.no", "say.que-es-esto", "say.otra-vez", "say.mas-despacio",
                "say.quiero-madera", "say.tengo-fruta", "say.ella-quiere-fruta", "say.necesitamos-agua", "say.voy-a-comer-pan", "say.ayer-comi-fruta"],
            bank.Where(static i => i.Kind == CheckItemKind.Production).Select(static i => i.Id));
        Assert.Equal(
            ["request_item", "confirm", "deny", "ask_directions", "confirm", "deny", "ask_what", "ask_repeat", "ask_slower", "state_want", "state_have", "state_want", "state_want", "state_action", "state_action"],
            bank.Where(static i => i.Kind == CheckItemKind.Production).Select(static i => i.Intent));
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
        var lines = SpeechLines.Enumerate(TestGame.Bundle, TestGame.Nouns, Sheet).Select(static l => l.LineId).ToHashSet(StringComparer.Ordinal);

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

        var error = Assert.Throws<InvalidDataException>(() => CheckBank.Parse(json, Sheet, Prompts));
        Assert.Contains("no-such-phrase", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AComprehensionAnswerOutsideItsOptionsIsRefused()
    {
        var json = Battery(Comprehension.Replace("\"correct\":0", "\"correct\":3", StringComparison.Ordinal));

        Assert.Throws<InvalidDataException>(() => CheckBank.Parse(json, Sheet, Prompts));
    }

    [Fact]
    public void AComprehensionItemNeedsThreeOptions()
    {
        var json = Battery(Comprehension.Replace(",{\"label\":\"c\"}", string.Empty, StringComparison.Ordinal));

        Assert.Throws<InvalidDataException>(() => CheckBank.Parse(json, Sheet, Prompts));
    }

    [Fact]
    public void AProductionItemIsGradedAtAPromptTheBundleHas()
    {
        var json = Battery(Production.Replace("\"stall-need\"", "\"no-such-prompt\"", StringComparison.Ordinal));

        Assert.Throws<InvalidDataException>(() => CheckBank.Parse(json, Sheet, Prompts));
    }

    [Fact]
    public void AProductionItemOnAPhraseTheLearnerDoesNotSayIsRefused()
    {
        var json = Battery(Production.Replace("\"pedir-agua\"", "\"que-necesitas\"", StringComparison.Ordinal));

        Assert.Throws<InvalidDataException>(() => CheckBank.Parse(json, Sheet, Prompts));
    }

    [Fact]
    public void TwoItemsWithOneIdAreRefused()
    {
        var json = Battery(Production, Production);

        Assert.Throws<InvalidDataException>(() => CheckBank.Parse(json, Sheet, Prompts));
    }

    [Fact]
    public void AnEmptyBatteryAndAnUnknownSchemaAreRefused()
    {
        Assert.Throws<InvalidDataException>(() => CheckBank.Parse("""{"schemaVersion":1,"items":[]}""", Sheet, Prompts));
        Assert.Throws<InvalidDataException>(() => CheckBank.Parse("""{"schemaVersion":2,"items":[]}""", Sheet, Prompts));
        Assert.Throws<InvalidDataException>(() => CheckBank.Parse("not json", Sheet, Prompts));
    }

    [Fact]
    public void AValidBatteryParses()
    {
        var items = CheckBank.Parse(Battery(Comprehension, Production), Sheet, Prompts);

        Assert.Equal(["hear.x", "say.x"], items.Select(static i => i.Id));
        Assert.Equal("Quiero agua, por favor.", items[0].Spanish);
        Assert.Equal("request_item", items[1].Intent);
    }
}
