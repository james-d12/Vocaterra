using System.Reflection;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// The commit the running game was built from (S21, the report's first row). The build stamps it as the assembly's <c>BuildCommit</c> metadata
/// (the project file reads the <c>VOCATERRA_BUILD_COMMIT</c> environment variable when the build starts); a run can also set that variable itself.
/// When neither is given the commit is <c>unknown</c>. To stamp a playtest build, set the variable to the commit before the publish, for example
/// <c>VOCATERRA_BUILD_COMMIT=$(git rev-parse HEAD) scripts/playtest-build.sh &lt;folder&gt;</c>.
/// </summary>
public static class BuildCommit
{
    /// <summary>The value when no build stamped the commit.</summary>
    public const string Unknown = "unknown";

    /// <summary>The environment variable that names the commit, read at build time (the project file) and at run time (this class).</summary>
    public const string Variable = "VOCATERRA_BUILD_COMMIT";

    /// <summary>The commit the game was built from, or <see cref="Unknown"/>.</summary>
    /// <param name="assembly">The assembly to read the metadata of (the game's own by default).</param>
    /// <param name="environment">Reads an environment variable (the process's by default).</param>
    public static string Read(Assembly? assembly = null, Func<string, string?>? environment = null)
    {
        var stamped = (assembly ?? typeof(BuildCommit).Assembly)
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(static attribute => attribute.Key == "BuildCommit")?.Value;
        if (!string.IsNullOrWhiteSpace(stamped))
        {
            return stamped.Trim();
        }

        var read = environment ?? (static name => Environment.GetEnvironmentVariable(name));
        var fromEnvironment = read(Variable);
        return string.IsNullOrWhiteSpace(fromEnvironment) ? Unknown : fromEnvironment.Trim();
    }
}
