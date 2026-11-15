using System.Security.Cryptography;
using Vocaterra.Game.Core.World.Biomes;
using Vocaterra.Game.Core.World.Generation;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.World;

/// <summary>
/// What the world's state digest starts with (B05): everything that decides what an unchanged chunk holds, so the digest needs to list only the changed chunks.
/// </summary>
/// <param name="Seed">The world seed.</param>
/// <param name="TemplateDigest">The digest of the region template the world copies its settlement from (32 bytes); all zero while the world has none (B06 supplies it).</param>
/// <param name="GeneratorVersion">The <see cref="ProceduralGenerator.Version"/> the world was generated with.</param>
/// <param name="GenerationDataHash">The SHA-256 of the world-generation data files only (<see cref="GenerationFiles"/>), so editing a phrase does not move the digest (32 bytes).</param>
public sealed record WorldStateHeader(ulong Seed, byte[] TemplateDigest, int GeneratorVersion, byte[] GenerationDataHash)
{
    /// <summary>The length of a digest field in bytes.</summary>
    public const int DigestLength = 32;

    /// <summary>The bundle's world-generation files, in hashing order. A file the bundle does not have is left out (<c>underground.json</c> arrives with the caves).</summary>
    public static readonly IReadOnlyList<string> GenerationFiles = [WorldGenData.RelativePath, BiomeData.RelativePath, "game/underground.json"];

    /// <summary>The header of a scene built without game data (a test's world): no template, no generation data.</summary>
    /// <param name="seed">The world seed.</param>
    public static WorldStateHeader Unkeyed(ulong seed) => new(seed, new byte[DigestLength], ProceduralGenerator.Version, new byte[DigestLength]);

    /// <summary>The header of a world built from a bundle.</summary>
    /// <param name="bundle">The bundle (loaded with hash verification).</param>
    /// <param name="seed">The world seed.</param>
    /// <param name="templateDigest">The region template's digest, or <see langword="null"/> for none.</param>
    public static WorldStateHeader For(ContentBundle bundle, ulong seed, byte[]? templateDigest = null)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var path in GenerationFiles)
        {
            if (!bundle.TryReadHostFile(path, out var bytes))
            {
                continue;
            }

            hash.AppendData(System.Text.Encoding.UTF8.GetBytes(path));
            hash.AppendData([0]);
            hash.AppendData(bytes.Span);
            hash.AppendData([0]);
        }

        return new WorldStateHeader(seed, templateDigest ?? new byte[DigestLength], ProceduralGenerator.Version, hash.GetHashAndReset());
    }
}
