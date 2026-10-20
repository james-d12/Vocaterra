using Slg.Phonology;

namespace Slg.Pronunciation;

/// <summary>The articulatory class of a phone; thresholds and GOP temperatures are set per class.</summary>
public enum PhoneClass
{
    /// <summary>Vowels, including the learner vowels <c>ə</c>, <c>eɪ</c> and <c>oʊ</c>.</summary>
    Vowel,

    /// <summary>Taps, trills and the English approximant <c>ɹ</c>.</summary>
    TapTrill,

    /// <summary>Fricatives and fricative-like approximants (<c>β ð ɣ ʝ h</c>).</summary>
    Fricative,

    /// <summary>Stops and the affricate <c>tʃ</c>.</summary>
    Stop,

    /// <summary>Nasals.</summary>
    Nasal,

    /// <summary>Laterals.</summary>
    Lateral,

    /// <summary>Glides <c>j</c> and <c>w</c>.</summary>
    Glide,
}

/// <summary>Maps phone tokens to <see cref="PhoneClass"/> and gives each class its stable text identifier.</summary>
public static class PhoneClasses
{
    private static readonly PhoneClass[] AllClasses =
    [
        PhoneClass.Vowel,
        PhoneClass.TapTrill,
        PhoneClass.Fricative,
        PhoneClass.Stop,
        PhoneClass.Nasal,
        PhoneClass.Lateral,
        PhoneClass.Glide,
    ];

    /// <summary>All classes in declaration order.</summary>
    public static IReadOnlyList<PhoneClass> All { get; } = Array.AsReadOnly(AllClasses);

    /// <summary>Classifies a phone token; tokens outside the Slg.Phonology inventory fall back to <see cref="PhoneClass.Stop"/>.</summary>
    public static PhoneClass Of(string phone)
    {
        ArgumentNullException.ThrowIfNull(phone);
        return phone switch
        {
            PhoneInventory.A or PhoneInventory.E or PhoneInventory.I or PhoneInventory.O or PhoneInventory.U
                or PhoneInventory.Schwa or PhoneInventory.EnglishEi or PhoneInventory.EnglishOu => PhoneClass.Vowel,
            PhoneInventory.Tap or PhoneInventory.Trill or PhoneInventory.EnglishR => PhoneClass.TapTrill,
            PhoneInventory.F or PhoneInventory.Theta or PhoneInventory.S or PhoneInventory.X or PhoneInventory.Yod
                or PhoneInventory.Beta or PhoneInventory.Eth or PhoneInventory.Gamma or PhoneInventory.VoicedS
                or PhoneInventory.H or PhoneInventory.Sh or PhoneInventory.Zh => PhoneClass.Fricative,
            PhoneInventory.M or PhoneInventory.N or PhoneInventory.PalatalNasal or PhoneInventory.VelarNasal => PhoneClass.Nasal,
            PhoneInventory.L or PhoneInventory.PalatalLateral => PhoneClass.Lateral,
            PhoneInventory.J or PhoneInventory.W => PhoneClass.Glide,
            _ => PhoneClass.Stop,
        };
    }

    /// <summary>The stable identifier of a class: <c>vowel</c>, <c>tap_trill</c>, <c>fricative</c>, <c>stop</c>, <c>nasal</c>, <c>lateral</c> or <c>glide</c>.</summary>
    public static string Name(PhoneClass phoneClass)
    {
        return phoneClass switch
        {
            PhoneClass.Vowel => "vowel",
            PhoneClass.TapTrill => "tap_trill",
            PhoneClass.Fricative => "fricative",
            PhoneClass.Stop => "stop",
            PhoneClass.Nasal => "nasal",
            PhoneClass.Lateral => "lateral",
            PhoneClass.Glide => "glide",
            _ => throw new ArgumentOutOfRangeException(nameof(phoneClass), phoneClass, "Unknown phone class."),
        };
    }
}
