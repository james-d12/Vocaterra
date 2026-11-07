using Vocaterra.Game.Graphics.Blocks;
using Vocaterra.Game.Graphics.World;

namespace Vocaterra.Game.Graphics.Water;

/// <summary>
/// The pixels of the water's animation (water-plan 4.8), drawn in code like <see cref="BlockArtNature"/>: every pixel is a palette colour picked by an
/// integer hash of its place and the frame, so the pictures are the same on every machine and CC0 by construction (D-game-5). There are 16 frames
/// (<see cref="WaterFrames.Count"/>) that loop seamlessly, of the five tiles of <see cref="WaterTileLayout"/>:
/// <list type="bullet">
/// <item>the flowing tiles are ripples, short bright streaks along the picture's horizontal that slide one pixel a frame towards its right (the mesh turns the
/// picture so right is downstream);</item>
/// <item>the still tiles hold sparkles that glint for a few frames and blotches that slowly brighten and dim, with no direction;</item>
/// <item>the falling tile is streaks of foam and clear water that move down three pixels a frame.</item>
/// </list>
/// Each picture repeats every 16 pixels both ways, which is what lets blocks side by side join.
/// </summary>
public static class WaterArt
{
    private const int Cell = WaterTileLayout.Cell;

    // How far a frame slides the falling streaks, in pixels (three of sixteen: coprime with the loop, so every offset shows once).
    private const int FallStep = 3;

    private readonly record struct Palette((int R, int G, int B) Body, (int R, int G, int B) Dark, (int R, int G, int B) Light, int Alpha);

    private static readonly Palette Deep = new((28, 110, 224), (20, 88, 200), (84, 168, 255), 205);
    private static readonly Palette Shallow = new((70, 188, 236), (52, 160, 222), (150, 226, 255), 170);

    /// <summary>The RGBA bytes of one frame (<see cref="WaterTileLayout.Width"/> by <see cref="WaterTileLayout.Height"/>, the top row first): the colour row, then its grey copy.</summary>
    public static byte[] Frame(int frame)
    {
        var f = ((frame % WaterFrames.Count) + WaterFrames.Count) % WaterFrames.Count;
        var width = WaterTileLayout.Width;
        var rowBytes = WaterTileLayout.Pitch * width * 4;
        var data = new byte[WaterTileLayout.Height * width * 4];
        for (var tile = 0; tile < WaterTileLayout.Columns; tile++)
        {
            for (var gy = 0; gy < WaterTileLayout.Pitch; gy++)
            {
                for (var gx = 0; gx < WaterTileLayout.Pitch; gx++)
                {
                    // The gutter holds the pixels from the other side of the picture.
                    var x = ((gx - WaterTileLayout.Gutter) % Cell + Cell) % Cell;
                    var y = ((gy - WaterTileLayout.Gutter) % Cell + Cell) % Cell;
                    var (r, g, b, a) = Pixel((WaterTile)tile, x, y, f);
                    var at = ((gy * width) + (tile * WaterTileLayout.Pitch) + gx) * 4;
                    data[at] = (byte)r;
                    data[at + 1] = (byte)g;
                    data[at + 2] = (byte)b;
                    data[at + 3] = (byte)a;
                }
            }
        }

        var grey = Greyscale.ToLuma(data.AsSpan(0, rowBytes).ToArray());
        grey.CopyTo(data, rowBytes);
        return data;
    }

    /// <summary>One pixel of a tile at a frame: the colour and the alpha.</summary>
    public static (int R, int G, int B, int A) Pixel(WaterTile tile, int x, int y, int frame) => tile switch
    {
        WaterTile.DeepStill => Still(Deep, 0x31, x, y, frame),
        WaterTile.ShallowStill => Still(Shallow, 0x32, x, y, frame),
        WaterTile.DeepFlow => Flow(Deep, 0x41, x, y, frame),
        WaterTile.ShallowFlow => Flow(Shallow, 0x42, x, y, frame),
        _ => Falling(x, y, frame),
    };

    private static (int R, int G, int B) Lerp((int R, int G, int B) a, (int R, int G, int B) b, int part, int of) =>
        (a.R + ((b.R - a.R) * part / of), a.G + ((b.G - a.G) * part / of), a.B + ((b.B - a.B) * part / of));

    // Still water: soft blotches of 4 by 4 pixels, each breathing between the body colour and a little lighter on its own phase, and sparse sparkles.
    private static (int R, int G, int B, int A) Still(Palette p, uint salt, int x, int y, int frame)
    {
        var blotch = BlockArtNature.Mix(salt, (uint)(x / 4), (uint)(y / 4));
        var phase = (int)(blotch % WaterFrames.Count);
        var wave = Math.Abs(((frame + phase) % WaterFrames.Count) - (WaterFrames.Count / 2)); // 0..8..0: a triangle that loops
        var colour = Lerp(p.Body, p.Light, wave, 8 * 5); // up to a fifth of the way to the light colour
        if (blotch % 5 == 0)
        {
            colour = Lerp(p.Dark, p.Body, wave, 8);
        }

        var alpha = p.Alpha;
        var spark = BlockArtNature.Mix(salt + 7, (uint)x, (uint)y);
        if (spark % 29 == 0)
        {
            // A glint lasts four frames, rising and fading.
            var age = (((frame - (int)((spark >> 8) % WaterFrames.Count)) % WaterFrames.Count) + WaterFrames.Count) % WaterFrames.Count;
            if (age < 4)
            {
                colour = Lerp(colour, (255, 255, 255), new[] { 2, 4, 5, 3 }[age], 5);
                alpha = Math.Min(255, p.Alpha + 40);
            }
        }

        return (colour.R, colour.G, colour.B, alpha);
    }

    // Flowing water: streaks along x, in two thirds of the rows, each starting at a place picked by its row and 4 to 9 pixels long, bright at the head and
    // softer behind it, and a few dark flecks. The whole picture slides one pixel to the right a frame.
    private static (int R, int G, int B, int A) Flow(Palette p, uint salt, int x, int y, int frame)
    {
        var sx = ((x - frame) % Cell + Cell) % Cell;
        var colour = p.Body;
        var alpha = p.Alpha;
        var row = BlockArtNature.Mix(salt, (uint)y, 1);
        if (row % 3 != 0)
        {
            var start = (int)((row >> 8) % Cell);
            var length = 4 + (int)((row >> 16) % 6);
            var into = ((sx - start) % Cell + Cell) % Cell;
            if (into < length)
            {
                var head = length - 1 - into; // 0 at the leading end
                colour = head < 2 ? p.Light : Lerp(p.Body, p.Light, 1, 2);
                alpha = p.Alpha + (head < 2 ? 40 : 15);
            }
        }

        if (colour == p.Body && BlockArtNature.Mix(salt + 9, (uint)sx, (uint)y) % 11 == 0)
        {
            colour = p.Dark;
        }

        return (colour.R, colour.G, colour.B, Math.Min(255, alpha));
    }

    // Falling water: vertical streaks of foam and clear water, offset column by column, sliding down three pixels a frame; period 8 divides 16, so it loops.
    private static (int R, int G, int B, int A) Falling(int x, int y, int frame)
    {
        var sy = ((y - (frame * FallStep)) % Cell + Cell) % Cell;
        var offset = (int)(BlockArtNature.Mix(0xC0, (uint)x, 1) % 8);
        var kind = (int)(BlockArtNature.Mix(0xC0, (uint)x, 2) % 3);
        var phase = (sy + offset) % 8;
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

        if (BlockArtNature.Mix(0xC3, (uint)x, (uint)sy) % 9 == 0)
        {
            colour = (255, 255, 255);
            alpha = 250;
        }

        return (colour.R, colour.G, colour.B, alpha);
    }
}
