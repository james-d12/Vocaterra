using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Language.Book;
using Vocaterra.Game.Core.Language.Checks;
using Vocaterra.Game.Core.Language.Lessons;
using Vocaterra.Game.Core.Language.Speech;
using Vocaterra.Game.Core.Reporting;
using Vocaterra.Game.Core.Save;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Startup;
using Vocaterra.Game.UI.Book;
using Vocaterra.Game.UI.Checks;
using Vocaterra.Game.UI.Common;
using Vocaterra.Game.UI.Crafting;
using Vocaterra.Game.UI.Dialogue;
using Vocaterra.Game.UI.Hud;
using Vocaterra.Game.UI.Inventory;
using Vocaterra.Game.UI.Lesson;
using Vocaterra.Game.UI.Overlays;
using Vocaterra.Game.UI.Report;
using Xunit.Abstractions;

namespace Vocaterra.Game.Tests.UI.Common;

/// <summary>
/// X08: the interface scale. The 2D layers are laid out on a canvas of the window over the scale, so each engine-free layout model is checked on the six canvases the
/// two window sizes (1280 x 720, 800 x 600) and the three scales (100, 125, 150 percent) make: no piece overlaps another, nothing is outside the canvas or its panel, text
/// wraps to its width (measured with a stand-in font wider than the real one: 7 px a character small, 14 px large), and what does not fit degrades as the model says. The
/// screenshots are not used; the layouts are plain numbers.
/// </summary>
public sealed class UiScaleLayoutTests(ITestOutputHelper output)
{
    private static readonly (int Width, int Height)[] Windows = [(1280, 720), (800, 600)];
    private static readonly int[] Scales = [100, 125, 150];

    private static float Small(string text) => text.Length * 7f;

    private static float Large(string text) => text.Length * 14f;

    private static readonly Lazy<LocaleText> English = new(() => TestGame.Data.Locale);

    /// <summary>Every window size and scale: the case's canvas is the window divided by the scale.</summary>
    public static TheoryData<int, int, int> Cases
    {
        get
        {
            var data = new TheoryData<int, int, int>();
            foreach (var (width, height) in Windows)
            {
                foreach (var scale in Scales)
                {
                    data.Add(width, height, scale);
                }
            }

            return data;
        }
    }

    private static (int Width, int Height) Canvas(int windowWidth, int windowHeight, int scale) => UiScale.Canvas(scale, windowWidth, windowHeight);

    private static PixelBox Whole((int Width, int Height) canvas) => new(0, 0, canvas.Width, canvas.Height);

    private static bool Inside(PixelBox box, PixelBox outer) => box.X >= outer.X && box.Y >= outer.Y && box.Right <= outer.Right && box.Bottom <= outer.Bottom;

    private static bool Apart(PixelBox a, PixelBox b) => !ConversationLayout.Overlaps(a, b);

    // ---- The scale.

    [Fact]
    public void TheCanvasIsTheWindowDividedByTheScaleAndAWindowTooSmallForTheScaleUsesALowerOne()
    {
        Assert.Equal((1280, 720), UiScale.Canvas(100, 1280, 720));
        Assert.Equal((1024, 576), UiScale.Canvas(125, 1280, 720));
        Assert.Equal((853, 480), UiScale.Canvas(150, 1280, 720));
        Assert.Equal((800, 600), UiScale.Canvas(100, 800, 600));
        Assert.Equal((640, 480), UiScale.Canvas(125, 800, 600));
        Assert.Equal((533, 400), UiScale.Canvas(150, 800, 600));

        // 640 x 480 at 150 percent would be a 427 x 320 canvas, under what the layouts are made for: the scale used is lowered until the canvas is the smallest one.
        var (width, height) = UiScale.Canvas(150, 640, 480);
        Assert.True(width >= UiScale.MinWidth && height >= UiScale.MinHeight, $"{width} x {height}");
        Assert.InRange(UiScale.Effective(150, 640, 480), 1.19, 1.21);
        Assert.Equal(1.0, UiScale.Effective(100, 640, 480));
        Assert.Equal(1.0, UiScale.Effective(150, 100, 100)); // never below 1
    }

    [Fact]
    public void ThePointerMapsFromTheWindowOntoTheCanvasAndTheStepsWrapRound()
    {
        var (canvasWidth, canvasHeight) = UiScale.Canvas(150, 800, 600);
        Assert.Equal((0, 0), UiScale.ToCanvas(0, 0, 800, 600, canvasWidth, canvasHeight));
        Assert.Equal((266, 200), UiScale.ToCanvas(400, 300, 800, 600, canvasWidth, canvasHeight));
        var corner = UiScale.ToCanvas(799, 599, 800, 600, canvasWidth, canvasHeight);
        Assert.True(corner.X < canvasWidth && corner.Y < canvasHeight);

        Assert.Equal([125, 150, 100, 125], new[] { 100, 125, 150, 100 }.Select(UiScale.Next));
        Assert.Equal(125, UiScale.Next(7)); // a value that is no step is snapped to the nearest (100), then stepped
        Assert.Equal([100, 125, 150], SaveSettings.TextScaleLevels);
        Assert.Equal(150, SaveSettings.SnapTextScale(999));
        Assert.Equal(100, SaveSettings.SnapTextScale(-5));
    }

    [Fact]
    public void TheTextScaleFlagTakesOneOfTheStepsAndNothingElse()
    {
        Assert.True(LaunchOptions.TryParse(["--screenshot", "a.png", "--text-scale", "150"], out var options, out var error), error);
        Assert.Equal(150, options.TextScale);
        Assert.True(LaunchOptions.TryParse(["--screenshot", "a.png"], out var plain, out _));
        Assert.Null(plain.TextScale); // none: the save's, else 100

        foreach (var bad in new[] { "110", "0", "200", "big", "-125" })
        {
            Assert.False(LaunchOptions.TryParse(["--screenshot", "a.png", "--text-scale", bad], out _, out error), bad);
            Assert.Contains("--text-scale", error, StringComparison.Ordinal);
        }
    }

    // ---- The dialogue panel.

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheDialoguePanelHoldsEveryPieceInsideItAndApartAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        var canvas = Canvas(windowWidth, windowHeight, scale);
        var window = Whole(canvas);
        var combos = 0;
        foreach (var typing in new[] { false, true })
        {
            foreach (var choices in new[] { Array.Empty<string>(), ["agua por favor", "ayuda", "ayuda por favor"] })
            {
                foreach (var hints in new[] { DialoguePanel.KeyHints(RouteScene.Shipped.Locale), DialoguePanel.SpeechOffKeyHints(RouteScene.Shipped.Locale), DialoguePanel.ButtonHints(RouteScene.Shipped.Locale), DialoguePanel.SpeechOffButtonHints(RouteScene.Shipped.Locale) })
                {
                    var layout = new DialogueLayout(canvas.Width, canvas.Height, Small, choices, SpeechButtons.For(RouteScene.Shipped.Words.Buttons), typing, static b => $"{b.Text} ({English.Value.ButtonSupport(SpeechButtons.KeyOf(b.Action)!)})", hints);
                    var where = $"{windowWidth}x{windowHeight} at {scale}%, typing {typing}, {choices.Length} choices";
                    Assert.True(Inside(layout.Panel, window), $"{where}: the panel is outside the canvas");

                    var pieces = new List<(string Name, PixelBox Box)>();
                    pieces.Add(("header", new PixelBox(layout.Panel.X + DialogueLayout.Padding, layout.Panel.Y + 8, layout.InnerWidth, DialogueLayout.LineHeight)));
                    if (layout.LinesBottom > layout.LinesTop)
                    {
                        pieces.Add(("lines", new PixelBox(layout.Panel.X + DialogueLayout.Padding, layout.LinesTop, layout.InnerWidth, layout.LinesBottom - layout.LinesTop)));
                    }

                    pieces.Add(("heard", new PixelBox(layout.Panel.X + DialogueLayout.Padding, layout.HeardY, layout.InnerWidth, DialogueLayout.LineHeight)));
                    pieces.AddRange(layout.Buttons.Select(b => ($"button {b.Action}", b.Box)));
                    pieces.AddRange(layout.Choices.Select(c => ($"choice {c.Action}", c.Box)));
                    if (layout.InputBox is { } input)
                    {
                        pieces.Add(("input", input));
                    }

                    if (layout.HasHints)
                    {
                        pieces.Add(("hints", new PixelBox(layout.Panel.X + DialogueLayout.Padding, layout.HintsY, layout.InnerWidth, layout.HintRows.Count * DialogueLayout.LineHeight - 2)));
                        Assert.All(layout.HintRows, row => Assert.True(Small(row) <= layout.InnerWidth, $"{where}: the hint row '{row}' is wider than the panel"));
                    }

                    foreach (var (name, box) in pieces)
                    {
                        Assert.True(Inside(box, layout.Panel), $"{where}: {name} {box} is outside the panel {layout.Panel}");
                    }

                    for (var i = 0; i < pieces.Count; i++)
                    {
                        for (var j = i + 1; j < pieces.Count; j++)
                        {
                            Assert.True(Apart(pieces[i].Box, pieces[j].Box), $"{where}: {pieces[i].Name} {pieces[i].Box} overlaps {pieces[j].Name} {pieces[j].Box}");
                        }
                    }

                    Assert.True(layout.LinesBottom >= layout.LinesTop, $"{where}: the lines area is upside down");
                    combos++;
                }
            }
        }

        output.WriteLine($"{windowWidth}x{windowHeight} at {scale}% (canvas {canvas.Width} x {canvas.Height}): {combos} panel states laid out; compact {DialogueLayout.IsCompact(canvas.Height)}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ThePlainPanelKeepsRoomForAtLeastOneLineOfConversationAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        var canvas = Canvas(windowWidth, windowHeight, scale);
        var layout = new DialogueLayout(canvas.Width, canvas.Height, Small, [], SpeechButtons.For(RouteScene.Shipped.Words.Buttons), typing: false, static b => $"{b.Text} ({English.Value.ButtonSupport(SpeechButtons.KeyOf(b.Action)!)})", DialoguePanel.ButtonHints(RouteScene.Shipped.Locale));

        Assert.True(layout.LinesBottom - layout.LinesTop >= DialogueLayout.LineHeight, $"{canvas.Width}x{canvas.Height}: {layout.LinesBottom - layout.LinesTop} px for the conversation");
    }

    // ---- The lesson strip, the bubble, the cards and the beacon's label: the conversation screen.

    private static LessonView Step(LessonStepType type, string english, string? spanish, string? hint = null, bool canContinue = true, IReadOnlyList<string>? options = null) =>
        new("sample", "A first hello", 4, 37, type, "Listen", english, spanish, null, hint, options ?? [], -1, canContinue, false, false, []);

    private static IReadOnlyList<LessonView> StripCases()
    {
        // Every step of the shipped lessons, with its text from the locale (C03).
        var views = new List<LessonView>();
        foreach (var lesson in TestGame.Data.Lessons.Lessons)
        {
            foreach (var step in lesson.Steps)
            {
                var english = step.English;
                var type = step.Type == LessonStepType.Listen ? LessonStepType.Listen : LessonStepType.Explain;
                IReadOnlyList<string> options = type == LessonStepType.Listen ? step.Options : [];
                views.Add(Step(type, english, "¿Dónde está el río, por favor?", "Listen again, then say it slowly. The phrase is on the card.", type != LessonStepType.Listen, options));
                views.Add(Step(LessonStepType.Explain, english, null));
            }
        }

        views.Add(Step(LessonStepType.Listen, "What does she say?", "Necesitamos agua y fruta para el fuego de la noche, por favor.", null, false, ["You are going to the shop today.", "I am going to the shop today.", "We are going to the shop today."]));
        return views;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheLessonStripStandsUnderThePanelInsideTheCanvasWithItsButtonsAndAllItsEnglishAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        var canvas = Canvas(windowWidth, windowHeight, scale);
        var window = Whole(canvas);
        var panel = DialogueLayout.PanelBox(canvas.Width, canvas.Height);
        var steps = StripCases();
        var checkedSteps = 0;
        var paged = 0;
        foreach (var view in steps)
        {
            var layout = LessonStripLayout.For(English.Value, view, null, canvas.Width, canvas.Height, panel.Y, Small, Large)!;
            var where = $"{canvas.Width}x{canvas.Height} step '{view.English[..Math.Min(30, view.English.Length)]}'";
            Assert.True(Inside(layout.Box, window), $"{where}: the strip {layout.Box} is outside the canvas");
            Assert.True(layout.Box.Y >= LessonStripLayout.MinTop, $"{where}: the strip starts at {layout.Box.Y}");
            Assert.True(layout.Box.Bottom <= panel.Y, $"{where}: the strip ends at {layout.Box.Bottom}, past the panel at {panel.Y}");
            Assert.True(Apart(layout.Box, panel));
            var inner = layout.Box.Width - (2 * layout.Pad);
            foreach (var row in layout.EnglishRows)
            {
                Assert.True(Small(row) <= inner, $"{where}: English row '{row}' ({Small(row)} px) is wider than the strip ({inner} px)");
            }

            foreach (var row in layout.SpanishRows)
            {
                Assert.True(Large(row) <= inner, $"{where}: Spanish row '{row}' ({Large(row)} px) is wider than the strip ({inner} px)");
            }

            foreach (var row in layout.HintRows)
            {
                Assert.True(Small(row) <= inner, $"{where}: hint row '{row}' ({Small(row)} px) is wider than the strip ({inner} px)");
            }


            var buttons = layout.Buttons;
            foreach (var (action, box, _) in buttons)
            {
                Assert.True(Inside(box, layout.Box), $"{where}: button {action} {box} is outside the strip {layout.Box}");
            }

            for (var i = 0; i < buttons.Count; i++)
            {
                for (var j = i + 1; j < buttons.Count; j++)
                {
                    Assert.True(Apart(buttons[i].Box, buttons[j].Box), $"{where}: button {buttons[i].Action} overlaps {buttons[j].Action}");
                }
            }

            // The text stands above the buttons.
            var textBottom = layout.EnglishY + (layout.EnglishRows.Count * LessonStripLayout.LineHeight) + (layout.HintRows.Count * LessonStripLayout.LineHeight);
            Assert.True(textBottom <= buttons.Min(static b => b.Box.Y), $"{where}: the text reaches {textBottom}, into the buttons at {buttons.Min(static b => b.Box.Y)}");

            // No English is left out: the pages together are the whole text.
            var wrapped = TextWrap.Wrap(DialoguePanel.SupportPrefix(RouteScene.Shipped.Locale) + view.English, inner, Small);
            var all = new List<string>();
            for (var page = 0; page < layout.EnglishPages; page++)
            {
                all.AddRange(LessonStripLayout.For(English.Value, view, null, canvas.Width, canvas.Height, panel.Y, Small, Large, page)!.EnglishRows);
            }

            Assert.Equal(wrapped, all);
            if (layout.EnglishPages > 1)
            {
                paged++;
                Assert.Contains(buttons, static b => b.Action == LessonAction.More);
            }
            else
            {
                Assert.DoesNotContain(buttons, static b => b.Action == LessonAction.More);
            }

            checkedSteps++;
        }

        output.WriteLine($"{canvas.Width}x{canvas.Height}: {checkedSteps} steps laid out under the panel at y {panel.Y}, {paged} of them over more than one page of English");
    }

    [Fact]
    public void ALongEnglishTextGoesOverPagesOnAShortCanvasAndNotOnATallOne()
    {
        var text = string.Join(' ', Enumerable.Repeat("a sentence of plain English", 10)); // 279 characters: three rows on a wide strip, five on a narrow one
        var view = Step(LessonStepType.Explain, text, null);
        var tall = Canvas(1280, 720, 100);
        var short_ = Canvas(800, 600, 150);

        var onTall = LessonStripLayout.For(English.Value, view, null, tall.Width, tall.Height, DialogueLayout.PanelBox(tall.Width, tall.Height).Y, Small, Large)!;
        var onShort = LessonStripLayout.For(English.Value, view, null, short_.Width, short_.Height, DialogueLayout.PanelBox(short_.Width, short_.Height).Y, Small, Large)!;

        Assert.Equal(1, onTall.EnglishPages);
        Assert.True(onShort.EnglishPages > 1, "a 533 x 400 canvas has no room for it on one page");
        Assert.Equal("[M] More (1/" + onShort.EnglishPages + ")", onShort.Buttons.Single(static b => b.Action == LessonAction.More).Text);
        Assert.Equal(LessonAction.More, LessonActions.ForKey(LessonKey.M));
        Assert.Equal("M", LessonActions.KeyLabel(LessonAction.More));
        var wrapped = onShort.EnglishPages;
        Assert.Equal(onShort.EnglishRows, LessonStripLayout.For(English.Value, view, null, short_.Width, short_.Height, DialogueLayout.PanelBox(short_.Width, short_.Height).Y, Small, Large, wrapped)!.EnglishRows); // the pages wrap round
    }

    private static readonly (double X, double Y)[] HeadSpots = [(0.5, 0.25), (0.2, 0.3), (0.8, 0.3), (0.5, 0.08), (0.05, 0.4), (0.95, 0.4)];

    private static readonly ConversationCardRequest[] CardSets0 = [];

    private static readonly ConversationCardRequest[] CardSets3 = [ConversationCards.NewWord, ConversationCards.Support, ConversationCards.Support];

    private static readonly ConversationCardRequest[] CardSets5 = [ConversationCards.NewWord, ConversationCards.Support, ConversationCards.Support, ConversationCards.KeyWord(false), ConversationCards.KeyWord(false)];

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheConversationScreenKeepsTheStripTheBubbleTheCardsTheCalledAndThePanelApartAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        var canvas = Canvas(windowWidth, windowHeight, scale);
        var window = Whole(canvas);
        var panel = DialogueLayout.PanelBox(canvas.Width, canvas.Height);
        var strips = new[]
        {
            Step(LessonStepType.Explain, "Luna tells you her name. “Me llamo” means “my name is”. You only need to understand this one, not say it.", "Me llamo Luna."),
            Step(LessonStepType.Listen, "What does she say?", "Necesitamos agua.", "Listen again.", false, ["She asks for water", "She says goodbye", "She offers bread"]),
            null,
        };
        var cases = 0;
        foreach (var view in strips)
        {
            PixelBox? strip = view is null ? null : LessonStripLayout.For(English.Value, view, null, canvas.Width, canvas.Height, panel.Y, Small, Large)!.Box;
            foreach (var spot in HeadSpots)
            {
                foreach (var cards in new[] { CardSets0, CardSets3, CardSets5 })
                {
                    var x = spot.X * canvas.Width;
                    var y = spot.Y * canvas.Height;
                    var head = ConversationCards.HeadRect(x, y, y + 110);
                    var inset = DialogueBubble.Margin;
                    if (strip is { } s)
                    {
                        inset = Math.Max(inset, s.Bottom + ConversationLayout.Gap);
                    }

                    var bubble = DialogueBubble.PlaceBetween(canvas.Width, canvas.Height, x, y, 340, 100, inset, panel.Y - ConversationLayout.Gap);
                    PixelBox? bubbleBox = bubble is { } b ? new PixelBox(b.Box.X, b.Box.Y, b.Box.Width, b.Box.Height + DialogueBubble.TailHeight) : null;
                    var hard = new List<PixelBox> { panel };
                    if (strip is { } st)
                    {
                        hard.Add(st);
                    }

                    var label = Called.TryPlace(canvas.Width, canvas.Height, canvas.Width - 56, canvas.Height * 0.45, Small("campamento  111 blocks"), hard, [ConversationLayout.Union(head, bubbleBox)]);
                    var extra = new List<PixelBox>();
                    if (label is { } placedLabel)
                    {
                        extra.Add(placedLabel);
                    }

                    if (strip is { } st2)
                    {
                        extra.Add(st2);
                    }

                    var layout = ConversationLayout.Compute(canvas.Width, canvas.Height, head, bubbleBox, panel, cards, extra);
                    var where = $"{canvas.Width}x{canvas.Height} head {spot} cards {cards.Length} strip {(strip is null ? "none" : "up")}";

                    if (label is { } lb)
                    {
                        Assert.True(Inside(lb, window), $"{where}: the beacon label {lb} is outside the canvas");
                        Assert.True(Apart(lb, panel), $"{where}: the beacon label overlaps the panel");
                        Assert.True(strip is not { } sl || Apart(lb, sl), $"{where}: the beacon label overlaps the lesson strip");
                    }

                    if (strip is { } st3)
                    {
                        if (bubbleBox is { } bb)
                        {
                            Assert.True(Apart(bb, st3), $"{where}: the bubble overlaps the lesson strip");
                        }
                    }

                    if (bubbleBox is { } bb2)
                    {
                        Assert.True(Inside(bb2, window), $"{where}: the bubble {bb2} is outside the canvas");
                        Assert.True(Apart(bb2, panel), $"{where}: the bubble overlaps the panel");
                    }

                    var placed = layout.Cards.Where(static c => c.State != CardPresence.Hidden).ToList();
                    foreach (var card in placed)
                    {
                        Assert.True(Inside(card.Box, window), $"{where}: a card {card.Box} is outside the canvas");
                        Assert.True(Apart(card.Box, panel), $"{where}: a card overlaps the panel");
                        Assert.True(Apart(card.Box, head), $"{where}: a card overlaps the head");
                        Assert.True(label is not { } lc || Apart(card.Box, lc), $"{where}: a card overlaps the beacon label");
                        Assert.True(bubbleBox is not { } bbx || Apart(card.Box, bbx), $"{where}: a card overlaps the bubble");
                        Assert.True(strip is not { } sx || Apart(card.Box, sx), $"{where}: a card overlaps the lesson strip");
                    }

                    for (var i = 0; i < placed.Count; i++)
                    {
                        for (var j = i + 1; j < placed.Count; j++)
                        {
                            Assert.True(Apart(placed[i].Box, placed[j].Box), $"{where}: cards {i} and {j} overlap");
                        }
                    }

                    cases++;
                }
            }
        }

        output.WriteLine($"{canvas.Width}x{canvas.Height}: {cases} conversation screens composed");
    }

    // ---- The bubble's band.

    [Fact]
    public void TheBubbleStaysInItsBandBetweenTheStripAndThePanelAndIsLeftOutWhenTheBandIsTooShort()
    {
        var inBand = DialogueBubble.PlaceBetween(853, 480, 400, 200, 300, 70, 100, 220);
        Assert.NotNull(inBand);
        Assert.True(inBand!.Value.Box.Y >= 100 && inBand.Value.Box.Bottom + DialogueBubble.TailHeight <= 220);

        Assert.Null(DialogueBubble.PlaceBetween(853, 480, 400, 200, 300, 70, 160, 220)); // 60 px is not enough for 70 and the pointer
        Assert.Null(DialogueBubble.PlaceBetween(853, 480, -5, 200, 300, 70, 8, 400)); // the head is off the canvas
    }

    // ---- The check card.

    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryItemOfTheCheckBankFitsTheCardAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        var canvas = Canvas(windowWidth, windowHeight, scale);
        var window = Whole(canvas);
        var bank = TestGame.Data.Checks;
        var panel = CheckCardLayout.PanelFor(canvas.Width, canvas.Height);
        Assert.True(Inside(panel, window));
        var side = CheckCardLayout.SideFor(panel);
        var inner = panel.Width - (2 * side);
        var rows = 0;
        foreach (var item in bank)
        {
            var layout = item.Kind == CheckItemKind.Comprehension
                ? CheckCardLayout.ForComprehension(panel, item.Question, CheckLayer.ComprehensionHint(RouteScene.Shipped.Locale), item.Options.Select(static o => (o.Label, o.Icon is not null)).ToList(), Large, Small)
                : CheckCardLayout.ForProduction(panel, CheckLayer.ProductionPreface(RouteScene.Shipped.Locale), item.Situation, CheckLayer.ProductionHint(RouteScene.Shipped.Locale), Large, Small);
            var where = $"{canvas.Width}x{canvas.Height} {item.Id}";
            Assert.All(layout.Preface, row => Assert.True(Small(row) <= inner, $"{where}: preface row '{row}' is wider than the card"));
            Assert.All(layout.Heading, row => Assert.True((layout.HeadingLarge ? Large(row) : Small(row)) <= inner, $"{where}: heading row '{row}' is wider than the card"));
            Assert.All(layout.Hint, row => Assert.True(Small(row) <= inner, $"{where}: hint row '{row}' is wider than the card"));
            var boxes = new List<(string Name, PixelBox Box)>();
            foreach (var option in layout.Options)
            {
                var room = option.Box.Right - 12 - option.TextX;
                Assert.All(option.Rows, row => Assert.True((option.Large ? Large(row) : Small(row)) <= room, $"{where}: option row '{row}' is wider than its box"));
                Assert.True(option.Large || option.Rows.Count * CheckCardLayout.SmallRow <= option.Box.Height, $"{where}: an option takes {option.Rows.Count} small rows in {option.Box.Height} px");
                Assert.True(Inside(option.Box, panel), $"{where}: an option is outside the card");
                boxes.Add(("option", option.Box));
                rows += option.Rows.Count;
            }

            if (layout.Input is { } input)
            {
                Assert.True(Inside(input, panel), $"{where}: the text box is outside the card");
                boxes.Add(("input", input));
            }

            for (var i = 0; i < boxes.Count; i++)
            {
                for (var j = i + 1; j < boxes.Count; j++)
                {
                    Assert.True(Apart(boxes[i].Box, boxes[j].Box), $"{where}: {boxes[i].Name} overlaps {boxes[j].Name}");
                }
            }

            Assert.True(layout.Bottom <= CheckCardLayout.ContentLimit(panel), $"{where}: the content reaches y {layout.Bottom}, past {CheckCardLayout.ContentLimit(panel)} where the notice and the footer start");
            Assert.True(CheckCardLayout.NoticeY(panel) + 18 <= CheckCardLayout.FooterY(panel), $"{where}: the notice and the footer meet");
            Assert.True(CheckCardLayout.FooterY(panel) + 18 <= panel.Bottom, $"{where}: the footer is outside the card");
            rows += layout.Heading.Count + layout.Hint.Count;
        }

        output.WriteLine($"{canvas.Width}x{canvas.Height}: {bank.Count} items, {rows} rows measured, the card {panel.Width} x {panel.Height} ({(CheckCardLayout.IsCompact(panel) ? "compact" : "regular")})");
    }

    // ---- The book.

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheBooksCoverRibbonsPagesAndConjugationCellsHoldAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        var canvas = Canvas(windowWidth, windowHeight, scale);
        var g = new BookGeometry(canvas.Width, canvas.Height);
        BookRect Rect(BookRect r) => r;
        bool In(BookRect inner, BookRect outer) => inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom;
        var window = new BookRect(0, 0, canvas.Width, canvas.Height);

        Assert.True(In(g.Panel, window), "the cover is outside the canvas");
        Assert.True(In(g.Paper, g.Panel));
        Assert.True(In(g.LeftPage, g.Paper) && In(g.RightPage, g.Paper));
        Assert.True(g.LeftPage.Right <= g.RightPage.X, "the pages overlap");

        // The ribbons: inside the cover, side by side, wide enough for the longest label.
        var ribbons = Enum.GetValues<BookPage>().Select(g.TabRect).ToList();
        foreach (var ribbon in ribbons)
        {
            Assert.True(ribbon.X >= g.Panel.X && ribbon.Right <= g.Panel.Right, $"a ribbon {ribbon.X}..{ribbon.Right} is off the cover {g.Panel.X}..{g.Panel.Right}");
            Assert.True(ribbon.Y >= 0 && ribbon.Bottom <= g.Paper.Bottom);
        }

        for (var i = 0; i < ribbons.Count; i++)
        {
            for (var j = i + 1; j < ribbons.Count; j++)
            {
                Assert.True(ribbons[i].Right <= ribbons[j].X || ribbons[j].Right <= ribbons[i].X, $"ribbons {i} and {j} overlap");
            }
        }

        var longest = BookTabs.Labels(RouteScene.Shipped.Locale).Max(static l => l.Length);
        Assert.True((longest * 7) + 8 <= g.TabPixels, $"a label of {longest} characters in a ribbon of {g.TabPixels} px");

        // The list and the detail are written inside their pages, and at least a few lines are visible.
        Assert.True(g.ContentLeft >= g.LeftPage.X && g.ContentRight <= g.LeftPage.Right);
        Assert.True(g.DetailLeft >= g.RightPage.X && g.DetailRight <= g.RightPage.Right);
        Assert.True(g.ContentBottom <= g.Paper.Bottom && g.ContentTop < g.ContentBottom);
        Assert.True(g.LinesVisible >= 8, $"{g.LinesVisible} lines visible");

        // A conjugation table: tense columns stand side by side while each keeps its width, and stack in blocks when the page is too narrow; the cells are inside the detail area.
        for (var columns = 1; columns <= ConjugationTable.AllColumns.Count; columns++)
        {
            var width = g.DetailRight - g.DetailLeft;
            var perBlock = ConjugationLayout.ColumnsPerBlock(width, columns);
            Assert.InRange(perBlock, 1, columns);
            var table = new BookRect(g.DetailLeft, g.ContentTop, width, ConjugationLayout.Height);
            for (var column = 0; column < perBlock; column++)
            {
                Assert.True(In(ConjugationLayout.Header(g.DetailLeft, g.ContentTop, width, perBlock, column), table));
                for (var row = 0; row < ConjugationLayout.PersonRows; row++)
                {
                    Assert.True(In(ConjugationLayout.Cell(g.DetailLeft, g.ContentTop, width, perBlock, row, column), table), $"cell {row},{column} of {perBlock} columns is outside the table");
                }
            }

            Assert.True(ConjugationLayout.ColumnWidth(width, perBlock) >= ConjugationLayout.MinCellWidth, $"a tense column of {ConjugationLayout.ColumnWidth(width, perBlock)} px on a page {width} px wide");
        }

        _ = Rect(window);
        output.WriteLine($"{canvas.Width}x{canvas.Height}: cover {g.Panel.Width} x {g.Panel.Height}, ribbons {g.TabPixels} px, {g.LinesVisible} list lines, detail {g.DetailRight - g.DetailLeft} px wide");
    }

    // ---- The report panel, the mic check, the inventory, the crafting panel, the controls help and the closing card.

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheReportPanelHoldsItsButtonsTextBoxAndHintsInsideItAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        var canvas = Canvas(windowWidth, windowHeight, scale);
        var window = Whole(canvas);
        var layout = new ReportLayout(canvas.Width, canvas.Height, Small, ReportLayer.HintsFirst(RouteScene.Shipped.Locale), ReportLayer.HintsSecond(RouteScene.Shipped.Locale));
        Assert.True(Inside(layout.Panel, window), $"the panel {layout.Panel} is outside the canvas {canvas}");
        var pieces = new List<(string Name, PixelBox Box)>
        {
            ("bug", layout.BugButton), ("feature", layout.FeatureButton), ("text", layout.TextBox), ("save", layout.SaveButton), ("cancel", layout.CancelButton),
            ("hints", new PixelBox(layout.Panel.X + ReportLayout.Padding, layout.HintsY, layout.Panel.Width - (2 * ReportLayout.Padding), (layout.FirstRows.Count + layout.SecondRows.Count) * ReportLayout.LineHeight - 2)),
            ("title", new PixelBox(layout.Panel.X + ReportLayout.Padding, layout.TitleY, layout.Panel.Width - (2 * ReportLayout.Padding), ReportLayout.LineHeight)),
        };
        foreach (var (name, box) in pieces)
        {
            Assert.True(Inside(box, layout.Panel), $"{name} {box} is outside the panel {layout.Panel}");
        }

        for (var i = 0; i < pieces.Count; i++)
        {
            for (var j = i + 1; j < pieces.Count; j++)
            {
                Assert.True(Apart(pieces[i].Box, pieces[j].Box), $"{pieces[i].Name} overlaps {pieces[j].Name}");
            }
        }

        Assert.All(layout.FirstRows.Concat(layout.SecondRows), row => Assert.True(Small(row) <= layout.Panel.Width - (2 * ReportLayout.Padding), $"hint row '{row}' is wider than the panel"));
        Assert.True(layout.VisibleRows >= ReportLayout.MinTextRows);
        Assert.Equal(ReportButton.Save, layout.ButtonAt(layout.SaveButton.X + 1, layout.SaveButton.Y + 1));
        output.WriteLine($"{canvas.Width}x{canvas.Height}: panel {layout.Panel.Width} x {layout.Panel.Height}, {layout.VisibleRows} text rows, hints {layout.FirstRows.Count}+{layout.SecondRows.Count} rows");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheMicCheckHoldsItsButtonsAndMeterInsideItAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        var canvas = Canvas(windowWidth, windowHeight, scale);
        var window = Whole(canvas);
        var layout = new MicCheckLayout(canvas.Width, canvas.Height, [MicCheckButton.Continue, MicCheckButton.Retry, MicCheckButton.Skip], Small, RouteScene.Shipped.Locale);

        Assert.True(Inside(layout.Panel, window));
        Assert.True(Inside(layout.Meter, layout.Panel));
        var pieces = layout.Buttons.Select(b => ($"button {b.Button}", b.Box)).Append(("meter", layout.Meter)).ToList();
        foreach (var (name, box) in pieces)
        {
            Assert.True(Inside(box, layout.Panel), $"{name} {box} is outside the panel");
        }

        for (var i = 0; i < pieces.Count; i++)
        {
            for (var j = i + 1; j < pieces.Count; j++)
            {
                Assert.True(Apart(pieces[i].Item2, pieces[j].Item2), $"{pieces[i].Item1} overlaps {pieces[j].Item1}");
            }
        }

        // "Esc: skip" at the right of the bottom row stays clear of the last button.
        var escWidth = (int)Small("Esc: skip");
        var escBox = new PixelBox(layout.Panel.Right - 40 - escWidth, layout.Panel.Bottom - 44, escWidth, 18);
        Assert.True(layout.Buttons.All(b => Apart(b.Box, escBox)), "the Esc hint overlaps a button");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheInventoryGridHoldsItsSlotsInsideThePanelAndTheCanvasAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        var canvas = Canvas(windowWidth, windowHeight, scale);
        var window = Whole(canvas);
        var layout = new InventoryLayout(canvas.Width, canvas.Height, 40);

        Assert.True(Inside(layout.Panel, window), $"the panel {layout.Panel} is outside the canvas {canvas}");
        var cells = Enumerable.Range(0, 40).Select(layout.Cell).ToList();
        Assert.All(cells, cell => Assert.True(Inside(cell, layout.Panel), $"a slot {cell} is outside the panel {layout.Panel}"));
        for (var i = 0; i < cells.Count; i++)
        {
            for (var j = i + 1; j < cells.Count; j++)
            {
                Assert.True(Apart(cells[i], cells[j]), $"slots {i} and {j} overlap");
            }
        }

        // The key hints (at the bottom of the panel) are clear of the last row, and the longest item name fits a slot, or is cut with an ellipsis.
        Assert.True(cells[^1].Bottom + 2 <= layout.Panel.Bottom - 26, "the last row reaches the key hints");
        output.WriteLine($"{canvas.Width}x{canvas.Height}: slots {layout.CellWidth} x {layout.CellHeight}, compact {layout.Compact}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheCraftingPanelHoldsItsRowsAndTheStationColumnOnlyWhereThereIsRoomAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        var canvas = Canvas(windowWidth, windowHeight, scale);
        var window = Whole(canvas);
        var layout = new CraftingLayout(canvas.Width, canvas.Height, 4);
        var panel = new PixelBox(layout.X, layout.Y, layout.Width, layout.Height);
        Assert.True(Inside(panel, window), $"the panel {panel} is outside the canvas {canvas}");
        for (var i = 0; i < layout.RowCount; i++)
        {
            Assert.True(Inside(new PixelBox(layout.RowX, layout.RowY(i), layout.RowWidth, layout.RowHeight), panel));
        }

        // The recipe row of the game: "antorcha", "0/2 madera", the station "Uses mesa" and the status "Needs mesa".
        var ingredientsEnd = 50 + 130 + (int)Small("0/2 madera") + 6 + 14;
        var statusWidth = (int)Small("Missing material");
        var stationWidth = (int)Small("Uses mesa");
        var station = CraftingLayout.StationX(layout.RowWidth, ingredientsEnd, stationWidth, statusWidth);
        if (station is { } x)
        {
            Assert.True(x >= ingredientsEnd && x + stationWidth <= layout.RowWidth - 10 - statusWidth - 12, "the station column runs into the ingredients or the status");
        }

        output.WriteLine($"{canvas.Width}x{canvas.Height}: rows {layout.RowWidth} px wide, station column {(station is null ? "left out" : "at " + station)}");
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheControlsHelpAndTheClosingCardFitTheCanvasOrScrollOrCutAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        var canvas = Canvas(windowWidth, windowHeight, scale);
        var window = Whole(canvas);

        // The controls: 21 rows of one to three lines each.
        var heights = Enumerable.Range(0, 21).Select(static i => (((i % 3) + 1) * 20) + 6).ToList();
        for (var scroll = 0; scroll <= heights.Count; scroll++)
        {
            var layout = new ControlsLayout(canvas.Width, canvas.Height, heights, scroll);
            Assert.True(Inside(layout.Panel, window), $"the controls panel {layout.Panel} is outside the canvas");
            Assert.True(layout.Count >= 1);
            var used = heights.Skip(layout.First).Take(layout.Count).Sum();
            Assert.True(ControlsLayout.HeaderHeight + used + ControlsLayout.FooterHeight <= layout.Panel.Height, "the rows shown do not fit the panel");
            if (layout.MaxScroll == 0)
            {
                Assert.Equal(heights.Count, layout.Count);
            }
            else
            {
                Assert.True(layout.Scrolls);
                var last = new ControlsLayout(canvas.Width, canvas.Height, heights, layout.MaxScroll);
                Assert.Equal(heights.Count, last.First + last.Count); // scrolled to the end, the last control is shown
            }
        }

        // The closing card: the summary gives up its last rows until the card fits; the line offering the sleep (V12c) is a row over the keys and stays.
        var ending = new EndingLayout(canvas.Width, canvas.Height, 2, 2, 9);
        Assert.True(Inside(ending.Box, window), $"the closing card {ending.Box} is outside the canvas");
        if (ending.SummaryCut)
        {
            Assert.True(ending.SummaryShown < 9);
        }

        var offering = new EndingLayout(canvas.Width, canvas.Height, 2, 2, 9, sleepRows: 1);
        Assert.True(Inside(offering.Box, window), $"the closing card with the sleep {offering.Box} is outside the canvas");
        Assert.True(offering.SummaryShown <= ending.SummaryShown);
        Assert.True(Small(English.Value.Ui("sleepCard", ("session", "2"))) <= offering.Box.Width - (2 * EndingLayout.Side), "the sleep's line is wider than the card");

        output.WriteLine($"{canvas.Width}x{canvas.Height}: controls max scroll {new ControlsLayout(canvas.Width, canvas.Height, heights, 0).MaxScroll}, closing card {ending.Box.Height} px tall with {ending.SummaryShown} of 9 summary rows");
    }

    // ---- The checklist, the toast and the labels.

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheChecklistAndTheToastStayInsideTheCanvasAndApartAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        var canvas = Canvas(windowWidth, windowHeight, scale);
        var window = Whole(canvas);
        // The checklist as the game builds it: the title, then each step as a word, its English and its count, with a dot between steps (6.1 px a character).
        var pieces = new List<(string Text, int Width, bool Step)>
        {
            ("Campamento", 61, false), (" (camp):", 49, false),
            ("madera", 37, true), (" (wood)", 43, false), (" 0/5", 24, false), (" · ", 18, false),
            ("frutas", 37, true), (" (fruit)", 49, false), (" 0/3", 24, false), (" · ", 18, false),
            ("antorcha", 49, true), (" (torch)", 49, false), (" 0/1", 24, false), (" · ", 18, false),
            ("agua", 24, true), (" (water)", 49, false), (" 0/1", 24, false),
        };
        var rows = HudLayout.ObjectiveRows(pieces, static p => p.Width, static p => p.Step, (float)(canvas.Width * ObjectiveLayer.WidthShare), canvas.Width - (2 * HudLayout.Margin) - 16);
        Assert.True(rows.Count <= HudLayout.MaxObjectiveRows);
        Assert.Equal(pieces, rows.SelectMany(static r => r).ToList()); // no piece is lost
        foreach (var row in rows.Skip(1))
        {
            Assert.True(row[0].Step, "a row after the title starts on a step: a step is not split from its English and its count");
        }

        var widest = rows.Max(static r => r.Sum(static p => p.Width));
        var box = HudLayout.ObjectiveBox(canvas.Width, widest, rows.Count);
        Assert.True(Inside(box, window), $"the checklist {box} is outside the canvas {canvas}");
        Assert.True(widest + 16 <= canvas.Width - (2 * HudLayout.Margin), $"a checklist row of {widest} px does not fit the canvas");

        // The workbench panel (168 x 244) stands under the checklist and above the hotbar, at every number of checklist rows.
        var hotbar = new PixelBox((canvas.Width - 516) / 2, canvas.Height - HudLayout.HotbarTop, 516, 48);
        foreach (var checklistRows in new[] { 1, 2, 3 })
        {
            var checklist = HudLayout.ObjectiveBox(canvas.Width, 400, checklistRows);
            var recipe = HudLayout.RecipePanelBox(canvas.Width, canvas.Height, 168, 244, checklist.Bottom);
            Assert.True(Inside(recipe, window), $"the workbench panel {recipe} is outside the canvas {canvas}");
            Assert.True(Apart(recipe, checklist), $"the workbench panel overlaps a checklist of {checklistRows} rows");
            Assert.True(Apart(recipe, hotbar), "the workbench panel overlaps the hotbar");
        }

        foreach (var text in new[] { "¡Nueva palabra: el árbol!", "¡Nueva palabra: el árbol! Habla con la guía en el campamento y pídele agua, por favor." })
        {
            var (toast, toastRows) = HudLayout.Toast(canvas.Width, text, Small);
            Assert.True(Inside(toast, window), $"the toast {toast} is outside the canvas");
            Assert.All(toastRows, row => Assert.True(Small(row) + (2 * HudLayout.ToastPadding) <= toast.Width, $"toast row '{row}' is wider than the toast"));
            Assert.True(Apart(toast, box), "the toast overlaps the checklist");
            Assert.Equal(text, string.Join(' ', toastRows));
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ACalledAndAnNpcLabelStayInsideTheCanvasAtEveryScale(int windowWidth, int windowHeight, int scale)
    {
        var canvas = Canvas(windowWidth, windowHeight, scale);
        var window = Whole(canvas);
        var panel = DialogueLayout.PanelBox(canvas.Width, canvas.Height);
        var text = Small("campamento  111 blocks");
        foreach (var (ax, ay) in new[] { (0.0, 0.0), (canvas.Width * 1.0, canvas.Height * 1.0), (canvas.Width * 0.5, 4.0), (4.0, canvas.Height * 0.5), (canvas.Width - 4.0, canvas.Height * 0.9) })
        {
            var label = Called.Place(canvas.Width, canvas.Height, ax, ay, text, [panel]);
            Assert.True(Inside(label, window), $"the label {label} for an arrow at {ax},{ay} is outside the canvas");
            Assert.True(Apart(label, panel), "the label overlaps the panel");

            var onScreen = Called.Clamp(new PixelBox((int)ax - 90, (int)ay - 11, (int)text + 12, 22), canvas.Width, canvas.Height);
            Assert.True(Inside(onScreen, window), $"the on-screen label {onScreen} is outside the canvas");
        }
    }

    // ---- The contrast of the text.

    [Fact]
    public void TheTextTheInterfaceDrawsMeetsTheContrastWcagAsksForOnItsSurface()
    {
        var failures = new List<string>();
        foreach (var pair in UiPalette.TextPairs)
        {
            var onPanel = pair.Text.ContrastWith(pair.Back.Colour);
            var overWorld = pair.Text.ContrastWith(pair.Back.OverWhite);
            var needed = pair.Large ? 3.0 : 4.5;
            output.WriteLine($"{pair.Name,-30} {onPanel,5:0.00}:1 on its panel, {overWorld,5:0.00}:1 where a bright sky shows through (needs {needed})");
            if (onPanel < needed)
            {
                failures.Add($"{pair.Name} {onPanel:0.00}");
            }

            // Where the panel is translucent over a bright world the ratio falls; it still holds 4.5 for the dim lines.
            if (pair.Back.Alpha < 255 && overWorld < needed)
            {
                failures.Add($"{pair.Name} over a bright sky {overWorld:0.00}");
            }
        }

        Assert.Empty(failures);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TheLessonsPagesLinesOfWhenAndWhereFitTheNarrowLeftPage(int windowWidth, int windowHeight, int scale)
    {
        // V12c: the line under a lesson not begun is its own indented row, whole on the left page at every scale (r16-text-scale: 800 x 600 at 150 %), for every lesson
        // in each of its four waits: the next, another unlocked, its session to come, the lesson before it not done. A row of the list is cut with "...", never wrapped, so it is measured
        // with the book's own font (PixelOperator at its native size; the stand-in of the other checks is wider than it) against the row's room: the page less the
        // indent and the gutter.
        var font = new FontProvider(Repo.Path("assets", "fonts", "pixel-operator", "PixelOperator.ttf")).Get(FontProvider.NativeSize);
        var canvas = Canvas(windowWidth, windowHeight, scale);
        var g = new BookGeometry(canvas.Width, canvas.Height);
        var room = g.ContentRight - g.ContentLeft - BookLayer.IndentWidth - BookLayer.Gutter;
        var session = RouteScene.NewSessionWithShippedLessons(TestGame.Bundle, TestGame.Nouns);
        var record = session.Lessons.Record;
        var widest = (Text: string.Empty, Width: 0f);
        foreach (var lesson in session.Lessons.Book.Lessons)
        {
            var waits = new List<LessonWait> { new(LessonWaitKind.Open, lesson.Session, lesson.Npc), new(LessonWaitKind.Meet, lesson.Session, lesson.Npc) };
            if (lesson.After is { } after)
            {
                waits.Add(new LessonWait(LessonWaitKind.After, lesson.Session, lesson.Npc, after));
            }

            foreach (var wait in waits)
            {
                var lines = BookPages.Lessons(English.Value, record, session.Phrases, null, new Dictionary<string, LessonWait> { [lesson.Id] = wait });
                var why = lines[lines.ToList().FindIndex(l => l.Text == lesson.Title) + 1];
                var width = Vocaterra.Game.Graphics.DrawHelpers.Width(font, why.Text);
                Assert.True(width <= room, $"“{why.Text}” ({width} px) is wider than the left page's {room} px at {canvas.Width}x{canvas.Height}");
                widest = width > widest.Width ? (why.Text, width) : widest;
            }
        }

        output.WriteLine($"{canvas.Width}x{canvas.Height}: the widest line, “{widest.Text}”, is {widest.Width} px of the row's {room} px");
    }

    [Fact]
    public void TheBookRibbonLabelsAreWhiteAndReadOnTheirRibbonLitOrNot()
    {
        for (var page = 0; page < UiPalette.Ribbons.Count; page++)
        {
            var ribbon = UiPalette.Ribbons[page];
            Assert.True(UiPalette.RibbonLabel.ContrastWith(ribbon) >= 4.5, $"ribbon {page} {UiPalette.RibbonLabel.ContrastWith(ribbon):0.00}");
            Assert.True(UiPalette.RibbonLabel.ContrastWith(ribbon.Lighten(UiPalette.ActiveRibbonLighten)) >= 4.5, $"lit ribbon {page} {UiPalette.RibbonLabel.ContrastWith(ribbon.Lighten(UiPalette.ActiveRibbonLighten)):0.00}");
        }

        Assert.Equal(BookTabs.Labels(RouteScene.Shipped.Locale).Count, UiPalette.Ribbons.Count);
    }

    [Fact]
    public void TheContrastRatioIsTheWcagFormula()
    {
        Assert.Equal(21.0, new Rgb(255, 255, 255).ContrastWith(new Rgb(0, 0, 0)), 3);
        Assert.Equal(1.0, new Rgb(120, 120, 120).ContrastWith(new Rgb(120, 120, 120)), 6);
        Assert.Equal(new Rgb(127, 127, 127), new Rgb(0, 0, 0).Over(128, new Rgb(255, 255, 255)));
        Assert.Equal(0.2126, new Rgb(255, 0, 0).Luminance, 4);
    }

    // ---- The font floor.

    [Fact]
    public void NoTextIsDrawnSmallerThanTheFontsNativeSize()
    {
        Assert.Equal(FontProvider.NativeSize, UiScale.MinFontSize);
        var sources = Directory.EnumerateFiles(Repo.Path("src", "Vocaterra.Game"), "*.cs", SearchOption.TopDirectoryOnly).ToList();
        Assert.NotEmpty(sources);
        var smallest = int.MaxValue;
        foreach (var file in sources)
        {
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(file), @"private const int (?:\w*Size)\s*=\s*(\d+);"))
            {
                smallest = Math.Min(smallest, int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        Assert.True(smallest >= UiScale.MinFontSize, $"a font size constant of {smallest} px is under the minimum {UiScale.MinFontSize}");
    }
}
