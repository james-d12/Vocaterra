using System.Text;

namespace Vocaterra.Game.Core.Voxel;

/// <summary>
/// Renders the voxel world as text, for test output, golden dumps and bug reports (no engine needed): a top-down map (the highest block of
/// each column, one character per block kind, with the spawn and the NPCs marked) and a side slice.
/// </summary>
public static class AsciiMap3D
{
    /// <summary>The character that marks the player's spawn on a top-down map.</summary>
    public const char SpawnMark = '@';

    /// <summary>The character that marks the player's body on a top-down map.</summary>
    public const char PlayerMark = 'P';

    /// <summary>The legend line to print under a map.</summary>
    public const string Legend =
        "Legend: . grass  , dirt  # stone  : sand  T trunk  o leaves  b bush  B berry bush  ~ water  = path  p planks  D door  "
        + "M workbench  i torch  $ stall  ^ roof  + fence  W wall  O fire ring  ! sign  H bridge  * snow  \" undergrowth  ` flower  @ spawn  P player  "
        + "G guide  K shopkeeper  V villager  N other npc";

    /// <summary>The character for a block kind (air is a space).</summary>
    public static char Glyph(BlockId block) => block switch
    {
        BlockId.Air => ' ',
        BlockId.Grass => '.',
        BlockId.Dirt => ',',
        BlockId.Stone => '#',
        BlockId.Sand => ':',
        BlockId.Trunk => 'T',
        BlockId.Leaves => 'o',
        BlockId.Bush => 'b',
        BlockId.BerryBush => 'B',
        BlockId.Water => '~',
        BlockId.Path => '=',
        BlockId.Planks => 'p',
        BlockId.Door => 'D',
        BlockId.Table => 'M',
        BlockId.Torch or BlockId.PostTorch => 'i',
        BlockId.Stall => '$',
        BlockId.Roof => '^',
        BlockId.Fence => '+',
        BlockId.Wall => 'W',
        BlockId.FireRing => 'O',
        BlockId.Sign => '!',
        BlockId.Bridge => 'H',
        BlockId.Snow => '*',
        BlockId.SpruceTrunk or BlockId.BirchTrunk => 'T',
        BlockId.SpruceLeaves or BlockId.BirchLeaves => 'o',
        BlockId.TallGrass or BlockId.Fern or BlockId.Reeds => '"',
        BlockId.YellowFlower or BlockId.WhiteFlower => '`',
        BlockId.MossyStone or BlockId.Cobble => '#',
        BlockId.Timber => 'X',
        BlockId.Lantern => 'l',
        BlockId.Crate or BlockId.Chest => 'c',
        BlockId.AwningRed or BlockId.AwningWhite => '$',
        BlockId.Canvas => '^',
        BlockId.Farmland => ';',
        BlockId.Wheat or BlockId.Loaves or BlockId.Apples or BlockId.Bottles => '"',
        BlockId.SignTiendaA or BlockId.SignTiendaB or BlockId.SignRio or BlockId.SignMontanaA or BlockId.SignMontanaB => '!',
        _ => '?',
    };

    /// <summary>The character that marks an NPC on a top-down map: <c>G</c> guide, <c>K</c> shopkeeper, <c>V</c> villager, <c>N</c> anyone else.</summary>
    public static char NpcMark(string name) => name switch
    {
        "guide" => 'G',
        "shopkeeper" => 'K',
        "villager" => 'V',
        _ => 'N',
    };

    /// <summary>
    /// The top-down map of a scene: one line per row of the grid (north first), one character per column, showing the highest block of the
    /// column; the NPCs and the spawn are drawn over it, and the <paramref name="player"/> if one is given.
    /// </summary>
    public static string TopDown(Scene scene, PlayerBody3D? player = null)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return TopDown(scene.Grid, scene.Spawn.Feet, scene.Npcs, player);
    }

    /// <summary>
    /// A top-down map of a grid (or a window of it: <paramref name="x0"/>, <paramref name="z0"/>, <paramref name="width"/> and
    /// <paramref name="depth"/>, the whole grid when they are not given). Columns with no block at all are drawn as spaces.
    /// </summary>
    public static string TopDown(
        VoxelGrid grid,
        BlockPos? spawn = null,
        IReadOnlyList<NpcPlacement>? npcs = null,
        PlayerBody3D? player = null,
        int x0 = 0,
        int z0 = 0,
        int width = -1,
        int depth = -1)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var w = width < 0 ? grid.SizeX - x0 : width;
        var d = depth < 0 ? grid.SizeZ - z0 : depth;
        var rows = new char[d][];
        for (var z = 0; z < d; z++)
        {
            rows[z] = new char[w];
            for (var x = 0; x < w; x++)
            {
                var top = grid.HighestBlock(x0 + x, z0 + z);
                rows[z][x] = top < 0 ? ' ' : Glyph(grid.GetBlock(x0 + x, top, z0 + z));
            }
        }

        void Mark(int x, int z, char c)
        {
            if (x >= x0 && x < x0 + w && z >= z0 && z < z0 + d)
            {
                rows[z - z0][x - x0] = c;
            }
        }

        if (spawn is { } feet)
        {
            Mark(feet.X, feet.Z, SpawnMark);
        }

        if (npcs is not null)
        {
            foreach (var npc in npcs)
            {
                Mark(npc.Feet.X, npc.Feet.Z, NpcMark(npc.Name));
            }
        }

        if (player is not null)
        {
            Mark(player.FeetBlock.X, player.FeetBlock.Z, PlayerMark);
        }

        var sb = new StringBuilder(d * (w + 1));
        foreach (var row in rows)
        {
            sb.Append(row).Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>
    /// A side slice looking north from the south: the blocks at row <paramref name="z"/> for columns <paramref name="x0"/> to
    /// <paramref name="x0"/> + <paramref name="width"/> - 1 and layers from <paramref name="yTop"/> down to <paramref name="yTop"/> -
    /// <paramref name="height"/> + 1, the top layer first, east to the right. Cells outside the grid are drawn as <c>%</c>.
    /// </summary>
    public static string SideSlice(VoxelGrid grid, int z, int x0, int width, int yTop, int height)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var sb = new StringBuilder(height * (width + 1));
        for (var y = yTop; y > yTop - height; y--)
        {
            for (var x = x0; x < x0 + width; x++)
            {
                sb.Append(grid.InBounds(x, y, z) ? Glyph(grid.GetBlock(x, y, z)) : '%');
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }
}
