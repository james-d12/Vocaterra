using Vocaterra.Game.Core.Language.Phrases;
using Vocaterra.Game.Core.Language.Speech;

namespace Vocaterra.Game.UI.Book;

/// <summary>
/// Which recording a row of the book plays (S20b). A noun plays its word card (<c>card.madera</c>); a phrase plays its own recording (<c>phrase.hola</c>)
/// when it has one, and otherwise the NPC line that already speaks it (the phrase sheet's <c>audioLineIds</c>, for example <c>shop_need.1</c> for
/// <c>¿Qué necesitas?</c>). The ids are those of the bundle's speech folder; the set of available ids is given, so the resolution is a pure function
/// and the tests can use the committed folder or a fixed set. Engine free.
/// </summary>
public sealed class BookRecordings
{
    private readonly IReadOnlySet<string> _available;
    private readonly PhraseSheet? _sheet;

    /// <summary>Creates the resolution over the recordings that exist.</summary>
    /// <param name="available">The line ids that have a recording in the speech folder (the normal file exists).</param>
    /// <param name="sheet">The phrase sheet, for a phrase's own id and its NPC lines; <see langword="null"/> resolves no phrase.</param>
    public BookRecordings(IEnumerable<string> available, PhraseSheet? sheet)
    {
        ArgumentNullException.ThrowIfNull(available);
        _available = available.ToHashSet(StringComparer.Ordinal);
        _sheet = sheet;
    }

    /// <summary>No recordings at all: no row of the book gets a button (the book without a speech folder, and the tests that do not need one).</summary>
    public static BookRecordings None { get; } = new(Array.Empty<string>(), null);

    /// <summary>How many recording ids are available.</summary>
    public int Count => _available.Count;

    /// <summary>Whether a line id has a recording.</summary>
    /// <param name="lineId">The line id.</param>
    public bool Has(string lineId) => _available.Contains(lineId);

    /// <summary>The recording of a catalogue noun: its word card, or <see langword="null"/> when the speech folder has none.</summary>
    /// <param name="lemma">The noun's lemma.</param>
    public string? ForNoun(string lemma)
    {
        ArgumentNullException.ThrowIfNull(lemma);
        var id = SpeechFiles.CardId(lemma);
        return Has(id) ? id : null;
    }

    /// <summary>
    /// The recording of a phrase of the sheet: its own <c>phrase.&lt;id&gt;</c> when there is one, else the first of its NPC lines (in the sheet's order) that
    /// has a recording, else <see langword="null"/>.
    /// </summary>
    /// <param name="phraseId">The phrase's id.</param>
    public string? ForPhrase(string phraseId)
    {
        ArgumentNullException.ThrowIfNull(phraseId);
        var row = _sheet?.Find(phraseId);
        if (row is null)
        {
            return null;
        }

        var own = "phrase." + row.Id;
        if (Has(own))
        {
            return own;
        }

        return row.AudioLineIds.FirstOrDefault(Has);
    }

    /// <summary>The recording a book row plays: a phrase's by its id (<see cref="ForPhrase"/>), a noun's by its lemma (<see cref="ForNoun"/>).</summary>
    /// <param name="target">A phrase id (<c>phrase:pedir-agua</c>) or a noun's lemma (<c>madera</c>), as the scripted run names them.</param>
    public string? ForTarget(string target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.StartsWith(PhraseTargetPrefix, StringComparison.Ordinal)
            ? ForPhrase(target[PhraseTargetPrefix.Length..])
            : ForNoun(target);
    }

    /// <summary>The prefix of a phrase target (<c>phrase:pedir-agua</c>).</summary>
    public const string PhraseTargetPrefix = "phrase:";
}
