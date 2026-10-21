using System.Collections.Immutable;
using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Language.Normalization;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Game;

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

/// <summary>How the typed or spoken line arrived. The credit of each kind is set in <see cref="CreditRules"/>.</summary>
public enum InputKind
{
    /// <summary>The player typed the line.</summary>
    Typed,

    /// <summary>The player spoke the line (P5).</summary>
    Spoken,
}

/// <summary>
/// The credit a correct use earns, per kind of input: <b>the one place</b> where the typed and spoken weights are set. Credit is counted in
/// units of a use: a full use is <see cref="WordProgress.UnitsPerUse"/> units, so a half-credit use is half of it. D-game-6: typed input earns
/// full credit; from P5 spoken input earns half by default, as a setting, which is the <see cref="SpokenUnits"/> value P5 changes.
/// </summary>
/// <param name="TypedUnits">The units a correct typed use earns.</param>
/// <param name="SpokenUnits">The units a correct spoken use earns (no spoken input exists before P5, so P4 gives it the full value).</param>
public sealed record CreditRules(int TypedUnits, int SpokenUnits)
{
    /// <summary>The rules the game uses: full credit for typed input (D-game-6), and full credit for spoken input until P5 sets it to half.</summary>
    public static CreditRules Default { get; } = new(WordProgress.UnitsPerUse, WordProgress.UnitsPerUse);

    /// <summary>The units a correct use of the given kind earns.</summary>
    public int For(InputKind kind) => kind == InputKind.Typed ? TypedUnits : SpokenUnits;
}

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
/// <param name="CorrectTurns">The turns that earned credit (at most one per turn).</param>
public sealed record WordRecord(
    string Lemma,
    int Heard = 0,
    int Read = 0,
    int Spoken = 0,
    int SpokenCorrect = 0,
    int CreditUnits = 0,
    int CorrectTurns = 0)
{
    /// <summary>Whether the learner has used it correctly in three turns.</summary>
    public bool Known => CreditUnits >= WordProgress.UnitsToKnow;

    /// <summary>The colour of the noun's objects: 0 to 1, in thirds for typed input.</summary>
    public double Saturation => Math.Min(1.0, CreditUnits / (double)WordProgress.UnitsToKnow);

    /// <summary>Whether the learner has seen the word at all (heard, read, said).</summary>
    public bool Seen => Heard > 0 || Read > 0 || Spoken > 0;

    /// <summary>The stage the counts give.</summary>
    public WordStage Stage => Known ? WordStage.Known : CreditUnits > 0 ? WordStage.Learning : Seen ? WordStage.Encountered : WordStage.Unseen;
}

/// <summary>What one turn changed: the nouns that earned credit and the nouns that became known in it, both in ordinal order.</summary>
/// <param name="Credited">The catalogue nouns that earned credit this turn.</param>
/// <param name="NewlyKnown">The nouns that reached three uses this turn.</param>
public sealed record WordTurnResult(IReadOnlyList<string> Credited, IReadOnlyList<string> NewlyKnown);

/// <summary>
/// The learner's progress with the catalogue nouns (research section 6.2, D-game-6), fed from each guide turn. A noun is <i>known</i> after three
/// correct uses in three separate turns; each correct use adds credit, so its colour grows by a third; nothing decays. Only catalogue nouns are
/// counted. Plain values, ordinal keys, no clock and no random numbers: the same turns give the same counts.
/// </summary>
/// <remarks>
/// <para>
/// <b>Which words a turn uses.</b> The lemmas of a turn are the catalogue nouns in its verdict's slot values plus the catalogue nouns that the
/// turn's normalized tokens can be (<see cref="NounCatalog.CatalogueNounsOf"/>, the CR-002 workaround). A token counts as correct when no verdict
/// error's span overlaps the token or the article before it. A turn that was not communicated counts nothing for use, only for hearing.
/// </para>
/// <para>
/// <b>One credit per turn.</b> A lemma earns at most one credit in a turn, however often it is used in it.
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

    private static readonly HashSet<string> Articles = new(StringComparer.Ordinal)
    {
        "el", "la", "los", "las", "un", "una", "unos", "unas",
    };

    private readonly NounCatalog _catalog;
    private ImmutableSortedDictionary<string, WordRecord> _records = ImmutableSortedDictionary.Create<string, WordRecord>(StringComparer.Ordinal);

    /// <summary>Starts with no word seen.</summary>
    /// <param name="catalog">The noun catalogue whose nouns are counted.</param>
    public WordProgress(NounCatalog catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

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
    /// The colour of a tile kind, 0 to 1: the saturation of the noun that names it. A colourless tile, or one with no noun, is always in full
    /// colour (1).
    /// </summary>
    public double SaturationOf(TileId tile)
    {
        var noun = _catalog.ForTile(tile);
        return noun is null ? 1.0 : SaturationOf(noun.Lemma);
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
    /// Counts one guide turn: what the guide said (heard), and, when the learner was understood, the catalogue nouns used and whether each
    /// use was correct (spoken, spoken correctly, credit).
    /// </summary>
    /// <param name="turn">The turn, with its line, its verdict's slots and errors, and the guide's lines.</param>
    /// <param name="kind">How the line arrived; it selects the credit in <paramref name="rules"/>.</param>
    /// <param name="rules">The credit per kind of input.</param>
    public WordTurnResult RecordTurn(GuideTurn turn, InputKind kind, CreditRules rules)
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
        if (!turn.Communicated)
        {
            return new WordTurnResult(credited, newlyKnown);
        }

        var normalized = TextNormalizer.Normalize(turn.Line);
        var tokens = normalized.Tokens;
        var uses = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < tokens.Count; i++)
        {
            foreach (var lemma in _catalog.CatalogueNounsOf(tokens[i].Text))
            {
                if (!uses.ContainsKey(lemma))
                {
                    uses.Add(lemma, []);
                }

                uses[lemma].Add(i);
            }
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
                : positions.Any(i => !OverlapsError(tokens, i, turn.Errors));
            var wasKnown = RecordOf(lemma).Known;
            var units = correct ? rules.For(kind) : 0;
            Update(lemma, record => record with
            {
                Spoken = record.Spoken + 1,
                SpokenCorrect = record.SpokenCorrect + (correct ? 1 : 0),
                CreditUnits = record.CreditUnits + units,
                CorrectTurns = record.CorrectTurns + (units > 0 ? 1 : 0),
            });
            if (units > 0)
            {
                credited.Add(lemma);
            }

            if (!wasKnown && RecordOf(lemma).Known)
            {
                newlyKnown.Add(lemma);
            }
        }

        return new WordTurnResult(credited, newlyKnown);
    }

    /// <summary>The catalogue nouns in a line of text, by its tokens' readings (ordinal, once each).</summary>
    private IEnumerable<string> NounsIn(string text)
    {
        var lemmas = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var token in TextNormalizer.Normalize(text).Tokens)
        {
            foreach (var lemma in _catalog.CatalogueNounsOf(token.Text))
            {
                lemmas.Add(lemma);
            }
        }

        return lemmas;
    }

    // A token is wrong when an error's span overlaps the token, or the article right before it (research section 7.5).
    private static bool OverlapsError(IReadOnlyList<Token> tokens, int index, IReadOnlyList<GuideError> errors)
    {
        var start = tokens[index].Span.Start;
        var end = tokens[index].Span.End;
        if (index > 0 && Articles.Contains(tokens[index - 1].Text))
        {
            start = tokens[index - 1].Span.Start;
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
