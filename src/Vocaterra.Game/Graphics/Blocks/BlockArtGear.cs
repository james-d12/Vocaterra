namespace Vocaterra.Game.Graphics.Blocks;

/// <summary>
/// The pixel art of the gear items' icons (I08a): the three materials and the seven pieces of clothing, drawn in code like the rest of the atlas (D-game-5: CC0 by construction, no picture file). Each icon is a
/// table of sixteen strings (one character a pixel, <c>.</c> clear) over a small palette of its own, so a new icon is one row of <see cref="Icons"/> and one cell of <see cref="BlockCell"/>.
/// </summary>
public static class BlockArtGear
{
    private const int Size = BlockAtlasLayout.CellSize;

    // Each row: the cell, the palette (a character and its colour), then the sixteen rows of sixteen characters.
    private static readonly Dictionary<BlockCell, (Dictionary<char, (byte R, byte G, byte B)> Palette, string[] Rows)> Icons = new()
    {
        // a folded bolt of pale blue cloth, two layers with a woven pattern
        [BlockCell.IconCloth] = (new Dictionary<char, (byte R, byte G, byte B)> { ['o'] = (58, 74, 110), ['h'] = (207, 220, 242), ['m'] = (155, 182, 221), ['s'] = (111, 143, 194) },
            [
                "................",
                "................",
                "................",
                "................",
                "...oooooooooo...",
                "..ohhhhhhhhhmo..",
                "..ohmhmhmhmhso..",
                "..omhmhmhmhmso..",
                "..osssssssssso..",
                "..ohhhhhhhhhhso.",
                "..omhmhmhmhmhso.",
                "..ommhmhmhmhmso.",
                "..ossssssssssso.",
                "...ooooooooooo..",
                "................",
                "................",
            ]),
        // a brown hide with stitched marks and four short legs
        [BlockCell.IconLeather] = (new Dictionary<char, (byte R, byte G, byte B)> { ['o'] = (74, 45, 23), ['h'] = (201, 138, 75), ['m'] = (168, 105, 58), ['s'] = (126, 74, 38), ['t'] = (226, 192, 138) },
            [
                "................",
                "................",
                "......oooo......",
                "....oommmmoo....",
                "...ohhhhhhhho...",
                "..ohmmmmmmmmso..",
                ".omhmtmtmtmmsmo.",
                ".omhmmmsmmmmsso.",
                ".omhmmmmsmmmsmo.",
                "..ohmtmtmtmmso..",
                "..ohmmmmmmmmso..",
                "...osssssssso...",
                "....oomsssoo....",
                "......oooo......",
                "................",
                "................",
            ]),
        // a grey iron ingot, lit on its top and left
        [BlockCell.IconIron] = (new Dictionary<char, (byte R, byte G, byte B)> { ['o'] = (44, 49, 58), ['h'] = (201, 209, 218), ['m'] = (141, 153, 168), ['s'] = (95, 107, 122), ['w'] = (161, 172, 186), ['W'] = (242, 245, 248) },
            [
                "................",
                "................",
                "................",
                "................",
                ".....oooooooooo.",
                "....ohhhhhhhhhho",
                "...ohhhhhhhhhhwo",
                "..ohhhhhhhhhhwwo",
                "..ommmmmmmmmmwwo",
                "..ommWWmmmmmmwwo",
                "..omWmmmmmmmmwo.",
                "..ommmmmmmmmmwo.",
                "..ossssssssssso.",
                "...ooooooooooo..",
                "................",
                "................",
            ]),
        // a straw hat with a wide brim and a red band
        [BlockCell.IconHat] = (new Dictionary<char, (byte R, byte G, byte B)> { ['o'] = (90, 66, 23), ['h'] = (236, 208, 138), ['m'] = (215, 181, 106), ['s'] = (168, 134, 63), ['b'] = (168, 58, 50) },
            [
                "................",
                "................",
                "......oooo......",
                ".....ohhhho.....",
                ".....ohoooo.....",
                "....ohhhhhmo....",
                "....ohhhhhmo....",
                "....ohhhhhmo....",
                "..ooobbbbbbooo..",
                ".ommmmmmmmmmmmo.",
                ".ommmmmmmmmmmmo.",
                "osssssssssssssso",
                ".oossssssssssoo.",
                "...oooooooooo...",
                "................",
                "................",
            ]),
        // an orange woolly cap with a ribbed brim and a cream pom-pom
        [BlockCell.IconCap] = (new Dictionary<char, (byte R, byte G, byte B)> { ['o'] = (92, 45, 10), ['h'] = (242, 161, 90), ['m'] = (217, 120, 45), ['s'] = (169, 90, 28), ['p'] = (245, 230, 208) },
            [
                "......oppo......",
                ".....oppppo.....",
                "......oppo......",
                ".....ommmmo.....",
                "....ohhhhhmo....",
                "...ohhhhhmsmo...",
                "...ohmmmmmmso...",
                "...ohmmmmmmso...",
                "...ommmmmmmso...",
                "...ommmmmmmso...",
                "..omsmsmsmsmso..",
                "..omsmsmsmsmso..",
                "..omsmsmsmsmso..",
                "...oooooooooo...",
                "................",
                "................",
            ]),
        // a cream shirt with short sleeves
        [BlockCell.IconShirt] = (new Dictionary<char, (byte R, byte G, byte B)> { ['o'] = (125, 116, 88), ['h'] = (249, 245, 230), ['m'] = (236, 230, 208), ['s'] = (196, 188, 158) },
            [
                "................",
                "................",
                "................",
                ".oooooo..ooooo..",
                "ohhhmhhoohhmmmo.",
                "ommmhmoooommmmso",
                "ommmhhossohmmmso",
                "ommmhmmmmmmmmmso",
                "ossssmmmmmmsssso",
                ".ooohmmmmmmsooo.",
                "...ohmmmmmmso...",
                "...ohmmmmmmso...",
                "...ohmmmmmmso...",
                "...ohssssssso...",
                "....oooooooo....",
                "................",
            ]),
        // an olive jacket with long sleeves and four buttons
        [BlockCell.IconJacket] = (new Dictionary<char, (byte R, byte G, byte B)> { ['o'] = (47, 64, 32), ['h'] = (134, 168, 94), ['m'] = (95, 127, 63), ['s'] = (68, 96, 42), ['b'] = (224, 208, 138) },
            [
                "................",
                "................",
                "................",
                ".ooooo....oooo..",
                "ohhhmho..ohmmmo.",
                "ommshmhoohmmmmmo",
                "ommshmmssmmmmmmo",
                "ommshmmssmmsmmmo",
                "ommshmbssbmsmmmo",
                "ommshmmssmmsmmmo",
                "ommshmmssmmsmmmo",
                "ommshmbssbmsmmmo",
                "ossshmmssmmsmmmo",
                ".ooohmmssmmsooo.",
                "...osssssssso...",
                "....oooooooo....",
            ]),
        // a wine-red coat with long sleeves, a pale collar, a belt and buttons
        [BlockCell.IconCoat] = (new Dictionary<char, (byte R, byte G, byte B)> { ['o'] = (74, 21, 32), ['h'] = (184, 80, 94), ['m'] = (140, 47, 60), ['s'] = (106, 32, 41), ['b'] = (107, 74, 42), ['B'] = (230, 195, 94), ['f'] = (232, 220, 200) },
            [
                "................",
                "................",
                ".ooooo....oooo..",
                "ohhhffo..offmmo.",
                "ommsfffoofffmmmo",
                "ommshmmssmmmmmmo",
                "ommshmmssmmmmmmo",
                "ommshmmssmmmmmmo",
                "ommshmmssmmmmmmo",
                "ommshbbBBbbbmmmo",
                "ommshmmssmmsmmmo",
                "ommshmmssmmsmmmo",
                "ommshmBssBmsmmmo",
                "ossshmmssmmsmmmo",
                ".ooohmmssmmsooo.",
                "...osssssssso...",
            ]),
        // blue trousers with a belt
        [BlockCell.IconTrousers] = (new Dictionary<char, (byte R, byte G, byte B)> { ['o'] = (31, 46, 77), ['h'] = (106, 140, 196), ['m'] = (63, 95, 149), ['s'] = (44, 69, 112), ['b'] = (138, 106, 58), ['B'] = (230, 195, 94) },
            [
                "................",
                "....ooooooooo...",
                "...ohhhhhhhhho..",
                "...obbbbBbbbbo..",
                "...ommmmmmmmmo..",
                "...ohmmmshmmso..",
                "...ohmmmshmmso..",
                "...ohmmsohmmso..",
                "...ohmmsohmmso..",
                "...ohmmsohmmso..",
                "...ohmmsohmmso..",
                "...ohmmsohmmso..",
                "...ohmmsohmmso..",
                "...ohmmsohmmso..",
                "...ossssosssso..",
                "....oooo.oooo...",
            ]),
        // a brown shoe with pale laces
        [BlockCell.IconShoes] = (new Dictionary<char, (byte R, byte G, byte B)> { ['o'] = (46, 27, 14), ['h'] = (140, 94, 58), ['m'] = (90, 58, 34), ['s'] = (64, 40, 26), ['z'] = (46, 27, 14), ['l'] = (232, 220, 192) },
            [
                "................",
                "................",
                "................",
                "...ooo..........",
                "..ossso.........",
                ".ohhhhmo........",
                ".ohmlmmo........",
                ".ohlmlmoooooo...",
                ".ohmlmmhlhlmmo..",
                ".ohmmmmlmlmmmmo.",
                ".ohmmmmmmmmmmmo.",
                ".ohmmmmmmmmmmmo.",
                ".osssssssssssso.",
                ".ozzzzzzzzzzzzzo",
                "..ooooooooooooo.",
                "................",
            ]),
    };

    /// <summary>Whether this class draws the cell.</summary>
    public static bool Draws(BlockCell cell) => Icons.ContainsKey(cell);

    /// <summary>The cells this class draws, in the atlas's order.</summary>
    public static IReadOnlyCollection<BlockCell> Cells => Icons.Keys;

    /// <summary>The RGBA bytes (16 x 16 x 4, row by row, the top row first) of a cell this class draws.</summary>
    /// <param name="cell">The cell (<see cref="Draws"/>).</param>
    public static byte[] Pixels(BlockCell cell)
    {
        var (palette, rows) = Icons[cell];
        var data = new byte[Size * Size * 4];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                if (!palette.TryGetValue(rows[y][x], out var colour))
                {
                    continue; // clear
                }

                var i = ((y * Size) + x) * 4;
                data[i] = colour.R;
                data[i + 1] = colour.G;
                data[i + 2] = colour.B;
                data[i + 3] = 255;
            }
        }

        return data;
    }
}
