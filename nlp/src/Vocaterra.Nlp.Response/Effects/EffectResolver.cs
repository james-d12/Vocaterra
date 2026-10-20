using Vocaterra.Nlp.Core.Verdicts;

namespace Vocaterra.Nlp.Response.Effects;

/// <summary>A resolved game effect: which effect, its kind and its value (a fraction, factor or flag, depending on the kind).</summary>
public sealed record GameEffect(string EffectId, string Kind, double Value);

/// <summary>Resolves the effects a verdict triggers by evaluating each effect's partial-credit curve at the verdict's correctness.</summary>
public static class EffectResolver
{
    /// <summary>
    /// Returns, in ordinal effect id order, one <see cref="GameEffect"/> for every effect whose intent equals the verdict's intent.
    /// Effects that require communication are skipped when the verdict was not communicated. The value is the curve linearly interpolated at the
    /// verdict's correctness, clamped to 0 to 1 (NaN counts as 0); outside the curve's range the nearest end value holds.
    /// </summary>
    public static IReadOnlyList<GameEffect> Resolve(Verdict verdict, EffectTable table)
    {
        ArgumentNullException.ThrowIfNull(verdict);
        ArgumentNullException.ThrowIfNull(table);
        var correctness = double.IsNaN(verdict.Correctness) ? 0.0 : Math.Clamp(verdict.Correctness, 0.0, 1.0);
        var intent = verdict.Intent.ToString();
        var effects = new List<GameEffect>();
        foreach (var definition in table.Effects)
        {
            if (!string.Equals(definition.Intent, intent, StringComparison.Ordinal)
                || (definition.RequiresCommunicated && !verdict.Communicated))
            {
                continue;
            }

            effects.Add(new GameEffect(definition.EffectId, definition.Kind, Interpolate(definition.Curve, correctness)));
        }

        return effects.AsReadOnly();
    }

    /// <summary>Evaluates a curve (strictly ascending in correctness) at <paramref name="correctness"/> by linear interpolation, holding the end values outside its range.</summary>
    public static double Interpolate(IReadOnlyList<CurvePoint> curve, double correctness)
    {
        ArgumentNullException.ThrowIfNull(curve);
        if (curve.Count == 0)
        {
            throw new ArgumentException("A curve needs at least one point.", nameof(curve));
        }

        if (correctness <= curve[0].Correctness)
        {
            return curve[0].Value;
        }

        for (var i = 1; i < curve.Count; i++)
        {
            var upper = curve[i];
            if (correctness > upper.Correctness)
            {
                continue;
            }

            var lower = curve[i - 1];
            if (correctness == upper.Correctness)
            {
                return upper.Value;
            }

            var fraction = (correctness - lower.Correctness) / (upper.Correctness - lower.Correctness);
            return lower.Value + ((upper.Value - lower.Value) * fraction);
        }

        return curve[^1].Value;
    }
}
