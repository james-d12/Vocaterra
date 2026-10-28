using Vocaterra.Game.Core.Mathematics;
using Vocaterra.Game.Core.Simulation;

namespace Vocaterra.Game.Graphics.Camera;

/// <summary>
/// The camera's orientation as plain numbers. The player's look is whole <see cref="Angles"/> steps (yaw 0 north, 1 024 east, pitch positive
/// up); this turns it into the three unit vectors a renderer needs. The engine camera builds its view matrix from the same vectors, and a test
/// holds them to the simulation's look vector (cos p sin y, sin p, -cos p cos y).
/// </summary>
/// <param name="Forward">The way the camera looks (east, up, south).</param>
/// <param name="Right">The camera's right, horizontal.</param>
/// <param name="Up">The camera's up.</param>
public readonly record struct CameraBasis((double X, double Y, double Z) Forward, (double X, double Y, double Z) Right, (double X, double Y, double Z) Up)
{
    /// <summary>The radians of one angle step (<see cref="LookControl.RadiansPerStep"/>).</summary>
    public const double RadiansPerStep = LookControl.RadiansPerStep;

    /// <summary>The basis for a yaw and a pitch in angle steps.</summary>
    public static CameraBasis From(double yawSteps, double pitchSteps)
    {
        var yaw = yawSteps * RadiansPerStep;
        var pitch = pitchSteps * RadiansPerStep;
        var forward = (Math.Cos(pitch) * Math.Sin(yaw), Math.Sin(pitch), -Math.Cos(pitch) * Math.Cos(yaw));
        var right = (Math.Cos(yaw), 0.0, Math.Sin(yaw));
        var up = (
            (right.Item2 * forward.Item3) - (right.Item3 * forward.Item2),
            (right.Item3 * forward.Item1) - (right.Item1 * forward.Item3),
            (right.Item1 * forward.Item2) - (right.Item2 * forward.Item1));
        return new CameraBasis(forward, right, up);
    }
}

/// <summary>Where a world point lands on the screen, with its position in the camera's own frame.</summary>
/// <param name="X">The pixel column (may lie outside the screen).</param>
/// <param name="Y">The pixel row, from the top (may lie outside the screen).</param>
/// <param name="Depth">The distance in front of the camera along its view direction; zero or less means behind it.</param>
/// <param name="Right">The point's position along the camera's right, in blocks.</param>
/// <param name="Up">The point's position along the camera's up, in blocks.</param>
public readonly record struct ProjectedPoint(double X, double Y, double Depth, double Right, double Up)
{
    /// <summary>Whether the point is in front of the camera.</summary>
    public bool InFront => Depth > 1e-6;
}

/// <summary>
/// A perspective camera as plain maths: from an eye, a look and a field of view to the pixel a world point lands on. The labels and the beacon
/// arrow use it so they need no matrix and no graphics device; the engine's matrices agree with it (a test checks).
/// </summary>
public sealed class ScreenProjection
{
    private readonly double _eyeX;
    private readonly double _eyeY;
    private readonly double _eyeZ;
    private readonly CameraBasis _basis;
    private readonly double _tanHalfFov;

    /// <summary>Creates the projection.</summary>
    /// <param name="eyeX">The eye's east position in blocks.</param>
    /// <param name="eyeY">The eye's height in blocks.</param>
    /// <param name="eyeZ">The eye's south position in blocks.</param>
    /// <param name="yawSteps">The yaw in angle steps.</param>
    /// <param name="pitchSteps">The pitch in angle steps.</param>
    /// <param name="verticalFovDegrees">The vertical field of view in degrees.</param>
    /// <param name="width">The viewport width in pixels.</param>
    /// <param name="height">The viewport height in pixels.</param>
    public ScreenProjection(double eyeX, double eyeY, double eyeZ, double yawSteps, double pitchSteps, double verticalFovDegrees, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        _eyeX = eyeX;
        _eyeY = eyeY;
        _eyeZ = eyeZ;
        _basis = CameraBasis.From(yawSteps, pitchSteps);
        _tanHalfFov = Math.Tan(verticalFovDegrees * Math.PI / 360.0);
        Width = width;
        Height = height;
    }

    /// <summary>The viewport width in pixels.</summary>
    public int Width { get; }

    /// <summary>The viewport height in pixels.</summary>
    public int Height { get; }

    /// <summary>The camera's orientation.</summary>
    public CameraBasis Basis => _basis;

    /// <summary>Projects a world point (blocks) to the screen.</summary>
    public ProjectedPoint Project(double x, double y, double z)
    {
        var dx = x - _eyeX;
        var dy = y - _eyeY;
        var dz = z - _eyeZ;
        var right = Dot(dx, dy, dz, _basis.Right);
        var up = Dot(dx, dy, dz, _basis.Up);
        var depth = Dot(dx, dy, dz, _basis.Forward);
        if (depth <= 1e-6)
        {
            return new ProjectedPoint(Width / 2.0, Height / 2.0, depth, right, up);
        }

        var aspect = Width / (double)Height;
        var ndcX = right / (depth * _tanHalfFov * aspect);
        var ndcY = up / (depth * _tanHalfFov);
        return new ProjectedPoint((Width / 2.0) * (1 + ndcX), (Height / 2.0) * (1 - ndcY), depth, right, up);
    }

    private static double Dot(double x, double y, double z, (double X, double Y, double Z) v) => (x * v.X) + (y * v.Y) + (z * v.Z);
}

/// <summary>
/// Where the pointer's edge arrow goes when its target is off screen: on the screen edge (inside a margin) in the direction of the target, as the
/// player would have to turn to face it. When the target is on screen there is no arrow.
/// </summary>
/// <param name="OnScreen">Whether the target is inside the inner screen rectangle (then the arrow is not drawn).</param>
/// <param name="X">The arrow's pixel column on the edge (the target's own column when on screen).</param>
/// <param name="Y">The arrow's pixel row on the edge (the target's own row when on screen).</param>
/// <param name="Angle">The direction the arrow points, in radians, 0 to the right and positive clockwise on screen (the sprite batch's convention).</param>
public readonly record struct EdgeArrow(bool OnScreen, double X, double Y, double Angle)
{
    /// <summary>
    /// Works out the arrow for a projected target. A target in front of the camera and inside the screen shrunk by <paramref name="margin"/> pixels
    /// is on screen. Any other is placed where the line from the screen's centre in the target's direction (right and up in the camera's frame, so
    /// a target behind the camera points the way round) meets that rectangle; a target straight behind points down.
    /// </summary>
    public static EdgeArrow For(ProjectedPoint target, int width, int height, int margin)
    {
        var halfW = (width / 2.0) - margin;
        var halfH = (height / 2.0) - margin;
        var centreX = width / 2.0;
        var centreY = height / 2.0;
        if (target.InFront && Math.Abs(target.X - centreX) <= halfW && Math.Abs(target.Y - centreY) <= halfH)
        {
            return new EdgeArrow(true, target.X, target.Y, 0);
        }

        // The direction on screen: right is +x, up is -y.
        var dx = target.Right;
        var dy = -target.Up;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 1e-6)
        {
            dx = 0;
            dy = 1; // straight ahead and behind, or straight above: say "down" (turn round) rather than point nowhere
            length = 1;
        }

        dx /= length;
        dy /= length;
        var scale = Math.Min(
            Math.Abs(dx) < 1e-9 ? double.MaxValue : halfW / Math.Abs(dx),
            Math.Abs(dy) < 1e-9 ? double.MaxValue : halfH / Math.Abs(dy));
        return new EdgeArrow(false, centreX + (dx * scale), centreY + (dy * scale), Math.Atan2(dy, dx));
    }
}
