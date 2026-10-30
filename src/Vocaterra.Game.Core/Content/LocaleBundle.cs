using System.Text;
using System.Text.Json;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Content;

/// <summary>One locale the locale bundle ships (<c>locales.json</c>).</summary>
/// <param name="Code">The BCP-47 code (<c>en</c>): the folder of its files.</param>
/// <param name="Name">The locale's name in itself (<c>English</c>).</param>
/// <param name="Glyphs">The characters beyond printable ASCII that its strings use (the font must draw them).</param>
public sealed record LocaleInfo(string Code, string Name, string Glyphs);

/// <summary>
/// The support-language bundle <c>content/locale</c> (CONTENT_PLAN 3.1, C03): beside <c>content/game-v1</c> and outside its hash, so an edit of the
/// learner's language changes no save, no fixture and no golden. It has its own <c>manifest.json</c> (written by
/// <c>scripts/content.sh hash content/locale --bundle-id locale</c>); every file is checked against it at load, and a file that differs is refused.
/// </summary>
/// <remarks>
/// The layout: <c>locales.json</c> (the shipped locales) and one folder per locale, <c>&lt;code&gt;/*.json</c>, each file a
/// <c>{ "strings": { ... } }</c> map (<see cref="LocaleText.Parse"/>). The default locale, <see cref="DefaultCode"/>, is every other locale's fallback.
/// </remarks>
public sealed class LocaleBundle
{
    /// <summary>The bundle's folder name, beside the game's bundle (<c>content/locale</c>).</summary>
    public const string FolderName = "locale";

    /// <summary>The bundle id its manifest carries.</summary>
    public const string BundleId = "locale";

    /// <summary>The default locale: the fallback of every other, and the one a bundle without a <c>supportLanguage</c> uses.</summary>
    public const string DefaultCode = "en";

    /// <summary>The list of the shipped locales, inside the bundle folder.</summary>
    public const string LocalesFile = "locales.json";

    private readonly Dictionary<string, LocaleText> _texts = new(StringComparer.Ordinal);

    private LocaleBundle(string directory, ContentManifest manifest, IReadOnlyList<LocaleInfo> locales)
    {
        Directory = directory;
        Manifest = manifest;
        Locales = locales;
    }

    /// <summary>The bundle's folder.</summary>
    public string Directory { get; }

    /// <summary>The bundle's manifest (its files and their hashes).</summary>
    public ContentManifest Manifest { get; }

    /// <summary>The bundle's hash (the session log and the report record it beside the game bundle's).</summary>
    public string BundleHash => Manifest.BundleHash;

    /// <summary>The shipped locales, in the file's order.</summary>
    public IReadOnlyList<LocaleInfo> Locales { get; }

    /// <summary>The locale bundle's folder beside a game bundle's (<c>content/game-v1</c> gives <c>content/locale</c>).</summary>
    /// <param name="gameBundleDirectory">The game bundle's folder.</param>
    public static string DirectoryBeside(string gameBundleDirectory)
    {
        ArgumentNullException.ThrowIfNull(gameBundleDirectory);
        var parent = System.IO.Directory.GetParent(Path.GetFullPath(gameBundleDirectory))?.FullName
            ?? throw new DirectoryNotFoundException($"{gameBundleDirectory} has no parent folder for the locale bundle.");
        return Path.Combine(parent, FolderName);
    }

    /// <summary>Loads the bundle and checks every file against its manifest.</summary>
    /// <param name="directory">The bundle's folder (<c>content/locale</c>).</param>
    /// <exception cref="DirectoryNotFoundException">The folder does not exist.</exception>
    /// <exception cref="InvalidDataException">The manifest is missing, a file differs from it or is not in it, or <c>locales.json</c> is malformed.</exception>
    public static LocaleBundle Load(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        if (!System.IO.Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"The locale bundle {directory} was not found (it sits beside content/game-v1).");
        }

        var manifestPath = Path.Combine(directory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            throw new InvalidDataException($"The locale bundle {directory} has no manifest.json (scripts/content.sh refresh writes it).");
        }

        var manifest = ContentManifest.FromJson(File.ReadAllText(manifestPath, Encoding.UTF8));
        var problems = Differences(manifest, ContentManifest.Compute(directory));
        if (problems.Count > 0)
        {
            throw new InvalidDataException($"The locale bundle {directory} does not match its manifest (run scripts/content.sh refresh after an edit):\n  {string.Join("\n  ", problems)}");
        }

        return new LocaleBundle(Path.GetFullPath(directory), manifest, ParseLocales(File.ReadAllText(Path.Combine(directory, LocalesFile), Encoding.UTF8)));
    }

    /// <summary>The strings of a locale, with the default locale as its fallback (the default locale has none).</summary>
    /// <param name="code">The locale's code; one of <see cref="Locales"/>.</param>
    /// <exception cref="ArgumentException">The bundle does not ship the locale.</exception>
    /// <exception cref="InvalidDataException">A file of the locale is malformed.</exception>
    public LocaleText Text(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        if (_texts.TryGetValue(code, out var known))
        {
            return known;
        }

        if (!Locales.Any(locale => string.Equals(locale.Code, code, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"The locale bundle has no locale '{code}'; it ships {string.Join(", ", Locales.Select(static l => l.Code))}.", nameof(code));
        }

        var fallback = string.Equals(code, DefaultCode, StringComparison.Ordinal) ? null : Text(DefaultCode);
        var prefix = code + "/";
        var files = Manifest.Files
            .Where(file => file.Path.StartsWith(prefix, StringComparison.Ordinal) && file.Path.EndsWith(".json", StringComparison.Ordinal))
            .Select(file => (file.Path, File.ReadAllText(Path.Combine(Directory, file.Path), Encoding.UTF8)))
            .ToList();
        var text = LocaleText.Parse(code, BundleHash, files, fallback);
        _texts[code] = text;
        return text;
    }

    /// <summary>The locale a run uses: the one asked for, else the game bundle's <c>supportLanguage</c>, else <see cref="DefaultCode"/>.</summary>
    /// <param name="asked">The code given with <c>--locale</c>, or <see langword="null"/>.</param>
    /// <param name="gameBundle">The game's bundle.</param>
    public static string ChooseCode(string? asked, ContentBundle gameBundle)
    {
        ArgumentNullException.ThrowIfNull(gameBundle);
        return asked ?? gameBundle.Manifest.SupportLanguage ?? DefaultCode;
    }

    private static List<string> Differences(ContentManifest stored, ContentManifest computed)
    {
        var problems = new List<string>();
        var want = stored.Files.ToDictionary(static f => f.Path, StringComparer.Ordinal);
        var have = computed.Files.ToDictionary(static f => f.Path, StringComparer.Ordinal);
        foreach (var (path, file) in want.Where(entry => !have.ContainsKey(entry.Key)))
        {
            problems.Add($"{path}: in the manifest, not on disk ({file.Bytes} bytes)");
        }

        foreach (var path in have.Keys.Where(path => !want.ContainsKey(path)))
        {
            problems.Add($"{path}: on disk, not in the manifest");
        }

        foreach (var (path, file) in want.Where(entry => have.TryGetValue(entry.Key, out var other) && !string.Equals(other.Sha256, entry.Value.Sha256, StringComparison.Ordinal)))
        {
            problems.Add($"{path}: differs from the manifest");
        }

        return problems;
    }

    private static List<LocaleInfo> ParseLocales(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var locales = new List<LocaleInfo>();
            foreach (var element in document.RootElement.GetProperty("locales").EnumerateArray())
            {
                var code = element.GetProperty("code").GetString() ?? string.Empty;
                var name = element.GetProperty("name").GetString() ?? string.Empty;
                var glyphs = element.TryGetProperty("glyphs", out var g) ? g.GetString() ?? string.Empty : string.Empty;
                if (code.Length == 0 || name.Length == 0 || locales.Any(l => string.Equals(l.Code, code, StringComparison.Ordinal)))
                {
                    throw new InvalidDataException($"{LocalesFile}: a locale has no code or name, or its code '{code}' is given twice.");
                }

                locales.Add(new LocaleInfo(code, name, glyphs));
            }

            if (!locales.Any(static l => string.Equals(l.Code, DefaultCode, StringComparison.Ordinal)))
            {
                throw new InvalidDataException($"{LocalesFile}: the default locale '{DefaultCode}' is not shipped.");
            }

            return locales;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException($"{LocalesFile} is malformed: {ex.Message}", ex);
        }
    }
}
