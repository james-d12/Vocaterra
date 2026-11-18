namespace Vocaterra.WorldPreview;

/// <summary>The CRC-32 of PNG (the IEEE polynomial, reflected), written by hand because the repository takes no package for it.</summary>
internal static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    /// <summary>The CRC-32 of the bytes, continuing from <paramref name="crc"/> (the CRC of what came before, 0 at the start).</summary>
    /// <param name="data">The bytes.</param>
    /// <param name="crc">The CRC so far.</param>
    public static uint Update(ReadOnlySpan<byte> data, uint crc = 0)
    {
        var c = ~crc;
        foreach (var b in data)
        {
            c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
        }

        return ~c;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
