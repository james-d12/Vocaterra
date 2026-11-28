using Vocaterra.Game.Core.Worlds;

namespace Vocaterra.Game.Core.Tests.Worlds;

/// <summary>The seed a player types (Q01): a number as it is, other text hashed with a fixed hash, blank random; and the rules for a world's name and its folder.</summary>
public sealed class SeedAndNameTests
{
    private static ulong Drawn => 777UL;

    private static string Default(int n) => "World " + n;

    [Theory]
    [InlineData("42", 42UL)]
    [InlineData("  42  ", 42UL)]
    [InlineData("0", 0UL)]
    [InlineData("007", 7UL)]
    [InlineData("18446744073709551615", ulong.MaxValue)]
    public void Given_ANumber_When_Parsed_Then_ItIsTheSeedAsItIs(string text, ulong expected)
    {
        var choice = SeedText.Parse(text, static () => Drawn);

        Assert.Equal(new SeedChoice(expected, SeedSource.Number), choice);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Given_BlankText_When_Parsed_Then_TheRandomSeedIsDrawn(string? text)
    {
        var draws = 0;

        var choice = SeedText.Parse(text, () => { draws++; return Drawn; });

        Assert.Equal(new SeedChoice(Drawn, SeedSource.Random), choice);
        Assert.Equal(1, draws);
    }

    [Theory]
    [InlineData("18446744073709551616")] // one past the largest seed
    [InlineData("-5")]
    [InlineData("+5")]
    [InlineData("4 2")]
    [InlineData("1e3")]
    [InlineData("hola")]
    public void Given_TextThatIsNotANumber_When_Parsed_Then_ItIsHashedAndTheDrawIsNotMade(string text)
    {
        var choice = SeedText.Parse(text, static () => throw new InvalidOperationException("no draw wanted"));

        Assert.Equal(SeedSource.Hashed, choice.Source);
        Assert.Equal(SeedText.Hash(text), choice.Seed);
    }

    [Fact]
    public void TheHashIsFnv1aOfTheUtf8BytesAndSoNeverMovesBetweenRunsOrMachines()
    {
        // The published test vectors of 64-bit FNV-1a: the same words make the same world on every machine.
        Assert.Equal(0xcbf29ce484222325UL, SeedText.Hash(string.Empty));
        Assert.Equal(0xaf63dc4c8601ec8cUL, SeedText.Hash("a"));
        Assert.Equal(0x85944171f73967e8UL, SeedText.Hash("foobar"));
        Assert.Equal(SeedText.Hash("mi mundo"), SeedText.Parse(" mi mundo ", static () => Drawn).Seed); // trimmed first
        Assert.NotEqual(SeedText.Hash("mundo"), SeedText.Hash("Mundo")); // case matters
    }

    [Theory]
    [InlineData("  My   world  ", "My world")]
    [InlineData("a\tb\nc", "a b c")]
    [InlineData("\u0001ok\u0002", "ok")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void TheNameIsCleanedToOneSpaceBetweenWordsAndNoControlCharacters(string typed, string expected) =>
        Assert.Equal(expected, WorldNames.Clean(typed));

    [Fact]
    public void Given_NamesTaken_When_ANameIsChecked_Then_EmptyAndTakenAndTooLongAreRefusedWithTheirReason()
    {
        string[] taken = ["World 1", "Mi Mundo"];

        Assert.Equal(NameProblem.None, WorldNames.Check("World 2", taken));
        Assert.Equal(NameProblem.Empty, WorldNames.Check("   ", taken));
        Assert.Equal(NameProblem.Empty, WorldNames.Check(null, taken));
        Assert.Equal(NameProblem.Taken, WorldNames.Check("world 1", taken)); // compared without regard to case
        Assert.Equal(NameProblem.Taken, WorldNames.Check("  Mi   mundo ", taken)); // and after cleaning
        Assert.Equal(NameProblem.TooLong, WorldNames.Check(new string('x', WorldNames.MaxLength + 1), taken));
        Assert.Equal(NameProblem.None, WorldNames.Check(new string('x', WorldNames.MaxLength), taken));
    }

    [Fact]
    public void TheDefaultNameIsWorldNForTheSmallestFreeN()
    {
        Assert.Equal("World 1", WorldNames.Next([], Default));
        Assert.Equal("World 2", WorldNames.Next(["World 1"], Default));
        Assert.Equal("World 2", WorldNames.Next(["World 1", "world 3", "Mine"], Default)); // the gap is filled
        Assert.Equal("World 4", WorldNames.Next(["World 1", "WORLD 2", "World 3"], Default));
    }

    [Theory]
    [InlineData("My World", "my-world")]
    [InlineData("  Mundo -- Nuevo!  ", "mundo-nuevo")]
    [InlineData("Año Nuevo: Cañón", "ano-nuevo-canon")]
    [InlineData("../../etc/passwd", "etc-passwd")]
    [InlineData("a/b\\c:d", "a-b-c-d")]
    [InlineData("!!!", "world")]
    [InlineData("日本", "world")]
    [InlineData("", "world")]
    public void TheFolderNameIsAFilesystemSafeSlug(string name, string expected) => Assert.Equal(expected, WorldNames.Slug(name));

    [Fact]
    public void TheSlugIsCutToFortyCharactersWithoutATrailingHyphen()
    {
        var slug = WorldNames.Slug(new string('a', 39) + " bbbbbb");

        Assert.Equal(new string('a', 39), slug);
        Assert.True(WorldNames.Slug(new string('z', 100)).Length <= 40);
    }

    [Fact]
    public void Given_SlugsThatCollide_When_FoldersAreChosen_Then_ANumberMakesEachUnique()
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in new[] { "A b", "a-b", "A.B", "a b!" })
        {
            var folder = WorldNames.UniqueFolder(name, taken.Contains);
            Assert.True(taken.Add(folder), folder);
        }

        Assert.Equal(["a-b", "a-b-2", "a-b-3", "a-b-4"], taken.Order(StringComparer.Ordinal));
    }
}
