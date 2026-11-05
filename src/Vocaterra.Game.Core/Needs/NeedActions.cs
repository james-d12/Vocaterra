namespace Vocaterra.Game.Core.Needs;

/// <summary>What a try to eat or drink came to (N02).</summary>
public enum NeedActionResult
{
    /// <summary>Nothing to do: the held item is no food, or the crosshair is not on water within reach.</summary>
    Nothing,

    /// <summary>The food or the drink was taken: the meters rose and the food was used up (the bottle comes back empty).</summary>
    Done,

    /// <summary>The meter was already full: the food or the drink is kept and a line says so.</summary>
    Kept,
}
