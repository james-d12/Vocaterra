using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Silk.NET.OpenGL;
using GlPrimitive = Silk.NET.OpenGL.PrimitiveType;

namespace Vocaterra.Game.Graphics.Gl;

/// <summary>
/// The opaque chunk pass drawn through the game's own GLSL program on MonoGame's context (B17a, <c>--gl-shaders</c>): what <c>AlphaTestEffect</c> does for the chunks today (the atlas texture, the
/// vertex colour times the band's tint, the alpha test, the linear fog), with the vertex data in buffers of this class's own and the atlas in a texture of its own. Everything else stays on
/// MonoGame; <see cref="Begin"/> and <see cref="End"/> bracket the pass and put the context's state back as it was (<see cref="GlStateSaver"/>).
/// </summary>
public sealed unsafe class GlOpaquePass : IDisposable
{
    /// <summary>The texture unit the pass samples the atlas from; MonoGame's samplers use the lowest ones, and the unit's own binding is saved and put back like the rest.</summary>
    public const int AtlasUnit = 8;

    private const int VertexSize = World.MeshVertex.SizeInBytes;
    private const float AlphaReference = 110f / 255f;

    private readonly GL _gl;
    private readonly GraphicsDevice _device;
    private readonly GlProgram _program;
    private readonly GlStateSaver _state;
    private readonly uint _vertexArray;
    private readonly uint _indexBuffer;
    private readonly uint _atlas;
    private readonly int _uWorldViewProjection;
    private readonly int _uFogVector;
    private readonly int _uDiffuse;
    private readonly int _uFogColour;
    private readonly int _uAlphaReference;
    private readonly int _uAtlas;
    private bool _inPass;
    private int _drawCalls;
    private int _drawnQuads;

    private GlOpaquePass(GL gl, GraphicsDevice device, GlProgram program, uint vertexArray, uint indexBuffer, uint atlas, GlStateSaver state)
    {
        _gl = gl;
        _device = device;
        _program = program;
        _vertexArray = vertexArray;
        _indexBuffer = indexBuffer;
        _atlas = atlas;
        _state = state;
        _uWorldViewProjection = program.Uniform("uWorldViewProjection");
        _uFogVector = program.Uniform("uFogVector");
        _uDiffuse = program.Uniform("uDiffuse");
        _uFogColour = program.Uniform("uFogColour");
        _uAlphaReference = program.Uniform("uAlphaReference");
        _uAtlas = program.Uniform("uAtlas");
    }

    /// <summary>What the driver says about the context (logged at the start).</summary>
    public GlInfo Info { get; private init; } = null!;

    /// <summary>The binding's functions on MonoGame's context.</summary>
    public GL Gl => _gl;

    /// <summary>GL errors the pass raised and cleared so far (0 in a healthy run), and the first one.</summary>
    public (int Count, GLEnum First) Errors => (_state.ClearedErrors, _state.FirstError);

    /// <summary>The number of draw calls and quads of the last pass (<see cref="Begin"/> to <see cref="End"/>).</summary>
    public (int DrawCalls, int Quads) LastPass { get; private set; }

    /// <summary>The number of passes that fell back to MonoGame's effect (a render target was bound, as under water, whose upside-down picture the pass does not draw yet).</summary>
    public int FallbackPasses { get; private set; }

    /// <summary>
    /// Loads the functions, reads the context, compiles the program and makes the pass's resources: its vertex array, the quad index buffer and a copy of the atlas texture. Returns <see langword="null"/>
    /// with the reason in <paramref name="error"/> when the context cannot run it (an old OpenGL, no SDL, a shader that did not compile), and the game then draws as it always did.
    /// </summary>
    /// <param name="device">MonoGame's graphics device, on the thread that holds its context.</param>
    /// <param name="atlas">The block atlas whose texels (every mip level) are copied to a texture of the pass's own.</param>
    /// <param name="maxQuads">The most quads a mesh holds (the shared index buffer's size).</param>
    /// <param name="log">Where the context's facts and a failure are said.</param>
    /// <param name="error">The reason it could not be made, or an empty string.</param>
    public static GlOpaquePass? TryCreate(GraphicsDevice device, Texture2D atlas, int maxQuads, Action<string> log, out string error)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(log);
        if (!GlLoader.TryLoad(out var gl, out error) || gl is null)
        {
            return null;
        }

        var info = GlInfo.Query(gl);
        foreach (var line in info.LogLines())
        {
            log(line);
        }

        if (!info.CanRunOpaquePass)
        {
            error = $"the context is OpenGL {info.Major}.{info.Minor}; the pass needs 3.3 (GLSL 330)";
            return null;
        }

        var program = GlProgram.TryCreate(gl, "opaque", ShaderSources.Load(ShaderSources.OpaqueVertex), ShaderSources.Load(ShaderSources.OpaqueFragment), out error);
        if (program is null)
        {
            return null;
        }

        var state = new GlStateSaver(gl, AtlasUnit);
        state.Capture(); // the creation binds a vertex array, a buffer and a texture: it is a pass like any other
        var vertexArray = gl.GenVertexArray();
        gl.BindVertexArray(vertexArray);
        var indexBuffer = gl.GenBuffer();
        gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, indexBuffer); // part of this vertex array: MonoGame's own index buffer binding is not touched
        var indices = new ushort[maxQuads * 6];
        for (var quad = 0; quad < maxQuads; quad++)
        {
            var v = (ushort)(quad * 4);
            var i = quad * 6;
            indices[i] = v;
            indices[i + 1] = (ushort)(v + 1);
            indices[i + 2] = (ushort)(v + 2);
            indices[i + 3] = v;
            indices[i + 4] = (ushort)(v + 2);
            indices[i + 5] = (ushort)(v + 3);
        }

        gl.BufferData<ushort>(BufferTargetARB.ElementArrayBuffer, indices, BufferUsageARB.StaticDraw);
        var texture = UploadAtlas(gl, atlas);
        state.Restore();
        if (state.ClearedErrors > 0)
        {
            error = $"the pass's resources raised GL error {state.FirstError}";
            gl.DeleteBuffer(indexBuffer);
            gl.DeleteVertexArray(vertexArray);
            gl.DeleteTexture(texture);
            program.Dispose();
            return null;
        }

        error = string.Empty;
        return new GlOpaquePass(gl, device, program, vertexArray, indexBuffer, texture, state) { Info = info };
    }

    // The atlas's texels, every mip level, copied to a GL texture sampled the way the block atlas's sampler state says (nearest within a level, linear between levels, clamped).
    private static uint UploadAtlas(GL gl, Texture2D atlas)
    {
        // The texels are read first: MonoGame's GetData binds its own texture on the active unit, which would take the place of ours.
        var levels = new Color[atlas.LevelCount][];
        for (var level = 0; level < levels.Length; level++)
        {
            var width = Math.Max(1, atlas.Width >> level);
            var height = Math.Max(1, atlas.Height >> level);
            levels[level] = new Color[width * height];
            atlas.GetData(level, null, levels[level], 0, levels[level].Length);
        }

        gl.ActiveTexture((GLEnum)((int)GLEnum.Texture0 + AtlasUnit));
        var texture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, texture);
        gl.BindSampler(AtlasUnit, 0);
        for (var level = 0; level < levels.Length; level++)
        {
            var width = Math.Max(1, atlas.Width >> level);
            var height = Math.Max(1, atlas.Height >> level);
            gl.TexImage2D<Color>(TextureTarget.Texture2D, level, InternalFormat.Rgba8, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, levels[level]);
        }

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, atlas.LevelCount - 1);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)GLEnum.NearestMipmapLinear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)GLEnum.Nearest);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        return texture;
    }

    /// <summary>Uploads a chunk's opaque quads to a buffer of the pass's own (the first <paramref name="quads"/> quads of the mesh), or returns <see langword="null"/> for none. Draws nothing and leaves MonoGame's bindings alone: it binds the copy-write target, which MonoGame does not use.</summary>
    public GlChunkBuffer? Upload(ReadOnlySpan<World.MeshVertex> vertices, int quads)
    {
        if (quads <= 0)
        {
            return null;
        }

        var handle = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, handle);
        _gl.BufferData(BufferTargetARB.CopyWriteBuffer, vertices[..(quads * 4)], BufferUsageARB.StaticDraw);
        _gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, 0);
        return new GlChunkBuffer(_gl, handle, quads);
    }

    /// <summary>Whether the pass can draw now: no render target is bound (a render target flips the picture, which the pass does not do yet; the caller then draws with MonoGame's effect).</summary>
    public bool CanDrawNow()
    {
        if (CanDraw(_device.RenderTargetCount))
        {
            return true;
        }

        FallbackPasses++;
        return false;
    }

    /// <summary>The rule of <see cref="CanDrawNow"/> on its own: the pass draws to the back buffer only.</summary>
    /// <param name="renderTargetCount">How many render targets are bound.</param>
    public static bool CanDraw(int renderTargetCount) => renderTargetCount == 0;

    /// <summary>
    /// Starts the pass: saves the context's state, sets the program, the opaque pass's states (depth test and write, less or equal, culling of clockwise triangles, no blending, no scissor, the
    /// device's viewport and depth range) and the frame's uniforms.
    /// </summary>
    /// <param name="view">The camera's view matrix (the world matrix is the identity).</param>
    /// <param name="projection">The camera's projection matrix.</param>
    /// <param name="fogColour">The fog's colour.</param>
    /// <param name="fogStart">Where the fog starts, in blocks of view-space depth.</param>
    /// <param name="fogEnd">Where the fog is complete.</param>
    public void Begin(in Matrix view, in Matrix projection, Vector3 fogColour, float fogStart, float fogEnd)
    {
        if (_inPass)
        {
            throw new InvalidOperationException("A GL pass is already open.");
        }

        _inPass = true;
        _drawCalls = 0;
        _drawnQuads = 0;
        _state.Capture();
        var viewport = _device.Viewport;
        _gl.Viewport(viewport.X, _device.PresentationParameters.BackBufferHeight - viewport.Y - viewport.Height, (uint)viewport.Width, (uint)viewport.Height);
        _gl.DepthRange(viewport.MinDepth, viewport.MaxDepth);
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Lequal);
        _gl.DepthMask(true);
        _gl.ColorMask(true, true, true, true);
        _gl.Enable(EnableCap.CullFace);
        _gl.CullFace(TriangleFace.Back);
        _gl.FrontFace(FrontFaceDirection.Ccw); // MonoGame's CullClockwise: triangles run counter-clockwise seen from outside
        _gl.Disable(EnableCap.Blend);
        _gl.Disable(EnableCap.StencilTest);
        _gl.Disable(EnableCap.ScissorTest);
        _gl.Disable(EnableCap.PolygonOffsetFill);
        _gl.UseProgram(_program.Handle);
        _gl.BindVertexArray(_vertexArray);
        _gl.ActiveTexture((GLEnum)((int)GLEnum.Texture0 + AtlasUnit));
        _gl.BindTexture(TextureTarget.Texture2D, _atlas);
        _gl.BindSampler(AtlasUnit, 0);
        _gl.Uniform1(_uAtlas, AtlasUnit);
        _gl.Uniform1(_uAlphaReference, AlphaReference);
        _gl.Uniform3(_uFogColour, fogColour.X, fogColour.Y, fogColour.Z);
        var viewProjection = view * projection;
        Span<float> matrix = stackalloc float[16];
        Write(viewProjection, matrix);
        _gl.UniformMatrix4(_uWorldViewProjection, false, matrix);
        var scale = fogStart == fogEnd ? 0f : 1f / (fogStart - fogEnd);
        _gl.Uniform4(_uFogVector, view.M13 * scale, view.M23 * scale, view.M33 * scale, fogStart == fogEnd ? 1f : (view.M43 + fogStart) * scale);
    }

    /// <summary>Sets the band's tint for the draws that follow (<c>AlphaTestEffect.DiffuseColor</c>).</summary>
    public void SetDiffuse(Vector3 tint) => _gl.Uniform3(_uDiffuse, tint.X, tint.Y, tint.Z);

    /// <summary>Draws one chunk's opaque quads.</summary>
    public void Draw(GlChunkBuffer buffer)
    {
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer.Handle);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, VertexSize, 0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.UnsignedByte, true, VertexSize, 12);
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, VertexSize, 16);
        _gl.DrawElements(GlPrimitive.Triangles, (uint)(buffer.Quads * 6), DrawElementsType.UnsignedShort, (void*)0); // offset 0 into the bound index buffer (this vertex array's)
        _drawCalls++;
        _drawnQuads += buffer.Quads;
    }

    /// <summary>Ends the pass: the context's state is put back as it was before <see cref="Begin"/>.</summary>
    public void End()
    {
        if (!_inPass)
        {
            return;
        }

        _state.Restore();
        LastPass = (_drawCalls, _drawnQuads);
        _inPass = false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _gl.DeleteTexture(_atlas);
        _gl.DeleteBuffer(_indexBuffer);
        _gl.DeleteVertexArray(_vertexArray);
        _program.Dispose();
    }

    private static void Write(in Matrix m, Span<float> target)
    {
        target[0] = m.M11; target[1] = m.M12; target[2] = m.M13; target[3] = m.M14;
        target[4] = m.M21; target[5] = m.M22; target[6] = m.M23; target[7] = m.M24;
        target[8] = m.M31; target[9] = m.M32; target[10] = m.M33; target[11] = m.M34;
        target[12] = m.M41; target[13] = m.M42; target[14] = m.M43; target[15] = m.M44;
    }
}
