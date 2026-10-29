using System.Buffers.Binary;

namespace Vocaterra.Game.Tests.TestSupport;

/// <summary>
/// A minimal TrueType 'cmap' reader (formats 0, 4, 6 and 12 of the Unicode subtables), just enough to ask which code points a font maps to a
/// glyph. Written here so the test needs no font library.
/// </summary>
public sealed class TrueTypeCmap
{
    private readonly HashSet<int> _codePoints = [];

    private TrueTypeCmap(byte[] font)
    {
        var tableCount = U16(font, 4);
        var cmapOffset = -1;
        for (var i = 0; i < tableCount; i++)
        {
            var entry = 12 + (i * 16);
            if (System.Text.Encoding.ASCII.GetString(font, entry, 4) == "cmap")
            {
                cmapOffset = (int)U32(font, entry + 8);
                break;
            }
        }

        if (cmapOffset < 0)
        {
            throw new InvalidDataException("The font has no cmap table.");
        }

        var subtables = U16(font, cmapOffset + 2);
        for (var i = 0; i < subtables; i++)
        {
            var record = cmapOffset + 4 + (i * 8);
            var platform = U16(font, record);
            var encoding = U16(font, record + 2);
            var isUnicode = platform == 0 || (platform == 3 && encoding is 1 or 10);
            if (isUnicode)
            {
                ReadSubtable(font, cmapOffset + (int)U32(font, record + 4));
            }
        }
    }

    /// <summary>Reads the cmap of a TrueType file.</summary>
    public static TrueTypeCmap Load(string path) => new(File.ReadAllBytes(path));

    /// <summary>Whether the font maps <paramref name="codePoint"/> to a real glyph (glyph 0, .notdef, does not count).</summary>
    public bool Covers(int codePoint) => _codePoints.Contains(codePoint);

    private void ReadSubtable(byte[] font, int offset)
    {
        switch (U16(font, offset))
        {
            case 0:
                for (var c = 0; c < 256; c++)
                {
                    if (font[offset + 6 + c] != 0)
                    {
                        _codePoints.Add(c);
                    }
                }

                break;
            case 4:
                ReadFormat4(font, offset);
                break;
            case 6:
                var first = U16(font, offset + 6);
                var count = U16(font, offset + 8);
                for (var i = 0; i < count; i++)
                {
                    if (U16(font, offset + 10 + (i * 2)) != 0)
                    {
                        _codePoints.Add(first + i);
                    }
                }

                break;
            case 12:
                var groups = (int)U32(font, offset + 12);
                for (var g = 0; g < groups; g++)
                {
                    var at = offset + 16 + (g * 12);
                    var start = (int)U32(font, at);
                    var end = (int)U32(font, at + 4);
                    var glyph = (int)U32(font, at + 8);
                    for (var c = start; c <= end; c++)
                    {
                        if (glyph + (c - start) != 0)
                        {
                            _codePoints.Add(c);
                        }
                    }
                }

                break;
            default:
                break;
        }
    }

    private void ReadFormat4(byte[] font, int offset)
    {
        var segments = U16(font, offset + 6) / 2;
        var endCodes = offset + 14;
        var startCodes = endCodes + (segments * 2) + 2;
        var deltas = startCodes + (segments * 2);
        var rangeOffsets = deltas + (segments * 2);
        for (var s = 0; s < segments; s++)
        {
            var end = U16(font, endCodes + (s * 2));
            var start = U16(font, startCodes + (s * 2));
            var delta = (short)U16(font, deltas + (s * 2));
            var rangeOffset = U16(font, rangeOffsets + (s * 2));
            for (var c = start; c <= end && c != 0xFFFF; c++)
            {
                int glyph;
                if (rangeOffset == 0)
                {
                    glyph = (c + delta) & 0xFFFF;
                }
                else
                {
                    var at = rangeOffsets + (s * 2) + rangeOffset + ((c - start) * 2);
                    var raw = U16(font, at);
                    glyph = raw == 0 ? 0 : (raw + delta) & 0xFFFF;
                }

                if (glyph != 0)
                {
                    _codePoints.Add(c);
                }
            }
        }
    }

    private static int U16(byte[] data, int offset) => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset));

    private static uint U32(byte[] data, int offset) => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset));
}
