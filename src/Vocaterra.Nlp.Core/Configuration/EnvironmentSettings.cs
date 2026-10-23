namespace Vocaterra.Nlp.Core.Configuration;

/// <summary>
/// Reads the environment variables of the library, its tools and its tests. Every variable has a <c>VOCATERRA_</c> name; its former
/// <c>SLG_</c> (or <c>SLGFUZZ_</c>) name is read only when the new one is unset or blank. The former names are deprecated and will be
/// removed after one release.
/// </summary>
public static class EnvironmentSettings
{
    /// <summary>Returns the value of <paramref name="name"/>, else the value of the deprecated <paramref name="legacyName"/>; null when both are unset or blank.</summary>
    public static string? Get(string name, string legacyName, Func<string, string?>? getVariable = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrEmpty(legacyName);
        var lookup = getVariable ?? Environment.GetEnvironmentVariable;
        var value = lookup(name);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        value = lookup(legacyName);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
