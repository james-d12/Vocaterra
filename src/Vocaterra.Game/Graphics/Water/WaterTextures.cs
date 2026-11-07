using Microsoft.Xna.Framework.Graphics;

namespace Vocaterra.Game.Graphics.Water;

/// <summary>
/// The water's animated art as textures, outside the block atlas so bread-pilot's atlas cells do not move (water-plan 4.8): one texture for each of the
/// <see cref="WaterFrames.Count"/> frames, made once from <see cref="WaterArt"/>. The translucent pass binds <see cref="FrameAt"/> for the tick, so the
/// whole of the water moves with no shader and no clock.
/// </summary>
public sealed class WaterTextures : IDisposable
{
    /// <summary>The sampler of the frames: bilinear, clamped (the tiles carry their own gutter, so a turned window never reads a neighbour).</summary>
    public static readonly SamplerState Sampler = SamplerState.LinearClamp;

    private readonly Texture2D[] _frames;

    /// <summary>Builds the frames on the device.</summary>
    public WaterTextures(GraphicsDevice device)
    {
        _frames = new Texture2D[WaterFrames.Count];
        for (var i = 0; i < _frames.Length; i++)
        {
            var texture = new Texture2D(device, WaterTileLayout.Width, WaterTileLayout.Height, false, SurfaceFormat.Color);
            texture.SetData(WaterArt.Frame(i));
            _frames[i] = texture;
        }
    }

    /// <summary>The frame texture for a simulation tick.</summary>
    public Texture2D FrameAt(long tick) => _frames[WaterFrames.IndexAt(tick)];

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var frame in _frames)
        {
            frame.Dispose();
        }
    }
}
