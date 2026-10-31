using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Tip;

namespace Vocaterra.Game.Core.Tests.Language.Lessons;

/// <summary>
/// The lessons file of S16 (<c>game/lessons.json</c>): the shipped sample loads and means what it says, the seven step types parse with their fields, and a
/// lesson that does not hold together (an unknown phrase, a prompt that cannot recognise the phrase, a line with no pre-rendered speech, an event the game
/// never raises...) is a load error, not a surprise in a playtest.
/// </summary>
public sealed class LessonBookTests
{
    /// <summary>A lesson with every step type, on phrases and lines that exist in the bundle.</summary>
    public const string Rich = """
        { "schemaVersion": 1, "lessons": [ { "id": "rich", "npc": "guide", "title": "Rich", "english": "Every step type.", "steps": [
          { "id": "read", "type": "explain", "english": "Read this." },
          { "id": "hear", "type": "model", "line": "phrase.hola", "translation": "Hello.", "english": "Listen." },
          { "id": "pick", "type": "listen", "line": "phrase.hola", "translation": "Hello.", "english": "What does she say?", "options": ["Hello", "Goodbye", "Thank you"], "correct": 0 },
          { "id": "say", "type": "repeat", "phrase": "hola", "prompt": "guide-open", "english": "Say it." },
          { "id": "reply", "type": "answer", "phrase": "gracias", "prompt": "guide-open", "line": "guide_offer_yes.1", "translation": "Of course! Here you are.", "english": "Thank her." },
          { "id": "get", "type": "do", "trigger": { "event": "item_picked_up", "detail": "fruta" }, "english": "Pick a fruit." },
          { "id": "sum", "type": "recap", "english": "Well done." } ] } ] }
        """;

    private static LessonBook Book(string json) => TestGame.Lessons(json);

    private static string Lesson(string steps, string head = "\"id\": \"t\", \"npc\": \"guide\", \"title\": \"T\", \"english\": \"E\"") =>
        "{ \"schemaVersion\": 1, \"lessons\": [ { " + head + ", \"steps\": [ " + steps + " ] } ] }";

    [Fact]
    public void TheShippedLessonsAreTheGuidesFirstWordsPersonasInSessionTwoAndAyerYMananaInSessionThree()
    {
        var book = RouteScene.DataOf(TestGame.Bundle).Lessons;

        Assert.Equal(["first-words", "personas", "ayer-y-manana"], book.Lessons.Select(static l => l.Id));
        var lesson = book.Find("first-words")!;
        Assert.Equal(("first-words", "guide", true, 1), (lesson.Id, lesson.Npc, lesson.AutoStart, lesson.Session)); // it starts by itself when the player first talks to her
        Assert.Null(book.Find("sample"));
        foreach (var unit in new[] { "u1", "u2", "u3", "u4", "u4v", "u5", "u6" })
        {
            Assert.Contains(lesson.Steps, s => s.Id.StartsWith(unit + "-", StringComparison.Ordinal));
        }

        Assert.Equal(LessonStepType.Recap, lesson.Steps[^1].Type); // the recap of the phrases learnt closes it
        Assert.Equal(
            ["hola", "si", "no", "que-es-esto", "si-por-favor", "gracias", "quiero-madera", "tengo-fruta", "necesito-agua", "no-se", "otra-vez", "mas-despacio"],
            lesson.TaughtPhrases);
        Assert.Equal(lesson.TaughtPhrases.Concat(book.Find("personas")!.TaughtPhrases).Concat(book.Find("ayer-y-manana")!.TaughtPhrases).Distinct(), book.TaughtPhrases); // quiero-madera is asked again at the start of Personas
        Assert.All(lesson.TaughtPhrases, id => Assert.True(RouteScene.DataOf(TestGame.Bundle).Phrases.Find(id)!.IsLearnerPhrase, id));
        foreach (var type in Enum.GetValues<LessonStepType>().Where(static t => t is not (LessonStepType.Recap or LessonStepType.Review)))
        {
            Assert.Contains(lesson.Steps, s => s.Type == type); // built from the runner's step types: explain, model, listen, repeat, answer and do (a review needs an earlier lesson, X06)
        }

        var pick = Assert.Single(lesson.Steps, static s => s.Type == LessonStepType.Do);
        Assert.Equal(("item_picked_up", "fruta", "fruta"), (pick.Event, pick.Detail, pick.Point)); // the fruit Luna offers is picked up from a bush
        Assert.False(string.IsNullOrWhiteSpace(pick.Hud));
    }

    [Fact]
    public void TheFirstUseOfAPhraseHasItsCardAndTheEnglishThinsAfterIt()
    {
        var book = RouteScene.DataOf(TestGame.Bundle).Lessons;
        var lesson = book.Find("first-words")!;

        // The first time a phrase is asked it is a repeat step (the card is up) unless an explain step has named it in English just before (the card is not
        // needed for a word the learner has just read); every later use is an answer step (no card), with fewer words of English. The same holds in Personas (S26).
        var sheet = RouteScene.DataOf(TestGame.Bundle).Phrases;
        foreach (var each in book.Lessons)
        {
            foreach (var phrase in each.TaughtPhrases)
            {
                var uses = each.Steps.Where(s => s.WaitsOnTurn && s.Phrase == phrase).ToList();
                if (uses[0].Type != LessonStepType.Repeat)
                {
                    var spanish = sheet.Find(phrase)!.Spanish.TrimEnd('.').Split(',')[0];
                    // an explain step before names it in English, or the step itself quotes the words it asks for (S26: "say “Tengo una fruta”")
                    var named = each.Steps.Take(each.Steps.ToList().IndexOf(uses[0]) + 1).Where(static s => s.Type is LessonStepType.Explain or LessonStepType.Answer);
                    Assert.Contains(named, s => s.English.Contains("“" + spanish + "”", StringComparison.OrdinalIgnoreCase));
                }

                Assert.All(uses.Skip(1), static s => Assert.Equal(LessonStepType.Answer, s.Type));
            }
        }

        var asks = lesson.Steps.Where(static s => s.Phrase == "que-es-esto").ToList();
        Assert.Equal(3, asks.Count);
        Assert.True(asks[1].English.Length < asks[0].English.Length);
        Assert.True(asks[2].English.Length <= asks[1].English.Length);
    }

    [Fact]
    public void EveryLineOfEveryStepOfTheShippedLessonsHasItsFilesAndTheirEnglish()
    {
        var book = RouteScene.DataOf(TestGame.Bundle).Lessons;
        var store = new Vocaterra.Nlp.Response.Speech.StaticLineStore(Path.Combine(GameBundle.ResolveDirectory(), SpeechLines.FolderName));
        var english = RouteScene.DataOf(TestGame.Bundle).Locale;

        foreach (var step in book.Lessons.SelectMany(static l => l.Steps))
        {
            Assert.False(string.IsNullOrWhiteSpace(step.English), step.Id);
            if (step.Line is { } line)
            {
                if (!PendingSpeech.Contains(line.LineId))
                {
                    Assert.True(store.Contains(line.LineId), $"{line.LineId}.wav");
                    Assert.True(store.Contains(SpeechFiles.SlowId(line.LineId)), $"{line.LineId}.slow.wav");
                }

                Assert.False(string.IsNullOrWhiteSpace(step.Translation), step.Id);
                Assert.Equal(step.Translation, english.Line(line.LineId)); // the gloss under the line is the same in the locale's lines and in the step
            }
        }
    }

    [Fact]
    public void TheSevenStepTypesParseWithTheirFields()
    {
        var lesson = Assert.Single(Book(Rich).Lessons);

        Assert.Equal(
            [LessonStepType.Explain, LessonStepType.Model, LessonStepType.Listen, LessonStepType.Repeat, LessonStepType.Answer, LessonStepType.Do, LessonStepType.Recap],
            lesson.Steps.Select(static s => s.Type));
        Assert.Equal(["explain", "model", "listen", "repeat", "answer", "do", "recap"], lesson.Steps.Select(static s => LessonStepTypes.Of(s.Type)));
        var listen = lesson.Steps[2];
        Assert.Equal((3, 0, "phrase.hola", "Hello."), (listen.Options.Count, listen.Correct, listen.Line!.LineId, listen.Translation));
        Assert.Equal("guide", listen.Line.Speaker);
        var say = lesson.Steps[3];
        Assert.Equal(("hola", "guide-open", LessonStep.DefaultAttempts), (say.Phrase, say.Prompt, say.Attempts));
        Assert.Equal(("item_picked_up", "fruta"), (lesson.Steps[5].Event, lesson.Steps[5].Detail));
        Assert.True(say.WaitsOnTurn);
        Assert.False(lesson.Steps[5].WaitsOnTurn);
    }

    [Fact]
    public void ARepeatStepWithoutAPromptTakesTheOneOfThePhrasesRow()
    {
        var book = Book(Lesson("{ \"id\": \"s\", \"type\": \"repeat\", \"phrase\": \"pedir-agua\", \"english\": \"Ask.\", \"attempts\": 1 }"));

        var step = Assert.Single(book.Lessons[0].Steps);
        Assert.Equal(("stall-need", 1), (step.Prompt, step.Attempts));
    }

    [Theory]
    [InlineData("{ \"id\": \"s\", \"type\": \"dance\", \"english\": \"x\" }", "dance")]
    [InlineData("{ \"id\": \"s\", \"type\": \"repeat\", \"phrase\": \"no-such\", \"english\": \"x\" }", "not in the phrase sheet")]
    [InlineData("{ \"id\": \"s\", \"type\": \"repeat\", \"phrase\": \"que-necesitas\", \"english\": \"x\" }", "does not say")]
    [InlineData("{ \"id\": \"s\", \"type\": \"repeat\", \"phrase\": \"hola\", \"prompt\": \"stall-need\", \"english\": \"x\" }", "does not allow the intent")]
    [InlineData("{ \"id\": \"s\", \"type\": \"repeat\", \"phrase\": \"hola\", \"prompt\": \"nowhere\", \"english\": \"x\" }", "the bundle does not have")]
    [InlineData("{ \"id\": \"s\", \"type\": \"model\", \"line\": \"nowhere.1\", \"translation\": \"x\", \"english\": \"x\" }", "no pre-rendered speech line")]
    [InlineData("{ \"id\": \"s\", \"type\": \"model\", \"line\": \"phrase.hola\", \"english\": \"x\" }", "translation")]
    [InlineData("{ \"id\": \"s\", \"type\": \"listen\", \"line\": \"phrase.hola\", \"translation\": \"x\", \"english\": \"x\", \"options\": [\"a\", \"b\"], \"correct\": 2 }", "outside the options")]
    [InlineData("{ \"id\": \"s\", \"type\": \"listen\", \"line\": \"phrase.hola\", \"translation\": \"x\", \"english\": \"x\", \"options\": [\"a\"], \"correct\": 0 }", "two to four")]
    [InlineData("{ \"id\": \"s\", \"type\": \"do\", \"trigger\": { \"event\": \"the_moon_landed\" }, \"english\": \"x\" }", "not an event the game raises")]
    [InlineData("{ \"id\": \"s\", \"type\": \"do\", \"english\": \"x\" }", "no trigger")]
    [InlineData("{ \"id\": \"s\", \"type\": \"do\", \"trigger\": { \"event\": \"talk_opened\", \"npc\": \"miller\" }, \"english\": \"x\" }", "is not one of the NPCs")]
    [InlineData("{ \"id\": \"s\", \"type\": \"explain\", \"english\": \"x\", \"attempts\": 9 }", "1 to 5")]
    [InlineData("{ \"id\": \"s\", \"type\": \"explain\" }", "lesson.t.s.text")]
    [InlineData("{ \"id\": \"s\", \"type\": \"recap\", \"phrases\": [\"adios\"], \"english\": \"x\" }", "no repeat or answer step")]
    public void ALessonThatDoesNotHoldTogetherIsALoadError(string step, string problem)
    {
        var ex = Assert.Throws<InvalidDataException>(() => Book(Lesson(step)));

        Assert.Contains(problem, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DuplicateIdsAnUnknownNpcAnEmptyLessonAndAnUnknownAfterAreRefused()
    {
        const string Step = "{ \"id\": \"s\", \"type\": \"explain\", \"english\": \"x\" }";

        Assert.Contains("twice", Assert.Throws<InvalidDataException>(() => Book(Lesson(Step + "," + Step))).Message, StringComparison.Ordinal);
        Assert.Contains("not one of the NPCs", Assert.Throws<InvalidDataException>(() => Book(Lesson(Step, "\"id\": \"t\", \"npc\": \"dragon\", \"title\": \"T\", \"english\": \"E\""))).Message, StringComparison.Ordinal);
        Assert.Contains("no steps", Assert.Throws<InvalidDataException>(() => Book(Lesson(string.Empty))).Message, StringComparison.Ordinal);
        Assert.Contains("earlier lesson", Assert.Throws<InvalidDataException>(() => Book(Lesson(Step, "\"id\": \"t\", \"npc\": \"guide\", \"title\": \"T\", \"english\": \"E\", \"after\": \"nope\""))).Message, StringComparison.Ordinal);
        var two = "{ \"schemaVersion\": 1, \"lessons\": [ { \"id\": \"a\", \"npc\": \"guide\", \"title\": \"T\", \"english\": \"E\", \"steps\": [ " + Step + " ] }, { \"id\": \"a\", \"npc\": \"guide\", \"title\": \"T\", \"english\": \"E\", \"steps\": [ " + Step + " ] } ] }";
        Assert.Contains("used twice", Assert.Throws<InvalidDataException>(() => Book(two)).Message, StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => Book("{ nope"));
        Assert.Throws<InvalidDataException>(() => Book("{ \"schemaVersion\": 2, \"lessons\": [] }"));
    }

    [Fact]
    public void TheLessonsAreAHashedHostFileOfTheBundleAndTheirTextIsTheLocalesOutsideIt()
    {
        var bundle = TestGame.Bundle;

        Assert.True(bundle.TryReadHostFile(LessonBook.RelativePath, out _));
        Assert.Contains(LessonBook.RelativePath, bundle.HostFilePaths);
        Assert.DoesNotContain("game/english.json", bundle.HostFilePaths); // the support language left the bundle (C03)
        var provenance = File.ReadAllText(Path.Combine(GameBundle.ResolveDirectory(), "provenance.json"));
        Assert.Contains("\"path\": \"game/lessons.json\", \"generatedBy\": \"hand-authored\"", provenance, StringComparison.Ordinal);
        Assert.DoesNotContain("english", File.ReadAllText(Repo.Path("content", "game-v1", "game", "lessons.json")).Split('"').Where(static token => token is "english" or "title" or "translation" or "help" or "retry"));
        Assert.Equal("First words with Luna", TestGame.Locale.Text(LessonBook.LessonId("first-words", "title")));
    }
}
