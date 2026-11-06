namespace Vocaterra.Game.Core.Activities;

/// <summary>How an instruction was presented to the learner when they acted on it (W06, design decision 19): the audio played, the text was shown, or both.</summary>
[Flags]
public enum InstructionPresentation
{
    /// <summary>Neither was presented (a scripted or silent step).</summary>
    None = 0,

    /// <summary>The audio of the instruction played.</summary>
    Heard = 1,

    /// <summary>The text of the instruction was shown.</summary>
    Read = 2,
}

/// <summary>
/// The one small policy the owner can change for decision 19 (accessible instruction evidence, open). The provisional value: the runner applies rung moves, folding and
/// mastery the same whichever way an instruction was presented, so a learner who reads (or who only hears) is not held back; the evidence keeps the two apart
/// (<see cref="ProcedureProgress.EvidenceOf"/>), so a later report can tell listening from reading.
/// </summary>
/// <param name="RequirePresentation">Whether an act on an instruction that was neither heard nor read earns no credit. <see langword="false"/> by default.</param>
public sealed record InstructionEvidencePolicy(bool RequirePresentation = false)
{
    /// <summary>The provisional policy of D-game-23, decision 19.</summary>
    public static InstructionEvidencePolicy Default { get; } = new();

    /// <summary>Whether an act on an instruction presented this way may earn credit and move rungs.</summary>
    /// <param name="presented">How the instruction was presented.</param>
    public bool Credits(InstructionPresentation presented) => !RequirePresentation || presented != InstructionPresentation.None;
}

/// <summary>What a procedure has taught one instruction row by acts: how many right acts were made on it when it was heard and when it was read (a right act may count in both).</summary>
/// <param name="Row">The instruction row's id.</param>
/// <param name="HeardActs">Right acts made with the audio played.</param>
/// <param name="ReadActs">Right acts made with the text shown.</param>
public sealed record ActEvidence(string Row, int HeardActs, int ReadActs);

/// <summary>
/// The learner's record with one procedure (W06): how many runs were started and finished, the play sessions in which it was finished without help, the steps right
/// the first time over all runs, and whether it is mastered.
/// </summary>
/// <param name="Id">The procedure's id.</param>
/// <param name="Runs">Runs started (a resumed run is not counted again).</param>
/// <param name="Completed">Runs whose exchange was made.</param>
/// <param name="CleanSittings">The distinct play sessions in which a run was finished without help, ascending.</param>
/// <param name="FirstTimeSteps">The steps right at the first attempt, over all runs.</param>
/// <param name="Mastered">Whether the goal row reached the last rung and two sittings were finished without help.</param>
public sealed record ProcedureRecord(string Id, int Runs = 0, int Completed = 0, IReadOnlyList<int>? CleanSittings = null, int FirstTimeSteps = 0, bool Mastered = false)
{
    /// <summary>The sittings finished without help, never <see langword="null"/>.</summary>
    public IReadOnlyList<int> Sittings => CleanSittings ?? [];
}

/// <summary>
/// The learner's progress with the procedures (W06): one <see cref="ProcedureRecord"/> per procedure started, the procedures known, and the evidence of the acts
/// by how the instruction was presented. Plain values, no clock: the same acts give the same progress.
/// </summary>
public sealed class ProcedureProgress
{
    /// <summary>The sittings finished without help that mastery asks for.</summary>
    public const int SittingsToMaster = 2;

    private readonly Dictionary<string, ProcedureRecord> _records = new(StringComparer.Ordinal);
    private readonly List<string> _known = [];
    private readonly Dictionary<string, ActEvidence> _evidence = new(StringComparer.Ordinal);

    /// <summary>The procedures the learner has started, in the order they were started.</summary>
    public IReadOnlyList<string> Known => _known;

    /// <summary>Every record, ordered by id.</summary>
    public IReadOnlyList<ProcedureRecord> Records => _records.Values.OrderBy(static r => r.Id, StringComparer.Ordinal).ToList();

    /// <summary>The evidence of right acts per instruction row, ordered by row.</summary>
    public IReadOnlyList<ActEvidence> Evidence => _evidence.Values.OrderBy(static e => e.Row, StringComparer.Ordinal).ToList();

    /// <summary>Whether the progress holds nothing (a new game, or a save made before W06).</summary>
    public bool IsEmpty => _known.Count == 0 && _records.Count == 0 && _evidence.Count == 0;

    /// <summary>The record of a procedure; an empty one for a procedure never started.</summary>
    /// <param name="id">The procedure's id.</param>
    public ProcedureRecord RecordOf(string id) => _records.TryGetValue(id, out var record) ? record : new ProcedureRecord(id);

    /// <summary>The evidence of an instruction row; an empty one for a row never acted on.</summary>
    /// <param name="row">The row's id.</param>
    public ActEvidence EvidenceOf(string row) => _evidence.TryGetValue(row, out var evidence) ? evidence : new ActEvidence(row, 0, 0);

    /// <summary>Whether the procedure is mastered.</summary>
    /// <param name="id">The procedure's id.</param>
    public bool IsMastered(string id) => RecordOf(id).Mastered;

    /// <summary>Counts a run started and remembers the procedure as known.</summary>
    /// <param name="id">The procedure's id.</param>
    /// <returns>The number of runs started before this one (the bake that is drawn for).</returns>
    internal int NoteStarted(string id)
    {
        var record = RecordOf(id);
        _records[id] = record with { Runs = record.Runs + 1 };
        if (!_known.Contains(id, StringComparer.Ordinal))
        {
            _known.Add(id);
        }

        return record.Runs;
    }

    /// <summary>Counts a finished run: the steps right first time, and the sitting when it was finished without help.</summary>
    internal void NoteCompleted(string id, int firstTimeSteps, bool helped, int session)
    {
        var record = RecordOf(id);
        var sittings = record.Sittings.ToList();
        if (!helped && !sittings.Contains(session))
        {
            sittings.Add(session);
            sittings.Sort();
        }

        _records[id] = record with { Completed = record.Completed + 1, FirstTimeSteps = record.FirstTimeSteps + firstTimeSteps, CleanSittings = sittings };
    }

    /// <summary>Marks the procedure mastered.</summary>
    internal void NoteMastered(string id) => _records[id] = RecordOf(id) with { Mastered = true };

    /// <summary>Counts a right act on an instruction row by how the instruction was presented.</summary>
    internal void NoteAct(string row, InstructionPresentation presented)
    {
        var evidence = EvidenceOf(row);
        _evidence[row] = evidence with
        {
            HeardActs = evidence.HeardActs + (presented.HasFlag(InstructionPresentation.Heard) ? 1 : 0),
            ReadActs = evidence.ReadActs + (presented.HasFlag(InstructionPresentation.Read) ? 1 : 0),
        };
    }

    /// <summary>Replaces everything with saved values.</summary>
    internal void Restore(IEnumerable<string> known, IEnumerable<ProcedureRecord> records, IEnumerable<ActEvidence> evidence)
    {
        _known.Clear();
        _records.Clear();
        _evidence.Clear();
        _known.AddRange(known);
        foreach (var record in records)
        {
            _records[record.Id] = record;
        }

        foreach (var item in evidence)
        {
            _evidence[item.Row] = item;
        }
    }
}
