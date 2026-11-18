using Microsoft.Xna.Framework.Graphics;
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

    /// <summary>Reads the tile's vertices and indices back from the card (the far view goes back to MonoGame's effects: the setting or the shaders were turned off while the tile was on the card in this form).</summary>
    /// <param name="vertexCount">How many vertices the tile has.</param>
    /// <param name="indexCount">How many indices.</param>
    public (VertexPositionColorTexture[] Vertices, ushort[]? Narrow, int[]? Wide) Download(int vertexCount, int indexCount)
    {
        var vertices = new VertexPositionColorTexture[vertexCount];
        _gl.BindBuffer(BufferTargetARB.CopyReadBuffer, _vertices);
        _gl.GetBufferSubData<VertexPositionColorTexture>(BufferTargetARB.CopyReadBuffer, 0, vertices);
        ushort[]? narrow = null;
        int[]? wide = null;
        _gl.BindBuffer(BufferTargetARB.CopyReadBuffer, _indices);
        if (Wide)
        {
            wide = new int[indexCount];
            _gl.GetBufferSubData<int>(BufferTargetARB.CopyReadBuffer, 0, wide);
        }
        else
        {
            narrow = new ushort[indexCount];
            _gl.GetBufferSubData<ushort>(BufferTargetARB.CopyReadBuffer, 0, narrow);
        }

        _gl.BindBuffer(BufferTargetARB.CopyReadBuffer, 0);
        return (vertices, narrow, wide);
    }

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
