using System.Collections.Immutable;
using Vocaterra.Game.Core.Items;
using Vocaterra.Game.Core.Npc;
using Vocaterra.Nlp.Es.Normalization;

namespace Vocaterra.Game.Core.Language.Words;

/// <summary>Where the learner is with a catalogue noun.</summary>
public enum WordStage
{
    /// <summary>Not heard, read or said yet: the object is grey and the journal shows <c>¿?</c>.</summary>
    Unseen,

    /// <summary>Heard, read or said, but no correct use yet.</summary>
    Encountered,

    /// <summary>At least one correct use, fewer than three: the object has some colour.</summary>
    Learning,

    /// <summary>Three correct uses in three turns (D-game-6): the object is in full colour.</summary>
    Known,
}

/// <summary>How the typed, spoken or button line arrived. The credit of each kind is set in <see cref="CreditRules"/>.</summary>
public enum InputKind
{
    /// <summary>The player typed the line.</summary>
    Typed,

    /// <summary>The player spoke the line (P5).</summary>
    Spoken,

    /// <summary>A button sent the line: a choice the repair ladder offered, <c>Ayuda</c> or <c>No sé</c> (S7). A recognition signal: it earns no credit.</summary>
    Button,
}

/// <summary>
/// The credit a correct use earns, per kind of input, and the gaps between credited uses: <b>the one place</b> where both are set. Credit is
/// counted in units of a use: a full use is <see cref="WordProgress.UnitsPerUse"/> units, so a half-credit use is half of it. D-game-6: spoken
/// input earns full credit (three uses to know a word); from P5 typed input earns half by default (six uses), as a setting:
/// <see cref="Default"/> is half for typed input, <see cref="FullTyped"/> gives typed input full credit (<c>--typed-credit full</c>).
/// Spaced repetition (D-game-6 as amended): after the first credited use the next needs the first of <see cref="DefaultGapSeconds"/> in
/// stored time (10 minutes), after the second the next gap (1 hour); later credited uses keep the last gap. A different play session lifts
/// the gap.
/// </summary>
/// <param name="TypedUnits">The units a correct typed use earns.</param>
/// <param name="SpokenUnits">The units a correct spoken use earns.</param>
/// <param name="GapSeconds">The gaps in seconds of stored time, in order, or <see langword="null"/> for <see cref="DefaultGapSeconds"/>.</param>
public sealed record CreditRules(int TypedUnits, int SpokenUnits, IReadOnlyList<long>? GapSeconds = null)
{
    /// <summary>The gaps the owner set: 10 minutes after the first credited use, 1 hour after the second.</summary>
    public static IReadOnlyList<long> DefaultGapSeconds { get; } = [10 * 60, 60 * 60];

    /// <summary>The rules the game uses (D-game-6, from P5): half credit for typed input, full credit for spoken input.</summary>
    public static CreditRules Default { get; } = new(WordProgress.UnitsPerUse / 2, WordProgress.UnitsPerUse);

    /// <summary>The setting that gives typed input full credit as well (the launch option <c>--typed-credit full</c>): both kinds earn a full use.</summary>
    public static CreditRules FullTyped { get; } = new(WordProgress.UnitsPerUse, WordProgress.UnitsPerUse);

    /// <summary>The gaps in force, in seconds.</summary>
    public IReadOnlyList<long> Gaps => GapSeconds ?? DefaultGapSeconds;

    /// <summary>The units a correct use of the given kind earns.</summary>
    public int For(InputKind kind) => kind switch
    {
        InputKind.Typed => TypedUnits,
        InputKind.Spoken => SpokenUnits,
        _ => 0,
    };

    /// <summary>
    /// The seconds of stored time that must pass after the <paramref name="creditedUses"/>-th credited use before the next one earns credit:
    /// zero before any credited use, then the gaps in order, the last one repeating.
    /// </summary>
    public long GapAfter(int creditedUses)
    {
        if (creditedUses <= 0 || Gaps.Count == 0)
        {
            return 0;
        }

        return Gaps[Math.Min(creditedUses, Gaps.Count) - 1];
    }
}

/// <summary>Why a correct-looking use of a noun did or did not move its colour (see <see cref="WordCredit"/>).</summary>
public enum CreditOutcome
{
    /// <summary>The use earned credit: the colour moved.</summary>
    Credited,

    /// <summary>Correct, but before its gap: it counts in the journal, the colour does not move.</summary>
    TooEarly,

    /// <summary>The verdict found an error on the noun or its article.</summary>
    Wrong,

    /// <summary>The line was well written but the guide marked the name wrong: it is not the noun of the tile.</summary>
    NamedWrong,

    /// <summary>A naming with nothing to check it against (the cursor was on nothing): said, but no colour.</summary>
    NoTarget,
}

/// <summary>What one turn did to one noun.</summary>
/// <param name="Lemma">The noun.</param>
/// <param name="Outcome">Why the colour did or did not move.</param>
/// <param name="SecondsRemaining">For <see cref="CreditOutcome.TooEarly"/>, the stored seconds still to wait; otherwise zero.</param>
public sealed record WordCredit(string Lemma, CreditOutcome Outcome, long SecondsRemaining = 0);

/// <summary>A word that is due for review: started, not known, and its gap has passed (or the play session changed).</summary>
/// <param name="Lemma">The noun.</param>
/// <param name="OverdueSeconds">How long it has been due, in stored seconds (since the last credited use when only the session made it due).</param>
public sealed record DueWord(string Lemma, long OverdueSeconds);

/// <summary>
/// The counts of one catalogue noun. Saturation is the colour: <see cref="CreditUnits"/> over <see cref="WordProgress.UnitsToKnow"/>, so
/// it grows in thirds with full credit (0, 1/3, 2/3, 1) and the noun is known at 1.
/// </summary>
/// <param name="Lemma">The noun.</param>
/// <param name="Heard">Turns in which the guide's lines said it.</param>
/// <param name="Read">Times its label was shown to the learner in the journal.</param>
/// <param name="Spoken">Communicated turns in which the learner used it.</param>
/// <param name="SpokenCorrect">Of those, the ones with no verdict error on the word or its article.</param>
/// <param name="CreditUnits">The credit earned so far, in units of a use (see <see cref="CreditRules"/>).</param>
/// <param name="CorrectTurns">The turns that earned credit (at most one per turn): the credited uses.</param>
/// <param name="LastCreditedAt">The stored time (Unix seconds) of the last credited use, or 0 when there is none.</param>
/// <param name="LastCreditedSession">The play session of the last credited use, or 0 when there is none.</param>
/// <param name="Lapses">How often the guide revealed the word to the learner after a wrong name (each restarts the gap, no credit is lost).</param>
public sealed record WordRecord(
    string Lemma,
    int Heard = 0,
    int Read = 0,
    int Spoken = 0,
    int SpokenCorrect = 0,
    int CreditUnits = 0,
    int CorrectTurns = 0,
    long LastCreditedAt = 0,
    int LastCreditedSession = 0,
    int Lapses = 0)
{
    /// <summary>Whether the learner has used it correctly in three turns.</summary>
    public bool Known => CreditUnits >= WordProgress.UnitsToKnow;

    /// <summary>The credit in full uses: 1 for a spoken use, 0.5 for a typed use at half credit (D-game-6), at most <see cref="WordProgress.UsesToKnow"/>.</summary>
    public double Uses => Math.Min(WordProgress.UsesToKnow, CreditUnits / (double)WordProgress.UnitsPerUse);

    /// <summary>
    /// How many of the three progress pips are lit (0 to <see cref="WordProgress.UsesToKnow"/>): a started word shows at least one, and only a
    /// known word shows all three, so half-credit uses do not leave the pips at zero.
    /// </summary>
    public int Pips => Known ? WordProgress.UsesToKnow : Math.Min(WordProgress.UsesToKnow - 1, (CreditUnits + WordProgress.UnitsPerUse - 1) / WordProgress.UnitsPerUse);

    /// <summary>The colour of the noun's objects: 0 to 1, in sixths at half credit and thirds at full credit.</summary>
    public double Saturation => Math.Min(1.0, CreditUnits / (double)WordProgress.UnitsToKnow);

    /// <summary>Whether the learner has seen the word at all (heard, read, said).</summary>
    public bool Seen => Heard > 0 || Read > 0 || Spoken > 0;

    /// <summary>The stage the counts give.</summary>
    public WordStage Stage => Known ? WordStage.Known : CreditUnits > 0 ? WordStage.Learning : Seen ? WordStage.Encountered : WordStage.Unseen;
}

/// <summary>What one turn changed: the nouns that earned credit and the nouns that became known in it, both in ordinal order.</summary>
/// <param name="Credited">The catalogue nouns that earned credit this turn.</param>
/// <param name="NewlyKnown">The nouns that reached three uses this turn.</param>
/// <param name="Outcomes">What happened to every noun used in the turn, in ordinal order of the noun (empty when the turn was not understood).</param>
public sealed record WordTurnResult(IReadOnlyList<string> Credited, IReadOnlyList<string> NewlyKnown, IReadOnlyList<WordCredit> Outcomes)
{
    /// <summary>The nouns used correctly but too early: logged, no colour.</summary>
    public IReadOnlyList<WordCredit> TooEarly => Outcomes.Where(static o => o.Outcome == CreditOutcome.TooEarly).ToList();
}

/// <summary>
/// The learner's progress with the catalogue nouns (research section 6.2, D-game-6), fed from each guide turn. A noun is <i>known</i> after three
/// correct uses in three separate turns; each correct use adds credit, so its colour grows by a third; nothing decays. Only catalogue nouns are
/// counted. Plain values, ordinal keys, no clock and no random numbers: the same turns give the same counts.
/// </summary>
/// <remarks>
/// <para>
/// <b>Which words a turn uses.</b> The lemmas of a turn are the catalogue nouns that the core's resolver chose for the line's words
/// (<see cref="GuideTurn.Words"/>, CR-002) plus the catalogue nouns in its verdict's slot values. A word counts as correct when no verdict
/// error's span overlaps the word or the article before it. A turn that was not communicated counts nothing for use, only for hearing.
/// What the guide says is heard when a word of its line is a catalogue noun's singular or plural form.
/// </para>
/// <para>
/// <b>One credit per turn.</b> A lemma earns at most one credit in a turn, however often it is used in it.
/// </para>
/// <para>
/// <b>Spaced credit (D-game-6 as amended).</b> A correct use earns colour only when its gap has passed since the last credited use (see
/// <see cref="CreditRules.GapAfter"/>) or the play session (<see cref="Session"/>) differs from the one of that use. An early correct use is
/// counted (said, correct) but earns nothing; <see cref="WordTurnResult.Outcomes"/> says which. A <c>name_object</c> that the guide marked wrong
/// earns nothing for the noun named. A saved game restores the records and sets <see cref="Session"/> to a new number: a plain value change.
/// </para>
/// </remarks>
public sealed class WordProgress
{
    /// <summary>Credit units of one full use.</summary>
    public const int UnitsPerUse = 6;

    /// <summary>Correct uses in separate turns that make a noun known (D-game-6).</summary>
    public const int UsesToKnow = 3;

    /// <summary>Credit units that make a noun known.</summary>
    public const int UnitsToKnow = UnitsPerUse * UsesToKnow;

    private readonly NounCatalog _catalog;
    private readonly Dictionary<string, string> _formToLemma = new(StringComparer.Ordinal);
    private ImmutableSortedDictionary<string, WordRecord> _records = ImmutableSortedDictionary.Create<string, WordRecord>(StringComparer.Ordinal);

    /// <summary>Starts with no word seen.</summary>
    /// <param name="catalog">The noun catalogue whose nouns are counted.</param>
    /// <param name="session">The number of this play session; each credited use records it.</param>
    public WordProgress(NounCatalog catalog, int session = 1)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        Session = session;
        foreach (var noun in catalog.Nouns)
        {
            foreach (var form in new[] { noun.Lemma, noun.Plural })
            {
                foreach (var token in SpanishTextNormalizer.Normalize(form).Tokens)
                {
                    _formToLemma.TryAdd(token.Text, noun.Lemma);
                }
            }
        }
    }

    /// <summary>The number of the current play session (1 for a new game; the saves of P9 will bump it on each load).</summary>
    public int Session { get; set; }

    /// <summary>The catalogue the counts are kept for.</summary>
    public NounCatalog Catalogue => _catalog;

    /// <summary>The counts of every catalogue noun seen so far, ordinal by lemma. Nouns never seen have no record.</summary>
    public IReadOnlyDictionary<string, WordRecord> Records => _records;

    /// <summary>The lemmas known so far, ordinal.</summary>
    public IReadOnlyList<string> KnownLemmas => _records.Values.Where(static r => r.Known).Select(static r => r.Lemma).ToList();

    /// <summary>The record of a lemma, or a record with no counts when the learner has not met it.</summary>
    public WordRecord RecordOf(string lemma) => _records.TryGetValue(lemma, out var record) ? record : new WordRecord(lemma);

    /// <summary>The colour of a catalogue noun, 0 to 1.</summary>
    public double SaturationOf(string lemma) => RecordOf(lemma).Saturation;

    /// <summary>
    /// The colour of an item kind, 0 to 1: the saturation of the noun that names it (the inventory's labels and icons, P6). An item no noun
    /// names is in full colour (1).
    /// </summary>
    public double SaturationOf(ItemId item)
    {
        var noun = _catalog.ForItem(item);
        return noun is null ? 1.0 : SaturationOf(noun.Lemma);
    }

    /// <summary>
    /// Replaces the records with saved ones (S10). Every record must name a catalogue noun; a record for another lemma, or one given twice, throws.
    /// The session is not changed here (the save sets it).
    /// </summary>
    /// <param name="saved">The records a save holds.</param>
    /// <exception cref="ArgumentException">A record names no catalogue noun, or a lemma is given twice.</exception>
    public void Restore(IEnumerable<WordRecord> saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        var records = ImmutableSortedDictionary.CreateBuilder<string, WordRecord>(StringComparer.Ordinal);
        foreach (var record in saved)
        {
            if (_catalog.ForLemma(record.Lemma) is null)
            {
                throw new ArgumentException($"'{record.Lemma}' is not a catalogue noun.", nameof(saved));
            }

            if (records.ContainsKey(record.Lemma))
            {
                throw new ArgumentException($"'{record.Lemma}' is given twice.", nameof(saved));
            }

            records.Add(record.Lemma, record);
        }

        _records = records.ToImmutable();
    }

    /// <summary>Records that the learner has seen a noun's label for a while in the journal (the front-end calls it once per noun per opening).</summary>
    /// <param name="lemma">A catalogue noun; any other lemma is ignored.</param>
    public void MarkRead(string lemma)
    {
        if (_catalog.ForLemma(lemma) is null)
        {
            return;
        }

        Update(lemma, record => record with { Read = record.Read + 1 });
    }

    /// <summary>
    /// Records that the learner has heard a noun's word outside a guide turn, for example on its word card the first time it is picked up
    /// (counted as heard, never as a use).
    /// </summary>
    /// <param name="lemma">A catalogue noun; any other lemma is ignored.</param>
    public void MarkHeard(string lemma)
    {
        if (_catalog.ForLemma(lemma) is null)
        {
            return;
        }

        Update(lemma, record => record with { Heard = record.Heard + 1 });
    }

    /// <summary>
    /// Counts one guide turn: what the guide said (heard), and, when the learner was understood, the catalogue nouns used and whether each
    /// use was correct (spoken, spoken correctly, credit).
    /// </summary>
    /// <param name="turn">The turn, with its line, its verdict's slots and errors, and the guide's lines.</param>
    /// <param name="kind">How the line arrived; it selects the credit in <paramref name="rules"/>.</param>
    /// <param name="rules">The credit per kind of input and the gaps.</param>
    /// <param name="now">The stored time of the turn, in Unix seconds (the host's time, never the clock).</param>
    public WordTurnResult RecordTurn(GuideTurn turn, InputKind kind, CreditRules rules, long now)
    {
        ArgumentNullException.ThrowIfNull(turn);
        ArgumentNullException.ThrowIfNull(rules);

        // Heard: each guide line's words, counted once per turn per noun.
        var heard = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var line in turn.GuideLines)
        {
            foreach (var lemma in NounsIn(line))
            {
                heard.Add(lemma);
            }
        }

        foreach (var lemma in heard)
        {
            Update(lemma, record => record with { Heard = record.Heard + 1 });
        }

        var credited = new List<string>();
        var newlyKnown = new List<string>();
        var outcomes = new List<WordCredit>();
        if (!turn.Taken || kind == InputKind.Button)
        {
            // A line a button sent (a choice, Ayuda, No sé) is a recognition signal: the words it holds were heard and read, never said (D-game-10).
            return new WordTurnResult(credited, newlyKnown, outcomes);
        }

        var named = turn.Slots.TryGetValue("object", out var objectLemma) ? objectLemma : null;

        var words = turn.Words ?? [];
        var uses = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < words.Count; i++)
        {
            var noun = _catalog.ForReading(words[i].Lemma, words[i].Pos);
            if (noun is null)
            {
                continue;
            }

            if (!uses.ContainsKey(noun.Lemma))
            {
                uses.Add(noun.Lemma, []);
            }

            uses[noun.Lemma].Add(i);
        }

        foreach (var value in turn.Slots.Values)
        {
            if (_catalog.ForLemma(value) is not null && !uses.ContainsKey(value))
            {
                uses.Add(value, []); // a slot value with no token of its own: judged by the whole line below
            }
        }

        foreach (var (lemma, positions) in uses)
        {
            var correct = positions.Count == 0
                ? turn.Errors.Count == 0
                : positions.Any(i => !OverlapsError(words, i, turn.Errors));
            var namedWrong = turn.Naming == NameCheck.Wrong && lemma == named;
            var noTarget = turn.Naming == NameCheck.NoTarget && lemma == named;
            correct &= !namedWrong && !noTarget;
            var wasKnown = RecordOf(lemma).Known;
            var units = correct ? rules.For(kind) : 0;
            var remaining = correct && units > 0 ? SecondsUntilCredit(RecordOf(lemma), now, Session, rules) : 0;
            var earns = correct && units > 0 && remaining == 0;
            Update(lemma, record => record with
            {
                Spoken = record.Spoken + 1,
                SpokenCorrect = record.SpokenCorrect + (correct ? 1 : 0),
                CreditUnits = record.CreditUnits + (earns ? units : 0),
                CorrectTurns = record.CorrectTurns + (earns ? 1 : 0),
                LastCreditedAt = earns ? now : record.LastCreditedAt,
                LastCreditedSession = earns ? Session : record.LastCreditedSession,
            });
            if (earns)
            {
                credited.Add(lemma);
                outcomes.Add(new WordCredit(lemma, CreditOutcome.Credited));
            }
            else if (correct)
            {
                outcomes.Add(new WordCredit(lemma, CreditOutcome.TooEarly, remaining));
            }
            else
            {
                outcomes.Add(new WordCredit(lemma, namedWrong ? CreditOutcome.NamedWrong : noTarget ? CreditOutcome.NoTarget : CreditOutcome.Wrong));
            }

            if (!wasKnown && RecordOf(lemma).Known)
            {
                newlyKnown.Add(lemma);
            }
        }

        // Lapse: the guide revealed the tile's word because the player named something else. A started word that is not known has its gap
        // restarted from now, so copying her correction is too early; nothing is credited or taken away, and a word with no credited use is
        // untouched (its first use may come at any time).
        if (turn.Naming == NameCheck.Wrong && turn.NameTarget is { } revealed && revealed != named && _catalog.ForLemma(revealed) is not null)
        {
            var shown = RecordOf(revealed);
            if (shown.CorrectTurns > 0 && !shown.Known)
            {
                Update(revealed, record => record with { LastCreditedAt = now, LastCreditedSession = Session, Lapses = record.Lapses + 1 });
            }
        }

        return new WordTurnResult(credited, newlyKnown, outcomes);
    }

    /// <summary>
    /// The stored seconds a learner must still wait before a correct use of <paramref name="lemma"/> earns colour: <see langword="null"/> when
    /// the word has no credited use yet or is known (nothing to wait for), 0 when a use would earn colour now, otherwise the seconds left.
    /// </summary>
    public long? SecondsUntilNextCredit(string lemma, long now, CreditRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var record = RecordOf(lemma);
        return record.CorrectTurns == 0 || record.Known ? null : SecondsUntilCredit(record, now, Session, rules);
    }

    /// <summary>
    /// The words due for review at <paramref name="now"/>: started (at least one credited use), not known, with their gap passed or the play
    /// session changed. The most overdue comes first, then the lemma in ordinal order, so the choice is deterministic.
    /// </summary>
    public IReadOnlyList<DueWord> DueWords(long now, CreditRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var due = new List<DueWord>();
        foreach (var record in _records.Values)
        {
            if (record.CorrectTurns == 0 || record.Known || SecondsUntilCredit(record, now, Session, rules) > 0)
            {
                continue;
            }

            var dueAt = record.LastCreditedSession != Session ? record.LastCreditedAt : record.LastCreditedAt + rules.GapAfter(record.CorrectTurns);
            due.Add(new DueWord(record.Lemma, now - dueAt));
        }

        return due.OrderByDescending(static d => d.OverdueSeconds).ThenBy(static d => d.Lemma, StringComparer.Ordinal).ToList();
    }

    private static long SecondsUntilCredit(WordRecord record, long now, int session, CreditRules rules)
    {
        if (record.CorrectTurns == 0 || record.LastCreditedSession != session)
        {
            return 0;
        }

        return Math.Max(0, record.LastCreditedAt + rules.GapAfter(record.CorrectTurns) - now);
    }

    /// <summary>The catalogue nouns a line of the guide's speech names: a word whose form is a noun's singular or plural (ordinal, once each).</summary>
    private IEnumerable<string> NounsIn(string text)
    {
        var lemmas = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var token in SpanishTextNormalizer.Normalize(text).Tokens)
        {
            if (_formToLemma.TryGetValue(token.Text, out var lemma))
            {
                lemmas.Add(lemma);
            }
        }

        return lemmas;
    }

    // A word is wrong when an error's span overlaps the word, or the article right before it (research section 7.5).
    private bool OverlapsError(IReadOnlyList<GuideWord> words, int index, IReadOnlyList<GuideError> errors)
    {
        var start = words[index].Span.Start;
        var end = words[index].Span.End;
        if (index > 0 && _catalog.Articles.Contains(words[index - 1].Text))
        {
            start = words[index - 1].Span.Start;
        }

        foreach (var error in errors)
        {
            if (error.Span.Start < end && start < error.Span.End)
            {
                return true;
            }
        }

        return false;
    }

    private void Update(string lemma, Func<WordRecord, WordRecord> change)
    {
        var current = RecordOf(lemma);
        _records = _records.SetItem(lemma, change(current));
    }
}
