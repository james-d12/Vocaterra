namespace Vocaterra.Game.Core.Save;

/// <summary>
/// The save profiles (T04, D-game-27 item 3): one folder per target language, <c>saves/&lt;language&gt;/</c>, where the autosave and F6 write. A player learning two
/// languages keeps two progress records; the learner's skill ids stay unqualified inside a profile, and a save names its language, so a load of another
/// language's save is refused (<see cref="SaveGame.Check"/>).
/// </summary>
public static class SaveProfile
{
    /// <summary>The name of the folder that holds the profiles.</summary>
    public const string FolderName = "saves";

    /// <summary>The file F6 writes in an ordinary run with no <c>--save</c> file.</summary>
    public const string ManualFileName = "manual.save";

    /// <summary>The folder the profiles are kept in when <c>--saves-dir</c> is not given: <c>~/.local/share/vocaterra/saves</c>.</summary>
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "vocaterra", FolderName);

    /// <summary>The folder of a language's profile: <c>&lt;root&gt;/&lt;language&gt;</c>.</summary>
    /// <param name="root">The folder of the profiles.</param>
    /// <param name="language">The target language's tag.</param>
    public static string ProfileFolder(string root, string language)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        if (language.AsSpan().IndexOfAny(['/', '\\']) >= 0 || language is "." or "..")
        {
            throw new ArgumentException($"'{language}' is not a language tag.", nameof(language));
        }

        return Path.Combine(root, language);
    }

    /// <summary>The autosave's file in a profile folder (<see cref="AutosaveFiles.FileName"/>).</summary>
    /// <param name="profileDirectory">The profile's folder.</param>
    public static string AutosavePath(string profileDirectory) => Path.Combine(profileDirectory, AutosaveFiles.FileName);

    /// <summary>The file F6 writes in a profile folder when the run has no <c>--save</c> file (<see cref="ManualFileName"/>).</summary>
    /// <param name="profileDirectory">The profile's folder.</param>
    public static string ManualPath(string profileDirectory) => Path.Combine(profileDirectory, ManualFileName);

    /// <summary>Makes a profile's folder (the first save of a language finds none); a write into a folder that vanished later still fails, as <see cref="SaveFile.Write"/> always did.</summary>
    /// <param name="profileDirectory">The profile's folder.</param>
    public static void Ensure(string profileDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(profileDirectory);
        Directory.CreateDirectory(profileDirectory);
    }
}
