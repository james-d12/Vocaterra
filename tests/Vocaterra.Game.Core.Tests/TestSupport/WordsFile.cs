using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Language.Words;

namespace Vocaterra.Game.Core.Tests.TestSupport;

/// <summary>
/// The shipped <c>game/words.json</c> with one value replaced (T02b): a test gives the game another language's list (German <c>hallo</c>, <c>von</c>,
/// <c>die</c>) and checks that the code reads it, the way the typing map's test does. Only the value under the key changes; the rest of the file is the shipped one.
/// </summary>
internal static class WordsFile
{
    /// <summary>The words with the list (or the object) under <paramref name="key"/> replaced by <paramref name="valueJson"/>.</summary>
    /// <param name="key">A top-level key of the file whose value is an array or an object without nested brackets.</param>
    /// <param name="valueJson">The new value, as JSON.</param>
    public static GameWords With(string key, string valueJson)
    {
        var json = File.ReadAllText(Repo.Path("content", "game-v1", "game", "words.json"));
        var pattern = "\"" + Regex.Escape(key) + "\": (\\[[^\\]]*\\]|\\{[^}]*\\})";
        var replaced = Regex.Replace(json, pattern, "\"" + key + "\": " + valueJson.Replace("$", "$$", StringComparison.Ordinal));
        if (replaced == json)
        {
            throw new InvalidOperationException($"words.json has no value under '{key}' to replace.");
        }

        return GameWords.Parse(replaced);
    }

    /// <summary>The words with a list of plain words under <paramref name="key"/>.</summary>
    /// <param name="key">The key.</param>
    /// <param name="words">The words, in order.</param>
    public static GameWords WithList(string key, params string[] words) =>
        With(key, "[" + string.Join(", ", words.Select(static w => "\"" + w + "\"")) + "]");
}
