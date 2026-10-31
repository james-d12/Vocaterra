using System.Text.Json.Nodes;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Language.Lessons;

/// <summary>
/// The lessons' levels (VILLAGE_PLAN 3.9, V08, D-game-22 (b)): every lesson declares <c>a1</c> or <c>a2</c>, and the game's validator holds the Spanish it teaches and
/// says to the bundle's CEFR lists up to that level, its phrases' variants to the bundle's ceiling (A2), and its tenses to the level's (no simple future at A1). The
/// fixtures' words are the word cards of two catalogue nouns, placed by the bundle's own lists: <c>hierba</c> (grass) is A2, <c>arena</c> (sand) is in neither list.
/// </summary>
public sealed class LessonLevelTests
{
    private const string Explain = """{ "id": "read", "type": "explain", "english": "Read this." }""";

    private const string RepeatAgain = """{ "id": "say", "type": "repeat", "phrase": "otra-vez", "english": "Say it." }""";

    private static string Lesson(string level, string steps, string verbs = "") =>
        $$"""{ "schemaVersion": 1, "lessons": [ { "id": "t", "npc": "guide", "level": "{{level}}", "title": "T", "english": "E", {{verbs}} "steps": [ {{steps}} ] } ] }""";

    private static string Model(string line) => $$"""{ "id": "hear", "type": "model", "line": "{{line}}", "translation": "Listen.", "english": "Listen." }""";

    // The validator's problems with the fixture lesson in place of the shipped ones (the shipped lessons' own problems, such as their locale strings left
    // without a lesson, are not the test's).
    private static List<string> Problems(string json, PhraseSheet? phrases = null) =>
        GameDataValidator.Validate((phrases is null ? TestGame.Data : TestGame.Data.WithPhrases(phrases)).WithLessons(TestGame.Lessons(json)))
            .Where(static p => p.StartsWith("game/lessons/t.json:", StringComparison.Ordinal))
            .ToList();

    // The fixture's premise, where the bundle's own lists put the word: a change of a list fails here, not in the test's assertions.
    private static void Premise(string lemma, bool a1, bool a2)
    {
        Assert.True(a1 == TestGame.Bundle.Cefr["a1"].Contains(lemma), $"'{lemma}' is {(a1 ? "not " : string.Empty)}in cefr/a1.txt");
        Assert.True(a2 == TestGame.Bundle.Cefr["a2"].Contains(lemma), $"'{lemma}' is {(a2 ? "not " : string.Empty)}in cefr/a2.txt");
    }

    [Fact]
    public void ALessonDeclaresItsLevel()
    {
        Assert.Equal(LessonLevels.A2, TestGame.Lessons(Lesson("a2", Explain)).Find("t")!.Level);

        var none = Lesson("a1", Explain).Replace("\"level\": \"a1\", ", string.Empty, StringComparison.Ordinal);
        Assert.Contains("declares no level", Assert.Throws<InvalidDataException>(() => TestGame.Lessons(none)).Message, StringComparison.Ordinal);
        Assert.Contains("declares the level 'b1'", Assert.Throws<InvalidDataException>(() => TestGame.Lessons(Lesson("b1", Explain))).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheShippedLessonsKeepToTheirLevelsAndPhaseBsFirstIsA2()
    {
        // Sessions 1 to 6 (phase A included) are A1, and session 7's ¿Cómo es? (V13) is A2, the first of phase B; GameDataTests holds the whole content to no problem, this
        // names the level's.
        Assert.All(TestGame.Data.Lessons.Lessons.Where(static l => l.Session <= 6), static lesson => Assert.Equal(LessonLevels.A1, lesson.Level));
        Assert.Equal(LessonLevels.A2, TestGame.Data.Lessons.Find("como-es")!.Level);
        Assert.DoesNotContain(GameDataValidator.Validate(TestGame.Data), static p => p.StartsWith(LessonBook.Folder, StringComparison.Ordinal));
    }

    [Fact]
    public void AnA1LessonWithAnA2WordFailsAndTheSameLessonAtA2Passes()
    {
        Premise("hierba", a1: false, a2: true);

        Assert.Equal(
            ["game/lessons/t.json: the a1 lesson says “la hierba” (the line 'card.hierba'), and 'hierba' is above A1 (not in cefr/a1.txt)."],
            Problems(Lesson("a1", Model("card.hierba"))));
        Assert.Empty(Problems(Lesson("a2", Model("card.hierba"))));
    }

    [Theory]
    [InlineData("a1", "A1", "not in cefr/a1.txt")]
    [InlineData("a2", "A2", "in none of cefr/a1.txt, cefr/a2.txt")]
    public void AWordAboveA2FailsAtEitherLevel(string level, string upper, string lists)
    {
        Premise("arena", a1: false, a2: false);

        Assert.Equal(
            [$"game/lessons/t.json: the {level} lesson says “la arena” (the line 'card.arena'), and 'arena' is above {upper} ({lists})."],
            Problems(Lesson(level, Model("card.arena"))));
    }

    [Fact]
    public void APhrasesVariantsAreHeldToTheCeilingWhateverTheLessonsLevel()
    {
        // otra-vez is taught at A1 (first-words) and accepts "repite" and "puedes repetir", whose repetir is A2 in the bundle's lists: the learner may say
        // them, the lesson does not teach them, so they are held to the bundle's ceiling as the core's lint holds an answer set's aliases to none.
        Premise("repetir", a1: false, a2: true);
        Assert.Contains("repite", TestGame.Phrases.Find("otra-vez")!.Variants);
        Assert.Empty(Problems(Lesson("a1", RepeatAgain)));

        // A variant above the ceiling fails at either level.
        Premise("arena", a1: false, a2: false);
        var sheet = SheetWithVariant("otra-vez", "otra vez la arena");
        foreach (var level in LessonLevels.All)
        {
            Assert.Equal(
                [$"game/lessons/t.json: the {level} lesson says “otra vez la arena” (a variant of the phrase 'otra-vez'), and 'arena' is above A2 (in none of cefr/a1.txt, cefr/a2.txt)."],
                Problems(Lesson(level, RepeatAgain), sheet));
        }
    }

    [Fact]
    public void TheSimpleFutureNeedsAnA2Lesson()
    {
        // The lists hold lemmas, so comeré and voy a comer are both comer to them: the tense is the level's too.
        const string Verbs = """ "verbs": [ { "lemma": "comer", "person": "1", "number": "sg", "tense": "ir-a" }, { "lemma": "comer", "person": "1", "number": "sg", "tense": "fut" } ], """;

        Assert.Equal(
            ["game/lessons/t.json: the a1 lesson declares the verb form comer 1 sg fut; an a1 lesson teaches pres, pret, ir-a."],
            Problems(Lesson("a1", Explain, Verbs)));
        Assert.Empty(Problems(Lesson("a2", Explain, Verbs)));
        Assert.Equal(["pres", "pret", TaughtVerbForm.NearFuture], LessonLevels.TensesAt(LessonLevels.A1));
        Assert.Equal(["pres", "pret", TaughtVerbForm.NearFuture, "fut"], LessonLevels.TensesAt(LessonLevels.A2));
    }

    // The shipped sheet with one more variant on a row.
    private static PhraseSheet SheetWithVariant(string phrase, string variant)
    {
        var root = JsonNode.Parse(GameData.HostText(TestGame.Bundle, PhraseSheet.RelativePath))!;
        root["phrases"]!.AsArray().Single(row => row!["id"]!.GetValue<string>() == phrase)!["variants"]!.AsArray().Add(variant);
        return PhraseSheet.Parse(root.ToJsonString(), TestGame.Locale);
    }
}
