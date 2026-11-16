using Vocaterra.Game.Core.Content;

namespace Vocaterra.Game.Core.World.Pass;

/// <summary>The checks of the <c>pass</c> section of <c>game/world.json</c> that need the other files (B08b): every required lesson exists, the rockfall is a named prop of the scene and the edge a site of it.</summary>
internal static class PassValidator
{
    private const string File = WorldGenData.RelativePath;

    /// <summary>Every problem of the pass's data, in a fixed order; empty when it holds together.</summary>
    /// <param name="data">The game's content.</param>
    public static IReadOnlyList<string> Validate(GameData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var pass = data.Pass;
        var problems = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in pass.RequiredLessons)
        {
            if (!seen.Add(id))
            {
                problems.Add($"{File}: pass.requiredLessons lists '{id}' twice.");
            }
            else if (data.Lessons.Find(id) is null)
            {
                problems.Add($"{File}: pass.requiredLessons names the lesson '{id}', which game/lessons.json does not have.");
            }
        }

        if (pass.RequiredLessons.Count == 0)
        {
            return problems; // no pass section: nothing to close, nothing to check
        }

        if (!data.Scene.Props.Any(prop => string.Equals(prop.Name, pass.Rockfall, StringComparison.Ordinal)))
        {
            problems.Add($"{File}: pass.rockfall '{pass.Rockfall}' is not a named prop of game/scene.json.");
        }

        if (data.Scene.Site(pass.Edge) is null)
        {
            problems.Add($"{File}: pass.edge '{pass.Edge}' is not a site of game/scene.json.");
        }

        return problems;
    }
}
