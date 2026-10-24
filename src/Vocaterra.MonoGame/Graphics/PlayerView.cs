namespace Vocaterra.MonoGame.Graphics;

/// <summary>Where the camera is (S15): in the player's eyes (the default) or behind the player's shoulder.</summary>
public enum CameraMode
{
    /// <summary>The first-person view: the camera is at the eye, the player's arm and the held item are drawn, the player's own figure is not.</summary>
    FirstPerson = 0,

    /// <summary>The behind-the-shoulder view: the camera stands behind and a little above and to the right of the head, and the player's figure is drawn.</summary>
    ThirdPerson,
}

/// <summary>The camera mode and its toggle: F5 flips it. First person is where every run starts.</summary>
public sealed class ViewModeState
{
    /// <summary>The camera mode now.</summary>
    public CameraMode Mode { get; private set; } = CameraMode.FirstPerson;

    /// <summary>Whether the camera is behind the shoulder.</summary>
    public bool IsThirdPerson => Mode == CameraMode.ThirdPerson;

    /// <summary>Flips the mode (the F5 key) and returns the new one.</summary>
    public CameraMode Toggle()
    {
        Mode = IsThirdPerson ? CameraMode.FirstPerson : CameraMode.ThirdPerson;
        return Mode;
    }

    /// <summary>Sets the mode (the <c>--third-person</c> launch option).</summary>
    public void Set(CameraMode mode) => Mode = mode;
}

/// <summary>
/// Where the camera stands in the behind-the-shoulder view (S15): from the head, back along the look direction, a little up and to the right, but never
/// into a solid block: the ray from the head to the wanted place is walked in small steps and the camera stops short of the first solid cell, so a wall behind
/// the player brings the camera in instead of hiding the player behind it. Plain maths over a solidity function, so a test needs no window.
/// </summary>
public static class ThirdPersonRig
{
    /// <summary>How far behind the head the camera stands in the open, in blocks.</summary>
    public const float Distance = 3.4f;

    /// <summary>How far above the head's line the camera stands, in blocks.</summary>
    public const float Rise = 0.4f;

    /// <summary>How far to the right of the head's line the camera stands, in blocks (over the right shoulder).</summary>
    public const float Side = 0.55f;

    /// <summary>How far short of a solid block the camera stops, in blocks.</summary>
    public const float Margin = 0.25f;

    private const float Step = 0.1f;

    /// <summary>The camera's place.</summary>
    /// <param name="isSolid">Whether the block cell (x, y, z) is solid.</param>
    /// <param name="head">The head (the eye of the first-person view).</param>
    /// <param name="forward">The unit look direction.</param>
    /// <param name="right">The unit direction to the right of the look.</param>
    public static (float X, float Y, float Z) Eye(Func<int, int, int, bool> isSolid, (float X, float Y, float Z) head, (float X, float Y, float Z) forward, (float X, float Y, float Z) right)
    {
        ArgumentNullException.ThrowIfNull(isSolid);
        var wantedX = head.X - (forward.X * Distance) + (right.X * Side);
        var wantedY = head.Y - (forward.Y * Distance) + Rise + (right.Y * Side);
        var wantedZ = head.Z - (forward.Z * Distance) + (right.Z * Side);
        var dx = wantedX - head.X;
        var dy = wantedY - head.Y;
        var dz = wantedZ - head.Z;
        var length = MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        var steps = (int)MathF.Ceiling(length / Step);
        var lastClear = 0f;
        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)steps;
            var x = head.X + (dx * t);
            var y = head.Y + (dy * t);
            var z = head.Z + (dz * t);
            if (isSolid((int)MathF.Floor(x), (int)MathF.Floor(y), (int)MathF.Floor(z)))
            {
                break;
            }

            lastClear = t;
        }

        var reach = Math.Max(0f, (lastClear * length) - (lastClear < 1f ? Margin : 0f)) / Math.Max(length, 1e-6f);
        return (head.X + (dx * reach), head.Y + (dy * reach), head.Z + (dz * reach));
    }
}
