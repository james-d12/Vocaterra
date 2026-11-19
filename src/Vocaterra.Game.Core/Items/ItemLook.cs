namespace Vocaterra.Game.Core.Items;

/// <summary>
/// How a piece of gear looks on the body (I03a): a colour for each body part it covers, by open part key (<c>head</c>, <c>body</c>, <c>legs</c>, <c>feet</c>, <c>sleeve</c>...). Read and checked
/// at load; I09 draws it on the player (<see cref="Gear.AppearanceBuilder"/>). The parts are sorted by key, so every machine lists them the same way.
/// </summary>
/// <param name="Parts">The colour of each part the item covers.</param>
public sealed record ItemLook(IReadOnlyDictionary<string, LookColour> Parts);
