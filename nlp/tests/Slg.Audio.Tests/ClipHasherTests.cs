using System.Security.Cryptography;
using Slg.Core.Determinism;

namespace Slg.Audio.Tests;

/// <summary>Tests for the clip hash.</summary>
public class ClipHasherTests
{
    [Fact]
    public void EmptyClipHasTheKnownSha256OfNothing()
    {
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", ClipHasher.Hash([]));
    }

    [Fact]
    public void HashIsSha256OfLittleEndianSampleBytes()
    {
        short[] samples = [1, -2, 0x1234, short.MinValue];
        byte[] bytes = [0x01, 0x00, 0xFE, 0xFF, 0x34, 0x12, 0x00, 0x80];

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), ClipHasher.Hash(samples));
        Assert.Equal(DeterministicHash.OfSamples(samples), ClipHasher.Hash(samples));
    }

    [Fact]
    public void DifferentSamplesGiveDifferentHashes()
    {
        Assert.NotEqual(ClipHasher.Hash([1, 2, 3]), ClipHasher.Hash([1, 2, 4]));
    }
}
