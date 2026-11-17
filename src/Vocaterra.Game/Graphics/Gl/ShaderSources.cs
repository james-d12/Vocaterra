using System.Reflection;

namespace Vocaterra.Game.Graphics.Gl;

/// <summary>The game's GLSL sources, embedded in the assembly (<c>Graphics/Gl/Shaders/*.glsl</c>, B17a): nothing is searched for next to the executable.</summary>
public static class ShaderSources
{
    /// <summary>The opaque chunk pass's vertex shader.</summary>
    public const string OpaqueVertex = "opaque.vert.glsl";

    /// <summary>The opaque chunk pass's fragment shader.</summary>
    public const string OpaqueFragment = "opaque.frag.glsl";

    /// <summary>The names of every shader source the assembly embeds.</summary>
    public static IReadOnlyList<string> Names { get; } = Assembly.GetExecutingAssembly().GetManifestResourceNames()
        .Where(static name => name.StartsWith(Prefix, StringComparison.Ordinal))
        .Select(static name => name[Prefix.Length..])
        .Order(StringComparer.Ordinal)
        .ToList();

    private const string Prefix = "Vocaterra.Game.Shaders.";

    /// <summary>Reads one embedded source by its file name (<see cref="OpaqueVertex"/>).</summary>
    /// <exception cref="InvalidOperationException">The assembly does not hold it.</exception>
    public static string Load(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(Prefix + name)
            ?? throw new InvalidOperationException($"The shader source '{name}' is not embedded in the assembly.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
