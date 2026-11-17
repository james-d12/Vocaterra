using Silk.NET.OpenGL;

namespace Vocaterra.Game.Graphics.Gl;

/// <summary>A linked GLSL program: the driver compiles the sources at run time, and a failure is said in the log in the driver's own words (B17a).</summary>
public sealed class GlProgram : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, int> _uniforms = [];

    private GlProgram(GL gl, uint handle)
    {
        _gl = gl;
        Handle = handle;
    }

    /// <summary>The program's GL name.</summary>
    public uint Handle { get; }

    /// <summary>Compiles the two stages and links them. Changes no state that outlives the call (the program is not made current).</summary>
    /// <param name="gl">The binding.</param>
    /// <param name="name">What the program is, for the messages.</param>
    /// <param name="vertexSource">The vertex shader's GLSL.</param>
    /// <param name="fragmentSource">The fragment shader's GLSL.</param>
    /// <param name="error">The driver's compile or link log, with the stage, or an empty string.</param>
    /// <returns>The program, or <see langword="null"/> when a stage did not compile or the link failed.</returns>
    public static GlProgram? TryCreate(GL gl, string name, string vertexSource, string fragmentSource, out string error)
    {
        error = string.Empty;
        var vertex = Compile(gl, ShaderType.VertexShader, vertexSource, out var vertexLog);
        if (vertex == 0)
        {
            error = $"{name}: the vertex shader did not compile:\n{vertexLog}";
            return null;
        }

        var fragment = Compile(gl, ShaderType.FragmentShader, fragmentSource, out var fragmentLog);
        if (fragment == 0)
        {
            gl.DeleteShader(vertex);
            error = $"{name}: the fragment shader did not compile:\n{fragmentLog}";
            return null;
        }

        var program = gl.CreateProgram();
        gl.AttachShader(program, vertex);
        gl.AttachShader(program, fragment);
        gl.LinkProgram(program);
        gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out var linked);
        var log = gl.GetProgramInfoLog(program);
        gl.DetachShader(program, vertex);
        gl.DetachShader(program, fragment);
        gl.DeleteShader(vertex);
        gl.DeleteShader(fragment);
        if (linked == 0)
        {
            gl.DeleteProgram(program);
            error = $"{name}: the program did not link:\n{log}";
            return null;
        }

        return new GlProgram(gl, program);
    }

    /// <summary>The location of a uniform, found once (a name the program does not have, or has optimised away, is -1 and setting it does nothing).</summary>
    public int Uniform(string name)
    {
        if (!_uniforms.TryGetValue(name, out var location))
        {
            location = _gl.GetUniformLocation(Handle, name);
            _uniforms[name] = location;
        }

        return location;
    }

    /// <inheritdoc />
    public void Dispose() => _gl.DeleteProgram(Handle);

    private static uint Compile(GL gl, ShaderType type, string source, out string log)
    {
        var shader = gl.CreateShader(type);
        gl.ShaderSource(shader, source);
        gl.CompileShader(shader);
        gl.GetShader(shader, ShaderParameterName.CompileStatus, out var status);
        log = gl.GetShaderInfoLog(shader);
        if (status == 0)
        {
            gl.DeleteShader(shader);
            return 0;
        }

        return shader;
    }
}
