using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Gl;
using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Graphics.Gl;

/// <summary>The game's own GLSL pass (B17a, <c>--gl-shaders</c>): the sources are embedded and agree with each other, the option is off by default, and the built-in path is the one chosen without it.</summary>
public sealed class GlShaderTests
{
    [Fact]
    public void TheShaderSourcesAreEmbeddedInTheAssembly()
    {
        Assert.Contains(ShaderSources.OpaqueVertex, ShaderSources.Names);
        Assert.Contains(ShaderSources.OpaqueFragment, ShaderSources.Names);
        foreach (var name in ShaderSources.Names)
        {
            var source = ShaderSources.Load(name);
            Assert.StartsWith("#version 330 core", source, StringComparison.Ordinal);
            Assert.Contains("void main()", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AnUnknownSourceNameIsRefusedByName()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ShaderSources.Load("nothing.glsl"));
        Assert.Contains("nothing.glsl", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheVertexStageHandsTheFragmentStageWhatItReads()
    {
        var vertex = ShaderSources.Load(ShaderSources.OpaqueVertex);
        var fragment = ShaderSources.Load(ShaderSources.OpaqueFragment);
        foreach (var varying in new[] { "vec4 vColour;", "vec2 vUv;", "float vFog;" })
        {
            Assert.Contains("out " + varying, vertex, StringComparison.Ordinal);
            Assert.Contains("in " + varying, fragment, StringComparison.Ordinal);
        }

        // The vertex layout is MeshVertex's: a position, a colour and a coordinate at locations 0, 1 and 2.
        Assert.Contains("layout(location = 0) in vec3 aPosition", vertex, StringComparison.Ordinal);
        Assert.Contains("layout(location = 1) in vec4 aColour", vertex, StringComparison.Ordinal);
        Assert.Contains("layout(location = 2) in vec2 aUv", vertex, StringComparison.Ordinal);

        // The uniforms the pass sets are declared (a name the program lacks would be set to nothing, silently).
        foreach (var uniform in new[] { "mat4 uWorldViewProjection;", "vec4 uFogVector;", "vec3 uDiffuse;" })
        {
            Assert.Contains("uniform " + uniform, vertex, StringComparison.Ordinal);
        }

        foreach (var uniform in new[] { "sampler2D uAtlas;", "vec3 uFogColour;", "float uAlphaReference;" })
        {
            Assert.Contains("uniform " + uniform, fragment, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheDepthIsMovedFromTheZeroToOneRangeOfMonoGamesProjectionsToOpenGlsClipRange()
    {
        Assert.Contains("gl_Position.z = gl_Position.z * 2.0 - gl_Position.w;", ShaderSources.Load(ShaderSources.OpaqueVertex), StringComparison.Ordinal);
    }

    [Fact]
    public void TheGlPassIsOffByDefaultAndTheFlagTurnsItOn()
    {
        Assert.True(LaunchOptions.TryParse([], out var plain, out var error), error);
        Assert.False(plain.GlShaders);
        Assert.True(LaunchOptions.TryParse(["--gl-shaders"], out var on, out error), error);
        Assert.True(on.GlShaders);
        Assert.Contains("--gl-shaders", LaunchOptions.Usage, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyTheLaunchOptionMakesAGlPassSoTheBuiltInPathIsTheDefault()
    {
        // The pass is made in one place, behind the option; no other file under src/ makes or sets one, and the renderer draws through the effect whenever its pass is null.
        var makers = Directory.EnumerateFiles(Repo.Path("src", "Vocaterra.Game"), "*.cs", SearchOption.AllDirectories)
            .Where(static file => !file.Contains($"{Path.DirectorySeparatorChar}Gl{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(static file => File.ReadAllText(file).Contains("GlOpaquePass.TryCreate", StringComparison.Ordinal))
            .Select(static file => Path.GetFileName(file))
            .ToList();
        Assert.Equal(["SceneDrawer.cs"], makers);
        var drawer = File.ReadAllText(Repo.Path("src", "Vocaterra.Game", "Graphics", "SceneDrawer.cs"));
        var guard = drawer.IndexOf("if (_options.GlShaders)", StringComparison.Ordinal);
        Assert.True(guard >= 0);
        Assert.True(drawer.IndexOf("GlOpaquePass.TryCreate", StringComparison.Ordinal) > guard);
        Assert.Equal(1, drawer.Split("SetGlPass(").Length - 1);
        var renderer = File.ReadAllText(Repo.Path("src", "Vocaterra.Game", "Graphics", "World", "VoxelRenderer.cs"));
        Assert.Contains("var useGl = _glOpaque is not null && _glOpaque.CanDrawNow();", renderer, StringComparison.Ordinal);
        Assert.Contains("(opaque, opaqueQuads) = Upload(result.Opaque);", renderer, StringComparison.Ordinal); // the effect's upload (B21: of the vertices a worker made), untouched
    }

    [Fact]
    public void ThePassDrawsToTheBackBufferOnlyAndTheEffectsDrawWhenItCannot()
    {
        Assert.True(GlOpaquePass.CanDraw(0));
        Assert.False(GlOpaquePass.CanDraw(1)); // under water the scene is drawn to a render target: the built-in path draws it
    }

    [Fact]
    public void TheEmbeddedAlphaReferenceIsTheOpaqueEffectsOne()
    {
        // AlphaTestEffect: CompareFunction.Greater, reference 110 of 255 (VoxelRenderer); the program's constant is the same number.
        var pass = typeof(GlOpaquePass).GetField("AlphaReference", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(pass);
        Assert.Equal(110f / 255f, (float)pass.GetRawConstantValue()!);
    }

    /// <summary>Compiles and links the program on a real context: run with <c>VOCATERRA_GL_TESTS=1</c> (it opens a window for a frame).</summary>
    [GlFact]
    public void TheOpaqueProgramCompilesAndLinksOnARealContext()
    {
        using var game = new GlProbeGame();
        game.RunOneFrame();
        Assert.True(GlLoader.TryLoad(out var gl, out var error), error);
        var info = GlInfo.Query(gl!);
        Assert.True(info.CanRunOpaquePass, string.Join('\n', info.LogLines()));
        using var program = GlProgram.TryCreate(gl!, "opaque", ShaderSources.Load(ShaderSources.OpaqueVertex), ShaderSources.Load(ShaderSources.OpaqueFragment), out error);
        Assert.True(program is not null, error);
        foreach (var uniform in new[] { "uWorldViewProjection", "uFogVector", "uDiffuse", "uFogColour", "uAlphaReference", "uAtlas" })
        {
            Assert.True(program.Uniform(uniform) >= 0, uniform);
        }
    }

    [GlFact]
    public void ABrokenShaderIsReportedWithTheDriversLog()
    {
        using var game = new GlProbeGame();
        game.RunOneFrame();
        Assert.True(GlLoader.TryLoad(out var gl, out var error), error);
        var program = GlProgram.TryCreate(gl!, "broken", "#version 330 core\nvoid main() { gl_Position = nonsense; }\n", ShaderSources.Load(ShaderSources.OpaqueFragment), out error);
        Assert.Null(program);
        Assert.Contains("broken: the vertex shader did not compile", error, StringComparison.Ordinal);
        Assert.Contains("nonsense", error, StringComparison.Ordinal);
    }
}
