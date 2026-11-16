namespace Vocaterra.Game.Core.World.Generation.Procedural;

/// <summary>
/// One passage from the surface into the caves (B11b): a straight stair, <see cref="Width"/> blocks across, whose floor starts at the ground of the mouth and drops one block every <see cref="Run"/>
/// blocks along until it meets a cave (<see cref="Length"/>), then runs level for <see cref="Landing"/> blocks. It is a pure description: the cells it cuts and the rim that frames its mouth are
/// computed from it for any block. Along the passage <c>along</c> is 0 at the mouth, and <c>lateral</c> counts across it (0 on the line of the mouth, left of the walker negative).
/// </summary>
internal sealed class EntranceRamp
{
    /// <summary>Describes a passage.</summary>
    /// <param name="anchorX">The mouth's x.</param>
    /// <param name="anchorZ">The mouth's z.</param>
    /// <param name="dirX">The direction's x (-1, 0 or 1).</param>
    /// <param name="dirZ">The direction's z (-1, 0 or 1; exactly one of the two is not 0).</param>
    /// <param name="mouthY">The y of the top solid block at the mouth: the first floor block.</param>
    /// <param name="length">The index along where the passage meets its cave (the last step of the descent).</param>
    /// <param name="landing">The level blocks after it.</param>
    /// <param name="width">The width.</param>
    /// <param name="headroom">The air above the floor.</param>
    /// <param name="run">The blocks along for each block down.</param>
    /// <param name="rimLength">The blocks from the mouth the rim frames.</param>
    /// <param name="rimWidth">How wide the rim is.</param>
    public EntranceRamp(int anchorX, int anchorZ, int dirX, int dirZ, int mouthY, int length, int landing, int width, int headroom, int run, int rimLength, int rimWidth)
    {
        AnchorX = anchorX;
        AnchorZ = anchorZ;
        DirX = dirX;
        DirZ = dirZ;
        MouthY = mouthY;
        Length = length;
        Landing = landing;
        Width = width;
        Headroom = headroom;
        Run = run;
        RimLength = rimLength;
        RimWidth = rimWidth;
    }

    /// <summary>The mouth's x.</summary>
    public int AnchorX { get; }

    /// <summary>The mouth's z.</summary>
    public int AnchorZ { get; }

    /// <summary>The direction's x.</summary>
    public int DirX { get; }

    /// <summary>The direction's z.</summary>
    public int DirZ { get; }

    /// <summary>The y of the top solid block at the mouth.</summary>
    public int MouthY { get; }

    /// <summary>The index along where the passage meets its cave.</summary>
    public int Length { get; }

    /// <summary>The level blocks after the descent.</summary>
    public int Landing { get; }

    /// <summary>The width.</summary>
    public int Width { get; }

    /// <summary>The air above the floor.</summary>
    public int Headroom { get; }

    /// <summary>The blocks along for each block down.</summary>
    public int Run { get; }

    /// <summary>The blocks from the mouth the rim frames.</summary>
    public int RimLength { get; }

    /// <summary>How wide the rim is.</summary>
    public int RimWidth { get; }

    /// <summary>The lowest lateral offset of the passage.</summary>
    public int LateralMin => -((Width - 1) / 2);

    /// <summary>The highest lateral offset of the passage.</summary>
    public int LateralMax => Width / 2;

    /// <summary>The last index along the passage, the end of the landing.</summary>
    public int Last => Length + Landing;

    /// <summary>The y of the floor block (the top solid block) at an index along: the mouth's, one lower every <see cref="Run"/> blocks, level on the landing.</summary>
    /// <param name="along">The index along.</param>
    public int FloorAt(int along) => MouthY - (Math.Clamp(along, 0, Length) / Run);

    /// <summary>The lowest floor: the landing's.</summary>
    public int LowestFloor => FloorAt(Length);

    /// <summary>Where a block column lies against the passage.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    /// <param name="along">The index along (negative before the mouth).</param>
    /// <param name="lateral">The offset across.</param>
    public void Locate(int x, int z, out int along, out int lateral)
    {
        var rx = x - AnchorX;
        var rz = z - AnchorZ;
        along = (rx * DirX) + (rz * DirZ);
        lateral = (rz * DirX) - (rx * DirZ);
    }

    /// <summary>The block of the passage's centre line at an index along.</summary>
    /// <param name="along">The index along.</param>
    public (int X, int Z) CentreAt(int along) => (AnchorX + (DirX * along), AnchorZ + (DirZ * along));

    /// <summary>Whether a column lies in the passage's cross-section (the cells above its floor are cut there).</summary>
    /// <param name="along">The index along.</param>
    /// <param name="lateral">The offset across.</param>
    public bool InPassage(int along, int lateral) => along >= 0 && along <= Last && lateral >= LateralMin && lateral <= LateralMax;

    /// <summary>Whether a column lies in the rim: within its width of the passage's first blocks.</summary>
    /// <param name="along">The index along.</param>
    /// <param name="lateral">The offset across.</param>
    public bool InRim(int along, int lateral) => along >= -RimWidth && along <= RimLength + RimWidth && lateral >= LateralMin - RimWidth && lateral <= LateralMax + RimWidth;

    /// <summary>Whether no tree or plant may stand on the column: the rim and one column more.</summary>
    /// <param name="x">The block's x.</param>
    /// <param name="z">The block's z.</param>
    public bool Clears(int x, int z)
    {
        Locate(x, z, out var along, out var lateral);
        return along >= -RimWidth - 1 && along <= RimLength + RimWidth + 1 && lateral >= LateralMin - RimWidth - 1 && lateral <= LateralMax + RimWidth + 1;
    }

    /// <summary>A box of columns that holds the passage, its landing and its rim (a little more than they need), padded by <paramref name="pad"/>.</summary>
    /// <param name="pad">The padding, in blocks.</param>
    public (int MinX, int MinZ, int MaxX, int MaxZ) Bounds(int pad)
    {
        var (ex, ez) = CentreAt(Last);
        var reach = Math.Max(LateralMax, -LateralMin) + RimWidth + 2 + pad;
        return (Math.Min(AnchorX, ex) - reach, Math.Min(AnchorZ, ez) - reach, Math.Max(AnchorX, ex) + reach, Math.Max(AnchorZ, ez) + reach);
    }
}
