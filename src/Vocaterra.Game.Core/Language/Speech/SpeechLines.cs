using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Nlp.Content;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Response.Speech;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Game.Core.Language.Speech;

/// <summary>
/// The speakers of the pre-rendered NPC voices (S9): a speaker is an NPC's id, and the renderer records each one with the voice of its NPC file (village-plan V01);
/// the game never chooses a Kokoro speaker.
/// </summary>
public static class SpeechSpeakers
{
    /// <summary>The guide (<c>ef_dora</c>, speaker 28). Also the word cards, the phrase sheet's model answers and every pool of no other NPC.</summary>
    public const string Guide = "guide";

    /// <summary>
    /// The speaker of a dialogue pool, from the NPC ids (village-plan V01): <c>&lt;id&gt;_*</c> and <c>guide.lesson.&lt;id&gt;.*</c> belong to the NPC <c>&lt;id&gt;</c>
    /// (or to the NPC whose file names that <c>poolPrefix</c>: the shopkeeper's pools are <c>shop_*</c> and <c>guide.lesson.shop.*</c>), and every other pool is the
    /// guide's voice.
    /// </summary>
    /// <param name="poolId">The pool id.</param>
    /// <param name="npcs">The NPCs.</param>
    public static string OfPool(string poolId, NpcRoster npcs)
    {
        ArgumentNullException.ThrowIfNull(poolId);
        ArgumentNullException.ThrowIfNull(npcs);
        return npcs.OwnerOfPool(poolId)?.Id ?? Guide;
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

    /// <summary>
    /// A line every NPC says (V21b: «¡Muy bien!», «No te entendí», the core's hints; #22: the unsure-recognition «No te entendí bien.»), recorded once more in the voice of
    /// each NPC: the line id is the shared line's, then <c>.&lt;npc&gt;</c> (<c>answer_accepted.1.baker</c>). The key words an NPC teaches are copies of their word cards
    /// (#36: <c>card.agua.shopkeeper</c>). The game plays the speaking NPC's file and the guide's when a file is missing.
    /// </summary>
    NpcCopy,
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
    /// <param name="npcs">The NPCs (the speaker of each dialogue pool, <see cref="SpeechSpeakers.OfPool"/>).</param>
    /// <exception cref="InvalidOperationException">Two lines share an id, or an id is not a plain file name.</exception>
    public static IReadOnlyList<SpeechLine> Enumerate(ContentBundle bundle, NounCatalog nouns, PhraseSheet phrases, NpcRoster npcs)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(nouns);
        ArgumentNullException.ThrowIfNull(phrases);
        ArgumentNullException.ThrowIfNull(npcs);

        var lines = new List<SpeechLine>();
        foreach (var pool in bundle.Dialogue.Pools.Values)
        {
            foreach (var template in pool.Variants)
            {
                if (TemplateEngine.ReferencedSlots(template).Count == 0)
                {
                    lines.Add(new SpeechLine(template.LineId, SpeechSpeakers.OfPool(pool.PoolId, npcs), bundle.TemplateEngine.Render(template, SlotMap.Empty), SpeechKind.Dialogue));
                }
            }
        }

        // V29: the template an NPC's slot reply says («Sí, hace {weather}.») is recorded once per value its file lists, as the line the slot makes: <lineId>.<value>.
        foreach (var npc in npcs.All)
        {
            foreach (var reply in npc.SlotReplies.Values)
            {
                if (NpcTurnRules.PoolSaidOn(bundle, npc.Id, reply.Event) is { } poolId && bundle.Dialogue.TryGetPool(poolId, out var replyPool))
                {
                    foreach (var template in replyPool.Variants)
                    {
                        foreach (var value in reply.Values)
                        {
                            var text = bundle.TemplateEngine.Render(template, SlotMap.From(new Dictionary<string, string>(StringComparer.Ordinal) { [reply.Slot] = value }));
                            lines.Add(new SpeechLine(SpeechFiles.ValueId(template.LineId, value), npc.Id, text, SpeechKind.Dialogue));
                        }
                    }
                }
            }
        }

        // V21b: the lines every NPC says are recorded in each NPC's own voice too (the guide's file stays the fallback).
        var shared = SharedPools(bundle);
        foreach (var pool in bundle.Dialogue.Pools.Values.Where(pool => shared.Contains(pool.PoolId) && npcs.OwnerOfPool(pool.PoolId) is null))
        {
            foreach (var template in pool.Variants.Where(static t => TemplateEngine.ReferencedSlots(t).Count == 0))
            {
                foreach (var npc in npcs.All.Where(static n => !string.Equals(n.Id, SpeechSpeakers.Guide, StringComparison.Ordinal)))
                {
                    lines.Add(new SpeechLine(template.LineId + "." + npc.Id, npc.Id, bundle.TemplateEngine.Render(template, SlotMap.Empty), SpeechKind.NpcCopy));
                }
            }
        }

        // #36: the key words an NPC teaches before its first question (the shopkeeper's «el agua», «el pan») are said by that NPC, so its voice records them too:
        // card.<lemma>.<npc> in its folder, an NPC copy of the word card (the guide's card file stays the fallback).
        foreach (var npc in npcs.All.Where(static n => !string.Equals(n.Id, SpeechSpeakers.Guide, StringComparison.Ordinal)))
        {
            foreach (var lemma in npc.KeyWords.Distinct(StringComparer.Ordinal))
            {
                if (nouns.ForLemma(lemma) is { } noun)
                {
                    lines.Add(new SpeechLine(SpeechFiles.CardId(noun.Lemma) + "." + npc.Id, npc.Id, noun.WithDefinite, SpeechKind.NpcCopy));
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

    // The pools more than one agent says (answer_accepted, not_understood), the core's feedback pools and the host's unsure-recognition pool: said by whoever speaks,
    // so the guide's voice would be wrong.
    private static HashSet<string> SharedPools(ContentBundle bundle)
    {
        var shared = bundle.Agents.Values
            .SelectMany(static machine => machine.Definition.VariantPools ?? [])
            .GroupBy(static pool => pool.Id, StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToHashSet(StringComparer.Ordinal);
        shared.UnionWith(bundle.Dialogue.Pools.Keys.Where(static id => id.StartsWith("feedback.", StringComparison.Ordinal)));
        if (bundle.Dialogue.Pools.ContainsKey(NpcService.UncertainPool))
        {
            shared.Add(NpcService.UncertainPool); // the host's «No te entendí bien.» when the recognizer was not sure: whoever the learner talks to says it (#22)
        }

        return shared;
    }
}

/// <summary>The file names of the speech folder.</summary>
public static class SpeechFiles
{
    /// <summary>The suffix that marks the slow file: <c>&lt;lineId&gt;.slow</c> names <c>&lt;lineId&gt;.slow.wav</c>.</summary>
    public const string SlowSuffix = ".slow";

    /// <summary>The id of a templated line said with one value of its slot (V29): <c>smith_weather.1</c> with <c>mucho frío</c> is <c>smith_weather.1.mucho-frio</c> (lower case, no accents, hyphens for spaces).</summary>
    /// <param name="lineId">The template's line id.</param>
    /// <param name="value">The slot's value.</param>
    public static string ValueId(string lineId, string value)
    {
        ArgumentNullException.ThrowIfNull(lineId);
        ArgumentNullException.ThrowIfNull(value);
        var bare = new string(value.Normalize(System.Text.NormalizationForm.FormD).Where(static c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        return lineId + "." + bare.ToLowerInvariant().Replace(' ', '-');
    }

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

    /// <summary>The subfolder of the speech folder that holds the word cards' files (V28: the other lines' files are in their speaker's).</summary>
    public const string CardsFolder = "cards";

    /// <summary>
    /// The subfolder of the speech folder a line's files are in (V28): the speaker's id (<c>baker</c>, <c>shopkeeper</c>), or <see cref="CardsFolder"/> for a word card (the guide's
    /// <c>card.*</c> files).
    /// </summary>
    /// <param name="line">The line.</param>
    public static string FolderOf(SpeechLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return line.Kind == SpeechKind.Word ? CardsFolder : line.Speaker;
    }

    /// <summary>The path of a line's file under the speech folder, with <c>/</c> (V28): <c>baker/baker_greet.1.slow.wav</c>.</summary>
    /// <param name="line">The line.</param>
    /// <param name="slow">Whether it is the slow file.</param>
    public static string RelativePath(SpeechLine line, bool slow) => FolderOf(line) + "/" + line.LineId + (slow ? SlowSuffix : string.Empty) + ".wav";

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
        var lineList = lines as IReadOnlyCollection<SpeechLine> ?? lines.ToList();
        lines = lineList;
        var store = new SpeechStore(speechDirectory, lineList);
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
    private readonly List<SpeechLine> _lines = [];

    /// <summary>Indexes the lines.</summary>
    /// <param name="lines">The speakable lines, in any order.</param>
    public SpeechIndex(IEnumerable<SpeechLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        _lines.AddRange(lines);
        // A dialogue line before a prompt's text, so a shared text is played as the dialogue line (the same words, the same speaker).
        foreach (var line in _lines.OrderBy(static l => l.Kind == SpeechKind.Dialogue ? 0 : 1).ThenBy(static l => l.LineId, StringComparer.Ordinal))
        {
            if (!_byText.TryGetValue(line.Text, out var same))
            {
                _byText[line.Text] = same = [];
            }

            same.Add(line);
        }
    }

    /// <summary>The lines the index was made of.</summary>
    public IReadOnlyList<SpeechLine> Lines => _lines;

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

/// <summary>
/// The pre-rendered files of the speech folder, found by line id through the folder each line is in (V28, <see cref="SpeechFiles.FolderOf"/>): one core
/// <see cref="StaticLineStore"/> per subfolder. The slow file of a line (<c>&lt;lineId&gt;.slow</c>) is in the folder of its line. An id of no line has no file.
/// </summary>
public sealed class SpeechStore
{
    private readonly Dictionary<string, StaticLineStore> _byLine = new(StringComparer.Ordinal);

    /// <summary>Creates the store over the bundle's speech folder.</summary>
    /// <param name="speechDirectory">The bundle's <c>speech/</c> folder (it need not exist: every file is then missing).</param>
    /// <param name="lines">The speakable lines.</param>
    public SpeechStore(string speechDirectory, IEnumerable<SpeechLine> lines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(speechDirectory);
        ArgumentNullException.ThrowIfNull(lines);
        var folders = new Dictionary<string, StaticLineStore>(StringComparer.Ordinal);
        foreach (var line in lines)
        {
            var folder = SpeechFiles.FolderOf(line);
            if (!folders.TryGetValue(folder, out var store))
            {
                folders[folder] = store = new StaticLineStore(Path.Combine(speechDirectory, folder));
            }

            _byLine[line.LineId] = store;
        }
    }

    /// <summary>Whether the folder has the file of a line id (a normal id or its slow one).</summary>
    /// <param name="id">The line id, or the slow file's id.</param>
    public bool Contains(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return StoreOf(id, out var store) && store.Contains(id);
    }

    /// <summary>Reads the audio of a line id (a normal id or its slow one).</summary>
    /// <param name="id">The line id, or the slow file's id.</param>
    /// <param name="buffer">The audio, when the file is there.</param>
    public bool TryGet(string id, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Vocaterra.Nlp.Core.Audio.AudioBuffer? buffer)
    {
        ArgumentNullException.ThrowIfNull(id);
        buffer = null;
        return StoreOf(id, out var store) && store.TryGet(id, out buffer);
    }

    private bool StoreOf(string id, out StaticLineStore store)
    {
        var line = id.EndsWith(SpeechFiles.SlowSuffix, StringComparison.Ordinal) ? id[..^SpeechFiles.SlowSuffix.Length] : id;
        return _byLine.TryGetValue(line, out store!);
    }
}
