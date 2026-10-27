using System.Text;

namespace Vocaterra.Nlp.Language.Packs;

/// <summary>
/// Cleans rendered Spanish text: collapses whitespace, removes the space after an opening <c>¿</c> or <c>¡</c> and before closing
/// punctuation, and balances the inverted marks so every <c>?</c> has a <c>¿</c> and every <c>!</c> has a <c>¡</c> in its sentence.
/// </summary>
/// <remarks>
/// The implementation of the Spanish pack's <see cref="IPunctuation"/> (M33). The engine's rendering goes through
/// <c>bundle.Language.Punctuation</c>; this class is the Spanish implementation behind that member and is not called by neutral code.
/// It lives in the packs folder so that the packs depend on no stage of the response (ADR 0011, point 9).
/// </remarks>
internal static class PunctuationFixer
{
    public static string Fix(string text)
    {
        var collapsed = CollapseSpaces(text);
        return Balance(collapsed);
    }

    private static string CollapseSpaces(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                var previous = builder[^1];
                var attaches = previous is '¿' or '¡' || c is ',' or '.' or ';' or ':' or '?' or '!' or '…';
                if (!attaches)
                {
                    builder.Append(' ');
                }

                pendingSpace = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    private static string Balance(string text)
    {
        var builder = new StringBuilder(text.Length + 4);
        var sentenceStart = 0;
        var atSentenceStart = true;
        var openQuestion = false;
        var openExclamation = false;
        foreach (var c in text)
        {
            if (atSentenceStart && !char.IsWhiteSpace(c))
            {
                sentenceStart = builder.Length;
                atSentenceStart = false;
            }

            switch (c)
            {
                case '¿':
                    openQuestion = true;
                    break;
                case '¡':
                    openExclamation = true;
                    break;
                case '?':
                    if (!openQuestion)
                    {
                        builder.Insert(sentenceStart, '¿');
                    }

                    openQuestion = false;
                    atSentenceStart = true;
                    break;
                case '!':
                    if (!openExclamation)
                    {
                        builder.Insert(sentenceStart, '¡');
                    }

                    openExclamation = false;
                    atSentenceStart = true;
                    break;
                case '.':
                    atSentenceStart = true;
                    break;
                default:
                    break;
            }

            builder.Append(c);
        }

        if (openQuestion)
        {
            builder.Append('?');
        }

        if (openExclamation)
        {
            builder.Append('!');
        }

        return builder.ToString();
    }
}
