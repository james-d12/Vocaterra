using Silk.NET.OpenGL;

namespace Vocaterra.Game.Graphics.Gl;

/// <summary>A chunk's opaque mesh in a vertex buffer of the game's own (B17a): 24-byte vertices (<see cref="World.MeshVertex"/>), four to a quad, drawn with the shared quad indices.</summary>
public sealed class GlChunkBuffer : IDisposable
{
    private readonly GL _gl;
    private uint _handle;

    internal GlChunkBuffer(GL gl, uint handle, int quads)
    {
        _gl = gl;
        _handle = handle;
        Quads = quads;
    }

    /// <summary>The number of quads in the buffer.</summary>
    public int Quads { get; }

    internal uint Handle => _handle;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_handle != 0)
        {
            _gl.DeleteBuffer(_handle);
            _handle = 0;
        }
    }
}
