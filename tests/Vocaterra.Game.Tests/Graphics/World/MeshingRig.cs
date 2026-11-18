using Microsoft.Xna.Framework;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.Water.Currents;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Game.Graphics.World;
using Vocaterra.Game.Streaming;

namespace Vocaterra.Game.Tests.Graphics.World;

/// <summary>
/// The renderer's meshing without a window (B21): a grid, the view set, the tracker, the light index and the pipeline over a pool, with a sink that keeps what would have gone to the card. A test edits the grid, runs frames and
/// compares what the sink holds with what the mesher makes of the grid as it is.
/// </summary>
internal sealed class MeshingRig : IChunkMeshSink, IDisposable
{
    private readonly Dictionary<VoxelChunkCoord, ChunkMeshResult> _meshes = [];
    private IReadOnlyList<VoxelChunkCoord> _wanted = [];

    public MeshingRig(VoxelGrid grid, RiverCurrents? currents, JobPool? pool, Vector3 eye, int viewDistance = 96, LightBand band = LightBand.Day, bool data = false)
    {
        Data = data;
        Grid = grid;
        Currents = currents;
        Band = band;
        Eye = eye;
        Tracker = new ChunkVersionTracker(grid);
        Lights = new ChunkLightIndex(grid);
        View = new ChunkViewSet(grid, viewDistance, 4000) { ResidencyRebuildCalls = 1 };
        Pipeline = new ChunkMeshPipeline(grid, Tracker, OptionsFor, currents, pool) { Band = band };
        grid.AddResidencyListener(Tracker);
    }

    public VoxelGrid Grid { get; }

    /// <summary>Whether the meshes carry their light as data (B17c).</summary>
    public bool Data { get; }

    public RiverCurrents? Currents { get; }

    public LightBand Band { get; set; }

    public Vector3 Eye { get; set; }

    public ChunkVersionTracker Tracker { get; }

    public ChunkLightIndex Lights { get; }

    public ChunkViewSet View { get; }

    public ChunkMeshPipeline Pipeline { get; }

    public IReadOnlyDictionary<VoxelChunkCoord, ChunkMeshResult> Meshes => _meshes;

    public IReadOnlyList<VoxelChunkCoord> Wanted => _wanted;

    public List<VoxelChunkCoord> InstallOrder { get; } = [];

    public Dictionary<VoxelChunkCoord, ChunkMeshResult> Grass { get; } = [];

    public MeshOptions OptionsFor(VoxelChunkCoord coord) => new(true, 100, Lights.Near(coord), Band, Data: Data);

    /// <summary>One frame, as the renderer does it: the view, the lights, the pipeline.</summary>
    public int Frame(double? budgetMilliseconds = null)
    {
        var wanted = View.Update(Eye.X, Eye.Y, Eye.Z);
        if (!ReferenceEquals(wanted, _wanted))
        {
            _wanted = wanted;
            Pipeline.SetWanted(wanted);
        }

        var changed = Lights.TakeChanged();
        for (var i = 0; i < changed.Count; i++)
        {
            foreach (var reached in ChunkLightIndex.ChunksReachedBy(changed[i]))
            {
                Pipeline.Invalidate(reached);
            }
        }

        return Pipeline.Frame(Eye, null, budgetMilliseconds, MonotonicClock.Milliseconds(), this);
    }

    /// <summary>Frames until nothing is left to do (a pool's workers get their time).</summary>
    public int Settle(int timeoutMilliseconds = 120_000)
    {
        var built = 0;
        var started = Environment.TickCount64;
        do
        {
            built += Frame();
            if (Pipeline.IsAsync)
            {
                Thread.Sleep(1);
            }

            Assert.True(Environment.TickCount64 - started < timeoutMilliseconds, "the view did not settle");
        }
        while (Pipeline.Busy);
        return built;
    }

    /// <summary>The mesh the mesher makes of a chunk from the grid as it is now, with the options the rig gives it.</summary>
    public ChunkMeshes Direct(VoxelChunkCoord coord) => ChunkMesher.Build(new GridBlockSource(Grid, Currents), coord.X, coord.Y, coord.Z, Tracker.GreyKinds, OptionsFor(coord));

    /// <summary>Whether the sink holds, for every wanted chunk that is not buried, the mesh the mesher makes of the grid now; the first chunk that differs (or null).</summary>
    public VoxelChunkCoord? FirstMismatch()
    {
        foreach (var coord in _wanted)
        {
            if (!Grid.TryGetChunk(coord, out var chunk) || ChunkMesher.IsBuried(new GridBlockSource(Grid, Currents), chunk))
            {
                continue;
            }

            if (!_meshes.TryGetValue(coord, out var held) || !Same(held, Direct(coord)) || held.Band != Band)
            {
                return coord;
            }
        }

        return null;
    }

    public static bool Same(ChunkMeshResult held, ChunkMeshes direct) =>
        Same(held.Opaque, direct.Opaque) && Same(held.Alpha, direct.Alpha) && Same(held.Plants, direct.PlantMesh) && Same(held.Lit, direct.LitMesh) && Same(held.Glow, direct.GlowMesh) && held.Kinds == direct.Kinds;

    private static bool Same(MeshPart part, MeshBuffer buffer)
    {
        var quads = Math.Min(buffer.FaceCount, MeshPart.MaxQuads);
        return part.Quads == quads && (quads == 0 || (part.Vertices.AsSpan(0, quads * 4).SequenceEqual(buffer.Vertices[..(quads * 4)]) && (part.Lights is null ? !buffer.HasLights : buffer.HasLights && part.Lights.AsSpan(0, quads * 4).SequenceEqual(buffer.Lights[..(quads * 4)]))));
    }

    public void Install(ChunkMeshResult result)
    {
        InstallOrder.Add(result.Coord);
        Tracker.MarkMeshed(result.Coord, result.Kinds, result.Band);
        if (result.Buried)
        {
            _ = _meshes.Remove(result.Coord);
            return;
        }

        _meshes[result.Coord] = result;
    }

    public void InstallGrass(ChunkMeshResult result) => Grass[result.Coord] = result;

    public void GrassRefused(VoxelChunkCoord coord)
    {
    }

    public void Release(VoxelChunkCoord coord)
    {
        _ = _meshes.Remove(coord);
        Tracker.Discard(coord);
    }

    public void Dispose()
    {
        Pipeline.Dispose();
        View.Detach();
        Lights.Detach();
        _ = Grid.RemoveResidencyListener(Tracker);
    }

    /// <summary>The valley as the renderer sees it, a rig at the spawn.</summary>
    public static MeshingRig Valley(JobPool? pool, int viewDistance = 96, LightBand band = LightBand.Day, bool data = false)
    {
        var scene = RouteScene.Build();
        var eye = new Vector3(scene.Spawn.Feet.X + 0.5f, scene.Spawn.Feet.Y + 1.6f, scene.Spawn.Feet.Z + 0.5f);
        return new MeshingRig(scene.Grid, scene.Currents, pool, eye, viewDistance, band, data);
    }
}
