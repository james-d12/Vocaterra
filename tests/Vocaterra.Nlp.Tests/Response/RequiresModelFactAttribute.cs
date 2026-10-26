using Vocaterra.Nlp.Core.Configuration;

namespace Vocaterra.Nlp.Response.Tests;

/// <summary>A fact that is skipped unless <c>VOCATERRA_MODELS_DIR</c> (or the deprecated <c>SLG_MODELS_DIR</c>) is set and contains the named model directory.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresModelFactAttribute : FactAttribute
{
    /// <summary>Creates the attribute for a model directory name relative to <c>VOCATERRA_MODELS_DIR</c>.</summary>
    public RequiresModelFactAttribute(string modelDirectory)
    {
        var path = ModelPath(modelDirectory);
        if (path is null)
        {
            Skip = $"Set VOCATERRA_MODELS_DIR to a directory containing '{modelDirectory}' to run this test.";
        }
    }

    /// <summary>Returns the full path of the model directory, or null when the variable is unset or the directory is missing.</summary>
    public static string? ModelPath(string modelDirectory)
    {
        var root = EnvironmentSettings.Get("VOCATERRA_MODELS_DIR", "SLG_MODELS_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        var path = Path.Combine(root, modelDirectory);
        return Directory.Exists(path) ? path : null;
    }
}
