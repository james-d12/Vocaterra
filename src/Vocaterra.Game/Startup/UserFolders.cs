using Vocaterra.Game.Core.Save;

namespace Vocaterra.Game.Startup;

/// <summary>
/// Where the game keeps what is the player's, under <c>~/.local/share/vocaterra/</c> (Q01): the worlds (<c>saves/&lt;world&gt;/</c>), the one global settings file (<c>settings.json</c>) and, as before, the session logs
/// (<c>sessions/</c>). Each can be moved for a run, so a test or a scripted run never touches the real folder: the <c>--saves-dir</c> and <c>--settings-file</c> options, then the
/// <c>VOCATERRA_SAVES_DIR</c> and <c>VOCATERRA_SETTINGS_FILE</c> environment variables.
/// </summary>
public static class UserFolders
{
    /// <summary>The environment variable that moves the saves folder.</summary>
    public const string SavesVariable = "VOCATERRA_SAVES_DIR";

    /// <summary>The environment variable that moves the settings file.</summary>
    public const string SettingsVariable = "VOCATERRA_SETTINGS_FILE";

    /// <summary>The game's data folder: <c>~/.local/share/vocaterra</c>.</summary>
    public static string DataRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "vocaterra");

    /// <summary>The folder of the worlds for a run: <c>--saves-dir</c>, else <c>VOCATERRA_SAVES_DIR</c>, else <c>~/.local/share/vocaterra/saves</c>.</summary>
    /// <param name="options">The launch options.</param>
    public static string Saves(LaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.SavesDir ?? Variable(SavesVariable) ?? Path.Combine(DataRoot, "saves");
    }

    /// <summary>
    /// The settings file for a run, or <see langword="null"/> when the run keeps none: <c>--settings-file</c>, else <c>VOCATERRA_SETTINGS_FILE</c>, else <c>~/.local/share/vocaterra/settings.json</c>; a scripted run
    /// (a screenshot) uses the default file never, so its picture is the same on every machine, but a file it is told of.
    /// </summary>
    /// <param name="options">The launch options.</param>
    public static string? SettingsPath(LaunchOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var named = options.SettingsFile ?? Variable(SettingsVariable);
        if (named is not null)
        {
            return named;
        }

        return options.IsScreenshotRun ? null : Path.Combine(DataRoot, SettingsFile.FileName);
    }

    private static string? Variable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
