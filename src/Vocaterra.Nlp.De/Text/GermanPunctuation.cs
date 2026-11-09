using System.Text;

namespace Vocaterra.Nlp.De.Text;

/// <summary>
/// Cleans rendered German text (M37): collapses white space, closes the gap before <c>, . ; : ? ! …</c> and before a closing quote (<c>“</c> or <c>«</c>), and after an
/// opening quote (<c>„</c> or <c>»</c>). German has no inverted marks, so nothing is inserted. The German pack's <see cref="IPunctuation"/>.
/// </summary>
public sealed class GermanPunctuation : IPunctuation
{
    private GermanPunctuation()
    {
    }

    /// <summary>The one instance.</summary>
    public static GermanPunctuation Instance { get; } = new();

    /// <inheritdoc />
    public string Fix(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
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
                var attaches = previous is '„' or '»' || c is ',' or '.' or ';' or ':' or '?' or '!' or '…' or '“' or '«';
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
}
