using System.Buffers.Binary;
using System.IO.Compression;

namespace Vocaterra.WorldPreview;

/// <summary>Writes an <see cref="Image"/> as a PNG (8-bit truecolour, no filter, one IDAT chunk) with the BCL's <see cref="ZLibStream"/> and a hand-written CRC-32.</summary>
internal static class PngWriter
{
    /// <summary>The eight bytes every PNG starts with.</summary>
    public static ReadOnlySpan<byte> Signature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>The PNG file of the image: the same image always gives the same bytes.</summary>
    public static byte[] Encode(Image image)
    {
        ArgumentNullException.ThrowIfNull(image);
        using var file = new MemoryStream();
        file.Write(Signature);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)image.Width);
        BinaryPrimitives.WriteUInt32BigEndian(header[4..], (uint)image.Height);
        header[8] = 8; // bit depth
        header[9] = 2; // truecolour
        header[10] = 0; // deflate
        header[11] = 0; // filter method 0
        header[12] = 0; // no interlace
        WriteChunk(file, "IHDR"u8, header);

        using var packed = new MemoryStream();
        using (var z = new ZLibStream(packed, CompressionLevel.Optimal, true))
        {
            var stride = image.Width * 3;
            for (var y = 0; y < image.Height; y++)
            {
                z.WriteByte(0); // each row starts with its filter type, None
                z.Write(image.Pixels, y * stride, stride);
            }
        }

        WriteChunk(file, "IDAT"u8, packed.ToArray());
        WriteChunk(file, "IEND"u8, []);
        return file.ToArray();
    }

    private static void WriteChunk(Stream to, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> number = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(number, (uint)data.Length);
        to.Write(number);
        to.Write(type);
        to.Write(data);
        var crc = Crc32.Update(data, Crc32.Update(type));
        BinaryPrimitives.WriteUInt32BigEndian(number, crc);
        to.Write(number);
    }
}
