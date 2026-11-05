using System.Globalization;
using System.Text;

namespace Vocaterra.Game.Core.Language.Lessons;

/// <summary>What a line said at a teacher means for a class (L01).</summary>
public enum ClassAnswer
{
    /// <summary>Neither an answer to the offer nor a request for a class.</summary>
    None,

    /// <summary>Yes: take the class on offer.</summary>
    Yes,

    /// <summary>No, or no thank you: the offer is over and costs nothing.</summary>
    No,

    /// <summary>The player asks for a class (<c>¿Me enseñas?</c>).</summary>
    Ask,
}

/// <summary>
/// The plain lines a player answers a teacher's offer of a class with, and asks for one with (L01, D-game-25; the lists are <c>classAnswers</c> in <c>game/words.json</c>). The core has no
/// intent for «¿Me enseñas?» (tried through its CLI: it is unresolved), so the game reads these few lines itself, from the text of the turn, before the NPC's ordinary talk takes the line for a trade.
/// </summary>
public sealed class ClassAnswers
{
    private readonly HashSet<string> _yes;
    private readonly HashSet<string> _no;
    private readonly HashSet<string> _ask;

    /// <summary>The lines of each kind, as the words file spells them (<c>classAnswers</c> in <c>game/words.json</c>).</summary>
    /// <param name="yes">Lines that take the class.</param>
    /// <param name="no">Lines that decline it.</param>
    /// <param name="ask">Lines that ask for a class.</param>
    public ClassAnswers(IEnumerable<string> yes, IEnumerable<string> no, IEnumerable<string> ask)
    {
        _yes = [.. yes.Select(Plain)];
        _no = [.. no.Select(Plain)];
        _ask = [.. ask.Select(Plain)];
    }

    /// <summary>Reads a line (typed or heard) as an answer to the offer of a class, or as a request for one.</summary>
    /// <param name="line">The line.</param>
    public ClassAnswer Read(string? line)
    {
        var plain = Plain(line);
        return _yes.Contains(plain) ? ClassAnswer.Yes : _no.Contains(plain) ? ClassAnswer.No : _ask.Contains(plain) ? ClassAnswer.Ask : ClassAnswer.None;
    }

    // Lower case, no accents, no punctuation, single spaces.
    private static string Plain(string? line)
    {
        var builder = new StringBuilder();
        foreach (var c in (line ?? string.Empty).Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
