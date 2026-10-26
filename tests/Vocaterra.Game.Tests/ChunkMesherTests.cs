using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World.Generation;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Game.Graphics;

namespace Vocaterra.Game.Tests;

/// <summary>A tiny world for the mesher: the blocks put in it, air everywhere else.</summary>
internal sealed class MapBlocks : IBlockSource
{
    private readonly Dictionary<(int, int, int), BlockId> _blocks = [];

    public MapBlocks Set(int x, int y, int z, BlockId id)
    {
        _blocks[(x, y, z)] = id;
        return this;
    }

    public MapBlocks Fill(int x0, int y0, int z0, int x1, int y1, int z1, BlockId id)
    {
        for (var x = x0; x <= x1; x++)
        {
            for (var y = y0; y <= y1; y++)
            {
                for (var z = z0; z <= z1; z++)
                {
                    Set(x, y, z, id);
                }
            }
        }

        return this;
    }

    public BlockId Get(int x, int y, int z) => _blocks.TryGetValue((x, y, z), out var id) ? id : BlockId.Air;
}

public sealed class ChunkMesherTests
{
    private static ChunkMeshes Mesh(MapBlocks blocks) => ChunkMesher.Build(blocks, 0, 0, 0);

    private static (double X, double Y, double Z) Normal(ReadOnlySpan<MeshVertex> v, int quad)
    {
        var a = v[quad * 4];
        var b = v[(quad * 4) + 1];
        var c = v[(quad * 4) + 2];
        double ux = b.X - a.X, uy = b.Y - a.Y, uz = b.Z - a.Z;
        double vx = c.X - a.X, vy = c.Y - a.Y, vz = c.Z - a.Z;
        var n = ((uy * vz) - (uz * vy), (uz * vx) - (ux * vz), (ux * vy) - (uy * vx));
        var length = Math.Sqrt((n.Item1 * n.Item1) + (n.Item2 * n.Item2) + (n.Item3 * n.Item3));
        return (n.Item1 / length, n.Item2 / length, n.Item3 / length);
    }

    private static (double X, double Y, double Z) Centre(ReadOnlySpan<MeshVertex> v, int quad)
    {
        double x = 0, y = 0, z = 0;
        for (var i = 0; i < 4; i++)
        {
            x += v[(quad * 4) + i].X / 4;
            y += v[(quad * 4) + i].Y / 4;
            z += v[(quad * 4) + i].Z / 4;
        }

        return (x, y, z);
    }

    [Fact]
    public void ASingleBlockHasSixFaces()
    {
        var meshes = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Stone));
        Assert.Equal(6, meshes.Opaque.FaceCount);
        Assert.Equal(0, meshes.Alpha.FaceCount);
        Assert.Equal(24, meshes.Opaque.VertexCount);
    }

    [Fact]
    public void TwoAdjacentBlocksHaveTenFacesWhateverTheDirection()
    {
        foreach (var (dx, dy, dz) in new[] { (1, 0, 0), (0, 1, 0), (0, 0, 1), (-1, 0, 0), (0, -1, 0), (0, 0, -1) })
        {
            var blocks = new MapBlocks().Set(5, 5, 5, BlockId.Stone).Set(5 + dx, 5 + dy, 5 + dz, BlockId.Stone);
            Assert.Equal(10, Mesh(blocks).Opaque.FaceCount);
        }
    }

    [Fact]
    public void ABlockShutInOnEveryEdgeShowsNoFaceAndTheSolidCubeShowsOnlyItsSurface()
    {
        var blocks = new MapBlocks().Fill(4, 4, 4, 6, 6, 6, BlockId.Stone); // a 3 x 3 x 3 cube; the middle one is (5, 5, 5)
        var meshes = Mesh(blocks);
        Assert.Equal(6 * 9, meshes.Opaque.FaceCount);
        var vertices = meshes.Opaque.Vertices;
        for (var quad = 0; quad < meshes.Opaque.FaceCount; quad++)
        {
            var centre = Centre(vertices, quad);
            // A quad of the middle block would lie on the planes 5 or 6 inside the cube; none of them is inside the surface [4, 7].
            var inside = centre is { X: > 4.01 and < 6.99, Y: > 4.01 and < 6.99, Z: > 4.01 and < 6.99 };
            Assert.False(inside, $"quad {quad} lies inside the cube at {centre}");
        }

        // The rule on its own: a block whose six neighbours are opaque cubes has no visible face.
        foreach (var neighbour in new[] { BlockId.Grass, BlockId.Dirt, BlockId.Stone, BlockId.Planks, BlockId.Wall, BlockId.Roof })
        {
            Assert.False(ChunkMesher.FaceVisible(BlockId.Stone, neighbour));
        }
    }

    [Fact]
    public void WaterAgainstStoneShowsTheStonesFaceToTheWaterButNotTheWatersFaceToTheStone()
    {
        var meshes = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Stone).Set(6, 5, 5, BlockId.Water));
        Assert.Equal(6, meshes.Opaque.FaceCount); // stone: all six, the east one is seen through the water
        Assert.Equal(5, meshes.Alpha.FaceCount); // water: the face towards the stone is culled
    }

    [Fact]
    public void WaterAgainstWaterShowsNoFaceBetweenThemAndLowersTheSurface()
    {
        var side = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Water).Set(6, 5, 5, BlockId.Water));
        Assert.Equal(10, side.Alpha.FaceCount);
        Assert.Equal(0, side.Opaque.FaceCount);

        var stacked = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Water).Set(5, 6, 5, BlockId.Water));
        Assert.Equal(10, stacked.Alpha.FaceCount);

        var single = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Water));
        var top = single.Alpha.Vertices.ToArray().Max(static v => v.Y);
        Assert.InRange(top, 5.8, 5.95); // the surface stands a little below the top of the cell
    }

    [Fact]
    public void LeavesAreTransparentSoTheNeighbourShowsItsFaceButLeavesAgainstLeavesAreCulled()
    {
        var stoneAndLeaves = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Stone).Set(6, 5, 5, BlockId.Leaves));
        Assert.Equal(11, stoneAndLeaves.Opaque.FaceCount); // stone 6 (the one towards the leaves too), leaves 5
        var leaves = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Leaves).Set(6, 5, 5, BlockId.Leaves));
        Assert.Equal(10, leaves.Opaque.FaceCount);
    }

    [Fact]
    public void ABlockOnTheChunkEdgeLooksAcrossIt()
    {
        // A stone block in chunk 0 on its east edge with another against it in chunk 1: the shared faces are culled in both chunks.
        var blocks = new MapBlocks().Set(15, 5, 5, BlockId.Stone).Set(16, 5, 5, BlockId.Stone);
        Assert.Equal(5, ChunkMesher.Build(blocks, 0, 0, 0).Opaque.FaceCount);
        Assert.Equal(5, ChunkMesher.Build(blocks, 1, 0, 0).Opaque.FaceCount);
        var alone = new MapBlocks().Set(15, 5, 5, BlockId.Stone);
        Assert.Equal(6, ChunkMesher.Build(alone, 0, 0, 0).Opaque.FaceCount);
    }

    [Fact]
    public void EveryFaceTurnsCounterClockwiseSeenFromOutsideAndFacesAwayFromTheBlock()
    {
        var meshes = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Grass));
        var vertices = meshes.Opaque.Vertices;
        var seen = new HashSet<(int, int, int)>();
        for (var quad = 0; quad < 6; quad++)
        {
            var n = Normal(vertices, quad);
            var c = Centre(vertices, quad);
            var outward = (c.X - 5.5, c.Y - 5.5, c.Z - 5.5);
            Assert.True((n.X * outward.Item1) + (n.Y * outward.Item2) + (n.Z * outward.Item3) > 0.4, $"quad {quad} winds the wrong way");
            seen.Add(((int)Math.Round(n.X), (int)Math.Round(n.Y), (int)Math.Round(n.Z)));
        }

        Assert.Equal(6, seen.Count); // one quad for each of the six directions
    }

    [Fact]
    public void ATopIsBrightestTheBottomDarkestAndTheSidesBetween()
    {
        var meshes = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Stone));
        var vertices = meshes.Opaque.Vertices;
        var byDirection = new Dictionary<string, int>();
        for (var quad = 0; quad < 6; quad++)
        {
            var n = Normal(vertices, quad);
            var key = Math.Abs(n.Y) > 0.5 ? (n.Y > 0 ? "up" : "down") : Math.Abs(n.X) > 0.5 ? (n.X > 0 ? "east" : "west") : (n.Z > 0 ? "south" : "north");
            byDirection[key] = (int)(vertices[quad * 4].Color & 0xFF); // the red channel is the shade for the grey white tint
        }

        // S14: the sun is in the north-west, so the west and north faces are lit, the east and south faces are not, and the bottom is darkest.
        Assert.True(byDirection["up"] > byDirection["north"]);
        Assert.True(byDirection["up"] > byDirection["west"]);
        Assert.True(byDirection["west"] > byDirection["east"]);
        Assert.True(byDirection["north"] > byDirection["south"]);
        Assert.Equal(byDirection["east"], byDirection["south"]);
        Assert.True(byDirection["east"] > byDirection["down"]);
        Assert.Equal(255, byDirection["up"]);
    }

    [Fact]
    public void EachFaceTakesItsPictureFromTheAtlasAndStaysInsideItsCell()
    {
        var meshes = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Grass));
        var vertices = meshes.Opaque.Vertices;
        var cells = new HashSet<int>();
        for (var quad = 0; quad < 6; quad++)
        {
            var n = Normal(vertices, quad);
            var face = Math.Abs(n.Y) > 0.5 ? (n.Y > 0 ? BlockFace.Up : BlockFace.Down) : Math.Abs(n.X) > 0.5 ? (n.X > 0 ? BlockFace.East : BlockFace.West) : (n.Z > 0 ? BlockFace.South : BlockFace.North);
            var cell = BlockAtlasLayout.CellFor(BlockId.Grass, face);
            cells.Add((int)cell);
            var (u0, v0, u1, v1) = BlockAtlasLayout.Uv(cell);
            for (var i = 0; i < 4; i++)
            {
                var vertex = vertices[(quad * 4) + i];
                Assert.InRange(vertex.U, u0 - 1e-6f, u1 + 1e-6f);
                Assert.InRange(vertex.V, v0 - 1e-6f, v1 + 1e-6f);
            }
        }

        Assert.Equal(3, cells.Count); // grass: top, the fringe on the four sides, dirt underneath
    }

    [Fact]
    public void TheCornersOfASideFaceRunBottomLeftBottomRightTopRightTopLeftWithTheTextureUpright()
    {
        // The south face of a block is seen from the south with east on the right: its texture's left edge is the west edge.
        var meshes = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Stone));
        var vertices = meshes.Opaque.Vertices;
        for (var quad = 0; quad < 6; quad++)
        {
            if (Normal(vertices, quad).Z < 0.5)
            {
                continue;
            }

            var bottomLeft = vertices[quad * 4];
            var bottomRight = vertices[(quad * 4) + 1];
            var topLeft = vertices[(quad * 4) + 3];
            Assert.True(bottomRight.X > bottomLeft.X);
            Assert.True(bottomRight.U > bottomLeft.U);
            Assert.True(topLeft.Y > bottomLeft.Y);
            Assert.True(topLeft.V < bottomLeft.V); // v grows downward in the atlas
        }
    }

    [Fact]
    public void PlantsTorchesAndFencesAreSmallShapesThatNeverHideTheirNeighbours()
    {
        Assert.Equal(6, Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Bush)).Opaque.FaceCount);
        Assert.Equal(6, Mesh(new MapBlocks().Set(5, 5, 5, BlockId.BerryBush)).Opaque.FaceCount);
        Assert.Equal(12, Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Torch)).Opaque.FaceCount); // the stick and the flame
        Assert.Equal(6, Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Fence)).Opaque.FaceCount);
        // A stone block next to a bush shows all six of its faces: the bush is not an opaque cube.
        var withBush = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Stone).Set(6, 5, 5, BlockId.Bush));
        Assert.Equal(6 + 6, withBush.Opaque.FaceCount);
    }

    [Fact]
    public void AFenceGrowsRailsTowardsTheFencesNextToIt()
    {
        var one = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Fence)).Opaque.FaceCount;
        var two = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Fence).Set(6, 5, 5, BlockId.Fence)).Opaque.FaceCount;
        // Each post is 6 faces; each of the two has two rails (6 faces each) towards the other.
        Assert.Equal(one * 2 + (2 * 2 * 6), two);
    }

    [Fact]
    public void AnOpenDoorIsAThinPanelAlongTheWallsDirectionAcrossTheDoorway()
    {
        // Alone, the panel lies along the east-west axis at the north edge: thin in z, so no vertex has x = 5.1875.
        var alone = Mesh(new MapBlocks().Set(5, 5, 5, BlockId.Door));
        Assert.Equal(6, alone.Opaque.FaceCount);
        Assert.DoesNotContain(alone.Opaque.Vertices.ToArray(), static v => Math.Abs(v.X - 5.1875f) < 1e-4f);
        Assert.Contains(alone.Opaque.Vertices.ToArray(), static v => Math.Abs(v.Z - 5.1875f) < 1e-4f);

        // In a wall that runs east-west (walls to the west and east) it swings out of the way: thin in x. The door is not an opaque cube,
        // so the two walls keep all their faces: 6 + 6 + 6.
        var inWall = Mesh(new MapBlocks().Set(4, 5, 5, BlockId.Wall).Set(6, 5, 5, BlockId.Wall).Set(5, 5, 5, BlockId.Door));
        Assert.Equal(18, inWall.Opaque.FaceCount);
        Assert.Contains(inWall.Opaque.Vertices.ToArray(), static v => Math.Abs(v.X - 5.1875f) < 1e-4f);
    }

    [Fact]
    public void TheMesherOnTheRealRouteBuildsEveryChunkWithinTheSixteenBitLimit()
    {
        var scene = SceneBuilder.Build(SceneLayout.Load(Repo.Path("content", "game-v1", "game", "scene.json")), 42);
        var source = new GridBlockSource(scene.Grid);
        var faces = 0;
        var vertices = 0;
        var skippable = 0;
        foreach (var chunk in scene.Grid.Chunks)
        {
            // A chunk of air and a chunk buried in solid blocks have no face: the renderer skips them, and the mesher agrees (checked on the first of each).
            var skipped = chunk.IsEmpty || ChunkMesher.IsBuried(source, chunk);
            if (skipped && ++skippable > 30)
            {
                continue;
            }

            var meshes = ChunkMesher.Build(source, chunk.Coord.X, chunk.Coord.Y, chunk.Coord.Z);
            Assert.True(meshes.Opaque.VertexCount <= 65532 && meshes.Alpha.VertexCount <= 65532);
            if (skipped)
            {
                Assert.Equal(0, meshes.FaceCount);
            }

            foreach (var v in meshes.Opaque.Vertices)
            {
                Assert.True(float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z));
            }

            faces += meshes.FaceCount;
            Assert.True(meshes.PlantMesh.VertexCount <= 65532);
            vertices += meshes.Opaque.VertexCount + meshes.Alpha.VertexCount + meshes.PlantMesh.VertexCount;
        }

        Assert.Equal(faces * 4, vertices);
        Assert.InRange(faces, 400_000, 2_000_000); // the culling works: 384 x 128 x 384 blocks, about a million faces with the undergrowth's crossed quads
        var allBlocks = scene.Grid.SizeX * scene.Grid.SizeY * scene.Grid.SizeZ;
        Assert.True(faces < allBlocks / 8);
        Assert.True(skippable > 2000, $"{skippable} chunks of air or buried in stone");
    }

    [Fact]
    public void WaterOfTheRiverGoesIntoTheTranslucentMeshAndNothingElseDoes()
    {
        var scene = SceneBuilder.Build(SceneLayout.Load(Repo.Path("content", "game-v1", "game", "scene.json")), 42);
        var source = new GridBlockSource(scene.Grid);
        var river = scene.Point("river");
        var chunk = VoxelChunkCoord.FromBlock(river.X, river.Y, river.Z);
        var meshes = ChunkMesher.Build(source, chunk.X, chunk.Y, chunk.Z);
        Assert.True(meshes.Alpha.FaceCount > 0);
        Assert.All(Enumerable.Range(0, meshes.Alpha.VertexCount), i => Assert.Contains(CellOf(meshes.Alpha.Vertices[i]), new[] { BlockCell.Water, BlockCell.WaterShallow, BlockCell.Waterfall }));
    }

    private static BlockCell CellOf(MeshVertex vertex)
    {
        var column = (int)(vertex.U * BlockAtlasLayout.Width / BlockAtlasLayout.CellSize);
        var row = (int)(vertex.V * BlockAtlasLayout.Height / BlockAtlasLayout.CellSize);
        return (BlockCell)((row * BlockAtlasLayout.Columns) + column);
    }

    [Fact]
    public void TheVertexIs24BytesLikeTheEnginesPositionColourTextureVertex()
    {
        Assert.Equal(MeshVertex.SizeInBytes, System.Runtime.InteropServices.Marshal.SizeOf<MeshVertex>());
        Assert.Equal(MeshVertex.SizeInBytes, Microsoft.Xna.Framework.Graphics.VertexPositionColorTexture.VertexDeclaration.VertexStride);
        Assert.Equal(0xFF0000FFu, MeshVertex.Pack(255, 0, 0)); // red is the lowest byte, alpha the highest
        Assert.Equal(0xFF808080u, MeshVertex.Shade(0.5f));
    }
}
