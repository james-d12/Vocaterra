using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Vocaterra.WorldPreview.Tests;

public sealed class PngTests
{
    [Theory]
    [InlineData("", 0x00000000u)]
    [InlineData("a", 0xE8B7BE43u)]
    [InlineData("123456789", 0xCBF43926u)]
    [InlineData("IEND", 0xAE426082u)]
    [InlineData("The quick brown fox jumps over the lazy dog", 0x414FA339u)]
    public void Crc32_matches_the_known_vectors(string text, uint expected) =>
        Assert.Equal(expected, Crc32.Update(Encoding.ASCII.GetBytes(text)));

    [Fact]
    public void Crc32_continues_from_an_earlier_value()
    {
        var whole = Crc32.Update("123456789"u8);
        Assert.Equal(whole, Crc32.Update("56789"u8, Crc32.Update("1234"u8)));
    }

    [Fact]
    public void A_written_png_decodes()
    {
        var image = new Image(7, 5, new Rgb(1, 2, 3));
        image.Set(0, 0, new Rgb(255, 0, 0));
        image.Set(6, 4, new Rgb(0, 255, 17));

        var (width, height, pixels) = PngDecoder.Decode(PngWriter.Encode(image));

        Assert.Equal(7, width);
        Assert.Equal(5, height);
        Assert.Equal(image.Pixels, pixels);
    }

    [Fact]
    public void A_written_png_has_the_signature_the_ihdr_first_and_the_iend_last_and_every_chunk_crc_is_right()
    {
        var bytes = PngWriter.Encode(new Image(40, 30, new Rgb(9, 9, 9)));

        Assert.True(bytes.AsSpan(0, 8).SequenceEqual(PngWriter.Signature));
        var types = PngDecoder.Chunks(bytes).Select(c => c.Type).ToArray();
        Assert.Equal(["IHDR", "IDAT", "IEND"], types);
        foreach (var chunk in PngDecoder.Chunks(bytes))
        {
            Assert.Equal(chunk.StoredCrc, Crc32.Update(chunk.Data, Crc32.Update(Encoding.ASCII.GetBytes(chunk.Type))));
        }
    }

    [Fact]
    public void The_inflated_data_is_one_filter_byte_and_the_row_for_each_row()
    {
        var bytes = PngWriter.Encode(new Image(13, 11, new Rgb(5, 6, 7)));
        var idat = PngDecoder.Chunks(bytes).Single(c => c.Type == "IDAT").Data;
        using var z = new ZLibStream(new MemoryStream(idat), CompressionMode.Decompress);
        using var inflated = new MemoryStream();
        z.CopyTo(inflated);

        Assert.Equal(11 * (1 + (13 * 3)), inflated.Length);
        var header = PngDecoder.Chunks(bytes).First().Data;
        Assert.Equal(13u, BinaryPrimitives.ReadUInt32BigEndian(header));
        Assert.Equal(11u, BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4)));
    }

    [Fact]
    public void The_same_image_gives_the_same_bytes()
    {
        var a = new Image(64, 64, new Rgb(3, 4, 5));
        for (var i = 0; i < 64; i++)
        {
            a.Set(i, i, new Rgb((byte)i, 0, 255));
        }

        Assert.Equal(PngWriter.Encode(a), PngWriter.Encode(a));
    }
}
