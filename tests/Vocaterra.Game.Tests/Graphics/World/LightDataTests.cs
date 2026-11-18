using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.Gl;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>B17c: the light word of a vertex (sky, block, occlusion, face, flags, extra), the vertex layout of the two streams, and the shader's inputs that the pass sets.</summary>
public sealed partial class LightDataTests
{
    [Fact]
    public void Given_EveryFieldAtItsLimits_When_AWordIsPackedAndRead_Then_EachFieldComesBackAlone()
    {
        foreach (var face in Enum.GetValues<LightFace>())
        {
            foreach (var occlusion in new[] { 0, 1, 2, 3 })
            {
                foreach (var (sky, block, extra, flags) in new[] { ((byte)0, (byte)255, (byte)0, 0), ((byte)255, (byte)0, (byte)255, LightData.WaveFlag), ((byte)17, (byte)34, (byte)51, 5) })
                {
                    var word = LightData.Pack(sky, block, occlusion, face, extra, flags);
                    Assert.Equal(sky, LightData.Sky(word));
                    Assert.Equal(block, LightData.Block(word));
                    Assert.Equal(occlusion, LightData.Occlusion(word));
                    Assert.Equal(face, LightData.Face(word));
                    Assert.Equal(extra, LightData.Extra(word));
                    Assert.Equal(flags, LightData.Flags(word));
                }
            }
        }
    }

    [Fact]
    public void Given_TheLightLevelsOfB23_When_TheyAreMadeChannelBytes_Then_FifteenIsTheWholeByteAndEveryLevelIsDistinct()
    {
        Assert.Equal(0, LightData.FromLevel(0));
        Assert.Equal(255, LightData.FromLevel(LightData.MaxLevel));
        Assert.Equal(255, LightData.FromLevel(40));
        Assert.Equal(0, LightData.FromLevel(-3));
        var bytes = Enumerable.Range(0, 16).Select(LightData.FromLevel).ToArray();
        Assert.Equal(16, bytes.Distinct().Count());
        for (var level = 0; level <= LightData.MaxLevel; level++)
        {
            Assert.Equal(level, LightData.LevelOf(LightData.FromLevel(level)), 4);
        }

        Assert.Equal(255, LightData.FromShare(1f));
        Assert.Equal(0, LightData.FromShare(0f));
        Assert.Equal(128, LightData.FromShare(0.5f));
    }

    [Fact]
    public void Given_TheBlockFaces_When_TheirCodesAreRead_Then_TheyAreTheNumbersOfTheFacesAndTheShadersTable()
    {
        foreach (var face in Enum.GetValues<BlockFace>())
        {
            Assert.Equal((int)face, (int)LightData.CodeOf(face));
        }

        // The vertex shader's table of normals is in the order of the face codes (east, west, up, down, south, north).
        var vertex = ShaderSources.Load(ShaderSources.ChunkVertex);
        var table = Regex.Match(vertex, @"kNormals\[6\] = vec3\[6\]\((?<v>[^;]*)\);").Groups["v"].Value;
        var numbers = Regex.Matches(table, @"-?\d+\.\d").Select(static m => float.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(18, numbers.Length);
        foreach (var face in Enum.GetValues<BlockFace>())
        {
            var (nx, ny, nz) = ChunkMesher.NormalOf(face);
            Assert.Equal(new[] { nx, ny, nz }, numbers[((int)face * 3)..(((int)face * 3) + 3)]);
        }
    }

    [Fact]
    public void Given_TheTwoVertexStreams_When_TheirSizesAreRead_Then_ThePositionStreamIsTheEnginesAndTheLightStreamIsFourBytes()
    {
        Assert.Equal(24, MeshVertex.SizeInBytes);
        Assert.Equal(4, LightData.SizeInBytes);
        Assert.Equal(LightData.SizeInBytes, sizeof(uint));
        Assert.Equal(MeshVertex.SizeInBytes + LightData.SizeInBytes, 28);
    }

    [GeneratedRegex(@"layout\(location = (?<l>\d)\) in (?<t>\w+) (?<n>\w+);")]
    private static partial Regex AttributeDeclaration();

    [GeneratedRegex(@"^uniform (?<t>\w+) (?<n>\w+)(\[\d+\])?;", RegexOptions.Multiline)]
    private static partial Regex UniformDeclaration();

    [Fact]
    public void Given_TheVertexShader_When_ItsAttributesAreRead_Then_TheyAreTheLayoutThePassPointsAt()
    {
        var found = AttributeDeclaration().Matches(ShaderSources.Load(ShaderSources.ChunkVertex))
            .Select(static m => (int.Parse(m.Groups["l"].Value, System.Globalization.CultureInfo.InvariantCulture), m.Groups["n"].Value, m.Groups["t"].Value))
            .OrderBy(static a => a.Item1)
            .ToArray();
        var expected = GlChunkPass.Attributes.Select(static a => (a.Location, a.Name, a.Type)).ToArray();
        Assert.Equal(expected, found);
        Assert.Equal(new[] { 3, 4, 2, 4 }, GlChunkPass.Attributes.Select(static a => a.Components).ToArray()); // position 3, colour 4, coordinate 2, light 4 (four bytes)
    }

    [Fact]
    public void Given_TheShaders_When_TheirUniformsAreListed_Then_TheyAreExactlyTheOnesThePassSets()
    {
        var declared = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var source in new[] { ShaderSources.ChunkVertex, ShaderSources.ChunkFragment })
        {
            foreach (Match match in UniformDeclaration().Matches(ShaderSources.Load(source)))
            {
                declared.Add(match.Groups["n"].Value);
            }
        }

        // A name the pass sets that the program lacks would be set to nothing, silently; a name the program declares that the pass never sets would read zero.
        Assert.Equal(GlChunkPass.UniformNames.Order(StringComparer.Ordinal).ToArray(), declared.ToArray());
        var pass = File.ReadAllText(Repo.Path("src", "Vocaterra.Game", "Graphics", "Gl", "GlChunkPass.cs"));
        foreach (var name in GlChunkPass.UniformNames.Where(static n => n != "uAtlas" && n != "uFogColour"))
        {
            Assert.True(pass.Contains($"Uniform(\"{name}\")", StringComparison.Ordinal), $"the pass never looks up {name}");
        }
    }
}
