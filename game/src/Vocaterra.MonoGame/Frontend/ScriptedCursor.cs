using System.Globalization;
using System.Text;
using Vocaterra.Game;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// Where the scripted run's mouse cursor sits for each <c>--say</c> line: on a fixed tile (<c>--point</c>), or on the tile of a noun nearest the
/// player when the line is sent (<c>--point-nearest</c>). The noun may be typed without its accent (<c>arbol</c>).
/// </summary>
/// <param name="Fixed">The tile, or <see langword="null"/>.</param>
/// <param name="Noun">The noun, or <see langword="null"/>.</param>
public sealed record ScriptedCursor(TilePoint? Fixed, string? Noun)
{
    /// <summary>The tile under the cursor for a line sent now, or <see langword="null"/> when the noun has no tile in range.</summary>
    public TilePoint? Resolve(GameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (Fixed is { } tile)
        {
            return tile;
        }

        if (Noun is null)
        {
            return null;
        }

        var wanted = Fold(Noun);
        var lemma = session.Nouns.Nouns.FirstOrDefault(n => Fold(n.Lemma) == wanted)?.Lemma ?? Noun;
        return session.Facts.NearestOfNoun(session.PlayerTile, lemma)?.Tile;
    }

    /// <summary>The text without accents, in lower case, for comparing a typed noun with a lemma.</summary>
    public static string Fold(string text)
    {
        var builder = new StringBuilder();
        foreach (var ch in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
