using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vocaterra.Game.Core.Tests.TestSupport;
using Vocaterra.Game.Core.World;
using Vocaterra.Game.Core.World.Options;

namespace Vocaterra.Game.Core.Tests.World.Options;

/// <summary>
/// The terrain options as data (Q02): the shipped rows load and hold together, a choice of values becomes one canonical <see cref="WorldOptions"/> (the defaults are the empty set, the same choice in any order is the same
/// digest, an unknown id or a value off the grid is refused with a message that says what is allowed), every option at its default leaves <c>game/world.json</c>'s numbers exactly as they are, and each option moves
/// the generator values its row names and no others.
/// </summary>
public sealed class TerrainOptionDataTests
{
    private static TerrainOptionData Table => TestGame.Data.TerrainOptions;

    private static WorldGenData World => TestGame.Data.WorldGen;

    private static string Json(WorldGenData world) => JsonSerializer.Serialize(world);

    [Fact]
    public void Given_TheShippedRows_When_TheyAreRead_Then_TheyHoldTogetherAndAreSlidersOnAGrid()
    {
        Assert.Equal(["mountains", "hills", "ocean", "continents", "rivers"], Table.Options.Select(static o => o.Id));
        Assert.Empty(TerrainOptionValidator.Validate(Table, World, TestGame.Data.Biomes));
        foreach (var option in Table.Options)
        {
            Assert.False(string.IsNullOrWhiteSpace(option.Label));
            Assert.True(option.OnGrid(option.Default), option.Id);
            Assert.InRange(option.Stops, 3, 16); // a slider with a few stops, not a field
            Assert.NotEmpty(option.Effects);
        }

        Assert.Equal("Normal", Table.Find("ocean")!.Show(1));
        Assert.Equal("150%", Table.Find("mountains")!.Show(150));
    }

    [Fact]
    public void Given_NoChoice_When_TheOptionsAreMade_Then_TheyAreTheDefaultsHoweverTheDefaultsAreWritten()
    {
        Assert.True(Table.Create([]).IsDefault);
        Assert.True(Table.Create(Table.Options.Select(static o => new KeyValuePair<string, int>(o.Id, o.Default))).IsDefault);
        Assert.True(Table.TryParse("mountains=100, hills=100", out var written, out _));
        Assert.Same(WorldOptions.Default, written);
        Assert.Equal(string.Empty, WorldOptions.Default.Canonical);
        Assert.Equal("defaults", WorldOptions.Default.ToString());
    }

    [Fact]
    public void Given_TheSameChoiceInAnotherOrder_When_TheOptionsAreMade_Then_TheyAreEqualWithOneDigest()
    {
        var a = Table.Create([new("hills", 50), new("mountains", 150)]);
        var b = Table.Create([new("mountains", 150), new("hills", 50)]);
        Assert.Equal(a, b);
        Assert.Equal("hills=50;mountains=150", a.Canonical);
        Assert.Equal(a.Digest, b.Digest);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes("hills=50;mountains=150")), a.Digest); // the digest is the SHA-256 of the canonical text: it never changes
        Assert.Equal(Convert.ToHexString(a.Digest).ToLowerInvariant(), a.Key);
        Assert.NotEqual(a.Key, Table.Create([new("hills", 50)]).Key);
        Assert.NotEqual(a.Key, Table.Create([new("hills", 75), new("mountains", 150)]).Key);
        Assert.Equal(150, a.Get("mountains"));
        Assert.Null(a.Get("ocean"));
    }

    [Theory]
    [InlineData("volcanoes=50", "there is no terrain option 'volcanoes'", "mountains, hills, ocean, continents, rivers")]
    [InlineData("mountains=110", "takes a value from 25 to 150 in steps of 25", "110 is not one")]
    [InlineData("mountains=175", "takes a value from 25 to 150", "175 is not one")]
    [InlineData("ocean=4", "takes a value from 0 to 3 in steps of 1", "4 is not one")]
    [InlineData("hills=-25", "takes a value from 0 to 200", "-25 is not one")]
    [InlineData("hills=50,hills=75", "is given twice", "hills")]
    [InlineData("mountains", "is not <id>=<whole number>", "mountains")]
    [InlineData("mountains=high", "is not <id>=<whole number>", "mountains=high")]
    [InlineData("=50", "is not <id>=<whole number>", "=50")]
    public void Given_ARefusedChoice_When_ItIsParsed_Then_TheMessageSaysWhatIsAllowed(string text, string first, string second)
    {
        Assert.False(Table.TryParse(text, out var options, out var error));
        Assert.Null(options);
        Assert.Contains(first, error, StringComparison.Ordinal);
        Assert.Contains(second, error, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_AnIdTheContentLacksOrAValueOffTheGrid_When_TheOptionsAreCreated_Then_ItIsRefused()
    {
        var unknown = Assert.Throws<ArgumentException>(() => Table.Create([new("volcanoes", 1)]));
        Assert.Contains("volcanoes", unknown.Message, StringComparison.Ordinal);
        var off = Assert.Throws<ArgumentException>(() => Table.Create([new("mountains", 110)]));
        Assert.Contains("110", off.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Given_EveryOptionAtItsDefault_When_ItIsApplied_Then_TheNumbersAreExactlyTheShippedOnes()
    {
        var shipped = Json(World);
        foreach (var option in Table.Options)
        {
            Assert.Equal(shipped, Json(TerrainOptionData.ApplyOne(World, option, option.Default)));
        }

        Assert.Same(World, Table.Apply(World, WorldOptions.Default)); // the defaults give the very same data
    }

    [Fact]
    public void Given_EachOption_When_ItIsMoved_Then_ItChangesItsTargetsAndNothingElse()
    {
        var peaks = World.Height.Peaks.Select(static p => p.Y).ToArray();
        var half = Table.Apply(World, Table.Create([new("mountains", 50)]));
        Assert.Equal(peaks.Select(static y => (int)Math.Round(y / 2.0, MidpointRounding.AwayFromZero)), half.Height.Peaks.Select(static p => p.Y));
        Assert.Equal(World.Height.Peaks.Select(static p => p.X), half.Height.Peaks.Select(static p => p.X)); // the inputs stay: only the heights scale
        Assert.Equal(Json(World with { Height = World.Height with { Peaks = half.Height.Peaks } }), Json(half));

        var flat = Table.Apply(World, Table.Create([new("hills", 0)]));
        Assert.Equal((0, 0), (flat.Height.Detail.CalmAmplitude, flat.Height.Detail.RuggedAmplitude));
        Assert.Equal(World.Height.Detail.HillsCell, flat.Height.Detail.HillsCell);

        var rolling = Table.Apply(World, Table.Create([new("hills", 200)]));
        Assert.Equal((World.Height.Detail.CalmAmplitude * 2, World.Height.Detail.RuggedAmplitude * 2), (rolling.Height.Detail.CalmAmplitude, rolling.Height.Detail.RuggedAmplitude));

        var seas = Table.Apply(World, Table.Create([new("ocean", 3)]));
        Assert.Equal(3, seas.Climate.Continents!.SeaPerBlock);
        Assert.Equal(World.Climate.Continents!.Cell, seas.Climate.Continents.Cell);

        var small = Table.Apply(World, Table.Create([new("continents", 50)]));
        Assert.Equal(World.Climate.Continents!.Cell / 2, small.Climate.Continents!.Cell);
        Assert.Equal(World.Climate.Continents.Channel, small.Climate.Continents.Channel); // the channels keep their width

        Assert.Equal(World.Height.DrainageData!.SourceArea / 2, Table.Apply(World, Table.Create([new("rivers", 200)])).Height.DrainageData!.SourceArea); // more rivers: a lower source area
        Assert.Equal(World.Height.DrainageData.SourceArea * 4, Table.Apply(World, Table.Create([new("rivers", 25)])).Height.DrainageData!.SourceArea);

        // No option names the vertical range, the sea level or the regions' blend, so the valley and its pass fit at every value.
        var everything = Table.Apply(World, Table.Create(Table.Options.Select(static o => new KeyValuePair<string, int>(o.Id, o.Max))));
        Assert.Equal(World.Vertical, everything.Vertical);
        Assert.Equal(JsonSerializer.Serialize(World.Blend), JsonSerializer.Serialize(everything.Blend));
        Assert.Equal(World.Altitude, everything.Altitude);
    }

    [Theory]
    [InlineData(25, 3, 1)]
    [InlineData(50, 3, 2)]
    [InlineData(75, 3, 2)]
    [InlineData(100, 3, 3)]
    [InlineData(150, 235, 353)] // 352.5 rounds away from zero
    [InlineData(100, -7, -7)]
    [InlineData(50, -7, -4)]
    public void Given_AScale_When_ItIsPutThroughAValue_Then_TheResultIsRoundedToTheNearestWholeNumberAwayFromZero(int percent, int value, int expected) =>
        Assert.Equal(expected, TerrainTarget.Function(TerrainMode.Scale, percent)(value));

    [Theory]
    [InlineData(100, 400, 400)]
    [InlineData(200, 400, 200)]
    [InlineData(25, 400, 1600)]
    [InlineData(150, 400, 267)]
    public void Given_AnInverseScale_When_ItIsPutThroughAValue_Then_TheValueFallsAsThePercentRises(int percent, int value, int expected) =>
        Assert.Equal(expected, TerrainTarget.Function(TerrainMode.ScaleInverse, percent)(value));

    [Fact]
    public void Given_OffsetAndSet_When_TheyArePutThroughAValue_Then_TheyAddAndReplace()
    {
        Assert.Equal(-380, TerrainTarget.Function(TerrainMode.Offset, 20)(-400));
        Assert.Equal(-400, TerrainTarget.Function(TerrainMode.Offset, 0)(-400));
        Assert.Equal(3, TerrainTarget.Function(TerrainMode.Set, 3)(1));
    }

    [Fact]
    public void Given_TheFileText_When_ItIsParsed_Then_TheRowsComeBackWithTheirEffects()
    {
        var table = TerrainOptionData.Parse("""
            { "schemaVersion": 1, "options": [
              { "id": "a", "label": "A", "min": 0, "max": 100, "step": 50, "default": 50, "hint": "H", "unit": "%", "labels": ["x", "y", "z"],
                "effects": [ { "target": "height.peaks", "mode": "scale" }, { "target": "height.drainage.sourceArea", "mode": "scaleInverse" } ] } ] }
            """);
        var row = Assert.Single(table.Options);
        Assert.Equal(("a", "A", "H", "%", 0, 100, 50, 50), (row.Id, row.Label, row.Hint, row.Unit, row.Min, row.Max, row.Step, row.Default));
        Assert.Equal(["x", "y", "z"], row.Labels);
        Assert.Equal([new TerrainEffect("height.peaks", TerrainMode.Scale), new TerrainEffect("height.drainage.sourceArea", TerrainMode.ScaleInverse)], row.Effects);
        Assert.Equal("y", row.Show(50));
        _ = Assert.Throws<InvalidDataException>(() => TerrainOptionData.Parse("""{ "schemaVersion": 2, "options": [] }"""));
        _ = Assert.Throws<InvalidDataException>(() => TerrainOptionData.Parse("""{ "schemaVersion": 1, "options": [ { "id": "a" } ] }"""));
        _ = Assert.Throws<InvalidDataException>(() => TerrainOptionData.Parse("""{ "schemaVersion": 1, "options": [ { "id": "a", "label": "A", "min": 0, "max": 1, "step": 1, "default": 0, "effects": [ { "target": "height.peaks", "mode": "twist" } ] } ] }"""));
    }

    // The validator ----------------------------------------------------------------------------------------------------------------------------

    private static TerrainOption Row(string id = "x", int min = 25, int max = 150, int step = 25, int @default = 100, string target = "height.peaks", TerrainMode mode = TerrainMode.Scale, IReadOnlyList<string>? labels = null) =>
        new(id, "Label", string.Empty, "%", min, max, step, @default, labels ?? [], [new TerrainEffect(target, mode)]);

    private static IReadOnlyList<string> Problems(params TerrainOption[] rows) =>
        TerrainOptionValidator.Validate(new TerrainOptionData(rows), World, TestGame.Data.Biomes);

    [Fact]
    public void Given_AGoodRow_When_ItIsValidated_Then_ThereIsNoProblem() => Assert.Empty(Problems(Row()));

    [Fact]
    public void Given_RowsThatDoNotHoldTogether_When_TheyAreValidated_Then_EachProblemIsNamed()
    {
        Assert.Contains("must be lower-case letters", Assert.Single(Problems(Row("Big Hills"))), StringComparison.Ordinal);
        Assert.Contains(Problems(Row("a"), Row("a", target: "height.detail.calmAmplitude")), static p => p.Contains("'a' is used twice", StringComparison.Ordinal));
        Assert.Contains(Problems(Row("a"), Row("b")), static p => p.Contains("both change 'height.peaks'", StringComparison.Ordinal));
        Assert.Contains(Problems(Row(step: 20)), static p => p.Contains("step of at least 1 that reaches the max", StringComparison.Ordinal));
        Assert.Contains(Problems(Row(min: 150, max: 25)), static p => p.Contains("needs a min below its max", StringComparison.Ordinal));
        Assert.Contains(Problems(Row(@default: 110)), static p => p.Contains("default of 110, which is not a stop", StringComparison.Ordinal));
        Assert.Contains(Problems(Row(labels: ["a", "b"])), static p => p.Contains("2 labels for 6 stops", StringComparison.Ordinal));
        Assert.Contains(Problems(Row(target: "height.clamp")), static p => p.Contains("does not offer", StringComparison.Ordinal));
        Assert.Contains(Problems(Row(max: 1000, min: 0, step: 100, @default: 100)), static p => p.Contains("game/world.json's checks refuse", StringComparison.Ordinal)); // peaks beyond the world's height
        Assert.Contains(Problems(Row(@default: 50)), static p => p.Contains("its default must be 100", StringComparison.Ordinal));
        Assert.Contains(Problems(Row(min: 0, mode: TerrainMode.ScaleInverse, target: "height.drainage.sourceArea")), static p => p.Contains("min 1 or more", StringComparison.Ordinal));
        Assert.Contains(Problems(Row(min: 0, max: 3, step: 1, @default: 2, target: "climate.continents.seaPerBlock", mode: TerrainMode.Set)), static p => p.Contains("must be the world file's value (1)", StringComparison.Ordinal));
        Assert.Contains(Problems(Row(min: 0, max: 3, step: 1, @default: 1, target: "climate.continents.seaPerBlock", mode: TerrainMode.Offset)), static p => p.Contains("offsets", StringComparison.Ordinal));
        Assert.Contains(Problems(), static p => p.Contains("at least one option", StringComparison.Ordinal));
        Assert.Contains(Problems(new TerrainOption("x", "L", string.Empty, string.Empty, 25, 150, 25, 100, [], [])), static p => p.Contains("needs at least one effect", StringComparison.Ordinal));
    }
}
