using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Graphics.World;

/// <summary>
/// How a corner's light is shaded (B17c): a face of a block takes the sun by its direction, a flat surface (the undergrowth, a waterfall's sheet) only the sun's colour, a fixed one nothing at all.
/// The values are the codes in the vertex's light word (<see cref="LightData"/>) and in <c>chunk.vert.glsl</c>.
/// </summary>
public enum LightFace : byte
{
    /// <summary>The face looking east (+x); the cardinal codes are the mesher's order of faces.</summary>
    East = 0,

    /// <summary>The face looking west (-x).</summary>
    West = 1,

    /// <summary>The face looking up (+y).</summary>
    Up = 2,

    /// <summary>The face looking down (-y), which takes the bottom tint and never the sun.</summary>
    Down = 3,

    /// <summary>The face looking south (+z).</summary>
    South = 4,

    /// <summary>The face looking north (-z).</summary>
    North = 5,

    /// <summary>No direction: the sun's colour only, times the light word's extra byte as a grey level (255 for the plants; a turned bed's faces use their own grey). The water pass reads the extra byte as the water's depth instead.</summary>
    Flat = 6,

    /// <summary>No light model: the vertex colour is the finished colour (the lantern's glass and a torch's flame, which no hour dims).</summary>
    Fixed = 7,
}

/// <summary>
/// The light of a mesh corner as data (B17c), one 32-bit word beside the 24-byte <see cref="MeshVertex"/> (a second vertex stream of 4 bytes a vertex): the shader combines it with the frame's uniforms (the sun's direction and colour, the glow's
/// strength), so the hour, the weather and the darkening change with no new mesh. Byte 0 is the <b>sky light</b> and byte 1 the <b>block light</b>, each 0 to 255 where 255 is level 15 (the whole sky; a lantern next to the face) and a level
/// <c>L</c> of 0 to 15, as B23's propagation will compute it, is <c>L * 17</c> (<see cref="FromLevel"/>); B12's darkening by depth fills the sky byte and the torches', lanterns' and lava's reach the block byte today, so B23 only
/// changes who fills them. Byte 2 holds the <b>corner occlusion</b> (bits 0 to 1: 3 is an open corner, 0 a corner shut in by two sides), the <b>face</b> (bits 2 to 4, <see cref="LightFace"/>) and the <b>flags</b> (bits 5 to 7, <see cref="WaveFlag"/>).
/// Byte 3 is the <b>extra</b> byte of <see cref="LightFace.Flat"/> and of the water.
/// </summary>
public static class LightData
{
    /// <summary>The highest light level of B23's propagation (0 to 15): the byte 255 of a channel.</summary>
    public const int MaxLevel = 15;

    /// <summary>A channel at its brightest (level 15).</summary>
    public const byte Full = 255;

    /// <summary>The flag of a water vertex that moves with the surface (B17c's water shader): the surface and the top edge of a side, not the bottom.</summary>
    public const int WaveFlag = 1;

    /// <summary>The bytes of a light word in a vertex stream.</summary>
    public const int SizeInBytes = 4;

    /// <summary>The channel byte of a light level 0 to 15 (<c>level * 17</c>, so 15 is 255).</summary>
    /// <param name="level">The level.</param>
    public static byte FromLevel(int level) => (byte)(Math.Clamp(level, 0, MaxLevel) * 17);

    /// <summary>The light level 0 to 15 (fractions between the levels) of a channel byte.</summary>
    /// <param name="channel">The byte.</param>
    public static float LevelOf(byte channel) => channel / 17f;

    /// <summary>The channel byte of a share 0 to 1 (rounded).</summary>
    /// <param name="share">The share.</param>
    public static byte FromShare(float share) => (byte)Math.Clamp((int)MathF.Round(share * 255f), 0, 255);

    /// <summary>Packs a light word.</summary>
    /// <param name="sky">The sky light byte.</param>
    /// <param name="block">The block light byte.</param>
    /// <param name="occlusion">The corner occlusion, 0 to 3 (3 open).</param>
    /// <param name="face">The face code.</param>
    /// <param name="extra">The extra byte (<see cref="LightFace.Flat"/>'s grey level; the water's depth).</param>
    /// <param name="flags">The flags (<see cref="WaveFlag"/>).</param>
    public static uint Pack(byte sky, byte block, int occlusion, LightFace face, byte extra = 0, int flags = 0) =>
        sky | ((uint)block << 8) | ((uint)((occlusion & 3) | ((int)face << 2) | ((flags & 7) << 5)) << 16) | ((uint)extra << 24);

    /// <summary>The word of a corner in the open: full sky light, no block light, an open corner.</summary>
    /// <param name="face">The face code.</param>
    /// <param name="extra">The extra byte.</param>
    public static uint Open(LightFace face, byte extra = 0) => Pack(Full, 0, 3, face, extra);

    /// <summary>The sky light byte of a word.</summary>
    public static byte Sky(uint word) => (byte)word;

    /// <summary>The block light byte of a word.</summary>
    public static byte Block(uint word) => (byte)(word >> 8);

    /// <summary>The corner occlusion of a word, 0 to 3.</summary>
    public static int Occlusion(uint word) => (int)((word >> 16) & 3);

    /// <summary>The face code of a word.</summary>
    public static LightFace Face(uint word) => (LightFace)((word >> 18) & 7);

    /// <summary>The flags of a word.</summary>
    public static int Flags(uint word) => (int)((word >> 21) & 7);

    /// <summary>The extra byte of a word.</summary>
    public static byte Extra(uint word) => (byte)(word >> 24);

    /// <summary>The face code of a block face.</summary>
    /// <param name="face">The block face.</param>
    public static LightFace CodeOf(BlockFace face) => (LightFace)(byte)face; // the block faces are numbered as the codes: east, west, up, down, south, north
}
