using System.Globalization;
using System.Text;
using Slg.Core.Determinism;

namespace Slg.Asr;

/// <summary>
/// Prepares the directory Vosk actually loads. Stock Vosk models leave Kaldi's default <c>--dither=1.0</c> in force, which adds
/// Gaussian noise drawn from a time-seeded generator to every frame, so decoding the same clip twice gives different scores and
/// sometimes a different n-best order. The runtime directory links every model file except <c>conf/mfcc.conf</c>, which is
/// rewritten with <c>--dither=0.0</c>; decoding is then bit-for-bit repeatable.
/// </summary>
internal static class VoskRuntimeModel
{
    /// <summary>The feature option that makes decoding repeatable.</summary>
    public const string DitherOption = "--dither=0.0";

    private const string RuntimeVersion = "vosk-runtime-1";
    private static readonly string MfccRelativePath = Path.Combine("conf", "mfcc.conf");
    private static int _stagingCounter;

    /// <summary>
    /// Returns a directory that holds the model with dithering disabled, creating it below <paramref name="runtimeRoot"/> (default:
    /// a folder in the temp directory) when it does not exist yet. The directory name derives from the model hash and the patched
    /// configuration, so equal models share one directory across calls and processes. Models without <c>conf/mfcc.conf</c> are
    /// returned unchanged.
    /// </summary>
    public static string Prepare(string modelDir, string modelHash, string? runtimeRoot = null)
    {
        var mfccPath = Path.Combine(modelDir, MfccRelativePath);
        if (!File.Exists(mfccPath))
        {
            return modelDir;
        }

        var patched = PatchMfccConfig(File.ReadAllText(mfccPath));
        var key = DeterministicHash.Combine(RuntimeVersion, modelHash, patched)[..32];
        var root = runtimeRoot ?? Path.Combine(Path.GetTempPath(), "slg-vosk-runtime");
        var target = Path.Combine(root, key);
        var files = Directory.EnumerateFiles(modelDir, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(modelDir, file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (IsComplete(target, files, patched))
        {
            return target;
        }

        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }

        Directory.CreateDirectory(root);
        var staging = target + ".tmp" + Environment.ProcessId.ToString(CultureInfo.InvariantCulture)
            + "-" + Interlocked.Increment(ref _stagingCounter).ToString(CultureInfo.InvariantCulture);
        try
        {
            Populate(staging, modelDir, files, patched);
            try
            {
                Directory.Move(staging, target);
            }
            catch (IOException) when (IsComplete(target, files, patched))
            {
                // Another process finished the same directory first; use its copy.
            }
        }
        finally
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }

        return target;
    }

    /// <summary>Returns the configuration text with every existing dither setting removed and <see cref="DitherOption"/> appended.</summary>
    public static string PatchMfccConfig(string config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var builder = new StringBuilder();
        foreach (var raw in config.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("--dither", StringComparison.Ordinal))
            {
                continue;
            }

            builder.Append(line).Append('\n');
        }

        return builder.Append(DitherOption).Append('\n').ToString();
    }

    private static bool IsComplete(string target, string[] files, string patched)
    {
        if (!Directory.Exists(target))
        {
            return false;
        }

        // File.Exists follows links, so a link whose model file has gone away counts as missing.
        foreach (var file in files)
        {
            if (!File.Exists(Path.Combine(target, file)))
            {
                return false;
            }
        }

        return string.Equals(File.ReadAllText(Path.Combine(target, MfccRelativePath)), patched, StringComparison.Ordinal);
    }

    private static void Populate(string staging, string modelDir, string[] files, string patched)
    {
        foreach (var file in files)
        {
            var destination = Path.Combine(staging, file);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (string.Equals(file, MfccRelativePath, StringComparison.Ordinal))
            {
                File.WriteAllText(destination, patched, new UTF8Encoding(false));
                continue;
            }

            var source = Path.GetFullPath(Path.Combine(modelDir, file));
            try
            {
                File.CreateSymbolicLink(destination, source);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                File.Copy(source, destination, overwrite: true);
            }
        }
    }
}
