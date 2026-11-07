using Microsoft.Xna.Framework;
using Vocaterra.Game.Core.Physics;

namespace Vocaterra.Game.Graphics.Water;

/// <summary>The small sums of the underwater pass that need no graphics device: where the eye is in the physics' units, and how big a blur target is.</summary>
public static class UnderwaterGeometry
{
    /// <summary>The eye point (blocks, as the camera has it) in <see cref="VoxelPhysics"/> fixed-point units, rounded down, ready for <c>WaterVolume.Contains</c>.</summary>
    /// <param name="eyeInBlocks">The camera's position in blocks (east, up, south).</param>
    public static (int X, int Y, int Z) EyeUnits(Vector3 eyeInBlocks) =>
        (UnitsOf(eyeInBlocks.X), UnitsOf(eyeInBlocks.Y), UnitsOf(eyeInBlocks.Z));

    /// <summary>The size of a blurred copy of a picture: each side divided by <paramref name="downscale"/>, rounded up, never below one pixel.</summary>
    /// <param name="width">The picture's width in pixels.</param>
    /// <param name="height">The picture's height in pixels.</param>
    /// <param name="downscale">How many times smaller the copy is (at least 1).</param>
    public static (int Width, int Height) CopySize(int width, int height, int downscale)
    {
        var factor = Math.Max(1, downscale);
        return (Math.Max(1, (width + factor - 1) / factor), Math.Max(1, (height + factor - 1) / factor));
    }

    private static int UnitsOf(float blocks) => (int)MathF.Floor(blocks * VoxelPhysics.UnitsPerBlock);
}
