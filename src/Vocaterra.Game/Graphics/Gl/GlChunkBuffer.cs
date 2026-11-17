using Silk.NET.OpenGL;

namespace Vocaterra.Game.Graphics.Gl;

/// <summary>A chunk part's mesh in vertex buffers of the game's own (B17a, B17c): 24-byte vertices (<see cref="World.MeshVertex"/>), four to a quad, and beside them, in a buffer of its own, the 4-byte light word of each vertex (<see cref="World.LightData"/>) when the mesh carries its light as data; drawn with the shared quad indices.</summary>
public sealed class GlChunkBuffer : IDisposable
{
    private readonly GL _gl;
    private uint _handle;
    private uint _lightHandle;

    internal GlChunkBuffer(GL gl, uint handle, int quads, uint lightHandle = 0)
    {
        _gl = gl;
        _handle = handle;
        _lightHandle = lightHandle;
        Quads = quads;
    }

    /// <summary>The number of quads in the buffer.</summary>
    public int Quads { get; }

    internal uint Handle => _handle;

    /// <summary>The buffer of the light words, or 0 for a mesh whose colours are baked.</summary>
    internal uint LightHandle => _lightHandle;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_handle != 0)
        {
            _gl.DeleteBuffer(_handle);
            _handle = 0;
        }

        if (_lightHandle != 0)
        {
            _gl.DeleteBuffer(_lightHandle);
            _lightHandle = 0;
        }
    }
}
