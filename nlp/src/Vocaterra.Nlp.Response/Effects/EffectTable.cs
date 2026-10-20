using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Nlp.Response.Effects;

/// <summary>The effect kinds a table can define.</summary>
public static class EffectKinds
{
    /// <summary>Spell power as a fraction of full power.</summary>
    public const string SpellPower = "spell_power";

    /// <summary>Trade price as a factor of the base price.</summary>
    public const string TradePrice = "trade_price";

    /// <summary>A quest flag value, for example 0 or 1.</summary>
    public const string QuestFlag = "quest_flag";

    /// <summary>A marker effect that changes nothing in the game.</summary>
    public const string None = "none";

    /// <summary>All kinds, in declaration order.</summary>
    public static IReadOnlyList<string> All { get; } = [SpellPower, TradePrice, QuestFlag, None];
}

/// <summary>One point of a piecewise-linear effect curve: the effect value at a correctness.</summary>
public sealed record CurvePoint(double Correctness, double Value)
{
    /// <summary>Correctness in the verdict's units (normally 0 to 1); finite.</summary>
    public double Correctness { get; init; } = double.IsFinite(Correctness)
        ? Correctness
        : throw new ArgumentOutOfRangeException(nameof(Correctness), Correctness, "Correctness must be finite.");

    /// <summary>The effect value at this correctness; finite.</summary>
    public double Value { get; init; } = double.IsFinite(Value)
        ? Value
        : throw new ArgumentOutOfRangeException(nameof(Value), Value, "Value must be finite.");
}

/// <summary>One effect: for verdicts of <c>Intent</c>, a curve from correctness to a value of the given kind, optionally only when the player communicated.</summary>
public sealed record EffectDefinition(string EffectId, string Intent, string Kind, IReadOnlyList<CurvePoint> Curve, bool RequiresCommunicated = true)
{
    /// <summary>Unique identifier of the effect; never empty.</summary>
    public string EffectId { get; init; } = !string.IsNullOrWhiteSpace(EffectId)
        ? EffectId
        : throw new ArgumentException("An effect id is required.", nameof(EffectId));

    /// <summary>The intent id of the verdicts this effect applies to; never empty.</summary>
    public string Intent { get; init; } = !string.IsNullOrWhiteSpace(Intent)
        ? Intent
        : throw new ArgumentException("An intent is required.", nameof(Intent));

    /// <summary>One of <see cref="EffectKinds"/>.</summary>
    public string Kind { get; init; } = Kind is not null && EffectKinds.All.Contains(Kind)
        ? Kind
        : throw new ArgumentException($"Unknown effect kind '{Kind}'.", nameof(Kind));

    /// <summary>The curve points, strictly ascending in correctness; never empty.</summary>
    public IReadOnlyList<CurvePoint> Curve { get; init; } = ValidateCurve(Curve);

    /// <summary>When true (the default) the effect only applies to verdicts in which the player communicated.</summary>
    public bool RequiresCommunicated { get; init; } = RequiresCommunicated;

    /// <summary>Structural equality including the curve.</summary>
    public bool Equals(EffectDefinition? other)
    {
        return other is not null
            && string.Equals(EffectId, other.EffectId, StringComparison.Ordinal)
            && string.Equals(Intent, other.Intent, StringComparison.Ordinal)
            && string.Equals(Kind, other.Kind, StringComparison.Ordinal)
            && ListEquality.Equal(Curve, other.Curve)
            && RequiresCommunicated == other.RequiresCommunicated;
    }

    /// <summary>Hash consistent with <see cref="Equals(EffectDefinition)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(EffectId, StringComparer.Ordinal);
        hash.Add(Intent, StringComparer.Ordinal);
        hash.Add(Kind, StringComparer.Ordinal);
        ListEquality.Add(ref hash, Curve);
        hash.Add(RequiresCommunicated);
        return hash.ToHashCode();
    }

    private static IReadOnlyList<CurvePoint> ValidateCurve(IReadOnlyList<CurvePoint> curve)
    {
        ArgumentNullException.ThrowIfNull(curve, nameof(Curve));
        if (curve.Count == 0)
        {
            throw new ArgumentException("A curve needs at least one point.", nameof(Curve));
        }

        for (var i = 0; i < curve.Count; i++)
        {
            ArgumentNullException.ThrowIfNull(curve[i], nameof(Curve));
            if (i > 0 && !(curve[i].Correctness > curve[i - 1].Correctness))
            {
                throw new ArgumentException("Curve points must be strictly ascending in correctness.", nameof(Curve));
            }
        }

        return curve;
    }
}

/// <summary>The game-effect table: effects are kept in ordinal <c>EffectId</c> order; serialized as canonical JSON <c>{"effects":[...]}</c>.</summary>
public sealed record EffectTable(IReadOnlyList<EffectDefinition> Effects)
{
    /// <summary>The effects sorted by ordinal effect id; ids are unique.</summary>
    public IReadOnlyList<EffectDefinition> Effects { get; init; } = SortAndValidate(Effects);

    /// <summary>Parses canonical JSON, validating every effect.</summary>
    public static EffectTable FromJson(string json)
    {
        return CanonicalJson.Deserialize<EffectTable>(json);
    }

    /// <summary>Serializes the table as canonical JSON.</summary>
    public string ToJson()
    {
        return CanonicalJson.Serialize(this);
    }

    /// <summary>Structural equality including the effect list.</summary>
    public bool Equals(EffectTable? other)
    {
        return other is not null && ListEquality.Equal(Effects, other.Effects);
    }

    /// <summary>Hash consistent with <see cref="Equals(EffectTable)"/>.</summary>
    public override int GetHashCode()
    {
        var hash = default(HashCode);
        ListEquality.Add(ref hash, Effects);
        return hash.ToHashCode();
    }

    private static IReadOnlyList<EffectDefinition> SortAndValidate(IReadOnlyList<EffectDefinition> effects)
    {
        ArgumentNullException.ThrowIfNull(effects, nameof(Effects));
        var sorted = new List<EffectDefinition>(effects.Count);
        foreach (var effect in effects)
        {
            ArgumentNullException.ThrowIfNull(effect, nameof(Effects));
            sorted.Add(effect);
        }

        sorted.Sort(static (a, b) => string.CompareOrdinal(a.EffectId, b.EffectId));
        for (var i = 1; i < sorted.Count; i++)
        {
            if (string.Equals(sorted[i - 1].EffectId, sorted[i].EffectId, StringComparison.Ordinal))
            {
                throw new ArgumentException($"Duplicate effect id '{sorted[i].EffectId}'.", nameof(Effects));
            }
        }

        return sorted.AsReadOnly();
    }
}
