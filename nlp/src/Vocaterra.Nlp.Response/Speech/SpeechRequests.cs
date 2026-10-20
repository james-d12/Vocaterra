using Vocaterra.Nlp.Core.Stages;
using Vocaterra.Nlp.Nlp.Normalization;
using Vocaterra.Nlp.Phonology;

namespace Vocaterra.Nlp.Response.Speech;

/// <summary>Builds <see cref="SynthesisRequest"/> values whose phones come from the Spanish G2P in <c>Vocaterra.Nlp.Phonology</c>.</summary>
public static class SpeechRequests
{
    /// <summary>
    /// Creates a request for the text with digit runs spelled out as Spanish words (the G2P drops digits) and the phones of the text for the dialect
    /// (Latin American neutral when null). Synthesizers that cannot take phones ignore them.
    /// </summary>
    public static SynthesisRequest Create(string text, string voiceId, DialectConfig? dialect = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(voiceId);
        var spoken = SpanishNumbers.DigitsToWords(text);
        var phones = SpanishG2p.Phonemize(spoken, dialect ?? DialectConfig.LatinAmericanNeutral).Phones;
        return new SynthesisRequest(spoken, voiceId, phones);
    }
}
