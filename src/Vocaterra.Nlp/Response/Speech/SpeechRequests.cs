using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Language.Packs;
using Vocaterra.Nlp.Phonology;

namespace Vocaterra.Nlp.Response.Speech;

/// <summary>Builds <see cref="SynthesisRequest"/> values whose digits and phones come from a language pack (M33).</summary>
public static class SpeechRequests
{
    /// <summary>
    /// Creates a request for the text with digit runs spelled out as words of the pack's language (the G2P drops digits) and, when the pack has a
    /// G2P, the phones of the spoken text for the dialect (the language's default dialect when null). A pack without a G2P gives no phones.
    /// Synthesizers that cannot take phones ignore them.
    /// </summary>
    /// <param name="text">The text to speak.</param>
    /// <param name="voiceId">The voice label, part of the synthesis cache keys.</param>
    /// <param name="language">The language of the text: the bundle's <c>Language</c>.</param>
    /// <param name="dialect">The dialect for the phones; the Latin American neutral dialect of Spanish when null.</param>
    public static SynthesisRequest Create(string text, string voiceId, ILanguagePack language, DialectConfig? dialect = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(voiceId);
        ArgumentNullException.ThrowIfNull(language);
        var spoken = language.Numbers.DigitsToWords(text);
        var phones = language.G2p is { } g2p
            ? g2p.Phonemize(spoken, dialect ?? DialectConfig.LatinAmericanNeutral).Phones
            : null;
        return new SynthesisRequest(spoken, voiceId, phones);
    }
}
