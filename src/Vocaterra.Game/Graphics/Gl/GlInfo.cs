using System.Globalization;
using Silk.NET.OpenGL;

namespace Vocaterra.Game.Graphics.Gl;

/// <summary>What the driver says about the context MonoGame made (B17a): the version strings, the profile and the 4.x features the later steps of B17 and B19 would use.</summary>
/// <param name="Version">GL_VERSION.</param>
/// <param name="Renderer">GL_RENDERER.</param>
/// <param name="Vendor">GL_VENDOR.</param>
/// <param name="ShadingLanguage">GL_SHADING_LANGUAGE_VERSION.</param>
/// <param name="Major">GL_MAJOR_VERSION.</param>
/// <param name="Minor">GL_MINOR_VERSION.</param>
/// <param name="Core">Whether the context is a core profile (otherwise a compatibility profile, or one with no profiles, below 3.2).</param>
/// <param name="Compute">Compute shaders (4.3 or ARB_compute_shader).</param>
/// <param name="MultiDrawIndirect">glMultiDrawElementsIndirect (4.3 or ARB_multi_draw_indirect).</param>
/// <param name="IndirectCount">glMultiDrawElementsIndirectCount (4.6 or ARB_indirect_parameters).</param>
/// <param name="BufferStorage">Immutable buffer storage and persistent mapping (4.4 or ARB_buffer_storage).</param>
/// <param name="ClipControl">glClipControl, so a reversed depth range can be used (4.5 or ARB_clip_control).</param>
/// <param name="BaseVertex">Draws with a base vertex (3.2 or ARB_draw_elements_base_vertex).</param>
/// <param name="ShaderStorage">Shader storage buffer objects (4.3 or ARB_shader_storage_buffer_object).</param>
/// <param name="MaxTextureSize">GL_MAX_TEXTURE_SIZE.</param>
/// <param name="MaxArrayLayers">GL_MAX_ARRAY_TEXTURE_LAYERS.</param>
public sealed record GlInfo(
    string Version,
    string Renderer,
    string Vendor,
    string ShadingLanguage,
    int Major,
    int Minor,
    bool Core,
    bool Compute,
    bool MultiDrawIndirect,
    bool IndirectCount,
    bool BufferStorage,
    bool ClipControl,
    bool BaseVertex,
    bool ShaderStorage,
    int MaxTextureSize,
    int MaxArrayLayers)
{
    /// <summary>Reads the driver's answers on the current context.</summary>
    public static GlInfo Query(GL gl)
    {
        ArgumentNullException.ThrowIfNull(gl);
        var major = gl.GetInteger(GLEnum.MajorVersion);
        var minor = gl.GetInteger(GLEnum.MinorVersion);
        var mask = gl.GetInteger(GLEnum.ContextProfileMask);
        var core = (mask & (int)GLEnum.ContextCoreProfileBit) != 0;
        var count = gl.GetInteger(GLEnum.NumExtensions);
        var extensions = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < count; i++)
        {
            extensions.Add(gl.GetStringS(StringName.Extensions, (uint)i));
        }

        bool Has(int atLeastMajor, int atLeastMinor, string extension) => major > atLeastMajor || (major == atLeastMajor && minor >= atLeastMinor) || extensions.Contains(extension);
        return new GlInfo(
            gl.GetStringS(StringName.Version),
            gl.GetStringS(StringName.Renderer),
            gl.GetStringS(StringName.Vendor),
            gl.GetStringS(StringName.ShadingLanguageVersion),
            major,
            minor,
            core,
            Has(4, 3, "GL_ARB_compute_shader"),
            Has(4, 3, "GL_ARB_multi_draw_indirect"),
            Has(4, 6, "GL_ARB_indirect_parameters"),
            Has(4, 4, "GL_ARB_buffer_storage"),
            Has(4, 5, "GL_ARB_clip_control"),
            Has(3, 2, "GL_ARB_draw_elements_base_vertex"),
            Has(4, 3, "GL_ARB_shader_storage_buffer_object"),
            gl.GetInteger(GLEnum.MaxTextureSize),
            gl.GetInteger(GLEnum.MaxArrayTextureLayers));
    }

    /// <summary>The lines the game logs for the context (the log's <c>gl:</c> lines).</summary>
    public IEnumerable<string> LogLines()
    {
        yield return $"gl: version {Version}, renderer {Renderer}, vendor {Vendor}";
        yield return $"gl: shading language {ShadingLanguage}, context {Major}.{Minor} {(Core ? "core" : "compatibility")} profile";
        yield return string.Create(CultureInfo.InvariantCulture, $"gl: compute {Yn(Compute)}, multi-draw indirect {Yn(MultiDrawIndirect)} (with count {Yn(IndirectCount)}), buffer storage {Yn(BufferStorage)}, clip control {Yn(ClipControl)}, base vertex {Yn(BaseVertex)}, shader storage {Yn(ShaderStorage)}, max texture {MaxTextureSize}, max array layers {MaxArrayLayers}");
    }

    /// <summary>Whether the context can run the opaque program (<see cref="GlOpaquePass"/> asks for GLSL 3.30, so OpenGL 3.3).</summary>
    public bool CanRunOpaquePass => Major > 3 || (Major == 3 && Minor >= 3);

    private static string Yn(bool value) => value ? "yes" : "no";
}
