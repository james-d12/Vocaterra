using Vocaterra.Game.Language.Lessons;
using Vocaterra.Game.Language.Phrases;
using Vocaterra.Game.Language.Speech;
using Vocaterra.Game.Language.Words;
using Vocaterra.Game.Npc;
using Vocaterra.Game.Session;
using Vocaterra.Game.Voxel;
using Vocaterra.Game.World;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Tests;

/// <summary>Shared set-up: the game bundle loaded once, and sessions on the bundle's route for a fixed seed and a fixed stored time.</summary>
internal static class TestGame
{
    /// <summary>The seed every guide test uses unless it says otherwise.</summary>
    public const ulong Seed = 42;

    private static readonly Lazy<ContentBundle> LoadedBundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> LoadedNouns = new(() => NounCatalog.Load(Bundle));

    /// <summary>The bundle, loaded and validated by the core's loader.</summary>
    public static ContentBundle Bundle => LoadedBundle.Value;

    /// <summary>The noun catalogue of the bundle.</summary>
    public static NounCatalog Nouns => LoadedNouns.Value;

    /// <summary>The bundle's route scene built from a seed.</summary>
    public static Scene Route(ulong seed = Seed) =>
        SceneBuilder.Build(SceneLayout.Load(Path.Combine(GameBundle.ResolveDirectory(), "game", "scene.json")), seed);

    /// <summary>
    /// A new session on the route for a seed, with the fixed start time. The guide has no lesson unless the test asks for one (S17: the shipped lesson starts by itself
    /// and takes the guide's conversation, which the tests of her ordinary conversation do not want); use <see cref="NewSessionWithShippedLessons"/> for the real one.
    /// </summary>
    public static VoxelSession NewSession(ulong seed = Seed, CreditRules? credit = null, LessonBook? lessons = null) =>
        new(Bundle, seed, GuideSession.DefaultStartedAt, Nouns, Route(seed), credit ?? CreditRules.FullTyped, lessons ?? NoLessons);

    /// <summary>A new session on the route with the lessons of the bundle (the guide's <c>first-words</c> lesson).</summary>
    public static VoxelSession NewSessionWithShippedLessons(ulong seed = Seed, CreditRules? credit = null) =>
        new(Bundle, seed, GuideSession.DefaultStartedAt, Nouns, Route(seed), credit ?? CreditRules.FullTyped, LessonBook.Load(Bundle, PhraseSheet.Load(Bundle), Speech));

    /// <summary>A book without lessons.</summary>
    public static LessonBook NoLessons => Lessons("{ \"schemaVersion\": 1, \"lessons\": [] }");

    /// <summary>The two-step sample lesson of S16 (hola, gracias), which S17 retired from the bundle and the runner's tests keep: the same text, the same ids.</summary>
    public const string SampleLesson = """
        { "schemaVersion": 1, "lessons": [ { "id": "sample", "npc": "guide", "title": "A first hello", "english": "Say hello to the guide, then say thank you. It takes a minute.", "autoStart": false, "steps": [
          { "id": "say-hola", "type": "repeat", "phrase": "hola", "prompt": "guide-open", "english": "Say hello to the guide. “Hola” is how you greet anyone.", "help": "Say: Hola (OH-lah). It means hello. The letter h is silent in Spanish.", "retry": "Not quite. Say: Hola. It means hello." },
          { "id": "say-gracias", "type": "answer", "phrase": "gracias", "prompt": "guide-open", "line": "guide_offer_yes.1", "translation": "Of course! Here you are.", "english": "The guide hands you something. Thank her.", "help": "Say: Gracias (GRAH-see-ahs). It means thank you.", "retry": "Not quite. Say: Gracias. It means thank you." }
        ] } ] }
        """;

    /// <summary>The bundle's speakable lines (the lessons' lines are checked against them).</summary>
    public static IReadOnlyList<SpeechLine> Speech => SpeechLines.Enumerate(Bundle, Nouns, PhraseSheet.Load(Bundle));

    /// <summary>Lessons from a file's text, checked against the bundle as the game checks its own.</summary>
    public static LessonBook Lessons(string json) => LessonBook.Parse(json, PhraseSheet.Load(Bundle), Speech, Bundle.Prompts);

    /// <summary>The live facts of a session's world (the blocks as they are now).</summary>
    public static WorldFacts3D FactsOf(VoxelSession session) => WorldFacts3D.Live(session.Scene.Grid, Nouns);

    /// <summary>Where the player stands as the guide's facts need it (the block under the body, the exact centre and the facing).</summary>
    public static GuideViewpoint ViewpointOf(VoxelSession session)
    {
        var body = session.Simulation.Player;
        return new GuideViewpoint(body.FeetBlock, body.CenterX, body.CenterZ, body.Yaw);
    }

    /// <summary>The nearest block of a catalogue noun from the player, as the guide finds it (the cell).</summary>
    public static GuideCell NearestOf(VoxelSession session, string lemma) =>
        FactsOf(session).NearestOfNoun(ViewpointOf(session), lemma)!.Cell;

    /// <summary>
    /// The nearest block of one of the kinds with the guide's rule written out again (over every block of the kinds, not the facts' chunk search): the smallest
    /// squared horizontal distance from the player's feet block, then the smaller z, then the smaller x, then the smaller y.
    /// </summary>
    public static BlockPos NearestBlock(VoxelSession session, params BlockId[] kinds)
    {
        var grid = session.Scene.Grid;
        var from = session.Simulation.Player.FeetBlock;
        BlockPos? best = null;
        long bestDistance = long.MaxValue;
        foreach (var kind in kinds)
        {
            foreach (var p in grid.BlocksOfKind(kind))
            {
                var distance = ((long)(p.X - from.X) * (p.X - from.X)) + ((long)(p.Z - from.Z) * (p.Z - from.Z));
                if (distance < bestDistance || (distance == bestDistance && (p.Z, p.X, p.Y).CompareTo((best!.Value.Z, best.Value.X, best.Value.Y)) < 0))
                {
                    bestDistance = distance;
                    best = p;
                }
            }
        }

        return best ?? throw new InvalidOperationException("No such block in the scene.");
    }
}
