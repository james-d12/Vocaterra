using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Slg.Core.Determinism;

/// <summary>Stable SHA-256 based hashing over canonical UTF-8 and little-endian bytes.</summary>
public static class DeterministicHash
{
    /// <summary>Returns the lowercase hexadecimal SHA-256 digest of the given bytes.</summary>
    public static string Sha256Hex(ReadOnlySpan<byte> data)
    {
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(data, digest);
        return Convert.ToHexStringLower(digest);
    }

    /// <summary>Hashes text after NFC normalization and CRLF to LF conversion, as UTF-8.</summary>
    public static string OfText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var canonical = text.Normalize(NormalizationForm.FormC).Replace("\r\n", "\n", StringComparison.Ordinal);
        return Sha256Hex(Encoding.UTF8.GetBytes(canonical));
    }

    /// <summary>Hashes 16-bit PCM samples as little-endian bytes.</summary>
    public static string OfSamples(ReadOnlySpan<short> samples)
    {
        var bytes = new byte[samples.Length * sizeof(short)];
        for (var i = 0; i < samples.Length; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * sizeof(short)), samples[i]);
        }

        return Sha256Hex(bytes);
    }

    /// <summary>Hashes the parts with unambiguous length-prefixed framing (4-byte little-endian UTF-8 length, then the bytes).</summary>
    public static string Combine(params string[] parts)
    {
        return Convert.ToHexStringLower(CombineBytes(parts));
    }

    /// <summary>Derives a 64-bit seed from the first 8 bytes of <see cref="Combine(string[])"/>, read little-endian.</summary>
    public static ulong Seed64(params string[] parts)
    {
        return BinaryPrimitives.ReadUInt64LittleEndian(CombineBytes(parts));
    }

    /// <summary>Derives a 64-bit seed from parts converted with the invariant culture, equal to the string overload on the same text.</summary>
    public static ulong Seed64(params object[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var strings = new string[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            strings[i] = parts[i] switch
            {
                null => throw new ArgumentException("Seed parts must not be null.", nameof(parts)),
                string s => s,
                IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
                var other => other.ToString() ?? string.Empty,
            };
        }

        return Seed64(strings);
    }

    private static byte[] CombineBytes(string[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[sizeof(int)];
        foreach (var part in parts)
        {
            ArgumentNullException.ThrowIfNull(part, nameof(parts));
            var bytes = Encoding.UTF8.GetBytes(part);
            BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return hash.GetHashAndReset();
    }
}
