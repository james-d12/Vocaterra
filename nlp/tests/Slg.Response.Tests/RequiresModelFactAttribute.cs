namespace Slg.Response.Tests;

/// <summary>A fact that is skipped unless <c>SLG_MODELS_DIR</c> is set and contains the named model directory.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresModelFactAttribute : FactAttribute
{
    /// <summary>Creates the attribute for a model directory name relative to <c>SLG_MODELS_DIR</c>.</summary>
    public RequiresModelFactAttribute(string modelDirectory)
    {
        var path = ModelPath(modelDirectory);
        if (path is null)
        {
            Skip = $"Set SLG_MODELS_DIR to a directory containing '{modelDirectory}' to run this test.";
        }
    }

    /// <summary>Returns the full path of the model directory, or null when the variable is unset or the directory is missing.</summary>
    public static string? ModelPath(string modelDirectory)
    {
        var root = Environment.GetEnvironmentVariable("SLG_MODELS_DIR");
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        var path = Path.Combine(root, modelDirectory);
        return Directory.Exists(path) ? path : null;
    }
}
