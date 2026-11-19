namespace Vocaterra.Game.Core.Items;

/// <summary>
/// How the hand shows an item (<c>held</c> in <c>game/items.json</c>, I01): the kinds the front-end has a model for. The core only names them; <c>Vocaterra.Game</c> draws them.
/// </summary>
public static class ItemHeld
{
    /// <summary>The item's icon extruded into voxels (the default; a berry, a coin, the book).</summary>
    public const string Sprite = "sprite";

    /// <summary>A log built of trunk blocks.</summary>
    public const string Log = "log";

    /// <summary>A torch built of blocks, with its flame.</summary>
    public const string Torch = "torch";

    /// <summary>A glass bottle (its icon extruded into voxels).</summary>
    public const string Bottle = "bottle";

    /// <summary>Every held kind the data may name.</summary>
    public static IReadOnlyList<string> All { get; } = [Sprite, Log, Torch, Bottle];
}
