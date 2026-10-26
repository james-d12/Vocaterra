using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Vocaterra.Game.Graphics;

/// <summary>
/// Draws the sky behind the world (S14): a dome round the eye with a gradient (deep blue overhead, pale at the horizon, where it meets the fog), a square
/// sun with a stepped halo in the direction the face shading uses (<see cref="ChunkMesher.SunDirection"/>), and a layer of flat blocky clouds
/// (<see cref="CloudField"/>) that fade into the haze with distance. It writes no depth and is drawn before the world, so the world covers it.
/// </summary>
public sealed class SkyRenderer : IDisposable
{
    private const int Rings = 14;
    private const int Segments = 32;
    private const float DomeRadius = 100f;
    private const float SkyFarPlane = 2400f;

    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    private readonly VertexPositionColor[] _dome;
    private readonly short[] _domeIndices;
    private readonly VertexPositionColor[] _sun = new VertexPositionColor[4 * 3];
    private readonly short[] _sunIndices;
    private readonly List<CloudBox> _boxes = [];
    private VertexPositionColor[] _clouds = new VertexPositionColor[6 * 5 * 128];
    private readonly bool _dusk;

    /// <summary>Creates the sky; with <paramref name="dusk"/> (S15, <c>--dusk</c>) it is an amber and rose evening sky with a low orange sun.</summary>
    public SkyRenderer(GraphicsDevice device, bool dusk = false)
    {
        _device = device;
        _dusk = dusk;
        _effect = new BasicEffect(device)
        {
            TextureEnabled = false,
            VertexColorEnabled = true,
            LightingEnabled = false,
            FogEnabled = false,
        };
        (_dome, _domeIndices) = BuildDome(dusk);
        _sunIndices = new short[6 * 3];
        for (var q = 0; q < 3; q++)
        {
            var v = (short)(q * 4);
            var i = q * 6;
            _sunIndices[i] = v;
            _sunIndices[i + 1] = (short)(v + 1);
            _sunIndices[i + 2] = (short)(v + 2);
            _sunIndices[i + 3] = v;
            _sunIndices[i + 4] = (short)(v + 2);
            _sunIndices[i + 5] = (short)(v + 3);
        }
    }

    /// <summary>The number of vertices the last frame's clouds used (0 when clouds are off).</summary>
    public int CloudVertices { get; private set; }

    /// <summary>Draws the dome, the sun and (when <paramref name="clouds"/>) the clouds. The caller has cleared the target.</summary>
    public void Draw(FirstPersonCamera camera, long tick, bool clouds)
    {
        ArgumentNullException.ThrowIfNull(camera);
        _device.DepthStencilState = DepthStencilState.None;
        _device.RasterizerState = RasterizerState.CullNone;
        _device.BlendState = BlendState.NonPremultiplied;
        var aspect = _device.Viewport.AspectRatio;
        var projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(FirstPersonCamera.FieldOfViewDegrees), aspect, 1f, SkyFarPlane);

        // The dome and the sun are centred on the eye: a view with no move.
        _effect.World = Matrix.Identity;
        _effect.View = Matrix.CreateLookAt(Vector3.Zero, camera.Forward, camera.Up);
        _effect.Projection = projection;
        _effect.CurrentTechnique.Passes[0].Apply();
        _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _dome, 0, _dome.Length, _domeIndices, 0, _domeIndices.Length / 3);
        DrawSun(camera);

        CloudVertices = 0;
        if (clouds)
        {
            DrawClouds(camera, projection, tick);
        }

        _device.BlendState = BlendState.Opaque;
    }

    /// <inheritdoc />
    public void Dispose() => _effect.Dispose();

    private static Color ToColor((byte R, byte G, byte B) c, byte alpha = 255) => new(c.R, c.G, c.B, alpha);

    private static (VertexPositionColor[] Vertices, short[] Indices) BuildDome(bool dusk)
    {
        // Rings from 25 degrees below the horizon up to the zenith; every vertex takes the sky colour of its elevation.
        var vertices = new List<VertexPositionColor>();
        var indices = new List<short>();
        for (var r = 0; r <= Rings; r++)
        {
            // Closer rings near the horizon, where the gradient changes fastest.
            var t = r / (float)Rings;
            var elevation = -25f + (115f * MathF.Pow(t, 1.25f));
            var rad = MathHelper.ToRadians(Math.Min(elevation, 90f));
            var y = MathF.Sin(rad) * DomeRadius;
            var ring = MathF.Cos(rad) * DomeRadius;
            var colour = ToColor(dusk ? SkyPalette.AtDusk(elevation) : SkyPalette.At(elevation));
            for (var s = 0; s < Segments; s++)
            {
                var a = s * MathHelper.TwoPi / Segments;
                vertices.Add(new VertexPositionColor(new Vector3(MathF.Sin(a) * ring, y, -MathF.Cos(a) * ring), colour));
            }
        }

        for (var r = 0; r < Rings; r++)
        {
            for (var s = 0; s < Segments; s++)
            {
                var a = (r * Segments) + s;
                var b = (r * Segments) + ((s + 1) % Segments);
                var c = ((r + 1) * Segments) + s;
                var d = ((r + 1) * Segments) + ((s + 1) % Segments);
                indices.AddRange([(short)a, (short)b, (short)c, (short)b, (short)d, (short)c]);
            }
        }

        return (vertices.ToArray(), indices.ToArray());
    }

    // The sun: a bright square with two wider, fainter squares behind it (a blocky glow).
    private void DrawSun(FirstPersonCamera camera)
    {
        var sun = Vector3.Normalize(new Vector3(ChunkMesher.SunDirection.X, ChunkMesher.SunDirection.Y, ChunkMesher.SunDirection.Z));
        var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, sun));
        var up = Vector3.Cross(sun, right);
        var centre = sun * (DomeRadius * 0.96f);
        (float Half, Color Colour)[] squares =
        [
            (12f, _dusk ? new Color(255, 170, 90, 40) : new Color(255, 250, 220, 28)),
            (8.5f, _dusk ? new Color(255, 176, 96, 90) : new Color(255, 250, 214, 66)),
            (5f, _dusk ? new Color(255, 214, 150, 255) : new Color(255, 252, 232, 255)),
        ];
        for (var q = 0; q < squares.Length; q++)
        {
            var (half, colour) = squares[q];
            var i = q * 4;
            _sun[i] = new VertexPositionColor(centre - (right * half) - (up * half), colour);
            _sun[i + 1] = new VertexPositionColor(centre + (right * half) - (up * half), colour);
            _sun[i + 2] = new VertexPositionColor(centre + (right * half) + (up * half), colour);
            _sun[i + 3] = new VertexPositionColor(centre - (right * half) + (up * half), colour);
        }

        _effect.CurrentTechnique.Passes[0].Apply();
        _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _sun, 0, _sun.Length, _sunIndices, 0, _sunIndices.Length / 3);
    }

    private void DrawClouds(FirstPersonCamera camera, Matrix projection, long tick)
    {
        CloudField.Generate(camera.Eye.X, camera.Eye.Z, tick, _boxes);
        var needed = _boxes.Count * 5 * 6;
        if (_clouds.Length < needed)
        {
            _clouds = new VertexPositionColor[needed];
        }

        var count = 0;
        var eye = camera.Eye;
        foreach (var box in _boxes)
        {
            var centre = new Vector3(box.X + (box.Width / 2f), box.Y, box.Z + (box.Depth / 2f));
            var distance = Vector3.Distance(centre, eye);
            // Far clouds melt into the haze: colour towards the fog's, and fainter at the very edge of the field.
            var haze = Math.Clamp((distance - 150f) / 520f, 0f, 0.85f);
            var fade = (byte)(255f * Math.Clamp(1.6f - (distance / (CloudField.Reach * CloudField.CellSize)), 0f, 1f));
            var horizon = _dusk ? SkyPalette.DuskHorizon : SkyPalette.Horizon;
            var top = ToColor(SkyPalette.Blend(_dusk ? SkyPalette.DuskCloudTop : SkyPalette.CloudTop, horizon, haze), fade);
            var side = ToColor(SkyPalette.Blend(_dusk ? SkyPalette.DuskCloudSide : SkyPalette.CloudSide, horizon, haze), fade);
            var bottom = ToColor(SkyPalette.Blend(_dusk ? SkyPalette.DuskCloudBottom : SkyPalette.CloudBottom, horizon, haze), fade);
            float x0 = box.X, x1 = box.X + box.Width, z0 = box.Z, z1 = box.Z + box.Depth, y0 = box.Y, y1 = box.Y + box.Thickness;
            count = Quad(count, new Vector3(x0, y1, z1), new Vector3(x1, y1, z1), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), top);
            count = Quad(count, new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1), bottom);
            count = Quad(count, new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), side);
            count = Quad(count, new Vector3(x1, y0, z0), new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), side);
            count = Quad(count, new Vector3(x1, y0, z1), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), side);
        }

        if (count == 0)
        {
            return;
        }

        _effect.World = Matrix.Identity;
        _effect.View = camera.View;
        _effect.Projection = projection;
        _effect.CurrentTechnique.Passes[0].Apply();
        _device.DrawUserPrimitives(PrimitiveType.TriangleList, _clouds, 0, count / 3);
        CloudVertices = count;
    }

    private int Quad(int count, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color colour)
    {
        _clouds[count++] = new VertexPositionColor(a, colour);
        _clouds[count++] = new VertexPositionColor(b, colour);
        _clouds[count++] = new VertexPositionColor(c, colour);
        _clouds[count++] = new VertexPositionColor(a, colour);
        _clouds[count++] = new VertexPositionColor(c, colour);
        _clouds[count++] = new VertexPositionColor(d, colour);
        return count;
    }
}
