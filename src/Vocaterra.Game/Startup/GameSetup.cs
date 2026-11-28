using Vocaterra.Game.Core.Content;
using Vocaterra.Game.Core.Session;
using Vocaterra.Game.Core.World.Layout;
using Vocaterra.Game.Diagnostics;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Startup;

/// <summary>
/// What every world of a run shares (Q01): the content bundle and its game data, the locale bundle and the scene's layout, loaded once before the first window opens, so a missing or invalid bundle is a message and an exit
/// code, not a crash, and the title screen has the locale's words before any world is built. A world is then made from it by <see cref="WorldStart"/>, as many times as the run starts one.
/// </summary>
/// <param name="Directory">The bundle's folder.</param>
/// <param name="Bundle">The content bundle.</param>
/// <param name="Data">The game's content, read once from the bytes the bundle's load verified.</param>
/// <param name="Locales">The locale bundle.</param>
/// <param name="Layout">The scene's layout.</param>
/// <param name="LayoutFile">The layout's file, for the console.</param>
/// <param name="StartedMilliseconds">The steady clock when the load began (the first world's "loaded in" line counts from it).</param>
internal sealed record GameSetup(string Directory, ContentBundle Bundle, GameData Data, LocaleBundle Locales, SceneLayout Layout, string LayoutFile, double StartedMilliseconds)
{
    /// <summary>Loads the bundle, the locale and the layout for a run.</summary>
    /// <param name="options">The launch options.</param>
    /// <param name="usageError">A usage error found once the content is read (an NPC or a weather kind the command line names that the bundle lacks), or <see langword="null"/>.</param>
    /// <returns>The setup, or <see langword="null"/> when <paramref name="usageError"/> says why not.</returns>
    public static GameSetup? Load(LaunchOptions options, out string? usageError)
    {
        ArgumentNullException.ThrowIfNull(options);
        usageError = null;
        var started = MonotonicClock.Milliseconds();
        var directory = GameBundle.ResolveDirectory(options.ContentDirectory);
        var bundle = GameBundle.Load(directory);
        // Every game file is read once, from the bytes the bundle's load verified, and checked against the others (content-plan C02): the scene too,
        // unless --scene names a file of its own, which is read from disk and skips the bundle's check.
        var locales = LocaleBundle.Load(LocaleBundle.DirectoryBeside(directory));
        var data = GameData.Load(bundle, locales.Text(LocaleBundle.ChooseCode(options.Locale, bundle)), Vocaterra.Game.Graphics.Blocks.BlockAtlasLayout.IconKeys); // I01: an item whose icon has no art is reported at start
        if (!options.CheckTalkTargets(data.Npcs.Ids, out var talkError))
        {
            // The NPCs --talk may name are the bundle's (game/npcs.json), so the check waits for the roster: a usage error like the others.
            usageError = talkError;
            return null;
        }

        if (!options.CheckWeather([.. data.Weather.Kinds.Select(static kind => kind.Id)], out var weatherError))
        {
            // The kinds --weather and --set-weather may name are the bundle's (game/weather.json), so the check waits for the data: a usage error like the others (E03, E05).
            usageError = weatherError;
            return null;
        }

        Console.WriteLine($"locale: {data.Locale.Code}, bundle {locales.BundleHash[..12]} ({string.Join(", ", locales.Locales.Select(static l => l.Code))} shipped)");
        var layoutFile = SceneLocator.Resolve(options.SceneFile, directory);
        var layout = options.SceneFile is null ? data.Scene : SceneLayout.Load(layoutFile);
        if (options.SceneFile is not null)
        {
            Console.WriteLine($"scene: {layoutFile} is read from disk (--scene); it skips the bundle's hash check");
        }

        return new GameSetup(directory, bundle, data, locales, layout, layoutFile, started);
    }
}
