using System.Globalization;
using Vocaterra.Game.Core.Event;
using Vocaterra.Game.Core.Language.Words;
using Vocaterra.Game.Core.Npc;

namespace Vocaterra.Game.Core.Language.Phrases;

/// <summary>
/// How much help the learner is given with a phrase (S8, the brief's "scaffold then fade"; S16 adds the English rung, D-game-13). The ladder runs from
/// the most help to the least: English with the Spanish card and audio, then the Spanish card and audio (the English only on the help key), then only the
/// audio, then nothing. The help key (<c>Ayuda</c>) shows the English and the card at every rung, so nothing is ever gated behind language (rule 8).
/// </summary>
/// <remarks>
/// <b>The rules of the ladder</b> (<see cref="PhraseProgress"/> applies them; the tests pin them):
/// <list type="number">
/// <item>A phrase with a scaffold starts at <see cref="EnglishAndCard"/>; a phrase with none stays at <see cref="None"/>.</item>
/// <item>A correct, full-credit use at <see cref="EnglishAndCard"/> steps to <see cref="CardAndAudio"/> at once: the English rung needs no spacing gap (the
/// English is a crutch for meaning, not a memory to space out). That use is the phrase's first credited use, so the gap clock starts there.</item>
/// <item>A correct, credited use at <see cref="CardAndAudio"/> or <see cref="AudioOnly"/> steps down one rung once the spacing gap of D-game-6 has passed
/// (10 minutes after the first credited use, 1 hour after later ones, or a new play session); a correct use before its gap is logged and does not step.</item>
/// <item>A half-credit (typed) use needs two to make a step.</item>
/// <item>"No sé", Ayuda, a pick among offered choices and a fail-forward step the level up one rung (never above <see cref="EnglishAndCard"/>) and clear the
/// credit toward the next step.</item>
/// </list>
/// </remarks>
public enum SupportLevel
{
    /// <summary>The first rung: an English explanation under the Spanish card, which is shown beside the panel with its model audio.</summary>
    EnglishAndCard,

    /// <summary>The Spanish card (the phrase sheet's card text) is shown beside the panel and its model audio plays when it appears; the English is on the help key.</summary>
    CardAndAudio,

    /// <summary>No card; the model audio plays when the prompt appears and can be repeated (<c>Repetir</c>).</summary>
    AudioOnly,

    /// <summary>Nothing is shown until the player asks (<c>Ayuda</c> shows the card and the English again).</summary>
    None,
}

/// <summary>What each rung of the <see cref="SupportLevel"/> ladder shows, as plain questions the panel and the tests share.</summary>
public static class SupportLevels
{
    /// <summary>Whether the Spanish card is drawn at this rung without the help key.</summary>
    /// <param name="level">The rung.</param>
    public static bool ShowsCard(SupportLevel level) => level is SupportLevel.EnglishAndCard or SupportLevel.CardAndAudio;

    /// <summary>Whether the English explanation is drawn at this rung without the help key.</summary>
    /// <param name="level">The rung.</param>
    public static bool ShowsEnglish(SupportLevel level) => level == SupportLevel.EnglishAndCard;
}

/// <summary>The wire names of the support levels for the console and the session log.</summary>
public static class SupportLevelNames
{
    /// <summary>The wire name of a level: <c>card-and-audio</c>, <c>audio-only</c> or <c>none</c>.</summary>
    public static string Of(SupportLevel level) => level switch
    {
        SupportLevel.EnglishAndCard => "english-and-card",
        SupportLevel.CardAndAudio => "card-and-audio",
        SupportLevel.AudioOnly => "audio-only",
        SupportLevel.None => "none",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Unknown support level."),
    };
}

/// <summary>
/// The counts of one phrase of the phrase sheet (D-game-10). The production counts (<see cref="Supported"/>, <see cref="Unaided"/>,
/// <see cref="Transfer"/>) are in credit units of a use (<see cref="WordProgress.UnitsPerUse"/> for a spoken use, half for a typed one at the
/// default rules, D-game-6), so <see cref="SupportedUses"/> reads 1 or 0.5. The other counts are whole events.
/// </summary>
/// <param name="Id">The phrase sheet's id (<c>pedir-agua</c>).</param>
/// <param name="Level">The support the learner is given now.</param>
/// <param name="CardShown">How many times the phrase's support card appeared (the panel opened on its prompt at <see cref="SupportLevel.CardAndAudio"/>, or Ayuda showed it).</param>
/// <param name="Supported">Correct uses with the scaffold on offer (card or audio) or right after the model was shown, in credit units.</param>
/// <param name="Unaided">Correct uses with no card or audio on offer and no model just shown, in credit units.</param>
/// <param name="ChoiceOnly">Times it was picked among offered choices: a recognition signal, never production.</param>
/// <param name="DontKnow">Times the player said "no sé" or asked for help while the phrase was the one in play.</param>
/// <param name="Transfer">Correct uses with another NPC or in a changed situation after the objective, in credit units.</param>
/// <param name="FailForward">Times the exchange ended without the phrase and the game carried on anyway.</param>
/// <param name="Slips">Times it was used with a mistake (communicated, but not correct): logged, never counted as a success.</param>
/// <param name="Heard">For a phrase an NPC says: times the NPC's prompt asked it.</param>
/// <param name="Answered">For a phrase an NPC says: times the learner answered it correctly in an allowed way (comprehension evidence).</param>
/// <param name="StepUnits">The credit toward the next step down, in units (a step needs <see cref="WordProgress.UnitsPerUse"/>).</param>
/// <param name="CreditedUses">The correct uses that were past their gap (the ones that counted toward a step); the gap grows with it.</param>
/// <param name="LastCreditedAt">The stored time (Unix seconds) of the last credited use, or 0.</param>
/// <param name="LastCreditedSession">The play session of the last credited use, or 0.</param>
/// <param name="LastUseAt">The stored time (Unix seconds) of the last use of any kind, or 0.</param>
/// <param name="LastUseSession">The play session of the last use of any kind, or 0.</param>
public sealed record PhraseRecord(
    string Id,
    SupportLevel Level,
    int CardShown = 0,
    int Supported = 0,
    int Unaided = 0,
    int ChoiceOnly = 0,
    int DontKnow = 0,
    int Transfer = 0,
    int FailForward = 0,
    int Slips = 0,
    int Heard = 0,
    int Answered = 0,
    int StepUnits = 0,
    int CreditedUses = 0,
    long LastCreditedAt = 0,
    int LastCreditedSession = 0,
    long LastUseAt = 0,
    int LastUseSession = 0)
{
    /// <summary>Supported uses in whole uses (1 for a spoken use, 0.5 for a typed one at half credit).</summary>
    public double SupportedUses => Supported / (double)WordProgress.UnitsPerUse;

    /// <summary>Unaided uses in whole uses.</summary>
    public double UnaidedUses => Unaided / (double)WordProgress.UnitsPerUse;

    /// <summary>Transfer uses in whole uses.</summary>
    public double TransferUses => Transfer / (double)WordProgress.UnitsPerUse;

    /// <summary>Whether the phrase has been used at all (any kind) or its card shown.</summary>
    public bool Seen => CardShown > 0 || Supported > 0 || Unaided > 0 || ChoiceOnly > 0 || DontKnow > 0 || Transfer > 0 || FailForward > 0 || Slips > 0 || Heard > 0;
}

/// <summary>What one phrase use did to the support level of one phrase.</summary>
public enum PhraseStep
{
    /// <summary>Nothing about the level: the use was logged (a use with a slip, a card shown, a phrase with no scaffold).</summary>
    Logged,

    /// <summary>A correct use that counted toward the next step but did not complete it (a typed use at half credit).</summary>
    Credited,

    /// <summary>A correct use before its gap: logged, the level does not move.</summary>
    TooEarly,

    /// <summary>The level stepped down one (less help).</summary>
    SteppedDown,

    /// <summary>The level stepped up one (more help) after "no sé", a choice or a fail-forward.</summary>
    SteppedUp,
}

/// <summary>What one phrase use did to one phrase.</summary>
/// <param name="PhraseId">The phrase.</param>
/// <param name="Step">What happened to its level.</param>
/// <param name="Level">The level after the use.</param>
/// <param name="SecondsRemaining">For <see cref="PhraseStep.TooEarly"/>, the stored seconds still to wait.</param>
public sealed record PhraseUseResult(string PhraseId, PhraseStep Step, SupportLevel Level, long SecondsRemaining = 0);

/// <summary>A phrase that is due for the guide's review: it has a credited use, still has a scaffold to fade, and its gap has passed.</summary>
/// <param name="PhraseId">The phrase.</param>
/// <param name="OverdueSeconds">How long it has been due, in stored seconds.</param>
public sealed record DuePhrase(string PhraseId, long OverdueSeconds);

/// <summary>
/// The learner's progress with the phrases of the phrase sheet (S8, D-game-10): one <see cref="PhraseRecord"/> per sheet row, fed from the game's
/// <see cref="PhraseUse"/> events, with a <see cref="SupportLevel"/> that fades as the phrase is used correctly and comes back when the learner
/// does not know. Plain values: no clock (every time is the stored time of the event), no random numbers, ordinal keys; the same events give the
/// same progress.
/// </summary>
/// <remarks>
/// <para>
/// <b>The mapping from a <see cref="PhraseUse"/> to the counts</b> (<see cref="Apply"/>); the uses are exclusive, a use is counted in one place:
/// </para>
/// <list type="bullet">
/// <item><see cref="PhraseUseKind.Supported"/>: <see cref="PhraseRecord.Supported"/> grows by the credit of the input (spoken full, typed half, D-game-6).</item>
/// <item><see cref="PhraseUseKind.Unaided"/>: <see cref="PhraseRecord.Unaided"/> grows the same way.</item>
/// <item><see cref="PhraseUseKind.Transfer"/>: <see cref="PhraseRecord.Transfer"/> grows the same way.</item>
/// <item><see cref="PhraseUseKind.Choice"/>: <see cref="PhraseRecord.ChoiceOnly"/> grows by one; no production credit; the level steps up.</item>
/// <item><see cref="PhraseUseKind.DontKnow"/>: <see cref="PhraseRecord.DontKnow"/> grows by one on each phrase in play (<see cref="PhraseUse.InPlay"/>,
/// or the service phrase itself when none is), and the level of each steps up; the service phrase (<c>no-se</c>, <c>ayuda</c>) is also counted as
/// said, unaided, when it was typed or spoken (a button earns nothing).</item>
/// <item><see cref="PhraseUseKind.FailForward"/>: <see cref="PhraseRecord.FailForward"/> grows by one and the level steps up.</item>
/// <item>A communicated use that was not correct (<see cref="PhraseUse.Correct"/> false) is a slip: <see cref="PhraseRecord.Slips"/> grows, nothing else.</item>
/// </list>
/// <para>
/// <b>The support level.</b> A correct production (supported, unaided or transfer) is <i>credited</i> when the phrase has no credited use yet, or the
/// spacing gap of D-game-6 has passed since the last one (<see cref="CreditRules.GapAfter"/>: 10 minutes after the first, 1 hour after the second and
/// later), or the play session changed. A credited use adds its credit units toward the next step; a full use of <see cref="WordProgress.UnitsPerUse"/>
/// units steps the level down one (<see cref="SupportLevel.EnglishAndCard"/> to <see cref="SupportLevel.CardAndAudio"/> to <see cref="SupportLevel.AudioOnly"/> to
/// <see cref="SupportLevel.None"/>), so a spoken credited use steps down and a typed one at half credit needs two. An early correct use is logged and does not
/// step. <b>The English rung has no gap</b> (S16): a correct use at <see cref="SupportLevel.EnglishAndCard"/> counts whenever it comes, and is the first credited
/// use, so the first gap runs from it. "No sé", a choice and a fail-forward step the level up one (to <see cref="SupportLevel.EnglishAndCard"/> at the most) and
/// clear the credit toward the next step. Only the phrases that have a scaffold to show (<see cref="HasScaffold"/>) have a level to move; the others stay at
/// <see cref="SupportLevel.None"/>. The scaffolded phrases are the four taught at the stall and the river (the NPCs' focus, <see cref="NpcRoster.ScaffoldPhrases"/>) and, since S16, the
/// phrases the lessons of <c>game/lessons.json</c> teach (the <c>scaffolds</c> argument of the constructor).
/// </para>
/// </remarks>
public sealed class PhraseProgress
{
    private readonly PhraseSheet _sheet;
    private readonly Dictionary<string, int> _index = new(StringComparer.Ordinal);
    private readonly HashSet<string> _scaffolds = new(StringComparer.Ordinal);
    private PhraseRecord[] _records;

    /// <summary>Starts with nothing seen: each phrase with a scaffold at <see cref="SupportLevel.EnglishAndCard"/>, every other at <see cref="SupportLevel.None"/>.</summary>
    /// <param name="sheet">The phrase sheet whose rows are tracked (all of them, in the sheet's order).</param>
    /// <param name="session">The number of this play session.</param>
    /// <param name="scaffolds">The phrases with a scaffold to fade: the NPCs' focus phrases (<see cref="NpcRoster.ScaffoldPhrases"/>) and the phrases a lesson teaches; <see langword="null"/> for none.</param>
    public PhraseProgress(PhraseSheet sheet, int session = 1, IEnumerable<string>? scaffolds = null)
    {
        _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
        Session = session;
        foreach (var id in scaffolds ?? [])
        {
            _ = _scaffolds.Add(id);
        }

        _records = new PhraseRecord[sheet.Rows.Count];
        for (var i = 0; i < _records.Length; i++)
        {
            var id = sheet.Rows[i].Id;
            _records[i] = new PhraseRecord(id, StartLevel(id));
            _index.Add(id, i);
        }
    }

    /// <summary>Whether a phrase has a scaffold to fade: one of the four taught at the stall and the river, or one a lesson teaches.</summary>
    /// <param name="id">The sheet's id.</param>
    public bool HasScaffold(string id) => _scaffolds.Contains(id);

    /// <summary>The number of the current play session (1 for a new game; the saves of S10 bump it on each load).</summary>
    public int Session { get; set; }

    /// <summary>The phrase sheet the progress is kept for.</summary>
    public PhraseSheet Sheet => _sheet;

    /// <summary>Every phrase's record, in the sheet's order.</summary>
    public IReadOnlyList<PhraseRecord> Records => _records;

    /// <summary>The record of a phrase; an empty record for an id the sheet does not have.</summary>
    /// <param name="id">The sheet's id.</param>
    public PhraseRecord RecordOf(string id) => _index.TryGetValue(id, out var at) ? _records[at] : new PhraseRecord(id, SupportLevel.None);

    /// <summary>The support level of a phrase now (<see cref="SupportLevel.None"/> for an id the sheet does not have).</summary>
    /// <param name="id">The sheet's id.</param>
    public SupportLevel LevelOf(string id) => RecordOf(id).Level;

    /// <summary>
    /// Replaces the records with saved ones (S10): every record whose id is in the sheet is taken as it is; the others stay as they are.
    /// </summary>
    /// <param name="saved">The records a save holds.</param>
    public void Restore(IEnumerable<PhraseRecord> saved)
    {
        ArgumentNullException.ThrowIfNull(saved);
        foreach (var record in saved)
        {
            if (_index.TryGetValue(record.Id, out var at))
            {
                _records[at] = record;
            }
        }
    }

    /// <summary>Records that a phrase's support card appeared (the panel opened on its prompt at <see cref="SupportLevel.CardAndAudio"/>, or Ayuda showed it again).</summary>
    /// <param name="id">The phrase.</param>
    public void NoteCardShown(string id) => Update(id, record => record with { CardShown = record.CardShown + 1 });

    /// <summary>Records that an NPC's prompt asked a phrase the learner hears (a row the learner does not say).</summary>
    /// <param name="id">The phrase.</param>
    public void NoteHeard(string id) => Update(id, record => record with { Heard = record.Heard + 1 });

    /// <summary>Records that the learner answered a phrase they hear correctly and in an allowed way.</summary>
    /// <param name="id">The phrase.</param>
    public void NoteAnswered(string id) => Update(id, record => record with { Answered = record.Answered + 1 });

    /// <summary>
    /// Counts one phrase use and moves the support levels it moves (the mapping is in the type's remarks). Unknown phrase ids are ignored.
    /// </summary>
    /// <param name="use">The phrase use, from the game's events.</param>
    /// <param name="rules">The credit per kind of input and the spacing gaps.</param>
    /// <returns>What it did to each phrase it touched, in the order touched.</returns>
    public IReadOnlyList<PhraseUseResult> Apply(PhraseUse use, CreditRules rules)
    {
        ArgumentNullException.ThrowIfNull(use);
        ArgumentNullException.ThrowIfNull(rules);
        var results = new List<PhraseUseResult>();
        if (!_index.ContainsKey(use.PhraseId))
        {
            return results;
        }

        switch (use.Kind)
        {
            case PhraseUseKind.Supported:
            case PhraseUseKind.Unaided:
            case PhraseUseKind.Transfer:
                results.Add(Produce(use, rules));
                break;
            case PhraseUseKind.Choice:
                Touch(use.PhraseId, use);
                Update(use.PhraseId, record => record with { ChoiceOnly = record.ChoiceOnly + 1 });
                results.Add(StepUp(use.PhraseId));
                break;
            case PhraseUseKind.FailForward:
                Touch(use.PhraseId, use);
                Update(use.PhraseId, record => record with { FailForward = record.FailForward + 1 });
                results.Add(StepUp(use.PhraseId));
                break;
            default: // DontKnow
                results.AddRange(NotKnown(use, rules));
                break;
        }

        return results;
    }

    /// <summary>The stored seconds a correct use of a phrase must still wait before it counts toward a step (0: now; <see langword="null"/> when it has no credited use yet).</summary>
    /// <param name="id">The phrase.</param>
    /// <param name="now">The stored time in Unix seconds.</param>
    /// <param name="rules">The credit rules (the gaps).</param>
    public long? SecondsUntilNextCredit(string id, long now, CreditRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var record = RecordOf(id);
        return record.CreditedUses == 0 ? null : record.Level == SupportLevel.EnglishAndCard ? 0 : SecondsUntilCredit(record, now, Session, rules);
    }

    /// <summary>
    /// The phrases due for the guide's review at <paramref name="now"/>: a phrase with a scaffold to fade (<see cref="HasScaffold"/>), a
    /// credited use, a level above <see cref="SupportLevel.None"/> (the help is not yet gone) and its gap passed or the play session changed. The most
    /// overdue comes first, then the sheet's order.
    /// </summary>
    /// <param name="now">The stored time in Unix seconds.</param>
    /// <param name="rules">The credit rules (the gaps).</param>
    public IReadOnlyList<DuePhrase> DuePhrases(long now, CreditRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var due = new List<(DuePhrase Phrase, int Order)>();
        for (var i = 0; i < _records.Length; i++)
        {
            var record = _records[i];
            if (record.CreditedUses == 0 || record.Level == SupportLevel.None || !HasScaffold(record.Id) || SecondsUntilCredit(record, now, Session, rules) > 0)
            {
                continue;
            }

            var dueAt = record.LastCreditedSession != Session ? record.LastCreditedAt : record.LastCreditedAt + rules.GapAfter(record.CreditedUses);
            due.Add((new DuePhrase(record.Id, now - dueAt), i));
        }

        return due.OrderByDescending(static d => d.Phrase.OverdueSeconds).ThenBy(static d => d.Order).Select(static d => d.Phrase).ToList();
    }

    /// <summary>
    /// The console line for a phrase after a use, for example <c>phrase: pedir-agua, supported 1, unaided 0, choice 0, no sé 0, transfer 0,
    /// fail-forward 0, cards 1, support audio-only</c>. Counts in whole uses (half a use for a typed one).
    /// </summary>
    /// <param name="id">The phrase.</param>
    public string ConsoleLine(string id)
    {
        var r = RecordOf(id);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"phrase: {r.Id}, supported {Uses(r.Supported)}, unaided {Uses(r.Unaided)}, choice {r.ChoiceOnly}, no sé {r.DontKnow}, transfer {Uses(r.Transfer)}, fail-forward {r.FailForward}, cards {r.CardShown}, support {SupportLevelNames.Of(r.Level)}");
    }

    /// <summary>The summary printed at exit: one line per phrase the learner said or saw, in the sheet's order (the same fields as <see cref="ConsoleLine"/>, plus slips and the last use; a phrase an NPC says shows heard and answered).</summary>
    public IReadOnlyList<string> SummaryLines()
    {
        var lines = new List<string>();
        foreach (var r in _records)
        {
            if (!r.Seen)
            {
                continue;
            }

            if (_sheet.Find(r.Id) is { IsLearnerPhrase: false })
            {
                lines.Add(string.Create(CultureInfo.InvariantCulture, $"  {r.Id}: heard {r.Heard}, answered {r.Answered} (a phrase the NPC says)"));
                continue;
            }

            var last = r.LastUseAt == 0 ? "never" : string.Create(CultureInfo.InvariantCulture, $"t={r.LastUseAt} s{r.LastUseSession}");
            lines.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"  {r.Id}: supported {Uses(r.Supported)}, unaided {Uses(r.Unaided)}, choice {r.ChoiceOnly}, no sé {r.DontKnow}, transfer {Uses(r.Transfer)}, fail-forward {r.FailForward}, slips {r.Slips}, cards {r.CardShown}, support {SupportLevelNames.Of(r.Level)}, last {last}"));
        }

        return lines;
    }

    /// <summary>A count in credit units as whole uses for display: <c>1</c>, <c>0.5</c>, <c>1.5</c>.</summary>
    /// <param name="units">Credit units (<see cref="WordProgress.UnitsPerUse"/> per full use).</param>
    public static string Uses(int units) => (units / (double)WordProgress.UnitsPerUse).ToString("0.#", CultureInfo.InvariantCulture);

    private SupportLevel StartLevel(string id) => HasScaffold(id) ? SupportLevel.EnglishAndCard : SupportLevel.None;

    // A correct (or slipped) production of a phrase: supported, unaided or transfer.
    private PhraseUseResult Produce(PhraseUse use, CreditRules rules)
    {
        var id = use.PhraseId;
        Touch(id, use);
        if (!use.Correct)
        {
            Update(id, record => record with { Slips = record.Slips + 1 });
            return new PhraseUseResult(id, PhraseStep.Logged, LevelOf(id));
        }

        var units = rules.For(use.Input);
        Update(id, record => use.Kind switch
        {
            PhraseUseKind.Supported => record with { Supported = record.Supported + units },
            PhraseUseKind.Unaided => record with { Unaided = record.Unaided + units },
            _ => record with { Transfer = record.Transfer + units },
        });
        if (units <= 0)
        {
            return new PhraseUseResult(id, PhraseStep.Logged, LevelOf(id));
        }

        var before = RecordOf(id);
        // The English rung has no spacing gap (S16): the English is help with the meaning, not a memory to space out.
        var remaining = before.Level == SupportLevel.EnglishAndCard ? 0 : SecondsUntilCredit(before, use.Time, Session, rules);
        if (remaining > 0)
        {
            return new PhraseUseResult(id, PhraseStep.TooEarly, before.Level, remaining);
        }

        var credit = before.StepUnits + units;
        var steps = HasScaffold(id) && before.Level != SupportLevel.None && credit >= WordProgress.UnitsPerUse;
        var level = steps ? before.Level + 1 : before.Level;
        Update(id, record => record with
        {
            Level = level,
            StepUnits = steps ? 0 : Math.Min(credit, WordProgress.UnitsPerUse),
            CreditedUses = record.CreditedUses + 1,
            LastCreditedAt = use.Time,
            LastCreditedSession = Session,
        });
        return new PhraseUseResult(id, steps ? PhraseStep.SteppedDown : PhraseStep.Credited, level);
    }

    // "No sé" or a request for help: the phrases in play did not come; the service phrase itself, if typed or said, is a use of that phrase.
    private IEnumerable<PhraseUseResult> NotKnown(PhraseUse use, CreditRules rules)
    {
        var results = new List<PhraseUseResult>();
        Touch(use.PhraseId, use);
        var units = rules.For(use.Input);
        if (units > 0)
        {
            Update(use.PhraseId, record => record with { Unaided = record.Unaided + units });
        }

        var inPlay = use.InPlay is { Count: > 0 } ? use.InPlay.Where(_index.ContainsKey).ToList() : [];
        if (inPlay.Count == 0)
        {
            inPlay = [use.PhraseId];
        }

        foreach (var id in inPlay)
        {
            Touch(id, use);
            Update(id, record => record with { DontKnow = record.DontKnow + 1 });
            results.Add(StepUp(id));
        }

        return results;
    }

    private PhraseUseResult StepUp(string id)
    {
        var before = RecordOf(id);
        if (!HasScaffold(id))
        {
            return new PhraseUseResult(id, PhraseStep.Logged, before.Level);
        }

        // The credit toward the next step down is lost either way: the learner needs the help again. The English rung is the top of the ladder.
        var level = before.Level == SupportLevel.EnglishAndCard ? before.Level : before.Level - 1;
        Update(id, record => record with { Level = level, StepUnits = 0 });
        return new PhraseUseResult(id, level == before.Level ? PhraseStep.Logged : PhraseStep.SteppedUp, level);
    }

    private void Touch(string id, PhraseUse use) =>
        Update(id, record => record with { LastUseAt = use.Time, LastUseSession = Session });

    private static long SecondsUntilCredit(PhraseRecord record, long now, int session, CreditRules rules)
    {
        if (record.CreditedUses == 0 || record.LastCreditedSession != session)
        {
            return 0;
        }

        return Math.Max(0, record.LastCreditedAt + rules.GapAfter(record.CreditedUses) - now);
    }

    private void Update(string id, Func<PhraseRecord, PhraseRecord> change)
    {
        if (_index.TryGetValue(id, out var at))
        {
            _records[at] = change(_records[at]);
        }
    }
}
