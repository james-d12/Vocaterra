namespace Vocaterra.Game.Core.Atmosphere;

/// <summary>A weather kind that may follow another, with its weight in the draw (E04).</summary>
/// <param name="Id">The kind's id.</param>
/// <param name="Weight">The weight, a positive whole number.</param>
public sealed record WeightedKind(string Id, int Weight);
