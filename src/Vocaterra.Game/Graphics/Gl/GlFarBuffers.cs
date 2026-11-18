using Silk.NET.OpenGL;

namespace Vocaterra.Game.Graphics.Gl;

/// <summary>A far-view tile's vertices and indices in buffers of the game's own (B17c): 24-byte vertices of <c>VertexPositionColorTexture</c> and 16- or 32-bit triangle indices, drawn in runs (the opaque land, the blocks that give light, the water).</summary>
public sealed class GlFarBuffers : IDisposable
{
    private readonly GL _gl;
    private uint _vertices;
    private uint _indices;

    internal GlFarBuffers(GL gl, uint vertices, uint indices, bool wide)
    {
        _gl = gl;
        _vertices = vertices;
        _indices = indices;
        Wide = wide;
    }

    /// <summary>Whether the indices are 32-bit (a tile of more than 65,535 vertices).</summary>
    public bool Wide { get; }

    internal uint Vertices => _vertices;

    internal uint Indices => _indices;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_vertices != 0)
        {
            _gl.DeleteBuffer(_vertices);
            _vertices = 0;
        }

        if (_indices != 0)
        {
            _gl.DeleteBuffer(_indices);
            _indices = 0;
        }
    }
}
