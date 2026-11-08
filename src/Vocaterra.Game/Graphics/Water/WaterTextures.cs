using Microsoft.Xna.Framework.Graphics;

namespace Vocaterra.Game.Graphics.Water;

/// <summary>
/// The water's animated art as textures, outside the block atlas so bread-pilot's atlas cells do not move (water-plan 4.8): one texture for each of the
/// <see cref="WaterFrames.Count"/> frames, made once from <see cref="WaterArt"/>. The translucent pass binds <see cref="FrameAt"/> for the tick, so the
/// whole of the water moves with no shader and no clock.
/// </summary>
public sealed class WaterTextures : IDisposable
{
    /// <summary>
    /// The sampler of the frames: trilinear, so far water is smooth instead of a moire of ripples, down to the level where a texel is one whole tile (the tiles carry
    /// their own gutter, so a turned window never reads a neighbour), clamped.
    /// </summary>
    public static readonly SamplerState Sampler = new()
    {
        Filter = TextureFilter.Linear,
        AddressU = TextureAddressMode.Clamp,
        AddressV = TextureAddressMode.Clamp,
        MaxMipLevel = WaterTileLayout.MaxMipLevel,
    };

    private readonly Texture2D[] _frames;

    /// <summary>Builds the frames on the device.</summary>
    public WaterTextures(GraphicsDevice device)
    {
        _frames = new Texture2D[WaterFrames.Count];
        for (var i = 0; i < _frames.Length; i++)
        {
            var texture = new Texture2D(device, WaterTileLayout.Width, WaterTileLayout.Height, true, SurfaceFormat.Color);
            var pixels = WaterArt.Frame(i);
            int width = WaterTileLayout.Width, height = WaterTileLayout.Height;
            for (var level = 0; level < texture.LevelCount; level++)
            {
                texture.SetData(level, null, pixels, 0, pixels.Length);
                if (level + 1 < texture.LevelCount)
                {
                    pixels = WaterArt.HalfSize(pixels, width, height);
                    (width, height) = (Math.Max(1, width / 2), Math.Max(1, height / 2));
                }
            }

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
