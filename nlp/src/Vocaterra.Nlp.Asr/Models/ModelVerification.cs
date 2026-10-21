namespace Vocaterra.Nlp.Asr.Models;

/// <summary>Outcome of verifying one model directory against the manifest.</summary>
public enum ModelStatus
{
    /// <summary>Every file is present and matches its pinned hash.</summary>
    Ok,

    /// <summary>The model is unknown, its directory is absent, or at least one file is absent.</summary>
    Missing,

    /// <summary>At least one present file differs from its pinned hash or size.</summary>
    Corrupt,

    /// <summary>All files are present but at least one has no pinned hash yet (or the entry lists no files).</summary>
    Unpinned,
}

/// <summary>Outcome of verifying a single file.</summary>
public enum FileStatus
{
    /// <summary>The file matches its pinned hash.</summary>
    Ok,

    /// <summary>The file is absent or unreadable.</summary>
    Missing,

    /// <summary>The file differs from its pinned hash or size.</summary>
    Corrupt,

    /// <summary>The file is present but the manifest has no hash for it.</summary>
    Unpinned,
}

/// <summary>Verification result of one manifest file.</summary>
/// <param name="Path">Manifest-relative path.</param>
/// <param name="Status">Outcome for this file.</param>
/// <param name="ExpectedSha256">The pinned hash, or null when unpinned.</param>
/// <param name="ActualSha256">The recomputed hash, or null when it was not computed (absent file, size mismatch, unpinned).</param>
public sealed record FileVerification(string Path, FileStatus Status, string? ExpectedSha256, string? ActualSha256);

/// <summary>Verification report for one model: overall status plus per-file results in ordinal path order.</summary>
/// <param name="Id">The model id that was verified.</param>
/// <param name="Status">Overall outcome; Corrupt outranks Missing, which outranks Unpinned.</param>
/// <param name="Files">Per-file results.</param>
public sealed record ModelVerification(string Id, ModelStatus Status, IReadOnlyList<FileVerification> Files)
{
    /// <summary>True when <see cref="Status"/> is <see cref="ModelStatus.Ok"/>.</summary>
    public bool IsOk => Status == ModelStatus.Ok;

    /// <summary>Structural equality including the file results.</summary>
    public bool Equals(ModelVerification? other)
    {
        return other is not null
            && string.Equals(Id, other.Id, StringComparison.Ordinal)
            && Status == other.Status
            && Files.SequenceEqual(other.Files);
    }

    /// <summary>Hash consistent with <see cref="Equals(ModelVerification)"/>.</summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Status, Files.Count);
    }
}
