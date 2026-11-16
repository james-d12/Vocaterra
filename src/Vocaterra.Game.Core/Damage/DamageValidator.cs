using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Core.Damage;

/// <summary>
/// The checks of <c>game/damage.json</c> (I05a): each cause id is a lowercase key, listed once and raised by the code (<see cref="DamageCause.Raised"/>, or the causes a test passes), every cause the code
/// raises has a row, and every modifier id a row names is registered in the <see cref="DamageModifiers"/> and named once in that row. <see cref="Content.GameDataValidator"/> runs it with the other files' checks.
/// </summary>
internal static partial class DamageValidator
{
    private const string File = DamageData.RelativePath;

    /// <summary>Every problem of the causes and their modifiers, in file order; empty when they hold together.</summary>
    /// <param name="data">The damage data.</param>
    /// <param name="modifiers">The modifiers the code has.</param>
    /// <param name="raised">The causes the code raises; the game's (<see cref="DamageCause.Raised"/>) when omitted.</param>
    public static IReadOnlyList<string> Validate(DamageData data, DamageModifiers modifiers, IReadOnlyList<DamageCause>? raised = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(modifiers);
        raised ??= DamageCause.Raised;
        var problems = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cause in data.Causes)
        {
            if (!KeyPattern().IsMatch(cause.Id))
            {
                problems.Add($"{File}: the cause id '{cause.Id}' is not a lowercase key (letters, digits and hyphens).");
            }
            else if (!ids.Add(cause.Id))
            {
                problems.Add($"{File}: the cause id '{cause.Id}' is used twice.");
            }

            if (!raised.Any(r => string.Equals(r.Id, cause.Id, StringComparison.Ordinal)))
            {
                problems.Add($"{File}: the cause '{cause.Id}' is not one the game raises ({string.Join(", ", raised.Select(static r => r.Id))}).");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in cause.Modifiers)
            {
                if (modifiers.Find(id) is null)
                {
                    problems.Add($"{File}: the cause '{cause.Id}' names the modifier '{id}', which is not one of {string.Join(", ", modifiers.Ids)}.");
                }
                else if (!seen.Add(id))
                {
                    problems.Add($"{File}: the cause '{cause.Id}' names the modifier '{id}' twice.");
                }
            }
        }

        foreach (var cause in raised.Where(r => !ids.Contains(r.Id)))
        {
            problems.Add($"{File}: the game raises the cause '{cause.Id}' and the file has no row for it.");
        }

        return problems;
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex KeyPattern();
}
