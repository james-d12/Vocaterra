using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Vocaterra.Game.Core.World.Options;

/// <summary>
/// The terrain options one world was made with (Q02, D-game-37): the value of each option the player moved off its default, by option id. It holds only the values that differ from the rows' defaults, so the
/// defaults are the empty set whichever way they were written (<c>mountains=100</c> and nothing are the same world). The generator is a pure function of the seed and these options: <see cref="TerrainOptionData.Apply"/>
/// lays them over <c>game/world.json</c>'s numbers, <see cref="Key"/> names the world's far-view cache and the world's state digest includes them, and a save keeps them beside the seed. Only
/// <see cref="TerrainOptionData.Create"/> makes one (it checks the ids and the grid), so a <see cref="WorldOptions"/> always names options the content has.
/// </summary>
public sealed class WorldOptions : IEquatable<WorldOptions>
{
    private readonly KeyValuePair<string, int>[] _entries;
    private string? _canonical;
    private string? _key;

    private WorldOptions(KeyValuePair<string, int>[] entries)
    {
        _entries = entries;
        Values = new ReadOnlyDictionary<string, int>(entries.ToDictionary(static v => v.Key, static v => v.Value, StringComparer.Ordinal));
    }

    /// <summary>The defaults: no option moved. A world made with these is the world the shipped files describe, block for block.</summary>
    public static WorldOptions Default { get; } = new([]);

    /// <summary>The options off their defaults and their values, by id.</summary>
    public IReadOnlyDictionary<string, int> Values { get; }

    /// <summary>Whether every option is at its default.</summary>
    public bool IsDefault => _entries.Length == 0;

    /// <summary>The options off their defaults, sorted by id, as the save keeps them.</summary>
    public IReadOnlyList<KeyValuePair<string, int>> Entries => _entries;

    /// <summary>The options as text, sorted by id: <c>hills=50;mountains=150</c>; empty for the defaults. The same options always give the same text.</summary>
    public string Canonical => _canonical ??= string.Join(';', _entries.Select(static v => string.Create(CultureInfo.InvariantCulture, $"{v.Key}={v.Value}")));

    /// <summary>The SHA-256 of <see cref="Canonical"/> (UTF-8), 32 bytes: the stable digest of the options.</summary>
    public byte[] Digest => SHA256.HashData(Encoding.UTF8.GetBytes(Canonical));

    /// <summary>The digest as 64 lowercase hex characters.</summary>
    public string Key => _key ??= Convert.ToHexString(Digest).ToLowerInvariant();

    /// <summary>The value of an option that is off its default, or <see langword="null"/> when it is at the default.</summary>
    /// <param name="id">The option's id.</param>
    public int? Get(string id) => Values.TryGetValue(id, out var value) ? value : null;

    /// <summary>The set of these values (already off their defaults and checked: <see cref="TerrainOptionData.Create"/> is the caller).</summary>
    /// <param name="values">The options off their defaults.</param>
    internal static WorldOptions FromNormalised(IEnumerable<KeyValuePair<string, int>> values)
    {
        var sorted = values.OrderBy(static v => v.Key, StringComparer.Ordinal).ToArray();
        return sorted.Length == 0 ? Default : new WorldOptions(sorted);
    }

    /// <inheritdoc />
    public bool Equals(WorldOptions? other) => other is not null && string.Equals(Canonical, other.Canonical, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is WorldOptions other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Canonical);

    /// <inheritdoc />
    public override string ToString() => IsDefault ? "defaults" : Canonical;
}
