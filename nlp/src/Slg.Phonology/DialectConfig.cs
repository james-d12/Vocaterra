namespace Slg.Phonology;

/// <summary>The consonant used for <c>ll</c> and consonantal <c>y</c> when a dialect has yeísmo.</summary>
public enum LlSound
{
    /// <summary>Palatal fricative or approximant <c>ʝ</c> (most of Latin America and Spain).</summary>
    Fricative,

    /// <summary>Voiceless postalveolar fricative <c>ʃ</c> (rioplatense sheísmo).</summary>
    Sh,

    /// <summary>Voiced postalveolar fricative <c>ʒ</c> (rioplatense žeísmo).</summary>
    Zh,
}

/// <summary>
/// Dialect switches for the G2P: seseo versus distinción, yeísmo versus a distinct <c>ll</c>,
/// the sound used for <c>ll</c>/<c>y</c> under yeísmo, and syllable-final <c>s</c> aspiration.
/// </summary>
/// <param name="Seseo">True when <c>z</c> and <c>c</c> before <c>e</c>/<c>i</c> are <c>s</c>; false when they are <c>θ</c>.</param>
/// <param name="Yeismo">True when <c>ll</c> merges with <c>y</c>; false keeps <c>ll</c> as <c>ʎ</c>.</param>
/// <param name="LlSound">The consonant for <c>ll</c> and consonantal <c>y</c> when <paramref name="Yeismo"/> is true.</param>
/// <param name="SAspiration">True when syllable-final <c>s</c> is realized as <c>h</c>.</param>
public sealed record DialectConfig(bool Seseo, bool Yeismo, LlSound LlSound, bool SAspiration)
{
    /// <summary>Latin American neutral: seseo and yeísmo. The default for NPC speech.</summary>
    public static readonly DialectConfig LatinAmericanNeutral = new(Seseo: true, Yeismo: true, LlSound: LlSound.Fricative, SAspiration: false);

    /// <summary>Castilian standard: distinción and yeísmo.</summary>
    public static readonly DialectConfig CastilianStandard = new(Seseo: false, Yeismo: true, LlSound: LlSound.Fricative, SAspiration: false);

    /// <summary>Rioplatense: seseo, yeísmo with <c>ʃ</c> for <c>ll</c> and <c>y</c>.</summary>
    public static readonly DialectConfig Rioplatense = new(Seseo: true, Yeismo: true, LlSound: LlSound.Sh, SAspiration: false);

    /// <summary>Caribbean: seseo, yeísmo and aspirated syllable-final <c>s</c>.</summary>
    public static readonly DialectConfig Caribbean = new(Seseo: true, Yeismo: true, LlSound: LlSound.Fricative, SAspiration: true);

    /// <summary>Andean: seseo with a distinct palatal lateral for <c>ll</c> (lleísmo).</summary>
    public static readonly DialectConfig AndeanLleista = new(Seseo: true, Yeismo: false, LlSound: LlSound.Fricative, SAspiration: false);

    /// <summary>Every shipped dialect, in a fixed order with the default first; used to accept all variants.</summary>
    public static readonly IReadOnlyList<DialectConfig> AllShipped = Array.AsReadOnly(
    [
        LatinAmericanNeutral,
        CastilianStandard,
        Rioplatense,
        Caribbean,
        AndeanLleista,
    ]);

    /// <summary>The phone for the letter <c>z</c> and for <c>c</c> before <c>e</c>/<c>i</c>.</summary>
    public string SibilantPhone => Seseo ? PhoneInventory.S : PhoneInventory.Theta;

    /// <summary>The phone for consonantal <c>y</c>.</summary>
    public string YPhone => Yeismo ? YeistaPhone() : PhoneInventory.Yod;

    /// <summary>The phone for the digraph <c>ll</c>.</summary>
    public string LlPhone => Yeismo ? YeistaPhone() : PhoneInventory.PalatalLateral;

    private string YeistaPhone()
    {
        return LlSound switch
        {
            LlSound.Sh => PhoneInventory.Sh,
            LlSound.Zh => PhoneInventory.Zh,
            _ => PhoneInventory.Yod,
        };
    }
}
