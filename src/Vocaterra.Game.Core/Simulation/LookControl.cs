using System.Globalization;
using Vocaterra.Game.Core.Mathematics;

namespace Vocaterra.Game.Core.Simulation;

/// <summary>
/// The player's look as the front-end holds it: a yaw and a pitch that mouse movement turns and that scripts set, in <see cref="Angles"/> steps
/// (yaw 0 north, 1 024 east, clockwise seen from above; pitch positive looks up, clamped like the simulation). Every tick the game sends the
/// rounded values to the simulation as an absolute <c>Look</c> command, and the camera draws from the same numbers, so what is on screen is
/// what the next tick will use.
/// </summary>
public sealed class LookControl
{
    /// <summary>The radians of one angle step.</summary>
    public const double RadiansPerStep = 2 * Math.PI / Angles.StepsPerTurn;

    /// <summary>The steps one pixel of mouse movement turns the view: about 0.14 degrees, 1.6 steps.</summary>
    public const double StepsPerPixel = 1.6;

    private double _yaw;
    private double _pitch;

    /// <summary>The yaw in steps, wrapped into [0, 4096).</summary>
    public double YawSteps => _yaw;

    /// <summary>The pitch in steps, within the simulation's limit.</summary>
    public double PitchSteps => _pitch;

    /// <summary>The yaw as the whole angle sent to the simulation.</summary>
    public int Yaw => Angles.Wrap((int)Math.Round(_yaw));

    /// <summary>The pitch as the whole angle sent to the simulation.</summary>
    public int Pitch => Angles.ClampPitch((int)Math.Round(_pitch));

    /// <summary>The yaw in degrees (0 north, 90 east).</summary>
    public double YawDegrees => _yaw * 360.0 / Angles.StepsPerTurn;

    /// <summary>The pitch in degrees (positive up).</summary>
    public double PitchDegrees => _pitch * 360.0 / Angles.StepsPerTurn;

    /// <summary>Faces the given angles in steps.</summary>
    public void Set(double yawSteps, double pitchSteps)
    {
        _yaw = Wrap(yawSteps);
        _pitch = Math.Clamp(pitchSteps, -Angles.MaxPitch, Angles.MaxPitch);
    }

    /// <summary>Faces the given angles in degrees (yaw clockwise from north, pitch positive up).</summary>
    public void SetDegrees(double yawDegrees, double pitchDegrees) =>
        Set(DegreesToSteps(yawDegrees), DegreesToSteps(pitchDegrees));

    /// <summary>Turns the view by mouse movement: right turns clockwise, up looks up.</summary>
    /// <param name="deltaX">Pixels moved to the right.</param>
    /// <param name="deltaY">Pixels moved down (the screen's convention, so moving the mouse up looks up).</param>
    /// <param name="sensitivity">A multiplier on <see cref="StepsPerPixel"/>.</param>
    public void Turn(double deltaX, double deltaY, double sensitivity = 1.0) =>
        Set(_yaw + (deltaX * StepsPerPixel * sensitivity), _pitch - (deltaY * StepsPerPixel * sensitivity));

    /// <summary>Faces a point in the world (blocks) from an eye position (blocks).</summary>
    public void LookAt(double eyeX, double eyeY, double eyeZ, double x, double y, double z)
    {
        var dx = x - eyeX;
        var dy = y - eyeY;
        var dz = z - eyeZ;
        var horizontal = Math.Sqrt((dx * dx) + (dz * dz));
        // Yaw 0 faces -z and grows clockwise: the bearing is atan2(east, north) with north = -dz.
        var yaw = Math.Atan2(dx, -dz) / RadiansPerStep;
        var pitch = Math.Atan2(dy, horizontal) / RadiansPerStep;
        Set(horizontal < 1e-6 ? _yaw : yaw, pitch);
    }

    /// <summary>Converts degrees to angle steps.</summary>
    public static double DegreesToSteps(double degrees) => degrees * Angles.StepsPerTurn / 360.0;

    /// <summary>Reads <c>yaw,pitch</c> in degrees (decimals allowed, the pitch within -89 and 89), as <c>--look</c> gives them.</summary>
    public static bool TryParseDegrees(string value, out double yawDegrees, out double pitchDegrees)
    {
        yawDegrees = 0;
        pitchDegrees = 0;
        var parts = value.Split(',');
        if (parts.Length != 2
            || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out yawDegrees)
            || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out pitchDegrees))
        {
            return false;
        }

        return double.IsFinite(yawDegrees) && double.IsFinite(pitchDegrees) && Math.Abs(pitchDegrees) <= 89.0;
    }

    private static double Wrap(double steps)
    {
        var wrapped = steps % Angles.StepsPerTurn;
        return wrapped < 0 ? wrapped + Angles.StepsPerTurn : wrapped;
    }
}
