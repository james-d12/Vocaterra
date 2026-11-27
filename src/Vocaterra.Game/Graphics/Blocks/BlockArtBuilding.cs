namespace Vocaterra.Game.Graphics.Blocks;

/// <summary>
/// The pixel art of the seven oak pieces' icons (K05, building plan 3.7): the log, the stairs, the slab, the fence, the fence gate, the door and the trapdoor, drawn in code in the planks' and the trunk's colours like the gear's
/// icons (<see cref="BlockArtCarried"/>). They sit in the block atlas's UI-only cells, so the block art (K04) is untouched; the item's <c>icon</c> key names the cell (<see cref="BlockAtlasLayout.IconForKey"/>).
/// Each is a sixteen-by-sixteen table over a small palette: <c>o</c> the outline, <c>h</c> the light face, <c>m</c> the middle, <c>s</c> the shade, <c>r</c> the log's end rings, <c>b</c> its bark, <c>i</c> the iron of a hinge or a handle.
/// </summary>
public static class BlockArtBuilding
{
    private static readonly Dictionary<char, (byte R, byte G, byte B)> Planks = new()
    {
        ['o'] = (74, 50, 26), ['h'] = (214, 170, 104), ['m'] = (188, 144, 82), ['s'] = (146, 104, 56), ['i'] = (60, 60, 66), ['r'] = (226, 190, 128),
    };

    private static readonly Dictionary<char, (byte R, byte G, byte B)> Trunk = new()
    {
        ['o'] = (50, 34, 18), ['h'] = (150, 108, 62), ['m'] = (120, 84, 46), ['s'] = (92, 64, 34), ['r'] = (222, 184, 122), ['b'] = (200, 160, 100),
    };

    private static readonly Dictionary<BlockCell, (Dictionary<char, (byte R, byte G, byte B)> Palette, string[] Rows)> Icons = new()
    {
        // a log lying on its side, its end ring to the left
        [BlockCell.IconOakLog] = (Trunk,
            [
                "................",
                "................",
                "................",
                "....oooooooooo..",
                "...orrrrohhhhho.",
                "..orrbbrohmmmmso",
                "..orbrrbohmmmsso",
                "..orbrrbohmmmsso",
                "..orrbbrohmmmsso",
                "...orrrrohmmsso.",
                "....ooooossssso.",
                ".........oooooo.",
                "................",
                "................",
                "................",
                "................",
            ]),
        // stairs climbing to the right, three planked steps
        [BlockCell.IconOakStairs] = (Planks,
            [
                "................",
                "................",
                "..........oooo..",
                ".........ohhhho.",
                ".........ommmmso",
                ".....oooooommsso",
                "....ohhhhhommsso",
                "....ommmmmommsso",
                "..ooommmmmommsso",
                ".ohhhommmmommsso",
                ".ommmommmmommsso",
                ".ommmommmmossso.",
                ".ossso.ooooooo..",
                "..ooooo.........",
                "................",
                "................",
            ]),
        // a single low slab, its top face above its edge
        [BlockCell.IconOakSlab] = (Planks,
            [
                "................",
                "................",
                "................",
                "................",
                "................",
                "...oooooooooooo.",
                "..ohhhhhhhhhhmo.",
                ".ohhhhhhhhhhhmso",
                ".ooooooooooooooo",
                ".ommmmmmmmmmmmso",
                ".ommmmmmmmmmmsso",
                ".osssssssssssso.",
                "..oooooooooooo..",
                "................",
                "................",
                "................",
            ]),
        // two posts with two rails between them
        [BlockCell.IconOakFence] = (Planks,
            [
                "................",
                "..oo........oo..",
                ".ohho......ohho.",
                ".ohmo......ohmo.",
                ".omso......omso.",
                ".omso......omso.",
                ".ooooooooooooooo",
                ".ohhhhhhhhhhhhmo",
                ".ommmmmmmmmmmmso",
                ".oooooooooooooo.",
                ".omso......omso.",
                ".omso......omso.",
                ".ohhmo....ohhmo.",
                ".ommso....ommso.",
                "..oooo....oooo..",
                "................",
            ]),
        // two tall posts with a framed gate between them: three rails and a brace
        [BlockCell.IconOakFenceGate] = (Planks,
            [
                "................",
                ".ooo........ooo.",
                ".ohmo......ohmo.",
                ".omso.oooo.omso.",
                ".omsooohhmoomso.",
                ".omsoommmmsomso.",
                ".omsooooooooomso",
                ".omsoommmommomso",
                ".omsoommommmomso",
                ".omsooooooooomso",
                ".omsoohhhhmsomso",
                ".omsoommmmssomso",
                ".omsooooooooomso",
                ".ohhmo......ohhm",
                ".ommso......omms",
                "..oooo.......ooo",
            ]),
        // a tall door: two long panels and a handle
        [BlockCell.IconOakDoor] = (Planks,
            [
                "....oooooooo....",
                "...ohhhhhhhho...",
                "...ohmmmmmmso...",
                "...omooooooso...",
                "...omohhhhoso...",
                "...omommmmoso...",
                "...omommmmoso...",
                "...omooooooso...",
                "...omohhhhoso...",
                "...omommmmoiso..",
                "...omommmmoiso..",
                "...omooooooso...",
                "...omssssssso...",
                "...ommmmmmmmo...",
                "...oooooooooo...",
                "................",
            ]),
        // a trapdoor seen from above: a square of slats, hinges on the left and a ring on the right
        [BlockCell.IconOakTrapdoor] = (Planks,
            [
                "................",
                "................",
                "..oooooooooooo..",
                "..ohhhhhhhhhmo..",
                "..oiiiiiiiiiio..",
                "..ommmmmmmmmso..",
                "..ommmmmmmmmso..",
                "..osssssssssso..",
                "..ohhhhhhhhhmo..",
                "..ommmmmmmiiso..",
                "..ommmmmmiooso..",
                "..osssssssssso..",
                "..ohhhhhhhhhmo..",
                "..oooooooooooo..",
                "................",
                "................",
            ]),
    };

    /// <summary>Whether this class draws the cell.</summary>
    /// <param name="cell">The cell.</param>
    public static bool Draws(BlockCell cell) => Icons.ContainsKey(cell);

    /// <summary>The cells this class draws, in the atlas's order.</summary>
    public static IReadOnlyCollection<BlockCell> Cells => Icons.Keys;

    /// <summary>The RGBA bytes (16 x 16 x 4, row by row, the top row first) of a cell this class draws.</summary>
    /// <param name="cell">The cell (<see cref="Draws"/>).</param>
    public static byte[] Pixels(BlockCell cell)
    {
        var (palette, rows) = Icons[cell];
        return BlockArtGear.Render(palette, rows);
    }
}
