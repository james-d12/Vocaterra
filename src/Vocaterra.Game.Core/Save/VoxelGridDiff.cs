using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Save;

/// <summary>
/// The scene diff of a save: which chunks of the edited world differ from the freshly built scene, and how they differ. Only the edited chunks
/// are listed, so a save of a world the player has barely changed is small; an untouched chunk is never written.
/// </summary>
public static class VoxelGridDiff
{
    /// <summary>
    /// The chunks of <paramref name="edited"/> whose blocks differ from <paramref name="fresh"/>, in chunk order, each as runs of its blocks.
    /// Both grids must have the same size.
    /// </summary>
    /// <exception cref="ArgumentException">The two grids have different sizes.</exception>
    public static IReadOnlyList<ChunkEdit> Capture(VoxelGrid edited, VoxelGrid fresh)
    {
        ArgumentNullException.ThrowIfNull(edited);
        ArgumentNullException.ThrowIfNull(fresh);
        if (edited.SizeX != fresh.SizeX || edited.SizeY != fresh.SizeY || edited.SizeZ != fresh.SizeZ)
        {
            throw new ArgumentException("The grids have different sizes.", nameof(fresh));
        }

        var edits = new List<ChunkEdit>();
        for (var i = 0; i < edited.Chunks.Count; i++)
        {
            var chunk = edited.Chunks[i];
            if (!chunk.Blocks.SequenceEqual(fresh.Chunks[i].Blocks))
            {
                edits.Add(new ChunkEdit(i, Runs(chunk.Blocks)));
            }
        }

        return edits;
    }

    /// <summary>
    /// Writes the chunks of a diff over <paramref name="target"/> (a freshly built grid of the same scene). Refuses a diff that does not fit the grid:
    /// a chunk index out of range, a run of an unknown block, runs that do not add up to one chunk, or a chunk listed twice.
    /// </summary>
    /// <exception cref="ArgumentException">The diff does not fit the grid.</exception>
    public static void Apply(VoxelGrid target, IReadOnlyList<ChunkEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(edits);
        var seen = new HashSet<int>();
        foreach (var edit in edits)
        {
            if ((uint)edit.Chunk >= (uint)target.Chunks.Count || !seen.Add(edit.Chunk))
            {
                throw new ArgumentException($"Chunk {edit.Chunk} is outside the grid or listed twice.", nameof(edits));
            }

            var origin = target.Chunks[edit.Chunk].Coord;
            var position = 0;
            foreach (var run in edit.Runs)
            {
                if (run.Length < 1 || !Enum.IsDefined(run.Block) || position + run.Length > VoxelChunk.Volume)
                {
                    throw new ArgumentException($"Chunk {edit.Chunk} has a bad run.", nameof(edits));
                }

                for (var end = position + run.Length; position < end; position++)
                {
                    var localX = position & (VoxelChunk.Size - 1);
                    var localZ = (position >> VoxelChunk.Shift) & (VoxelChunk.Size - 1);
                    var localY = position >> (2 * VoxelChunk.Shift);
                    _ = target.SetBlock(
                        (origin.X << VoxelChunk.Shift) + localX,
                        (origin.Y << VoxelChunk.Shift) + localY,
                        (origin.Z << VoxelChunk.Shift) + localZ,
                        run.Block);
                }
            }

            if (position != VoxelChunk.Volume)
            {
                throw new ArgumentException($"Chunk {edit.Chunk} has {position} blocks, not {VoxelChunk.Volume}.", nameof(edits));
            }
        }
    }

    /// <summary>
    /// Writes one block of a save onto a freshly built grid (X01: a save made from another content bundle applies its edits cell by cell, by position, see
    /// <c>SaveMigration</c>). Only the save's load uses it, on a grid no simulation is running on.
    /// </summary>
    /// <param name="target">The grid.</param>
    /// <param name="x">The x of the cell.</param>
    /// <param name="y">The y of the cell.</param>
    /// <param name="z">The z of the cell.</param>
    /// <param name="block">The block to write.</param>
    /// <exception cref="ArgumentOutOfRangeException">The cell lies outside the grid.</exception>
    public static void SetCell(VoxelGrid target, int x, int y, int z, BlockId block)
    {
        ArgumentNullException.ThrowIfNull(target);
        _ = target.SetBlock(x, y, z, block);
    }

    private static List<BlockRun> Runs(ReadOnlySpan<BlockId> blocks)
    {
        var runs = new List<BlockRun>();
        var current = blocks[0];
        var length = 0;
        foreach (var block in blocks)
        {
            if (block == current)
            {
                length++;
                continue;
            }

            runs.Add(new BlockRun(current, length));
            current = block;
            length = 1;
        }

        runs.Add(new BlockRun(current, length));
        return runs;
    }
}

