using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.Camera;

namespace Vocaterra.Game.Graphics.Sky;

/// <summary>
/// Draws the sky behind the world (S14, E02) from a <see cref="SkyLook"/>: a dome round the eye with a gradient (by day deep blue overhead and pale at the horizon, where it meets
/// the fog), square stars that fade in with the night, a square sun with a stepped halo that follows its arc over the day (<see cref="SkyArc"/>), a square moon in the night sky, and
/// a layer of flat blocky clouds (<see cref="CloudField"/>) that fade into the haze with distance. The dome is coloured again only when the look's three dome colours have moved,
/// in place, so a frame allocates nothing for the sky. It writes no depth and is drawn before the world, so the world covers it.
/// </summary>
public sealed class SkyRenderer : IDisposable
{
    private const int Rings = 14;
    private const int Segments = 32;
    private const float DomeRadius = 100f;
    private const float SkyFarPlane = 2400f;

    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    private const int StarCount = 150;
    private const int BodyQuads = 7; // the sun's three squares and the moon's four

    private readonly VertexPositionColor[] _dome;
    private readonly short[] _domeIndices;
    private readonly float[] _ringElevation = new float[Rings + 1];
    private readonly VertexPositionColor[] _bodies = new VertexPositionColor[4 * BodyQuads];
    private readonly short[] _bodyIndices;
    private readonly VertexPositionColor[] _stars = new VertexPositionColor[4 * StarCount];
    private readonly byte[] _starBrightness = new byte[StarCount];
    private readonly short[] _starIndices;
    private readonly List<CloudBox> _boxes = [];
    private VertexPositionColor[] _clouds = new VertexPositionColor[6 * 5 * 128];
    private SkyLook _look;
    private bool _domeColoured;
    private float _starAmount = -1f;
    private readonly VertexPositionColor[] _haze = new VertexPositionColor[4];
    private readonly short[] _hazeIndices = [0, 1, 2, 0, 2, 3];
    private Color _hazeColour = Color.Transparent;
    private float _hazeAmount;

    /// <summary>Creates the sky in the colours of a look (<see cref="SetLook"/> changes them as the day goes on).</summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="look">The look to start with.</param>
    public SkyRenderer(GraphicsDevice device, in SkyLook look)
    {
        _device = device;
        _look = look;
        _effect = new BasicEffect(device)
        {
            TextureEnabled = false,
            VertexColorEnabled = true,
            LightingEnabled = false,
            FogEnabled = false,
        };
        (_dome, _domeIndices) = BuildDome(_ringElevation);
        _bodyIndices = QuadIndices(BodyQuads);
        _starIndices = QuadIndices(StarCount);
        BuildStars();
        Recolour();
    }

    /// <summary>The look the sky is drawn in now.</summary>
    public SkyLook Look => _look;

    /// <summary>The number of vertices the last frame's clouds used (0 when clouds are off).</summary>
    public int CloudVertices { get; private set; }

    /// <summary>
    /// Sets how far the sky is lost in the water's colour (H05): <paramref name="amount"/> 0 leaves the sky as it is, 1 draws none of it (the caller's clear
    /// colour is the water's), and in between a sheet of <paramref name="colour"/> at that opacity is laid over the sky.
    /// </summary>
    /// <param name="colour">The water's colour.</param>
    /// <param name="amount">How far under water, 0 to 1.</param>
    public void SetHaze(Color colour, float amount)
    {
        _hazeColour = colour;
        _hazeAmount = Math.Clamp(amount, 0f, 1f);
    }

    /// <summary>Draws the dome, the sun and (when <paramref name="clouds"/>) the clouds. The caller has cleared the target.</summary>
    public void Draw(FirstPersonCamera camera, long tick, bool clouds)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if (_hazeAmount >= 1f)
        {
            CloudVertices = 0;
            return; // fully under water: the sky is not visible, the target is already the water's colour
        }

        _device.DepthStencilState = DepthStencilState.None;
        _device.RasterizerState = RasterizerState.CullNone;
        _device.BlendState = BlendState.NonPremultiplied;
        var aspect = _device.Viewport.AspectRatio;
        var projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(camera.FieldOfView), aspect, 1f, SkyFarPlane);

        // The dome and the sun are centred on the eye: a view with no move.
        _effect.World = Matrix.Identity;
        _effect.View = Matrix.CreateLookAt(Vector3.Zero, camera.Forward, camera.Up);
        _effect.Projection = projection;
        _effect.CurrentTechnique.Passes[0].Apply();
        _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _dome, 0, _dome.Length, _domeIndices, 0, _domeIndices.Length / 3);
        DrawStars();
        DrawBodies();

        CloudVertices = 0;
        if (clouds)
        {
            DrawClouds(camera, projection, tick);
        }

        if (_hazeAmount > 0f)
        {
            DrawHaze();
        }

        _device.BlendState = BlendState.Opaque;
    }

    /// <summary>
    /// Takes the look of this frame (E02). The dome is coloured again when its three colours moved (in place, nothing allocated), and the stars' brightness when the stars' amount moved;
    /// the sun, the moon and the clouds are read from the look when they are drawn.
    /// </summary>
    /// <param name="look">The look at this moment of the cycle.</param>
    public void SetLook(in SkyLook look)
    {
        var domeMoved = !_domeColoured || look.DomeHorizon != _look.DomeHorizon || look.DomeMiddle != _look.DomeMiddle || look.DomeZenith != _look.DomeZenith;
        _look = look;
        if (domeMoved)
        {
            Recolour();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _effect.Dispose();

    private static Color ToColor((byte R, byte G, byte B) c, byte alpha = 255) => new(c.R, c.G, c.B, alpha);

    private static short[] QuadIndices(int quads)
    {
        var indices = new short[6 * quads];
        for (var q = 0; q < quads; q++)
        {
            var v = (short)(q * 4);
            var i = q * 6;
            indices[i] = v;
            indices[i + 1] = (short)(v + 1);
            indices[i + 2] = (short)(v + 2);
            indices[i + 3] = v;
            indices[i + 4] = (short)(v + 2);
            indices[i + 5] = (short)(v + 3);
        }

        return indices;
    }

    private static (VertexPositionColor[] Vertices, short[] Indices) BuildDome(float[] ringElevation)
    {
        // Rings from 25 degrees below the horizon up to the zenith; the colours are set by Recolour, from the look.
        var vertices = new List<VertexPositionColor>();
        var indices = new List<short>();
        for (var r = 0; r <= Rings; r++)
        {
            // Closer rings near the horizon, where the gradient changes fastest.
            var t = r / (float)Rings;
            var elevation = -25f + (115f * MathF.Pow(t, 1.25f));
            ringElevation[r] = elevation;
            var rad = MathHelper.ToRadians(Math.Min(elevation, 90f));
            var y = MathF.Sin(rad) * DomeRadius;
            var ring = MathF.Cos(rad) * DomeRadius;
            for (var s = 0; s < Segments; s++)
            {
                var a = s * MathHelper.TwoPi / Segments;
                vertices.Add(new VertexPositionColor(new Vector3(MathF.Sin(a) * ring, y, -MathF.Cos(a) * ring), Color.Black));
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

    // Every vertex of a ring takes the sky colour of its elevation in the look's three colours.
    private void Recolour()
    {
        for (var r = 0; r <= Rings; r++)
        {
            var colour = ToColor(SkyPalette.At(_ringElevation[r], _look.DomeHorizon, _look.DomeMiddle, _look.DomeZenith));
            for (var s = 0; s < Segments; s++)
            {
                _dome[(r * Segments) + s].Color = colour;
            }
        }

        _domeColoured = true;
    }

    // The stars: little squares at places picked by a hash of the star's number (no random generator, no clock), spread evenly over the sphere's upper part. They stand on the dome,
    // a little inside it, facing the eye; their brightness differs and the look's amount fades them in.
    private void BuildStars()
    {
        for (var i = 0; i < StarCount; i++)
        {
            var h = BlockArtNature.Mix((uint)i, 0u, 0x57A2);
            var g = BlockArtNature.Mix((uint)i, 1u, 0x57A3);
            var azimuth = (h & 0xFFFF) / 65535f * MathHelper.TwoPi;
            var up = 0.03f + (0.96f * ((h >> 16) / 65535f));
            var flat = MathF.Sqrt(1f - (up * up));
            var direction = new Vector3(MathF.Sin(azimuth) * flat, up, -MathF.Cos(azimuth) * flat);
            var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, direction));
            var top = Vector3.Cross(direction, right);
            var half = 0.28f + (0.30f * ((g & 0xF) / 15f));
            var centre = direction * (DomeRadius * 0.985f);
            var v = i * 4;
            _stars[v] = new VertexPositionColor(centre - (right * half) - (top * half), Color.Transparent);
            _stars[v + 1] = new VertexPositionColor(centre + (right * half) - (top * half), Color.Transparent);
            _stars[v + 2] = new VertexPositionColor(centre + (right * half) + (top * half), Color.Transparent);
            _stars[v + 3] = new VertexPositionColor(centre - (right * half) + (top * half), Color.Transparent);
            _starBrightness[i] = (byte)(115 + (((g >> 4) & 0xF) * 9));
        }
    }

    private void DrawStars()
    {
        if (_look.Stars <= 0.004f)
        {
            return;
        }

        if (_look.Stars != _starAmount)
        {
            _starAmount = _look.Stars;
            for (var i = 0; i < StarCount; i++)
            {
                var alpha = (byte)MathF.Round(_starBrightness[i] * _starAmount);
                var colour = new Color((byte)235, (byte)240, (byte)255, alpha);
                for (var k = 0; k < 4; k++)
                {
                    _stars[(i * 4) + k].Color = colour;
                }
            }
        }

        _effect.CurrentTechnique.Passes[0].Apply();
        _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _stars, 0, _stars.Length, _starIndices, 0, _starIndices.Length / 3);
    }

    // A sheet over the whole view in the water's colour, at the haze's opacity: the sky fades into the murk as the view goes under.
    private void DrawHaze()
    {
        var colour = new Color(_hazeColour.R, _hazeColour.G, _hazeColour.B, (byte)MathF.Round(_hazeAmount * 255f));
        _haze[0] = new VertexPositionColor(new Vector3(-1f, -1f, 0f), colour);
        _haze[1] = new VertexPositionColor(new Vector3(1f, -1f, 0f), colour);
        _haze[2] = new VertexPositionColor(new Vector3(1f, 1f, 0f), colour);
        _haze[3] = new VertexPositionColor(new Vector3(-1f, 1f, 0f), colour);
        _effect.World = Matrix.Identity;
        _effect.View = Matrix.Identity;
        _effect.Projection = Matrix.Identity;
        _effect.CurrentTechnique.Passes[0].Apply();
        _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _haze, 0, 4, _hazeIndices, 0, 2);
    }

    // The sun: a bright square with two wider, fainter squares behind it (a blocky glow); the moon: a pale square with a faint halo and two darker squares for its seas. Each
    // fades with the look's strength for it (the sun into the horizon, the moon into the daylight).
    private void DrawBodies()
    {
        var quads = 0;
        if (_look.SunStrength > 0.004f)
        {
            var sun = Vector3.Normalize(new Vector3(_look.SunDirection.X, _look.SunDirection.Y, _look.SunDirection.Z));
            var k = _look.SunStrength;
            var (r, g, b) = _look.SunColour;
            quads = Square(quads, sun, 12f, 0f, 0f, new Color(r, g, b, (byte)(34 * k)));
            quads = Square(quads, sun, 8.5f, 0f, 0f, new Color(r, g, b, (byte)(78 * k)));
            quads = Square(quads, sun, 5f, 0f, 0f, new Color(r, g, b, (byte)(255 * k)));
        }

        if (_look.MoonStrength > 0.004f)
        {
            var moon = Vector3.Normalize(new Vector3(_look.MoonDirection.X, _look.MoonDirection.Y, _look.MoonDirection.Z));
            var k = _look.MoonStrength;
            quads = Square(quads, moon, 8.5f, 0f, 0f, new Color(200, 214, 255, (int)(40 * k)));
            quads = Square(quads, moon, 4.5f, 0f, 0f, new Color(236, 240, 252, (int)(255 * k)));
            quads = Square(quads, moon, 1.3f, -1.4f, 1.6f, new Color(196, 204, 226, (int)(255 * k)));
            quads = Square(quads, moon, 1.0f, 1.8f, -1.2f, new Color(204, 212, 232, (int)(255 * k)));
        }

        if (quads == 0)
        {
            return;
        }

        _effect.CurrentTechnique.Passes[0].Apply();
        _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _bodies, 0, quads * 4, _bodyIndices, 0, quads * 2);
    }

    // Adds a square facing the eye on the dome in a direction, its middle moved by (shiftRight, shiftUp); returns the number of quads now in the buffer.
    private int Square(int quads, Vector3 direction, float half, float shiftRight, float shiftUp, Color colour)
    {
        var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, direction));
        var up = Vector3.Cross(direction, right);
        var centre = (direction * (DomeRadius * 0.96f)) + (right * shiftRight) + (up * shiftUp);
        var i = quads * 4;
        _bodies[i] = new VertexPositionColor(centre - (right * half) - (up * half), colour);
        _bodies[i + 1] = new VertexPositionColor(centre + (right * half) - (up * half), colour);
        _bodies[i + 2] = new VertexPositionColor(centre + (right * half) + (up * half), colour);
        _bodies[i + 3] = new VertexPositionColor(centre - (right * half) + (up * half), colour);
        return quads + 1;
    }

    private void DrawClouds(FirstPersonCamera camera, Matrix projection, long tick)
    {
        CloudField.Generate(camera.Eye.X, camera.Eye.Z, tick, _boxes, _look.CloudCover);
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
            var fade = (byte)(255f * Math.Clamp(1.6f - (distance / (CloudField.Reach * CloudField.CellSize)), 0f, 1f) * box.Alpha); // a cloud the cover is only passing is fainter (E05)
            var horizon = _look.FogColour;
            var top = ToColor(SkyPalette.Blend(_look.CloudTop, horizon, haze), fade);
            var side = ToColor(SkyPalette.Blend(_look.CloudSide, horizon, haze), fade);
            var bottom = ToColor(SkyPalette.Blend(_look.CloudBottom, horizon, haze), fade);
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
