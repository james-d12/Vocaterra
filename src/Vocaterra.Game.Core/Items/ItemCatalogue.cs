using System.Text.Json;
using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Items;

/// <summary>
/// The item kinds of <c>game/items.json</c> (I01), loaded once by <see cref="GameData"/>. The loader fails loudly: a row that is malformed, a name or an id used twice, an
/// <see cref="ItemId"/> member with no row of the same id and name, or a row of a member's id with another name stops the load with a message that names the row.
/// </summary>
public sealed partial class ItemCatalogue : IItemCatalogue
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/items.json";

    /// <summary>The file's format.</summary>
    public const int SchemaVersion = 1;

    private readonly ItemDefinition[] _all;
    private readonly Dictionary<ItemId, ItemDefinition> _byId;
    private readonly Dictionary<string, ItemDefinition> _byName;

    private ItemCatalogue(ItemDefinition[] all)
    {
        _all = all;
        _byId = all.ToDictionary(static d => d.Id);
        _byName = all.ToDictionary(static d => d.Name, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public IReadOnlyList<ItemDefinition> All => _all;

    /// <summary>Reads the catalogue from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file does not describe the items.</exception>
    public static ItemCatalogue Load(ContentBundle bundle) => Parse(GameData.HostText(bundle, RelativePath));

    /// <summary>Builds the catalogue from the file's text.</summary>
    /// <param name="json">The contents of <c>game/items.json</c>.</param>
    /// <exception cref="InvalidDataException">The text is malformed or the rows do not hold together (see <see cref="Create"/>).</exception>
    public static ItemCatalogue Parse(string json)
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

            var rows = new List<ItemDefinition>();
            foreach (var element in root.GetProperty("items").EnumerateArray())
            {
                rows.Add(Row(element));
            }

            return Create(rows);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {e.Message}", e);
        }
    }

    /// <summary>Builds the catalogue from rows and checks them (the loader's own checks; a test's catalogue passes through here too).</summary>
    /// <param name="rows">The rows, in catalogue order.</param>
    /// <exception cref="InvalidDataException">A name is not a lowercase key or is used twice, an id is used twice, a stack limit is below one, an icon is empty, a held kind is unknown,
    /// or an <see cref="ItemId"/> member has no row with its id and name.</exception>
    public static ItemCatalogue Create(IEnumerable<ItemDefinition> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var all = rows.ToArray();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<ItemId>();
        foreach (var row in all)
        {
            if (!NamePattern().IsMatch(row.Name))
            {
                throw new InvalidDataException($"{RelativePath}: the item name '{row.Name}' is not a lowercase key (letters, digits and hyphens).");
            }

            if (!names.Add(row.Name))
            {
                throw new InvalidDataException($"{RelativePath}: the item name '{row.Name}' is used twice.");
            }

            if (!ids.Add(row.Id))
            {
                throw new InvalidDataException($"{RelativePath}: the id {(ushort)row.Id} of '{row.Name}' is used twice.");
            }

            if (row.MaxStack < 1)
            {
                throw new InvalidDataException($"{RelativePath}: '{row.Name}' has a stack limit of {row.MaxStack}; it must be at least 1.");
            }

            if (string.IsNullOrEmpty(row.Icon))
            {
                throw new InvalidDataException($"{RelativePath}: '{row.Name}' has no icon key.");
            }

            if (!ItemHeld.All.Contains(row.Held))
            {
                throw new InvalidDataException($"{RelativePath}: '{row.Name}' is held as '{row.Held}'; the kinds are {string.Join(", ", ItemHeld.All)}.");
            }

            if (row.Tool is { } tool && (tool.Speed < 1 || tool.Yield < 1))
            {
                throw new InvalidDataException($"{RelativePath}: the tool of '{row.Name}' needs a speed and a yield of at least 1.");
            }
        }

        var catalogue = new ItemCatalogue(all);
        foreach (var member in Enum.GetValues<ItemId>())
        {
            var expected = KeyOf(member);
            var row = catalogue.Find(member)
                ?? throw new InvalidDataException($"{RelativePath} has no row for the item {member} (id {(ushort)member}, name '{expected}'), which the code refers to.");
            if (!string.Equals(row.Name, expected, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{RelativePath}: the row of id {(ushort)member} is named '{row.Name}', and the code's item {member} is '{expected}'.");
            }
        }

        return catalogue;
    }

    /// <summary>The key an <see cref="ItemId"/> member must have in the data: its name in lowercase with hyphens (<c>WaterBottle</c> is <c>water-bottle</c>).</summary>
    /// <param name="member">The member.</param>
    public static string KeyOf(ItemId member) => Words().Replace(member.ToString(), "$1-$2").ToLowerInvariant();

    /// <inheritdoc />
    public ItemDefinition Get(ItemId id) =>
        _byId.TryGetValue(id, out var definition) ? definition : throw new ArgumentOutOfRangeException(nameof(id), id, "No item definition for this id.");

    /// <inheritdoc />
    public ItemDefinition? Find(ItemId id) => _byId.GetValueOrDefault(id);

    /// <inheritdoc />
    public ItemDefinition? Find(string name) => name is not null && _byName.TryGetValue(name, out var definition) ? definition : null;

    private static ItemDefinition Row(JsonElement element)
    {
        var name = element.GetProperty("name").GetString() ?? throw new InvalidDataException($"{RelativePath}: an item has no name.");
        var id = element.GetProperty("id").GetInt32();
        if (id is < 0 or > ushort.MaxValue)
        {
            throw new InvalidDataException($"{RelativePath}: the id {id} of '{name}' is not a number from 0 to {ushort.MaxValue}.");
        }

        var held = element.TryGetProperty("held", out var heldElement) ? heldElement.GetString() ?? string.Empty : ItemHeld.Sprite;
        IReadOnlyList<string> tags = element.TryGetProperty("tags", out var tagsElement)
            ? [.. tagsElement.EnumerateArray().Select(static t => t.GetString() ?? string.Empty)]
            : [];
        ToolSpec? tool = null;
        if (element.TryGetProperty("tool", out var toolElement) && toolElement.ValueKind == JsonValueKind.Object)
        {
            var className = toolElement.GetProperty("class").GetString() ?? string.Empty;
            var material = Enum.GetValues<Material>().Where(static m => m != Material.None).Cast<Material?>()
                .FirstOrDefault(m => string.Equals(m!.Value.ToString(), className, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"{RelativePath}: the tool of '{name}' names an unknown material '{className}'.");
            tool = new ToolSpec(material, toolElement.GetProperty("speed").GetInt32(), toolElement.TryGetProperty("yield", out var yield) ? yield.GetInt32() : 1);
        }

        return new ItemDefinition(
            (ItemId)id,
            name,
            element.GetProperty("stack").GetInt32(),
            element.GetProperty("icon").GetString() ?? string.Empty,
            held,
            tags,
            tool);
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex NamePattern();

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex Words();
}
