using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Tests;

/// <summary>The bundle's route as a test session: the scene built from a seed, and a voxel session over it at the fixed start time.</summary>
internal static class RouteScene
{
    /// <summary>
    /// The small flat test world with the first prototype route's layout (<c>Fixtures/clearing-route.json</c>): the front-end tests that name blocks by
    /// coordinate use it, so they keep their coordinates when the bundle's world is the large valley (S13).
    /// </summary>
    public static Scene Clearing(ulong seed = 42) => SceneBuilder.Build(SceneLayout.Load(Repo.Path("tests", "Vocaterra.Game.Tests", "Fixtures", "clearing-route.json")), seed);

    /// <summary>The scene of the bundle's route (the large valley) for a seed.</summary>
    public static Scene Build(ulong seed = 42) =>
        SceneBuilder.Build(SceneLayout.Load(System.IO.Path.Combine(GameBundle.ResolveDirectory(), "game", "scene.json")), seed);

    /// <summary>
    /// A new session on the route at the fixed start time, <i>without</i> the guide's lesson (S17: the shipped lesson starts by itself and takes the guide's
    /// conversation, which the tests of her ordinary conversation, of the stall and of the checks do not want); <paramref name="lessons"/> gives it lessons of its own.
    /// </summary>
    public static VoxelSession NewSession(ContentBundle bundle, NounCatalog nouns, CreditRules? credit = null, ulong seed = 42, LessonBook? lessons = null) =>
        new(bundle, seed, GuideSession.DefaultStartedAt, nouns, Build(seed), credit ?? CreditRules.Default, lessons ?? NoLessons(bundle, nouns));

    /// <summary>A new session on the route with the lessons of the bundle (the guide's <c>first-words</c>).</summary>
    public static VoxelSession NewSessionWithShippedLessons(ContentBundle bundle, NounCatalog nouns, CreditRules? credit = null, ulong seed = 42) =>
        new(bundle, seed, GuideSession.DefaultStartedAt, nouns, Build(seed), credit ?? CreditRules.Default);

    /// <summary>A book without lessons.</summary>
    public static LessonBook NoLessons(ContentBundle bundle, NounCatalog nouns) =>
        LessonBook.Parse("{ \"schemaVersion\": 1, \"lessons\": [] }", PhraseSheet.Load(bundle), SpeechLines.Enumerate(bundle, nouns, PhraseSheet.Load(bundle)), bundle.Prompts);

    /// <summary>The two-step sample lesson of S16 (hola, gracias), retired from the bundle in S17 and kept as the plain lesson the front-end tests run.</summary>
    public const string SampleLesson = """
        { "schemaVersion": 1, "lessons": [ { "id": "sample", "npc": "guide", "title": "A first hello", "english": "Say hello to the guide, then say thank you. It takes a minute.", "autoStart": false, "steps": [
          { "id": "say-hola", "type": "repeat", "phrase": "hola", "prompt": "guide-open", "english": "Say hello to the guide. “Hola” is how you greet anyone.", "help": "Say: Hola (OH-lah). It means hello. The letter h is silent in Spanish.", "retry": "Not quite. Say: Hola. It means hello." },
          { "id": "say-gracias", "type": "answer", "phrase": "gracias", "prompt": "guide-open", "line": "guide_offer_yes.1", "translation": "Of course! Here you are.", "english": "The guide hands you something. Thank her.", "help": "Say: Gracias (GRAH-see-ahs). It means thank you.", "retry": "Not quite. Say: Gracias. It means thank you." }
        ] } ] }
        """;

    /// <summary>The sample lesson as a book.</summary>
    public static LessonBook Sample(ContentBundle bundle, NounCatalog nouns) =>
        LessonBook.Parse(SampleLesson, PhraseSheet.Load(bundle), SpeechLines.Enumerate(bundle, nouns, PhraseSheet.Load(bundle)), bundle.Prompts);
}
