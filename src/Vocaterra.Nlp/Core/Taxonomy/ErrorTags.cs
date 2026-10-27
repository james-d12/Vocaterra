namespace Vocaterra.Nlp.Core.Taxonomy;

/// <summary>Stable v1 error tag identifiers, in the order of the HLD taxonomy table.</summary>
public static class ErrorTags
{
    /// <summary>Wrong word for the concept.</summary>
    public const string VocabWrong = "vocab.wrong";

    /// <summary>Word not in the lexicon.</summary>
    public const string VocabUnknown = "vocab.unknown";

    /// <summary>Verb person or number mismatch.</summary>
    public const string ConjPerson = "conj.person";

    /// <summary>Wrong tense.</summary>
    public const string ConjTense = "conj.tense";

    /// <summary>Regularized irregular verb.</summary>
    public const string ConjIrregular = "conj.irregular";

    /// <summary>Article or adjective gender mismatch.</summary>
    public const string GenderAgree = "gender.agree";

    /// <summary>Singular or plural mismatch.</summary>
    public const string NounNumber = "noun.number";

    /// <summary>Ser/estar confusion.</summary>
    public const string SerEstar = "ser_estar";

    /// <summary>Non-Spanish word order.</summary>
    public const string WordOrder = "word_order";

    /// <summary>Vowel reduced or diphthongized.</summary>
    public const string PronVowel = "pron.vowel";

    /// <summary>Tap or trill error.</summary>
    public const string PronR = "pron.r";

    /// <summary>Pronounced silent h.</summary>
    public const string PronSilentH = "pron.silent_h";

    /// <summary>Stress on the wrong syllable.</summary>
    public const string PronStress = "pron.stress";

    private static readonly string[] AllTags =
    [
        VocabWrong,
        VocabUnknown,
        ConjPerson,
        ConjTense,
        ConjIrregular,
        GenderAgree,
        NounNumber,
        SerEstar,
        WordOrder,
        PronVowel,
        PronR,
        PronSilentH,
        PronStress,
    ];

    /// <summary>All v1 tags in HLD table order.</summary>
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(AllTags);

    /// <summary>Returns true when the tag is one of the v1 identifiers (ordinal comparison).</summary>
    public static bool IsKnown(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return Array.IndexOf(AllTags, tag) >= 0;
    }

    /// <summary>Returns the family: the text before the first dot, or the whole tag when it has no dot.</summary>
    public static string Family(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        var dot = tag.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? tag : tag[..dot];
    }

    /// <summary>Returns true for tags in the pronunciation family (<c>pron.*</c>).</summary>
    public static bool IsPronunciation(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return tag.StartsWith("pron.", StringComparison.Ordinal);
    }
}
