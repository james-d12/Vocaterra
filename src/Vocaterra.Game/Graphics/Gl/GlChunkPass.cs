using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Silk.NET.OpenGL;
using GlPrimitive = Silk.NET.OpenGL.PrimitiveType;

namespace Vocaterra.Game.Graphics.Gl;

/// <summary>What a stretch of the chunk draws is, which sets the context's state for it (<see cref="GlChunkPass.Use"/>).</summary>
public enum GlPassKind
{
    /// <summary>The opaque faces and the lit pieces: depth test and write (less or equal), back faces culled, no blending, the alpha test on.</summary>
    Opaque,

    /// <summary>The cut-outs (the plants, the grass layer): as <see cref="Opaque"/> with both sides of a quad showing.</summary>
    Cutout,

    /// <summary>The water: translucent (source alpha over the picture), depth tested and not written, no alpha test, the water's own animated texture, the light as data with the water shader's tint, movement and edge.</summary>
    Water,

    /// <summary>The far view's land (B17c): opaque, back faces culled, the baked colour times the sun's light, brought from the day's sun to the moment's by each face's own direction; the atlas or the noise by <see cref="GlChunkPass.SetFarTexture"/>.</summary>
    FarLand,

    /// <summary>The far view's blocks that give light (lava): as <see cref="FarLand"/> with no light of the sun on them, so they glow at night.</summary>
    FarLit,

    /// <summary>The far view's water: translucent, depth tested and not written, the noise texture, the baked colour times the sun's light.</summary>
    FarWater,
}

/// <summary>
/// Every chunk pass drawn through the game's own GLSL program on MonoGame's context (B17a, B17b, B17c): the opaque faces, the lit pieces, the cut-outs, B18's grass layer, the water, and the far view's land. One program
/// (<c>chunk.vert.glsl</c>, <c>chunk.frag.glsl</c>) does what <c>AlphaTestEffect</c> and <c>BasicEffect</c> do for the chunks (the texture times the vertex colour, the alpha test, the fog) and, since B17c, the light: the chunk meshes carry each
/// corner's sky light, block light, occlusion and face as data (<see cref="World.LightData"/>, a second vertex stream) and the shader combines them with the frame's sun and glow (<see cref="GlLightFrame"/>), with a fog by height and the
/// water shader. The vertex data is in buffers of this class's own, the atlas, the water's frames and the far view's textures in textures of its own, and one shared quad index buffer. The passes that meet by depth equality share the program and its
/// bit-identical position transform. <see cref="Begin"/> and <see cref="End"/> are the one state scope of a stretch of drawing (<see cref="GlStateSaver"/>), the chunks' and, apart, the far view's: no MonoGame call may stand between them.
/// </summary>
public sealed unsafe class GlChunkPass : IDisposable
{
    /// <summary>The texture unit the pass samples from; MonoGame's samplers use the lowest ones, and the unit's own binding is saved and put back like the rest.</summary>
    public const int TextureUnit = 8;

    private const int VertexSize = World.MeshVertex.SizeInBytes;
    private const float AlphaReference = 110f / 255f;

    // The mesher's table of how much of its colour a corner keeps by its occlusion level (B17c), the shader's too.
    private static readonly float[] AoKeep = [World.ChunkMesher.AoKeepOf(0), World.ChunkMesher.AoKeepOf(1), World.ChunkMesher.AoKeepOf(2), World.ChunkMesher.AoKeepOf(3)];

    // MonoGame's sampler state of the water's frames has MaxMipLevel 5 (WaterTileLayout.MaxMipLevel: the coarsest level that still keeps one tile's picture apart from its neighbour's), which its OpenGL code sets as the
    // texture's last mip level (TEXTURE_MAX_LEVEL); the block atlas has none (all its levels are sampled). B17b's first version took it for the base level, and the water came out smooth where the effects' has its glints.
    private const int WaterMaxLevel = 5;

    private readonly GL _gl;
    private readonly GlProgram _program;
    private readonly GlStateSaver _state;
    private readonly uint _vertexArray;
    private readonly uint _indexBuffer;
    private readonly uint _atlas;
    private uint _farAtlas;
    private uint _noise;
    private readonly uint[] _waterFrames;
    private readonly int _uWorldViewProjection;
    private readonly int _uFogVector;
    private readonly int _uDiffuse;
    private readonly int _uFogColour;
    private readonly int _uAlphaReference;
    private readonly int _uAtlas;
    private readonly int _uFlipY;
    private readonly int _uLightMode;
    private readonly int _uWater;
    private readonly int _uRelight;
    private bool _inPass;
    private int _modeWanted;
    private int _modeSet = -1;
    private GlLightFrame _light;
    private GlPassKind? _kind;
    private Vector3 _fogColour;
    private int _drawCalls;
    private int _drawnQuads;
    private int _stateChanges;

    private GlChunkPass(GL gl, GlProgram program, uint vertexArray, uint indexBuffer, uint atlas, uint[] waterFrames, GlStateSaver state)
    {
        _gl = gl;
        _program = program;
        _vertexArray = vertexArray;
        _indexBuffer = indexBuffer;
        _atlas = atlas;
        _waterFrames = waterFrames;
        _state = state;
        _uWorldViewProjection = program.Uniform("uWorldViewProjection");
        _uFogVector = program.Uniform("uFogVector");
        _uDiffuse = program.Uniform("uDiffuse");
        _uFogColour = program.Uniform("uFogColour");
        _uAlphaReference = program.Uniform("uAlphaReference");
        _uAtlas = program.Uniform("uAtlas");
        _uFlipY = program.Uniform("uFlipY");
        _uLightMode = program.Uniform("uLightMode");
        _uWater = program.Uniform("uWater");
        _uRelight = program.Uniform("uRelight");
    }

    /// <summary>The uniforms of the program the pass sets (a name here that the sources do not declare would be set to nothing, silently: a test holds the two lists together).</summary>
    public static IReadOnlyList<string> UniformNames { get; } =
    [
        "uWorldViewProjection", "uFogVector", "uEye", "uDiffuse", "uFlipY", "uLightMode", "uWater", "uSunDirection", "uSunColour", "uShadeTint", "uSunTint", "uBottomTint", "uSunHeight", "uAoKeep",
        "uGlowColour", "uHeightFog", "uWaterShader", "uTime", "uWaveAmount", "uAtlas", "uFogColour", "uAlphaReference", "uRelight", "uDayDirection",
        "uPlayerLight", "uPlayerLightColour",
    ];

    /// <summary>The vertex attributes of the program, by location: the position, the colour and the atlas coordinate from the 24-byte <see cref="World.MeshVertex"/> stream, and the light word of <see cref="World.LightData"/> from the 4-byte stream beside it.</summary>
    public static IReadOnlyList<(int Location, string Name, int Components, string Type)> Attributes { get; } =
    [
        (0, "aPosition", 3, "vec3"),
        (1, "aColour", 4, "vec4"),
        (2, "aUv", 2, "vec2"),
        (3, "aLight", 4, "uvec4"),
    ];

    /// <summary>What the driver says about the context (logged at the start).</summary>
    public GlInfo Info { get; private init; } = null!;

    /// <summary>The binding's functions on MonoGame's context.</summary>
    public GL Gl => _gl;

    /// <summary>GL errors the pass raised and cleared so far (0 in a healthy run), and the first one.</summary>
    public (int Count, GLEnum First) Errors => (_state.ClearedErrors, _state.FirstError);

    /// <summary>The number of draw calls and quads of the last frame's scope (<see cref="Begin"/> to <see cref="End"/>), and how many times the state was set for another kind of draw.</summary>
    public (int DrawCalls, int Quads, int StateChanges) LastPass { get; private set; }

    /// <summary>Whether the scope is open (<see cref="Begin"/> was called and <see cref="End"/> has not been yet).</summary>
    public bool InScope => _inPass;

    /// <summary>
    /// Loads the functions, reads the context, compiles the program and makes the pass's resources: its vertex array, the quad index buffer, a copy of the atlas texture and a copy of each frame of the water. Returns
    /// <see langword="null"/> with the reason in <paramref name="error"/> when the context cannot run it (an old OpenGL, no SDL, a shader that did not compile), and the game then draws with the effects as always.
    /// </summary>
    /// <param name="device">MonoGame's graphics device, on the thread that holds its context.</param>
    /// <param name="atlas">The block atlas whose texels (every mip level) are copied to a texture of the pass's own.</param>
    /// <param name="waterFrames">The water's animation frames, copied the same way (in order).</param>
    /// <param name="maxQuads">The most quads a mesh holds (the shared index buffer's size).</param>
    /// <param name="log">Where the context's facts and a failure are said.</param>
    /// <param name="error">The reason it could not be made, or an empty string.</param>
    public static GlChunkPass? TryCreate(GraphicsDevice device, Texture2D atlas, IReadOnlyList<Texture2D> waterFrames, int maxQuads, Action<string> log, out string error)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(waterFrames);
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

        if (!info.CanRunChunkPasses)
        {
            error = $"the context is OpenGL {info.Major}.{info.Minor}; the chunk passes need 3.3 (GLSL 330)";
            return null;
        }

        var program = GlProgram.TryCreate(gl, "chunk", ShaderSources.Load(ShaderSources.ChunkVertex), ShaderSources.Load(ShaderSources.ChunkFragment), out error);
        if (program is null)
        {
            return null;
        }

        // The texels are all read before anything is bound: MonoGame's GetData binds its own texture on the active unit, which would take the place of ours.
        var atlasLevels = ReadLevels(atlas);
        var frameLevels = waterFrames.Select(ReadLevels).ToList();
        var state = new GlStateSaver(gl, TextureUnit, info.FixedRestartIndex);
        state.Capture(); // the creation binds a vertex array, a buffer and textures: it is a scope like any other
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
        gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4); // the texels are four bytes each (RGBA8)
        var atlasTexture = UploadTexture(gl, atlas.Width, atlas.Height, atlasLevels, atlasLevels.Length - 1, GLEnum.NearestMipmapLinear, GLEnum.Nearest); // the block atlas sampler's settings: nearest within a level, linear between
        var frames = new uint[frameLevels.Count];
        for (var i = 0; i < frames.Length; i++)
        {
            frames[i] = UploadTexture(gl, waterFrames[i].Width, waterFrames[i].Height, frameLevels[i], WaterMaxLevel, GLEnum.LinearMipmapLinear, GLEnum.Linear); // WaterTextures.Sampler: trilinear
        }

        state.Restore();
        if (state.ClearedErrors > 0)
        {
            error = $"the pass's resources raised GL error {state.FirstError}";
            gl.DeleteBuffer(indexBuffer);
            gl.DeleteVertexArray(vertexArray);
            gl.DeleteTexture(atlasTexture);
            gl.DeleteTextures(frames);
            program.Dispose();
            return null;
        }

        error = string.Empty;
        return new GlChunkPass(gl, program, vertexArray, indexBuffer, atlasTexture, frames, state) { Info = info };
    }

    private static Color[][] ReadLevels(Texture2D texture)
    {
        var levels = new Color[texture.LevelCount][];
        for (var level = 0; level < levels.Length; level++)
        {
            var width = Math.Max(1, texture.Width >> level);
            var height = Math.Max(1, texture.Height >> level);
            levels[level] = new Color[width * height];
            texture.GetData(level, null, levels[level], 0, levels[level].Length);
        }

        return levels;
    }

    // The texels, every mip level, in a GL texture sampled the way the MonoGame sampler state says (clamped, the given filters, the given coarsest level).
    private static uint UploadTexture(GL gl, int baseWidth, int baseHeight, Color[][] levels, int maxLevel, GLEnum minFilter, GLEnum magFilter, GLEnum wrap = GLEnum.ClampToEdge)
    {
        gl.ActiveTexture((GLEnum)((int)GLEnum.Texture0 + TextureUnit));
        var texture = gl.GenTexture();
        gl.BindTexture(TextureTarget.Texture2D, texture);
        gl.BindSampler(TextureUnit, 0);
        for (var level = 0; level < levels.Length; level++)
        {
            var width = Math.Max(1, baseWidth >> level);
            var height = Math.Max(1, baseHeight >> level);
            gl.TexImage2D<Color>(TextureTarget.Texture2D, level, InternalFormat.Rgba8, (uint)width, (uint)height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, levels[level]);
        }

        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, maxLevel);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)minFilter);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)magFilter);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)wrap);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)wrap);
        return texture;
    }

    /// <summary>Uploads a chunk part's quads to a buffer of the pass's own (the first <paramref name="quads"/> quads of the mesh), or returns <see langword="null"/> for none. Draws nothing and leaves MonoGame's bindings alone: it binds the copy-write target, which MonoGame does not use.</summary>
    public GlChunkBuffer? Upload(ReadOnlySpan<World.MeshVertex> vertices, int quads) => Upload(vertices, default, quads);

    /// <summary>
    /// Uploads a chunk part's quads and, when the mesh carries its light as data (B17c), the light words beside them in a buffer of their own (4 bytes a vertex: <see cref="World.LightData"/>), or returns <see langword="null"/> for no quads.
    /// A mesh with no light words (the lit pieces' finished colours are in a data mesh with the fixed word, so they have words too) is drawn in the baked mode.
    /// </summary>
    /// <param name="vertices">The vertices, four to a quad.</param>
    /// <param name="lights">The light word of each vertex, or empty for a mesh whose colours are baked.</param>
    /// <param name="quads">How many quads.</param>
    public GlChunkBuffer? Upload(ReadOnlySpan<World.MeshVertex> vertices, ReadOnlySpan<uint> lights, int quads)
    {
        if (quads <= 0)
        {
            return null;
        }

        var handle = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, handle);
        _gl.BufferData(BufferTargetARB.CopyWriteBuffer, vertices[..(quads * 4)], BufferUsageARB.StaticDraw);
        uint lightHandle = 0;
        if (lights.Length >= quads * 4)
        {
            lightHandle = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, lightHandle);
            _gl.BufferData(BufferTargetARB.CopyWriteBuffer, lights[..(quads * 4)], BufferUsageARB.StaticDraw);
        }

        _gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, 0);
        return new GlChunkBuffer(_gl, handle, quads, lightHandle);
    }

    /// <summary>Uploads a far-view tile's mesh (B17c): the vertices (<c>VertexPositionColorTexture</c>, 24 bytes each) and the indices (16-bit, or 32-bit when <paramref name="indices16"/> is <see langword="null"/>) into buffers of the pass's own. The arrays may be longer than the counts (the mesher's pooled arrays). Binds the copy-write target only.</summary>
    /// <param name="vertices">The vertices' array.</param>
    /// <param name="vertexCount">How many of them are the tile's.</param>
    /// <param name="indices16">The 16-bit indices' array, or <see langword="null"/>.</param>
    /// <param name="indices32">The 32-bit indices' array when there are no 16-bit ones.</param>
    /// <param name="indexCount">How many indices the tile has.</param>
    public GlFarBuffers UploadFar(VertexPositionColorTexture[] vertices, int vertexCount, ushort[]? indices16, int[]? indices32, int indexCount)
    {
        var vertexHandle = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, vertexHandle);
        _gl.BufferData(BufferTargetARB.CopyWriteBuffer, new ReadOnlySpan<VertexPositionColorTexture>(vertices, 0, vertexCount), BufferUsageARB.StaticDraw);
        var indexHandle = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, indexHandle);
        if (indices16 is not null)
        {
            _gl.BufferData(BufferTargetARB.CopyWriteBuffer, new ReadOnlySpan<ushort>(indices16, 0, indexCount), BufferUsageARB.StaticDraw);
        }
        else
        {
            _gl.BufferData(BufferTargetARB.CopyWriteBuffer, new ReadOnlySpan<int>(indices32!, 0, indexCount), BufferUsageARB.StaticDraw);
        }

        _gl.BindBuffer(BufferTargetARB.CopyWriteBuffer, 0);
        return new GlFarBuffers(_gl, vertexHandle, indexHandle, indices16 is null);
    }

    /// <summary>Draws a run of a far tile's triangle indices (B17c): <paramref name="indexCount"/> indices from the index <paramref name="firstIndex"/>, with the kind and the texture in force.</summary>
    /// <param name="tile">The tile's buffers.</param>
    /// <param name="firstIndex">The first index of the run.</param>
    /// <param name="indexCount">How many indices.</param>
    public void DrawFar(GlFarBuffers tile, int firstIndex, int indexCount)
    {
        if (indexCount <= 0)
        {
            return;
        }

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, tile.Vertices);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, tile.Indices); // part of this vertex array
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, VertexSize, 0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.UnsignedByte, true, VertexSize, 12);
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, VertexSize, 16);
        if (_modeSet != 0)
        {
            _gl.Uniform1(_uLightMode, 0);
            _modeSet = 0;
        }

        _gl.DisableVertexAttribArray(3);
        var size = tile.Wide ? sizeof(int) : sizeof(ushort);
        _gl.DrawElements(GlPrimitive.Triangles, (uint)indexCount, tile.Wide ? DrawElementsType.UnsignedInt : DrawElementsType.UnsignedShort, (void*)(firstIndex * size));
        _drawCalls++;
        _drawnQuads += indexCount / 6;
    }

    /// <summary>Clears the depth buffer (the far view's pass ends with it, so the chunks drawn after it with their own depth range always stand in front of the far land under them): depth writes on, the scissor test is already off in the scope.</summary>
    public void ClearDepth()
    {
        _gl.DepthMask(true);
        _gl.Clear(ClearBufferMask.DepthBufferBit);
    }

    /// <summary>The sign the clip y is multiplied by, and the front face, for a draw to the back buffer (<paramref name="renderTarget"/> false) or to a render target (true): MonoGame's pictures are upside down in a target, and so is the winding.</summary>
    /// <param name="renderTarget">Whether a render target is bound.</param>
    public static (float FlipY, bool FrontIsClockwise) OrientationFor(bool renderTarget) => renderTarget ? (-1f, true) : (1f, false);

    /// <summary>
    /// Opens the frame's scope: saves the context's state (once), sets the program, the device's viewport and depth range, the uniforms of the frame and the opaque kind's states. No MonoGame call may be made until <see cref="End"/>.
    /// </summary>
    /// <param name="view">The camera's view matrix (the world matrix is the identity).</param>
    /// <param name="projection">The camera's projection matrix.</param>
    /// <param name="fogColour">The fog's colour.</param>
    /// <param name="fogStart">Where the fog starts, in blocks of view-space depth.</param>
    /// <param name="fogEnd">Where the fog is complete.</param>
    /// <param name="viewport">The device's viewport (read before the scope).</param>
    /// <param name="backBufferHeight">The back buffer's height in pixels (MonoGame's viewport y is from the top; OpenGL's is from the bottom).</param>
    /// <param name="renderTarget">Whether a render target is bound (the scene under water): the clip y and the front face turn over.</param>
    /// <param name="light">The frame's light (B17c): the sun, the glow, the height fog, the water shader; <see langword="null"/> for the midday's, with none of the extras.</param>
    public void Begin(in Matrix view, in Matrix projection, Vector3 fogColour, float fogStart, float fogEnd, in Viewport viewport, int backBufferHeight, bool renderTarget, GlLightFrame? light = null)
    {
        if (_inPass)
        {
            throw new InvalidOperationException("A GL scope is already open.");
        }

        _inPass = true;
        _drawCalls = 0;
        _drawnQuads = 0;
        _stateChanges = 0;
        _kind = null;
        _fogColour = fogColour;
        _light = light ?? GlLightFrame.Noon(Vector3.Zero);
        _state.Capture();
        var (flipY, clockwise) = OrientationFor(renderTarget);
        _gl.Viewport(viewport.X, renderTarget ? viewport.Y : backBufferHeight - viewport.Y - viewport.Height, (uint)viewport.Width, (uint)viewport.Height);
        _gl.DepthRange(viewport.MinDepth, viewport.MaxDepth);
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Lequal);
        _gl.ColorMask(true, true, true, true);
        _gl.Disable(EnableCap.StencilTest);
        _gl.Disable(EnableCap.ScissorTest);
        _gl.Disable(EnableCap.PolygonOffsetFill);
        _gl.Disable(EnableCap.PrimitiveRestart); // the quad indices run to 65,535
        if (Info.FixedRestartIndex)
        {
            _gl.Disable((EnableCap)0x8D69);
        }

        _gl.CullFace(TriangleFace.Back);
        _gl.FrontFace(clockwise ? FrontFaceDirection.CW : FrontFaceDirection.Ccw); // MonoGame's CullClockwise: triangles run counter-clockwise seen from outside (clockwise in a render target)
        _gl.BlendEquationSeparate(BlendEquationModeEXT.FuncAdd, BlendEquationModeEXT.FuncAdd);
        _gl.UseProgram(_program.Handle);
        _gl.BindVertexArray(_vertexArray);
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, _indexBuffer); // the far view's tiles bind their own indices to this vertex array: the shared quad indices are put back
        _gl.ActiveTexture((GLEnum)((int)GLEnum.Texture0 + TextureUnit));
        _gl.BindTexture(TextureTarget.Texture2D, _atlas);
        _gl.BindSampler(TextureUnit, 0);
        _gl.Uniform1(_uAtlas, TextureUnit);
        _gl.Uniform1(_uFlipY, flipY);
        SetFrameUniforms();
        var viewProjection = view * projection;
        Span<float> matrix = stackalloc float[16];
        Write(viewProjection, matrix);
        _gl.UniformMatrix4(_uWorldViewProjection, true, matrix); // transposed: the shader dots the position with the matrix's columns
        var scale = fogStart == fogEnd ? 0f : 1f / (fogStart - fogEnd);
        _gl.Uniform4(_uFogVector, view.M13 * scale, view.M23 * scale, view.M33 * scale, fogStart == fogEnd ? 1f : (view.M43 + fogStart) * scale);
        Use(GlPassKind.Opaque);
    }

    // The frame's light: the sun, the glow, the tints and the occlusion table of the mesher (so the baked fallback and the shader share their numbers), the fog by height, the water shader's switch, time and movement.
    private void SetFrameUniforms()
    {
        var l = _light;
        _gl.Uniform3(_program.Uniform("uEye"), l.Eye.X, l.Eye.Y, l.Eye.Z);
        _gl.Uniform3(_program.Uniform("uSunDirection"), l.SunDirection.X, l.SunDirection.Y, l.SunDirection.Z);
        _gl.Uniform3(_program.Uniform("uSunColour"), l.SunColour.X, l.SunColour.Y, l.SunColour.Z);
        _gl.Uniform3(_program.Uniform("uDayDirection"), l.DayDirection.X, l.DayDirection.Y, l.DayDirection.Z);
        _gl.Uniform3(_program.Uniform("uGlowColour"), l.GlowColour.X, l.GlowColour.Y, l.GlowColour.Z);
        var (sr, sg, sb) = World.ChunkMesher.ShadeTint;
        _gl.Uniform3(_program.Uniform("uShadeTint"), sr, sg, sb);
        var (ur, ug, ub) = World.ChunkMesher.SunTint;
        _gl.Uniform3(_program.Uniform("uSunTint"), ur, ug, ub);
        var (br, bg, bb) = World.ChunkMesher.BottomTint;
        _gl.Uniform3(_program.Uniform("uBottomTint"), br, bg, bb);
        _gl.Uniform1(_program.Uniform("uSunHeight"), World.ChunkMesher.SunDirection.Y);
        _gl.Uniform1(_program.Uniform("uAoKeep"), AoKeep);
        _gl.Uniform4(_program.Uniform("uHeightFog"), l.HeightFogDensity, l.HeightFogBase, l.HeightFogScale, 0f);
        _gl.Uniform1(_program.Uniform("uWaterShader"), l.WaterShader ? 1 : 0);
        _gl.Uniform1(_program.Uniform("uTime"), l.Time);
        _gl.Uniform1(_program.Uniform("uWaveAmount"), l.WaveAmount);
        _gl.Uniform4(_program.Uniform("uPlayerLight"), l.PlayerLightPosition.X, l.PlayerLightPosition.Y, l.PlayerLightPosition.Z, l.PlayerLightRadius);
        _gl.Uniform3(_program.Uniform("uPlayerLightColour"), l.PlayerLightColour.X, l.PlayerLightColour.Y, l.PlayerLightColour.Z);
    }

    /// <summary>Sets the context's state for a kind of draw (a no-op when the kind is the one in force). The water's frame is chosen with <see cref="SetWaterFrame"/>, the far view's texture with <see cref="SetFarTexture"/>.</summary>
    /// <param name="kind">The kind.</param>
    public void Use(GlPassKind kind)
    {
        if (_kind == kind)
        {
            return;
        }

        _kind = kind;
        _stateChanges++;
        var far = kind is GlPassKind.FarLand or GlPassKind.FarLit or GlPassKind.FarWater;
        _modeWanted = kind is GlPassKind.Opaque or GlPassKind.Cutout or GlPassKind.Water ? 1 : 0; // the chunks' meshes carry their light as data; the far view's and the glow's colours are baked (a mesh with no light words is drawn baked too: see Draw)
        _modeSet = -1;
        _gl.Uniform1(_uWater, kind == GlPassKind.Water ? 1 : 0);
        _gl.Uniform1(_uRelight, kind == GlPassKind.FarLand ? 1 : 0);
        switch (kind)
        {
            case GlPassKind.Opaque or GlPassKind.Cutout:
                _gl.DepthMask(true);
                _gl.Disable(EnableCap.Blend);
                SetCull(kind == GlPassKind.Opaque);
                _gl.Uniform1(_uAlphaReference, AlphaReference);
                _gl.Uniform3(_uFogColour, _fogColour.X, _fogColour.Y, _fogColour.Z);
                _gl.BindTexture(TextureTarget.Texture2D, _atlas);
                break;
            case GlPassKind.FarLand or GlPassKind.FarLit:
                _gl.DepthMask(true);
                _gl.Disable(EnableCap.Blend);
                SetCull(true);
                _gl.Uniform1(_uAlphaReference, AlphaReference);
                _gl.Uniform3(_uFogColour, _fogColour.X, _fogColour.Y, _fogColour.Z);
                break;
            default:
                _gl.DepthMask(false);
                _gl.Enable(EnableCap.Blend);
                _gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha); // MonoGame's NonPremultiplied
                SetCull(true);
                _gl.Uniform1(_uAlphaReference, -1f); // no alpha test: BasicEffect drew the water
                _gl.Uniform3(_uFogColour, _fogColour.X, _fogColour.Y, _fogColour.Z);
                if (far)
                {
                    _gl.BindTexture(TextureTarget.Texture2D, _noise);
                }

                break;
        }
    }

    /// <summary>Binds the far view's texture for the far draws that follow (call after <see cref="Use"/> with a far kind): the block atlas, sampled as the far view's sampler does (to the level where a texel is a whole cell), or the noise of the flat-coloured tiles.</summary>
    /// <param name="textured">Whether the tile draws every face with its block's own atlas texture.</param>
    public void SetFarTexture(bool textured)
    {
        if (_kind == GlPassKind.FarWater || !HasFarTextures)
        {
            return;
        }

        _gl.BindTexture(TextureTarget.Texture2D, textured ? _farAtlas : _noise);
    }

    /// <summary>Whether the far view's textures were made (<see cref="TryAddFarTextures"/>), so the far view can be drawn through this pass.</summary>
    public bool HasFarTextures => _farAtlas != 0 && _noise != 0;

    /// <summary>
    /// Makes the far view's two textures (B17c): a copy of the block atlas sampled as the far sampler does (nearest within a level, linear between, down to the level at which one texel is a whole cell and no further, so no neighbouring cell bleeds in), and a
    /// copy of the noise the flat-coloured tiles are textured with (every mip level, nearest up close, linear between levels, wrapping). Both are mipped.
    /// </summary>
    /// <param name="atlas">The block atlas's texture (its levels are copied again for the far sampler's own last level).</param>
    /// <param name="noise">The far view's noise texture (<see cref="Far.FarNoise.Create"/>).</param>
    /// <param name="atlasLastLevel">The last mip level the far view samples the atlas at (<c>log2</c> of a cell's size).</param>
    public bool TryAddFarTextures(Texture2D atlas, Texture2D noise, int atlasLastLevel)
    {
        ArgumentNullException.ThrowIfNull(atlas);
        ArgumentNullException.ThrowIfNull(noise);
        if (HasFarTextures)
        {
            return true;
        }

        var atlasLevels = ReadLevels(atlas);
        var noiseLevels = ReadLevels(noise);
        var wasInScope = _inPass;
        if (wasInScope)
        {
            return false; // the textures are made outside a scope (the copies read MonoGame's textures first)
        }

        _state.Capture();
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 4);
        _farAtlas = UploadTexture(_gl, atlas.Width, atlas.Height, atlasLevels, Math.Min(atlasLastLevel, atlasLevels.Length - 1), GLEnum.NearestMipmapLinear, GLEnum.Nearest);
        _noise = UploadTexture(_gl, noise.Width, noise.Height, noiseLevels, noiseLevels.Length - 1, GLEnum.LinearMipmapLinear, GLEnum.Nearest, GLEnum.Repeat);
        _state.Restore();
        return _state.ClearedErrors == 0;
    }

    /// <summary>Binds one frame of the water's art for the water draws that follow (call after <see cref="Use"/> with <see cref="GlPassKind.Water"/>).</summary>
    /// <param name="frame">The frame, 0 to the number of frames given at the start.</param>
    public void SetWaterFrame(int frame) => _gl.BindTexture(TextureTarget.Texture2D, _waterFrames[Math.Clamp(frame, 0, _waterFrames.Length - 1)]);

    private void SetCull(bool on)
    {
        if (on)
        {
            _gl.Enable(EnableCap.CullFace);
        }
        else
        {
            _gl.Disable(EnableCap.CullFace);
        }
    }

    /// <summary>Sets the tint for the draws that follow (the band's tint, white for the lit pieces, the glow's colour): <c>AlphaTestEffect.DiffuseColor</c>.</summary>
    public void SetDiffuse(Vector3 tint) => _gl.Uniform3(_uDiffuse, tint.X, tint.Y, tint.Z);

    /// <summary>Draws one buffer's quads, all of them or a range of them (the grass layer draws a quadrant at a time).</summary>
    /// <param name="buffer">The chunk part's buffer.</param>
    /// <param name="firstQuad">The first quad to draw.</param>
    /// <param name="quads">How many quads, or -1 for the rest.</param>
    public void Draw(GlChunkBuffer buffer, int firstQuad = 0, int quads = -1)
    {
        var count = quads < 0 ? buffer.Quads - firstQuad : quads;
        if (count <= 0)
        {
            return;
        }

        var mode = _modeWanted == 1 && buffer.LightHandle != 0 ? 1 : 0;
        if (mode != _modeSet)
        {
            _gl.Uniform1(_uLightMode, mode);
            _modeSet = mode;
        }

        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer.Handle);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, VertexSize, 0);
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.UnsignedByte, true, VertexSize, 12);
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, VertexSize, 16);
        if (buffer.LightHandle != 0)
        {
            _gl.BindBuffer(BufferTargetARB.ArrayBuffer, buffer.LightHandle);
            _gl.EnableVertexAttribArray(3);
            _gl.VertexAttribIPointer(3, 4, VertexAttribIType.UnsignedByte, World.LightData.SizeInBytes, 0); // the light word: four bytes as integers (sky, block, occlusion | face | flags, extra)
        }
        else
        {
            _gl.DisableVertexAttribArray(3);
        }

        _gl.DrawElementsBaseVertex(GlPrimitive.Triangles, (uint)(count * 6), DrawElementsType.UnsignedShort, (void*)0, firstQuad * 4); // offset 0 into the bound index buffer (this vertex array's)
        _drawCalls++;
        _drawnQuads += count;
    }

    /// <summary>Closes the frame's scope: the context's state is put back as it was before <see cref="Begin"/>.</summary>
    public void End()
    {
        if (!_inPass)
        {
            return;
        }

        _state.Restore();
        LastPass = (_drawCalls, _drawnQuads, _stateChanges);
        _inPass = false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _gl.DeleteTexture(_atlas);
        if (_farAtlas != 0)
        {
            _gl.DeleteTexture(_farAtlas);
        }

        if (_noise != 0)
        {
            _gl.DeleteTexture(_noise);
        }

        _gl.DeleteTextures(_waterFrames);
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
