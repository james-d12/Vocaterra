using Vocaterra.Game.Graphics.Blocks;

namespace Vocaterra.Game.Graphics.Sky;

/// <summary>
/// The colours of the sky (S14) and the one place the fog colour comes from: a deep blue overhead that grows paler towards the horizon, where it meets
/// the colour of the distance fog, so far mountains fade into the haze instead of ending in a wall. Plain values, no engine type.
/// </summary>
public static class SkyPalette
{
    /// <summary>The colour at the horizon, and the colour of the fog: a pale, slightly blue haze.</summary>
    public static readonly (byte R, byte G, byte B) Horizon = (172, 210, 242);

    /// <summary>The colour half way up the sky.</summary>
    public static readonly (byte R, byte G, byte B) Middle = (112, 172, 238);

    /// <summary>The colour straight overhead: a deep, saturated blue.</summary>
    public static readonly (byte R, byte G, byte B) Zenith = (44, 112, 214);

    /// <summary>The colour of a cloud's lit top.</summary>
    public static readonly (byte R, byte G, byte B) CloudTop = (255, 255, 255);

    /// <summary>The colour of a cloud's sides (in the shade of its own top).</summary>
    public static readonly (byte R, byte G, byte B) CloudSide = (228, 236, 250);

    /// <summary>The colour of a cloud's underside.</summary>
    public static readonly (byte R, byte G, byte B) CloudBottom = (204, 216, 238);

    /// <summary>The colour at the horizon at dusk (S15, <c>--dusk</c>), and the colour of the dusk fog: a warm amber haze.</summary>
    public static readonly (byte R, byte G, byte B) DuskHorizon = (246, 170, 118);

    /// <summary>The colour half way up the sky at dusk: a dusty rose.</summary>
    public static readonly (byte R, byte G, byte B) DuskMiddle = (178, 118, 146);

    /// <summary>The colour overhead at dusk: a deep indigo.</summary>
    public static readonly (byte R, byte G, byte B) DuskZenith = (44, 54, 124);

    /// <summary>The colour of a cloud's lit top at dusk.</summary>
    public static readonly (byte R, byte G, byte B) DuskCloudTop = (255, 204, 168);

    /// <summary>The colour of a cloud's sides at dusk.</summary>
    public static readonly (byte R, byte G, byte B) DuskCloudSide = (226, 156, 148);

    /// <summary>The colour of a cloud's underside at dusk.</summary>
    public static readonly (byte R, byte G, byte B) DuskCloudBottom = (150, 104, 134);

    /// <summary>The colour of the sky at an elevation in degrees above the horizon (below the horizon it is the haze).</summary>
    public static (byte R, byte G, byte B) At(float elevationDegrees) => At(elevationDegrees, Horizon, Middle, Zenith);

    /// <summary>The colour of the dusk sky at an elevation in degrees (S15).</summary>
    public static (byte R, byte G, byte B) AtDusk(float elevationDegrees) => At(elevationDegrees, DuskHorizon, DuskMiddle, DuskZenith);

    /// <summary>The colour of a sky with the given three colours at an elevation in degrees (E02: the day, dusk and night looks all use the one gradient).</summary>
    /// <param name="elevationDegrees">The elevation above the horizon.</param>
    /// <param name="horizon">The colour at and below the horizon.</param>
    /// <param name="middle">The colour half way up.</param>
    /// <param name="zenith">The colour overhead.</param>
    public static (byte R, byte G, byte B) At(float elevationDegrees, (byte R, byte G, byte B) horizon, (byte R, byte G, byte B) middle, (byte R, byte G, byte B) zenith)
    {
        if (elevationDegrees <= 0f)
        {
            return horizon;
        }

        var t = Math.Clamp(elevationDegrees / 90f, 0f, 1f);
        // Pale near the horizon, blue quickly, deep only near the zenith.
        var low = Math.Clamp(t * 2.4f, 0f, 1f);
        low = low * low * (3f - (2f * low));
        var high = Math.Clamp((t - 0.35f) / 0.65f, 0f, 1f);
        var mid = Blend(horizon, middle, low);
        return Blend(mid, zenith, high);
    }

    /// <summary>The colour that is <paramref name="t"/> of the way from <paramref name="a"/> to <paramref name="b"/>.</summary>
    public static (byte R, byte G, byte B) Blend((byte R, byte G, byte B) a, (byte R, byte G, byte B) b, float t)
    {
        var k = Math.Clamp(t, 0f, 1f);
        return ((byte)(a.R + ((b.R - a.R) * k)), (byte)(a.G + ((b.G - a.G) * k)), (byte)(a.B + ((b.B - a.B) * k)));
    }
}

/// <summary>One flat cloud: a slab of blocks high in the sky.</summary>
/// <param name="X">The west edge.</param>
/// <param name="Y">The underside.</param>
/// <param name="Z">The north edge.</param>
/// <param name="Width">The size along x.</param>
/// <param name="Depth">The size along z.</param>
/// <param name="Thickness">The height of the slab.</param>
/// <param name="Alpha">How much of the cloud shows, above 0 up to 1: 1 for a cloud that is there at any cover from <see cref="CloudField.Cover"/> percent up, less while the cover is just passing its cell.</param>
public readonly record struct CloudBox(float X, float Y, float Z, float Width, float Depth, float Thickness, float Alpha = 1f);

/// <summary>
/// The clouds (S14): a field of flat blocky slabs, each cell of a grid holding one cloud of two or three overlapping boxes or none, picked by a hash of
/// the cell (no random generator, no clock: the same sky for the same drift). The field wraps round the camera, so it never ends, and drifts with the tick.
/// </summary>
public static class CloudField
{
    /// <summary>The side of a cell of the grid in blocks.</summary>
    public const float CellSize = 96f;

    /// <summary>How many cells away from the camera's cell the field reaches in each direction.</summary>
    public const int Reach = 6;

    /// <summary>The height of the clouds' undersides in blocks (above the peaks, which top out near 125).</summary>
    public const float Altitude = 168f;

    /// <summary>How far a cloud drifts east in a tick, in blocks (about 0.7 blocks a second).</summary>
    public const float DriftPerTick = 0.012f;

    /// <summary>The percent of cells that hold a cloud in fair weather: the cover the sky has always had (a weather's cloud cover of 0.42).</summary>
    public const int Cover = 42;

    /// <summary>How wide, in percent of cover, the fade of a cell is: a cloud takes this much of a rise in cover to come from clear to fully shown.</summary>
    public const float FadeWidth = 6f;

    /// <summary>
    /// How much of a cell's cloud shows at a cover (E05), 0 (none) to 1. A cell has a place in the order of the cells (<paramref name="rank"/>, 0 up to 100: the hash's percent, plus a fraction
    /// so cells of one percent differ). The cells below <see cref="Cover"/> are the fair sky's own and show fully at that cover and above, fading out only as the cover drops under their
    /// rank; the others fade in as the cover rises past theirs, over <see cref="FadeWidth"/>. The function never falls as the cover rises, and at the fair cover it is exactly 1 for the fair sky's
    /// cells and 0 for the rest, so that sky is today's.
    /// </summary>
    /// <param name="rank">The cell's rank, 0 up to 100.</param>
    /// <param name="fair">Whether the cell is one of the fair sky's (its percent is under <see cref="Cover"/>).</param>
    /// <param name="cover">The cloud cover, 0 to 1.</param>
    public static float CellAlpha(float rank, bool fair, double cover)
    {
        // The cover on the scale of the ranks: the fair cover is where the fair cells end, a full cover is where the last cell is fully in, and zero is where the last fair cell is fully out.
        var c = Math.Clamp(cover, 0, 1);
        var fairShare = Cover / 100.0;
        var threshold = c >= fairShare
            ? Cover + ((c - fairShare) / (1 - fairShare) * (100 + FadeWidth - Cover))
            : -FadeWidth + (c / fairShare * (Cover + FadeWidth));
        var alpha = ((threshold - rank) / FadeWidth) + (fair ? 1d : 0d);
        return (float)Math.Clamp(alpha, 0, 1);
    }

    /// <summary>Fills <paramref name="boxes"/> with the clouds round the camera at a tick in fair weather (the list is cleared first).</summary>
    public static void Generate(float cameraX, float cameraZ, long tick, List<CloudBox> boxes) => Generate(cameraX, cameraZ, tick, boxes, Cover / 100.0);

    /// <summary>
    /// Fills <paramref name="boxes"/> with the clouds round the camera at a tick under a cloud cover (the list is cleared first). At <see cref="Cover"/> percent the boxes are the fair sky's;
    /// a higher cover only adds clouds (none is moved or removed), each fading in by <see cref="CloudBox.Alpha"/> as the cover passes its cell, and a lower one fades the fair sky's out.
    /// </summary>
    /// <param name="cameraX">The camera's x.</param>
    /// <param name="cameraZ">The camera's z.</param>
    /// <param name="tick">The simulation's tick (the drift).</param>
    /// <param name="boxes">The list to fill.</param>
    /// <param name="cover">The weather's cloud cover, 0 to 1.</param>
    public static void Generate(float cameraX, float cameraZ, long tick, List<CloudBox> boxes, double cover)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        boxes.Clear();
        var drift = tick * DriftPerTick;
        var cellX0 = (int)MathF.Floor((cameraX - drift) / CellSize);
        var cellZ0 = (int)MathF.Floor(cameraZ / CellSize);
        for (var cz = cellZ0 - Reach; cz <= cellZ0 + Reach; cz++)
        {
            for (var cx = cellX0 - Reach; cx <= cellX0 + Reach; cx++)
            {
                var h = BlockArtNature.Mix((uint)cx, (uint)cz, 0xC10D);
                var percent = (h >> 20) % 100;
                var rank = percent + (((h >> 28) & 0xF) / 16f); // a fraction from the hash's top bits, so cells of one percent differ
                var alpha = CellAlpha(rank, percent < Cover, cover);
                if (alpha <= 0f)
                {
                    continue;
                }

                var originX = (cx * CellSize) + drift + (((h & 0xFF) / 255f) * 40f);
                var originZ = (cz * CellSize) + ((((h >> 8) & 0xFF) / 255f) * 40f);
                var y = Altitude + ((((h >> 16) & 0xF) / 15f) * 14f);
                var width = 30f + ((((h >> 24) & 0xF) / 15f) * 34f);
                var depth = 18f + ((((h >> 4) & 0xF) / 15f) * 22f);
                var thick = 5f + (((h >> 12) & 3) * 1f);
                boxes.Add(new CloudBox(originX, y, originZ, width, depth, thick, alpha));
                var extra = BlockArtNature.Mix((uint)cx, (uint)cz, 0xC10E);
                boxes.Add(new CloudBox(originX + (width * 0.45f), y + 1f, originZ + (depth * 0.35f), width * 0.7f, depth * 0.8f, thick + 1f, alpha));
                if ((extra & 3) != 0)
                {
                    boxes.Add(new CloudBox(originX - (width * 0.2f), y - 0.5f, originZ + (depth * 0.5f), width * 0.5f, depth * 0.7f, thick - 1f, alpha));
                }
            }
        }
    }
}
