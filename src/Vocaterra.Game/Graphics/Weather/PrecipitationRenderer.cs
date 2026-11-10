using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Camera;

namespace Vocaterra.Game.Graphics.Weather;

/// <summary>
/// The rain's pass (E06): the streaks of <see cref="PrecipitationField"/> as camera-facing quads in one reused dynamic vertex buffer (a fixed array and a fixed index buffer, so a frame
/// allocates nothing), drawn with <see cref="BasicEffect"/> and vertex colour, alpha blend, depth read and not written. It is fogged like the world (the world's fog colour and distances
/// are handed in), lit by the look (the colour is the style's times the sun's light, with a floor so night rain shows), and fades with the under-water amount: not drawn when the eye is
/// under the surface. It draws nothing for an unknown kind, nothing at an intensity of 0, and nothing when the quality's budget is 0.
/// </summary>
public sealed class PrecipitationRenderer : IDisposable
{
    /// <summary>The share of the sun's light the rain keeps at the least: even by night the streaks are not black.</summary>
    public const float LightFloor = 0.3f;

    private const float LightCurve = 0.7f; // the light's power: 1 would follow the world's light, which leaves the rain dark against a dark village

    private const float NarrowRange = 6f; // the distance from the eye (blocks) from which a streak has the style's full width
    private const float NarrowestShare = 0.3f; // the share of its width a streak at the lens keeps

    private const float TailAlpha = 0.12f; // the share of its head's opacity a streak's tail keeps: the streak fades out up its line

    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    private readonly DynamicVertexBuffer _vertices;
    private readonly IndexBuffer _indices;
    private readonly VertexPositionColor[] _scratch = new VertexPositionColor[PrecipitationField.MaxDrops * 4];
    private readonly Streak[] _streaks = new Streak[PrecipitationField.MaxDrops];
    private readonly PrecipitationField _field;

    /// <summary>Creates the pass for a grid's world.</summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="grid">The world: the cover of its columns stops the drops.</param>
    /// <param name="log">Where an unknown precipitation kind is reported once.</param>
    public PrecipitationRenderer(GraphicsDevice device, VoxelGrid grid, Action<string>? log = null)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _field = new PrecipitationField(new ColumnCover(grid), log);
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            TextureEnabled = false,
            LightingEnabled = false,
            FogEnabled = true,
        };
        _vertices = new DynamicVertexBuffer(device, VertexPositionColor.VertexDeclaration, _scratch.Length, BufferUsage.WriteOnly);
        var indices = new short[PrecipitationField.MaxDrops * 6];
        for (var q = 0; q < PrecipitationField.MaxDrops; q++)
        {
            var v = (short)(q * 4);
            var i = q * 6;
            indices[i] = v;
            indices[i + 1] = (short)(v + 1);
            indices[i + 2] = (short)(v + 2);
            indices[i + 3] = (short)(v + 2);
            indices[i + 4] = (short)(v + 1);
            indices[i + 5] = (short)(v + 3);
        }

        _indices = new IndexBuffer(device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
        _indices.SetData(indices);
    }

    /// <summary>How many vertices the last <see cref="Draw"/> sent (four to a drop).</summary>
    public int DrawnVertices { get; private set; }

    /// <summary>
    /// Draws the rain of this frame over the world drawn so far. The caller has drawn the opaque world and the water, and leaves the blend state (alpha) and the depth state (read only) as
    /// the water pass set them; the rasterizer state is set to draw both faces here and is given back by the caller.
    /// </summary>
    /// <param name="camera">The camera of the frame.</param>
    /// <param name="tick">The simulation's tick.</param>
    /// <param name="state">What falls: kind, intensity and wind.</param>
    /// <param name="dropBudget">The quality's drop budget.</param>
    /// <param name="sunLight">The look's sun light (the colour the world is lit by); the rain is dimmed by it.</param>
    /// <param name="fogColour">The world's fog colour now (the under-water blend included).</param>
    /// <param name="fogStart">Where the world's fog starts.</param>
    /// <param name="fogEnd">Where the world's fog is complete.</param>
    /// <param name="underwater">How far under water the view is, 0 to 1: the rain fades out with it and is not drawn at 1.</param>
    public void Draw(FirstPersonCamera camera, long tick, in PrecipitationState state, int dropBudget, Vector3 sunLight, Vector3 fogColour, float fogStart, float fogEnd, float underwater)
    {
        DrawnVertices = 0;
        var visible = 1f - Math.Clamp(underwater, 0f, 1f);
        if (visible <= 0.001f)
        {
            return;
        }

        var eye = camera.Eye;
        var count = _field.Fill(_streaks, eye.X, eye.Y, eye.Z, tick, state, dropBudget, out var style);
        if (count == 0 || style is null)
        {
            return;
        }

        var tint = new Vector3(
            style.Colour.R / 255f * Lit(sunLight.X),
            style.Colour.G / 255f * Lit(sunLight.Y),
            style.Colour.B / 255f * Lit(sunLight.Z));
        var half = style.Width / 2f;
        var quads = 0;
        for (var i = 0; i < count; i++)
        {
            var s = _streaks[i];
            var head = new Vector3(s.HeadX, s.HeadY, s.HeadZ);
            var tail = new Vector3(s.TailX, s.TailY, s.TailZ);
            var along = tail - head;
            var side = Vector3.Cross(along, eye - head);
            var length = side.Length();
            if (length < 1e-5f)
            {
                continue; // looking straight down the streak: it is a point
            }

            // A streak near the lens is narrowed (a drop a metre away would be a bar across the picture); from six blocks out it has the style's width.
            side *= half * Math.Clamp(Vector3.Distance(head, eye) / NarrowRange, NarrowestShare, 1f) / length;
            var headAlpha = s.Alpha * visible;
            var headColour = new Color(tint.X, tint.Y, tint.Z, headAlpha);
            var tailColour = new Color(tint.X, tint.Y, tint.Z, headAlpha * TailAlpha);
            var v = quads * 4;
            _scratch[v] = new VertexPositionColor(head - side, headColour);
            _scratch[v + 1] = new VertexPositionColor(head + side, headColour);
            _scratch[v + 2] = new VertexPositionColor(tail - side, tailColour);
            _scratch[v + 3] = new VertexPositionColor(tail + side, tailColour);
            quads++;
        }

        if (quads == 0)
        {
            return;
        }

        _vertices.SetData(_scratch, 0, quads * 4, SetDataOptions.Discard);
        _effect.World = Matrix.Identity;
        _effect.View = camera.View;
        _effect.Projection = camera.Projection;
        _effect.FogColor = fogColour;
        _effect.FogStart = fogStart;
        _effect.FogEnd = fogEnd;
        _effect.CurrentTechnique.Passes[0].Apply();
        _device.RasterizerState = RasterizerState.CullNone;
        _device.SetVertexBuffer(_vertices);
        _device.Indices = _indices;
        _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, quads * 2);
        DrawnVertices = quads * 4;
    }

    // The share of the style's colour a channel of the sun's light leaves: dimmer with the light but not in step with it (a streak is seen against the haze, which the same light dims), and never under the floor.
    private static float Lit(float sun) => MathF.Pow(Math.Max(sun, LightFloor), LightCurve);

    /// <inheritdoc />
    public void Dispose()
    {
        _vertices.Dispose();
        _indices.Dispose();
        _effect.Dispose();
    }
}
