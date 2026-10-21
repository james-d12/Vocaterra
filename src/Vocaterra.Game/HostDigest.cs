using System.Security.Cryptography;
using System.Text;

namespace Vocaterra.Game;

/// <summary>
/// The game's host state digest for an utterance that carries host slots or extra skills. The core's contract (docs/API.md, section 4): a host
/// that gives the context its own digest must include the host slots and the extra skills of the utterance in it.
/// </summary>
/// <remarks>
/// The input is SHA-256 over text: the simulation digest on its first line, then a <c>slots</c> header and one <c>key=value</c> line per host
/// slot in ordinal key order, then a <c>skills</c> header and one skill per line in ordinal order (distinct). Values and skill ids are lemmas,
/// phrases and <c>word.&lt;lemma&gt;</c> ids, which hold no line break, so the lines cannot be read two ways. Nothing here reads a clock or a
/// random number.
/// </remarks>
public static class HostDigest
{
    /// <summary>The digest of the simulation state together with one utterance's host slots and extra skills.</summary>
    /// <param name="simulationDigest">The simulation's state digest (<c>GameSimulation.ComputeStateDigest()</c>).</param>
    /// <param name="hostSlots">The host facts of the utterance, by name; the order they were added in does not matter.</param>
    /// <param name="extraSkills">The extra skill ids of the utterance; the order does not matter and a repeated id counts once.</param>
    /// <returns>The lower-case hexadecimal SHA-256 of the sections above.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static string Of(string simulationDigest, IReadOnlyDictionary<string, string> hostSlots, IReadOnlyList<string> extraSkills)
    {
        ArgumentNullException.ThrowIfNull(simulationDigest);
        ArgumentNullException.ThrowIfNull(hostSlots);
        ArgumentNullException.ThrowIfNull(extraSkills);

        var text = new StringBuilder();
        text.Append("simulation=").Append(simulationDigest).Append('\n');
        text.Append("slots\n");
        foreach (var key in hostSlots.Keys.Order(StringComparer.Ordinal))
        {
            text.Append(key).Append('=').Append(hostSlots[key]).Append('\n');
        }

        text.Append("skills\n");
        foreach (var skill in extraSkills.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            text.Append(skill).Append('\n');
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}
