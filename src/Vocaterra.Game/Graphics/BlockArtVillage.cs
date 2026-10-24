namespace Vocaterra.Game.Graphics;

/// <summary>
/// The pixel art of the village's cells (S15), drawn in code like the rest of the atlas (D-game-5: CC0 by construction, no picture file): the timber, the
/// lantern, the crates and the chest, the awning's stripes, the tent's canvas, the crop field, the stall's goods, the signpost's boards with their words, the
/// faces and cloth of the figures and the torch's icon. Every pixel comes from <see cref="BlockArtNature.Mix"/> and a fixed palette, so the picture is the
/// same on every run. The signpost's words are drawn with a 3 x 5 pixel font (<see cref="Glyph"/>), accents included.
/// </summary>
public static class BlockArtVillage
{
    private const int Size = BlockAtlasLayout.CellSize;
    private static readonly object Gate = new();
    private static readonly byte[]?[] Cache = new byte[Enum.GetValues<BlockCell>().Length][];

    /// <summary>The colour of a pixel, red, green, blue and alpha.</summary>
    private readonly record struct Rgba(byte R, byte G, byte B, byte A = 255);

    private static readonly Rgba Clear = new(0, 0, 0, 0);

    /// <summary>Whether this class draws the cell.</summary>
    public static bool Draws(BlockCell cell) => cell switch
    {
        BlockCell.TimberSide or BlockCell.TimberEnd or BlockCell.Lantern or BlockCell.CrateSide or BlockCell.CrateTop or BlockCell.AwningRed
            or BlockCell.AwningWhite or BlockCell.Canvas or BlockCell.ChestSide or BlockCell.ChestTop or BlockCell.FarmlandTop or BlockCell.Wheat
            or BlockCell.Loaves or BlockCell.Apples or BlockCell.Bottles or BlockCell.SignTiendaA or BlockCell.SignTiendaB or BlockCell.SignRio
            or BlockCell.SignMontanaA or BlockCell.SignMontanaB or BlockCell.SignBoard or BlockCell.FaceGuide or BlockCell.FaceShopkeeper
            or BlockCell.FaceVillager or BlockCell.FacePlayer or BlockCell.Cloth or BlockCell.Skin or BlockCell.Hair or BlockCell.Backpack
            or BlockCell.IconTorch or BlockCell.SignBlank => true,
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

    /// <summary>
    /// The 3 x 5 pixel font of the signs, one string of three characters per row (<c>#</c> is ink), or <see langword="null"/> for a character it does not
    /// know. Upper-case letters of the three place names, an arrow to the left (<c>&lt;</c>) and one up (<c>^</c>).
    /// </summary>
    public static string[]? Glyph(char c) => c switch
    {
        'T' => ["###", ".#.", ".#.", ".#.", ".#."],
        'I' => ["###", ".#.", ".#.", ".#.", "###"],
        'E' => ["###", "#..", "##.", "#..", "###"],
        'N' => ["##.", "#.#", "#.#", "#.#", "#.#"],
        'D' => ["##.", "#.#", "#.#", "#.#", "##."],
        'A' => [".#.", "#.#", "###", "#.#", "#.#"],
        'R' => ["##.", "#.#", "##.", "#.#", "#.#"],
        'O' => ["###", "#.#", "#.#", "#.#", "###"],
        'M' => ["#.#", "###", "###", "#.#", "#.#"],
        '<' => [".#.", "##.", "###", "##.", ".#."],
        '^' => [".#.", "###", ".#.", ".#.", ".#."],
        _ => null,
    };

    private static byte[] Draw(BlockCell cell)
    {
        var data = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                var p = Pixel(cell, x, y);
                var i = ((y * Size) + x) * 4;
                data[i] = p.R;
                data[i + 1] = p.G;
                data[i + 2] = p.B;
                data[i + 3] = p.A;
            }
        }

        return data;
    }

    private static Rgba Jit(int r, int g, int b, uint n, int amount = 3)
    {
        var d = ((int)(n % 7) - 3) * amount;
        return new Rgba(Clamp(r + d), Clamp(g + d), Clamp(b + d));
    }

    private static byte Clamp(int v) => (byte)Math.Clamp(v, 0, 255);

    private static Rgba Pixel(BlockCell cell, int x, int y)
    {
        var n = BlockArtNature.Mix((uint)cell + 900, (uint)x, (uint)y);
        switch (cell)
        {
            case BlockCell.TimberSide:
                // A squared beam: vertical grain, a darker edge on each side.
                if (x is 0 or 15)
                {
                    return Jit(96, 60, 30, n);
                }

                return x % 4 == 2 ? Jit(128, 84, 42, n) : Jit(168, 114, 60, n);
            case BlockCell.TimberEnd:
                {
                    var ring = Math.Max(Math.Abs((x * 2) - 15), Math.Abs((y * 2) - 15)) / 2;
                    return ring % 3 == 0 ? Jit(130, 86, 44, n) : Jit(190, 140, 80, n);
                }

            case BlockCell.Lantern:
                {
                    // A cage: dark caps at the top and the bottom, dark posts at the sides, and a flame of light behind the glass, white-hot in the middle.
                    if (y is <= 1 or >= 14 || x is <= 1 or >= 14)
                    {
                        return new Rgba(58, 44, 34);
                    }

                    var d = Math.Abs(x - 7.5f) + (Math.Abs(y - 8f) * 0.8f);
                    return d < 3.2f ? new Rgba(255, 250, 214) : d < 6.2f ? new Rgba(255, 214, 96) : new Rgba(242, 160, 48);
                }

            case BlockCell.CrateSide:
                {
                    if (x is 0 or 15 || y is 0 or 15)
                    {
                        return Jit(96, 60, 28, n);
                    }

                    if (x == y || x + y == 15)
                    {
                        return Jit(120, 78, 38, n); // the cross brace
                    }

                    return y % 5 == 4 ? Jit(130, 86, 44, n) : Jit(178, 124, 66, n);
                }

            case BlockCell.CrateTop:
                if (x is 0 or 15 || y is 0 or 15)
                {
                    return Jit(96, 60, 28, n);
                }

                return y % 5 == 4 ? Jit(130, 86, 44, n) : Jit(190, 136, 74, n);
            case BlockCell.AwningRed:
                {
                    var weave = (x + y) % 2 == 0;
                    return y % 8 == 7 ? Jit(150, 30, 30, n, 2) : weave ? Jit(222, 58, 50, n, 2) : Jit(206, 46, 42, n, 2);
                }

            case BlockCell.AwningWhite:
                {
                    var weave = (x + y) % 2 == 0;
                    return y % 8 == 7 ? Jit(206, 200, 188, n, 2) : weave ? Jit(250, 246, 236, n, 2) : Jit(238, 232, 220, n, 2);
                }

            case BlockCell.Canvas:
                {
                    var seam = y % 8 == 7 || x % 8 == 0;
                    return seam ? Jit(168, 150, 108, n, 2) : Jit(224, 208, 164, n, 3);
                }

            case BlockCell.ChestSide:
                {
                    var band = y is <= 2 or >= 13;
                    if (band)
                    {
                        return Jit(52, 52, 62, n, 2);
                    }

                    if (x is 7 or 8 && y is >= 4 and <= 9)
                    {
                        return new Rgba(190, 192, 200); // the latch
                    }

                    if (x is 0 or 15)
                    {
                        return Jit(96, 60, 28, n);
                    }

                    return y % 4 == 3 ? Jit(128, 84, 42, n) : Jit(176, 118, 56, n);
                }

            case BlockCell.ChestTop:
                if (x is 0 or 15 || y is 0 or 15 || x is 3 or 12 && y is >= 1 and <= 14)
                {
                    return Jit(56, 56, 66, n, 2);
                }

                return x % 5 == 4 ? Jit(128, 84, 42, n) : Jit(186, 128, 62, n);
            case BlockCell.FarmlandTop:
                return y % 4 == 0 ? Jit(70, 44, 26, n) : Jit(104, 68, 40, n);
            case BlockCell.Wheat:
                return WheatPixel(x, y, n);
            case BlockCell.Loaves:
                return LoavesPixel(x, y, n);
            case BlockCell.Apples:
                return ApplesPixel(x, y, n);
            case BlockCell.Bottles:
                return BottlesPixel(x, y, n);
            case BlockCell.SignBoard:
                {
                    // The post column of the signpost: the post runs through the middle, the board's plank is cut at its top and bottom.
                    if (x is >= 6 and <= 9)
                    {
                        return x == 6 ? Jit(98, 62, 30, n) : Jit(150, 100, 52, n);
                    }

                    return BoardPixel(x, y, n);
                }

            case BlockCell.SignBlank:
                return BoardPixel(x, y, n);
            case BlockCell.SignTiendaA:
                return SignPixel("<TIE", x, y, n);
            case BlockCell.SignTiendaB:
                return SignPixel("NDA", x, y, n);
            case BlockCell.SignRio:
                return SignPixel("<RIO", x, y, n, accentOn: 2);
            case BlockCell.SignMontanaA:
                return SignPixel("^MON", x, y, n);
            case BlockCell.SignMontanaB:
                return SignPixel("TANA", x, y, n, tildeOn: 2);
            case BlockCell.FaceGuide:
                return FacePixel(x, y, n, hair: new Rgba(78, 48, 28), longHair: true, beard: false, cheeks: true, eyes: new Rgba(40, 80, 60));
            case BlockCell.FaceShopkeeper:
                return FacePixel(x, y, n, hair: new Rgba(60, 40, 26), longHair: false, beard: true, cheeks: false, eyes: new Rgba(50, 36, 28));
            case BlockCell.FaceVillager:
                return FacePixel(x, y, n, hair: new Rgba(176, 120, 60), longHair: false, beard: false, cheeks: true, eyes: new Rgba(40, 60, 110));
            case BlockCell.FacePlayer:
                return FacePixel(x, y, n, hair: new Rgba(92, 56, 34), longHair: false, beard: false, cheeks: false, eyes: new Rgba(36, 36, 48));
            case BlockCell.Cloth:
                {
                    var weave = (x + y) % 2 == 0 ? 0 : -9;
                    var fold = y % 8 == 7 ? -16 : 0;
                    var v = 244 + weave + fold + (int)(n % 5) - 2;
                    return new Rgba(Clamp(v), Clamp(v), Clamp(v));
                }

            case BlockCell.Skin:
                {
                    var v = 244 + (int)(n % 5) - 2;
                    return new Rgba(Clamp(v), Clamp(v - 8), Clamp(v - 14));
                }

            case BlockCell.Hair:
                {
                    var streak = x % 3 == 1 ? -26 : 0;
                    var v = 232 + streak + (int)(n % 9) - 4;
                    return new Rgba(Clamp(v), Clamp(v), Clamp(v));
                }

            case BlockCell.Backpack:
                {
                    if (x is 0 or 15 || y is 0 or 15)
                    {
                        return Jit(84, 52, 28, n);
                    }

                    if (y == 7 || (y is 8 or 9 && x is 7 or 8))
                    {
                        return y == 7 ? Jit(70, 42, 22, n) : new Rgba(210, 196, 120); // the flap's edge and the buckle
                    }

                    return y < 7 ? Jit(140, 92, 50, n) : Jit(122, 78, 42, n);
                }

            case BlockCell.IconTorch:
                return TorchIconPixel(x, y, n);
            default:
                return Clear;
        }
    }

    // A board: a plank 10 pixels high with a darker edge, the rows above and below it clear (so stacked boards show the gap between them).
    private static Rgba BoardPixel(int x, int y, uint n)
    {
        if (y is < 3 or > 12)
        {
            return Clear;
        }

        if (y is 3 or 12)
        {
            return Jit(86, 54, 26, n, 2);
        }

        return y % 3 == 0 ? Jit(150, 100, 52, n, 2) : Jit(186, 132, 72, n, 2);
    }

    // A signpost board's cell: the plank with a text of up to four glyphs (an arrow or letters), three pixels wide each and one apart, ink dark brown.
    // accentOn: the index of the glyph that carries an acute accent (the í of Río); tildeOn: the index of the one with a tilde (the ñ of Montaña).
    private static Rgba SignPixel(string text, int x, int y, uint n, int accentOn = -1, int tildeOn = -1)
    {
        var board = BoardPixel(x, y, n);
        if (board.A == 0)
        {
            return board;
        }

        var ink = new Rgba(44, 26, 12);
        var index = x / 4;
        var col = x % 4;
        if (index < text.Length && col < 3)
        {
            var glyph = Glyph(text[index]);
            var row = y - 6;
            if (glyph is not null && row is >= 0 and < 5 && glyph[row][col] == '#')
            {
                return ink;
            }

            if (index == accentOn && ((y == 5 && col == 1) || (y == 4 && col == 2)))
            {
                return ink; // the acute accent
            }

            if (index == tildeOn && ((y == 5 && col == 0) || (y == 4 && col is 1 or 2)))
            {
                return ink; // the tilde
            }
        }

        return board;
    }

    private static Rgba WheatPixel(int x, int y, uint n)
    {
        // Five stalks with a golden ear on each, on clear ground: cut-out pixels, like the other plants.
        var stalk = x is 2 or 5 or 8 or 11 or 14;
        if (stalk && y >= 5)
        {
            return y % 4 == 0 ? Jit(120, 150, 52, n) : Jit(150, 168, 62, n);
        }

        var ear = y is >= 1 and <= 6 && (x % 3 == 2 || (x % 3 == 1 && y > 2));
        if (ear)
        {
            return y % 2 == 0 ? Jit(232, 190, 70, n) : Jit(212, 164, 52, n);
        }

        return Clear;
    }

    private static Rgba LoavesPixel(int x, int y, uint n)
    {
        // Two or three loaves on a cloth: rounded brown crusts with a lighter crown and a score, set on a pale cloth.
        bool Inside(float cx, float cy, float rx, float ry) => (((x - cx) * (x - cx)) / (rx * rx)) + (((y - cy) * (y - cy)) / (ry * ry)) <= 1f;
        foreach (var (cx, cy, rx, ry) in new[] { (4.5f, 9.5f, 4.5f, 3.5f), (11f, 8f, 4.5f, 3.5f), (8f, 12.5f, 4.5f, 2.8f) })
        {
            if (Inside(cx, cy, rx, ry))
            {
                if ((x + y) % 5 == 0 && Inside(cx, cy, rx - 1.5f, ry - 1.5f))
                {
                    return Jit(246, 214, 138, n, 2);
                }

                return Inside(cx, cy - 1, rx - 1f, ry - 1.4f) ? Jit(222, 156, 70, n, 2) : Jit(160, 100, 44, n, 2);
            }
        }

        return y < 5 ? Clear : (x + y) % 4 == 0 ? Jit(214, 206, 188, n, 2) : Jit(238, 230, 212, n, 2);
    }

    private static Rgba ApplesPixel(int x, int y, uint n)
    {
        foreach (var (cx, cy) in new[] { (4f, 11f), (11f, 11f), (7.5f, 7f), (2.5f, 6f), (13f, 6.5f) })
        {
            var d2 = ((x - cx) * (x - cx)) + ((y - cy) * (y - cy));
            if (d2 <= 7.5f)
            {
                if (x - cx <= -0.9f && y - cy <= -0.9f)
                {
                    return new Rgba(255, 140, 120); // the highlight
                }

                return d2 > 5.2f ? Jit(150, 20, 24, n, 2) : Jit(214, 38, 38, n, 2);
            }

            if (Math.Abs(x - cx) < 0.6f && y - cy is >= -3.4f and <= -2.4f)
            {
                return new Rgba(86, 54, 24); // the stalk
            }
        }

        return y < 4 ? Clear : Jit(104, 66, 34, n, 2);
    }

    private static Rgba BottlesPixel(int x, int y, uint n)
    {
        foreach (var (left, right) in new[] { (1, 4), (6, 9), (11, 14) })
        {
            if (x < left || x > right)
            {
                continue;
            }

            if (y is >= 1 and <= 2 && x >= left + 1 && x <= right - 1)
            {
                return new Rgba(190, 140, 84); // the cork
            }

            if (y is >= 3 and <= 5 && x >= left + 1 && x <= right - 1)
            {
                return new Rgba(170, 214, 244); // the neck
            }

            if (y >= 6)
            {
                if (x == left + 1 && y is >= 7 and <= 11)
                {
                    return new Rgba(226, 244, 255); // the shine
                }

                return y > 9 ? Jit(70, 130, 218, n, 2) : Jit(110, 168, 238, n, 2);
            }
        }

        return y < 4 ? Clear : Jit(104, 66, 34, n, 2);
    }

    // A face of the 16 x 16 cell the head's front shows: skin, brows, eyes, nose, mouth, a fringe of hair, long hair beside the cheeks, a beard.
    private static Rgba FacePixel(int x, int y, uint n, Rgba hair, bool longHair, bool beard, bool cheeks, Rgba eyes)
    {
        var skin = Jit(240, 196, 150, n, 2);
        var hairPixel = Jit(hair.R, hair.G, hair.B, n, 3);
        if (y <= 2 || (y == 3 && (x < 3 || x > 12 || x % 5 == 2)))
        {
            return hairPixel; // the fringe
        }

        if (longHair && (x <= 1 || x >= 14))
        {
            return hairPixel;
        }

        if (!longHair && y <= 5 && (x == 0 || x == 15))
        {
            return hairPixel;
        }

        if (y == 5 && (x is >= 2 and <= 5 || x is >= 10 and <= 13))
        {
            return Jit(hair.R * 3 / 4, hair.G * 3 / 4, hair.B * 3 / 4, n, 2); // the brows
        }

        if (y is 6 or 7 && (x is >= 3 and <= 6 || x is >= 9 and <= 12))
        {
            var left = x is >= 3 and <= 6;
            var inner = left ? x >= 5 : x <= 10;
            return inner ? new Rgba(eyes.R, eyes.G, eyes.B) : new Rgba(250, 250, 252); // white at the outer corner, the iris towards the nose
        }

        if (y is 9 or 10 && x is 7 or 8)
        {
            return Jit(214, 166, 124, n, 2); // the nose
        }

        if (cheeks && y is 9 or 10 && (x is 3 or 4 || x is 11 or 12))
        {
            return new Rgba(240, 152, 140);
        }

        if (beard && y >= 11 && x is >= 3 and <= 12)
        {
            if (y == 12 && x is >= 6 and <= 9)
            {
                return new Rgba(176, 82, 76); // the mouth in the beard
            }

            return Jit(hair.R, hair.G, hair.B, n, 3);
        }

        if (beard && y == 10 && x is >= 4 and <= 11 && x is not 7 and not 8)
        {
            return Jit(hair.R, hair.G, hair.B, n, 3); // the moustache
        }

        if (y == 12 && x is >= 6 and <= 9)
        {
            return new Rgba(176, 82, 76); // the mouth
        }

        if (y == 11 && (x is 5 or 10))
        {
            return Jit(200, 150, 112, n, 2); // the corners of a smile
        }

        return skin;
    }

    // The torch icon: a brown stick on the diagonal and a flame above its tip.
    private static Rgba TorchIconPixel(int x, int y, uint n)
    {
        var line = x + y;
        if (line is 16 or 17 && x is >= 3 and <= 10)
        {
            return line == 16 ? Jit(88, 56, 28, n, 2) : Jit(136, 92, 46, n, 2);
        }

        var fx = x - 11f;
        var fy = y - 5f;
        var d = ((fx * fx) / 6f) + ((fy * fy) / 16f);
        if (d <= 1f)
        {
            return d < 0.28f ? new Rgba(255, 248, 190) : d < 0.6f ? new Rgba(255, 200, 64) : new Rgba(246, 120, 28);
        }

        return Clear;
    }
}
