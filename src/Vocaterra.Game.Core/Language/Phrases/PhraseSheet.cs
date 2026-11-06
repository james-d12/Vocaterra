using System.Globalization;
using System.Text;
using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Prompts;

namespace Vocaterra.Game.Core.Language.Phrases;

/// <summary>
/// One row of the phrase sheet as the game reads it at run time (<c>game/phrases.json</c>, the machine-readable copy of the rows of
/// <c>content/PHRASES.md</c>, which stays the source of truth; a test holds the two together row by row).
/// </summary>
/// <param name="Id">The row's id (<c>pedir-agua</c>), the key the phrase tracking uses (<c>phrase.&lt;id&gt;</c>).</param>
/// <param name="Says">Who says the phrase: <c>says</c> (the learner), <c>hears</c> (an NPC), <c>both</c>, or <c>does</c> (an instruction an NPC gives while a procedure is done, W05).</param>
/// <param name="Spanish">The phrase as taught, with its accents and inverted marks.</param>
/// <param name="English">The intended meaning, in the support language (<c>phrase.&lt;id&gt;.meaning</c> in the locale).</param>
/// <param name="Intent">For a learner row, the bundle intent every variant resolves to; <see langword="null"/> for a <c>hears</c> row.</param>
/// <param name="Prompt">The prompt the phrase is said at (a <c>hears</c> row: the prompt whose text it is).</param>
/// <param name="Variants">Other things the game accepts as the same phrase.</param>
/// <param name="Card">The support card text, <c>phrase = meaning · word = gloss</c> (<c>phrase.&lt;id&gt;.card</c> in the locale).</param>
/// <param name="AudioLineIds">The bundle dialogue line ids whose NPC audio speaks the phrase (S9).</param>
/// <param name="ReviewLine">The dialogue line the guide's review of the phrase asks with when the bundle has no review pool for it (S8; C08: it was a table in the
/// code), or <see langword="null"/> for none.</param>
public sealed record PhraseRow(
    string Id,
    string Says,
    string Spanish,
    string English,
    string? Intent,
    string? Prompt,
    IReadOnlyList<string> Variants,
    string Card,
    IReadOnlyList<string> AudioLineIds,
    string? ReviewLine = null)
{
    /// <summary>Whether the learner says the phrase (<c>says</c> or <c>both</c>).</summary>
    public bool IsLearnerPhrase => string.Equals(Says, "says", StringComparison.Ordinal) || string.Equals(Says, "both", StringComparison.Ordinal);

    /// <summary>Whether the row is an instruction an NPC gives while a procedure is done (<c>does</c>, W05): no intent, no prompt, no learner error.</summary>
    public bool IsInstruction => string.Equals(Says, "does", StringComparison.Ordinal);

    /// <summary>The phrase of the support card, the part before the first <c> = </c>: what the panel shows as the expected phrase.</summary>
    public string CardPhrase
    {
        get
        {
            var cut = Card.IndexOf(" = ", StringComparison.Ordinal);
            return cut < 0 ? Card : Card[..cut];
        }
    }

    /// <summary>The phrase and its accepted variants.</summary>
    public IEnumerable<string> Texts => Variants.Prepend(Spanish);
}

/// <summary>
/// The phrase sheet at run time: the rows of <c>game/phrases.json</c> in the bundle (a host-owned, hashed file), and the small questions the speech
/// UX asks of it: which phrase a prompt expects of the learner (the "Esperaba" of the panel's heard-versus-expected line), and which row a phrase
/// offered as a choice is (its label). S8 (phrase progress) reads the same rows.
/// </summary>
/// <remarks>
/// The sheet is data: nothing here reads the clock or draws a random number, and every comparison is ordinal.
/// </remarks>
public sealed class PhraseSheet
{
    /// <summary>The sheet's path inside the bundle folder.</summary>
    public const string RelativePath = "game/phrases.json";

    private readonly List<PhraseRow> _rows;
    private readonly IReadOnlyDictionary<string, string> _answeredAs;
    private readonly NounCatalog? _nouns;

    private PhraseSheet(List<PhraseRow> rows, IReadOnlyDictionary<string, string>? answeredAs = null, NounCatalog? nouns = null)
    {
        _nouns = nouns;
        _rows = rows;
        _answeredAs = answeredAs ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>The rows in the sheet's order.</summary>
    public IReadOnlyList<PhraseRow> Rows => _rows;

    /// <summary>Reads the sheet from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <param name="locale">The support language, which holds each row's meaning and card (<c>phrase.&lt;id&gt;.meaning</c>, <c>.card</c>).</param>
    /// <param name="npcs">The NPCs: the prompts they wait on that no row names are answered with the rows of one that accepts the same answers (<see cref="PhrasesAt"/>).</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file is malformed, or a row's meaning or card is not in the locale.</exception>
    public static PhraseSheet Load(ContentBundle bundle, LocaleText locale, NpcRoster npcs)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(npcs);
        if (!bundle.TryReadHostFile(RelativePath, out var bytes))
        {
            throw new InvalidDataException($"The bundle has no host file {RelativePath}: it is missing from the manifest, or the bundle was loaded without hash verification.");
        }

        var rows = Parse(Encoding.UTF8.GetString(bytes.Span), locale)._rows;
        return new PhraseSheet(rows, AnsweredAlike(rows, bundle, npcs), NounCatalog.Load(bundle));
    }

    /// <summary>The locale id of a row's support-language text: <c>phrase.&lt;id&gt;.&lt;field&gt;</c> (meaning, card, explain).</summary>
    /// <param name="id">The row's id.</param>
    /// <param name="field">The field.</param>
    public static string LocaleId(string id, string field) => "phrase." + id + "." + field;

    /// <summary>Builds the sheet from the file's text and the support language.</summary>
    /// <param name="json">The contents of <c>game/phrases.json</c>.</param>
    /// <param name="locale">The support language, which holds each row's meaning and card.</param>
    /// <exception cref="InvalidDataException">The text is malformed, or a row's meaning or card is not in the locale.</exception>
    public static PhraseSheet Parse(string json, LocaleText locale)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(locale);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != 1)
            {
                throw new InvalidDataException($"{RelativePath} must have schemaVersion 1.");
            }

            var rows = new List<PhraseRow>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var element in root.GetProperty("phrases").EnumerateArray())
            {
                var id = element.GetProperty("id").GetString()!;
                var row = new PhraseRow(
                    id,
                    element.GetProperty("says").GetString()!,
                    element.GetProperty("spanish").GetString()!,
                    Required(locale, id, "meaning"),
                    Optional(element, "intent"),
                    Optional(element, "prompt"),
                    Strings(element, "variants"),
                    Required(locale, id, "card"),
                    Strings(element, "audioLineIds"),
                    Optional(element, "reviewLine"));
                if (!ids.Add(row.Id))
                {
                    throw new InvalidDataException($"{RelativePath}: the phrase id '{row.Id}' appears twice.");
                }

                rows.Add(row);
            }

            return new PhraseSheet(rows);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    /// <summary>The row with this id, or <see langword="null"/>.</summary>
    /// <param name="id">The sheet's id (<c>pedir-agua</c>).</param>
    public PhraseRow? Find(string id) => _rows.FirstOrDefault(row => string.Equals(row.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// Whether a phrase is one the learner practises: a row the learner says, that is not a service line ("ayuda", "no sé", which are help, never a phrase to
    /// practise). The second try (X04) and the session summary's practise list use it.
    /// </summary>
    /// <param name="id">The phrase sheet's id.</param>
    public bool IsPractisable(string id) => Find(id) is { } row && row.IsLearnerPhrase && !IsService(row);

    /// <summary>
    /// The learner phrases an NPC prompt asks for, in the sheet's order: the rows whose prompt is this prompt's, without the service words (help,
    /// "no sé"), which are possible at every prompt and are never what the prompt is waiting for. A prompt an NPC waits on that no row names is answered
    /// with the rows of another NPC's prompt that accepts the same answers (village-plan V01): the guide's offer with the stall's offer rows (the sheet's
    /// <c>si-por-favor</c> and <c>no-gracias</c> work for both).
    /// </summary>
    /// <param name="prompt">The prompt id (<c>stall-need</c>).</param>
    public IReadOnlyList<PhraseRow> PhrasesAt(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var group = _answeredAs.GetValueOrDefault(prompt, prompt);
        return _rows.Where(row => IsAsked(row) && string.Equals(row.Prompt, group, StringComparison.Ordinal)).ToList();
    }

    /// <summary>
    /// The sheet's id of the phrase a communicated turn at a prompt used, by the sheet alone: the row the prompt teaches for the turn's intent (<see cref="PhrasesAt"/>,
    /// the rows whose home it is; <see cref="OfIntent"/> picks among several), else the fixed mapping of <see cref="PhraseIds.For"/>, which credits the stall's and the
    /// river's phrases wherever they are said (a request is <c>pedir-agua</c> whatever the item: at the villager it is a transfer). A prompt's own row comes first
    /// (village-plan V12b): the baker's "quiero dos panes, por favor" was the stall's <c>pedir-agua</c> while her file named no focus, though her prompt teaches
    /// <c>quiero-dos-panes</c>. <see langword="null"/> for a turn that is none of the sheet's phrases (a question about a tree).
    /// </summary>
    /// <param name="prompt">The prompt the turn answered.</param>
    /// <param name="intent">The intent the core resolved.</param>
    /// <param name="slots">The slots the core filled.</param>
    /// <param name="river">The river's noun (<c>río</c>, <c>game/words.json</c>).</param>
    /// <param name="practised">In free talk, the phrases the NPCs' open prompts practise (<see cref="NpcRoster.Homes"/>), for <see cref="ByPlace"/>; <see langword="null"/> in a lesson's
    /// step or a review, whose phrase is the step's.</param>
    /// <param name="said">In free talk, the phrase of a lesson begun or done that the line is (village-plan V20, <see cref="Said"/>): it comes before the fixed mapping, which
    /// knows only the stall's and the river's phrases; <see langword="null"/> for none.</param>
    public string? PhraseFor(string prompt, string intent, IReadOnlyDictionary<string, string> slots, string river, IEnumerable<string>? practised = null, string? said = null)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var here = PhrasesAt(prompt);
        if (practised is not null && ByPlace(here, intent, slots, practised, out var placed))
        {
            return placed;
        }

        return OfIntent(here, intent, slots, _nouns) ?? said ?? PhraseIds.For(intent, slots, river);
    }

    /// <summary>
    /// V12b's rule for one row (village-plan V14): in free talk, when the only row of the turn's intent among these (an NPC's focus, or the rows its prompt teaches) names
    /// another place than the one the turn asks about, the phrase is the row practised elsewhere that names it, else none. The villager practises <c>¿Dónde está el
    /// río?</c>, and <c>¿dónde está la panadería?</c> said to him is the fisher's <c>donde-esta-la-panaderia</c> (a transfer), not the river's; <c>¿dónde está la tienda?</c>,
    /// which no practised row names, is none of them, as at the fisher, whose two rows name neither. Returns whether the rule applies, with the phrase it gives.
    /// </summary>
    /// <param name="rows">The rows in play.</param>
    /// <param name="intent">The intent the core resolved.</param>
    /// <param name="slots">The slots the core filled: the rule needs a <c>place</c> and no <c>item</c>.</param>
    /// <param name="practised">The phrases the NPCs' open prompts practise.</param>
    /// <param name="phrase">The practised row of the intent that names the place, in the sheet's order, or <see langword="null"/>.</param>
    public bool ByPlace(IEnumerable<PhraseRow> rows, string intent, IReadOnlyDictionary<string, string> slots, IEnumerable<string> practised, out string? phrase)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(practised);
        phrase = null;
        var ofIntent = rows.Where(row => string.Equals(row.Intent, intent, StringComparison.Ordinal)).ToList();
        if (ofIntent.Count != 1 || slots.ContainsKey("item") || !slots.TryGetValue("place", out var place) || Names(ofIntent[0], place, _nouns))
        {
            return false;
        }

        var homes = practised.ToHashSet(StringComparer.Ordinal);
        phrase = _rows.FirstOrDefault(row => row.IsLearnerPhrase && homes.Contains(row.Id) && string.Equals(row.Intent, intent, StringComparison.Ordinal) && Names(row, place, _nouns))?.Id;
        return true;
    }

    /// <summary>
    /// The id of the row of an intent among these rows (the phrases in play on a prompt, or the rows it teaches): the only one, or, when several share the intent (the
    /// fisher's <c>¿Dónde está la panadería?</c> and <c>¿Dónde está la herrería?</c>), the one whose phrase or a variant names the place or the item the turn asked
    /// about. <see langword="null"/> when no row has the intent, the only one names another item than the turn asks for (V26), or several have it and none names the turn's place or item: crediting one of them would be a guess.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <param name="intent">The intent the core resolved.</param>
    /// <param name="slots">The slots the core filled.</param>
    /// <param name="nouns">The noun catalogue, for the plural of an item (V30: a row whose every variant says <c>manzanas</c> names <c>manzana</c>); <see langword="null"/> names the word as given only.</param>
    public static string? OfIntent(IEnumerable<PhraseRow> rows, string intent, IReadOnlyDictionary<string, string> slots, NounCatalog? nouns = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(slots);
        var ofIntent = rows.Where(row => string.Equals(row.Intent, intent, StringComparison.Ordinal)).ToList();
        if (ofIntent.Count == 1)
        {
            // V26: the only row of the intent is the phrase unless the turn asks for an item the row does not name: `quiero dos manzanas` is no use of `quiero dos panes`
            // (a request the baker refuses). A count, a place or no item at all leaves the row credited: `quiero tres panes` names the same bread.
            return slots.TryGetValue("item", out var asked) && !Names(ofIntent[0], asked, nouns) ? null : ofIntent[0].Id;
        }

        if (ofIntent.Count == 0)
        {
            return null;
        }

        if (DetailOf(slots) is not { } detail)
        {
            return null;
        }

        return ofIntent.FirstOrDefault(row => Names(row, detail, nouns))?.Id;
    }

    // Whether the row's phrase or one of its variants names the word (a place or an item), as a whole word, in its singular or its plural: the catalogue's forms of the noun, never a
    // made-up ending (V30).
    private static bool Names(PhraseRow row, string word, NounCatalog? nouns)
    {
        var forms = new List<string> { " " + Plain(word) + " " };
        var entry = nouns?.ForLemma(word) ?? nouns?.Nouns.FirstOrDefault(n => string.Equals(Plain(n.Plural), Plain(word), StringComparison.Ordinal));
        if (entry is not null)
        {
            forms.Add(" " + Plain(entry.Lemma) + " ");
            forms.Add(" " + Plain(entry.Plural) + " ");
        }

        return row.Texts.Any(text => forms.Any(form => (" " + Plain(text) + " ").Contains(form, StringComparison.Ordinal)));
    }

    /// <summary>The slot of a turn that matters to its phrase: the item asked for, else the place asked about, else <see langword="null"/>.</summary>
    /// <param name="slots">The slots the core filled.</param>
    public static string? DetailOf(IReadOnlyDictionary<string, string> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        return slots.TryGetValue("item", out var item) ? item : slots.TryGetValue("place", out var place) ? place : null;
    }

    /// <summary>
    /// The phrase the panel shows as <c>Esperaba</c> after a spoken turn: for a turn the core understood, the row of the intent that matched (at this
    /// prompt's rows first, then anywhere in the sheet); otherwise (or when the intent was help or "no sé", or the sheet has no such row) the row of
    /// this prompt that is closest to what was heard, the first one when nothing is close. <see langword="null"/> when the prompt has no row and no
    /// intent matched (the guide's open prompt for a question about crafting).
    /// </summary>
    /// <param name="prompt">The prompt the line was evaluated on.</param>
    /// <param name="intent">The intent the core resolved, or <c>unknown</c>.</param>
    /// <param name="communicated">Whether the NPC understood.</param>
    /// <param name="heard">What the recognizer heard.</param>
    public PhraseRow? Expected(string prompt, string intent, bool communicated, string heard)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(heard);
        var here = PhrasesAt(prompt);
        if (communicated)
        {
            var matched = here.FirstOrDefault(row => string.Equals(row.Intent, intent, StringComparison.Ordinal))
                ?? _rows.FirstOrDefault(row => row.IsLearnerPhrase && !IsService(row) && string.Equals(row.Intent, intent, StringComparison.Ordinal));
            if (matched is not null)
            {
                return matched;
            }
        }

        return here.Count == 0 ? null : Closest(here, heard);
    }

    /// <summary>
    /// The row a phrase offered as a choice is (it equals the row's Spanish or one of its variants, ignoring case, accents and punctuation), or
    /// <see langword="null"/> for a phrase that is no row.
    /// </summary>
    /// <param name="phrase">The offered phrase, for example <c>agua por favor</c>.</param>
    public PhraseRow? Match(string phrase)
    {
        ArgumentNullException.ThrowIfNull(phrase);
        var plain = Plain(phrase);
        return _rows.FirstOrDefault(row => row.IsLearnerPhrase && row.Texts.Any(text => string.Equals(Plain(text), plain, StringComparison.Ordinal)));
    }

    /// <summary>
    /// The row a learner's line is, among the phrases a test admits (village-plan V20: those of the lessons begun or done): the first in the sheet's order that the
    /// learner says, is no service line, and whose Spanish or a variant the line equals, ignoring case, accents and punctuation (<see cref="Plain"/>); or
    /// <see langword="null"/>.
    /// </summary>
    /// <param name="line">What the learner said.</param>
    /// <param name="admits">Whether a phrase (by its id) may be the one.</param>
    public PhraseRow? Said(string line, Func<string, bool> admits)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(admits);
        var plain = Plain(line);
        return plain.Length == 0
            ? null
            : _rows.FirstOrDefault(row => row.IsLearnerPhrase && !IsService(row) && admits(row.Id) && row.Texts.Any(text => string.Equals(Plain(text), plain, StringComparison.Ordinal)));
    }

    /// <summary>
    /// The buttons for the choices the repair ladder offered: each phrase becomes one option whose label is the sheet's phrase when it is a row
    /// (so <c>ayuda</c> and <c>ayuda por favor</c> are one button, <c>Ayuda.</c>) and the phrase itself, capitalized, when it is not. The option's
    /// line is the phrase the NPC offered, which is what the choice turn sends.
    /// </summary>
    /// <param name="phrases">The offered phrases, in the order the core gave them.</param>
    public IReadOnlyList<ChoiceOption> OptionsFor(IReadOnlyList<string> phrases)
    {
        ArgumentNullException.ThrowIfNull(phrases);
        var options = new List<ChoiceOption>();
        var labels = new HashSet<string>(StringComparer.Ordinal);
        foreach (var phrase in phrases)
        {
            var label = Match(phrase)?.Spanish ?? Capitalize(phrase);
            if (labels.Add(label))
            {
                options.Add(new ChoiceOption(label, phrase));
            }
        }

        return options;
    }

    /// <summary>The text without case, accents, punctuation or the inverted marks, words separated by single spaces: how a heard text meets a sheet text.</summary>
    /// <param name="text">Any text.</param>
    public static string Plain(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormD).ToLowerInvariant())
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // The prompts the NPC files say the NPCs wait on (by state, and the review) that no row names, each with the first such prompt, in the roster's order, that has
    // rows and accepts exactly the same answers: the same answer set and the same allowed intents, so the same phrases answer both. It was one pair in the code
    // (the guide's offer graded as the stall's); a lesson's prompt is left out, because its step names its own phrase.
    private static Dictionary<string, string> AnsweredAlike(IReadOnlyList<PhraseRow> rows, ContentBundle bundle, NpcRoster npcs)
    {
        var standing = npcs.All.SelectMany(static npc => npc.Prompts.Values.Append(npc.ReviewPrompt)).OfType<string>()
            .Distinct(StringComparer.Ordinal).Where(bundle.Prompts.ContainsKey).ToList();
        var withRows = standing.Where(prompt => rows.Any(row => IsAsked(row) && string.Equals(row.Prompt, prompt, StringComparison.Ordinal))).ToList();
        var answeredAs = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var prompt in standing.Except(withRows, StringComparer.Ordinal))
        {
            if (withRows.FirstOrDefault(other => AcceptTheSame(bundle.Prompts[prompt], bundle.Prompts[other])) is { } alike)
            {
                answeredAs[prompt] = alike;
            }
        }

        return answeredAs;
    }

    private static bool AcceptTheSame(PromptSpec a, PromptSpec b) =>
        string.Equals(a.AnswerSetId, b.AnswerSetId, StringComparison.Ordinal) && a.AllowedIntentIds.ToHashSet(StringComparer.Ordinal).SetEquals(b.AllowedIntentIds);

    // A row the prompt can be waiting for: a phrase the learner says, not a service word.
    private static bool IsAsked(PhraseRow row) => row.IsLearnerPhrase && !IsService(row);

    private static bool IsService(PhraseRow row) =>
        string.Equals(row.Intent, "ask_help", StringComparison.Ordinal) || string.Equals(row.Intent, "dont_know", StringComparison.Ordinal);

    // The row whose phrase or variants are most like what was heard (edit distance over the plain texts); the first row wins a tie.
    private static PhraseRow Closest(IReadOnlyList<PhraseRow> rows, string heard)
    {
        var plain = Plain(heard);
        var best = rows[0];
        var bestScore = -1.0;
        foreach (var row in rows)
        {
            foreach (var text in row.Texts)
            {
                var candidate = Plain(text);
                var length = Math.Max(Math.Max(plain.Length, candidate.Length), 1);
                var score = 1.0 - ((double)Distance(plain, candidate) / length);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = row;
                }
            }
        }

        return best;
    }

    private static int Distance(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    private static string Capitalize(string phrase) =>
        phrase.Length == 0 ? phrase : string.Create(CultureInfo.InvariantCulture, $"{char.ToUpperInvariant(phrase[0])}{phrase[1..]}.");

    private static string Required(LocaleText locale, string id, string field) =>
        locale.Find(LocaleId(id, field)) ?? throw new InvalidDataException($"{RelativePath}: the phrase '{id}' has no {field} in the locale {locale.Code} ({LocaleId(id, field)}).");

    private static string? Optional(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static List<string> Strings(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Select(static e => e.GetString()!).ToList()
            : [];
}

/// <summary>One button of the choices the repair ladder offered (S7): what the button says, and the line the choice sends to the NPC.</summary>
/// <param name="Label">The button's text, for example <c>Quiero agua, por favor.</c>.</param>
/// <param name="Line">The phrase the NPC offered (and the choice turn sends), for example <c>agua por favor</c>.</param>
public sealed record ChoiceOption(string Label, string Line);
