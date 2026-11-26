using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Silk.NET.OpenGL;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Graphics.Gl;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Startup;

namespace Vocaterra.Game.Tests.Graphics.Gl;

/// <summary>The game's own GLSL passes (B17a, B17b): the sources are embedded and agree with each other, the default and the fallback are what they are, and the built-in path is the one the effects draw when the pass is not there.</summary>
public sealed class GlShaderTests
{
    [Fact]
    public void TheShaderSourcesAreEmbeddedInTheAssembly()
    {
        Assert.Contains(ShaderSources.ChunkVertex, ShaderSources.Names);
        Assert.Contains(ShaderSources.ChunkFragment, ShaderSources.Names);
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
        var vertex = ShaderSources.Load(ShaderSources.ChunkVertex);
        var fragment = ShaderSources.Load(ShaderSources.ChunkFragment);
        foreach (var varying in new[] { "vec4 vColour;", "vec2 vUvGrad;", "float vFog;", "vec3 vWorld;" })
        {
            Assert.Contains("out " + varying, vertex, StringComparison.Ordinal);
            Assert.Contains("in " + varying, fragment, StringComparison.Ordinal);
        }

        // The atlas coordinate is interpolated at the centroid, so that a multisampled pixel on a face's edge never samples the neighbouring cell of the atlas (the dotted seams along the rocks' edges).
        Assert.Contains("centroid out vec2 vUv;", vertex, StringComparison.Ordinal);
        Assert.Contains("centroid in vec2 vUv;", fragment, StringComparison.Ordinal);
        Assert.Contains("textureGrad(uAtlas, vUv, dFdx(vUvGrad), dFdy(vUvGrad))", fragment, StringComparison.Ordinal);

        // The vertex layout is MeshVertex's: a position, a colour and a coordinate at locations 0, 1 and 2.
        Assert.Contains("layout(location = 0) in vec3 aPosition", vertex, StringComparison.Ordinal);
        Assert.Contains("layout(location = 1) in vec4 aColour", vertex, StringComparison.Ordinal);
        Assert.Contains("layout(location = 2) in vec2 aUv", vertex, StringComparison.Ordinal);

        // The uniforms the pass sets are declared (a name the program lacks would be set to nothing, silently).
        foreach (var uniform in new[] { "mat4 uWorldViewProjection;", "vec4 uFogVector;", "vec3 uDiffuse;", "float uFlipY;" })
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
        Assert.Contains("gl_Position.z = gl_Position.z * 2.0 - gl_Position.w;", ShaderSources.Load(ShaderSources.ChunkVertex), StringComparison.Ordinal);
    }

    [Fact]
    public void ThePositionIsFourDotProductsSoEveryPassMeetsTheOthersByDepthEqualityToTheLastBit()
    {
        // B17a's finding: `matrix * position` differed from MonoGame's translated shaders in the last bit and the glow came out in triangles. The one program does the same four dots for every pass.
        var vertex = ShaderSources.Load(ShaderSources.ChunkVertex);
        Assert.Contains("dot(position, uWorldViewProjection[0]), dot(position, uWorldViewProjection[1]), dot(position, uWorldViewProjection[2]), dot(position, uWorldViewProjection[3])", vertex, StringComparison.Ordinal);
        Assert.DoesNotContain("uWorldViewProjection * position", vertex, StringComparison.Ordinal);
    }

    [Fact]
    public void TheWaterHasNoAlphaTestAndTheOthersHaveTheEffectsReference()
    {
        // AlphaTestEffect: CompareFunction.Greater, reference 110 of 255 (VoxelRenderer); the program's constant is the same number. The water was BasicEffect's: a reference below 0 is no test.
        var reference = typeof(GlChunkPass).GetField("AlphaReference", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(reference);
        Assert.Equal(110f / 255f, (float)reference.GetRawConstantValue()!);
        var source = File.ReadAllText(Repo.Path("src", "Vocaterra.Game", "Graphics", "Gl", "GlChunkPass.cs"));
        Assert.Contains("_gl.Uniform1(_uAlphaReference, -1f);", source, StringComparison.Ordinal);
        Assert.Contains("colour.a <= uAlphaReference + 0.5 / 255.0", ShaderSources.Load(ShaderSources.ChunkFragment), StringComparison.Ordinal);
    }

    [Fact]
    public void TheShadersAreOnByDefaultAndTheOptionSaysOnOrOff()
    {
        Assert.True(LaunchOptions.TryParse([], out var plain, out var error), error);
        Assert.Null(plain.GlShaders); // the saved setting decides, and its default is on
        Assert.True(LaunchOptions.TryParse(["--gl-shaders"], out var bare, out error), error);
        Assert.True(bare.GlShaders);
        Assert.True(LaunchOptions.TryParse(["--gl-shaders", "on"], out var on, out error), error);
        Assert.True(on.GlShaders);
        Assert.True(LaunchOptions.TryParse(["--gl-shaders", "off"], out var off, out error), error);
        Assert.False(off.GlShaders);
        Assert.True(LaunchOptions.TryParse(["--gl-shaders", "off", "--quality", "low"], out var more, out error), error);
        Assert.False(more.GlShaders);
        Assert.Equal(Vocaterra.Game.Core.Menu.QualityLevel.Low, more.Quality);
        Assert.Contains("--gl-shaders [on|off]", LaunchOptions.Usage, StringComparison.Ordinal);
    }

    [Fact]
    public void TheEffectsDrawWhenThePassIsNotThereAndOnlyTheSceneMakesAPass()
    {
        // The pass is made in one place (the scene drawer, from the Shaders setting); no other file under src/ makes or sets one, and the renderer draws through the effects whenever its pass is null.
        var makers = Directory.EnumerateFiles(Repo.Path("src", "Vocaterra.Game"), "*.cs", SearchOption.AllDirectories)
            .Where(static file => !file.Contains($"{Path.DirectorySeparatorChar}Gl{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(static file => File.ReadAllText(file).Contains("GlChunkPass.TryCreate", StringComparison.Ordinal))
            .Select(static file => Path.GetFileName(file))
            .ToList();
        Assert.Equal(["SceneDrawer.cs"], makers);
        var drawer = File.ReadAllText(Repo.Path("src", "Vocaterra.Game", "Graphics", "SceneDrawer.cs"));
        Assert.Equal(2, drawer.Split("SetGlPass(").Length - 1); // on (the pass) and off (null)
        Assert.Contains("gl: shaders not used, the effects draw as always: {glError}", drawer, StringComparison.Ordinal); // the one clear log line of a context or a shader that fails
        var renderer = File.ReadAllText(Repo.Path("src", "Vocaterra.Game", "Graphics", "World", "VoxelRenderer.cs"));
        Assert.Contains("_glPass is not null ? DrawChunksGl(camera, tick, highlight, avatar, plotOutlines) : DrawChunksEffects(camera, tick, highlight, avatar, plotOutlines);", renderer, StringComparison.Ordinal);
        Assert.Contains("var (opaque, opaqueQuads) = Upload(result.Opaque);", renderer, StringComparison.Ordinal); // the effects' upload (B21: of the vertices a worker made), untouched
        Assert.Contains("_solid.CurrentTechnique.Passes[0].Apply();", renderer, StringComparison.Ordinal);
    }

    [Fact]
    public void TheContextBelowThreeThreeFallsBackToTheEffects()
    {
        var old = new GlInfo("2.1 Mesa", "r", "v", "1.20", 2, 1, false, false, false, false, false, false, false, false, false, false, 4096, 256);
        var current = old with { Major = 3, Minor = 3 };
        var newer = old with { Major = 4, Minor = 6 };
        Assert.False(old.CanRunChunkPasses);
        Assert.True(current.CanRunChunkPasses);
        Assert.True(newer.CanRunChunkPasses);
        Assert.False((old with { Major = 3, Minor = 2 }).CanRunChunkPasses);
    }

    [Fact]
    public void ARenderTargetFlipsTheClipYAndTheFrontFaceAndTheBackBufferDoesNot()
    {
        Assert.Equal((1f, false), GlChunkPass.OrientationFor(renderTarget: false));
        Assert.Equal((-1f, true), GlChunkPass.OrientationFor(renderTarget: true)); // under water the scene is drawn to a render target: MonoGame's pictures and windings are upside down there
        var source = File.ReadAllText(Repo.Path("src", "Vocaterra.Game", "Graphics", "World", "VoxelRenderer.cs"));
        Assert.Contains("var renderTarget = _device.RenderTargetCount > 0;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CanDrawNow", source, StringComparison.Ordinal); // no fallback left over for a render target
    }

    [Fact]
    public void TheFrameHasOneStateScopeAndNoMonoGameCallStandsInsideIt()
    {
        var source = File.ReadAllText(Repo.Path("src", "Vocaterra.Game", "Graphics", "World", "VoxelRenderer.cs"));
        var start = source.IndexOf("private double DrawChunksGl(", StringComparison.Ordinal);
        Assert.True(start > 0, "the GL path's method");
        var body = source[start..source.IndexOf("private void DrawGlow()", start, StringComparison.Ordinal)];
        Assert.Equal(1, body.Split("pass.Begin(").Length - 1);
        Assert.Equal(1, body.Split("pass.End()").Length - 1);
        var begin = body.IndexOf("pass.Begin(", StringComparison.Ordinal);
        var scope = body[body.IndexOf('\n', begin)..body.IndexOf("pass.End()", StringComparison.Ordinal)]; // after the call that opens it (its arguments are read before the state is touched)
        foreach (var forbidden in new[] { "_device.", "_solid.", "_effect.", "DrawChunk(", "DrawDynamic(", "DrawFiguresAndOverlays(", "Passes[0].Apply" })
        {
            Assert.DoesNotContain(forbidden, scope, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EveryChunkPassHasABufferInTheGlPath()
    {
        var source = File.ReadAllText(Repo.Path("src", "Vocaterra.Game", "Graphics", "World", "VoxelRenderer.cs"));
        foreach (var part in new[] { "GlOpaque", "GlAlpha", "GlPlants", "GlLit", "GlGrass" })
        {
            Assert.Contains($"public GlChunkBuffer? {part};", source, StringComparison.Ordinal);
            Assert.Contains($"{part}?.Dispose();", source, StringComparison.Ordinal);
        }

        foreach (var kind in Enum.GetValues<GlPassKind>())
        {
            Assert.Contains($"GlPassKind.{kind}", source + File.ReadAllText(Repo.Path("src", "Vocaterra.Game", "Graphics", "Gl", "GlChunkPass.cs")), StringComparison.Ordinal);
        }
    }

    /// <summary>Compiles and links the program on a real context: run with <c>VOCATERRA_GL_TESTS=1</c> (it opens a window for a frame).</summary>
    [GlFact]
    public void TheChunkProgramCompilesAndLinksOnARealContext()
    {
        _ = GlProbeGame.Shared;
        Assert.True(GlLoader.TryLoad(out var gl, out var error), error);
        var info = GlInfo.Query(gl!);
        Assert.True(info.CanRunChunkPasses, string.Join('\n', info.LogLines()));
        using var program = GlProgram.TryCreate(gl!, "chunk", ShaderSources.Load(ShaderSources.ChunkVertex), ShaderSources.Load(ShaderSources.ChunkFragment), out error);
        Assert.True(program is not null, error);
        foreach (var uniform in new[] { "uWorldViewProjection", "uFogVector", "uDiffuse", "uFogColour", "uAlphaReference", "uAtlas", "uFlipY" })
        {
            Assert.True(program.Uniform(uniform) >= 0, uniform);
        }
    }

    [GlFact]
    public void EveryShaderSourceTheAssemblyEmbedsCompilesOnARealContext()
    {
        _ = GlProbeGame.Shared;
        Assert.True(GlLoader.TryLoad(out var gl, out var error), error);
        var vertexSources = ShaderSources.Names.Where(static name => name.EndsWith(".vert.glsl", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(vertexSources);
        foreach (var vertexName in vertexSources)
        {
            var fragmentName = vertexName.Replace(".vert.", ".frag.", StringComparison.Ordinal);
            Assert.Contains(fragmentName, ShaderSources.Names);
            using var program = GlProgram.TryCreate(gl!, vertexName, ShaderSources.Load(vertexName), ShaderSources.Load(fragmentName), out error);
            Assert.True(program is not null, error);
        }
    }

    [GlFact]
    public void ABrokenShaderIsReportedWithTheDriversLog()
    {
        _ = GlProbeGame.Shared;
        Assert.True(GlLoader.TryLoad(out var gl, out var error), error);
        var program = GlProgram.TryCreate(gl!, "broken", "#version 330 core\nvoid main() { gl_Position = nonsense; }\n", ShaderSources.Load(ShaderSources.ChunkFragment), out error);
        Assert.Null(program);
        Assert.Contains("broken: the vertex shader did not compile", error, StringComparison.Ordinal);
        Assert.Contains("nonsense", error, StringComparison.Ordinal);
    }

    [GlFact]
    public void TheStateScopeRestoresEverythingItCapturedAfterEveryKindOfDraw()
    {
        var game = GlProbeGame.Shared;
        var device = game.GraphicsDevice;
        using var atlas = White(device, 8, 8, mips: true);
        using var water = White(device, 64, 64, mips: true);
        using var pass = GlChunkPass.TryCreate(device, atlas, [water], 16, static _ => { }, out var error);
        Assert.True(pass is not null, error);
        var gl = pass.Gl;

        // The shared context is put back as it was when the test is over (MonoGame's cache of it must stay true for the tests that draw next).
        var guard = new GlStateSaver(gl, GlChunkPass.TextureUnit, pass.Info.FixedRestartIndex);
        guard.Capture();
        try
        {
            ScopeRestoresWhatItCaptured(pass, gl);
        }
        finally
        {
            guard.Restore();
        }
    }

    private static void ScopeRestoresWhatItCaptured(GlChunkPass pass, GL gl)
    {
        // A context in an unusual state: every value the scope saves is something the passes set to another.
        gl.UseProgram(0);
        gl.ActiveTexture(GLEnum.Texture3);
        gl.Enable(EnableCap.Blend);
        gl.BlendFuncSeparate(BlendingFactor.DstColor, BlendingFactor.ConstantAlpha, BlendingFactor.SrcAlphaSaturate, BlendingFactor.OneMinusDstAlpha);
        gl.BlendEquationSeparate(BlendEquationModeEXT.FuncSubtract, BlendEquationModeEXT.FuncReverseSubtract);
        gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        gl.Enable(EnableCap.PrimitiveRestart);
        gl.Disable(EnableCap.DepthTest);
        gl.DepthFunc(DepthFunction.Greater);
        gl.DepthMask(false);
        gl.Enable(EnableCap.CullFace);
        gl.CullFace(TriangleFace.Front);
        gl.FrontFace(FrontFaceDirection.CW);
        gl.ColorMask(true, false, true, false);
        gl.Enable(EnableCap.ScissorTest);
        gl.Enable(EnableCap.StencilTest);
        gl.Viewport(3, 5, 17, 19);
        gl.DepthRange(0.25, 0.75);
        _ = gl.GetError();
        var before = Snapshot(gl);

        var vertices = Quad(0, 0, 1, 1, -3);
        using var buffer = pass.Upload(vertices, 1);
        Assert.NotNull(buffer);
        pass.Begin(Matrix.Identity, Matrix.CreatePerspectiveFieldOfView(1f, 1f, 0.1f, 100f), Vector3.One, 1000f, 2000f, new Viewport(0, 0, 64, 64), 64, renderTarget: false);
        Assert.True(pass.InScope);
        foreach (var kind in Enum.GetValues<GlPassKind>())
        {
            pass.Use(kind);
            if (kind == GlPassKind.Water)
            {
                pass.SetWaterFrame(0);
            }

            pass.SetDiffuse(Vector3.One);
            pass.Draw(buffer);
        }

        pass.End();
        Assert.False(pass.InScope);
        Assert.Equal(before, Snapshot(gl));
        Assert.Equal(0, pass.Errors.Count);
    }

    [GlFact]
    public void ASceneDrawnToARenderTargetOrTheBackBufferThroughThePassEqualsTheEffectsPicture()
    {
        var game = GlProbeGame.Shared;
        var device = game.GraphicsDevice;
        using var atlas = White(device, 8, 8, mips: true);
        using var water = White(device, 64, 64, mips: true);
        using var pass = GlChunkPass.TryCreate(device, atlas, [water], 16, static _ => { }, out var error);
        Assert.True(pass is not null, error);
        var projection = Matrix.CreatePerspectiveFieldOfView(1.2f, 1f, 0.1f, 100f);
        var view = Matrix.Identity;
        var quad = Quad(-0.5f, 0.2f, 1.5f, 1.2f, -3f); // up and to the right of the middle: a flip shows
        using var buffer = pass.Upload(quad, 1);
        Assert.NotNull(buffer);
        foreach (var target in new[] { true, false })
        {
            using var rt = new RenderTarget2D(device, 64, 64, false, SurfaceFormat.Color, DepthFormat.Depth24);
            var viewport = default(Viewport);
            Color[] Draw(bool throughPass)
            {
                if (target)
                {
                    device.SetRenderTarget(rt);
                }

                device.Clear(Color.Black);
                device.DepthStencilState = DepthStencilState.Default;
                device.RasterizerState = RasterizerState.CullClockwise;
                device.BlendState = BlendState.Opaque;
                device.SamplerStates[0] = SamplerState.PointClamp;
                if (throughPass)
                {
                    pass.Begin(view, projection, Vector3.One, 1000f, 2000f, device.Viewport, device.PresentationParameters.BackBufferHeight, target);
                    pass.SetDiffuse(Vector3.One);
                    pass.Draw(buffer);
                    pass.End();
                }
                else
                {
                    using var effect = new BasicEffect(device) { View = view, Projection = projection, VertexColorEnabled = true, TextureEnabled = true, Texture = atlas, LightingEnabled = false };
                    effect.CurrentTechnique.Passes[0].Apply();
                    var v = new VertexPositionColorTexture[6];
                    for (var i = 0; i < 6; i++)
                    {
                        var source = quad[new[] { 0, 1, 2, 0, 2, 3 }[i]];
                        v[i] = new VertexPositionColorTexture(new Vector3(source.X, source.Y, source.Z), new Color(source.Color), new Vector2(source.U, source.V));
                    }

                    device.DrawUserPrimitives(Microsoft.Xna.Framework.Graphics.PrimitiveType.TriangleList, v, 0, 2);
                }

                var pixels = new Color[64 * 64];
                if (target)
                {
                    device.SetRenderTarget(null);
                    rt.GetData(pixels);
                }
                else
                {
                    device.GetBackBufferData(pixels);
                }

                viewport = device.Viewport;
                return pixels;
            }

            var effects = Draw(throughPass: false);
            var glsl = Draw(throughPass: true);
            Assert.Contains(effects, static pixel => pixel != Color.Black); // the quad is on the picture
            Assert.Contains(effects, static pixel => pixel == Color.Black); // and not all of it
            var differing = effects.Zip(glsl).Count(static pair => pair.First != pair.Second);
            Assert.True(differing == 0, $"{(target ? "render target" : "back buffer")}: {differing} of {effects.Length} pixels differ from the effects' picture (viewport {viewport})");
            Assert.Equal(0, pass.Errors.Count);
        }
    }

    /// <summary>B17c: the light of a corner in the shader is the C# model's: a quad whose four corners carry one light word, drawn through the pass under a dusk sun and a glow, has the colour <see cref="LightModel"/> gives that word, to a byte or two.</summary>
    [GlFact]
    public void TheLightOfACornerInTheShaderIsTheCSharpModelsToTheByte()
    {
        var game = GlProbeGame.Shared;
        var device = game.GraphicsDevice;
        using var atlas = White(device, 8, 8, mips: true);
        using var water = White(device, 64, 64, mips: true);
        using var pass = GlChunkPass.TryCreate(device, atlas, [water], 16, static _ => { }, out var error);
        Assert.True(pass is not null, error);
        var direction = Vector3.Normalize(new Vector3(-1f, 0.2f, -0.1f));
        var sun = new SunLightState((direction.X, direction.Y, direction.Z), (0.88f, 0.56f, 0.46f));
        const float glowStrength = 0.5f;
        var frame = new GlLightFrame(Vector3.Zero, direction, new Vector3(sun.Colour.R, sun.Colour.G, sun.Colour.B), new Vector3(ChunkMesher.SunDirection.X, ChunkMesher.SunDirection.Y, ChunkMesher.SunDirection.Z), new Vector3(LanternGlow.Amber.R * glowStrength, LanternGlow.Amber.G * glowStrength, LanternGlow.Amber.B * glowStrength));
        var material = MeshVertex.Pack(230, 200, 170);
        var words = new[]
        {
            LightData.Open(LightFace.Up),
            LightData.Open(LightFace.West),
            LightData.Open(LightFace.East),
            LightData.Open(LightFace.Down),
            LightData.Pack(180, 0, 1, LightFace.North),
            LightData.Pack(255, LightData.FromLevel(9), 3, LightFace.Up),
            LightData.Pack(LightData.FromLevel(4), LightData.FromLevel(15), 0, LightFace.South),
            LightData.Open(LightFace.Flat, 140),
            LightData.Open(LightFace.Fixed),
        };
        var projection = Matrix.CreatePerspectiveFieldOfView(1.2f, 1f, 0.1f, 100f);
        foreach (var word in words)
        {
            var quad = Quad(-1f, -1f, 1f, 1f, -3f).Select(v => v with { Color = material }).ToArray();
            using var buffer = pass.Upload(quad, [word, word, word, word], 1);
            Assert.NotNull(buffer);
            device.Clear(Color.Black);
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullClockwise;
            device.BlendState = BlendState.Opaque;
            pass.Begin(Matrix.Identity, projection, Vector3.One, 1000f, 2000f, device.Viewport, device.PresentationParameters.BackBufferHeight, renderTarget: false, frame);
            pass.Draw(buffer);
            pass.End();
            var pixels = new Color[device.PresentationParameters.BackBufferWidth * device.PresentationParameters.BackBufferHeight];
            device.GetBackBufferData(pixels);
            var centre = pixels[(pixels.Length / 2) + (device.PresentationParameters.BackBufferWidth / 2)];
            var (r, g, b) = LightModel.Corner(material, word, sun, glowStrength);
            static int Byte(float v) => (int)Math.Clamp(MathF.Round(v * 255f), 0f, 255f);
            Assert.True(Math.Abs(centre.R - Byte(r)) <= 2 && Math.Abs(centre.G - Byte(g)) <= 2 && Math.Abs(centre.B - Byte(b)) <= 2, $"word {word:X8}: the shader drew ({centre.R}, {centre.G}, {centre.B}), the model says ({Byte(r)}, {Byte(g)}, {Byte(b)})");
        }

        Assert.Equal(0, pass.Errors.Count);
    }

    private static Texture2D White(GraphicsDevice device, int width, int height, bool mips)
    {
        var texture = new Texture2D(device, width, height, mips, SurfaceFormat.Color);
        for (var level = 0; level < texture.LevelCount; level++)
        {
            var w = Math.Max(1, width >> level);
            var h = Math.Max(1, height >> level);
            var pixels = new Color[w * h];
            Array.Fill(pixels, Color.White);
            texture.SetData(level, null, pixels, 0, pixels.Length);
        }

        return texture;
    }

    // A quad facing the camera, counter-clockwise: bottom left, bottom right, top right, top left (as the mesher writes them).
    private static MeshVertex[] Quad(float x0, float y0, float x1, float y1, float z)
    {
        var white = MeshVertex.Pack(255, 255, 255);
        return
        [
            new MeshVertex(x0, y0, z, white, 0.25f, 0.75f),
            new MeshVertex(x1, y0, z, white, 0.75f, 0.75f),
            new MeshVertex(x1, y1, z, white, 0.75f, 0.25f),
            new MeshVertex(x0, y1, z, white, 0.25f, 0.25f),
        ];
    }

    // Everything the scope saves, read back (the unit's texture and sampler on the pass's unit, the bindings, the enables, the functions, the masks, the viewport and the depth range).
    private static string Snapshot(GL gl)
    {
        var active = gl.GetInteger(GLEnum.ActiveTexture);
        gl.ActiveTexture((GLEnum)((int)GLEnum.Texture0 + GlChunkPass.TextureUnit));
        var onUnit = $"{gl.GetInteger(GLEnum.TextureBinding2D)}/{gl.GetInteger(GLEnum.SamplerBinding)}";
        gl.ActiveTexture((GLEnum)active);
        Span<int> mask = stackalloc int[4];
        gl.GetInteger(GLEnum.ColorWritemask, mask);
        var viewport = new int[4];
        gl.GetInteger(GLEnum.Viewport, viewport);
        var range = new double[2];
        gl.GetDouble(GLEnum.DepthRange, range);
        var enables = string.Join(',', new[] { EnableCap.DepthTest, EnableCap.CullFace, EnableCap.Blend, EnableCap.StencilTest, EnableCap.ScissorTest, EnableCap.PolygonOffsetFill, EnableCap.PrimitiveRestart }.Select(c => gl.IsEnabled(c) ? "1" : "0"));
        return string.Join(
            '|',
            gl.GetInteger(GLEnum.CurrentProgram),
            gl.GetInteger(GLEnum.VertexArrayBinding),
            gl.GetInteger(GLEnum.ArrayBufferBinding),
            active,
            onUnit,
            enables,
            gl.GetInteger(GLEnum.DepthFunc),
            gl.GetBoolean(GLEnum.DepthWritemask),
            gl.GetInteger(GLEnum.CullFaceMode),
            gl.GetInteger(GLEnum.FrontFace),
            string.Join(',', mask.ToArray()),
            string.Join(',', viewport),
            string.Join(',', range),
            gl.GetInteger(GLEnum.BlendSrcRgb),
            gl.GetInteger(GLEnum.BlendDstRgb),
            gl.GetInteger(GLEnum.BlendSrcAlpha),
            gl.GetInteger(GLEnum.BlendDstAlpha),
            gl.GetInteger(GLEnum.BlendEquationRgb),
            gl.GetInteger(GLEnum.BlendEquationAlpha),
            gl.GetInteger(GLEnum.UnpackAlignment));
    }
}
