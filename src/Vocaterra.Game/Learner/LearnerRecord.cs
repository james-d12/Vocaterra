namespace Vocaterra.Game;

/// <summary>
/// The learner's record (S19): the history of every line the game took (<see cref="History"/>, the newest 500 of it saved) and the word record folded from
/// it (<see cref="Rows"/>, <see cref="Nouns"/>, <see cref="Verbs"/>). The lesson record is the lesson runner's (<see cref="LessonRunner.Record"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The read API for the book (S20).</b> Every list is sorted deterministically, and nothing here reads the clock or a random number:
/// <see cref="Nouns"/> and <see cref="Verbs"/> are the word rows of that part of speech, ordered by lemma (ordinal); <see cref="Rows"/> is every row,
/// ordered by lemma then part of speech; <see cref="Recent"/> is the history newest first; <see cref="LessonRunner.Record"/> is in the bundle's lesson order.
/// </para>
/// <para>
/// <b>The save.</b> The word rows are kept whole in the save (a fold of the full history), while the save keeps only the newest
/// <see cref="SavedEntries"/> history entries. A loaded record is therefore the saved one, not a fold of the saved history, which would lose the older uses.
/// <see cref="NextId"/> is saved too, so the numbering goes on.
/// </para>
/// </remarks>
public sealed class LearnerRecord
{
    /// <summary>How many of the newest history entries the save keeps.</summary>
    public const int SavedEntries = 500;

    private readonly List<InputEntry> _history = [];
    private LearnerWords _words = new();

    /// <summary>The number the next entry takes (entries are numbered from 1 and never repeat).</summary>
    public long NextId { get; private set; } = 1;

    /// <summary>Every entry of this run and of the saves it was loaded from, oldest first (a loaded game holds the newest <see cref="SavedEntries"/> of its history).</summary>
    public IReadOnlyList<InputEntry> History => _history;

    /// <summary>Every word row, sorted by lemma then part of speech.</summary>
    public IReadOnlyList<WordRow> Rows => _words.Rows;

    /// <summary>The noun rows, sorted by lemma.</summary>
    public IReadOnlyList<WordRow> Nouns => Rows.Where(static row => row.Pos == "noun").ToList();

    /// <summary>The verb rows, sorted by infinitive.</summary>
    public IReadOnlyList<WordRow> Verbs => Rows.Where(static row => row.Pos == "verb").ToList();

    /// <summary>The newest history entries, newest first.</summary>
    /// <param name="count">How many at most.</param>
    public IReadOnlyList<InputEntry> Recent(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        return _history.AsEnumerable().Reverse().Take(count).ToList();
    }

    /// <summary>Adds an entry to the history and folds it into the word record. Its id must be <see cref="NextId"/>.</summary>
    /// <param name="entry">The entry.</param>
    /// <exception cref="ArgumentException">The entry's id is not the next one.</exception>
    public void Append(InputEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Id != NextId)
        {
            throw new ArgumentException($"The history entry {entry.Id} is not the next one ({NextId}).", nameof(entry));
        }

        _history.Add(entry);
        NextId++;
        _words.Apply(entry);
    }

    /// <summary>The save's part: the newest entries of the history, the word rows and the next number.</summary>
    internal LearnerRecordSave Capture()
    {
        var kept = _history.Skip(Math.Max(0, _history.Count - SavedEntries)).ToArray();
        return new LearnerRecordSave(NextId, kept, Rows);
    }

    /// <summary>Puts a saved record back (S10). The history's ids must be increasing and below the next id.</summary>
    /// <param name="save">The save's part.</param>
    /// <param name="checksCounted">Whether the save is a version 2 save, whose word rows counted the check answers (S19b); they are taken out on the load.</param>
    /// <exception cref="ArgumentException">The save's history or word rows are not a record this game writes.</exception>
    internal void Restore(LearnerRecordSave save, bool checksCounted)
    {
        ArgumentNullException.ThrowIfNull(save);
        if (save.NextId < 1)
        {
            throw new ArgumentException($"The learner's next entry is {save.NextId}; entries start at 1.", nameof(save));
        }

        long last = 0;
        foreach (var entry in save.History)
        {
            if (entry.Id <= last || entry.Id >= save.NextId)
            {
                throw new ArgumentException($"The history entry {entry.Id} is out of order or not below the next entry ({save.NextId}).", nameof(save));
            }

            last = entry.Id;
        }

        _history.Clear();
        _history.AddRange(save.History);
        NextId = save.NextId;
        _words = checksCounted
            ? LearnerWords.FromVersion2(save.Words, save.History, save.NextId)
            : LearnerWords.FromRows(save.Words);
    }
}
