using System.Text.RegularExpressions;
using Vocaterra.Game.Core.Tests.Atmosphere;
using Vocaterra.Game.Core.Tests.TestSupport;

namespace Vocaterra.Game.Core.Tests.Architecture;

/// <summary>
/// The series rests on kinds being rows of data (E04, D-game-31): no file under <c>src/</c> names the id of a kind of the first data (as a string literal) except the default
/// data in <c>WeatherData.cs</c>, so a new kind is a row and no consumer switches on one.
/// </summary>
public sealed partial class NoWeatherKindIdsInSourceTests
{
    private const string DefaultData = "src/Vocaterra.Game.Core/Atmosphere/WeatherData.cs";

    [Fact]
    public void NoSourceFileNamesAKindOfTheFirstDataOutsideTheDefaultData()
    {
        var ids = WeatherTestData.Shipped.Kinds.Select(static k => k.Id).Append(WeatherTestData.Shipped.Kinds.Select(static k => k.Channels.PrecipitationKind).First(static p => p.Length > 0)).Distinct().ToList();
        Assert.Contains("sunny", ids);
        var found = new List<string>();
        foreach (var file in Directory.EnumerateFiles(Repo.Path("src"), "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(Repo.Root, file).Replace('\\', '/');
            if (relative.Contains("/obj/", StringComparison.Ordinal) || relative.Contains("/bin/", StringComparison.Ordinal) || relative == DefaultData)
            {
                continue;
            }

            var number = 0;
            foreach (var line in File.ReadLines(file))
            {
                number++;
                var code = Comment().Replace(line, string.Empty);
                found.AddRange(Quoted().Matches(code).Select(static m => m.Groups[1].Value).Where(ids.Contains).Select(id => $"{relative}:{number}: \"{id}\""));
            }
        }

        Assert.True(found.Count == 0, "A weather kind's id is named in the code; a kind is a row of game/weather.json:\n" + string.Join("\n", found));
    }

    [Fact]
    public void TheScanFindsAQuotedIdAndPassesCommentsAndLongerWords()
    {
        Assert.Single(Quoted().Matches("if (kind == \"rain\") {"));
        Assert.DoesNotContain(Quoted().Matches("var x = \"terrain\";"), static m => m.Groups[1].Value == "rain");
        Assert.Equal(string.Empty, Comment().Replace("  // \"rain\" is a kind", string.Empty).Trim());
    }

    [GeneratedRegex("\"([^\"\\\\]*)\"")]
    private static partial Regex Quoted();

    [GeneratedRegex("^\\s*(//|///).*$|\\s//\\s.*$")]
    private static partial Regex Comment();
}
