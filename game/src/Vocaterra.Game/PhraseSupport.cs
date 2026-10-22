using Vocaterra.Nlp.Content;

namespace Vocaterra.Game;

/// <summary>
/// The support card of a phrase (S8): the phrase sheet's card text broken into the phrase, its meaning and the word glosses, ready to draw. The
/// card is <c>phrase = meaning · word = gloss · word = gloss</c> in <c>content/PHRASES.md</c>.
/// </summary>
/// <param name="PhraseId">The phrase sheet's id.</param>
/// <param name="Phrase">The Spanish phrase (<c>Quiero agua, por favor.</c>).</param>
/// <param name="Meaning">The English meaning (<c>I want water, please.</c>).</param>
/// <param name="Glosses">The word glosses (<c>quiero = I want</c>), possibly none.</param>
/// <param name="AudioLineId">The bundle line id of the phrase's model audio (<c>shop_help_ask.1</c>), or <see langword="null"/>.</param>
public sealed record SupportCard(string PhraseId, string Phrase, string Meaning, IReadOnlyList<string> Glosses, string? AudioLineId)
{
    /// <summary>Reads a card from a phrase row.</summary>
    /// <param name="row">The sheet's row.</param>
    public static SupportCard From(PhraseRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        var parts = row.Card.Split(" · ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var head = parts.Length > 0 ? parts[0] : row.Card;
        var cut = head.IndexOf(" = ", StringComparison.Ordinal);
        var phrase = cut < 0 ? head : head[..cut];
        var meaning = cut < 0 ? row.English : head[(cut + 3)..];
        return new SupportCard(row.Id, phrase, meaning, parts.Skip(1).ToList(), row.AudioLineIds.Count > 0 ? row.AudioLineIds[0] : null);
    }
}

/// <summary>
/// A sound the front-end is to play (the audio hook of S8): the model audio of a phrase when its card appears or when its prompt appears at
/// <see cref="SupportLevel.AudioOnly"/>, and a key word of the stall's word cards. S9 makes the hook audible; until then the front-end's line player is silent.
/// </summary>
/// <param name="Npc">The NPC the sound belongs to (the one the panel is open on).</param>
/// <param name="Text">The Spanish text of the line (for a phrase, the text of its audio line in the bundle; for a key word, the card's phrase <c>el agua</c>).</param>
/// <param name="LineId">The bundle's dialogue line id, or <see langword="null"/> for a key word (S9 renders those by text).</param>
/// <param name="Reason">Why: <c>card</c>, <c>help</c>, <c>audio-only</c> or <c>key-word</c>.</param>
public sealed record AudioCue(string Npc, string Text, string? LineId, string Reason);

/// <summary>
/// The help on offer at the moment a line was submitted (S8): which phrases the prompt was asking for, and which of them had a card, audio or a model
/// just shown. The turn is applied later, possibly after the panel moved on, so the classification of the use reads this, not the panel.
/// </summary>
/// <param name="InPlay">The phrases the prompt (or the review) was asking for.</param>
/// <param name="Scaffolded">Those of them with help on offer.</param>
public sealed record SupportSnapshot(IReadOnlyList<string> InPlay, IReadOnlyList<string> Scaffolded);

/// <summary>What <see cref="PhraseSupport.Show"/> found new on the screen.</summary>
/// <param name="NewCards">The phrases whose support card appeared (each counts as a card shown, once per appearance).</param>
/// <param name="NewHears">The phrases the NPC's prompt asked (the rows the learner hears), once per appearance of the prompt.</param>
public sealed record SupportChange(IReadOnlyList<string> NewCards, IReadOnlyList<string> NewHears)
{
    /// <summary>No change.</summary>
    public static SupportChange None { get; } = new([], []);
}

/// <summary>
/// What the dialogue panel shows by way of support for the prompt it is on (S8, the fading scaffold): the support cards of the phrases at
/// <see cref="SupportLevel.CardAndAudio"/> (and those Ayuda or "no sé" just revealed), the audio-only state, and the sounds to play. It is plain
/// state with no engine type and no clock: the session calls <see cref="Show"/> when the panel opens on an NPC and after each turn, and the front-end
/// reads <see cref="Cards"/>, <see cref="AudioOnly"/> and <see cref="TakeCues"/>.
/// </summary>
/// <remarks>
/// At <see cref="SupportLevel.AudioOnly"/> there is no card, only the sound (and <c>Repetir</c> replays it); at <see cref="SupportLevel.None"/> nothing
/// is shown until the player asks for help. During the guide's phrase review no card is shown at all, whatever the level: the review is the test.
/// The <c>Esperaba</c> line of the panel (S7) is not part of this state: it appears only after a spoken turn, so it never gives the answer away before the turn.
/// </remarks>
public sealed class PhraseSupport
{
    private readonly PhraseSheet _sheet;
    private readonly PhraseProgress _progress;
    private readonly Dictionary<string, string> _lineText = new(StringComparer.Ordinal);
    private readonly HashSet<string> _revealed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _counted = new(StringComparer.Ordinal);
    private readonly HashSet<string> _helpCounted = new(StringComparer.Ordinal);
    private readonly HashSet<string> _cued = new(StringComparer.Ordinal);
    private readonly HashSet<string> _heard = new(StringComparer.Ordinal);
    private readonly Queue<AudioCue> _cues = new();
    private string? _npc;
    private string? _prompt;
    private string? _reviewPhrase;
    private bool _help;

    /// <summary>Creates the support state over the phrase sheet and the progress whose levels it follows.</summary>
    /// <param name="sheet">The phrase sheet.</param>
    /// <param name="progress">The phrase progress.</param>
    /// <param name="bundle">The bundle whose dialogue lines hold the model audio's text.</param>
    public PhraseSupport(PhraseSheet sheet, PhraseProgress progress, ContentBundle bundle)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(bundle);
        _sheet = sheet;
        _progress = progress;
        var wanted = new HashSet<string>(sheet.Rows.SelectMany(static r => r.AudioLineIds), StringComparer.Ordinal);
        foreach (var pool in bundle.Dialogue.Pools.Values)
        {
            foreach (var variant in pool.Variants)
            {
                if (wanted.Contains(variant.LineId))
                {
                    _lineText[variant.LineId] = variant.Text;
                }
            }
        }
    }

    /// <summary>The NPC the panel is open on, or <see langword="null"/> when it is closed.</summary>
    public string? Npc => _npc;

    /// <summary>The prompt the NPC is waiting on, or <see langword="null"/> when the panel is closed.</summary>
    public string? Prompt => _prompt;

    /// <summary>Whether help was given on this prompt (Ayuda or "no sé" and the NPC's model line), so the next use is supported, not unaided.</summary>
    public bool HelpGiven => _help;

    /// <summary>The phrases in play on this prompt: the review's phrase during a review, otherwise the prompt's focus (<see cref="PhraseIds.FocusAt"/>).</summary>
    public IReadOnlyList<string> InPlay => _npc is null ? [] : _reviewPhrase is not null ? [_reviewPhrase] : PhraseIds.FocusAt(_prompt ?? string.Empty);

    /// <summary>The support cards to draw now, in the phrase sheet's order.</summary>
    public IReadOnlyList<SupportCard> Cards => _npc is null
        ? []
        : InPlay.Where(id => CardUp(id)).Select(id => SupportCard.From(_sheet.Find(id)!)).ToList();

    /// <summary>The phrases in play at <see cref="SupportLevel.AudioOnly"/> (no card): the panel shows only the replay hook for them.</summary>
    public IReadOnlyList<string> AudioOnly => _npc is null || _reviewPhrase is not null
        ? []
        : InPlay.Where(id => _progress.LevelOf(id) == SupportLevel.AudioOnly && !_revealed.Contains(id)).ToList();

    /// <summary>The sound <c>Repetir</c> replays while a phrase's help is on offer (its card is up, or it is at audio-only), or <see langword="null"/> to replay the NPC's last line.</summary>
    public AudioCue? ReplayCue
    {
        get
        {
            if (_npc is null)
            {
                return null;
            }

            foreach (var id in InPlay)
            {
                if (CardUp(id) || (_reviewPhrase is null && _progress.LevelOf(id) == SupportLevel.AudioOnly))
                {
                    return CueFor(id, "replay");
                }
            }

            return null;
        }
    }

    /// <summary>The phrases in play and the help on offer for them now, to be carried with a submitted line.</summary>
    public SupportSnapshot Snapshot() => new(InPlay.ToList(), InPlay.Where(Scaffolded).ToList());

    /// <summary>Whether a phrase's card is up now.</summary>
    /// <param name="phraseId">The phrase.</param>
    public bool CardUp(string phraseId) =>
        _npc is not null && InPlay.Contains(phraseId, StringComparer.Ordinal) && (_revealed.Contains(phraseId) || (_reviewPhrase is null && _progress.LevelOf(phraseId) == SupportLevel.CardAndAudio));

    /// <summary>
    /// Whether the learner has help on offer for a phrase right now (a card, the audio, or a model just shown after Ayuda or "no sé"): a use then
    /// is a supported use, otherwise an unaided one.
    /// </summary>
    /// <param name="phraseId">The phrase.</param>
    public bool Scaffolded(string phraseId) =>
        _help || _revealed.Contains(phraseId) || (_reviewPhrase is null && _progress.LevelOf(phraseId) != SupportLevel.None);

    /// <summary>
    /// The panel is open on an NPC, or a turn moved its prompt on. Brings the cards and the sounds up to date with the levels. A change of NPC or
    /// prompt starts afresh (the earlier cards go down and the help given is forgotten); the same prompt keeps what is up, minus the cards whose
    /// phrase has since stepped down. Returns what appeared, so the session can count the cards shown.
    /// </summary>
    /// <param name="npc">The NPC the panel is open on.</param>
    /// <param name="prompt">The prompt the NPC is waiting on.</param>
    /// <param name="reviewPhrase">The phrase the guide's review asks (no card is shown for it), or <see langword="null"/>.</param>
    public SupportChange Show(string npc, string prompt, string? reviewPhrase = null)
    {
        ArgumentNullException.ThrowIfNull(npc);
        ArgumentNullException.ThrowIfNull(prompt);
        var fresh = !string.Equals(_npc, npc, StringComparison.Ordinal) || !string.Equals(_prompt, prompt, StringComparison.Ordinal) || !string.Equals(_reviewPhrase, reviewPhrase, StringComparison.Ordinal);
        if (fresh)
        {
            ResetPrompt();
        }

        _npc = npc;
        _prompt = prompt;
        _reviewPhrase = reviewPhrase;
        var newCards = new List<string>();
        foreach (var id in InPlay)
        {
            var level = _progress.LevelOf(id);
            if (_reviewPhrase is null && level == SupportLevel.CardAndAudio && _counted.Add(id))
            {
                newCards.Add(id);
                Cue(id, "card");
            }
            else if (fresh && _reviewPhrase is null && level == SupportLevel.AudioOnly && _cued.Add(id))
            {
                Cue(id, "audio-only"); // when the prompt appears, not each time a use steps the level down in front of the player
            }
        }

        var newHears = new List<string>();
        if (_reviewPhrase is null)
        {
            foreach (var row in _sheet.Rows)
            {
                if (string.Equals(row.Says, "hears", StringComparison.Ordinal) && string.Equals(row.Prompt, prompt, StringComparison.Ordinal) && _heard.Add(row.Id))
                {
                    newHears.Add(row.Id);
                }
            }
        }

        return new SupportChange(newCards, newHears);
    }

    /// <summary>
    /// Ayuda or "no sé" was pressed (or typed): the cards of the phrases in play are shown, whatever their level, and the next use counts as
    /// supported. Returns the phrases whose card newly appeared.
    /// </summary>
    public IReadOnlyList<string> Reveal()
    {
        if (_npc is null)
        {
            return [];
        }

        _help = true;
        var shown = new List<string>();
        foreach (var id in InPlay)
        {
            _revealed.Add(id);
            if (_counted.Add(id))
            {
                _helpCounted.Add(id);
                shown.Add(id);
                Cue(id, "help");
            }
        }

        return shown;
    }

    /// <summary>A phrase was used: the help given before it has been spent (the next use is judged by the levels again), and its revealed card goes down.</summary>
    public void SpendHelp()
    {
        _help = false;
        _revealed.Clear();
        _counted.ExceptWith(_helpCounted); // a later Ayuda shows the card again, and counts again
        _helpCounted.Clear();
    }

    /// <summary>Takes the panel down (the talk ended): no cards, no help given, no sounds waiting for an NPC that is no longer there.</summary>
    public void Clear()
    {
        ResetPrompt();
        _cues.Clear();
        _npc = null;
        _prompt = null;
        _reviewPhrase = null;
    }

    /// <summary>Takes the sounds waiting to be played, oldest first, and clears them.</summary>
    public IReadOnlyList<AudioCue> TakeCues()
    {
        var cues = _cues.ToArray();
        _cues.Clear();
        return cues;
    }

    /// <summary>Queues a sound for the front-end (the key words of the stall use it too).</summary>
    /// <param name="cue">The sound.</param>
    public void Enqueue(AudioCue cue)
    {
        ArgumentNullException.ThrowIfNull(cue);
        _cues.Enqueue(cue);
    }

    private void ResetPrompt()
    {
        _revealed.Clear();
        _counted.Clear();
        _helpCounted.Clear();
        _cued.Clear();
        _heard.Clear();
        _help = false;
    }

    private void Cue(string phraseId, string reason) => _cues.Enqueue(CueFor(phraseId, reason));

    private AudioCue CueFor(string phraseId, string reason)
    {
        var row = _sheet.Find(phraseId);
        var lineId = row is { AudioLineIds.Count: > 0 } ? row.AudioLineIds[0] : null;
        var text = lineId is not null && _lineText.TryGetValue(lineId, out var found) ? found : row?.Spanish ?? phraseId;
        return new AudioCue(_npc ?? string.Empty, text, lineId, reason);
    }
}
