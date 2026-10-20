using Slg.Core.Determinism;

namespace Slg.Core.Tests;

/// <summary>Tests for <see cref="DeterministicHash"/>.</summary>
public class DeterministicHashTests
{
    private const string EmptySha = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
    private const string AbcSha = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

    [Fact]
    public void Sha256HexMatchesKnownVectors()
    {
        Assert.Equal(EmptySha, DeterministicHash.Sha256Hex(ReadOnlySpan<byte>.Empty));
        Assert.Equal(AbcSha, DeterministicHash.Sha256Hex("abc"u8));
    }

    [Fact]
    public void OfTextMatchesKnownVectors()
    {
        Assert.Equal(EmptySha, DeterministicHash.OfText(string.Empty));
        Assert.Equal(AbcSha, DeterministicHash.OfText("abc"));
    }

    [Fact]
    public void OfTextTreatsComposedAndDecomposedAccentsIdentically()
    {
        Assert.Equal(DeterministicHash.OfText("café"), DeterministicHash.OfText("café"));
    }

    [Fact]
    public void OfTextConvertsCrLfToLfAndHashesUtf8()
    {
        const string expected = "7b49b9e063bd91a4f9252b413261f5557b9c570aa61516989499f64a62dbcdd6";
        Assert.Equal(expected, DeterministicHash.OfText("café\r\n"));
        Assert.Equal(DeterministicHash.OfText("a\nb"), DeterministicHash.OfText("a\r\nb"));
    }

    [Fact]
    public void OfSamplesHashesLittleEndianBytes()
    {
        short[] samples = [1, -2, 300];
        Assert.Equal("449c6a1f60e95b7c5cbc273adb0b1500608f742c10489e5cc45e6cdf549cf45b", DeterministicHash.OfSamples(samples));
        Assert.Equal(DeterministicHash.Sha256Hex([1, 0, 0xFE, 0xFF, 0x2C, 0x01]), DeterministicHash.OfSamples(samples));
        Assert.Equal(EmptySha, DeterministicHash.OfSamples(ReadOnlySpan<short>.Empty));
    }

    [Fact]
    public void CombineFramingIsUnambiguous()
    {
        Assert.NotEqual(DeterministicHash.Combine("a", "b"), DeterministicHash.Combine("ab", string.Empty));
        Assert.NotEqual(DeterministicHash.Combine("ab"), DeterministicHash.Combine("a", "b"));
    }

    [Fact]
    public void CombineMatchesIndependentReference()
    {
        Assert.Equal("37efc929c5c40fd796cc3111e218b304223414aa11ecf5c41f8187cd40cb5955", DeterministicHash.Combine("a", "b"));
        Assert.Equal("4b4dfb90f0b53d4dc6d867451f6199d206bdeab0269c05c4030e23cd0f9d9d34", DeterministicHash.Combine("ab", string.Empty));
    }

    [Fact]
    public void Seed64ReadsFirstEightBytesLittleEndian()
    {
        Assert.Equal(0x36ECCE18DF832932UL, DeterministicHash.Seed64("42", "blacksmith", "7"));
    }

    [Fact]
    public void Seed64ObjectOverloadUsesInvariantTextOfEachPart()
    {
        Assert.Equal(DeterministicHash.Seed64("42", "blacksmith", "7"), DeterministicHash.Seed64(42UL, "blacksmith", 7L));
    }
}
