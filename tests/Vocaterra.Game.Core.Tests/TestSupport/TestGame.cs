using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>Shared set-up: the game bundle loaded once, and sessions on the bundle's route for a fixed seed and a fixed stored time.</summary>
internal static class TestGame
{
    /// <summary>The seed every guide test uses unless it says otherwise.</summary>
    public const ulong Seed = 42;

    private static readonly Lazy<ContentBundle> LoadedBundle = new(() => GameBundle.Load());
    private static readonly Lazy<NounCatalog> LoadedNouns = new(() => Data.Nouns);

    /// <summary>The bundle, loaded and validated by the core's loader.</summary>
    public static ContentBundle Bundle => LoadedBundle.Value;

    /// <summary>The noun catalogue of the bundle.</summary>
    public static NounCatalog Nouns => LoadedNouns.Value;

    /// <summary>The game's content of the bundle (every game file, loaded and checked once).</summary>
    public static GameData Data => RouteScene.DataOf(Bundle);

    /// <summary>The shipped game with the bread procedure (W10); <see cref="Data"/> is the same without it, for the tests that predate it.</summary>
    public static GameData BreadData => RouteScene.BreadDataOf(Bundle);

    /// <summary>The phrase sheet of the bundle.</summary>
    public static PhraseSheet Phrases => Data.Phrases;

    /// <summary>The bundle's route scene for a seed: a copy of the one built in this test process (<see cref="RouteScene.Build"/>), for the test to edit.</summary>
    public static Scene Route(ulong seed = Seed) => RouteScene.Build(seed);

    /// <summary>
    /// A new session on the route for a seed, with the fixed start time. The guide has no lesson unless the test asks for one (S17: the shipped lesson starts by itself
    /// and takes the guide's conversation, which the tests of her ordinary conversation do not want); use <see cref="NewSessionWithShippedLessons"/> for the real one.
    /// </summary>
    public static VoxelSession NewSession(ulong seed = Seed, CreditRules? credit = null, LessonBook? lessons = null) =>
        new(Data, seed, GuideSession.DefaultStartedAt, Route(seed), credit ?? CreditRules.FullTyped, lessons ?? NoLessons);

    /// <summary>The camp checklist of the shipped session, with nothing done (<c>game/objectives/camp.json</c>, C07).</summary>
    public static Objective Camp() => new(Data.Objectives.Find("camp")!);

    /// <summary>A new session on the route with the lessons of the bundle (the guide's <c>first-words</c> lesson).</summary>
    public static VoxelSession NewSessionWithShippedLessons(ulong seed = Seed, CreditRules? credit = null) =>
        new(Data, seed, GuideSession.DefaultStartedAt, Route(seed), credit ?? CreditRules.FullTyped);

    /// <summary>A book without lessons.</summary>
    public static LessonBook NoLessons => RouteScene.NoLessons(Bundle, Nouns);

    /// <summary>The two-step sample lesson of S16 (hola, gracias), which S17 retired from the bundle and the runner's tests keep: the same text, the same ids.</summary>
    public const string SampleLesson = """
        { "schemaVersion": 1, "lessons": [ { "id": "sample", "npc": "guide", "level": "a1", "title": "A first hello", "english": "Say hello to the guide, then say thank you. It takes a minute.", "autoStart": false, "steps": [
          { "id": "say-hola", "type": "repeat", "phrase": "hola", "prompt": "guide-open", "english": "Say hello to the guide. “Hola” is how you greet anyone.", "help": "Say: Hola (OH-lah). It means hello. The letter h is silent in Spanish.", "retry": "Not quite. Say: Hola. It means hello." },
          { "id": "say-gracias", "type": "answer", "phrase": "gracias", "prompt": "guide-open", "line": "guide_offer_yes.1", "translation": "Of course! Here you are.", "english": "The guide hands you something. Thank her.", "help": "Say: Gracias (GRAH-see-ahs). It means thank you.", "retry": "Not quite. Say: Gracias. It means thank you." }
        ] } ] }
        """;

    /// <summary>The bundle's speakable lines (the lessons' lines are checked against them).</summary>
    public static IReadOnlyList<SpeechLine> Speech => Data.Speech;

    /// <summary>Lessons from a file's text, checked against the bundle as the game checks its own.</summary>
    public static LessonBook Lessons(string json) => RouteScene.Lessons(json, Bundle);

    /// <summary>The support language of the repository's locale bundle (its default locale), with the names its placeholders take (C04).</summary>
    public static LocaleText Locale => Data.Locale; // with the names its {npc:…} and {key:…} take (C04)

    /// <summary>The live facts of a session's world (the blocks as they are now).</summary>
    public static WorldFacts FactsOf(VoxelSession session) => WorldFacts.Live(session.Scene.Grid, Nouns, TestGame.Data.Words.Directions);

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
