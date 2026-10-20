using System.Text.Json.Serialization;
using Slg.Core.Determinism;

namespace Slg.Core.Verdicts;

/// <summary>Stable identifier of a player intent, serialized as a plain JSON string.</summary>
[JsonConverter(typeof(IntentIdJsonConverter))]
public readonly record struct IntentId(string Value)
{
    /// <summary>The text of <see cref="Unknown"/>.</summary>
    public const string UnknownValue = "unknown";

    /// <summary>The intent used when nothing could be resolved.</summary>
    public static readonly IntentId Unknown = new(UnknownValue);

    /// <summary>True for <see cref="Unknown"/>, for an empty value and for <c>default</c>.</summary>
    public bool IsUnknown => string.IsNullOrEmpty(Value) || string.Equals(Value, UnknownValue, StringComparison.Ordinal);

    /// <summary>Returns <see cref="Value"/> (or "unknown" for <c>default</c>).</summary>
    public override string ToString()
    {
        return Value ?? UnknownValue;
    }
}
