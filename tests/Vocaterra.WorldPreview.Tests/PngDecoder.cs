using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Vocaterra.WorldPreview.Tests;

/// <summary>A reader of the PNGs the tool writes (8-bit truecolour, filter 0), for the tests: it checks the signature and every chunk's length.</summary>
internal static class PngDecoder
{
    public sealed record Chunk(string Type, byte[] Data, uint StoredCrc);

    public static IReadOnlyList<Chunk> Chunks(byte[] png)
    {
        Assert.True(png.AsSpan(0, 8).SequenceEqual(PngWriter.Signature), "not a PNG");
        var chunks = new List<Chunk>();
        var at = 8;
        while (at < png.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(at));
            var type = Encoding.ASCII.GetString(png, at + 4, 4);
            var data = png.AsSpan(at + 8, length).ToArray();
            var crc = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(at + 8 + length));
            chunks.Add(new Chunk(type, data, crc));
            at += 12 + length;
        }

        Assert.Equal(png.Length, at);
        return chunks;
    }

    public static (int Width, int Height, byte[] Pixels) Decode(byte[] png)
    {
        var chunks = Chunks(png);
        var header = chunks[0].Data;
        var width = (int)BinaryPrimitives.ReadUInt32BigEndian(header);
        var height = (int)BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4));
        Assert.Equal(8, header[8]);
        Assert.Equal(2, header[9]);
        using var z = new ZLibStream(new MemoryStream(chunks.Single(c => c.Type == "IDAT").Data), CompressionMode.Decompress);
        using var inflated = new MemoryStream();
        z.CopyTo(inflated);
        var raw = inflated.ToArray();
        var stride = width * 3;
        Assert.Equal(height * (stride + 1), raw.Length);
        var pixels = new byte[height * stride];
        for (var y = 0; y < height; y++)
        {
            Assert.Equal(0, raw[y * (stride + 1)]);
            Array.Copy(raw, (y * (stride + 1)) + 1, pixels, y * stride, stride);
        }

        return (width, height, pixels);
    }

    public static Rgb Pixel((int Width, int Height, byte[] Pixels) image, int x, int y)
    {
        var i = ((y * image.Width) + x) * 3;
        return new Rgb(image.Pixels[i], image.Pixels[i + 1], image.Pixels[i + 2]);
    }
}
