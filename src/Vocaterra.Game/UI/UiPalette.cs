namespace Vocaterra.Game.UI;

/// <summary>A colour as three bytes, with the WCAG 2.x relative luminance and contrast ratio (X08). Plain values: no engine types.</summary>
/// <param name="R">Red.</param>
/// <param name="G">Green.</param>
/// <param name="B">Blue.</param>
public readonly record struct Rgb(byte R, byte G, byte B)
{
    /// <summary>The relative luminance, 0 (black) to 1 (white), as WCAG 2.x defines it.</summary>
    public double Luminance => (0.2126 * Channel(R)) + (0.7152 * Channel(G)) + (0.0722 * Channel(B));

    /// <summary>The contrast ratio of two colours, 1 to 21 (the lighter luminance plus 0.05 over the darker plus 0.05).</summary>
    /// <param name="other">The other colour.</param>
    public double ContrastWith(Rgb other)
    {
        var a = Luminance;
        var b = other.Luminance;
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    /// <summary>This colour drawn with <paramref name="alpha"/> (0 to 255) over <paramref name="behind"/>.</summary>
    /// <param name="alpha">The opacity.</param>
    /// <param name="behind">What it is drawn over.</param>
    public Rgb Over(int alpha, Rgb behind)
    {
        var a = Math.Clamp(alpha, 0, 255) / 255.0;
        return new Rgb((byte)Math.Round((R * a) + (behind.R * (1 - a))), (byte)Math.Round((G * a) + (behind.G * (1 - a))), (byte)Math.Round((B * a) + (behind.B * (1 - a))));
    }

    /// <summary>This colour moved <paramref name="amount"/> (0 to 1) of the way to white.</summary>
    /// <param name="amount">How far.</param>
    public Rgb Lighten(double amount) => new((byte)(R + ((255 - R) * amount)), (byte)(G + ((255 - G) * amount)), (byte)(B + ((255 - B) * amount)));

    private static double Channel(byte value)
    {
        var c = value / 255.0;
        return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}

/// <summary>A surface text stands on: its colour and its opacity (a panel is drawn a little translucent over the world).</summary>
/// <param name="Colour">The colour.</param>
/// <param name="Alpha">The opacity, 0 to 255.</param>
public readonly record struct PaintedSurface(Rgb Colour, int Alpha)
{
    /// <summary>The colour the surface shows over a bright world (white): the worst case for light text.</summary>
    public Rgb OverWhite => Colour.Over(Alpha, new Rgb(255, 255, 255));
}

/// <summary>One pair of text colour and surface the interface draws, for the contrast test (X08).</summary>
/// <param name="Name">What the text is.</param>
/// <param name="Text">The text colour.</param>
/// <param name="Back">The surface under it.</param>
/// <param name="Large">Whether it is large text (24 px or more), for which WCAG asks 3:1 instead of 4.5:1.</param>
public readonly record struct TextPair(string Name, Rgb Text, PaintedSurface Back, bool Large = false);

/// <summary>
/// The colours of the interface's secondary text and the surfaces under it (X08). The layers take their dim lines from here, so the contrast test reads the colours the
/// game draws. The dim lines were small grey text on a dark panel (140, 140, 150 on 16, 16, 24: 5.7 to 1 on the panel itself, 4.8 to 1 where the panel lets a bright sky
/// through), and a few were under 4.5 to 1 (the report's placeholder 4.4, the paper book's unseen ink 3.3, the hotbar's key numbers over a bright world 4.2).
/// </summary>
public static class UiPalette
{
    /// <summary>The panels' and cards' own colours.</summary>
    public static PaintedSurface DialogueBack { get; } = new(new Rgb(16, 16, 24), 236);

    /// <summary>The support card and the lesson strip.</summary>
    public static PaintedSurface CardBack { get; } = new(new Rgb(12, 16, 30), 226);

    /// <summary>The check card, the mic check, the crafting panel and the inventory.</summary>
    public static PaintedSurface ScreenBack { get; } = new(new Rgb(18, 18, 34), 250);

    /// <summary>The checklist, the tips and the word card.</summary>
    public static PaintedSurface HudBack { get; } = new(new Rgb(10, 10, 20), 190);

    /// <summary>The hotbar's slots.</summary>
    public static PaintedSurface HotbarBack { get; } = new(new Rgb(22, 24, 30), 220);

    /// <summary>The recipe panel.</summary>
    public static PaintedSurface RecipeBack { get; } = new(new Rgb(18, 20, 26), 232);

    /// <summary>The paper of the book.</summary>
    public static PaintedSurface Paper { get; } = new(new Rgb(238, 227, 200), 255);

    /// <summary>The row the book's selection highlights.</summary>
    public static PaintedSurface PaperSelect { get; } = new(new Rgb(246, 218, 128), 255);

    /// <summary>The key hints, the notes and the dim lines on a dark panel.</summary>
    public static Rgb Muted { get; } = new(160, 160, 176);

    /// <summary>The first-time tip's small lines and the controls reminder (they were 180, 180, 200: 4.4 to 1 where a bright sky shows through the tip's panel).</summary>
    public static Rgb TipDim { get; } = new(200, 200, 215);

    /// <summary>The report's placeholder (it was 120, 120, 140).</summary>
    public static Rgb Placeholder { get; } = new(150, 150, 168);

    /// <summary>The hotbar's key numbers (they were 150, 154, 170).</summary>
    public static Rgb HotbarKey { get; } = new(190, 194, 208);

    /// <summary>The English of a step that is done in the checklist (it was 150, 190, 160).</summary>
    public static Rgb DoneEnglish { get; } = new(176, 214, 186);

    /// <summary>The unlearned word in the crafting panel and the inventory, before it takes its colour.</summary>
    public static Rgb Unseen { get; } = new(160, 160, 176);

    /// <summary>The ink of the paper book for a row not met yet (it was 138, 122, 102, 3.3 to 1 on the paper).</summary>
    public static Rgb InkUnseen { get; } = new(108, 92, 72);

    /// <summary>The ink of the paper book's hints and notes.</summary>
    public static Rgb InkHint { get; } = new(104, 90, 74);

    /// <summary>The ink of the paper book for a row met but not learned.</summary>
    public static Rgb InkSeen { get; } = new(78, 64, 52);

    /// <summary>The ink of the paper book's body text.</summary>
    public static Rgb Ink { get; } = new(44, 32, 26);

    /// <summary>The ink of the paper book for a known row.</summary>
    public static Rgb InkKnown { get; } = new(116, 70, 6);

    /// <summary>The colour of a book ribbon's label: white on every ribbon.</summary>
    public static Rgb RibbonLabel { get; } = new(255, 255, 255);

    /// <summary>How much lighter the lit ribbon is than the others.</summary>
    public const double ActiveRibbonLighten = 0.12;

    /// <summary>The ribbons, by page: nouns, verbs, phrases, lessons, what I said (darker than before, so the white label reads on the lit one too).</summary>
    public static IReadOnlyList<Rgb> Ribbons { get; } = [new(150, 46, 46), new(42, 92, 138), new(44, 102, 62), new(110, 62, 128), new(136, 88, 20)];

    /// <summary>The pairs the contrast test measures: the dark panels' dim lines and light lines, and the paper book's inks.</summary>
    public static IReadOnlyList<TextPair> TextPairs { get; } = BuildPairs();

    private static List<TextPair> BuildPairs()
    {
        var gold = new Rgb(255, 214, 64);
        var english = new Rgb(226, 176, 255);
        var pairs = new List<TextPair>
        {
            new("dialogue note", Muted, DialogueBack),
            new("dialogue player", new Rgb(150, 205, 255), DialogueBack),
            new("dialogue guide", new Rgb(255, 242, 180), DialogueBack),
            new("dialogue hint", gold, DialogueBack),
            new("dialogue english", english, DialogueBack),
            new("dialogue heard", new Rgb(190, 225, 255), DialogueBack),
            new("dialogue notice", new Rgb(255, 150, 120), DialogueBack),
            new("dialogue name", new Rgb(255, 204, 110), DialogueBack),
            new("button text", new Rgb(236, 236, 246), new PaintedSurface(new Rgb(34, 34, 58), 235)),
            new("report placeholder", Placeholder, DialogueBack),
            new("support card dim", Muted, CardBack),
            new("lesson strip english", english, CardBack),
            new("lesson strip heading", gold, CardBack),
            new("check card dim", Muted, ScreenBack),
            new("check card body", new Rgb(250, 246, 232), ScreenBack),
            new("mic check dim", Muted, ScreenBack),
            new("crafting hints", Muted, ScreenBack),
            new("crafting unseen word", Unseen, ScreenBack),
            new("inventory hints", Muted, ScreenBack),
            new("hotbar key", HotbarKey, HotbarBack),
            new("checklist open", new Rgb(245, 245, 245), HudBack),
            new("checklist done", new Rgb(120, 220, 130), HudBack),
            new("checklist done english", DoneEnglish, HudBack),
            new("checklist english", english, HudBack),
            new("tip dim", TipDim, new PaintedSurface(new Rgb(10, 10, 20), 205)),
            new("recipe soft", new Rgb(168, 170, 182), RecipeBack),
            new("book ink", Ink, Paper),
            new("book ink known", InkKnown, Paper),
            new("book ink seen", InkSeen, Paper),
            new("book ink unseen", InkUnseen, Paper),
            new("book ink hint", InkHint, Paper),
            new("book ink known on selection", InkKnown, PaperSelect),
            new("book ink hint on selection", InkHint, PaperSelect),
            new("book ink unseen on selection", InkUnseen, PaperSelect),
        };
        return pairs;
    }
}
