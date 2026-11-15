using Vocaterra.Game.Core.Voxel;
using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Vocaterra.Game.Core.World;

/// <summary>
/// The digest of the world's state (B05): the SHA-256 of a <see cref="WorldStateHeader"/> and then the changed chunks (<see cref="VoxelChunk.IsChanged"/>), sorted by
/// coordinate, each as its three coordinates and its chunk digest. A chunk nobody wrote, or whose edits were all undone, is not listed, so the digest does not depend on
/// which chunks are resident and its cost grows with the number of changed chunks, not with the size of the world. It works on bounded and streamed grids.
/// Layout: seed (u64), template digest (32 bytes), generator version (i32), generation data hash (32 bytes), the number of changed chunks (i32), then per chunk X, Y, Z (i32 each)
/// and its 32-byte digest; every integer is little-endian.
/// </summary>
public static class WorldStateDigest
{
    /// <summary>The digest of <paramref name="grid"/> under <paramref name="header"/>.</summary>
    /// <param name="grid">The grid (bounded or streamed).</param>
    /// <param name="header">The header.</param>
    public static byte[] Compute(VoxelGrid grid, WorldStateHeader header)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentNullException.ThrowIfNull(header);
        var changed = grid.ChangedChunks();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> number = stackalloc byte[12];
        BinaryPrimitives.WriteUInt64LittleEndian(number, header.Seed);
        hash.AppendData(number[..8]);
        hash.AppendData(header.TemplateDigest);
        BinaryPrimitives.WriteInt32LittleEndian(number, header.GeneratorVersion);
        hash.AppendData(number[..4]);
        hash.AppendData(header.GenerationDataHash);
        BinaryPrimitives.WriteInt32LittleEndian(number, changed.Count);
        hash.AppendData(number[..4]);
        foreach (var chunk in changed)
        {
            BinaryPrimitives.WriteInt32LittleEndian(number, chunk.Coord.X);
            BinaryPrimitives.WriteInt32LittleEndian(number[4..], chunk.Coord.Y);
            BinaryPrimitives.WriteInt32LittleEndian(number[8..], chunk.Coord.Z);
            hash.AppendData(number);
            hash.AppendData(chunk.ComputeDigest());
        }

        return hash.GetHashAndReset();
    }

    /// <summary>The digest as 64 lowercase hex characters.</summary>
    /// <param name="grid">The grid.</param>
    /// <param name="header">The header.</param>
    public static string ComputeHex(VoxelGrid grid, WorldStateHeader header) => Convert.ToHexString(Compute(grid, header)).ToLowerInvariant();
}
