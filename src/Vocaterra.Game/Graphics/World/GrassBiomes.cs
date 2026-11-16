using System.Text.Json;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Graphics.World;

/// <summary>
/// The decorative grass's data (B18): which block kinds carry tufts on their tops and how lush each biome is, read from the embedded <c>GrassBiomes.json</c>. Render data only: it is
/// not part of the world's generation files, so no weight can move the state digest or a save. The weights are looked up by the id of the biome row of a column
/// (<c>IPlaceInfo.BiomeAt</c>), the same place the far view reads the biome from.
/// </summary>
public sealed class GrassBiomes
{
    private const string ResourceName = "Vocaterra.Game.GrassBiomes.json";

    private readonly Dictionary<string, float> _weights;
    private readonly float _other;
    private readonly float _unplaced;
    private readonly UInt128 _tops;

    private GrassBiomes(IEnumerable<BlockId> tops, Dictionary<string, float> weights, float other, float unplaced)
    {
        UInt128 mask = 0;
        foreach (var top in tops)
        {
            mask |= BlockKinds.Bit(top);
        }

        _tops = mask;
        _weights = weights;
        _other = other;
        _unplaced = unplaced;
    }

    /// <summary>The data the game ships (the embedded file).</summary>
    public static GrassBiomes Shipped { get; } = Load();

    /// <summary>The kinds that carry tufts as a kinds mask (one bit per block id), to ask a chunk's own mask whether it holds any.</summary>
    public UInt128 TopMask => _tops;

    /// <summary>Whether a block kind carries tufts on its top face.</summary>
    /// <param name="id">The kind.</param>
    public bool IsTop(BlockId id) => (_tops & BlockKinds.Bit(id)) != UInt128.Zero;

    /// <summary>The weight (0 to 1) of a biome: the share of its grass columns that carry a tuft at the slider's 100 percent. <see langword="null"/> is a column the world gives no biome for (the bounded valley).</summary>
    /// <param name="biomeId">The biome row's id, or <see langword="null"/>.</param>
    public float WeightOf(string? biomeId) => biomeId is null ? _unplaced : _weights.TryGetValue(biomeId, out var weight) ? weight : _other;

    /// <summary>Reads the data from a JSON text (the shape of <c>GrassBiomes.json</c>); a weight is limited to 0 to 1.</summary>
    /// <param name="json">The JSON text.</param>
    public static GrassBiomes Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var tops = root.GetProperty("tops").EnumerateArray().Select(static name => Enum.Parse<BlockId>(name.GetString()!, ignoreCase: true)).ToList();
        var weights = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var entry in root.GetProperty("weights").EnumerateObject())
        {
            weights[entry.Name] = Clamp(entry.Value.GetSingle());
        }

        return new GrassBiomes(tops, weights, Clamp(root.GetProperty("other").GetSingle()), Clamp(root.GetProperty("unplaced").GetSingle()));
    }

    private static float Clamp(float weight) => Math.Clamp(weight, 0f, 1f);

    private static GrassBiomes Load()
    {
        using var stream = typeof(GrassBiomes).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The embedded resource {ResourceName} is missing.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
