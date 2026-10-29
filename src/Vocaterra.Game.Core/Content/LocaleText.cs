using System.Globalization;
using System.Text.Json;
using Vocaterra.Game.Core.Language.Learner;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.Tip;
using Vocaterra.Nlp.Core.Taxonomy;

namespace Vocaterra.Game.Core.Content;

/// <summary>One verb's forms in the support language (<c>verb.&lt;lemma&gt;</c>): every form listed, none built by a rule.</summary>
/// <param name="Gloss">The infinitive's gloss (<c>to want</c>, <c>to be: what</c>).</param>
/// <param name="Base">The base form (<c>want</c>, <c>be</c>).</param>
/// <param name="Third">The third person singular of the present (<c>wants</c>, <c>is</c>).</param>
/// <param name="Past">The past (<c>wanted</c>, <c>was</c>).</param>
public sealed record VerbForms(string Gloss, string Base, string Third, string Past);

/// <summary>
/// The learner's support language (L1), read from one locale of the locale bundle <c>content/locale</c> (CONTENT_PLAN 3.1 and 3.2, C03; it replaces the
/// <c>EnglishText</c> of S16, <c>game/english.json</c>): every word the game draws that is not the target language, each by its id. The target-language data carries the ids, never
/// the text, so a second locale is a folder of strings, and an edit here touches no save, no fixture and no golden.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lookup.</b> A string is looked for in this locale, then in the fallback locale (<c>en</c>), and a string neither has is the visible marker
/// <c>⟦id⟧</c>, so a missing string shows on screen and is never empty (<see cref="Content.GameDataValidator"/> refuses a shipped locale that lacks one).
/// Placeholders are <c>{name}</c>, filled by the caller.
/// </para>
/// <para>
/// <b>The game's own use of the strings.</b> The feedback after a turn (<see cref="FeedbackFor(GuideTurn)"/>), the objective line
/// (<see cref="ObjectivePieces"/>), the stage lines and the NPCs' roles are built here from the strings, as the English text built them.
/// </para>
/// <para>Plain values, no clock, no random numbers; every comparison is ordinal.</para>
/// </remarks>
public sealed class LocaleText
{
    /// <summary>The feedback outcome keys the locale must have a line for (<c>feedback.outcome.&lt;key&gt;</c>).</summary>
    public static IReadOnlyList<string> OutcomeKeys { get; } =
    [
        "understood", "understood_with_mistake", "not_understood", "uncertain", "dont_know", "help", "choice", "fail_forward", "pointed",
    ];

    /// <summary>The interface words the locale must have (<c>ui.&lt;key&gt;</c>).</summary>
    public static IReadOnlyList<string> UiKeys { get; } =
    [
        "english", "lesson", "stepOf", "continue", "skip", "help", "startLesson", "lessonDone", "retry", "helpDefault", "recapTitle", "controlsTitle", "controlsClose",
        "tipNext", "controlsHint", "stepExplain", "stepModel", "stepListen", "stepRepeat", "stepAnswer", "stepDo", "stepRecap", "outcomeDone", "outcomeSkipped", "outcomeMovedOn",
        "objectiveComplete", "spareTorch", "sessionCompleteTitle", "sessionCompleteLine", "sessionCompleteKeys",
        "btnRepeat", "btnSlower", "btnHelp", "btnDontKnow", "btnType",
        "summaryTitle", "summaryLessons", "summaryLessonDone", "summaryLessonAdvanced", "summaryWords", "summaryUnaided", "summaryForms", "summaryPractise",
        "summaryPractiseNoSe", "summaryPractiseSlip", "summaryPractiseSupport", "summaryPractiseNone", "summaryReplay", "summaryNone", "summaryMore",
        "tensePres", "tensePret", "tenseImpf", "tenseFut", "person1", "person2", "person3", "singular", "plural", "retryAgain",
    ];

    /// <summary>The most error lines one turn's feedback carries.</summary>
    public const int MaxErrorLines = 2;

    private readonly Dictionary<string, string> _strings;
    private readonly Dictionary<string, VerbForms> _verbs;
    private readonly LocaleText? _fallback;

    private LocaleText(string code, string bundleHash, Dictionary<string, string> strings, Dictionary<string, VerbForms> verbs, LocaleText? fallback)
    {
        Code = code;
        BundleHash = bundleHash;
        _strings = strings;
        _verbs = verbs;
        _fallback = fallback;
    }

    /// <summary>The locale's code (BCP-47: <c>en</c>).</summary>
    public string Code { get; }

    /// <summary>The hash of the locale bundle the strings were read from (the session log and the report record it beside game-v1's).</summary>
    public string BundleHash { get; }

    /// <summary>The ids of this locale's own strings and verbs (not the fallback's), ordinal order.</summary>
    public IReadOnlyList<string> Ids => _strings.Keys.Concat(_verbs.Keys.Select(static lemma => VerbPrefix + lemma)).Order(StringComparer.Ordinal).ToList();

    /// <summary>The fallback locale, or <see langword="null"/> for the default locale itself.</summary>
    public LocaleText? Fallback => _fallback;

    private const string VerbPrefix = "verb.";

    /// <summary>
    /// Builds a locale from its files' text: each file is <c>{ "$schema", "about"?, "strings": { "&lt;id&gt;": "&lt;text&gt;" | { verb forms } } }</c>.
    /// An id may appear once across the files.
    /// </summary>
    /// <param name="code">The locale's code.</param>
    /// <param name="bundleHash">The locale bundle's hash.</param>
    /// <param name="files">Each file's name (for the messages) and text.</param>
    /// <param name="fallback">The locale a missing string is looked for in, or <see langword="null"/>.</param>
    /// <exception cref="InvalidDataException">A file is malformed, or an id is given twice or is empty.</exception>
    public static LocaleText Parse(string code, string bundleHash, IEnumerable<(string Name, string Json)> files, LocaleText? fallback = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);
        ArgumentNullException.ThrowIfNull(bundleHash);
        ArgumentNullException.ThrowIfNull(files);
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        var verbs = new Dictionary<string, VerbForms>(StringComparer.Ordinal);
        foreach (var (name, json) in files)
        {
            try
            {
                using var document = JsonDocument.Parse(json);
                foreach (var property in document.RootElement.GetProperty("strings").EnumerateObject())
                {
                    var id = property.Name;
                    if (strings.ContainsKey(id) || (id.StartsWith(VerbPrefix, StringComparison.Ordinal) && verbs.ContainsKey(id[VerbPrefix.Length..])))
                    {
                        throw new InvalidDataException($"locale {code}: the id '{id}' is given twice ({name}).");
                    }

                    if (property.Value.ValueKind == JsonValueKind.Object && id.StartsWith(VerbPrefix, StringComparison.Ordinal))
                    {
                        verbs[id[VerbPrefix.Length..]] = new VerbForms(Field(property.Value, "gloss", id), Field(property.Value, "base", id), Field(property.Value, "third", id), Field(property.Value, "past", id));
                        continue;
                    }

                    var text = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        throw new InvalidDataException($"locale {code}: the string '{id}' is empty or not a string ({name}).");
                    }

                    strings[id] = text;
                }
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                throw new InvalidDataException($"locale {code}: {name} is malformed: {ex.Message}", ex);
            }
        }

        return new LocaleText(code, bundleHash, strings, verbs, fallback);
    }

    /// <summary>This locale with some strings of its own put first (a test's lessons or items with their own text; the locale's own strings stay under them).</summary>
    /// <param name="strings">The strings by id.</param>
    public LocaleText With(IReadOnlyDictionary<string, string> strings)
    {
        ArgumentNullException.ThrowIfNull(strings);
        var merged = new Dictionary<string, string>(_strings, StringComparer.Ordinal);
        foreach (var (id, text) in strings)
        {
            merged[id] = text;
        }

        return new LocaleText(Code, BundleHash, merged, _verbs, _fallback);
    }

    // ---- Lookup.

    /// <summary>The string with this id, from this locale or its fallback, or <see langword="null"/> when neither has it.</summary>
    /// <param name="id">The id (<c>ui.continue</c>).</param>
    public string? Find(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _strings.TryGetValue(id, out var text) ? text : _fallback?.Find(id);
    }

    /// <summary>Whether this locale or its fallback has the string.</summary>
    /// <param name="id">The id.</param>
    public bool Has(string id) => Find(id) is not null;

    /// <summary>The string with this id, or the marker <c>⟦id⟧</c> when no locale has it (so a missing string shows and is never empty).</summary>
    /// <param name="id">The id.</param>
    public string Text(string id) => Find(id) ?? Missing(id);

    /// <summary>The string with <c>{name}</c> placeholders filled in.</summary>
    /// <param name="id">The id.</param>
    /// <param name="values">The placeholder values as name/value pairs.</param>
    public string Text(string id, params (string Name, string Value)[] values) => Fill(Text(id), values);

    /// <summary>The marker a missing string shows as.</summary>
    /// <param name="id">The id.</param>
    public static string Missing(string id) => "⟦" + id + "⟧";

    /// <summary>A verb's forms (<c>verb.&lt;lemma&gt;</c>), or <see langword="null"/> when no locale lists the verb (the book then shows its infinitive alone).</summary>
    /// <param name="lemma">The infinitive.</param>
    public VerbForms? Verb(string lemma)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        return _verbs.TryGetValue(lemma, out var forms) ? forms : _fallback?.Verb(lemma);
    }

    /// <summary>The lemmas of every verb this locale lists (its own, ordinal order).</summary>
    public IReadOnlyList<string> Verbs => _verbs.Keys.Order(StringComparer.Ordinal).ToList();

    // ---- The game's words.

    /// <summary>An interface word (<c>continue</c>, <c>skip</c>...): the string <c>ui.&lt;key&gt;</c>.</summary>
    /// <param name="key">One of <see cref="UiKeys"/>.</param>
    public string Ui(string key) => Text("ui." + key);

    /// <summary>The interface word with <c>{name}</c> placeholders filled in.</summary>
    /// <param name="key">One of <see cref="UiKeys"/>.</param>
    /// <param name="values">The placeholder values as name/value pairs.</param>
    public string Ui(string key, params (string Name, string Value)[] values) => Fill(Ui(key), values);

    /// <summary>
    /// The support language of a speech button or of the hold-to-talk hint (S17), written after the target-language label: <c>repeat</c> for <c>Repetir</c>.
    /// </summary>
    /// <param name="button">One of <see cref="SpeechButtonKeys.All"/>.</param>
    public string ButtonSupport(string button) => Ui("btn" + char.ToUpperInvariant(button[0]) + button[1..]);

    /// <summary>
    /// The line the objective area shows for an objective that is no checklist (S17, C07): <c>objective.&lt;id&gt;.hud</c> ("Go down the trail and find the guide").
    /// Once every objective is done it shows <c>ui.objectiveComplete</c>.
    /// </summary>
    /// <param name="objectiveId">The objective's id (<c>meet-guide</c>).</param>
    public string ObjectiveHudLine(string objectiveId) => Text("objective." + objectiveId + ".hud");

    /// <summary>The explanation of a phrase (<c>phrase.&lt;id&gt;.explain</c>), or <see langword="null"/> when no locale has one.</summary>
    /// <param name="phraseId">The phrase sheet's id.</param>
    public string? Explanation(string phraseId) => Find("phrase." + phraseId + ".explain");

    /// <summary>The gloss of an NPC's line by its speech-line id (<c>line.&lt;lineId&gt;</c>), or <see langword="null"/> when no locale has one.</summary>
    /// <param name="lineId">The speech line's id (<c>guide_greet.1</c>, <c>prompt.guide-open</c>).</param>
    public string? Line(string lineId) => Find("line." + lineId);

    /// <summary>The support-language word of a catalogue noun (<c>noun.&lt;lemma&gt;</c>: <c>fruta</c> gives <c>fruit</c>), or <see langword="null"/> when no locale has it.</summary>
    /// <param name="lemma">The noun.</param>
    public string? NounSupport(string lemma) => Find("noun." + lemma);

    /// <summary>The role of an NPC for the feedback (<c>npc.&lt;id&gt;.role</c>: <c>The guide</c>); the agent's id when no locale has it.</summary>
    /// <param name="npc">The agent id.</param>
    public string NpcName(string npc) => Find("npc." + npc + ".role") ?? npc;

    // ---- The objective line.

    /// <summary>
    /// A checklist as coloured pieces: <c>Campamento (camp): madera (wood) 0/5 · frutas (fruit) 0/3 · ...</c>. A step's gloss is its noun's
    /// (<c>noun.&lt;lemma&gt;</c>) and is left out once the noun is known (<paramref name="isKnown"/>, D-game-6); the title's (<c>objective.&lt;id&gt;.title</c>) once every step's is.
    /// </summary>
    /// <param name="objective">The checklist.</param>
    /// <param name="isKnown">Whether a noun (<c>madera</c>) counts as known (D-game-6).</param>
    public IReadOnlyList<ObjectivePiece> ObjectivePieces(Objective objective, Func<string, bool> isKnown)
    {
        ArgumentNullException.ThrowIfNull(objective);
        ArgumentNullException.ThrowIfNull(isKnown);
        var pieces = new List<ObjectivePiece>();
        var allKnown = objective.Steps.All(step => isKnown(step.Noun));
        pieces.Add(new ObjectivePiece(objective.Title, ObjectivePieceKind.Title));
        if (!allKnown)
        {
            pieces.Add(new ObjectivePiece(" (" + Text("objective." + objective.Id + ".title") + ")", ObjectivePieceKind.Support));
        }

        pieces.Add(new ObjectivePiece(": ", ObjectivePieceKind.Plain));
        for (var i = 0; i < objective.Steps.Count; i++)
        {
            var step = objective.Steps[i];
            if (i > 0)
            {
                pieces.Add(new ObjectivePiece(" · ", ObjectivePieceKind.Plain));
            }

            pieces.Add(new ObjectivePiece(step.Label, ObjectivePieceKind.Word, step.Done));
            if (!isKnown(step.Noun) && NounSupport(step.Noun) is { } gloss)
            {
                pieces.Add(new ObjectivePiece(" (" + gloss + ")", ObjectivePieceKind.Support, step.Done));
            }

            pieces.Add(new ObjectivePiece(string.Create(CultureInfo.InvariantCulture, $" {Math.Min(step.Count, step.Target)}/{step.Target}"), ObjectivePieceKind.Count, step.Done));
        }

        return pieces;
    }

    /// <summary>The objective line as one string (see <see cref="ObjectivePieces"/>).</summary>
    /// <param name="objective">The checklist.</param>
    /// <param name="isKnown">Whether a noun counts as known.</param>
    public string ObjectiveLine(Objective objective, Func<string, bool> isKnown) =>
        string.Concat(ObjectivePieces(objective, isKnown).Select(static p => p.Text));

    // ---- Feedback after a turn.

    /// <summary>
    /// The plain feedback for a finished turn: the outcome (understood, understood with a mistake, not understood, an uncertain recognition, "no sé", help,
    /// a choice, a fail-forward, a pointed arrow) and, for a turn understood with mistakes, one line per distinct error code (at most two). An error code the
    /// locale has no line for gets the fallback line, and an uncertain recognition is said to be the microphone's trouble, never the language's (D-game-9).
    /// </summary>
    /// <param name="turn">The turn.</param>
    public IReadOnlyList<string> FeedbackFor(GuideTurn turn)
    {
        ArgumentNullException.ThrowIfNull(turn);
        var npc = NpcName(turn.Npc);
        var lines = new List<string>();
        if (turn.Uncertain && turn.Input == InputKind.Spoken)
        {
            lines.Add(Outcome("uncertain", ("npc", npc)));
            return lines;
        }

        if (!turn.Communicated)
        {
            lines.Add(Outcome("not_understood", ("npc", npc)));
            return lines;
        }

        var isHelp = string.Equals(turn.Intent, "ask_help", StringComparison.Ordinal);
        var isDontKnow = string.Equals(turn.Intent, "dont_know", StringComparison.Ordinal);
        if (isDontKnow || isHelp)
        {
            lines.Add(Outcome(isDontKnow ? "dont_know" : "help", ("npc", npc)));
        }
        else if (turn.Choice)
        {
            lines.Add(Outcome("choice", ("npc", npc)));
        }
        else if (turn.Errors.Count == 0)
        {
            lines.Add(Outcome("understood", ("npc", npc)));
        }
        else
        {
            lines.Add(Outcome("understood_with_mistake", ("npc", npc)));
            foreach (var error in turn.Errors.DistinctBy(static e => e.Tag, StringComparer.Ordinal).Take(MaxErrorLines))
            {
                lines.Add(ErrorLine(error));
            }
        }

        if (turn.FailForward)
        {
            lines.Add(Outcome("fail_forward", ("npc", npc)));
        }

        if (turn.Pointer is { } pointer)
        {
            var noun = NounSupport(pointer.Place) ?? pointer.Place;
            lines.Add(Outcome("pointed", ("npc", npc), ("noun", noun), ("blocks", pointer.Blocks.ToString(CultureInfo.InvariantCulture))));
        }

        return lines;
    }

    /// <summary>
    /// The plain feedback for a line the learner gave, as the book's "What I said" page shows it (S20): the same outcome lines as
    /// <see cref="FeedbackFor(GuideTurn)"/>, read from the history entry (its core verdict and errors; a held or unsure line says so).
    /// </summary>
    /// <param name="entry">The history entry.</param>
    public IReadOnlyList<string> FeedbackFor(InputEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var npc = NpcName(entry.Npc ?? string.Empty);
        if (entry.Held)
        {
            return [Outcome("uncertain", ("npc", npc))];
        }

        if (!entry.Communicated)
        {
            return [Outcome("not_understood", ("npc", npc))];
        }

        if (string.Equals(entry.Intent, "dont_know", StringComparison.Ordinal))
        {
            return [Outcome("dont_know", ("npc", npc))];
        }

        if (string.Equals(entry.Intent, "ask_help", StringComparison.Ordinal))
        {
            return [Outcome("help", ("npc", npc))];
        }

        if (entry.Errors.Count == 0)
        {
            return [Outcome("understood", ("npc", npc))];
        }

        var lines = new List<string> { Outcome("understood_with_mistake", ("npc", npc)) };
        foreach (var error in entry.Errors.DistinctBy(static e => e.Tag, StringComparer.Ordinal).Take(MaxErrorLines))
        {
            lines.Add(ErrorLine(error));
        }

        return lines;
    }

    /// <summary>The line for one error of the core: the locale's line for its code (<c>feedback.error.&lt;tag&gt;</c>), or the fallback line for a code it does not know (or a line that needs a form the core did not give).</summary>
    /// <param name="error">The error.</param>
    public string ErrorLine(GuideError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var values = new[] { ("expected", error.Expected), ("actual", error.Actual), ("tag", error.Tag) };
        if (Find("feedback.error." + error.Tag) is { } template && !NeedsMissingForm(template, error))
        {
            return Fill(template, values);
        }

        return Fill(Text("feedback.unknownError"), values);
    }

    /// <summary>Whether the locale has its own line for an error code (as opposed to the fallback).</summary>
    /// <param name="tag">The core's error code.</param>
    public bool HasErrorLine(string tag) => Has("feedback.error." + tag);

    /// <summary>The error codes of the core the locale has no line of its own for: the tests use this to keep the strings in step with <see cref="ErrorTags"/>.</summary>
    public IReadOnlyList<string> MissingErrorTags() => ErrorTags.All.Where(tag => !HasErrorLine(tag)).ToList();

    private string Outcome(string key, params (string Name, string Value)[] values) => Text("feedback.outcome." + key, values);

    private static bool NeedsMissingForm(string template, GuideError error) =>
        (template.Contains("{expected}", StringComparison.Ordinal) && error.Expected.Length == 0)
        || (template.Contains("{actual}", StringComparison.Ordinal) && error.Actual.Length == 0);

    private static string Fill(string template, params (string Name, string Value)[] values)
    {
        var text = template;
        foreach (var (name, value) in values)
        {
            text = text.Replace("{" + name + "}", value, StringComparison.Ordinal);
        }

        return text;
    }

    private static string Field(JsonElement element, string name, string id) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException($"the verb '{id}' has no '{name}'.");
}
