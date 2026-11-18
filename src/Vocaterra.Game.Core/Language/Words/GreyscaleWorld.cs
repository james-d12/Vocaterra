using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Tip;

namespace Vocaterra.Game.Core.Language.Words;

/// <summary>
/// The colours of the 3D world as the learner's words change. <see cref="Refresh"/> (once a frame) recomputes the grey set and hands it to the
/// renderer when it changed, so the chunks with a block that just became known are meshed again and regain colour. In greyscale mode it also
/// queues the toast <c>¡Nueva palabra: el agua!</c> for each catalogue noun that became known during play (the 3D world had no word toast before
/// this milestone). With the switch off nothing is grey and nothing is toasted.
/// </summary>
public sealed class GreyscaleWorld
{
    private readonly LocaleText _ui;
    private bool _enabled;
    private readonly NounCatalog _nouns;
    private readonly WordProgress _words;
    private readonly ToastQueue _toasts;
    private readonly Action<UInt128> _setGreyKinds;
    private HashSet<string> _known;

    /// <summary>
    /// Starts the colours of the world. The nouns in <paramref name="assumeKnown"/> are counted as known first (see
    /// <see cref="WordColours.AssumeKnown"/>); the known nouns at the start raise no toast.
    /// </summary>
    /// <param name="enabled">Whether <c>--greyscale</c> is on.</param>
    /// <param name="nouns">The catalogue (the block to noun map and the nouns' words).</param>
    /// <param name="words">The learner's progress, which the game changes.</param>
    /// <param name="toasts">Where the new-word toast goes.</param>
    /// <param name="setGreyKinds">Receives the grey set when it changes (the renderer's <c>VoxelRenderer.SetGreyKinds</c>).</param>
    /// <param name="ui">The support language (the new-word toast, <c>ui.greyNewWord</c>, C04).</param>
    /// <param name="assumeKnown">Nouns counted as known from the start (<c>--assume-known</c>), or <see langword="null"/>.</param>
    public GreyscaleWorld(bool enabled, NounCatalog nouns, WordProgress words, ToastQueue toasts, Action<UInt128> setGreyKinds, LocaleText ui, IReadOnlyList<string>? assumeKnown = null)
    {
        ArgumentNullException.ThrowIfNull(nouns);
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(toasts);
        ArgumentNullException.ThrowIfNull(setGreyKinds);
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
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

    /// <summary>
    /// Whether the greyscale A/B is on (<c>--greyscale</c>; the menu's Greyscale row switches it during the run). Turning it on takes the words known now as the ones that raise no
    /// toast; turning it off hands the renderer an empty grey set at the next <see cref="Refresh"/>.
    /// </summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (value && !_enabled)
            {
                _known = _words.KnownLemmas.ToHashSet(StringComparer.Ordinal);
            }

            _enabled = value;
        }
    }

    /// <summary>
    /// A view-only test setting (the menu): every block is drawn in colour as if all the nouns were known. The learner's progress is not read or changed by it, so nothing of it reaches a
    /// save (unlike the launch option <c>--assume-known</c>, which credits the nouns it names).
    /// </summary>
    public bool AssumeAllKnown { get; set; }

    /// <summary>The grey set last handed to the renderer (one bit per block kind); 0 while the switch is off.</summary>
    public UInt128 GreyKinds { get; private set; }

    /// <summary>
    /// Recomputes the grey set from the progress; when it changed, hands it to the renderer. In greyscale mode, queues a toast for each noun that is
    /// known now and was not at the last call, in lemma order. Call once a frame, before the world is meshed.
    /// </summary>
    public void Refresh()
    {
        if (!_enabled)
        {
            if (GreyKinds != UInt128.Zero)
            {
                GreyKinds = UInt128.Zero;
                _setGreyKinds(UInt128.Zero);
            }

            return;
        }

        var grey = AssumeAllKnown ? UInt128.Zero : WordColours.GreyKinds(_nouns, _words);
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
                _toasts.Show(_ui.Ui("greyNewWord", ("word", noun.WithDefinite)));
            }
        }
    }
}
