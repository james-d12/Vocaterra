using System.Text;
using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.Core.Atmosphere;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Items.Crafting;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Needs;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Content;

/// <summary>
/// The game's content, loaded once from the bundle's verified host files and checked as a whole (content-plan 3.7, C02): the noun catalogue, the recipes,
/// the phrase sheet, the tips and the controls, the speakable lines, the lessons, the check battery and the scene's layout, with the learner's support
/// language from the locale bundle (C03). The game builds it once at start and hands it to the session; no call site reads a game file of its own.
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

    private readonly Dictionary<string, List<SpeechLine>> _linesByPlainText;

    private GameData(
        ContentBundle bundle,
        LocaleText locale,
        NounCatalog nouns,
        RecipeBook recipes,
        PhraseSheet phrases,
        IReadOnlyList<TipSpec> tips,
        IReadOnlyList<ControlHelp> controls,
        IReadOnlyList<SpeechLine> speech,
        LessonBook lessons,
        CheckBank checks,
        SceneLayout scene,
        GameWords words,
        NpcRoster npcs,
        HostEventTable hostEvents,
        ObjectiveBook objectives,
        NeedsData needs,
        DayData day,
        WeatherData weather,
        ProcedureBook procedures,
        StationBook stations,
        ActionTable actions)
    {
        Bundle = bundle;
        Procedures = procedures;
        Stations = stations;
        Actions = actions;
        Needs = needs;
        Day = day;
        Weather = weather;
        Objectives = objectives;
        Words = words;
        Npcs = npcs;
        HostEvents = hostEvents;
        Locale = locale;
        Nouns = nouns;
        Recipes = recipes;
        Phrases = phrases;
        Tips = tips;
        Controls = controls;
        Glosses = VerbGlosses.From(locale);
        Speech = speech;
        Lessons = lessons;
        Checks = checks;
        Scene = scene;
        _linesByPlainText = new Dictionary<string, List<SpeechLine>>(StringComparer.Ordinal);
        foreach (var line in speech.Where(static l => l.Kind is SpeechKind.Dialogue or SpeechKind.Prompt)
                     .OrderBy(static l => l.Kind == SpeechKind.Dialogue ? 0 : 1).ThenBy(static l => l.LineId, StringComparer.Ordinal))
        {
            var key = PhraseSheet.Plain(line.Text);
            if (!_linesByPlainText.TryGetValue(key, out var same))
            {
                _linesByPlainText[key] = same = [];
            }

            same.Add(line);
        }
    }

    /// <summary>The content bundle the parts were read from (<c>content/game-v1</c>).</summary>
    public ContentBundle Bundle { get; }

    /// <summary>The learner's support language (a locale of <c>content/locale</c>): every word drawn that is not the target language.</summary>
    public LocaleText Locale { get; }

    /// <summary>The noun catalogue (<c>game/nouns.json</c>).</summary>
    public NounCatalog Nouns { get; }

    /// <summary>The crafting recipes (<c>game/recipes.json</c>).</summary>
    public RecipeBook Recipes { get; }

    /// <summary>The phrase sheet (<c>game/phrases.json</c>, with each row's meaning and card from the locale).</summary>
    public PhraseSheet Phrases { get; }

    /// <summary>The first-time tips (<c>game/tips.json</c>, their text from the locale).</summary>
    public IReadOnlyList<TipSpec> Tips { get; }

    /// <summary>The controls help (<c>game/controls.json</c>, its text from the locale).</summary>
    public IReadOnlyList<ControlHelp> Controls { get; }

    /// <summary>The verbs' support-language forms (the locale's <c>verb.&lt;lemma&gt;</c>).</summary>
    public VerbGlosses Glosses { get; }

    /// <summary>The bundle's speakable lines: the dialogue lines without slots, the prompts, the word cards and the phrases (<see cref="SpeechLines.Enumerate"/>).</summary>
    public IReadOnlyList<SpeechLine> Speech { get; }

    /// <summary>The lessons (<c>game/lessons.json</c>, their text from the locale).</summary>
    public LessonBook Lessons { get; }

    /// <summary>The check battery's items (<c>game/checks.json</c>, their text from the locale).</summary>
    public CheckBank Checks { get; }

    /// <summary>The scene's layout (<c>game/scene.json</c>), parsed from the verified bytes.</summary>
    public SceneLayout Scene { get; }

    /// <summary>The target-language words the game itself uses (<c>game/words.json</c>, C05): directions, button labels, pronouns, key words and the like.</summary>
    public GameWords Words { get; }

    /// <summary>The NPCs (<c>game/npcs.json</c> and <c>game/npcs/&lt;id&gt;.json</c>, C08): their ids in order, labels, names, figures and the phrases they practise.</summary>
    public NpcRoster Npcs { get; }

    /// <summary>What each NPC host event does in the world (<c>game/host-events.json</c>, C08).</summary>
    public HostEventTable HostEvents { get; }

    /// <summary>The session's objectives, their order, the closing pool, the starting items and the beacon's start (<c>game/session.json</c>, <c>game/objectives/</c>, C07).</summary>
    public ObjectiveBook Objectives { get; }

    /// <summary>The player's needs: the drives, health and stamina with their stages and links, the foods and the death rules (<c>game/needs.json</c>, N01).</summary>
    public NeedsData Needs { get; }

    /// <summary>The day and night cycle: its length, the hours each phase begins, the start hour and the wake hour (<c>game/day.json</c>, E01).</summary>
    public DayData Day { get; }

    /// <summary>The weather: its kinds as rows, the kind a new game starts in and the time of a change (<c>game/weather.json</c>, E04).</summary>
    public WeatherData Weather { get; }

    /// <summary>The procedures (<c>game/procedures.json</c> and <c>game/procedures/&lt;id&gt;.json</c>, W05): the steps of the jobs done at the stations.</summary>
    public ProcedureBook Procedures { get; }

    /// <summary>The stations (<c>game/stations.json</c>, W05): the scene prop, the teaching NPC and the fixtures of each.</summary>
    public StationBook Stations { get; }

    /// <summary>The actions (<c>game/actions.json</c>, W05): the verbs for each pair of a use and a fixture.</summary>
    public ActionTable Actions { get; }

    /// <summary>Reads every game file of a bundle, with the support language, and checks them against each other.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <param name="locale">The support language (<see cref="LocaleBundle.Text"/>).</param>
    /// <exception cref="InvalidDataException">A file is missing from the bundle or malformed, or the files do not hold together (every problem is listed).</exception>
    /// <exception cref="SceneLayoutException">The scene's layout does not describe a buildable scene.</exception>
    public static GameData Load(ContentBundle bundle, LocaleText locale) => Checked(Read(bundle, locale));

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
    /// <param name="locale">The support language.</param>
    /// <exception cref="InvalidDataException">A file is missing from the bundle or malformed.</exception>
    /// <exception cref="SceneLayoutException">The scene's layout does not describe a buildable scene.</exception>
    internal static GameData Read(ContentBundle bundle, LocaleText locale)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(locale);
        var npcs = NpcRoster.Load(bundle);
        locale = locale.WithNames(npcs.All.ToDictionary(static n => n.Id, static n => n.Called, StringComparer.Ordinal), InputKeys.Defaults); // {npc:…} and {key:…} (C04)
        var nouns = NounCatalog.Load(bundle);
        var phrases = PhraseSheet.Load(bundle, locale, npcs);
        var speech = SpeechLines.Enumerate(bundle, nouns, phrases, npcs);
        return new GameData(
            bundle,
            locale,
            nouns,
            RecipeBook.Load(bundle, nouns),
            phrases,
            TipBook.LoadTips(bundle, locale),
            TipBook.LoadControls(bundle, locale),
            speech,
            LessonBook.Load(bundle, phrases, speech, locale, npcs.Ids),
            CheckBank.Load(bundle, locale),
            SceneLayout.Parse(HostText(bundle, ScenePath)),
            GameWords.Load(bundle),
            npcs,
            HostEventTable.Load(bundle),
            ObjectiveBook.Load(bundle, nouns),
            NeedsData.Load(bundle),
            DayData.Load(bundle),
            WeatherData.Load(bundle),
            ProcedureBook.Load(bundle),
            StationBook.Load(bundle),
            ActionTable.Load(bundle));
    }

    /// <summary>
    /// The support-language gloss of an NPC's line as the panel shows it (S16), or <see langword="null"/> when it has none (a line with slots, or one built in
    /// code). The locale keys the glosses by speech-line id (<c>line.&lt;lineId&gt;</c>, C03); the line is found by its text without case, accents or
    /// punctuation, the speaker's line first, so a Spanish edit keeps its gloss (the id stays).
    /// </summary>
    /// <param name="speaker">The NPC that says the line (an agent id).</param>
    /// <param name="text">The line as shown.</param>
    public string? SupportForLine(string speaker, string text)
    {
        ArgumentNullException.ThrowIfNull(speaker);
        ArgumentNullException.ThrowIfNull(text);
        if (!_linesByPlainText.TryGetValue(PhraseSheet.Plain(text), out var same))
        {
            return null;
        }

        foreach (var line in same.Where(l => string.Equals(l.Speaker, SpeechSpeakers.OfAgent(speaker), StringComparison.Ordinal)).Concat(same))
        {
            if (Locale.Line(line.LineId) is { } gloss)
            {
                return gloss;
            }
        }

        return null;
    }

    /// <summary>The same parts with other lessons (a test's own, or none).</summary>
    /// <param name="lessons">The lessons.</param>
    public GameData WithLessons(LessonBook lessons)
    {
        ArgumentNullException.ThrowIfNull(lessons);
        return new GameData(Bundle, Locale, Nouns, Recipes, Phrases, Tips, Controls, Speech, lessons, Checks, Scene, Words, Npcs, HostEvents, Objectives, Needs, Day, Weather, Procedures, Stations, Actions);
    }

    /// <summary>The same parts with another phrase sheet (the validator's tests).</summary>
    /// <param name="phrases">The sheet.</param>
    internal GameData WithPhrases(PhraseSheet phrases) => new(Bundle, Locale, Nouns, Recipes, phrases, Tips, Controls, Speech, Lessons, Checks, Scene, Words, Npcs, HostEvents, Objectives, Needs, Day, Weather, Procedures, Stations, Actions);

    /// <summary>The same parts with another check battery (the validator's tests).</summary>
    /// <param name="checks">The items.</param>
    internal GameData WithChecks(CheckBank checks) => new(Bundle, Locale, Nouns, Recipes, Phrases, Tips, Controls, Speech, Lessons, checks, Scene, Words, Npcs, HostEvents, Objectives, Needs, Day, Weather, Procedures, Stations, Actions);

    /// <summary>The same parts with another support language (the validator's tests).</summary>
    /// <param name="locale">The locale.</param>
    internal GameData WithLocale(LocaleText locale) => new(Bundle, locale, Nouns, Recipes, Phrases, Tips, Controls, Speech, Lessons, Checks, Scene, Words, Npcs, HostEvents, Objectives, Needs, Day, Weather, Procedures, Stations, Actions);

    /// <summary>The same parts with another scene layout (the validator's tests).</summary>
    /// <param name="scene">The layout.</param>
    internal GameData WithScene(SceneLayout scene) => new(Bundle, Locale, Nouns, Recipes, Phrases, Tips, Controls, Speech, Lessons, Checks, scene, Words, Npcs, HostEvents, Objectives, Needs, Day, Weather, Procedures, Stations, Actions);

    /// <summary>The same parts with other target-language words (the validator's tests).</summary>
    /// <param name="words">The words.</param>
    internal GameData WithWords(GameWords words) => new(Bundle, Locale, Nouns, Recipes, Phrases, Tips, Controls, Speech, Lessons, Checks, Scene, words, Npcs, HostEvents, Objectives, Needs, Day, Weather, Procedures, Stations, Actions);

    /// <summary>The same parts with other NPCs (the validator's tests).</summary>
    /// <param name="npcs">The NPCs.</param>
    internal GameData WithNpcs(NpcRoster npcs) => new(Bundle, Locale, Nouns, Recipes, Phrases, Tips, Controls, Speech, Lessons, Checks, Scene, Words, npcs, HostEvents, Objectives, Needs, Day, Weather, Procedures, Stations, Actions);

    /// <summary>The same parts with another host-event table (the validator's tests).</summary>
    /// <param name="hostEvents">The table.</param>
    internal GameData WithHostEvents(HostEventTable hostEvents) => new(Bundle, Locale, Nouns, Recipes, Phrases, Tips, Controls, Speech, Lessons, Checks, Scene, Words, Npcs, hostEvents, Objectives, Needs, Day, Weather, Procedures, Stations, Actions);

    /// <summary>The same parts with other objectives (the validator's tests).</summary>
    /// <param name="objectives">The objectives.</param>
    internal GameData WithObjectives(ObjectiveBook objectives) => new(Bundle, Locale, Nouns, Recipes, Phrases, Tips, Controls, Speech, Lessons, Checks, Scene, Words, Npcs, HostEvents, objectives, Needs, Day, Weather, Procedures, Stations, Actions);

    /// <summary>The same parts with other tips (the validator's tests).</summary>
    /// <param name="tips">The tips.</param>
    internal GameData WithTips(IReadOnlyList<TipSpec> tips) => new(Bundle, Locale, Nouns, Recipes, Phrases, tips, Controls, Speech, Lessons, Checks, Scene, Words, Npcs, HostEvents, Objectives, Needs, Day, Weather, Procedures, Stations, Actions);

    /// <summary>The same parts with other procedures (the validator's tests).</summary>
    /// <param name="procedures">The procedures.</param>
    internal GameData WithProcedures(ProcedureBook procedures) => new(Bundle, Locale, Nouns, Recipes, Phrases, Tips, Controls, Speech, Lessons, Checks, Scene, Words, Npcs, HostEvents, Objectives, Needs, Day, Weather, procedures, Stations, Actions);

    /// <summary>The same parts with other stations (the validator's tests).</summary>
    /// <param name="stations">The stations.</param>
    internal GameData WithStations(StationBook stations) => new(Bundle, Locale, Nouns, Recipes, Phrases, Tips, Controls, Speech, Lessons, Checks, Scene, Words, Npcs, HostEvents, Objectives, Needs, Day, Weather, Procedures, stations, Actions);

    /// <summary>The same parts with another action table (the validator's tests).</summary>
    /// <param name="actions">The table.</param>
    internal GameData WithActions(ActionTable actions) => new(Bundle, Locale, Nouns, Recipes, Phrases, Tips, Controls, Speech, Lessons, Checks, Scene, Words, Npcs, HostEvents, Objectives, Needs, Day, Weather, Procedures, Stations, actions);

    /// <summary>The same parts with other needs (the validator's tests).</summary>
    /// <param name="needs">The needs.</param>
    internal GameData WithNeeds(NeedsData needs) => new(Bundle, Locale, Nouns, Recipes, Phrases, Tips, Controls, Speech, Lessons, Checks, Scene, Words, Npcs, HostEvents, Objectives, needs, Day, Weather, Procedures, Stations, Actions);

    /// <summary>The same parts with other weather (the validator's tests).</summary>
    /// <param name="weather">The weather.</param>
    internal GameData WithWeather(WeatherData weather) => new(Bundle, Locale, Nouns, Recipes, Phrases, Tips, Controls, Speech, Lessons, Checks, Scene, Words, Npcs, HostEvents, Objectives, Needs, Day, weather, Procedures, Stations, Actions);

    /// <summary>The same parts with another day cycle (the validator's tests).</summary>
    /// <param name="day">The cycle.</param>
    internal GameData WithDay(DayData day) => new(Bundle, Locale, Nouns, Recipes, Phrases, Tips, Controls, Speech, Lessons, Checks, Scene, Words, Npcs, HostEvents, Objectives, Needs, day, Weather, Procedures, Stations, Actions);

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
