using Microsoft.Xna.Framework;
using Vocaterra.MonoGame.Frontend;

namespace Vocaterra.MonoGame;

/// <summary>
/// The first-person camera: the player's eye, yaw and pitch as the simulation gives them, turned into MonoGame's view and projection matrices.
/// The world is right-handed with x east, y up and z south, which is MonoGame's own convention (a camera looks down -z), so yaw 0 faces north and
/// no axis is flipped. The vectors come from <see cref="CameraBasis"/>, the same ones the label and arrow maths use, and tests hold both to the
/// simulation's look vector. No graphics device is needed to use it.
/// </summary>
public sealed class FirstPersonCamera
{
    /// <summary>The vertical field of view in degrees.</summary>
    public const float FieldOfViewDegrees = 70f;

    /// <summary>The near plane in blocks (small, so a block against the eye is not cut).</summary>
    public const float NearPlane = 0.05f;

    /// <summary>The default far plane in blocks; a run sets it from its view distance (<see cref="VoxelRenderer.FarPlaneFor"/>).</summary>
    public const float FarPlane = 220f;

    /// <summary>The eye position in blocks (east, up, south).</summary>
    public Vector3 Eye { get; private set; }

    /// <summary>The yaw in angle steps.</summary>
    public double YawSteps { get; private set; }

    /// <summary>The pitch in angle steps.</summary>
    public double PitchSteps { get; private set; }

    /// <summary>The unit vector the camera looks along.</summary>
    public Vector3 Forward { get; private set; } = -Vector3.UnitZ;

    /// <summary>The camera's right.</summary>
    public Vector3 Right { get; private set; } = Vector3.UnitX;

    /// <summary>The camera's up.</summary>
    public Vector3 Up { get; private set; } = Vector3.UnitY;

    /// <summary>The world-to-camera matrix.</summary>
    public Matrix View { get; private set; } = Matrix.Identity;

    /// <summary>The camera-to-clip matrix.</summary>
    public Matrix Projection { get; private set; } = Matrix.Identity;

    /// <summary>The viewing frustum, for skipping chunks that cannot be seen.</summary>
    public BoundingFrustum Frustum { get; private set; } = new(Matrix.Identity);

    /// <summary>The plain-maths projection to the screen (labels and the beacon's arrow).</summary>
    public ScreenProjection Screen { get; private set; } = new(0, 0, 0, 0, 0, FieldOfViewDegrees, 1, 1);

    /// <summary>Places the camera. Call once per frame, after the eye and the look are known.</summary>
    /// <param name="eye">The eye in blocks.</param>
    /// <param name="yawSteps">The yaw in angle steps (0 north, 1 024 east).</param>
    /// <param name="pitchSteps">The pitch in angle steps (positive up).</param>
    /// <param name="width">The viewport width in pixels.</param>
    /// <param name="height">The viewport height in pixels.</param>
    /// <param name="farPlane">The far plane in blocks (the view distance and a little more).</param>
    /// <param name="screenWidth">The width of the canvas the 2D layers are drawn on (X08), for <see cref="Screen"/>; the viewport's by default.</param>
    /// <param name="screenHeight">The height of that canvas; the viewport's by default.</param>
    public void Update(Vector3 eye, double yawSteps, double pitchSteps, int width, int height, float farPlane = FarPlane, int? screenWidth = null, int? screenHeight = null)
    {
        Eye = eye;
        YawSteps = yawSteps;
        PitchSteps = pitchSteps;
        var basis = CameraBasis.From(yawSteps, pitchSteps);
        Forward = ToVector(basis.Forward);
        Right = ToVector(basis.Right);
        Up = ToVector(basis.Up);
        View = Matrix.CreateLookAt(eye, eye + Forward, Up);
        var aspect = width / (float)Math.Max(1, height);
        Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(FieldOfViewDegrees), aspect, NearPlane, farPlane);
        Frustum = new BoundingFrustum(View * Projection);
        Screen = new ScreenProjection(eye.X, eye.Y, eye.Z, yawSteps, pitchSteps, FieldOfViewDegrees, Math.Max(1, screenWidth ?? width), Math.Max(1, screenHeight ?? height));
    }

    private static Vector3 ToVector((double X, double Y, double Z) v) => new((float)v.X, (float)v.Y, (float)v.Z);
}
