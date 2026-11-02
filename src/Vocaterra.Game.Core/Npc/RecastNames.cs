using System.Text;
using Vocaterra.Game.Core.Language.Phrases;

namespace Vocaterra.Game.Core.Npc;

/// <summary>
/// The villagers' names in a correction line as the game shows it (VILLAGE_PLAN V20, handover 9.3 D (d)): the core writes the learner's words back in lower case, so
/// Domingo's recast of <c>buscaré madera para Luz</c> read <c>«buscaré madera para luz»</c>. Before the line is shown, a word that is an NPC's name (the roster's, never a
/// list here) gets the name's own spelling, <c>Luz</c>, unless a determiner comes before it: then it is the common noun (<c>la luz</c>, <c>el domingo</c>, <c>del mar</c>,
/// <c>la luna</c>) and stays as it is. The core's own text is unchanged; only the game's copy of the line is.
/// </summary>
public static class RecastNames
{
    // The words before a name that make it the common noun: the articles (with al and del), the demonstratives and the possessives.
    private static readonly string[] Determiners =
    [
        "el", "la", "los", "las", "lo", "un", "una", "unos", "unas", "al", "del",
        "este", "esta", "estos", "estas", "ese", "esa", "esos", "esas", "aquel", "aquella", "aquellos", "aquellas",
        "mi", "mis", "tu", "tus", "su", "sus", "nuestro", "nuestra", "nuestros", "nuestras",
    ];

    /// <summary>The line with each NPC name that no determiner precedes spelt as the roster spells it (see the type's summary), or the line as it is.</summary>
    /// <param name="line">The correction line (a recast, a hint or an explicit correction).</param>
    /// <param name="names">The NPCs' names (<see cref="NpcEntry.Name"/>).</param>
    public static string Capitalise(string line, IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(names);
        var byPlain = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            if (!string.IsNullOrWhiteSpace(name) && !name.Contains(' ', StringComparison.Ordinal))
            {
                byPlain.TryAdd(PhraseSheet.Plain(name), name);
            }
        }

        if (byPlain.Count == 0)
        {
            return line;
        }

        var text = new StringBuilder(line.Length);
        string? previous = null;
        var at = 0;
        while (at < line.Length)
        {
            if (!char.IsLetter(line[at]))
            {
                text.Append(line[at++]);
                continue;
            }

            var start = at;
            while (at < line.Length && char.IsLetter(line[at]))
            {
                at++;
            }

            var word = line[start..at];
            var plain = PhraseSheet.Plain(word);
            var determined = previous is not null && Determiners.Contains(previous, StringComparer.Ordinal);
            text.Append(!determined && byPlain.TryGetValue(plain, out var name) ? name : word);
            previous = plain;
        }

        return text.ToString();
    }
}
