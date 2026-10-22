using System.Text;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// The file edge of the save (S10): the save's text goes to a file and a file's text comes back, as UTF-8 without a byte order mark. The write
/// goes to a temporary file and then replaces the save, so a quit in the middle of a write leaves the previous save whole.
/// </summary>
public static class SaveFile
{
    private static readonly UTF8Encoding Encoding = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Writes the save's text to a file, replacing it.</summary>
    /// <param name="path">The file.</param>
    /// <param name="text">The save's canonical JSON.</param>
    public static void Write(string path, string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(text);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, text, Encoding);
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Reads a save file's text.</summary>
    /// <param name="path">The file.</param>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    public static string Read(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return File.ReadAllText(path, Encoding);
    }
}
