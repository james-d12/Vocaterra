using Vocaterra.Game;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// The grey set of the 3D world, from the learner's progress (D-game-6 as amended, the playtest A/B of <c>--greyscale</c>). A block kind is grey
/// while every catalogue noun that names it (<see cref="NounCatalog.NounsOfBlock"/>) is unknown (<see cref="WordRecord.Known"/>): one known
/// name is enough (the water block is named by <c>agua</c> and <c>río</c>, S12). A block no noun names is always in colour, and so are the
/// figures, the sky and the UI.
/// </summary>
public static class WordColours
{
    /// <summary>The grey set for the catalogue and the progress: one bit per block kind (<see cref="ChunkMesher.KindBit"/>).</summary>
    public static ulong GreyKinds(NounCatalog nouns, WordProgress words)
    {
        ArgumentNullException.ThrowIfNull(nouns);
        ArgumentNullException.ThrowIfNull(words);
        var grey = 0UL;
        foreach (var block in Enum.GetValues<BlockId>())
        {
            if (block == BlockId.Air)
            {
                continue;
            }

            var named = nouns.NounsOfBlock(block);
            if (named.Count > 0 && named.All(noun => !words.RecordOf(noun.Lemma).Known))
            {
                grey |= ChunkMesher.KindBit(block);
            }
        }

        return grey;
    }

    /// <summary>
    /// Counts the given nouns as known from the start (the test aid <c>--assume-known</c>): each keeps its other counts and gets full credit. A
    /// noun already known keeps its record. Nothing is said or toasted for these nouns, since they are known before the first frame.
    /// </summary>
    /// <param name="words">The progress to change; the other records are kept.</param>
    /// <param name="lemmas">Catalogue nouns, in any order.</param>
    /// <returns>The lemmas that are not catalogue nouns (they are skipped).</returns>
    public static IReadOnlyList<string> AssumeKnown(WordProgress words, IReadOnlyList<string> lemmas)
    {
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(lemmas);
        var skipped = new List<string>();
        var named = new Dictionary<string, WordRecord>(StringComparer.Ordinal);
        foreach (var lemma in lemmas)
        {
            if (words.Catalogue.ForLemma(lemma) is null)
            {
                skipped.Add(lemma);
                continue;
            }

            named[lemma] = words.RecordOf(lemma) with { CreditUnits = WordProgress.UnitsToKnow, CorrectTurns = WordProgress.UsesToKnow };
        }

        if (named.Count == 0)
        {
            return skipped;
        }

        var kept = words.Records.Values.Where(record => !named.ContainsKey(record.Lemma)).ToList();
        kept.AddRange(named.Values);
        words.Restore(kept);
        return skipped;
    }
}

/// <summary>
/// The colours of the 3D world as the learner's words change. <see cref="Refresh"/> (once a frame) recomputes the grey set and hands it to the
/// renderer when it changed, so the chunks with a block that just became known are meshed again and regain colour. In greyscale mode it also
/// queues the toast <c>¡Nueva palabra: el agua!</c> for each catalogue noun that became known during play (the 3D world had no word toast before
/// this milestone). With the switch off nothing is grey and nothing is toasted.
/// </summary>
public sealed class GreyscaleWorld
{
    private readonly bool _enabled;
    private readonly NounCatalog _nouns;
    private readonly WordProgress _words;
    private readonly ToastQueue _toasts;
    private readonly Action<ulong> _setGreyKinds;
    private readonly HashSet<string> _known;

    /// <summary>
    /// Starts the colours of the world. The nouns in <paramref name="assumeKnown"/> are counted as known first (see
    /// <see cref="WordColours.AssumeKnown"/>); the known nouns at the start raise no toast.
    /// </summary>
    /// <param name="enabled">Whether <c>--greyscale</c> is on.</param>
    /// <param name="nouns">The catalogue (the block to noun map and the nouns' words).</param>
    /// <param name="words">The learner's progress, which the game changes.</param>
    /// <param name="toasts">Where the new-word toast goes.</param>
    /// <param name="setGreyKinds">Receives the grey set when it changes (the renderer's <see cref="VoxelRenderer.SetGreyKinds"/>).</param>
    /// <param name="assumeKnown">Nouns counted as known from the start (<c>--assume-known</c>), or <see langword="null"/>.</param>
    public GreyscaleWorld(bool enabled, NounCatalog nouns, WordProgress words, ToastQueue toasts, Action<ulong> setGreyKinds, IReadOnlyList<string>? assumeKnown = null)
    {
        ArgumentNullException.ThrowIfNull(nouns);
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(toasts);
        ArgumentNullException.ThrowIfNull(setGreyKinds);
        _enabled = enabled;
        _nouns = nouns;
        _words = words;
        _toasts = toasts;
        _setGreyKinds = setGreyKinds;
        if (assumeKnown is { Count: > 0 })
        {
            WordColours.AssumeKnown(words, assumeKnown);
        }

        _known = words.KnownLemmas.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Whether the greyscale A/B is on (<c>--greyscale</c>).</summary>
    public bool Enabled => _enabled;

    /// <summary>The grey set last handed to the renderer (one bit per block kind); 0 while the switch is off.</summary>
    public ulong GreyKinds { get; private set; }

    /// <summary>
    /// Recomputes the grey set from the progress; when it changed, hands it to the renderer. In greyscale mode, queues a toast for each noun that is
    /// known now and was not at the last call, in lemma order. Call once a frame, before the world is meshed.
    /// </summary>
    public void Refresh()
    {
        if (!_enabled)
        {
            return;
        }

        var grey = WordColours.GreyKinds(_nouns, _words);
        if (grey != GreyKinds)
        {
            GreyKinds = grey;
            _setGreyKinds(grey);
        }

        foreach (var lemma in _words.KnownLemmas.OrderBy(static lemma => lemma, StringComparer.Ordinal))
        {
            if (!_known.Add(lemma))
            {
                continue;
            }

            if (_nouns.ForLemma(lemma) is { } noun)
            {
                _toasts.Show($"¡Nueva palabra: {noun.WithDefinite}!");
            }
        }
    }
}
