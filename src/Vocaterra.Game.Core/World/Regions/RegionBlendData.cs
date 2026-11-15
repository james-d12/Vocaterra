using System.Text.Json;

namespace Vocaterra.Game.Core.World.Regions;

/// <summary>
/// How the procedural land is eased towards the edge of an authored region (the <c>regionBlend</c> section of <c>game/world.json</c>, B06): lengths are blocks, field values thousandths of
/// their range. It is a section of its own so that tuning the terrain (the other sections) never touches these numbers.
/// </summary>
/// <param name="Margin">How far outside a region's edge, in blocks, the ground is eased towards the edge's height (the blend's width).</param>
/// <param name="TreeClearance">How many columns outside the edge keep no trunk, so a crown never crosses into the region.</param>
/// <param name="LandBias">The pull of the continentalness towards land round a region.</param>
/// <param name="ExitCorridors">The walkable ways out: a road that continues beyond an exit of a region, at a height of its own, for a short stretch.</param>
/// <param name="EdgeSmoothing">How the edge's own roughness is smoothed away from the border (<see cref="EdgeSmoothingSpec.Default"/> when <see langword="null"/>).</param>
public sealed record RegionBlendData(int Margin, int TreeClearance, LandBiasSpec LandBias, IReadOnlyList<ExitCorridorSpec> ExitCorridors, EdgeSmoothingSpec? EdgeSmoothing = null)
{
    /// <summary>The edge smoothing: the file's, or the default.</summary>
    public EdgeSmoothingSpec Smoothing => EdgeSmoothing ?? EdgeSmoothingSpec.Default;

    /// <summary>The section's name in <c>game/world.json</c>.</summary>
    public const string SectionName = "regionBlend";

    /// <summary>The numbers a world file with no <c>regionBlend</c> section gets (no corridor).</summary>
    public static RegionBlendData Default { get; } = new(64, 3, new LandBiasSpec(100, -120, 512), []);

    /// <summary>Reads the section of a parsed <c>game/world.json</c>, or <see cref="Default"/> when the file has none.</summary>
    /// <param name="root">The file's root object.</param>
    /// <exception cref="KeyNotFoundException">A part of the section is missing.</exception>
    internal static RegionBlendData Read(JsonElement root)
    {
        if (!root.TryGetProperty(SectionName, out var section))
        {
            return Default;
        }

        var bias = section.GetProperty("landBias");
        var corridors = new List<ExitCorridorSpec>();
        if (section.TryGetProperty("exitCorridors", out var list))
        {
            foreach (var item in list.EnumerateArray())
            {
                corridors.Add(new ExitCorridorSpec(
                    item.GetProperty("id").GetString() ?? string.Empty,
                    item.GetProperty("fromX").GetInt32(),
                    item.GetProperty("fromZ").GetInt32(),
                    item.GetProperty("toX").GetInt32(),
                    item.GetProperty("toZ").GetInt32(),
                    item.GetProperty("halfWidth").GetInt32(),
                    item.GetProperty("shoulder").GetInt32(),
                    item.GetProperty("fade").GetInt32()));
            }
        }

        return new RegionBlendData(
            section.GetProperty("margin").GetInt32(),
            section.GetProperty("treeClearance").GetInt32(),
            new LandBiasSpec(bias.GetProperty("floor").GetInt32(), bias.GetProperty("shoreFloor").GetInt32(), bias.GetProperty("radius").GetInt32()),
            corridors,
            section.TryGetProperty("edgeSmoothing", out var smoothing)
                ? new EdgeSmoothingSpec(smoothing.GetProperty("blurRadius").GetInt32(), smoothing.GetProperty("ease").GetInt32())
                : EdgeSmoothingSpec.Default);
    }
}

/// <summary>
/// The pull of the continentalness towards land round a region: within <see cref="Radius"/> blocks of its edge the continentalness is never below a floor that falls in a straight line from
/// <see cref="Floor"/> at the edge to <see cref="ShoreFloor"/> at the radius (and goes on falling at the same rate beyond it), so no sea opens at the region's feet and the coast, when it comes,
/// comes with a shelf.
/// </summary>
/// <param name="Floor">The least continentalness, in thousandths, at the region's edge.</param>
/// <param name="ShoreFloor">The least continentalness, in thousandths, at <paramref name="Radius"/> blocks out: a little inland of the sea.</param>
/// <param name="Radius">The distance, in blocks, over which the floor falls from <paramref name="Floor"/> to <paramref name="ShoreFloor"/>.</param>
public sealed record LandBiasSpec(int Floor, int ShoreFloor, int Radius);

/// <summary>
/// A walkable way out of a region: a straight road from (<see cref="FromX"/>, <see cref="FromZ"/>), at the region's edge, to (<see cref="ToX"/>, <see cref="ToZ"/>) outside it. Its ground starts
/// at the region's edge height and follows the land beyond at no more than one block of rise a column, <see cref="HalfWidth"/> blocks each side of the line, with a bank of <see cref="Shoulder"/>
/// blocks to the land beside it; over the last <see cref="Fade"/> blocks it gives way to the land.
/// </summary>
/// <param name="Id">The corridor's name (for the author's own reference; it is not used in the game's words).</param>
/// <param name="FromX">The start's x, at the region's edge.</param>
/// <param name="FromZ">The start's z.</param>
/// <param name="ToX">The end's x.</param>
/// <param name="ToZ">The end's z.</param>
/// <param name="HalfWidth">The road's half width in blocks (a width of 2 x halfWidth + 1).</param>
/// <param name="Shoulder">The blocks over which the bank eases from the road to the land beside it.</param>
/// <param name="Fade">The blocks at the far end over which the road gives way to the land.</param>
public sealed record ExitCorridorSpec(string Id, int FromX, int FromZ, int ToX, int ToZ, int HalfWidth, int Shoulder, int Fade);

/// <summary>
/// How the blend keeps a region's rough edge from combing the land outside into ridges that run straight out from it: the ground the margin eases to is the edge's own height at the border
/// itself, and over <see cref="Ease"/> blocks it gives way to the edge's height blurred over <see cref="BlurRadius"/> columns (the region's ground averaged in a square of 2 x radius + 1).
/// </summary>
/// <param name="BlurRadius">The blur's radius in columns; 0 for none.</param>
/// <param name="Ease">The blocks over which the target goes from the edge's own height to its blurred height; at least 1.</param>
public sealed record EdgeSmoothingSpec(int BlurRadius, int Ease)
{
    /// <summary>The numbers a world file with no <c>edgeSmoothing</c> gets.</summary>
    public static EdgeSmoothingSpec Default { get; } = new(16, 16);
}
