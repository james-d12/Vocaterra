using Silk.NET.OpenGL;

namespace Vocaterra.Game.Graphics.Gl;

/// <summary>
/// What the game's own GL work of a frame changes of the context's state, read before it and put back after it (B17a; the scope round all of a frame's chunk passes since B17b: one <see cref="Capture"/> and one
/// <see cref="Restore"/> a frame, and no MonoGame call between them). MonoGame keeps a cache of what it believes the context holds
/// (its blend, depth, rasteriser and sampler states, the bound program, buffers and textures) and skips a call when the cache says it is already done; a pass of ours that left the
/// context in another state would make the cache wrong, and MonoGame's next draw would come out with our state. The cache is not readable from outside, but it is only ever wrong when the
/// context differs from what it was when the pass began, so the rule is: read the real state before, set what the pass needs, and write back exactly what was read. Whatever MonoGame
/// had pending (a state set on the device and not yet applied) stays pending, because the real state is again the one the cache last knew.
/// </summary>
/// <remarks>
/// What is saved: the program, the vertex array and array buffer bindings, the active texture unit and the 2D texture and sampler bound on the unit the passes use, the enable bits the passes
/// touch (depth test, cull face, blend, stencil test, scissor test, polygon offset, primitive restart), the depth function and mask, the cull mode and front face, the colour mask, the blend function
/// and equation (the glow's and the water's), the pixel store's unpack alignment (the texture uploads), the viewport and the depth range. What is not: the element array buffer (it is part of the vertex
/// array, and the passes have a vertex array of their own), the framebuffer (never changed), the polygon offset's factors (only the enable bit is toggled), line and point sizes, clip distances, the
/// uniform values of other programs (a program keeps its own).
/// </remarks>
public sealed class GlStateSaver
{
    private readonly GL _gl;
    private readonly int _textureUnit;
    private int _program;
    private int _vertexArray;
    private int _arrayBuffer;
    private int _activeTexture;
    private int _texture2D;
    private int _sampler;
    private bool _depthTest;
    private bool _cullFace;
    private bool _blend;
    private bool _stencilTest;
    private bool _scissorTest;
    private bool _polygonOffsetFill;
    private bool _primitiveRestart;
    private bool _primitiveRestartFixed;
    private readonly int[] _blendFunction = new int[4];
    private readonly int[] _blendEquation = new int[2];
    private int _unpackAlignment;
    private int _depthFunc;
    private bool _depthMask;
    private int _cullMode;
    private int _frontFace;
    private readonly bool[] _colourMask = new bool[4];
    private readonly int[] _viewport = new int[4];
    private readonly double[] _depthRange = new double[2];
    private bool _captured;
    private readonly bool _fixedRestartIndex;

    /// <summary>Creates a saver for the passes that use the given texture unit (0 to 31).</summary>
    /// <param name="gl">The binding.</param>
    /// <param name="textureUnit">The unit the passes sample from.</param>
    /// <param name="fixedRestartIndex">Whether the context knows the fixed primitive restart index (OpenGL 4.3 or ARB_ES3_compatibility): its enable bit is saved too.</param>
    public GlStateSaver(GL gl, int textureUnit, bool fixedRestartIndex = false)
    {
        _gl = gl;
        _textureUnit = textureUnit;
        _fixedRestartIndex = fixedRestartIndex;
    }

    /// <summary>Whether a scope is open (<see cref="Capture"/> was called and <see cref="Restore"/> has not been yet).</summary>
    public bool IsOpen => _captured;

    /// <summary>How many GL errors the pass left that had to be cleared, over the run (MonoGame checks for an error after each of its own calls in a debug build and would blame its own call).</summary>
    public int ClearedErrors { get; private set; }

    /// <summary>The first error cleared, for the log (<see cref="GLEnum.NoError"/> when none).</summary>
    public GLEnum FirstError { get; private set; }

    /// <summary>Reads the state the pass is about to change. Errors left by the code before are cleared first, so the pass's own are told apart.</summary>
    public void Capture()
    {
        DrainErrors(countThem: false);
        _program = _gl.GetInteger(GLEnum.CurrentProgram);
        _vertexArray = _gl.GetInteger(GLEnum.VertexArrayBinding);
        _arrayBuffer = _gl.GetInteger(GLEnum.ArrayBufferBinding);
        _activeTexture = _gl.GetInteger(GLEnum.ActiveTexture);
        _gl.ActiveTexture((GLEnum)((int)GLEnum.Texture0 + _textureUnit));
        _texture2D = _gl.GetInteger(GLEnum.TextureBinding2D);
        _sampler = _gl.GetInteger(GLEnum.SamplerBinding);
        _depthTest = _gl.IsEnabled(EnableCap.DepthTest);
        _cullFace = _gl.IsEnabled(EnableCap.CullFace);
        _blend = _gl.IsEnabled(EnableCap.Blend);
        _stencilTest = _gl.IsEnabled(EnableCap.StencilTest);
        _scissorTest = _gl.IsEnabled(EnableCap.ScissorTest);
        _polygonOffsetFill = _gl.IsEnabled(EnableCap.PolygonOffsetFill);
        _primitiveRestart = _gl.IsEnabled(EnableCap.PrimitiveRestart);
        _primitiveRestartFixed = _fixedRestartIndex && _gl.IsEnabled(PrimitiveRestartFixedIndex);
        _gl.GetInteger(GLEnum.BlendSrcRgb, out _blendFunction[0]);
        _gl.GetInteger(GLEnum.BlendDstRgb, out _blendFunction[1]);
        _gl.GetInteger(GLEnum.BlendSrcAlpha, out _blendFunction[2]);
        _gl.GetInteger(GLEnum.BlendDstAlpha, out _blendFunction[3]);
        _gl.GetInteger(GLEnum.BlendEquationRgb, out _blendEquation[0]);
        _gl.GetInteger(GLEnum.BlendEquationAlpha, out _blendEquation[1]);
        _gl.GetInteger(GLEnum.UnpackAlignment, out _unpackAlignment);
        _depthFunc = _gl.GetInteger(GLEnum.DepthFunc);
        _depthMask = _gl.GetBoolean(GLEnum.DepthWritemask);
        _cullMode = _gl.GetInteger(GLEnum.CullFaceMode);
        _frontFace = _gl.GetInteger(GLEnum.FrontFace);
        Span<int> mask = stackalloc int[4];
        _gl.GetInteger(GLEnum.ColorWritemask, mask);
        for (var i = 0; i < 4; i++)
        {
            _colourMask[i] = mask[i] != 0;
        }

        _gl.GetInteger(GLEnum.Viewport, _viewport);
        _gl.GetDouble(GLEnum.DepthRange, _depthRange);
        _captured = true;
    }

    /// <summary>Writes back what <see cref="Capture"/> read, and clears the errors the pass left.</summary>
    public void Restore()
    {
        if (!_captured)
        {
            return;
        }

        _gl.UseProgram((uint)_program);
        _gl.BindVertexArray((uint)_vertexArray);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, (uint)_arrayBuffer);
        _gl.BindTexture(TextureTarget.Texture2D, (uint)_texture2D);
        _gl.BindSampler((uint)_textureUnit, (uint)_sampler);
        _gl.ActiveTexture((GLEnum)_activeTexture);
        Set(EnableCap.DepthTest, _depthTest);
        Set(EnableCap.CullFace, _cullFace);
        Set(EnableCap.Blend, _blend);
        Set(EnableCap.StencilTest, _stencilTest);
        Set(EnableCap.ScissorTest, _scissorTest);
        Set(EnableCap.PolygonOffsetFill, _polygonOffsetFill);
        Set(EnableCap.PrimitiveRestart, _primitiveRestart);
        if (_fixedRestartIndex)
        {
            Set(PrimitiveRestartFixedIndex, _primitiveRestartFixed);
        }

        _gl.BlendFuncSeparate((BlendingFactor)_blendFunction[0], (BlendingFactor)_blendFunction[1], (BlendingFactor)_blendFunction[2], (BlendingFactor)_blendFunction[3]);
        _gl.BlendEquationSeparate((BlendEquationModeEXT)_blendEquation[0], (BlendEquationModeEXT)_blendEquation[1]);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, _unpackAlignment);
        _gl.DepthFunc((DepthFunction)_depthFunc);
        _gl.DepthMask(_depthMask);
        _gl.CullFace((TriangleFace)_cullMode);
        _gl.FrontFace((FrontFaceDirection)_frontFace);
        _gl.ColorMask(_colourMask[0], _colourMask[1], _colourMask[2], _colourMask[3]);
        _gl.Viewport(_viewport[0], _viewport[1], (uint)_viewport[2], (uint)_viewport[3]);
        _gl.DepthRange(_depthRange[0], _depthRange[1]);
        DrainErrors(countThem: true);
        _captured = false;
    }

    private const EnableCap PrimitiveRestartFixedIndex = (EnableCap)0x8D69; // GL_PRIMITIVE_RESTART_FIXED_INDEX (4.3)

    private void Set(EnableCap capability, bool on)
    {
        if (on)
        {
            _gl.Enable(capability);
        }
        else
        {
            _gl.Disable(capability);
        }
    }

    private void DrainErrors(bool countThem)
    {
        GLEnum error;
        var guard = 0;
        while ((error = _gl.GetError()) != GLEnum.NoError && guard++ < 16)
        {
            if (countThem)
            {
                ClearedErrors++;
                if (FirstError == GLEnum.NoError)
                {
                    FirstError = error;
                }
            }
        }
    }
}
