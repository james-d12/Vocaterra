using System.Globalization;
using System.Text;

namespace Vocaterra.Game.Core.Worlds;

/// <summary>Why a world name is refused (the New game page shows the reason).</summary>
public enum NameProblem
{
    /// <summary>The name is fine.</summary>
    None,

    /// <summary>Nothing but spaces was typed.</summary>
    Empty,

    /// <summary>The name is longer than <see cref="WorldNames.MaxLength"/>.</summary>
    TooLong,

    /// <summary>Another world has this name (compared without regard to case).</summary>
    Taken,
}

/// <summary>The rules for a world's name and for the folder it is kept in (Q01): cleaning, the default "World N", the check against the names taken, and the filesystem-safe folder name.</summary>
public static class WorldNames
{
    /// <summary>The longest a name may be.</summary>
    public const int MaxLength = 40;

    private const int MaxSlug = 40;

    /// <summary>The name as it is kept: control characters dropped, a run of white space one space, the ends trimmed.</summary>
    /// <param name="text">What was typed.</param>
    public static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                space = builder.Length > 0;
                continue;
            }

            if (char.IsControl(c))
            {
                continue;
            }

            if (space)
            {
                _ = builder.Append(' ');
                space = false;
            }

            _ = builder.Append(c);
        }

        return builder.ToString();
    }

    /// <summary>Whether <paramref name="text"/> can name a new world, given the names taken.</summary>
    /// <param name="text">What was typed.</param>
    /// <param name="taken">The names of the worlds there are.</param>
    public static NameProblem Check(string? text, IEnumerable<string> taken)
    {
        ArgumentNullException.ThrowIfNull(taken);
        var name = Clean(text);
        if (name.Length == 0)
        {
            return NameProblem.Empty;
        }

        if (name.Length > MaxLength)
        {
            return NameProblem.TooLong;
        }

        return taken.Any(other => string.Equals(Clean(other), name, StringComparison.OrdinalIgnoreCase)) ? NameProblem.Taken : NameProblem.None;
    }

    /// <summary>The default name of the next world: the name <paramref name="nameOf"/> makes ("World N" in English) for the smallest N from 1 that is not taken.</summary>
    /// <param name="taken">The names of the worlds there are.</param>
    /// <param name="nameOf">The default name of the world number N (the locale's <c>ui.titleDefaultName</c>).</param>
    public static string Next(IEnumerable<string> taken, Func<int, string> nameOf)
    {
        ArgumentNullException.ThrowIfNull(taken);
        ArgumentNullException.ThrowIfNull(nameOf);
        var names = taken.Select(Clean).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var n = 1; ; n++)
        {
            var candidate = Clean(nameOf(n));
            if (!names.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>
    /// A filesystem-safe version of a name: lower-case ASCII letters and digits, accents taken off, every other run of characters one hyphen, at most 40 characters, <c>world</c> when nothing is left.
    /// </summary>
    /// <param name="name">The world's name.</param>
    public static string Slug(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var builder = new StringBuilder();
        var hyphen = false;
        foreach (var c in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(c))
            {
                if (hyphen && builder.Length > 0)
                {
                    _ = builder.Append('-');
                }

                hyphen = false;
                _ = builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                hyphen = true;
            }
        }

        var slug = builder.Length > MaxSlug ? builder.ToString(0, MaxSlug).TrimEnd('-') : builder.ToString();
        return slug.Length == 0 ? "world" : slug;
    }

    /// <summary>A folder name for a world that no existing folder has: the slug, then the slug with <c>-2</c>, <c>-3</c> and so on.</summary>
    /// <param name="name">The world's name.</param>
    /// <param name="exists">Whether a folder of that name is there (compared without regard to case, as a case-insensitive disk would).</param>
    public static string UniqueFolder(string name, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        var slug = Slug(name);
        if (!exists(slug))
        {
            return slug;
        }

        for (var n = 2; ; n++)
        {
            var candidate = slug + "-" + n.ToString(CultureInfo.InvariantCulture);
            if (!exists(candidate))
            {
                return candidate;
            }
        }
    }
}
