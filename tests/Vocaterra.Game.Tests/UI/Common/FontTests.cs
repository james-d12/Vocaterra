using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Dialogue;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Startup;
using Vocaterra.Game.Tests.TestSupport;
using Vocaterra.Game.UI.Dialogue;
using Vocaterra.Game.UI.Overlays;

namespace Vocaterra.Game.Tests.UI.Common;

/// <summary>The committed font must be able to draw Spanish, and must be recorded as CC0 (D-game-5).</summary>
public sealed class FontTests
{
    private const string SpanishLetters = "áéíóúüñÁÉÍÓÚÜÑ¿¡";

    private static string CommittedFont => Repo.Path("assets", "fonts", "pixel-operator", "PixelOperator.ttf");

    [Fact]
    public void TheCommittedFontCoversEverySpanishLetter()
    {
        var cmap = TrueTypeCmap.Load(CommittedFont);
        var missing = SpanishLetters.Where(c => !cmap.Covers(c)).Select(c => $"U+{(int)c:X4} {c}").ToList();
        Assert.True(missing.Count == 0, "The font has no glyph for: " + string.Join(", ", missing));
    }

    [Fact]
    public void TheFontCoversPlainLettersDigitsAndPunctuation()
    {
        var cmap = TrueTypeCmap.Load(CommittedFont);
        foreach (var c in "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,:;?!()-'\"/")
        {
            Assert.True(cmap.Covers(c), $"The font has no glyph for '{c}'.");
        }
    }

    [Fact]
    public void TheFontCoversEveryCharacterOfThePanelAndTheGuidesTexts()
    {
        var cmap = TrueTypeCmap.Load(CommittedFont);
        var strings = new List<string>
        {
            DialoguePanel.KeyHints(RouteScene.Shipped.Locale, RouteScene.Shipped.Words.Typing), RouteScene.Shipped.Objectives.Find("camp")!.Title!, "New word", "audio", "Done: madera 5/5", "Campamento: madera 5/5 · frutas 3/3 · antorcha 1/1 · agua 0/1", RouteScene.Shipped.Locale.Ui("craftingTitle"), RouteScene.Shipped.Locale.Ui("craftingHints"), "Make", "Needs mesa", "Inventory full", "Missing material", "A mano", "Usa", "You: ", "Guía: ", "Esc: quitar la flecha", "understood, not understood", "Tendero: ", "Aldeano: ",
            DialoguePanel.ListeningText(RouteScene.Shipped.Locale), DialoguePanel.ThinkingText(RouteScene.Shipped.Locale), DialoguePanel.SpokenPrefix(RouteScene.Shipped.Locale), LoadingLayer.Text(RouteScene.Shipped.Locale), PushToTalkSpeechCapture.NoMicrophone, PushToTalkSpeechCapture.Silence,
            "heard at 96 %, Could not open the microphone: ", "Speech model vosk-model-small-es-0.42 is missing from /ruta. You can still type.", "Speech off: the recogniser is not loaded. Type your answer.", "I didn't hear anything.",
            // S7: the speech UX (the buttons, the heard-versus-expected line, the mic check, the typing offer).
            DialoguePanel.ButtonHints(RouteScene.Shipped.Locale), DialoguePanel.SpeechOffButtonHints(RouteScene.Shipped.Locale), DialoguePanel.SpeechOffKeyHints(RouteScene.Shipped.Locale, RouteScene.Shipped.Words.Typing), RouteScene.Shipped.Words.TypeOffer, "not understood",
            "Esc: skip", "Loading speech…", "Speech is not available.",
            string.Join(' ', SpeechButtons.For(RouteScene.Shipped.Words.Buttons).Select(static b => b.Text)), "[1] Quiero agua, por favor. [2] Ayuda. [3] Sí, por favor. No, gracias. Adiós. «…»",
        };

        // C08: the NPCs' labels and names (the panel's title, the label over the head, the beacon).
        strings.AddRange(RouteScene.Shipped.Npcs.All.SelectMany(static npc => new[] { npc.Label, npc.Called }));

        // Every template and prompt of the bundle, as written (the slot words are plain nouns the font covers as letters).
        var bundle = Repo.Path("content", "game-v1");
        using (var dialogue = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(bundle, "dialogue", "dialogue.json"))))
        {
            foreach (var pool in dialogue.RootElement.GetProperty("pools").EnumerateObject())
            {
                foreach (var variant in pool.Value.GetProperty("variants").EnumerateArray())
                {
                    strings.Add(variant.GetProperty("text").GetString()!);
                }
            }
        }

        using (var prompts = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(bundle, "prompts", "prompts.json"))))
        {
            foreach (var prompt in prompts.RootElement.EnumerateArray())
            {
                strings.Add(prompt.GetProperty("text").GetString()!);
            }
        }

        var missing = strings
            .SelectMany(s => s)
            .Where(c => c is not ('{' or '}' or ':' or '(' or ')') && !cmap.Covers(c))
            .Distinct()
            .Select(c => $"U+{(int)c:X4} {c}")
            .ToList();
        Assert.True(missing.Count == 0, "The font has no glyph for: " + string.Join(", ", missing));
    }

    [Fact]
    public void EveryCharacterOfEveryShippedLocaleIsInTheFontAndInTheLocalesGlyphList()
    {
        // content-plan 3.2 (C04): the support language's strings are data, so the font check reads them all, as they are drawn ({npc:…} and {key:…} filled).
        // locales.json lists each locale's characters beyond printable ASCII, the set a font for it must draw.
        var cmap = TrueTypeCmap.Load(CommittedFont);
        var bundle = LocaleBundle.Load(Repo.Path("content", "locale"));
        foreach (var info in bundle.Locales)
        {
            var locale = RouteScene.Shipped.Locale.Code == info.Code ? RouteScene.Shipped.Locale : bundle.Text(info.Code);
            var used = locale.Ids.Where(static id => !id.StartsWith("verb.", StringComparison.Ordinal)).SelectMany(id => locale.Find(id) ?? string.Empty)
                .Concat(locale.Verbs.Select(locale.Verb).SelectMany(static v => v!.Gloss + v.Base + v.Third + v.Past))
                .Where(static c => c is not ('\n' or '\r'))
                .ToHashSet();
            var missing = used.Where(c => !cmap.Covers(c)).Select(static c => $"U+{(int)c:X4} {c}").Order(StringComparer.Ordinal).ToList();
            Assert.True(missing.Count == 0, $"locale {info.Code}: the font has no glyph for {string.Join(", ", missing)}.");
            var unlisted = used.Where(static c => c > '~').Where(c => !info.Glyphs.Contains(c, StringComparison.Ordinal)).Select(static c => c.ToString()).Order(StringComparer.Ordinal).ToList();
            Assert.True(unlisted.Count == 0, $"locale {info.Code}: locales.json's glyphs do not list {string.Join(" ", unlisted)}.");
        }
    }

    [Fact]
    public void TheCmapReaderReportsAMissingGlyph()
    {
        var cmap = TrueTypeCmap.Load(CommittedFont);
        Assert.False(cmap.Covers(0x4E2D), "A CJK ideograph should not be in a 16 px Latin pixel font.");
    }

    [Fact]
    public void TheFontIsCopiedNextToTheExecutable()
    {
        Assert.True(File.Exists(AssetPaths.Font), $"The project file should copy the font to {AssetPaths.Font}.");
        Assert.Equal(File.ReadAllBytes(CommittedFont), File.ReadAllBytes(AssetPaths.Font));
    }

    [Fact]
    public void TheFontAndItsLicenceAreRecordedAsCc0()
    {
        var licence = File.ReadAllText(Repo.Path("assets", "fonts", "pixel-operator", "LICENSE.txt"));
        Assert.Contains("CC0 1.0 Universal", licence, StringComparison.Ordinal);

        var attribution = File.ReadAllText(Repo.Path("assets", "ATTRIBUTION.md"));
        Assert.Contains("PixelOperator.ttf", attribution, StringComparison.Ordinal);
        Assert.Contains("CC0", attribution, StringComparison.Ordinal);
        Assert.Contains("https://", attribution, StringComparison.Ordinal);
    }
}
