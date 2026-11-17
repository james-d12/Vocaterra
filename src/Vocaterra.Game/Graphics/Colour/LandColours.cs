using System.Text.Json;
using Vocaterra.Game.Graphics.Far;
using Vocaterra.Game.Core.Voxel;

namespace Vocaterra.Game.Graphics.Colour;

/// <summary>
/// The colour of the land's plants across the climate (B22, G2): grass and oak leaves take their colour from a small map of the column's ground temperature and humidity (<c>land-colours.json</c>, data: the chunk mesher and the far
/// view both read it, so a meadow is the same green near and far), instead of one lime green everywhere. The block art is the light, nearly neutral end of every green (a vertex colour can only darken a picture); the map's colour is
/// what the art is multiplied by, per channel (the target over the art's average). No biome is named: the colour is a function of the climate, and the biome table is the one that also reads the climate.
/// </summary>
public sealed class LandColours
{
    /// <summary>How much of a grass block's tint its side faces take: the side is the dirt under a thin green rim, so the dirt is only a little greened.</summary>
    public const float SideShare = 0.55f;

    private readonly int[] _temperature;
    private readonly int[] _humidity;
    private readonly (float R, float G, float B)[] _grass;
    private readonly (float R, float G, float B)[] _leaves;
    private readonly (byte R, byte G, byte B)[] _grassColour;
    private readonly (byte R, byte G, byte B)[] _leafColour;

    private LandColours(int[] temperature, int[] humidity, (byte R, byte G, byte B)[] grass, (byte R, byte G, byte B)[] leaves, (byte R, byte G, byte B) grassArt, (byte R, byte G, byte B) leafArt)
    {
        _temperature = temperature;
        _humidity = humidity;
        _grassColour = grass;
        _leafColour = leaves;
        _grass = [.. grass.Select(c => Divide(c, grassArt))];
        _leaves = [.. leaves.Select(c => Divide(c, leafArt))];
    }

    private static readonly Lazy<LandColours> Shared = new(() => Parse(ReadData(), FarPalette.Of(BlockId.Grass), FarPalette.Of(BlockId.Leaves)), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The map the game ships (<c>land-colours.json</c>, embedded), made once against the block art's average colours.</summary>
    public static LandColours Default => Shared.Value;

    /// <summary>The temperature of the map's middle (the neutral colour: used where no climate is known).</summary>
    public const int NeutralTemperature = 0;

    /// <summary>The humidity of the map's middle.</summary>
    public const int NeutralHumidity = 0;

    /// <summary>The multiplier of the grass picture at a ground temperature and humidity (each channel 0 to 1).</summary>
    /// <param name="temperature">The ground temperature (-4096 to 4096).</param>
    /// <param name="humidity">The humidity (-4096 to 4096).</param>
    public (float R, float G, float B) GrassTint(int temperature, int humidity) => Sample(_grass, temperature, humidity);

    /// <summary>The multiplier of the oak leaves' picture at a ground temperature and humidity.</summary>
    /// <param name="temperature">The ground temperature.</param>
    /// <param name="humidity">The humidity.</param>
    public (float R, float G, float B) LeafTint(int temperature, int humidity) => Sample(_leaves, temperature, humidity);

    /// <summary>The colour grass shows in full light at a ground temperature and humidity (the map's own, before the art is divided out).</summary>
    /// <param name="temperature">The ground temperature.</param>
    /// <param name="humidity">The humidity.</param>
    public (byte R, byte G, byte B) GrassColour(int temperature, int humidity) => ToByte(Sample(_grassColour.Select(static c => (c.R / 255f, c.G / 255f, c.B / 255f)).ToArray(), temperature, humidity));

    /// <summary>The colour oak leaves show in full light at a ground temperature and humidity.</summary>
    /// <param name="temperature">The ground temperature.</param>
    /// <param name="humidity">The humidity.</param>
    public (byte R, byte G, byte B) LeafColour(int temperature, int humidity) => ToByte(Sample(_leafColour.Select(static c => (c.R / 255f, c.G / 255f, c.B / 255f)).ToArray(), temperature, humidity));

    /// <summary>A multiplier brought a share of the way to white: what a face that is only partly tinted takes (<see cref="SideShare"/>).</summary>
    /// <param name="tint">The full tint.</param>
    /// <param name="share">The share of it kept, 0 to 1.</param>
    public static (float R, float G, float B) Partly((float R, float G, float B) tint, float share) =>
        (1f + ((tint.R - 1f) * share), 1f + ((tint.G - 1f) * share), 1f + ((tint.B - 1f) * share));

    private static (float R, float G, float B) Divide((byte R, byte G, byte B) target, (byte R, byte G, byte B) art) =>
        (Math.Min(1f, target.R / (float)Math.Max(1, (int)art.R)), Math.Min(1f, target.G / (float)Math.Max(1, (int)art.G)), Math.Min(1f, target.B / (float)Math.Max(1, (int)art.B)));

    private static (byte R, byte G, byte B) ToByte((float R, float G, float B) c) => ((byte)Math.Round(c.R * 255f), (byte)Math.Round(c.G * 255f), (byte)Math.Round(c.B * 255f));

    // The bilinear lookup in the grid: rows by humidity, columns by temperature, a value outside the nodes taking the edge.
    private (float R, float G, float B) Sample((float R, float G, float B)[] grid, int temperature, int humidity)
    {
        var (t0, t1, tf) = Locate(_temperature, temperature);
        var (h0, h1, hf) = Locate(_humidity, humidity);
        var w = _temperature.Length;
        var a = Lerp(grid[(h0 * w) + t0], grid[(h0 * w) + t1], tf);
        var b = Lerp(grid[(h1 * w) + t0], grid[(h1 * w) + t1], tf);
        return Lerp(a, b, hf);
    }

    private static (float R, float G, float B) Lerp((float R, float G, float B) a, (float R, float G, float B) b, float t) =>
        (a.R + ((b.R - a.R) * t), a.G + ((b.G - a.G) * t), a.B + ((b.B - a.B) * t));

    private static (int Low, int High, float Fraction) Locate(int[] nodes, int value)
    {
        if (value <= nodes[0])
        {
            return (0, 0, 0f);
        }

        var last = nodes.Length - 1;
        if (value >= nodes[last])
        {
            return (last, last, 0f);
        }

        var i = 1;
        while (nodes[i] < value)
        {
            i++;
        }

        return (i - 1, i, (value - nodes[i - 1]) / (float)(nodes[i] - nodes[i - 1]));
    }

    /// <summary>Reads a map and finds what is wrong with it, or nothing (the data's validation: the loader throws with the first problem, the test lists them all).</summary>
    /// <param name="json">The text of <c>land-colours.json</c>.</param>
    /// <param name="grassArt">The average colour of the grass picture.</param>
    /// <param name="leafArt">The average colour of the leaf picture.</param>
    public static IReadOnlyList<string> Validate(string json, (byte R, byte G, byte B) grassArt, (byte R, byte G, byte B) leafArt)
    {
        var problems = new List<string>();
        try
        {
            var data = JsonSerializer.Deserialize<Data>(json, Options);
            if (data is null)
            {
                return ["the file holds no data"];
            }

            Check(data, grassArt, leafArt, problems, out _);
        }
        catch (JsonException ex)
        {
            problems.Add("not valid JSON: " + ex.Message);
        }

        return problems;
    }

    /// <summary>Builds a map from its text, throwing <see cref="InvalidDataException"/> with the first problem when it is not valid.</summary>
    /// <param name="json">The text of <c>land-colours.json</c>.</param>
    /// <param name="grassArt">The average colour of the grass picture.</param>
    /// <param name="leafArt">The average colour of the leaf picture.</param>
    public static LandColours Parse(string json, (byte R, byte G, byte B) grassArt, (byte R, byte G, byte B) leafArt)
    {
        var data = JsonSerializer.Deserialize<Data>(json, Options) ?? throw new InvalidDataException("land-colours.json holds no data");
        var problems = new List<string>();
        Check(data, grassArt, leafArt, problems, out var colours);
        if (problems.Count > 0)
        {
            throw new InvalidDataException("land-colours.json: " + problems[0]);
        }

        return new LandColours(data.TemperatureNodes, data.HumidityNodes, colours!.Value.Grass, colours.Value.Leaves, grassArt, leafArt);
    }

    private static void Check(Data data, (byte R, byte G, byte B) grassArt, (byte R, byte G, byte B) leafArt, List<string> problems, out ((byte R, byte G, byte B)[] Grass, (byte R, byte G, byte B)[] Leaves)? colours)
    {
        colours = null;
        var temperature = data.TemperatureNodes ?? [];
        var humidity = data.HumidityNodes ?? [];
        CheckNodes("temperatureNodes", temperature, problems);
        CheckNodes("humidityNodes", humidity, problems);
        var grass = CheckGrid("grass", data.Grass, temperature.Length, humidity.Length, grassArt, problems);
        var leaves = CheckGrid("leaves", data.Leaves, temperature.Length, humidity.Length, leafArt, problems);
        if (problems.Count == 0)
        {
            colours = (grass, leaves);
        }
    }

    private static void CheckNodes(string name, int[] nodes, List<string> problems)
    {
        if (nodes.Length < 2)
        {
            problems.Add($"{name} needs at least two values");
            return;
        }

        for (var i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] is < -4096 or > 4096)
            {
                problems.Add($"{name}[{i}] = {nodes[i]} is outside -4096 to 4096");
            }

            if (i > 0 && nodes[i] <= nodes[i - 1])
            {
                problems.Add($"{name} must rise: {nodes[i - 1]} then {nodes[i]}");
            }
        }
    }

    private static (byte R, byte G, byte B)[] CheckGrid(string name, string[][]? rows, int columns, int expectedRows, (byte R, byte G, byte B) art, List<string> problems)
    {
        var all = new List<(byte R, byte G, byte B)>();
        if (rows is null || rows.Length != expectedRows)
        {
            problems.Add($"{name} needs {expectedRows} rows (one per humidity node), has {rows?.Length ?? 0}");
            return [];
        }

        for (var r = 0; r < rows.Length; r++)
        {
            if (rows[r].Length != columns)
            {
                problems.Add($"{name} row {r} needs {columns} colours (one per temperature node), has {rows[r].Length}");
                continue;
            }

            for (var c = 0; c < columns; c++)
            {
                var text = rows[r][c];
                if (text.Length != 7 || text[0] != '#' || !int.TryParse(text.AsSpan(1), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value))
                {
                    problems.Add($"{name}[{r}][{c}] = '{text}' is not #rrggbb");
                    continue;
                }

                var colour = ((byte)(value >> 16), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF));
                if (colour.Item1 > art.R || colour.Item2 > art.G || colour.Item3 > art.B)
                {
                    problems.Add($"{name}[{r}][{c}] = {text} is lighter than the block art's average ({art.R}, {art.G}, {art.B}) in a channel: a picture can only be darkened, so make the colour darker or the art lighter");
                }

                all.Add(colour);
            }
        }

        return [.. all];
    }

    private static string ReadData()
    {
        using var stream = typeof(LandColours).Assembly.GetManifestResourceStream("Vocaterra.Game.Graphics.Colour.land-colours.json") ?? throw new InvalidOperationException("land-colours.json is not embedded");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };

    private sealed class Data
    {
        public int[] TemperatureNodes { get; set; } = [];

        public int[] HumidityNodes { get; set; } = [];

        public string[][]? Grass { get; set; }

        public string[][]? Leaves { get; set; }
    }
}
