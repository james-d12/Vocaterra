using System.IO.Compression;
using Vocaterra.Game.Graphics.Blocks;

namespace Vocaterra.Game.Tests.Graphics.Blocks;

/// <summary>
/// Draws the underground tile sheet (B10, B10b) at 8x as a PNG, without a graphics device, when VOCATERRA_ORE_SHEET names a file; otherwise it does nothing.
/// Top row: stone, cobble, water, slate, granite, limestone. Bottom row: gravel, lava, bedrock, iron ore, tin ore, copper ore.
/// </summary>
public sealed class OreSheetTests
{
    private const int Scale = 8;
    private const int Gap = 8;
    private static readonly BlockCell[] Cells =
    [
        BlockCell.Stone, BlockCell.Cobble, BlockCell.Water, BlockCell.Slate, BlockCell.Granite, BlockCell.Limestone,
        BlockCell.Gravel, BlockCell.Lava, BlockCell.Bedrock, BlockCell.IronOre, BlockCell.TinOre, BlockCell.CopperOre,
    ];

    [Fact]
    public void WritesTheSheetWhenAsked()
    {
        var path = Environment.GetEnvironmentVariable("VOCATERRA_ORE_SHEET");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        const int Cell = BlockAtlasLayout.CellSize;
        var width = (6 * ((Cell * Scale) + Gap)) + Gap;
        var height = (2 * ((Cell * Scale) + Gap)) + Gap;
        var rgb = new byte[width * height * 3];
        Array.Fill(rgb, (byte)30);
        for (var i = 0; i < Cells.Length; i++)
        {
            var art = BlockArt.Pixels(Cells[i]);
            var ox = Gap + ((i % 6) * ((Cell * Scale) + Gap));
            var oy = Gap + ((i / 6) * ((Cell * Scale) + Gap));
            for (var y = 0; y < Cell * Scale; y++)
            {
                for (var x = 0; x < Cell * Scale; x++)
                {
                    var s = (((y / Scale) * Cell) + (x / Scale)) * 4;
                    var a = art[s + 3] / 255f;
                    var d = (((oy + y) * width) + ox + x) * 3;
                    rgb[d] = (byte)((art[s] * a) + (20 * (1 - a)));
                    rgb[d + 1] = (byte)((art[s + 1] * a) + (24 * (1 - a)));
                    rgb[d + 2] = (byte)((art[s + 2] * a) + (40 * (1 - a)));
                }
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, Png(width, height, rgb));
    }

    private static byte[] Png(int width, int height, byte[] rgb)
    {
        using var raw = new MemoryStream();
        for (var y = 0; y < height; y++)
        {
            raw.WriteByte(0);
            raw.Write(rgb, y * width * 3, width * 3);
        }

        using var z = new MemoryStream();
        using (var deflate = new ZLibStream(z, CompressionLevel.Optimal, true))
        {
            deflate.Write(raw.ToArray());
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        WriteInt(header, 0, width);
        WriteInt(header, 4, height);
        header[8] = 8;
        header[9] = 2;
        Chunk(png, "IHDR", header);
        Chunk(png, "IDAT", z.ToArray());
        Chunk(png, "IEND", []);
        return png.ToArray();
    }

    private static void WriteInt(byte[] b, int at, int v)
    {
        b[at] = (byte)(v >> 24);
        b[at + 1] = (byte)(v >> 16);
        b[at + 2] = (byte)(v >> 8);
        b[at + 3] = (byte)v;
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var length = new byte[4];
        WriteInt(length, 0, data.Length);
        s.Write(length);
        var body = new byte[4 + data.Length];
        for (var i = 0; i < 4; i++)
        {
            body[i] = (byte)type[i];
        }

        data.CopyTo(body, 4);
        s.Write(body);
        var crc = 0xFFFFFFFFu;
        foreach (var b in body)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        var tail = new byte[4];
        WriteInt(tail, 0, (int)~crc);
        s.Write(tail);
    }
}
