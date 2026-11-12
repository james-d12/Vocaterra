namespace Vocaterra.Game.Graphics.World;

/// <summary>
/// Which of three baked lights a chunk mesh was built in (E02): the sun's multiplier is in the vertex colours (the lanterns' glow is not: it is its own mesh, <see cref="LanternGlow"/>), so a mesh keeps the band it was
/// built in until it is built again. The renderer draws every chunk with a tint (<see cref="LightBands.Tint"/>) that brings its band's baked light to the light of the
/// moment, so the light moves smoothly through the day while the meshes are replaced one by one in the background.
/// </summary>
public enum LightBand
{
    /// <summary>The light of the day: the sun at full strength.</summary>
    Day,

    /// <summary>The low, warm light of dawn and dusk (what <c>--dusk</c> always showed).</summary>
    Dusk,

    /// <summary>The light of the night: the sun's baked light low and blue.</summary>
    Night,
}
