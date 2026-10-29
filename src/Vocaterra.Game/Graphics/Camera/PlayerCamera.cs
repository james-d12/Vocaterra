using Microsoft.Xna.Framework;
using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Simulation;

namespace Vocaterra.Game.Graphics.Camera;

/// <summary>
/// The camera that follows the player's eye: the eye of the last two ticks and the step-up ease between them (<see cref="CameraEase"/>), placed
/// at the interpolated point each frame, first person or over the right shoulder (S15). The look is the player's (<see cref="LookControl"/>).
/// </summary>
public sealed class PlayerCamera : ITickObserver
{
    private readonly VoxelSimulation _sim;
    private readonly LookControl _look;
    private readonly ViewModeState _viewMode;
    private readonly Func<int, int, int, bool> _isSolid;
    private readonly CameraEase _ease = new();
    private Vector3 _previousEye;
    private double _previousEase;
    private double _currentEase;

    /// <summary>Starts the camera at the body's eye.</summary>
    /// <param name="sim">The simulation whose body the camera follows.</param>
    /// <param name="look">The player's look.</param>
    /// <param name="viewMode">First person or over the shoulder.</param>
    /// <param name="isSolid">Whether a block is solid, so the shoulder view stays in front of a wall.</param>
    public PlayerCamera(VoxelSimulation sim, LookControl look, ViewModeState viewMode, Func<int, int, int, bool> isSolid)
    {
        _sim = sim;
        _look = look;
        _viewMode = viewMode;
        _isSolid = isSolid;
        CurrentEye = _previousEye = EyeBlocks();
    }

    /// <summary>The 3D camera the world and the labels are drawn with.</summary>
    public FirstPersonCamera Camera { get; } = new();

    /// <summary>The body's eye after the last tick, in blocks.</summary>
    public Vector3 CurrentEye { get; private set; }

    /// <summary>Whether the camera is over the shoulder.</summary>
    public bool Shoulder => _viewMode.IsThirdPerson;

    /// <summary>Once a tick, after the step: the ease follows a step-up and the eye of this tick becomes the newer of the two.</summary>
    public void AfterTick()
    {
        _ease.Tick(_sim.Player.SteppedUp);
        _previousEye = CurrentEye;
        CurrentEye = EyeBlocks();
        _previousEase = _currentEase;
        _currentEase = _ease.Offset;
    }

    /// <inheritdoc />
    void ITickObserver.AfterTick(IReadOnlyList<VoxelCommand> commands) => AfterTick();

    /// <summary>
    /// Once a frame: places the camera at the eye between the last two ticks (<paramref name="alpha"/> of the way), or over the right shoulder,
    /// three blocks back and in front of any wall behind the player. Returns the first-person eye, which the arm and the labels use.
    /// </summary>
    public Vector3 Place(float alpha, int width, int height, float farPlane, int canvasWidth, int canvasHeight)
    {
        var eye = Vector3.Lerp(_previousEye, CurrentEye, alpha);
        eye.Y += (float)(_previousEase + ((_currentEase - _previousEase) * alpha));
        Camera.Update(eye, _look.YawSteps, _look.PitchSteps, width, height, farPlane, canvasWidth, canvasHeight); // the labels and the arrow are placed on the canvas of the 2D layers (X08)
        if (Shoulder)
        {
            var back = ThirdPersonRig.Eye(
                _isSolid,
                (eye.X, eye.Y, eye.Z),
                (Camera.Forward.X, Camera.Forward.Y, Camera.Forward.Z),
                (Camera.Right.X, Camera.Right.Y, Camera.Right.Z));
            Camera.Update(new Vector3(back.X, back.Y, back.Z), _look.YawSteps, _look.PitchSteps, width, height, farPlane, canvasWidth, canvasHeight);
        }

        return eye;
    }

    private Vector3 EyeBlocks()
    {
        var body = _sim.Player;
        const float U = VoxelPhysics.UnitsPerBlock;
        return new Vector3(body.EyeX / U, body.EyeY / U, body.EyeZ / U);
    }
}
