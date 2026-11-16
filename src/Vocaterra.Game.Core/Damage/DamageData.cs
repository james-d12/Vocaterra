using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Damage;

/// <summary>
/// The damage causes and what applies to each (<c>game/damage.json</c>, I05a): per cause the ordered ids of the <see cref="IDamageModifier"/>s. Armour applies to none today (the owner: it does not reduce
/// drowning, lava or cold); letting it soften a cause is adding <c>"armour"</c> to that row. A bundle with no <c>game/damage.json</c> does not load; <see cref="DamageValidator"/> checks the ids.
/// </summary>
/// <param name="Causes">The causes, in file order.</param>
public sealed record DamageData(IReadOnlyList<DamageCauseRule> Causes)
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/damage.json";

    /// <summary>The file's format.</summary>
    public const int SchemaVersion = 1;

    /// <summary>The modifier ids that apply to a cause, in order; none for a cause the file does not list.</summary>
    /// <param name="cause">The cause's id.</param>
    public IReadOnlyList<string> ModifiersFor(string cause) => Causes.FirstOrDefault(c => string.Equals(c.Id, cause, StringComparison.Ordinal))?.Modifiers ?? [];

    /// <summary>Reads the damage data from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file is malformed.</exception>
    public static DamageData Load(ContentBundle bundle) => Parse(GameData.HostText(bundle, RelativePath));

    /// <summary>Builds the data from the file's text. The shape is checked here; the ids are <see cref="DamageValidator"/>'s.</summary>
    /// <param name="json">The contents of <c>game/damage.json</c>.</param>
    /// <exception cref="InvalidDataException">The text is malformed or a part is missing.</exception>
    public static DamageData Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != SchemaVersion)
            {
                throw new InvalidDataException($"{RelativePath} must have schemaVersion {SchemaVersion}.");
            }

            return new DamageData([.. root.GetProperty("causes").EnumerateArray().Select(static row => new DamageCauseRule(
                row.GetProperty("id").GetString() ?? string.Empty,
                [.. row.GetProperty("modifiers").EnumerateArray().Select(static m => m.GetString() ?? string.Empty)]))]);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }
}
