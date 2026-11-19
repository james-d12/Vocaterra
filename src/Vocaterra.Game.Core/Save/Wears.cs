using Vocaterra.Game.Core.Items;

namespace Vocaterra.Game.Core.Save;

/// <summary>What a load makes of a saved wear (I07): it is kept for an item that has a durability and clamped into 0 up to that durability, so a save of content whose durability changed (or that lost it) still loads.</summary>
internal static class Wears
{
    /// <summary>The wear to restore for <paramref name="definition"/>.</summary>
    /// <param name="definition">The item.</param>
    /// <param name="saved">The saved wear.</param>
    public static int Clamp(ItemDefinition definition, int saved) => definition.Durability is { } durability ? Math.Clamp(saved, 0, durability.Max) : 0;
}
