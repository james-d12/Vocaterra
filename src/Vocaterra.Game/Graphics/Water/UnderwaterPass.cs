using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Water;

namespace Vocaterra.Game.Graphics.Water;

/// <summary>
/// The extra work of the view under water, wrapped round the world's draw (H05). It does nothing at all above water (no target made, nothing drawn).
/// Under water with the blur off it only lays the blue-green tint over the frame. With the blur on, <see cref="Begin"/> sends the world into a render
/// target (with a depth buffer); <see cref="End"/> then makes a smaller copy with linear filtering (two steps for a quarter-size one, so no texel is skipped),
/// draws the full picture to the back buffer, draws the up-scaled copy over it four times, each offset a little, at the blur's weight, and lays the tint
/// over it all. No custom shader (mgfxc needs Wine on Linux): only the sprite batch and render targets. The layers are drawn afterwards, sharp.
/// </summary>
public sealed class UnderwaterPass : IDisposable
{
    private static readonly Vector2[] TapDirections = [new(-1, 0), new(1, 0), new(0, -1), new(0, 1)];

    private readonly GraphicsDevice _device;
    private readonly SpriteBatch _spriteBatch;
    private readonly Texture2D _pixel;
    private RenderTarget2D? _scene;
    private RenderTarget2D? _half;
    private RenderTarget2D? _quarter;
    private UnderwaterBlurSettings _settings;
    private bool _redirected;

    /// <summary>Creates the pass on a device, sharing the scene's sprite batch and its one-pixel texture. No target is made until the view is under water.</summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="spriteBatch">The sprite batch to composite with.</param>
    /// <param name="pixel">A white one-pixel texture (the tint is a stretched pixel).</param>
    public UnderwaterPass(GraphicsDevice device, SpriteBatch spriteBatch, Texture2D pixel)
    {
        _device = device;
        _spriteBatch = spriteBatch;
        _pixel = pixel;
    }

    /// <summary>Whether a render target is held now (false until the view first goes under water with the blur on).</summary>
    public bool HasTargets => _scene is not null;

    /// <summary>
    /// Call before the world is cleared and drawn. Above water (<paramref name="amount"/> 0) it does nothing. Under water with the blur on it makes (or
    /// resizes) the targets and sends the next draws into the scene target.
    /// </summary>
    /// <param name="amount">How far under water, 0 to 1.</param>
    /// <param name="blur">The blur setting.</param>
    public void Begin(float amount, UnderwaterBlur blur)
    {
        _redirected = false;
        if (amount <= 0f)
        {
            return;
        }

        _settings = UnderwaterBlurSettings.For(blur);
        if (!_settings.Enabled)
        {
            return;
        }

        var width = _device.PresentationParameters.BackBufferWidth;
        var height = _device.PresentationParameters.BackBufferHeight;
        EnsureTargets(width, height);
        _device.SetRenderTarget(_scene);
        _redirected = true;
    }

    /// <summary>
    /// Call after the world is drawn. Above water it does nothing. Under water it composites the blurred picture to the back buffer (when it was redirected)
    /// and then lays the tint over the frame.
    /// </summary>
    /// <param name="amount">How far under water, 0 to 1.</param>
    /// <param name="tintOpacity">The tint's opacity now (<see cref="UnderwaterView.TintOpacity"/>).</param>
    /// <param name="light">How much of the water's own colour the light of the moment leaves, 0 to 1 (E02: 1 by day, less at dusk, least at night, so the water is dark at night).</param>
    public void End(float amount, float tintOpacity, float light = 1f)
    {
        if (amount <= 0f)
        {
            return;
        }

        var width = _device.PresentationParameters.BackBufferWidth;
        var height = _device.PresentationParameters.BackBufferHeight;
        if (_redirected)
        {
            Composite(width, height);
            _redirected = false;
        }

        DrawTint(width, height, tintOpacity, light);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _scene?.Dispose();
        _half?.Dispose();
        _quarter?.Dispose();
        _scene = null;
        _half = null;
        _quarter = null;
    }

    private void EnsureTargets(int width, int height)
    {
        if (_scene is { } scene && scene.Width == width && scene.Height == height && (_settings.Downscale < 4 || _quarter is not null))
        {
            return;
        }

        Dispose();
        _scene = new RenderTarget2D(_device, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24, _device.PresentationParameters.MultiSampleCount, RenderTargetUsage.DiscardContents); // B22: antialiased like the window
        var (hw, hh) = UnderwaterGeometry.CopySize(width, height, 2);
        _half = new RenderTarget2D(_device, hw, hh, false, SurfaceFormat.Color, DepthFormat.None);
        if (_settings.Downscale >= 4)
        {
            var (qw, qh) = UnderwaterGeometry.CopySize(width, height, 4);
            _quarter = new RenderTarget2D(_device, qw, qh, false, SurfaceFormat.Color, DepthFormat.None);
        }
    }

    private void Composite(int width, int height)
    {
        var scene = _scene!;
        var half = _half!;

        // The smaller copies, each a linear-filtered draw of the one before.
        Blit(scene, half);
        var copy = half;
        if (_settings.Downscale >= 4 && _quarter is { } quarter)
        {
            Blit(half, quarter);
            copy = quarter;
        }

        _device.SetRenderTarget(null);
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        _spriteBatch.Draw(scene, new Rectangle(0, 0, width, height), Color.White);
        _spriteBatch.End();

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        var scale = new Vector2(width / (float)copy.Width, height / (float)copy.Height);
        var opacities = _settings.TapOpacities();
        for (var i = 0; i < TapDirections.Length; i++)
        {
            var colour = new Color(1f, 1f, 1f, opacities[i]);
            var position = TapDirections[i] * _settings.OffsetPixels;
            _spriteBatch.Draw(copy, position, null, colour, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        _spriteBatch.End();
    }

    private void Blit(Texture2D from, RenderTarget2D to)
    {
        _device.SetRenderTarget(to);
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
        _spriteBatch.Draw(from, new Rectangle(0, 0, to.Width, to.Height), Color.White);
        _spriteBatch.End();
    }

    /// <summary>Lays a deep orange tint over the frame while the eye is in lava (B10b); call after <see cref="End"/>. Nothing when <paramref name="opacity"/> is 0.</summary>
    public void DrawLavaTint(float opacity)
    {
        if (opacity <= 0f)
        {
            return;
        }

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, _device.PresentationParameters.BackBufferWidth, _device.PresentationParameters.BackBufferHeight), new Color(0.85f, 0.25f, 0.03f, opacity));
        _spriteBatch.End();
    }

    private void DrawTint(int width, int height, float opacity, float light)
    {
        if (opacity <= 0f)
        {
            return;
        }

        var water = UnderwaterView.WaterFog;
        var colour = new Color(water.Red * light, water.Green * light, water.Blue * light, opacity);
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.NonPremultiplied, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, width, height), colour);
        _spriteBatch.End();
    }
}
