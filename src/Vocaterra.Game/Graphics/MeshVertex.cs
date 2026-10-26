using System.Runtime.InteropServices;

namespace Vocaterra.Game.Graphics;

/// <summary>
/// One vertex of a voxel mesh, in plain values: a position in blocks (x east, y up, z south, the simulation's axes), a packed colour (red in
/// the lowest byte, then green, blue and alpha: the layout of the engine's colour) and a texture coordinate. The 24-byte layout is the engine's
/// position, colour and texture vertex, so the renderer uploads these arrays without converting them.
/// </summary>
/// <param name="X">The east position in blocks.</param>
/// <param name="Y">The height in blocks.</param>
/// <param name="Z">The south position in blocks.</param>
/// <param name="Color">The packed colour: red | green &lt;&lt; 8 | blue &lt;&lt; 16 | alpha &lt;&lt; 24.</param>
/// <param name="U">The horizontal texture coordinate (0 to 1 over the atlas).</param>
/// <param name="V">The vertical texture coordinate (0 at the top of the atlas).</param>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct MeshVertex(float X, float Y, float Z, uint Color, float U, float V)
{
    /// <summary>The size of one vertex in bytes (it must match the engine's vertex declaration).</summary>
    public const int SizeInBytes = 24;

    /// <summary>Packs a colour from its channels.</summary>
    public static uint Pack(byte red, byte green, byte blue, byte alpha = 255) =>
        red | ((uint)green << 8) | ((uint)blue << 16) | ((uint)alpha << 24);

    /// <summary>Packs a grey level (a shade from 0 to 1) with the given alpha.</summary>
    public static uint Shade(float shade, byte alpha = 255)
    {
        var level = (byte)Math.Clamp((int)MathF.Round(shade * 255f), 0, 255);
        return Pack(level, level, level, alpha);
    }
}

/// <summary>
/// A growing list of quads. Every quad is four vertices in counter-clockwise order seen from outside (bottom left, bottom right, top right,
/// top left), which the renderer turns into two triangles with one shared index pattern; so a mesh's face count is its vertex count over four.
/// </summary>
public sealed class MeshBuffer
{
    private MeshVertex[] _vertices = new MeshVertex[256];

    /// <summary>The number of vertices.</summary>
    public int VertexCount { get; private set; }

    /// <summary>The number of quads.</summary>
    public int FaceCount => VertexCount / 4;

    /// <summary>The vertices written so far.</summary>
    public ReadOnlySpan<MeshVertex> Vertices => new(_vertices, 0, VertexCount);

    /// <summary>Appends one quad.</summary>
    public void AddQuad(in MeshVertex bottomLeft, in MeshVertex bottomRight, in MeshVertex topRight, in MeshVertex topLeft)
    {
        if (VertexCount + 4 > _vertices.Length)
        {
            Array.Resize(ref _vertices, _vertices.Length * 2);
        }

        _vertices[VertexCount++] = bottomLeft;
        _vertices[VertexCount++] = bottomRight;
        _vertices[VertexCount++] = topRight;
        _vertices[VertexCount++] = topLeft;
    }

    /// <summary>Removes every quad (the storage is kept).</summary>
    public void Clear() => VertexCount = 0;

    /// <summary>A copy of the vertices as an array.</summary>
    public MeshVertex[] ToArray() => Vertices.ToArray();
}
