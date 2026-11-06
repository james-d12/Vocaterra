using Vocaterra.Game.Core.Activities;
using Vocaterra.Game.UI.Common;

namespace Vocaterra.Game.UI.Station;

/// <summary>
/// Where the station overlay's pieces are on a canvas (W07, design 10.8), as plain numbers so that drawing and mouse hits use the same ones and a test can click a cell and check
/// every canvas without a window: the instruction strip across the top, the fixtures on the left, the targets in the middle, the page of what can be held along the bottom with
/// the empty hand and the page controls, the selection line and the key hints under it. The overlay is centred and keeps <see cref="Margin"/> from the canvas's edge.
/// </summary>
public readonly record struct StationLayout
{
    /// <summary>The distance kept from the canvas's edge.</summary>
    public const int Margin = 8;

    /// <summary>The cells of one page of what can be held.</summary>
    public const int HeldColumns = StationScreen.PageSize;

    /// <summary>The height of the title row.</summary>
    public const int TitleHeight = 22;

    /// <summary>The height of the selection line.</summary>
    public const int SelectionHeight = 20;

    /// <summary>The height of the key hints (two lines).</summary>
    public const int FooterHeight = 40;

    /// <summary>The height of the notice under the targets.</summary>
    public const int NoticeHeight = 20;

    private const int Gap = 6;
    private const int CellGap = 3;
    private const int MaxWidth = 960;

    private readonly PixelBox[] _fixtures;
    private readonly PixelBox[] _targets;
    private readonly PixelBox[] _held;
    private readonly PixelBox[] _verbs;

    /// <summary>Computes the layout for a canvas and the shape of the view.</summary>
    /// <param name="canvasWidth">The canvas's width.</param>
    /// <param name="canvasHeight">The canvas's height.</param>
    /// <param name="fixtureCount">The fixtures of the station (the left column).</param>
    /// <param name="targetCount">The targets (the middle).</param>
    /// <param name="stepCount">The steps of the procedure (the dots).</param>
    /// <param name="verbCount">The verbs of the open choice, or 0.</param>
    public StationLayout(int canvasWidth, int canvasHeight, int fixtureCount, int targetCount, int stepCount, int verbCount = 0)
    {
        Compact = canvasHeight < 560 || canvasWidth < 700;
        var pad = Compact ? 8 : 14;
        var width = Math.Min(canvasWidth - (2 * Margin), MaxWidth);
        var height = Compact ? canvasHeight - (2 * Margin) : Math.Min(canvasHeight - (2 * Margin), 620);
        Panel = new PixelBox((canvasWidth - width) / 2, (canvasHeight - height) / 2, width, height);
        StepCount = stepCount;
        var left = Panel.X + pad;
        var inner = Panel.Width - (2 * pad);
        var top = Panel.Y + pad;
        Title = new PixelBox(left, top, inner, TitleHeight);
        top = Title.Bottom + 4;
        Strip = new InstructionStripLayout(new PixelBox(left, top, inner, Compact ? InstructionStripLayout.CompactHeight : InstructionStripLayout.RegularHeight), Compact);
        top = Strip.Box.Bottom + Gap;

        var cellHeight = Compact ? 54 : 76;
        var captionHeight = Compact ? 20 : 22;
        var heldBlock = captionHeight + cellHeight;
        Footer = new PixelBox(left, Panel.Bottom - pad - FooterHeight, inner, FooterHeight);
        Selection = new PixelBox(left, Footer.Y - 4 - SelectionHeight, inner, SelectionHeight);
        var heldTop = Selection.Y - 4 - heldBlock;
        HeldCaption = new PixelBox(left, heldTop, inner, captionHeight);
        var middleHeight = Math.Max(NoticeHeight + 24, heldTop - Gap - top);
        Middle = new PixelBox(left, top, inner, middleHeight);

        var fixtureWidth = Math.Min(Compact ? 130 : 180, inner / 3);
        FixturesColumn = new PixelBox(left, top, fixtureWidth, middleHeight);
        var fixtureHeight = fixtureCount <= 0 ? 0 : Math.Clamp((middleHeight - ((fixtureCount - 1) * 4)) / fixtureCount, 20, 44);
        FixtureCapacity = fixtureHeight == 0 ? 0 : (middleHeight + 4) / (fixtureHeight + 4);
        var shown = Math.Min(Math.Max(0, fixtureCount), FixtureCapacity); // a station with more fixtures than the column holds shows the first ones
        _fixtures = Enumerable.Range(0, shown).Select(i => new PixelBox(left, top + (i * (fixtureHeight + 4)), fixtureWidth, fixtureHeight)).ToArray();

        var targetsLeft = left + fixtureWidth + 8;
        TargetsArea = new PixelBox(targetsLeft, top, inner - fixtureWidth - 8, middleHeight - NoticeHeight - 4);
        Notice = new PixelBox(targetsLeft, top + middleHeight - NoticeHeight, TargetsArea.Width, NoticeHeight);
        var targets = Math.Max(0, targetCount);
        var targetWidth = targets == 0 ? 0 : Math.Clamp((TargetsArea.Width - ((targets - 1) * 10)) / targets, 60, 160);
        var targetHeight = Math.Min(TargetsArea.Height, Compact ? 74 : 110);
        var targetsRow = (targets * targetWidth) + (Math.Max(0, targets - 1) * 10);
        var targetsX = TargetsArea.X + Math.Max(0, (TargetsArea.Width - targetsRow) / 2);
        var targetsY = TargetsArea.Y + Math.Max(0, (TargetsArea.Height - targetHeight) / 2);
        _targets = Enumerable.Range(0, targets).Select(i => new PixelBox(targetsX + (i * (targetWidth + 10)), targetsY, targetWidth, targetHeight)).ToArray();

        // The held row: the empty hand first, then the nine cells of the page.
        var cellsTop = heldTop + captionHeight;
        var cellWidth = Math.Clamp((inner - ((HeldColumns) * CellGap) - 10) / (HeldColumns + 1), 28, 96);
        var rowWidth = ((HeldColumns + 1) * cellWidth) + (HeldColumns * CellGap) + 10;
        var rowLeft = left + Math.Max(0, (inner - rowWidth) / 2);
        Hand = new PixelBox(rowLeft, cellsTop, cellWidth, cellHeight);
        _held = Enumerable.Range(0, HeldColumns).Select(i => new PixelBox(rowLeft + cellWidth + 10 + (i * (cellWidth + CellGap)), cellsTop, cellWidth, cellHeight)).ToArray();

        // The caption row: what the row is on the left, the page controls on the right.
        var control = Compact ? 22 : 24;
        PageNext = new PixelBox(HeldCaption.Right - control, HeldCaption.Y, control, captionHeight);
        PageLabel = new PixelBox(PageNext.X - 4 - 76, HeldCaption.Y, 76, captionHeight);
        PagePrevious = new PixelBox(PageLabel.X - 4 - control, HeldCaption.Y, control, captionHeight);
        CaptionText = new PixelBox(left, HeldCaption.Y, Math.Max(0, PagePrevious.X - 8 - left), captionHeight);

        // The verb choice floats over the middle.
        var verbs = Math.Max(0, verbCount);
        var verbWidth = Math.Min(inner, 300);
        var verbHeight = verbs == 0 ? 0 : 30 + (verbs * 28) + 6;
        var verbBox = verbs == 0 ? new PixelBox(0, 0, 0, 0) : new PixelBox(Middle.X + ((Middle.Width - verbWidth) / 2), Middle.Y + Math.Max(0, (Middle.Height - verbHeight) / 2), verbWidth, verbHeight);
        VerbBox = verbBox;
        _verbs = Enumerable.Range(0, verbs).Select(i => new PixelBox(verbBox.X + 8, verbBox.Y + 30 + (i * 28), verbBox.Width - 16, 26)).ToArray();
    }

    /// <summary>Whether the canvas is small (smaller cells, strip and padding).</summary>
    public bool Compact { get; }

    /// <summary>The overlay's panel.</summary>
    public PixelBox Panel { get; }

    /// <summary>The title row.</summary>
    public PixelBox Title { get; }

    /// <summary>The instruction strip's pieces.</summary>
    public InstructionStripLayout Strip { get; }

    /// <summary>The steps of the procedure (the dots).</summary>
    public int StepCount { get; }

    /// <summary>The middle band: the fixtures, the targets and the notice.</summary>
    public PixelBox Middle { get; }

    /// <summary>The left column of fixtures.</summary>
    public PixelBox FixturesColumn { get; }

    /// <summary>How many fixtures the column holds at this size; the ones past it are not shown.</summary>
    public int FixtureCapacity { get; }

    /// <summary>Where the targets stand.</summary>
    public PixelBox TargetsArea { get; }

    /// <summary>The line under the targets: what the last act was.</summary>
    public PixelBox Notice { get; }

    /// <summary>The caption row over the held cells.</summary>
    public PixelBox HeldCaption { get; }

    /// <summary>The caption's text.</summary>
    public PixelBox CaptionText { get; }

    /// <summary>The previous-page control.</summary>
    public PixelBox PagePrevious { get; }

    /// <summary>The page indicator (<c>1/2</c>).</summary>
    public PixelBox PageLabel { get; }

    /// <summary>The next-page control.</summary>
    public PixelBox PageNext { get; }

    /// <summary>The empty hand (key 0).</summary>
    public PixelBox Hand { get; }

    /// <summary>The line naming the selection.</summary>
    public PixelBox Selection { get; }

    /// <summary>The key hints.</summary>
    public PixelBox Footer { get; }

    /// <summary>The verb choice's box, empty when no choice is open.</summary>
    public PixelBox VerbBox { get; }

    /// <summary>The cell of the fixture at <paramref name="index"/>.</summary>
    public PixelBox Fixture(int index) => _fixtures[index];

    /// <summary>The target at <paramref name="index"/>.</summary>
    public PixelBox Target(int index) => _targets[index];

    /// <summary>The cell of the page at <paramref name="index"/> (0 to 8; key index + 1).</summary>
    public PixelBox Held(int index) => _held[index];

    /// <summary>The verb at <paramref name="index"/> of the open choice.</summary>
    public PixelBox Verb(int index) => _verbs[index];

    /// <summary>The number of targets laid out.</summary>
    public int TargetCount => _targets.Length;

    /// <summary>The number of fixtures laid out (the station's, up to <see cref="FixtureCapacity"/>).</summary>
    public int FixtureCount => _fixtures.Length;

    /// <summary>The number of verbs laid out.</summary>
    public int VerbCount => _verbs.Length;

    /// <summary>What is under a point, or <see langword="null"/>: the open verb choice first, then the strip's buttons, the page controls, the empty hand, the cells and the targets.</summary>
    /// <param name="x">The canvas x.</param>
    /// <param name="y">The canvas y.</param>
    /// <param name="heldCount">How many cells the page has (cells past it are not hit).</param>
    /// <param name="pages">The number of pages (the controls are not hit when there is one).</param>
    public StationHit? HitAt(int x, int y, int heldCount, int pages)
    {
        if (_verbs.Length > 0)
        {
            for (var i = 0; i < _verbs.Length; i++)
            {
                if (_verbs[i].Contains(x, y))
                {
                    return new StationHit(StationRegion.Verb, i);
                }
            }

            return null; // while the choice is open the rest of the overlay waits
        }

        if (Strip.Repeat.Contains(x, y))
        {
            return new StationHit(StationRegion.Repeat, 0);
        }

        if (Strip.Slower.Contains(x, y))
        {
            return new StationHit(StationRegion.Slower, 0);
        }

        if (Strip.Help.Contains(x, y))
        {
            return new StationHit(StationRegion.Help, 0);
        }

        if (pages > 1 && PagePrevious.Contains(x, y))
        {
            return new StationHit(StationRegion.PagePrevious, 0);
        }

        if (pages > 1 && PageNext.Contains(x, y))
        {
            return new StationHit(StationRegion.PageNext, 0);
        }

        if (Hand.Contains(x, y))
        {
            return new StationHit(StationRegion.Hand, 0);
        }

        for (var i = 0; i < Math.Min(heldCount, _held.Length); i++)
        {
            if (_held[i].Contains(x, y))
            {
                return new StationHit(StationRegion.Held, i);
            }
        }

        for (var i = 0; i < _targets.Length; i++)
        {
            if (_targets[i].Contains(x, y))
            {
                return new StationHit(StationRegion.Target, i);
            }
        }

        return null;
    }
}
