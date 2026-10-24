using System.Globalization;
using System.Text;
using System.Text.Json;
using Vocaterra.Game.Npc;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Language.Phrases;

/// <summary>
/// One row of the phrase sheet as the game reads it at run time (<c>game/phrases.json</c>, the machine-readable copy of the rows of
/// <c>content/PHRASES.md</c>, which stays the source of truth; a test holds the two together row by row).
/// </summary>
/// <param name="Id">The row's id (<c>pedir-agua</c>), the key the phrase tracking uses (<c>phrase.&lt;id&gt;</c>).</param>
/// <param name="Says">Who says the phrase: <c>says</c> (the learner), <c>hears</c> (an NPC) or <c>both</c>.</param>
/// <param name="Spanish">The phrase as taught, with its accents and inverted marks.</param>
/// <param name="English">The intended meaning.</param>
/// <param name="Intent">For a learner row, the bundle intent every variant resolves to; <see langword="null"/> for a <c>hears</c> row.</param>
/// <param name="Prompt">The prompt the phrase is said at (a <c>hears</c> row: the prompt whose text it is).</param>
/// <param name="Variants">Other things the game accepts as the same phrase.</param>
/// <param name="Card">The support card text, <c>phrase = meaning · word = gloss</c>.</param>
/// <param name="AudioLineIds">The bundle dialogue line ids whose NPC audio speaks the phrase (S9).</param>
public sealed record PhraseRow(
    string Id,
    string Says,
    string Spanish,
    string English,
    string? Intent,
    string? Prompt,
    IReadOnlyList<string> Variants,
    string Card,
    IReadOnlyList<string> AudioLineIds)
{
    /// <summary>Whether the learner says the phrase (<c>says</c> or <c>both</c>).</summary>
    public bool IsLearnerPhrase => string.Equals(Says, "says", StringComparison.Ordinal) || string.Equals(Says, "both", StringComparison.Ordinal);

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

    private PhraseSheet(List<PhraseRow> rows) => _rows = rows;

    /// <summary>The rows in the sheet's order.</summary>
    public IReadOnlyList<PhraseRow> Rows => _rows;

    /// <summary>Reads the sheet from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file is malformed.</exception>
    public static PhraseSheet Load(ContentBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        if (!bundle.TryReadHostFile(RelativePath, out var bytes))
        {
            throw new InvalidDataException($"The bundle has no host file {RelativePath}: it is missing from the manifest, or the bundle was loaded without hash verification.");
        }

        return Parse(Encoding.UTF8.GetString(bytes.Span));
    }

    /// <summary>Builds the sheet from the file's text.</summary>
    /// <param name="json">The contents of <c>game/phrases.json</c>.</param>
    /// <exception cref="InvalidDataException">The text is malformed.</exception>
    public static PhraseSheet Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
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
                var row = new PhraseRow(
                    element.GetProperty("id").GetString()!,
                    element.GetProperty("says").GetString()!,
                    element.GetProperty("spanish").GetString()!,
                    element.GetProperty("english").GetString()!,
                    Optional(element, "intent"),
                    Optional(element, "prompt"),
                    Strings(element, "variants"),
                    element.GetProperty("card").GetString()!,
                    Strings(element, "audioLineIds"));
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
    /// "no sé"), which are possible at every prompt and are never what the prompt is waiting for. The guide's offer is answered with the stall's
    /// offer rows (the sheet's <c>si-por-favor</c> and <c>no-gracias</c> work for both).
    /// </summary>
    /// <param name="prompt">The prompt id (<c>stall-need</c>).</param>
    public IReadOnlyList<PhraseRow> PhrasesAt(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        var group = GroupOf(prompt);
        return _rows.Where(row => row.IsLearnerPhrase && !IsService(row) && string.Equals(row.Prompt, group, StringComparison.Ordinal)).ToList();
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

    private static string GroupOf(string prompt) =>
        string.Equals(prompt, NpcService.GuideOfferPrompt, StringComparison.Ordinal) ? NpcService.StallOfferPrompt : prompt;

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
