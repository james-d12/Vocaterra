using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>B17c: a mesh that carries its light as data is a pure function of its blocks, whatever thread builds it and whatever the hour; it is never built again for a change of light; and the baked mesh is still what the effects' fallback draws.</summary>
public sealed class DataMeshTests
{
    private static MapBlocks Scene() => new MapBlocks().Fill(0, 4, 0, 15, 5, 15, BlockId.Grass)
        .Set(8, 6, 8, BlockId.Fence).Set(3, 6, 3, BlockId.Bush).Set(5, 6, 5, BlockId.TallGrass).Set(10, 6, 4, BlockId.Water)
        .Set(6, 6, 6, BlockId.Lantern).Set(9, 6, 9, BlockId.PostTorch).Set(12, 6, 12, BlockId.Bed).Set(12, 6, 13, BlockId.BedFoot).Set(2, 6, 12, BlockId.Door);

    private static readonly BlockPos[] Lights = [new BlockPos(6, 6, 6), new BlockPos(9, 6, 9)];

    private static MeshOptions Data(LightBand band = LightBand.Day) => new(true, 100, Lights, band, new SkySurface(0, 0, static (_, _) => 6), Data: true);

    private static IEnumerable<(string Name, MeshBuffer Mesh)> Parts(ChunkMeshes meshes) =>
        [("opaque", meshes.Opaque), ("alpha", meshes.Alpha), ("plants", meshes.PlantMesh), ("lit", meshes.LitMesh), ("glow", meshes.GlowMesh)];

    private static string Fingerprint(ChunkMeshes meshes) =>
        string.Join('|', Parts(meshes).Select(static p => $"{p.Name}:{p.Mesh.VertexCount}:{string.Join(',', p.Mesh.Vertices.ToArray().Select(static v => $"{v.X},{v.Y},{v.Z},{v.Color},{v.U},{v.V}"))}:{string.Join(',', p.Mesh.Lights.ToArray())}"));

    [Fact]
    public void Given_TheDefaultOptions_When_AMeshIsBuilt_Then_ItIsBakedWithNoLightWords()
    {
        var meshes = ChunkMesher.Build(Scene(), 0, 0, 0);
        Assert.False(new MeshOptions().Data);
        Assert.All(Parts(meshes), static p => Assert.False(p.Mesh.HasLights, p.Name));
    }

    [Fact]
    public void Given_ADataMesh_When_ItIsRead_Then_EveryVertexHasAWordAndTheGlowIsInTheFacesNotInAMesh()
    {
        var meshes = ChunkMesher.Build(Scene(), 0, 0, 0, 0, Data());
        foreach (var (name, mesh) in Parts(meshes).Where(static p => p.Mesh.VertexCount > 0))
        {
            Assert.True(mesh.HasLights, name);
            Assert.Equal(mesh.VertexCount, mesh.Lights.Length);
            Assert.DoesNotContain(0u, mesh.Lights.ToArray()); // no quad was added without its words (a corner's word is never zero: the sky share and the face give it bits)
        }

        Assert.Equal(0, meshes.GlowMesh.VertexCount);
        Assert.All(meshes.LitMesh.Lights.ToArray(), static w => Assert.Equal(LightFace.Fixed, LightData.Face(w))); // a lantern's glass and a torch's flame are finished colours
        Assert.All(meshes.PlantMesh.Lights.ToArray(), static w => Assert.Equal(LightFace.Flat, LightData.Face(w)));
        Assert.All(meshes.Opaque.Lights.ToArray(), static w => Assert.InRange(LightData.Occlusion(w), 0, 3));
    }

    [Fact]
    public void Given_ADataMesh_When_ItIsBuiltInEveryBand_Then_ItIsTheSameMeshAndNothingOfTheHourIsInIt()
    {
        var day = Fingerprint(ChunkMesher.Build(Scene(), 0, 0, 0, 0, Data(LightBand.Day)));
        foreach (var band in Enum.GetValues<LightBand>())
        {
            Assert.Equal(day, Fingerprint(ChunkMesher.Build(Scene(), 0, 0, 0, 0, Data(band))));
        }
    }

    [Fact]
    public async Task Given_ASnapshot_When_ADataMeshIsBuiltOnAWorkerAndInline_Then_TheBytesAreTheSame()
    {
        var inline = Fingerprint(ChunkMesher.Build(Scene(), 0, 0, 0, 0, Data()));
        var fromWorkers = Enumerable.Range(0, 4).Select(_ => Task.Run(() => Fingerprint(ChunkMesher.Build(Scene(), 0, 0, 0, 0, Data())))).ToArray();
        var built = await Task.WhenAll(fromWorkers);
        Assert.All(built, fingerprint => Assert.Equal(inline, fingerprint));
        Assert.Equal(inline, Fingerprint(ChunkMesher.Build(Scene(), 0, 0, 0, 0, Data()))); // and again: a pure function of its blocks
    }

    [Fact]
    public void Given_ADataMesh_When_TheMaterialsAreRead_Then_TheyCarryTheRocksTintAndNoLight()
    {
        var stone = new MapBlocks().Fill(0, 40, 0, 15, 41, 15, BlockId.Stone);
        var data = ChunkMesher.Build(stone, 0, 2, 0, 0, new MeshOptions(true, 100, null, LightBand.Night, Data: true)).Opaque;
        var tint = ChunkMesher.RockTint(BlockId.Stone, 41);
        var top = data.Vertices.ToArray().Where(static v => v.Y == 42f).ToArray();
        // The night's dim light is not in the colour: it is the rock's tint, whatever the band.
        Assert.NotEmpty(top);
        Assert.All(top, v => Assert.InRange((int)(v.Color & 0xFF), (int)MathF.Round(255f * tint.R) - 1, (int)MathF.Round(255f * tint.R) + 1));
    }

    [Fact]
    public void Given_TheLightBandsAHourMoves_When_TheMeshBandIsChosen_Then_ADataMeshAndASunThatDoesNotFollowTheHourStayInTheDay()
    {
        foreach (var look in Enum.GetValues<LightBand>())
        {
            Assert.Equal(LightBand.Day, LightBands.MeshBand(look, lightAsData: true, sunFollowsHour: true));
            Assert.Equal(LightBand.Day, LightBands.MeshBand(look, lightAsData: true, sunFollowsHour: false));
            Assert.Equal(LightBand.Day, LightBands.MeshBand(look, lightAsData: false, sunFollowsHour: false));
            Assert.Equal(look, LightBands.MeshBand(look, lightAsData: false, sunFollowsHour: true)); // the effects bake the band's sun, as before
        }
    }

    [Fact]
    public void Given_ASettledViewOfDataMeshes_When_TheHourMovesThroughTheWholeDay_Then_NoChunkIsBuiltAgainAndWhatWorkersMadeIsWhatTheMesherMakes()
    {
        using var pool = new JobPool(2);
        using var rig = MeshingRig.Valley(pool, 64, data: true);
        _ = rig.Settle();
        var jobs = rig.Pipeline.Submitted;
        Assert.True(jobs > 20);
        Assert.Null(rig.FirstMismatch()); // the meshes the workers made are the mesher's own, light words included
        Assert.Contains(rig.Meshes.Values, static m => m.Opaque.Lights is not null);
        foreach (var look in new[] { LightBand.Dusk, LightBand.Night, LightBand.Dawn, LightBand.Day, LightBand.Night })
        {
            var band = LightBands.MeshBand(look, lightAsData: true, sunFollowsHour: true);
            if (band != rig.Pipeline.Band)
            {
                rig.Band = band;
                rig.Pipeline.Band = band;
                rig.Pipeline.BandChanged();
            }

            _ = rig.Settle();
        }

        Assert.Equal(jobs, rig.Pipeline.Submitted); // a change of hour is a few uniforms, not a mesh
        Assert.Null(rig.FirstMismatch());
    }
}
