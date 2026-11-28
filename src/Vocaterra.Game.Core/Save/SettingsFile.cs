using System.Text.Json;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Game.Core.Save;

/// <summary>The settings file's document (Q01): the format, its version and the settings.</summary>
/// <param name="Format">The format name (<see cref="SettingsFile.FormatName"/>).</param>
/// <param name="Version">The format version (<see cref="SettingsFile.CurrentVersion"/>).</param>
/// <param name="Settings">The player's settings.</param>
public sealed record SettingsDocument(string Format, int Version, SaveSettings Settings);

/// <summary>
/// The one global settings file (Q01): the player's settings (sound, graphics, interface, mouse, needs, cheats) belong to the machine, not to a world, and the title screen
/// edits them before any world exists, so they are kept here (<c>~/.local/share/vocaterra/settings.json</c> by default) and no longer in a save. The values are
/// <see cref="SaveSettings"/>, the record the settings page has always been kept as, so the same limits apply when they are read. A missing, unreadable or foreign file is
/// no file: the game starts with the defaults and the next change writes a good one. No path is chosen here: the front-end gives it, so a test never touches the real file.
/// </summary>
public static class SettingsFile
{
    /// <summary>The format name every settings file carries.</summary>
    public const string FormatName = "vocaterra-settings";

    /// <summary>The format version this code writes and reads.</summary>
    public const int CurrentVersion = 1;

    /// <summary>The file's name in the game's data folder.</summary>
    public const string FileName = "settings.json";

    /// <summary>The text of a settings file: the settings limited to their ranges, as canonical JSON.</summary>
    /// <param name="settings">The settings.</param>
    public static string Serialize(SaveSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return CanonicalJson.Serialize(new SettingsDocument(FormatName, CurrentVersion, settings.Clamped()));
    }

    /// <summary>The settings a file's text holds (limited to their ranges), or <see langword="null"/> when the text is not a settings file of this version.</summary>
    /// <param name="json">The text.</param>
    public static SaveSettings? Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            var document = CanonicalJson.Deserialize<SettingsDocument>(json);
            return string.Equals(document.Format, FormatName, StringComparison.Ordinal) && document.Version == CurrentVersion ? document.Settings.Clamped() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Reads the settings file at <paramref name="path"/>, or <see langword="null"/> when there is none or it cannot be read (the defaults then apply).</summary>
    /// <param name="path">The file.</param>
    public static SaveSettings? Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        try
        {
            return File.Exists(path) ? Parse(SaveFile.Read(path)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Writes the settings to <paramref name="path"/> (a temporary file, then a rename, as a save is written), creating the folder.</summary>
    /// <param name="path">The file.</param>
    /// <param name="settings">The settings.</param>
    /// <exception cref="IOException">The write failed.</exception>
    public static void Write(string path, SaveSettings settings)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        SaveFile.Write(path, Serialize(settings));
    }
}
