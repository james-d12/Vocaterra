using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.Save;

/// <summary>
/// The world part of a save: the chunks that differ from the world as generated (<see cref="VoxelChunk.IsChanged"/>), and how they differ. Only changed chunks are listed,
/// so a save of a world the player has barely changed is small and an untouched chunk is never written. No second copy of the world is kept to compare against: a chunk
/// remembers its own pristine digest.
/// </summary>
public static class VoxelGridDiff
{
    /// <summary>The changed chunks of <paramref name="grid"/>, sorted by coordinate, each as runs of its blocks, of its water levels and of its states.</summary>
    /// <param name="grid">The grid, bounded or streamed.</param>
    public static IReadOnlyList<ChunkEdit> Capture(VoxelGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var edits = new List<ChunkEdit>();
        foreach (var chunk in grid.ChangedChunks())
        {
            edits.Add(new ChunkEdit(chunk.Coord.X, chunk.Coord.Y, chunk.Coord.Z, Runs(chunk.Blocks), WaterRuns(chunk.WaterLevels), StateRuns(chunk.States)));
        }

        return edits;
    }

    /// <summary>
    /// Writes the chunks of a diff over <paramref name="target"/>, a world built as a new game builds it. A chunk the target does not hold yet (a streamed grid) is generated
    /// first, by the grid's chunk generator, and then overwritten. Refuses a diff that does not fit the grid:
    /// a chunk outside the grid's limits, a run of an unknown block, runs that do not add up to one chunk, a water run that covers a cell that is not water or holds
    /// no valid level, a state run that covers air or lies outside the chunk, or a chunk listed twice.
    /// </summary>
    /// <exception cref="ArgumentException">The diff does not fit the grid.</exception>
    public static void Apply(VoxelGrid target, IReadOnlyList<ChunkEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(edits);
        var seen = new HashSet<VoxelChunkCoord>();
        foreach (var edit in edits)
        {
            var origin = new VoxelChunkCoord(edit.X, edit.Y, edit.Z);
            var corner = origin.Origin;
            if (!target.InBounds(corner.X, corner.Y, corner.Z) || !seen.Add(origin))
            {
                throw new ArgumentException($"Chunk ({edit.X}, {edit.Y}, {edit.Z}) is outside the grid or listed twice.", nameof(edits));
            }

            var position = 0;
            foreach (var run in edit.Runs)
            {
                if (run.Length < 1 || !Enum.IsDefined(run.Block) || position + run.Length > VoxelChunk.Volume)
                {
                    throw new ArgumentException($"Chunk ({edit.X}, {edit.Y}, {edit.Z}) has a bad run.", nameof(edits));
                }

                for (var end = position + run.Length; position < end; position++)
                {
                    var localX = position & (VoxelChunk.Size - 1);
                    var localZ = (position >> VoxelChunk.Shift) & (VoxelChunk.Size - 1);
                    var localY = position >> (2 * VoxelChunk.Shift);
                    _ = target.SetBlock(corner.X + localX, corner.Y + localY, corner.Z + localZ, run.Block);
                }
            }

            if (position != VoxelChunk.Volume)
            {
                throw new ArgumentException($"Chunk ({edit.X}, {edit.Y}, {edit.Z}) has {position} blocks, not {VoxelChunk.Volume}.", nameof(edits));
            }

            ApplyWater(target, origin, edit);
            ApplyStates(target, origin, edit);
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

    // Sets the levels of a chunk's water runs once its blocks are in place.
    private static void ApplyWater(VoxelGrid target, VoxelChunkCoord origin, ChunkEdit edit)
    {
        var covered = 0;
        foreach (var run in edit.Water ?? [])
        {
            if (run.Start < covered || run.Length < 1 || run.Level is < 1 or > byte.MaxValue || run.Start + run.Length > VoxelChunk.Volume)
            {
                throw new ArgumentException($"Chunk ({edit.X}, {edit.Y}, {edit.Z}) has a bad water run.", nameof(edit));
            }

            var level = WaterLevel.FromEncoded((byte)run.Level); // refuses an encoding that is not a level
            for (var position = run.Start; position < run.Start + run.Length; position++)
            {
                var x = (origin.X << VoxelChunk.Shift) + (position & (VoxelChunk.Size - 1));
                var z = (origin.Z << VoxelChunk.Shift) + ((position >> VoxelChunk.Shift) & (VoxelChunk.Size - 1));
                var y = (origin.Y << VoxelChunk.Shift) + (position >> (2 * VoxelChunk.Shift));
                if (target.GetBlock(x, y, z) != BlockId.Water)
                {
                    throw new ArgumentException($"Chunk ({edit.X}, {edit.Y}, {edit.Z}) has a water level on a cell that is not water.", nameof(edit));
                }

                _ = target.SetWater(x, y, z, level);
            }

            covered = run.Start + run.Length;
        }
    }

    // Sets the states of a chunk's state runs once its blocks are in place: a state is written with its cell's own block (the same block, so only the state changes).
    private static void ApplyStates(VoxelGrid target, VoxelChunkCoord origin, ChunkEdit edit)
    {
        var covered = 0;
        foreach (var run in edit.States ?? [])
        {
            if (run.Start < covered || run.Length < 1 || run.State is < 1 or > byte.MaxValue || run.Start + run.Length > VoxelChunk.Volume)
            {
                throw new ArgumentException($"Chunk ({edit.X}, {edit.Y}, {edit.Z}) has a bad state run.", nameof(edit));
            }

            for (var position = run.Start; position < run.Start + run.Length; position++)
            {
                var x = (origin.X << VoxelChunk.Shift) + (position & (VoxelChunk.Size - 1));
                var z = (origin.Z << VoxelChunk.Shift) + ((position >> VoxelChunk.Shift) & (VoxelChunk.Size - 1));
                var y = (origin.Y << VoxelChunk.Shift) + (position >> (2 * VoxelChunk.Shift));
                var block = target.GetBlock(x, y, z);
                if (block == BlockId.Air)
                {
                    throw new ArgumentException($"Chunk ({edit.X}, {edit.Y}, {edit.Z}) has a state on a cell that is air.", nameof(edit));
                }

                _ = target.SetBlock(x, y, z, block, (byte)run.State);
            }

            covered = run.Start + run.Length;
        }
    }

    // The cells holding a state other than 0, as runs of equal states; null when there are none.
    private static List<StateRun>? StateRuns(ReadOnlySpan<byte> states)
    {
        if (states.IsEmpty)
        {
            return null;
        }

        var runs = new List<StateRun>();
        var i = 0;
        while (i < states.Length)
        {
            if (states[i] == 0)
            {
                i++;
                continue;
            }

            var start = i;
            var state = states[i];
            while (i < states.Length && states[i] == state)
            {
                i++;
            }

            runs.Add(new StateRun(start, i - start, state));
        }

        return runs;
    }

    // The cells holding a level other than source, as runs of equal levels; null when there are none.
    private static List<WaterRun>? WaterRuns(ReadOnlySpan<byte> levels)
    {
        if (levels.IsEmpty)
        {
            return null;
        }

        var runs = new List<WaterRun>();
        var i = 0;
        while (i < levels.Length)
        {
            if (levels[i] == 0)
            {
                i++;
                continue;
            }

            var start = i;
            var level = levels[i];
            while (i < levels.Length && levels[i] == level)
            {
                i++;
            }

            runs.Add(new WaterRun(start, i - start, level));
        }

        return runs;
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

