using System.Buffers.Binary;
using System.Security.Cryptography;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Core.World.Regions;

/// <summary>
/// The digest of a region template (B06), which the world's state digest starts with (<see cref="WorldStateHeader.TemplateDigest"/>): a different valley, or the same one in another place,
/// is a different world. It is the SHA-256 of the template's origin chunk and size (little-endian integers) and then the digest of every chunk in the dense order of a bounded grid (layer, row,
/// column), so it reads the same from the built grid or from the frozen region.
/// </summary>
internal static class RegionTemplateDigest
{
    /// <summary>The digest of a bounded grid as the template of a region at (<paramref name="originChunkX"/>, <paramref name="originChunkZ"/>).</summary>
    /// <param name="grid">The grid as built.</param>
    /// <param name="originChunkX">The region's first chunk column.</param>
    /// <param name="originChunkZ">The region's first chunk row.</param>
    /// <param name="originChunkY">The region's first chunk layer: part of the digest only when it is not 0, so the valley (layer 0) keeps the bytes it always had (B15).</param>
    public static byte[] Of(VoxelGrid grid, int originChunkX, int originChunkZ, int originChunkY = 0)
    {
        var limits = grid.Limits;
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> number = stackalloc byte[4];
        foreach (var value in new[] { originChunkX, originChunkZ, limits.EndChunkX, limits.EndChunkY, limits.EndChunkZ })
        {
            BinaryPrimitives.WriteInt32LittleEndian(number, value);
            hash.AppendData(number);
        }

        if (originChunkY != 0)
        {
            BinaryPrimitives.WriteInt32LittleEndian(number, originChunkY);
            hash.AppendData(number);
        }

        for (var cy = limits.MinChunkY; cy < limits.EndChunkY; cy++)
        {
            for (var cz = limits.MinChunkZ; cz < limits.EndChunkZ; cz++)
            {
                for (var cx = limits.MinChunkX; cx < limits.EndChunkX; cx++)
                {
                    hash.AppendData(grid.TryGetChunk(new VoxelChunkCoord(cx, cy, cz), out var chunk) ? chunk.ComputeDigest() : VoxelChunk.AirChunkDigest);
                }
            }
        }

        return hash.GetHashAndReset();
    }
}
