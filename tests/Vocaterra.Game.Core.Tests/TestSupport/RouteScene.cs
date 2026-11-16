using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Generation;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>
/// The bundle's route as a test session: the scene built from a seed, and a voxel session over it at the fixed start time. Each scene is built once per
/// layout and seed in a test process and every caller gets a clone of it (<see cref="Scene.Clone"/>), because a run edits its grid in place and a build of
/// the route takes about 0.6 s alone and up to 2.5 s beside the other tests. A test of the builder itself builds with <see cref="SceneBuilder"/>.
/// </summary>
internal static class RouteScene
{
    private const string EmptyBook = "{ \"schemaVersion\": 1, \"lessons\": [] }";

    private static readonly Lazy<SceneLayout> RouteLayout = new(() => SceneLayout.Load(System.IO.Path.Combine(GameBundle.ResolveDirectory(), "game", "scene.json")));

    private static readonly Lazy<SceneLayout> ClearingLayout = new(() => SceneLayout.Load(Repo.Path("tests", "Vocaterra.Game.Tests", "Fixtures", "clearing-route.json")));

    // The built scenes, never handed out: only their clones are. The default seed's scene of each layout stays for the whole run; any other seed's scene is
    // kept only while it is among the last few asked for (T10: a scene is about 110 MB, and the ten seeds the tests ask for held 1.1 GB of the 3 GB heap for the rest of the run).
    private const ulong KeptSeed = 42;
    private const int RecentKept = 2;
    private static readonly ConcurrentDictionary<(Lazy<SceneLayout> Layout, ulong Seed), Lazy<Scene>> Built = new();
    private static readonly List<((Lazy<SceneLayout> Layout, ulong Seed) Key, Lazy<Scene> Scene)> Recent = [];

    // The book without lessons of each bundle (it is read-only, so the sessions share it).
    private static readonly ConditionalWeakTable<ContentBundle, LessonBook> EmptyBooks = new();

    private static readonly ConditionalWeakTable<ContentBundle, GameData> Loaded = new();
    private static readonly ConditionalWeakTable<ContentBundle, GameData> LoadedWithBread = new();

    private static readonly Lazy<LocaleText> LoadedLocale =
        new(() => LocaleBundle.Load(LocaleBundle.DirectoryBeside(GameBundle.ResolveDirectory())).Text(LocaleBundle.DefaultCode));

    /// <summary>The layout of the bundle's route, <c>content/game-v1/game/scene.json</c>, parsed once.</summary>
    public static SceneLayout Layout => RouteLayout.Value;

    /// <summary>
    /// The small flat test world with the first prototype route's layout (<c>Fixtures/clearing-route.json</c>): the front-end tests that name blocks by
    /// coordinate use it, so they keep their coordinates when the bundle's world is the large valley (S13).
    /// </summary>
    public static Scene Clearing(ulong seed = 42) => CloneOf(ClearingLayout, seed);

    /// <summary>The scene of the bundle's route (the large valley) for a seed.</summary>
    public static Scene Build(ulong seed = 42) => CloneOf(RouteLayout, seed);

    /// <summary>
    /// A new session on the route at the fixed start time, <i>without</i> the guide's lesson (S17: the shipped lesson starts by itself and takes the guide's
    /// conversation, which the tests of her ordinary conversation, of the stall and of the checks do not want); <paramref name="lessons"/> gives it lessons of its own.
    /// </summary>
    public static VoxelSession NewSession(ContentBundle bundle, NounCatalog nouns, CreditRules? credit = null, ulong seed = 42, LessonBook? lessons = null) =>
        new(DataOf(bundle), seed, GuideSession.DefaultStartedAt, Build(seed), credit ?? CreditRules.Default, lessons ?? NoLessons(bundle, nouns));

    /// <summary>A new session on the route with the lessons of the bundle (the guide's <c>first-words</c>).</summary>
    public static VoxelSession NewSessionWithShippedLessons(ContentBundle bundle, NounCatalog nouns, CreditRules? credit = null, ulong seed = 42) =>
        new(DataOf(bundle), seed, GuideSession.DefaultStartedAt, Build(seed), credit ?? CreditRules.Default);

    /// <summary>The game's content of the repository's bundle, loaded once (a test with no bundle of its own reads the shipped words and strings here).</summary>
    public static GameData Shipped => TestGame.Data;

    /// <summary>The game's content of a loaded bundle, loaded once per bundle and shared, as the game shares it between its sessions.</summary>
    public static GameData DataOf(ContentBundle bundle)
    {
        lock (Loaded)
        {
            if (!Loaded.TryGetValue(bundle, out var data))
            {
                // The tests that predate the bread (W10) see the shipped game without its procedure, so Luz makes no offer to bake and her talk opens as it did; BreadDataOf has it.
                data = BreadDataOf(bundle).WithProcedures(ProcedureBook.Empty);
                Loaded.Add(bundle, data);
            }

            return data;
        }
    }

    /// <summary>The game's content of a loaded bundle with the bread procedure, its station and its actions (W10), loaded once per bundle.</summary>
    public static GameData BreadDataOf(ContentBundle bundle)
    {
        lock (LoadedWithBread)
        {
            if (!LoadedWithBread.TryGetValue(bundle, out var data))
            {
                data = GameData.Load(bundle, LocaleOf(bundle));
                LoadedWithBread.Add(bundle, data);
            }

            return data;
        }
    }

    /// <summary>A book without lessons.</summary>
    public static LessonBook NoLessons(ContentBundle bundle, NounCatalog nouns) => EmptyBooks.GetValue(bundle, b => Lessons(EmptyBook, b));

    /// <summary>
    /// Lessons from a fixture with its text inline (<c>"english"</c>, <c>"title"</c>, ...), checked against the bundle as the game checks its own: the text
    /// becomes locale strings over the bundle's locale (<see cref="LocaleFixture"/>).
    /// </summary>
    public static LessonBook Lessons(string json, ContentBundle bundle)
    {
        var data = DataOf(bundle);
        if (!LocaleFixture.TrySplitLessons(json, out var file, out var strings))
        {
            return LessonBook.Parse(json, data.Phrases, data.Speech, bundle.Prompts, data.Locale, data.Npcs.Ids); // malformed: the loader names the problem
        }

        return LessonBook.Parse(file, data.Phrases, data.Speech, bundle.Prompts, data.Locale.With(strings), data.Npcs.Ids);
    }

    /// <summary>The support language of the repository's locale bundle (<c>content/locale</c>, its default locale), loaded once.</summary>
    public static LocaleText LocaleOf(ContentBundle bundle) => LoadedLocale.Value;

    /// <summary>The two-step sample lesson of S16 (hola, gracias), retired from the bundle in S17 and kept as the plain lesson the front-end tests run.</summary>
    public const string SampleLesson = """
        { "schemaVersion": 1, "lessons": [ { "id": "sample", "npc": "guide", "level": "a1", "title": "A first hello", "english": "Say hello to the guide, then say thank you. It takes a minute.", "autoStart": false, "steps": [
          { "id": "say-hola", "type": "repeat", "phrase": "hola", "prompt": "guide-open", "english": "Say hello to the guide. “Hola” is how you greet anyone.", "help": "Say: Hola (OH-lah). It means hello. The letter h is silent in Spanish.", "retry": "Not quite. Say: Hola. It means hello." },
          { "id": "say-gracias", "type": "answer", "phrase": "gracias", "prompt": "guide-open", "line": "guide_offer_yes.1", "translation": "Of course! Here you are.", "english": "The guide hands you something. Thank her.", "help": "Say: Gracias (GRAH-see-ahs). It means thank you.", "retry": "Not quite. Say: Gracias. It means thank you." }
        ] } ] }
        """;

    /// <summary>The sample lesson as a book.</summary>
    public static LessonBook Sample(ContentBundle bundle, NounCatalog nouns) => Lessons(SampleLesson, bundle);

    /// <summary>How many scenes of other seeds than the default the cache holds now (at most the last few).</summary>
    internal static int CachedOtherSeeds
    {
        get
        {
            lock (Recent)
            {
                return Recent.Count;
            }
        }
    }

    private static Scene CloneOf(Lazy<SceneLayout> layout, ulong seed)
    {
        var key = (layout, seed);
        static Lazy<Scene> Make((Lazy<SceneLayout> Layout, ulong Seed) key) => new(() => Warmed(key.Layout == RouteLayout ? ValleyBuild.Build(key.Layout.Value, key.Seed) : SceneBuilder.Build(key.Layout.Value, key.Seed)));
        if (seed == KeptSeed)
        {
            return Built.GetOrAdd(key, Make).Value.Clone();
        }

        Lazy<Scene> entry;
        lock (Recent)
        {
            var at = Recent.FindIndex(r => r.Key == key);
            if (at >= 0)
            {
                entry = Recent[at].Scene;
                Recent.RemoveAt(at);
            }
            else
            {
                entry = Make(key);
            }

            Recent.Add((key, entry));
            if (Recent.Count > RecentKept)
            {
                Recent.RemoveAt(0);
            }
        }

        return entry.Value.Clone();
    }

    // The world digest, the chunk digests and the chunks' kind masks are worked out once, on the built scene: a clone copies them, so a test that hashes or
    // searches the world does not redo it over the 4 608 chunks (an edit drops the edited chunk's own, as in any grid).
    private static Scene Warmed(Scene scene)
    {
        scene.Grid.ComputeDigest();
        foreach (var chunk in scene.Grid.Chunks)
        {
            _ = chunk.KindMask;
        }

        return scene;
    }
}
