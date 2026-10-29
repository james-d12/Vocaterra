using System.Text;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Content;

/// <summary>
/// The game's content, loaded once from the bundle's verified host files and checked as a whole (CONTENT_PLAN 3.7, C02): the noun catalogue, the recipes,
/// the phrase sheet, the English text, the verbs' glosses, the speakable lines, the lessons, the check battery and the scene's layout. The game builds it
/// once at start and hands it to the session; no call site reads a game file of its own.
/// </summary>
/// <remarks>
/// <para>
/// Every file is read from the bytes the core's load hashed against the manifest (<see cref="ContentBundle.TryReadHostFile"/>), the scene included, so no
/// game file is read from disk a second time. The parts are immutable and shared by every session built on them.
/// </para>
/// <para>
/// Each loader checks its own file (its shape, and the references it resolves into its model, such as a lesson's speech lines). The checks between files
/// are <see cref="GameDataValidator"/>'s, which reports every problem at once. The host's <c>--validate-content</c> runs it from the command line.
/// </para>
/// </remarks>
public sealed class GameData
{
    /// <summary>The scene layout's path inside the bundle folder.</summary>
    public const string ScenePath = "game/scene.json";

    private GameData(
        ContentBundle bundle,
        NounCatalog nouns,
        RecipeBook recipes,
        PhraseSheet phrases,
        EnglishText english,
        VerbGlosses glosses,
        IReadOnlyList<SpeechLine> speech,
        LessonBook lessons,
        IReadOnlyList<CheckItem> checks,
        SceneLayout scene)
    {
        Bundle = bundle;
        Nouns = nouns;
        Recipes = recipes;
        Phrases = phrases;
        English = english;
        Glosses = glosses;
        Speech = speech;
        Lessons = lessons;
        Checks = checks;
        Scene = scene;
    }

    /// <summary>The content bundle the parts were read from (<c>content/game-v1</c>).</summary>
    public ContentBundle Bundle { get; }

    /// <summary>The noun catalogue (<c>game/nouns.json</c>).</summary>
    public NounCatalog Nouns { get; }

    /// <summary>The crafting recipes (<c>game/recipes.json</c>).</summary>
    public RecipeBook Recipes { get; }

    /// <summary>The phrase sheet (<c>game/phrases.json</c>).</summary>
    public PhraseSheet Phrases { get; }

    /// <summary>The game's English text (<c>game/english.json</c>).</summary>
    public EnglishText English { get; }

    /// <summary>The verbs' English (<c>game/glosses.json</c>).</summary>
    public VerbGlosses Glosses { get; }

    /// <summary>The bundle's speakable lines: the dialogue lines without slots, the prompts, the word cards and the phrases (<see cref="SpeechLines.Enumerate"/>).</summary>
    public IReadOnlyList<SpeechLine> Speech { get; }

    /// <summary>The lessons (<c>game/lessons.json</c>).</summary>
    public LessonBook Lessons { get; }

    /// <summary>The check battery's items (<c>game/checks.json</c>).</summary>
    public IReadOnlyList<CheckItem> Checks { get; }

    /// <summary>The scene's layout (<c>game/scene.json</c>), parsed from the verified bytes.</summary>
    public SceneLayout Scene { get; }

    /// <summary>Reads every game file of a bundle and checks them against each other.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">A file is missing from the bundle or malformed, or the files do not hold together (every problem is listed).</exception>
    /// <exception cref="SceneLayoutException">The scene's layout does not describe a buildable scene.</exception>
    public static GameData Load(ContentBundle bundle) => Checked(Read(bundle));

    /// <summary>The content itself when its files hold together.</summary>
    /// <param name="data">The content, read without the checks.</param>
    /// <exception cref="InvalidDataException">The files do not hold together; the message lists every problem.</exception>
    internal static GameData Checked(GameData data)
    {
        var problems = GameDataValidator.Validate(data);
        if (problems.Count > 0)
        {
            throw new InvalidDataException(
                $"The game content of bundle {data.Bundle.Manifest.BundleId} does not hold together ({problems.Count} {(problems.Count == 1 ? "problem" : "problems")}):\n  "
                + string.Join("\n  ", problems));
        }

        return data;
    }

    /// <summary>Reads every game file of a bundle without the checks between files (the validator's tests build broken content from it).</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">A file is missing from the bundle or malformed.</exception>
    /// <exception cref="SceneLayoutException">The scene's layout does not describe a buildable scene.</exception>
    internal static GameData Read(ContentBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        var nouns = NounCatalog.Load(bundle);
        var phrases = PhraseSheet.Load(bundle);
        var speech = SpeechLines.Enumerate(bundle, nouns, phrases);
        return new GameData(
            bundle,
            nouns,
            RecipeBook.Load(bundle, nouns),
            phrases,
            EnglishText.Load(bundle),
            VerbGlosses.Load(bundle),
            speech,
            LessonBook.Load(bundle, phrases, speech),
            CheckBank.Load(bundle),
            SceneLayout.Parse(HostText(bundle, ScenePath)));
    }

    /// <summary>The same parts with other lessons (a test's own, or none).</summary>
    /// <param name="lessons">The lessons.</param>
    public GameData WithLessons(LessonBook lessons)
    {
        ArgumentNullException.ThrowIfNull(lessons);
        return new GameData(Bundle, Nouns, Recipes, Phrases, English, Glosses, Speech, lessons, Checks, Scene);
    }

    /// <summary>The same parts with another check battery (the validator's tests).</summary>
    /// <param name="checks">The items.</param>
    internal GameData WithChecks(IReadOnlyList<CheckItem> checks) => new(Bundle, Nouns, Recipes, Phrases, English, Glosses, Speech, Lessons, checks, Scene);

    /// <summary>The same parts with another English text (the validator's tests).</summary>
    /// <param name="english">The English text.</param>
    internal GameData WithEnglish(EnglishText english) => new(Bundle, Nouns, Recipes, Phrases, english, Glosses, Speech, Lessons, Checks, Scene);

    /// <summary>The same parts with another scene layout (the validator's tests).</summary>
    /// <param name="scene">The layout.</param>
    internal GameData WithScene(SceneLayout scene) => new(Bundle, Nouns, Recipes, Phrases, English, Glosses, Speech, Lessons, Checks, scene);

    /// <summary>The text of one of the bundle's verified host files.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <param name="path">The file's path inside the bundle folder (<c>game/nouns.json</c>).</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file.</exception>
    public static string HostText(ContentBundle bundle, string path)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(path);
        return bundle.TryReadHostFile(path, out var bytes)
            ? Encoding.UTF8.GetString(bytes.Span)
            : throw new InvalidDataException($"The bundle has no host file {path}: it is missing from the manifest, or the bundle was loaded without hash verification.");
    }
}
