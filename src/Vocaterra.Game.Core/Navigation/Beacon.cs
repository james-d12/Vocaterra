using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Navigation;

/// <summary>
/// The point the player is being sent to: a beacon column stands on the block and an arrow at the screen edge points to it when it is off
/// screen. The game sets it (the guide's directions in S3, a script's <c>--beacon</c> now) and clears it when the player arrives.
/// </summary>
public sealed class Beacon
{
    /// <summary>How close, in blocks, the player must come to the target for the beacon to count as reached.</summary>
    public const double ReachedDistance = 3.0;

    /// <summary>The block the column stands on, or <see langword="null"/> when there is no beacon.</summary>
    public BlockPos? Target { get; private set; }

    /// <summary>The word shown with the beacon (the noun, <c>río</c>), or <see langword="null"/>.</summary>
    public string? Label { get; private set; }

    /// <summary>The scene's name of the point the beacon stands on (<c>camp.guide</c>), or <see langword="null"/> when it was set on a block.</summary>
    public string? Name { get; private set; }

    /// <summary>Whether a beacon is set.</summary>
    public bool IsSet => Target is not null;

    /// <summary>Puts the beacon on a block.</summary>
    /// <param name="target">The block.</param>
    /// <param name="label">The word shown with it.</param>
    /// <param name="name">The scene's name of the place, when the block is one (village-plan V02: the arrival is logged with it).</param>
    public void Set(BlockPos target, string? label = null, string? name = null)
    {
        Target = target;
        Label = label;
        Name = name;
    }

    /// <summary>Puts the beacon on a named point of the scene (<c>river</c>, <c>stall</c>...).</summary>
    /// <returns>Whether the scene has such a point.</returns>
    public bool SetPoint(Scene scene, string name, string? label = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (!scene.Points.TryGetValue(name, out var point))
        {
            return false;
        }

        Set(point, label ?? name, name);
        return true;
    }

    /// <summary>Takes the beacon away.</summary>
    public void Clear()
    {
        Target = null;
        Label = null;
        Name = null;
    }

    /// <summary>Clears the beacon when the player is within <see cref="ReachedDistance"/> of it (horizontally and a little vertically); returns whether it did.</summary>
    public bool ClearIfReached(double playerX, double playerZ)
    {
        if (Target is not { } target)
        {
            return false;
        }

        var dx = playerX - (target.X + 0.5);
        var dz = playerZ - (target.Z + 0.5);
        if ((dx * dx) + (dz * dz) > ReachedDistance * ReachedDistance)
        {
            return false;
        }

        Clear();
        return true;
    }
}
