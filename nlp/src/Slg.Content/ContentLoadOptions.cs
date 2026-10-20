namespace Slg.Content;

/// <summary>Options of <see cref="ContentLoader.Load(string, ContentLoadOptions?)"/>.</summary>
public sealed record ContentLoadOptions
{
    /// <summary>The default options: hashes are verified.</summary>
    public static ContentLoadOptions Default { get; } = new();

    /// <summary>When true (the default) every file hash, the file list and the bundle hash must match the manifest.</summary>
    public bool VerifyHashes { get; init; } = true;
}
