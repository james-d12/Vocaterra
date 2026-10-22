namespace Vocaterra.Game.Tests;

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
          { "id": "get", "type": "do", "event": "item_picked_up", "detail": "fruta", "english": "Pick a fruit." },
          { "id": "sum", "type": "recap", "english": "Well done." } ] } ] }
        """;

    private static LessonBook Book(string json) => TestGame.Lessons(json);

    private static string Lesson(string steps, string head = "\"id\": \"t\", \"npc\": \"guide\", \"title\": \"T\", \"english\": \"E\"") =>
        "{ \"schemaVersion\": 1, \"lessons\": [ { " + head + ", \"steps\": [ " + steps + " ] } ] }";

    [Fact]
    public void TheShippedSampleIsATwoStepLessonAtTheGuideOnPhrasesAndLinesThatAlreadyExist()
    {
        var book = LessonBook.Load(TestGame.Bundle, PhraseSheet.Load(TestGame.Bundle), TestGame.Speech);

        var sample = Assert.Single(book.Lessons);
        Assert.Equal(("sample", "guide", 2), (sample.Id, sample.Npc, sample.Steps.Count));
        Assert.Equal([LessonStepType.Repeat, LessonStepType.Answer], sample.Steps.Select(static s => s.Type));
        Assert.Equal(["hola", "gracias"], sample.TaughtPhrases);
        Assert.Equal(["hola", "gracias"], book.TaughtPhrases);
        Assert.False(sample.AutoStart);
        var answer = sample.Steps[1];
        Assert.Equal("guide_offer_yes.1", answer.Line!.LineId);
        var folder = Path.Combine(GameBundle.ResolveDirectory(), SpeechLines.FolderName);
        var store = new Vocaterra.Nlp.Response.Speech.StaticLineStore(folder);
        Assert.True(store.Contains(answer.Line.LineId));
        Assert.True(store.Contains(SpeechFiles.SlowId(answer.Line.LineId)));
        Assert.True(store.Contains("phrase.hola")); // the repeat step's model is the phrase's own file
    }

    [Fact]
    public void EveryLineOfEveryStepOfTheShippedLessonsHasItsFilesAndTheirEnglish()
    {
        var book = LessonBook.Load(TestGame.Bundle, PhraseSheet.Load(TestGame.Bundle), TestGame.Speech);
        var store = new Vocaterra.Nlp.Response.Speech.StaticLineStore(Path.Combine(GameBundle.ResolveDirectory(), SpeechLines.FolderName));

        foreach (var step in book.Lessons.SelectMany(static l => l.Steps))
        {
            Assert.False(string.IsNullOrWhiteSpace(step.English), step.Id);
            if (step.Line is { } line)
            {
                Assert.True(store.Contains(line.LineId), $"{line.LineId}.wav");
                Assert.True(store.Contains(SpeechFiles.SlowId(line.LineId)), $"{line.LineId}.slow.wav");
                Assert.False(string.IsNullOrWhiteSpace(step.Translation), step.Id);
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
    [InlineData("{ \"id\": \"s\", \"type\": \"do\", \"event\": \"the_moon_landed\", \"english\": \"x\" }", "not a world event")]
    [InlineData("{ \"id\": \"s\", \"type\": \"explain\", \"english\": \"x\", \"attempts\": 9 }", "1 to 5")]
    [InlineData("{ \"id\": \"s\", \"type\": \"explain\" }", "english")]
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
    public void TheLessonsAndTheEnglishAreHashedHostFilesOfTheBundle()
    {
        var bundle = TestGame.Bundle;

        Assert.True(bundle.TryReadHostFile(LessonBook.RelativePath, out _));
        Assert.True(bundle.TryReadHostFile(EnglishText.RelativePath, out _));
        Assert.Contains(LessonBook.RelativePath, bundle.HostFilePaths);
        Assert.Contains(EnglishText.RelativePath, bundle.HostFilePaths);
        var provenance = File.ReadAllText(Path.Combine(GameBundle.ResolveDirectory(), "provenance.json"));
        Assert.Contains("\"path\": \"game/lessons.json\", \"generatedBy\": \"hand-authored\"", provenance, StringComparison.Ordinal);
        Assert.Contains("\"path\": \"game/english.json\", \"generatedBy\": \"hand-authored\"", provenance, StringComparison.Ordinal);
    }
}
