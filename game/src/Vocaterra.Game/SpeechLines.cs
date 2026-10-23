using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Response.Speech;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Game;

/// <summary>The speakers of the pre-rendered NPC voices (S9): the renderer chooses each one's Kokoro speaker, the game never does.</summary>
public static class SpeechSpeakers
{
    /// <summary>The guide (<c>ef_dora</c>, speaker 28). Also the word cards and the phrase sheet's model answers.</summary>
    public const string Guide = "guide";

    /// <summary>The shopkeeper at the stall (<c>em_alex</c>, speaker 29).</summary>
    public const string Shopkeeper = "shopkeeper";

    /// <summary>The villager (<c>em_santa</c>, speaker 53).</summary>
    public const string Villager = "villager";

    /// <summary>The pool-id prefix of the villager's lesson lines (S26): they are named <c>guide.lesson.*</c> so the content lint counts them as the game's own pools.</summary>
    public const string VillagerLessonPrefix = "guide.lesson.villager.";

    /// <summary>The pool-id prefix of the shopkeeper's lesson lines (S26), named like the villager's.</summary>
    public const string ShopkeeperLessonPrefix = "guide.lesson.shop.";

    /// <summary>
    /// The speaker of a dialogue pool: the shopkeeper's pools are <c>shop_*</c> and <c>guide.lesson.shop.*</c>, the villager's <c>villager_*</c> and <c>guide.lesson.villager.*</c>, and every
    /// other pool is the guide's voice.
    /// </summary>
    /// <param name="poolId">The pool id.</param>
    public static string OfPool(string poolId)
    {
        ArgumentNullException.ThrowIfNull(poolId);
        if (poolId.StartsWith(VillagerLessonPrefix, StringComparison.Ordinal))
        {
            return Villager;
        }

        if (poolId.StartsWith(ShopkeeperLessonPrefix, StringComparison.Ordinal))
        {
            return Shopkeeper;
        }

        if (poolId.StartsWith("shop_", StringComparison.Ordinal))
        {
            return Shopkeeper;
        }

        return poolId.StartsWith("villager_", StringComparison.Ordinal) ? Villager : Guide;
    }

    /// <summary>The speaker of a prompt: its agent, or the guide when the prompt names none.</summary>
    /// <param name="agentId">The prompt's agent id.</param>
    public static string OfAgent(string? agentId) => string.IsNullOrEmpty(agentId) ? Guide : agentId;
}

/// <summary>What a speech line is: a dialogue variant, a prompt's text, a word card's noun, or a learner phrase spoken as its model answer.</summary>
public enum SpeechKind
{
    /// <summary>A slot-free variant of a dialogue pool (<c>dialogue/dialogue.json</c>).</summary>
    Dialogue,

    /// <summary>A prompt's text (<c>prompts/prompts.json</c>): the NPC's opening question or answer.</summary>
    Prompt,

    /// <summary>The noun of a word card, with its article (<c>la madera</c>).</summary>
    Word,

    /// <summary>A learner phrase of the phrase sheet, spoken as the model answer (its Spanish).</summary>
    Phrase,
}

/// <summary>
/// One line that has a pre-rendered voice in the bundle's <c>speech/</c> folder: the line id (the file name without <c>.wav</c>; the slow file
/// is <c>&lt;lineId&gt;.slow.wav</c>), the speaker, the Spanish text as the game shows it and the kind of line.
/// </summary>
/// <param name="LineId">The line id, a plain file name (letters, digits, <c>.</c>, <c>_</c>, <c>-</c>).</param>
/// <param name="Speaker">One of <see cref="SpeechSpeakers"/>.</param>
/// <param name="Text">The Spanish text.</param>
/// <param name="Kind">What the line is.</param>
public sealed record SpeechLine(string LineId, string Speaker, string Text, SpeechKind Kind);

/// <summary>
/// The speakable lines of the bundle (S9): every slot-free dialogue variant (the NPCs' pools, the feedback and the not-understood lines), every
/// prompt's text, one line per noun of a word card, and one per learner phrase of the phrase sheet. A template with a slot (the guide's world
/// lines, <c>{place}</c> and the like) is not speakable: it stays text.
/// </summary>
/// <remarks>
/// The list is a pure function of the bundle, sorted by line id in ordinal order, so the renderer, the startup count and the tests agree. Every
/// id must be a plain file name (<see cref="SpeechFiles.IsPlainId"/>), because the core's <see cref="StaticLineStore"/> reads only such names.
/// </remarks>
public static class SpeechLines
{
    /// <summary>The bundle folder that holds the pre-rendered WAV files.</summary>
    public const string FolderName = "speech";

    /// <summary>The bundle's speakable lines.</summary>
    /// <param name="bundle">The loaded content bundle.</param>
    /// <param name="nouns">The noun catalogue (the word cards).</param>
    /// <param name="phrases">The phrase sheet (the model answers).</param>
    /// <exception cref="InvalidOperationException">Two lines share an id, or an id is not a plain file name.</exception>
    public static IReadOnlyList<SpeechLine> Enumerate(ContentBundle bundle, NounCatalog nouns, PhraseSheet phrases)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(nouns);
        ArgumentNullException.ThrowIfNull(phrases);

        var lines = new List<SpeechLine>();
        foreach (var pool in bundle.Dialogue.Pools.Values)
        {
            foreach (var template in pool.Variants)
            {
                if (TemplateEngine.ReferencedSlots(template).Count == 0)
                {
                    lines.Add(new SpeechLine(template.LineId, SpeechSpeakers.OfPool(pool.PoolId), bundle.TemplateEngine.Render(template, SlotMap.Empty), SpeechKind.Dialogue));
                }
            }
        }

        foreach (var prompt in bundle.Prompts.Values)
        {
            if (!string.IsNullOrWhiteSpace(prompt.Text))
            {
                lines.Add(new SpeechLine("prompt." + prompt.PromptId, SpeechSpeakers.OfAgent(prompt.AgentId), prompt.Text, SpeechKind.Prompt));
            }
        }

        // Every catalogue noun has a card (S20b): the card shows on the first pickup of a noun that comes in an item, and the book's Nouns page plays
        // the card of any catalogue noun. The noun has the article the lexicon gives it.
        foreach (var noun in nouns.Nouns)
        {
            lines.Add(new SpeechLine(SpeechFiles.CardId(noun.Lemma), SpeechSpeakers.Guide, noun.WithDefinite, SpeechKind.Word));
        }

        foreach (var row in phrases.Rows)
        {
            if (row.IsLearnerPhrase)
            {
                lines.Add(new SpeechLine("phrase." + row.Id, SpeechSpeakers.Guide, row.Spanish, SpeechKind.Phrase));
            }
        }

        lines.Sort(static (a, b) => string.CompareOrdinal(a.LineId, b.LineId));
        for (var i = 0; i < lines.Count; i++)
        {
            if (!SpeechFiles.IsPlainId(lines[i].LineId))
            {
                throw new InvalidOperationException($"The speech line id '{lines[i].LineId}' is not a plain file name.");
            }

            if (i > 0 && string.Equals(lines[i - 1].LineId, lines[i].LineId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The speech line id '{lines[i].LineId}' is used twice.");
            }
        }

        return lines;
    }
}

/// <summary>The file names of the speech folder.</summary>
public static class SpeechFiles
{
    /// <summary>The suffix that marks the slow file: <c>&lt;lineId&gt;.slow</c> names <c>&lt;lineId&gt;.slow.wav</c>.</summary>
    public const string SlowSuffix = ".slow";

    /// <summary>Whether a line id is a plain file name the core's <see cref="StaticLineStore"/> reads (letters, digits, <c>.</c>, <c>_</c>, <c>-</c>).</summary>
    /// <param name="lineId">The line id.</param>
    public static bool IsPlainId(string lineId)
    {
        ArgumentNullException.ThrowIfNull(lineId);
        if (lineId.Length == 0)
        {
            return false;
        }

        foreach (var c in lineId)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The id of the slow file: <c>shop_need.1</c> gives <c>shop_need.1.slow</c>.</summary>
    /// <param name="lineId">The normal line id.</param>
    public static string SlowId(string lineId) => lineId + SlowSuffix;

    /// <summary>
    /// The id of a noun's word card (S20b): <c>card.madera</c>. The file name is plain ASCII, so the accents are folded: <c>río</c> gives <c>card.rio</c>
    /// (the card's text keeps its accent: <c>el río</c>).
    /// </summary>
    /// <param name="lemma">The noun's lemma.</param>
    public static string CardId(string lemma)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        var folded = new string(lemma.Normalize(System.Text.NormalizationForm.FormD).Where(static c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        return "card." + folded;
    }
}

/// <summary>
/// The startup line of a run: <c>speech: N lines, 2N files, M missing</c>, where M counts the normal and the slow files of the speakable lines that
/// are not in the speech folder. A missing file is not an error for the game (the line's text is shown), but the committed bundle must have none.
/// </summary>
/// <param name="Lines">The speakable lines (N).</param>
/// <param name="Files">The files they should have, two per line (2N).</param>
/// <param name="Missing">How many of those files are not in the folder (M).</param>
public sealed record SpeechSummary(int Lines, int Files, int Missing)
{
    /// <summary>The line the game prints at startup.</summary>
    public override string ToString() => $"speech: {Lines} lines, {Files} files, {Missing} missing";

    /// <summary>Counts the speakable lines' files in a folder.</summary>
    /// <param name="lines">The speakable lines.</param>
    /// <param name="speechDirectory">The bundle's speech folder (it need not exist: every file is then missing).</param>
    public static SpeechSummary Of(IEnumerable<SpeechLine> lines, string speechDirectory)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var store = new StaticLineStore(speechDirectory);
        var count = 0;
        var missing = 0;
        foreach (var line in lines)
        {
            count++;
            if (!store.Contains(line.LineId))
            {
                missing++;
            }

            if (!store.Contains(SpeechFiles.SlowId(line.LineId)))
            {
                missing++;
            }
        }

        return new SpeechSummary(count, 2 * count, missing);
    }
}

/// <summary>
/// Finds the speech line a piece of NPC text was rendered from, so that the front-end can play the NPC's voice for a line it shows (a turn's
/// lines are text). One text may be said by two speakers (<c>Hola.</c> by the guide and by the villager): the speaker wins, then the first line
/// by id.
/// </summary>
public sealed class SpeechIndex
{
    private readonly Dictionary<string, List<SpeechLine>> _byText = new(StringComparer.Ordinal);

    /// <summary>Indexes the lines.</summary>
    /// <param name="lines">The speakable lines, in any order.</param>
    public SpeechIndex(IEnumerable<SpeechLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        // A dialogue line before a prompt's text, so a shared text is played as the dialogue line (the same words, the same speaker).
        foreach (var line in lines.OrderBy(static l => l.Kind == SpeechKind.Dialogue ? 0 : 1).ThenBy(static l => l.LineId, StringComparer.Ordinal))
        {
            if (!_byText.TryGetValue(line.Text, out var same))
            {
                _byText[line.Text] = same = [];
            }

            same.Add(line);
        }
    }

    /// <summary>The line the text is spoken from by this speaker (or by any speaker when none says it), or <see langword="null"/> when no line has the text.</summary>
    /// <param name="speaker">The NPC that says the text.</param>
    /// <param name="text">The Spanish text as shown.</param>
    public SpeechLine? Find(string speaker, string text)
    {
        ArgumentNullException.ThrowIfNull(speaker);
        ArgumentNullException.ThrowIfNull(text);
        if (!_byText.TryGetValue(text, out var same))
        {
            return null;
        }

        return same.FirstOrDefault(line => string.Equals(line.Speaker, speaker, StringComparison.Ordinal)) ?? same[0];
    }
}
