using Vocaterra.Game.Core.Physics;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Sound;

/// <summary>The surface the player walks on, as the footsteps hear it (S25).</summary>
public enum Surface
{
    /// <summary>Grass, leaves, undergrowth and bushes.</summary>
    Grass,

    /// <summary>Dirt and farmland.</summary>
    Dirt,

    /// <summary>The gravel path and the village square.</summary>
    Gravel,

    /// <summary>Stone, cobble, mossy stone, the fire ring and the house walls.</summary>
    Stone,

    /// <summary>Planks, a bridge, timber, trunks and the wooden things of the village.</summary>
    Wood,

    /// <summary>Sand.</summary>
    Sand,

    /// <summary>Snow.</summary>
    Snow,

    /// <summary>Water: the feet are in it.</summary>
    Water,
}

/// <summary>What the footsteps need to know about the player this tick (S25).</summary>
/// <param name="X">The middle of the feet, east-west, in blocks.</param>
/// <param name="Z">The middle of the feet, north-south, in blocks.</param>
/// <param name="Grounded">Whether the player is on the ground (a step is heard only on the ground).</param>
/// <param name="Under">The surface under the feet.</param>
/// <param name="InWater">Whether the feet are in water.</param>
/// <param name="FeetY">The height of the feet, in blocks (the landing and the falls are measured with it).</param>
public readonly record struct FootState(double X, double Z, bool Grounded, Surface Under, bool InWater, double FeetY = 0);

/// <summary>
/// The footsteps (S25): a step every <see cref="StrideBlocks"/> walked on the ground, so the pace follows the walking speed (at 4.3 blocks a second, about two steps
/// a second), on the surface underfoot; a splash in water; a jump and a landing. Engine free and clock free: it reads the player's positions each tick, and the
/// same route gives the same cues. It never writes to the simulation.
/// </summary>
public sealed class Footsteps
{
    /// <summary>The distance walked between two steps, in blocks.</summary>
    public const double StrideBlocks = 2.0;

    /// <summary>
    /// The fall, in blocks, from the highest point of an airborne spell to the ground, above which a landing thumps (S25). A step down of up to this is an
    /// ordinary footstep: walking off a one-block step is not a landing.
    /// </summary>
    public const double HardFallBlocks = 1.5;

    private double _distance;
    private double _lastX;
    private double _lastZ;
    private bool _hasLast;
    private bool _airborne;
    private bool _jumpedInAir;
    private double _peakY;
    private double _groundedY;

    /// <summary>The surface of a block the player can stand on (every solid kind, and the rest as the ground they cover). A new block kind must be placed here (the test checks every kind).</summary>
    /// <param name="id">The block.</param>
    public static Surface SurfaceOf(BlockId id) => id switch
    {
        BlockId.Air => Surface.Grass,
        BlockId.Grass => Surface.Grass,
        BlockId.Dirt => Surface.Dirt,
        BlockId.Stone => Surface.Stone,
        BlockId.Sand => Surface.Sand,
        BlockId.Trunk => Surface.Wood,
        BlockId.Leaves => Surface.Grass,
        BlockId.Bush => Surface.Grass,
        BlockId.BerryBush => Surface.Grass,
        BlockId.Water => Surface.Water,
        BlockId.Path => Surface.Gravel,
        BlockId.Planks => Surface.Wood,
        BlockId.Door => Surface.Wood,
        BlockId.Table => Surface.Wood,
        BlockId.Torch => Surface.Wood,
        BlockId.Stall => Surface.Wood,
        BlockId.Roof => Surface.Wood,
        BlockId.Fence => Surface.Wood,
        BlockId.Wall => Surface.Stone,
        BlockId.FireRing => Surface.Stone,
        BlockId.Sign => Surface.Wood,
        BlockId.Bridge => Surface.Wood,
        BlockId.Snow => Surface.Snow,
        BlockId.SpruceTrunk => Surface.Wood,
        BlockId.BirchTrunk => Surface.Wood,
        BlockId.SpruceLeaves => Surface.Grass,
        BlockId.BirchLeaves => Surface.Grass,
        BlockId.TallGrass => Surface.Grass,
        BlockId.Fern => Surface.Grass,
        BlockId.Reeds => Surface.Grass,
        BlockId.YellowFlower => Surface.Grass,
        BlockId.WhiteFlower => Surface.Grass,
        BlockId.MossyStone => Surface.Stone,
        BlockId.Cobble => Surface.Stone,
        BlockId.Timber => Surface.Wood,
        BlockId.Lantern => Surface.Wood,
        BlockId.Crate => Surface.Wood,
        BlockId.AwningRed => Surface.Wood,
        BlockId.AwningWhite => Surface.Wood,
        BlockId.Canvas => Surface.Wood,
        BlockId.Chest => Surface.Wood,
        BlockId.Farmland => Surface.Dirt,
        BlockId.Wheat => Surface.Grass,
        BlockId.Loaves => Surface.Grass,
        BlockId.Apples => Surface.Grass,
        BlockId.Bottles => Surface.Grass,
        BlockId.SignTiendaA => Surface.Wood,
        BlockId.SignTiendaB => Surface.Wood,
        BlockId.SignRio => Surface.Wood,
        BlockId.SignMontanaA => Surface.Wood,
        BlockId.SignMontanaB => Surface.Wood,
        BlockId.PostTorch => Surface.Wood,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "A block kind with no footstep surface: place it in SurfaceOf."),
    };

    /// <summary>The step cue of a surface.</summary>
    /// <param name="surface">The surface.</param>
    public static SoundCue CueOf(Surface surface) => surface switch
    {
        Surface.Grass => SoundCue.FootGrass,
        Surface.Dirt => SoundCue.FootDirt,
        Surface.Gravel => SoundCue.FootGravel,
        Surface.Stone => SoundCue.FootStone,
        Surface.Wood => SoundCue.FootWood,
        Surface.Sand => SoundCue.FootSand,
        Surface.Snow => SoundCue.FootSnow,
        Surface.Water => SoundCue.FootWater,
        _ => throw new ArgumentOutOfRangeException(nameof(surface), surface, "Unknown surface."),
    };

    /// <summary>
    /// The cues of one tick: a jump, a landing, and the steps the distance walked on the ground calls for. A landing is <see cref="SoundCue.Land"/> only after a
    /// jump, or after a fall of more than <see cref="HardFallBlocks"/> from the highest point of the airborne spell; any other touch-down (a walk off a one-block
    /// step) is a footstep on the surface, and a landing in water is a splash.
    /// </summary>
    /// <param name="state">The player this tick.</param>
    /// <param name="jumped">Whether the player jumped this tick.</param>
    /// <param name="landed">Whether the simulation reported a landing this tick.</param>
    public IReadOnlyList<SoundCue> Observe(FootState state, bool jumped, bool landed)
    {
        var cues = new List<SoundCue>();
        if (jumped)
        {
            cues.Add(SoundCue.Jump);
            _jumpedInAir = true;
        }

        if (!state.Grounded)
        {
            if (!_airborne)
            {
                _airborne = true;
                _peakY = _groundedY; // the highest point starts at the takeoff
            }

            _peakY = Math.Max(_peakY, state.FeetY);
        }
        else
        {
            _groundedY = state.FeetY;
        }

        if (state.Grounded && (landed || _airborne))
        {
            var fall = _peakY - state.FeetY;
            var hard = _jumpedInAir || fall > HardFallBlocks;
            _airborne = false;
            _jumpedInAir = false;
            if (state.InWater)
            {
                cues.Add(SoundCue.FootWater);
            }
            else
            {
                cues.Add(hard ? SoundCue.Land : CueOf(state.Under));
            }
        }

        if (_hasLast && state.Grounded && !jumped)
        {
            var dx = state.X - _lastX;
            var dz = state.Z - _lastZ;
            _distance += Math.Sqrt((dx * dx) + (dz * dz));
        }

        _lastX = state.X;
        _lastZ = state.Z;
        _hasLast = true;
        while (_distance >= StrideBlocks && state.Grounded)
        {
            _distance -= StrideBlocks;
            cues.Add(state.InWater ? SoundCue.FootWater : CueOf(state.Under));
        }

        return cues;
    }

    /// <summary>Reads the player's state for the footsteps from the simulation's body and grid.</summary>
    /// <param name="player">The body.</param>
    /// <param name="blockAt">Reads the block at a cell.</param>
    public static FootState StateOf(PlayerBody3D player, Func<int, int, int, BlockId> blockAt)
    {
        const int unitsPerBlock = VoxelPhysics.UnitsPerBlock;
        var cellX = FloorDiv(player.CenterX, unitsPerBlock);
        var cellY = FloorDiv(player.MinY, unitsPerBlock);
        var cellZ = FloorDiv(player.CenterZ, unitsPerBlock);
        var inWater = blockAt(cellX, cellY, cellZ) == BlockId.Water;
        var under = SurfaceOf(blockAt(cellX, cellY - 1, cellZ));
        return new FootState(player.CenterX / (double)unitsPerBlock, player.CenterZ / (double)unitsPerBlock, player.Grounded, under, inWater, player.MinY / (double)unitsPerBlock);
    }

    // Rounds towards minus infinity (a body below the zero of the grid is in the cell below it).
    private static int FloorDiv(int value, int divisor) => value >= 0 ? value / divisor : -((-value + divisor - 1) / divisor);
}
