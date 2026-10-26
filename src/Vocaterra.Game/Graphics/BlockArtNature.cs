namespace Vocaterra.Game.Graphics;

/// <summary>
/// The pixel art of the natural cells of the block atlas (S14), drawn in code: grass, dirt, stone and its mossy and cobbled forms, snow, sand, the
/// gravel path, the bark and the rings of the oak, the spruce and the birch, planks, three kinds of leaves with cut-out holes, the plants of the
/// undergrowth and the water. Every pixel comes from <see cref="Mix"/> (an integer hash of the cell and the position) and a fixed palette, so a picture
/// is the same on every machine and on every run, and CC0 by construction (D-game-5). The palettes are bright and saturated (the brief's art
/// direction); the renderer's sun shading only darkens them.
/// </summary>
public static class BlockArtNature
{
    private const int Size = BlockAtlasLayout.CellSize;
    private static readonly object Gate = new();
    private static readonly byte[]?[] Cache = new byte[Enum.GetValues<BlockCell>().Length][];

    /// <summary>Whether this class draws the cell (the rest are drawn by <see cref="BlockArt"/>).</summary>
    public static bool Draws(BlockCell cell) => cell switch
    {
        BlockCell.GrassTop or BlockCell.GrassSide or BlockCell.Dirt or BlockCell.Stone or BlockCell.Sand or BlockCell.TrunkSide or BlockCell.TrunkEnd
            or BlockCell.Leaves or BlockCell.Bush or BlockCell.BerryBush or BlockCell.Water or BlockCell.PathTop or BlockCell.Planks or BlockCell.Snow
            or BlockCell.SnowSide or BlockCell.MossyStone or BlockCell.Cobble or BlockCell.SpruceBark or BlockCell.SpruceEnd or BlockCell.BirchBark
            or BlockCell.BirchEnd or BlockCell.SpruceLeaves or BlockCell.BirchLeaves or BlockCell.TallGrass or BlockCell.Fern or BlockCell.Reeds
            or BlockCell.YellowFlower or BlockCell.WhiteFlower or BlockCell.Waterfall or BlockCell.WaterShallow => true,
        _ => false,
    };

    /// <summary>The RGBA bytes (16 x 16 x 4, row by row, the top row first) of a cell this class draws.</summary>
    public static byte[] Pixels(BlockCell cell)
    {
        var index = (int)cell;
        lock (Gate)
        {
            return Cache[index] ??= Draw(cell);
        }
    }

    /// <summary>An integer hash of three values, so the art needs neither a random generator nor the clock.</summary>
    public static uint Mix(uint a, uint b, uint c)
    {
        var h = unchecked((a * 0x9E3779B1) ^ (b * 0x85EBCA6B) ^ (c * 0xC2B2AE35));
        h ^= h >> 15;
        h = unchecked(h * 0x2C1B3C6D);
        h ^= h >> 12;
        h = unchecked(h * 0x297A2D39);
        return h ^ (h >> 15);
    }

    private static byte[] Draw(BlockCell cell)
    {
        var c = new Canvas((uint)cell);
        switch (cell)
        {
            case BlockCell.GrassTop:
                Fill(c, Grass, 3, 3);
                break;
            case BlockCell.GrassSide:
                Fill(c, Dirt, 2, 3);
                Pebbles(c, 7, 14, 0xA1);
                Cap(c, Grass, 3, 3, 0x11);
                break;
            case BlockCell.Dirt:
                Fill(c, Dirt, 2, 3);
                Pebbles(c, 7, 15, 0xB1);
                break;
            case BlockCell.Stone:
                StoneBody(c);
                break;
            case BlockCell.MossyStone:
                StoneBody(c);
                Moss(c);
                break;
            case BlockCell.Cobble:
                CobbleBody(c);
                break;
            case BlockCell.Snow:
                Fill(c, SnowPal, 3, 5);
                break;
            case BlockCell.SnowSide:
                StoneBody(c);
                Cap(c, SnowPal, 4, 3, 0x21);
                break;
            case BlockCell.Sand:
                Fill(c, SandPal, 2, 5);
                break;
            case BlockCell.PathTop:
                Gravel(c);
                break;
            case BlockCell.TrunkSide:
                Bark(c, OakBark, 0x31, false);
                break;
            case BlockCell.TrunkEnd:
                Rings(c, OakBark, OakRings);
                break;
            case BlockCell.SpruceBark:
                Bark(c, SpruceBarkPal, 0x32, false);
                break;
            case BlockCell.SpruceEnd:
                Rings(c, SpruceBarkPal, SpruceRings);
                break;
            case BlockCell.BirchBark:
                Bark(c, BirchBarkPal, 0x33, true);
                break;
            case BlockCell.BirchEnd:
                Rings(c, BirchBarkPal, BirchRings);
                break;
            case BlockCell.Planks:
                Planks(c);
                break;
            case BlockCell.Leaves:
                Leaves(c, OakLeaves, 12, false);
                break;
            case BlockCell.SpruceLeaves:
                Leaves(c, SpruceLeavesPal, 12, true);
                break;
            case BlockCell.BirchLeaves:
                Leaves(c, BirchLeavesPal, 15, false);
                break;
            case BlockCell.Bush:
                Leaves(c, BushPal, 9, false);
                break;
            case BlockCell.BerryBush:
                Leaves(c, BushPal, 9, false);
                Berries(c);
                break;
            case BlockCell.TallGrass:
                TallGrass(c);
                break;
            case BlockCell.Fern:
                Fern(c);
                break;
            case BlockCell.Reeds:
                Reeds(c);
                break;
            case BlockCell.YellowFlower:
                Flowers(c, (255, 226, 38), (255, 150, 24));
                break;
            case BlockCell.WhiteFlower:
                Flowers(c, (252, 252, 255), (255, 208, 48));
                break;
            case BlockCell.Water:
                WaterTexture(c, deep: true);
                break;
            case BlockCell.WaterShallow:
                WaterTexture(c, deep: false);
                break;
            case BlockCell.Waterfall:
                Waterfall(c);
                break;
            default:
                break;
        }

        return c.Bytes;
    }

    // ---------------------------------------------------------------- palettes (dark to light)

    private static readonly (int R, int G, int B)[] Grass = [(60, 150, 38), (74, 170, 44), (88, 188, 52), (104, 204, 60), (132, 224, 80)];
    private static readonly (int R, int G, int B)[] Dirt = [(104, 68, 40), (122, 82, 48), (140, 96, 56), (158, 110, 66), (178, 128, 80)];
    private static readonly (int R, int G, int B)[] StonePal = [(98, 102, 112), (122, 126, 134), (146, 150, 158), (170, 174, 180), (194, 198, 204)];
    private static readonly (int R, int G, int B)[] SnowPal = [(196, 216, 244), (218, 234, 252), (236, 246, 255), (248, 252, 255), (255, 255, 255)];
    private static readonly (int R, int G, int B)[] SandPal = [(206, 176, 104), (224, 196, 120), (238, 214, 140), (248, 230, 164), (255, 242, 188)];
    private static readonly (int R, int G, int B)[] OakBark = [(60, 38, 20), (92, 60, 32), (118, 78, 42), (142, 98, 54), (166, 120, 68)];
    private static readonly (int R, int G, int B)[] OakRings = [(150, 104, 52), (184, 138, 76), (210, 166, 98), (228, 188, 120), (240, 206, 142)];
    private static readonly (int R, int G, int B)[] SpruceBarkPal = [(52, 38, 30), (76, 56, 42), (98, 74, 56), (120, 92, 70), (140, 110, 86)];
    private static readonly (int R, int G, int B)[] SpruceRings = [(140, 102, 64), (170, 128, 82), (196, 154, 102), (214, 176, 124), (228, 194, 144)];
    private static readonly (int R, int G, int B)[] BirchBarkPal = [(168, 164, 154), (196, 192, 182), (222, 218, 208), (240, 236, 226), (252, 250, 244)];
    private static readonly (int R, int G, int B)[] BirchRings = [(176, 150, 100), (200, 176, 124), (222, 200, 148), (238, 220, 172), (248, 234, 196)];
    private static readonly (int R, int G, int B)[] OakLeaves = [(24, 104, 32), (36, 136, 40), (52, 168, 48), (82, 198, 58), (122, 224, 78)];
    private static readonly (int R, int G, int B)[] SpruceLeavesPal = [(16, 70, 52), (24, 96, 62), (34, 124, 72), (54, 152, 90), (86, 180, 112)];
    private static readonly (int R, int G, int B)[] BirchLeavesPal = [(70, 140, 34), (94, 168, 40), (126, 196, 52), (164, 220, 70), (200, 236, 100)];
    private static readonly (int R, int G, int B)[] BushPal = [(28, 112, 36), (40, 144, 42), (58, 172, 50), (88, 200, 60), (124, 224, 80)];

    // ---------------------------------------------------------------- the picture buffer

    private sealed class Canvas(uint salt)
    {
        public byte[] Bytes { get; } = new byte[Size * Size * 4];

        public uint Salt { get; } = salt;

        public uint H(int x, int y, uint extra = 0) => Mix(Salt + (extra * 977), (uint)(x & 63), (uint)(y & 63));

        public void Set(int x, int y, (int R, int G, int B) colour, int alpha = 255)
        {
            if ((uint)x >= Size || (uint)y >= Size)
            {
                return;
            }

            var i = ((y * Size) + x) * 4;
            Bytes[i] = (byte)Math.Clamp(colour.R, 0, 255);
            Bytes[i + 1] = (byte)Math.Clamp(colour.G, 0, 255);
            Bytes[i + 2] = (byte)Math.Clamp(colour.B, 0, 255);
            Bytes[i + 3] = (byte)alpha;
        }

        public (int R, int G, int B, int A) Get(int x, int y)
        {
            if ((uint)x >= Size || (uint)y >= Size)
            {
                return (0, 0, 0, 0);
            }

            var i = ((y * Size) + x) * 4;
            return (Bytes[i], Bytes[i + 1], Bytes[i + 2], Bytes[i + 3]);
        }

        public bool Solid(int x, int y) => Get(x, y).A > 0;
    }

    private static (int R, int G, int B) Lerp((int R, int G, int B) a, (int R, int G, int B) b, int t, int of) =>
        (a.R + ((b.R - a.R) * t / of), a.G + ((b.G - a.G) * t / of), a.B + ((b.B - a.B) * t / of));

    // ---------------------------------------------------------------- noise textures

    // Clustered speckle: a coarse blotch (blobs of `blob` pixels) mixed with per-pixel grain picks a tone of the palette; a rare light and dark pixel
    // keep it lively. Tileable (the hash wraps at the cell size).
    private static void Fill(Canvas c, (int R, int G, int B)[] palette, int blob, int tones)
    {
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var coarse = (int)(c.H(x / blob, y / blob, 1) % 100);
                var fine = (int)(c.H(x, y, 2) % 100);
                var v = ((coarse * 3) + (fine * 2)) / 5; // 0 to 99, centred
                var tone = Math.Clamp(((v * tones) / 100) + (palette.Length - tones) / 2, 0, palette.Length - 1);
                var rare = c.H(x, y, 3) % 19;
                if (rare == 0)
                {
                    tone = Math.Min(palette.Length - 1, tone + 1);
                }
                else if (rare == 1)
                {
                    tone = Math.Max(0, tone - 1);
                }

                c.Set(x, y, palette[tone]);
            }
        }
    }

    // Small darker stones in a soil texture.
    private static void Pebbles(Canvas c, int count, int range, uint salt)
    {
        for (var i = 0; i < count; i++)
        {
            var x = (int)(c.H(i, 5, salt) % Size);
            var y = (int)(c.H(i, 6, salt) % Size);
            if (range == 14 && y < 5)
            {
                continue; // under the grass of a side
            }

            var tone = c.H(i, 7, salt) % 3 == 0 ? (176, 160, 140) : (80, 56, 40);
            c.Set(x, y, tone);
            if (c.H(i, 8, salt) % 2 == 0)
            {
                c.Set(x + 1, y, Dirt[0]);
            }
        }
    }

    // A cap over the top of a side texture (grass over dirt, snow over stone) with a ragged lower edge and a darker lip.
    private static void Cap(Canvas c, (int R, int G, int B)[] palette, int min, int spread, uint salt)
    {
        for (var x = 0; x < Size; x++)
        {
            var hang = min + (int)(c.H(x, 0, salt) % (uint)(spread + 1));
            if (c.H(x, 1, salt) % 5 == 0)
            {
                hang++;
            }

            for (var y = 0; y <= hang && y < Size; y++)
            {
                var coarse = (int)(c.H(x / 3, y / 2, salt + 1) % 100);
                var tone = Math.Clamp((coarse * 4 / 100) + 1, 0, palette.Length - 1);
                if (y == hang)
                {
                    tone = Math.Max(0, tone - 2); // the shaded lip
                }
                else if (y == 0)
                {
                    tone = Math.Min(palette.Length - 1, tone + 1);
                }

                c.Set(x, y, palette[tone]);
            }
        }
    }

    private static void StoneBody(Canvas c)
    {
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var blotch = (int)(c.H(x / 4, y / 4, 1) % 100);
                var fine = (int)(c.H(x, y, 2) % 100);
                var v = ((blotch * 2) + (fine * 3)) / 5;
                var tone = v < 12 ? 0 : v < 38 ? 1 : v < 72 ? 2 : v < 92 ? 3 : 4;
                c.Set(x, y, StonePal[Math.Clamp(tone, 0, 4)]);
            }
        }

        // A crack with a light edge, so the block reads as rock.
        (int X, int Y)[] crack = [(3, 3), (4, 4), (5, 4), (5, 5), (6, 6), (7, 6), (8, 7), (8, 8), (9, 9), (10, 9), (11, 10)];
        foreach (var (x, y) in crack)
        {
            c.Set(x, y, StonePal[0]);
            c.Set(x, y + 1, StonePal[1]);
            c.Set(x - 1, y, StonePal[3]);
        }
    }

    private static void Moss(Canvas c)
    {
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var patch = (int)(c.H(x / 3, y / 3, 9) % 100);
                var edge = (int)(c.H(x, y, 10) % 100);
                if (patch + (edge / 4) > 68)
                {
                    var tone = edge < 30 ? 1 : edge < 70 ? 2 : 3;
                    c.Set(x, y, Grass[tone]);
                }
            }
        }
    }

    private static void CobbleBody(Canvas c)
    {
        var mortar = (74, 80, 94);
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var row = y / 4;
                var shifted = x + ((row % 2) * 3);
                var stone = (shifted / 5) + (row * 7);
                var inMortar = y % 4 == 3 || shifted % 5 == 4;
                if (inMortar)
                {
                    c.Set(x, y, mortar);
                    continue;
                }

                var tone = 1 + (int)(c.H(stone, 3, 4) % 3);
                var lightEdge = y % 4 == 0 || shifted % 5 == 0;
                var darkEdge = y % 4 == 2 || shifted % 5 == 3;
                tone += lightEdge ? 1 : darkEdge ? -1 : 0;
                if (c.H(x, y, 5) % 9 == 0)
                {
                    tone -= 1;
                }

                c.Set(x, y, StonePal[Math.Clamp(tone, 0, 4)]);
            }
        }
    }

    private static void Gravel(Canvas c)
    {
        (int R, int G, int B)[] pebble = [(176, 154, 120), (190, 168, 132), (202, 182, 146), (216, 198, 162), (150, 128, 98)];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var id = c.H(x / 2, y / 2, 1);
                var tone = (int)(id % 4);
                var inner = x % 2 == 1 && y % 2 == 1; // the shaded corner of every pebble
                var gap = c.H(x / 2, y / 2, 2) % 11 == 0 && (x + y) % 2 == 0;
                c.Set(x, y, gap ? pebble[4] : inner ? pebble[Math.Max(0, tone - 1)] : pebble[tone]);
            }
        }
    }

    // ---------------------------------------------------------------- wood

    private static void Bark(Canvas c, (int R, int G, int B)[] palette, uint salt, bool birch)
    {
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                // Vertical ridges: each column has its own tone, broken up every few rows.
                var column = (int)(c.H(x, y / 5, salt) % 100);
                var groove = c.H(x, 0, salt + 1) % 4 == 0;
                var tone = groove ? 0 : column < 25 ? 1 : column < 65 ? 2 : column < 90 ? 3 : 4;
                if (c.H(x, y, salt + 2) % 11 == 0)
                {
                    tone = Math.Max(0, tone - 1);
                }

                c.Set(x, y, palette[Math.Clamp(tone, 0, 4)]);
            }
        }

        if (birch)
        {
            // The dark horizontal scars of a birch.
            for (var i = 0; i < 9; i++)
            {
                var y = (int)(c.H(i, 1, 0x70) % Size);
                var x = (int)(c.H(i, 2, 0x70) % 12);
                var len = 2 + (int)(c.H(i, 3, 0x70) % 5);
                for (var k = 0; k < len; k++)
                {
                    c.Set(x + k, y, (44, 40, 36));
                    if (k % 2 == 0)
                    {
                        c.Set(x + k, y + 1, (96, 90, 82));
                    }
                }
            }
        }
        else
        {
            // A knot.
            var kx = 3 + (int)(c.H(1, 1, 0x71) % 9);
            var ky = 4 + (int)(c.H(2, 1, 0x71) % 8);
            c.Set(kx, ky, palette[0]);
            c.Set(kx + 1, ky, palette[1]);
            c.Set(kx, ky + 1, palette[1]);
            c.Set(kx - 1, ky, palette[3]);
        }
    }

    private static void Rings(Canvas c, (int R, int G, int B)[] bark, (int R, int G, int B)[] rings)
    {
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var dx = (2 * x) - 15;
                var dy = (2 * y) - 15;
                var d2 = (dx * dx) + (dy * dy);
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) >= 13)
                {
                    var tone = 1 + (int)(c.H(x, y, 3) % 3);
                    c.Set(x, y, bark[tone]); // the bark rim
                    continue;
                }

                var ring = (int)(Math.Sqrt(d2) / 2.6);
                var tone2 = ring % 2 == 0 ? 3 : 1;
                if (d2 < 10)
                {
                    tone2 = 0;
                }

                tone2 += (int)(c.H(x, y, 4) % 3) - 1;
                c.Set(x, y, rings[Math.Clamp(tone2, 0, 4)]);
            }
        }
    }

    private static void Planks(Canvas c)
    {
        (int R, int G, int B)[] wood = [(150, 98, 50), (176, 120, 62), (198, 142, 78), (218, 164, 96), (232, 184, 116)];
        for (var y = 0; y < Size; y++)
        {
            var plank = y / 4;
            var offset = (int)(c.H(plank, 1, 0x80) % 16);
            for (var x = 0; x < Size; x++)
            {
                if (y % 4 == 3)
                {
                    c.Set(x, y, (112, 70, 34));
                    continue;
                }

                var joint = (x + offset) % 16 == 0;
                var grain = (int)(c.H((x + offset) / 3, plank, 0x81) % 3);
                var tone = 2 + grain - 1 + (y % 4 == 0 ? 1 : 0);
                if (c.H(x, y, 0x82) % 13 == 0)
                {
                    tone--;
                }

                c.Set(x, y, joint ? (118, 74, 36) : wood[Math.Clamp(tone, 0, 4)]);
            }
        }

        for (var plank = 0; plank < 4; plank++)
        {
            var offset = (int)(c.H(plank, 1, 0x80) % 16);
            c.Set((16 - offset) % 16 + 1 > 15 ? 14 : (16 - offset) % 16 + 1, (plank * 4) + 1, (84, 52, 26)); // a nail beside the joint
        }
    }

    // ---------------------------------------------------------------- leaves and plants (cut-out)

    private static void Leaves(Canvas c, (int R, int G, int B)[] palette, int holePercent, bool needles)
    {
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var coarse = (int)(c.H(x / 2, y / 2, 1) % 100);
                var fine = (int)(c.H(x, y, 2) % 100);
                var v = ((coarse * 3) + (fine * 2)) / 5;
                var tone = v < 18 ? 0 : v < 45 ? 1 : v < 75 ? 2 : v < 93 ? 3 : 4;
                if (needles && (x + y) % 4 == 0 && c.H(x, y, 3) % 3 != 0)
                {
                    tone = Math.Min(4, tone + 1); // slanted needle strokes
                }

                c.Set(x, y, palette[tone]);
            }
        }

        // Cut-out holes: short horizontal gaps (never a single stray pixel), so the canopy lets the sky through in specks.
        var holes = new bool[Size * Size];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (c.H(x, y, 9) % 100 < (uint)holePercent / 2)
                {
                    holes[(y * Size) + x] = true;
                    if (c.H(x, y, 10) % 2 == 0)
                    {
                        holes[(y * Size) + ((x + 1) % Size)] = true;
                    }
                    else
                    {
                        holes[((((y + 1) % Size)) * Size) + x] = true;
                    }
                }
            }
        }

        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (holes[(y * Size) + x])
                {
                    c.Set(x, y, (0, 0, 0), 0);
                }
            }
        }

        // Darken the rim of every hole, so the leaves keep their body.
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (!c.Solid(x, y))
                {
                    continue;
                }

                if (!c.Solid(x - 1, y) && x > 0 || !c.Solid(x, y - 1) && y > 0)
                {
                    var (r, g, b, _) = c.Get(x, y);
                    c.Set(x, y, ((r * 3) / 4, (g * 3) / 4, (b * 3) / 4));
                }
            }
        }
    }

    private static void Berries(Canvas c)
    {
        (int X, int Y)[] spots = [(3, 3), (10, 2), (7, 8), (2, 11), (12, 10), (6, 13)];
        foreach (var (x, y) in spots)
        {
            c.Set(x, y, (232, 34, 52));
            c.Set(x + 1, y, (200, 20, 40));
            c.Set(x, y + 1, (200, 20, 40));
            c.Set(x + 1, y + 1, (170, 14, 34));
            c.Set(x, y, (255, 120, 120));
        }
    }

    private static void TallGrass(Canvas c)
    {
        int[] columns = [0, 2, 3, 5, 6, 8, 9, 11, 12, 14];
        var index = 0;
        foreach (var x0 in columns)
        {
            var seed = c.H(x0, 1, 0x90);
            var height = 8 + (int)(seed % 8); // 8 to 15 pixels
            var lean = (int)((seed >> 8) % 3) - 1; // -1, 0 or 1
            for (var k = 0; k < height; k++)
            {
                var y = Size - 1 - k;
                var x = x0 + (k > height / 2 ? lean : 0);
                var colour = Lerp((44, 128, 36), (150, 232, 84), k, Math.Max(1, height - 1));
                c.Set(x, y, colour);
                if (k < height / 2 && (index % 2 == 0))
                {
                    c.Set(x + 1, y, Lerp((38, 112, 30), (112, 200, 66), k, height));
                }
            }

            index++;
        }
    }

    private static void Fern(Canvas c)
    {
        // Three arching fronds from the root: a stem that curves, with leaflets on both sides that shrink towards the tip.
        (int Tx, int Ty)[] tips = [(2, 4), (8, 1), (13, 4)];
        foreach (var (tx, ty) in tips)
        {
            var sx = 8;
            var sy = 15;
            var steps = Math.Max(Math.Abs(tx - sx), Math.Abs(ty - sy));
            for (var i = 0; i <= steps; i++)
            {
                var x = sx + ((tx - sx) * i / steps);
                var y = sy + ((ty - sy) * i / steps);
                var bow = (i * (steps - i)) / Math.Max(1, steps) / 2; // the stem arches
                var px = x + (tx < sx ? -bow : tx > sx ? bow : 0);
                c.Set(px, y, (40, 118, 40));
                var reach = Math.Min(3, 1 + (steps - i) / 3);
                if (i > 2 && i % 2 == 1)
                {
                    for (var k = 1; k <= reach; k++)
                    {
                        c.Set(px - k, y + (k / 2), Lerp((50, 150, 46), (110, 216, 74), k, 3));
                        c.Set(px + k, y + (k / 2), Lerp((50, 150, 46), (110, 216, 74), k, 3));
                    }
                }
            }

            c.Set(tx, ty, (130, 232, 90));
        }
    }

    private static void Reeds(Canvas c)
    {
        // Slim stems with a pair of cattails and a few long leaf blades.
        int[] stems = [2, 5, 8, 11, 14];
        foreach (var x in stems)
        {
            var seed = c.H(x, 2, 0xA0);
            var top = 0 + (int)(seed % 4);
            for (var y = top; y < Size; y++)
            {
                c.Set(x, y, Lerp((150, 206, 92), (66, 148, 52), y - top, Size - top));
            }

            if (seed % 3 == 0)
            {
                // The cattail head, a dark brown cylinder on the stem with a thin spike on top.
                c.Set(x, top, (150, 206, 92));
                c.Set(x, top + 1, (132, 84, 44));
                c.Set(x + 1, top + 1, (112, 70, 38));
                c.Set(x, top + 2, (112, 70, 38));
                c.Set(x + 1, top + 2, (84, 52, 28));
                c.Set(x, top + 3, (112, 70, 38));
                c.Set(x + 1, top + 3, (84, 52, 28));
                c.Set(x, top + 4, (126, 80, 42));
            }
        }

        // Blades leaning outwards at the root.
        (int X, int Y, int Dx)[] blades = [(3, 10, -1), (6, 9, 1), (9, 11, -1), (12, 10, 1), (0, 12, 1), (15, 12, -1)];
        foreach (var (x, y, dx) in blades)
        {
            for (var k = 0; k < 5; k++)
            {
                c.Set(x + (dx * (k / 2)), y + k, Lerp((120, 200, 72), (60, 140, 48), k, 5));
            }
        }
    }

    private static void Flowers(Canvas c, (int R, int G, int B) petal, (int R, int G, int B) centre)
    {
        // A few grass blades at the foot, then two stalks with a bloom each.
        (int X, int Y, int Size)[] blooms = [(5, 5, 2), (11, 8, 2)];
        for (var x = 1; x < 15; x += 2)
        {
            var h = 3 + (int)(c.H(x, 1, 0xB0) % 4);
            for (var k = 0; k < h; k++)
            {
                c.Set(x, Size - 1 - k, Lerp((46, 134, 38), (110, 210, 70), k, h));
            }
        }

        foreach (var (bx, by, _) in blooms)
        {
            for (var y = by + 2; y < Size; y++)
            {
                c.Set(bx, y, Lerp((98, 190, 62), (46, 130, 40), y - by, Size - by));
            }

            c.Set(bx - 1, by + 5, (70, 160, 52));
            c.Set(bx + 1, by + 6, (70, 160, 52));
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    c.Set(bx + dx, by + dy, petal);
                }
            }

            c.Set(bx, by - 2, petal);
            c.Set(bx, by + 2, petal);
            c.Set(bx - 2, by, petal);
            c.Set(bx + 2, by, petal);
            c.Set(bx, by, centre);
            c.Set(bx + 1, by + 1, ShadeOf(petal));
        }
    }

    private static (int R, int G, int B) ShadeOf((int R, int G, int B) colour) => (colour.R * 4 / 5, colour.G * 4 / 5, colour.B * 4 / 5);

    // ---------------------------------------------------------------- water

    private static void WaterTexture(Canvas c, bool deep)
    {
        (int R, int G, int B) body = deep ? (28, 110, 224) : (70, 188, 236);
        (int R, int G, int B) dark = deep ? (20, 88, 200) : (52, 160, 222);
        (int R, int G, int B) light = deep ? (84, 168, 255) : (150, 226, 255);
        var alpha = deep ? 205 : 170;
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var wave = ((x * 2) + (y * 3) + (int)(c.H(x / 4, y / 4, 1) % 4)) % 11;
                var crest = wave == 0 || (wave == 1 && c.H(x, y, 2) % 2 == 0);
                var trough = (x + (y * 5)) % 13 == 0;
                var colour = crest ? light : trough ? dark : body;
                c.Set(x, y, colour, crest ? alpha + 30 : alpha);
            }
        }
    }

    private static void Waterfall(Canvas c)
    {
        // Falling water: vertical streaks of foam and clear water, offset column by column.
        for (var x = 0; x < Size; x++)
        {
            var offset = (int)(c.H(x, 1, 0xC0) % 8);
            var kind = (int)(c.H(x, 2, 0xC0) % 3);
            for (var y = 0; y < Size; y++)
            {
                var phase = (y + offset) % 8;
                (int R, int G, int B) colour;
                int alpha;
                if (phase < 3)
                {
                    colour = kind == 0 ? (252, 254, 255) : (214, 238, 255);
                    alpha = 245;
                }
                else if (phase < 6)
                {
                    colour = (120, 196, 255);
                    alpha = 220;
                }
                else
                {
                    colour = (70, 156, 240);
                    alpha = 205;
                }

                if (c.H(x, y, 3) % 9 == 0)
                {
                    colour = (255, 255, 255);
                    alpha = 250;
                }

                c.Set(x, y, colour, alpha);
            }
        }
    }
}
