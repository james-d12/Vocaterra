namespace Vocaterra.Game.Core.World.Layout;

/// <summary>
/// The undergrowth (S14): crossed plants set on the grass by the scene builder from <c>WorldRng</c> (the pass <c>undergrowth</c>). Every figure is a
/// percent of the columns that can hold a plant (open grass, not on a path, not beside a prop).
/// </summary>
/// <param name="Grass">The percent of open grass columns that hold tall grass or a fern at the densest, before the patches thin it.</param>
/// <param name="Fern">The percent of those plants that are ferns (more under the trees).</param>
/// <param name="Flowers">The percent of grass columns inside a flower patch that hold a flower.</param>
/// <param name="Reeds">The percent of the shore columns (grass or sand within two blocks of water) that hold reeds.</param>
/// <param name="RouteMargin">The columns round a path, a prop, a point or an NPC where nothing grows (and, one more, where only a third grows).</param>
public sealed record UndergrowthSpec(int Grass, int Fern, int Flowers, int Reeds, int RouteMargin)
{
    /// <summary>No undergrowth.</summary>
    public static UndergrowthSpec None { get; } = new(0, 0, 0, 0, 1);
}

