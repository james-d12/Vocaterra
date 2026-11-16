using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Items.Gear;

/// <summary>
/// The slot groups of <c>game/equipment.json</c> (I03a, inventory-plan 3.4): where gear is worn and what each place accepts. A bundle with no such file does not load; a simulation built without game
/// data uses <see cref="Empty"/> (nothing can be worn). <see cref="EquipmentValidator"/> checks the values and the items against them.
/// </summary>
public sealed class EquipmentData
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/equipment.json";

    /// <summary>The file's format.</summary>
    public const int SchemaVersion = 1;

    private readonly int[] _offsets;
    private EquipmentShape? _shape;

    /// <summary>The data of a list of groups.</summary>
    /// <param name="groups">The groups, in order; a slot's place in the commands, the digest and a save is its place in this order.</param>
    /// <param name="bagSlots">The inventory's slots with no bag worn (I03b); <see cref="Inventory.DefaultBaseSlots"/> when the data does not say.</param>
    public EquipmentData(IReadOnlyList<EquipmentGroup> groups, int bagSlots = Inventory.DefaultBaseSlots)
    {
        ArgumentNullException.ThrowIfNull(groups);
        Groups = groups;
        BagSlots = bagSlots;
        _offsets = new int[groups.Count + 1];
        for (var i = 0; i < groups.Count; i++)
        {
            _offsets[i + 1] = _offsets[i] + groups[i].Slots.Count;
        }
    }

    /// <summary>No groups: nothing can be worn.</summary>
    public static EquipmentData Empty { get; } = new([]);

    /// <summary>The groups, in the file's order.</summary>
    public IReadOnlyList<EquipmentGroup> Groups { get; }

    /// <summary>The inventory's slots with no bag worn (<c>bagSlots</c>, I03b): the hotbar and the rows under it. A worn bag adds its <see cref="ItemDefinition.Capacity"/>.</summary>
    public int BagSlots { get; }

    /// <summary>The paper doll's grid of these groups (I04), made once: the equipment panel's cells and their order, from the data alone.</summary>
    public EquipmentShape Shape => _shape ??= EquipmentShape.Of(this);

    /// <summary>The number of slots in every group together.</summary>
    public int SlotCount => _offsets[^1];

    /// <summary>Whether <paramref name="slot"/> names a slot of the data.</summary>
    /// <param name="slot">The slot.</param>
    public bool Contains(EquipSlot slot) => (uint)slot.Group < (uint)Groups.Count && (uint)slot.Index < (uint)Groups[slot.Group].Slots.Count;

    /// <summary>The slot's description.</summary>
    /// <param name="slot">A slot of the data (<see cref="Contains"/>).</param>
    /// <exception cref="ArgumentOutOfRangeException">There is no such slot.</exception>
    public EquipmentSlotSpec Spec(EquipSlot slot) => Contains(slot) ? Groups[slot.Group].Slots[slot.Index] : throw new ArgumentOutOfRangeException(nameof(slot), slot, "No such equipment slot.");

    /// <summary>The place of a slot in the flat list of every slot (group by group, in order).</summary>
    /// <param name="slot">A slot of the data.</param>
    public int FlatIndex(EquipSlot slot) => Contains(slot) ? _offsets[slot.Group] + slot.Index : throw new ArgumentOutOfRangeException(nameof(slot), slot, "No such equipment slot.");

    /// <summary>The slot at a place of the flat list.</summary>
    /// <param name="flat">The place, 0 up to <see cref="SlotCount"/>.</param>
    public EquipSlot SlotAt(int flat)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(flat);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(flat, SlotCount);
        var group = 0;
        while (_offsets[group + 1] <= flat)
        {
            group++;
        }

        return new EquipSlot(group, flat - _offsets[group]);
    }

    /// <summary>Every slot, group by group, in order.</summary>
    public IEnumerable<EquipSlot> AllSlots()
    {
        for (var g = 0; g < Groups.Count; g++)
        {
            for (var i = 0; i < Groups[g].Slots.Count; i++)
            {
                yield return new EquipSlot(g, i);
            }
        }
    }

    /// <summary>The index of the group whose key is <paramref name="id"/>, or -1.</summary>
    /// <param name="id">The group's key.</param>
    public int IndexOf(string id)
    {
        for (var i = 0; i < Groups.Count; i++)
        {
            if (string.Equals(Groups[i].Id, id, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Reads the data from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file is malformed.</exception>
    public static EquipmentData Load(ContentBundle bundle) => Parse(GameData.HostText(bundle, RelativePath));

    /// <summary>Builds the data from the file's text. The shape is checked here; the values are <see cref="EquipmentValidator"/>'s.</summary>
    /// <param name="json">The contents of <c>game/equipment.json</c>.</param>
    /// <exception cref="InvalidDataException">The text is malformed, a group has neither <c>slots</c> nor <c>count</c> (or both), or <c>labels</c> names a slot the group lacks.</exception>
    public static EquipmentData Parse(string json)
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

            return new EquipmentData(
                [.. root.GetProperty("groups").EnumerateArray().Select(ParseGroup)],
                root.TryGetProperty("bagSlots", out var bagSlots) ? bagSlots.GetInt32() : Inventory.DefaultBaseSlots);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    private static EquipmentGroup ParseGroup(JsonElement row)
    {
        var id = row.GetProperty("id").GetString() ?? string.Empty;
        var hasSlots = row.TryGetProperty("slots", out var slotNames);
        var hasCount = row.TryGetProperty("count", out var count);
        if (hasSlots == hasCount)
        {
            throw new InvalidDataException($"{RelativePath}: the group '{id}' needs either 'slots' (names) or 'count' (with 'wear'), not both and not neither.");
        }

        var slots = new List<EquipmentSlotSpec>();
        if (hasSlots)
        {
            var labels = row.TryGetProperty("labels", out var labelRow) && labelRow.ValueKind == JsonValueKind.Object
                ? labelRow.EnumerateObject().ToDictionary(static p => p.Name, static p => p.Value.GetString(), StringComparer.Ordinal)
                : new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var name in slotNames.EnumerateArray().Select(static e => e.GetString() ?? string.Empty))
            {
                slots.Add(new EquipmentSlotSpec(name, name, labels.GetValueOrDefault(name)));
            }

            var unknown = labels.Keys.FirstOrDefault(key => slots.TrueForAll(s => !string.Equals(s.Name, key, StringComparison.Ordinal)));
            if (unknown is not null)
            {
                throw new InvalidDataException($"{RelativePath}: the group '{id}' labels the slot '{unknown}', which it does not have.");
            }
        }
        else
        {
            var wear = row.GetProperty("wear").GetString() ?? string.Empty;
            var label = row.TryGetProperty("label", out var labelText) ? labelText.GetString() : null;
            var n = count.GetInt32();
            for (var i = 1; i <= n; i++)
            {
                slots.Add(new EquipmentSlotSpec(n == 1 ? wear : $"{wear}{i}", wear, label));
            }
        }

        return new EquipmentGroup(
            id,
            row.TryGetProperty("layer", out var layer) && layer.ValueKind == JsonValueKind.Number ? layer.GetInt32() : null,
            slots,
            [.. row.GetProperty("accepts").EnumerateArray().Select(static e => e.GetString() ?? string.Empty)],
            row.GetProperty("noDuplicates").GetBoolean(),
            row.GetProperty("statsApply").GetBoolean());
    }
}
