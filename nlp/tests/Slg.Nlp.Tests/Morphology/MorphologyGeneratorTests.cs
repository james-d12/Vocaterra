namespace Slg.Nlp.Tests.Morphology;

using Slg.Nlp.Lexicon;
using Slg.Nlp.Morphology;
using Slg.Nlp.Tests.Analysis;

public class MorphologyGeneratorTests
{
    private static MorphologyGenerator Rules => Fixtures.RulesOnly;

    private static MorphologyGenerator WithSeed => Fixtures.Morphology;

    [Theory]
    [InlineData("casa", "casas")]
    [InlineData("mesa", "mesas")]
    [InlineData("arco", "arcos")]
    [InlineData("llave", "llaves")]
    [InlineData("sofá", "sofás")]
    [InlineData("café", "cafés")]
    [InlineData("papel", "papeles")]
    [InlineData("ciudad", "ciudades")]
    [InlineData("mar", "mares")]
    [InlineData("tren", "trenes")]
    [InlineData("luz", "luces")]
    [InlineData("lápiz", "lápices")]
    [InlineData("pez", "peces")]
    [InlineData("rey", "reyes")]
    [InlineData("ley", "leyes")]
    [InlineData("lunes", "lunes")]
    [InlineData("martes", "martes")]
    [InlineData("miércoles", "miércoles")]
    [InlineData("crisis", "crisis")]
    [InlineData("virus", "virus")]
    [InlineData("mes", "meses")]
    [InlineData("dios", "dioses")]
    [InlineData("país", "países")]
    [InlineData("inglés", "ingleses")]
    [InlineData("autobús", "autobuses")]
    [InlineData("canción", "canciones")]
    [InlineData("poción", "pociones")]
    [InlineData("jardín", "jardines")]
    [InlineData("ratón", "ratones")]
    [InlineData("árbol", "árboles")]
    [InlineData("joven", "jóvenes")]
    [InlineData("examen", "exámenes")]
    [InlineData("origen", "orígenes")]
    [InlineData("fax", "faxes")]
    [InlineData("tórax", "tórax")]
    public void PluralizeFollowsTheSpellingRules(string singular, string plural)
    {
        Assert.Equal(plural, Rules.Pluralize(singular));
    }

    [Theory]
    [InlineData("espadas", "espada")]
    [InlineData("llaves", "llave")]
    [InlineData("calles", "calle")]
    [InlineData("torres", "torre")]
    [InlineData("bosques", "bosque")]
    [InlineData("hombres", "hombre")]
    [InlineData("papeles", "papel")]
    [InlineData("ciudades", "ciudad")]
    [InlineData("mares", "mar")]
    [InlineData("luces", "luz")]
    [InlineData("peces", "pez")]
    [InlineData("reyes", "rey")]
    [InlineData("meses", "mes")]
    [InlineData("trenes", "tren")]
    [InlineData("ingleses", "inglés")]
    [InlineData("canciones", "canción")]
    [InlineData("pociones", "poción")]
    [InlineData("jardines", "jardín")]
    [InlineData("leones", "león")]
    [InlineData("jóvenes", "joven")]
    [InlineData("exámenes", "examen")]
    [InlineData("sofás", "sofá")]
    [InlineData("crisis", "crisis")]
    public void SingularizeInvertsThePluralRules(string plural, string singular)
    {
        Assert.Equal(singular, Rules.Singularize(plural));
    }

    [Fact]
    public void LexiconPluralsOverrideTheRules()
    {
        var odd = Lexicon.FromEntries(
        [
            new LexiconEntry("regla", "regla", "noun", new EntryFeatures(null, "sg", null, null, "f")),
            new LexiconEntry("reglitas", "regla", "noun", new EntryFeatures(null, "pl", null, null, "f")),
        ]);
        var generator = new MorphologyGenerator(odd);

        Assert.Equal("reglitas", generator.Pluralize("regla"));
        Assert.Equal("regla", generator.Singularize("reglitas"));
        Assert.Equal("reglitas", generator.InflectNoun("regla", "pl"));
        Assert.Equal("regla", generator.InflectNoun("regla", "sg"));
    }

    [Fact]
    public void SeedKnowsInvariantWeekdays()
    {
        Assert.Equal("lunes", WithSeed.Singularize("lunes"));
        Assert.Equal("lunes", WithSeed.Pluralize("lunes"));
        Assert.Equal("país", WithSeed.Singularize("países"));
    }

    [Fact]
    public void PluralizeAndSingularizeNormalizeCaseAndRejectEmptyInput()
    {
        Assert.Equal("espadas", WithSeed.Pluralize("Espada"));
        Assert.Throws<ArgumentException>(() => WithSeed.Pluralize("  "));
        Assert.Throws<ArgumentNullException>(() => WithSeed.Singularize(null!));
    }

    [Theory]
    [InlineData(Definiteness.Definite, "m", "sg", "el")]
    [InlineData(Definiteness.Definite, "f", "sg", "la")]
    [InlineData(Definiteness.Definite, "m", "pl", "los")]
    [InlineData(Definiteness.Definite, "f", "pl", "las")]
    [InlineData(Definiteness.Indefinite, "m", "sg", "un")]
    [InlineData(Definiteness.Indefinite, "f", "sg", "una")]
    [InlineData(Definiteness.Indefinite, "m", "pl", "unos")]
    [InlineData(Definiteness.Indefinite, "f", "pl", "unas")]
    public void ArticleCoversAllEightForms(Definiteness definiteness, string gender, string number, string expected)
    {
        Assert.Equal(expected, WithSeed.Article(definiteness, gender, number));
    }

    [Fact]
    public void ArticleUsesElBeforeStressedAFeminineNouns()
    {
        Assert.Equal("el", WithSeed.Article(Definiteness.Definite, "f", "sg", "agua"));
        Assert.Equal("un", WithSeed.Article(Definiteness.Indefinite, "f", "sg", "arma"));
        Assert.Equal("las", WithSeed.Article(Definiteness.Definite, "f", "pl", "armas"));
        Assert.Equal("la", WithSeed.Article(Definiteness.Definite, "f", "sg", "mesa"));
    }

    [Fact]
    public void ArticleRejectsUnknownGenderOrNumber()
    {
        Assert.Throws<ArgumentException>(() => WithSeed.Article(Definiteness.Definite, "n", "sg"));
        Assert.Throws<ArgumentException>(() => WithSeed.Article(Definiteness.Definite, "m", "dual"));
    }

    [Theory]
    [InlineData("rojo", "m", "sg", "rojo")]
    [InlineData("rojo", "f", "sg", "roja")]
    [InlineData("rojo", "m", "pl", "rojos")]
    [InlineData("rojo", "f", "pl", "rojas")]
    [InlineData("grande", "f", "sg", "grande")]
    [InlineData("grande", "m", "pl", "grandes")]
    [InlineData("bueno", "m", "sg", "bueno")]
    [InlineData("bueno", "f", "pl", "buenas")]
    [InlineData("feliz", "f", "pl", "felices")]
    [InlineData("cansado", "f", "sg", "cansada")]
    [InlineData("rojas", "m", "sg", "rojo")]
    [InlineData("gran", "f", "pl", "grandes")]
    public void AgreeAdjectiveUsesLexiconForms(string lemma, string gender, string number, string expected)
    {
        Assert.Equal(expected, WithSeed.AgreeAdjective(lemma, gender, number));
    }

    [Theory]
    [InlineData("pequeño", "f", "pl", "pequeñas")]
    [InlineData("nuevo", "f", "sg", "nueva")]
    [InlineData("verde", "m", "pl", "verdes")]
    [InlineData("azul", "f", "pl", "azules")]
    [InlineData("feliz", "m", "pl", "felices")]
    [InlineData("fácil", "f", "pl", "fáciles")]
    [InlineData("español", "f", "sg", "española")]
    [InlineData("español", "m", "pl", "españoles")]
    [InlineData("francés", "f", "sg", "francesa")]
    [InlineData("francés", "m", "pl", "franceses")]
    [InlineData("trabajador", "f", "pl", "trabajadoras")]
    [InlineData("mejor", "f", "sg", "mejor")]
    [InlineData("mayor", "f", "pl", "mayores")]
    [InlineData("holgazán", "f", "sg", "holgazana")]
    [InlineData("joven", "f", "pl", "jóvenes")]
    public void AgreeAdjectiveFallsBackToRules(string lemma, string gender, string number, string expected)
    {
        Assert.Equal(expected, Rules.AgreeAdjective(lemma, gender, number));
    }

    [Theory]
    [InlineData("comprar", "pres", "compro compras compra compramos compráis compran")]
    [InlineData("comprar", "pret", "compré compraste compró compramos comprasteis compraron")]
    [InlineData("comer", "pres", "como comes come comemos coméis comen")]
    [InlineData("comer", "pret", "comí comiste comió comimos comisteis comieron")]
    [InlineData("vivir", "pres", "vivo vives vive vivimos vivís viven")]
    [InlineData("vivir", "pret", "viví viviste vivió vivimos vivisteis vivieron")]
    [InlineData("cantar", "pres", "canto cantas canta cantamos cantáis cantan")]
    [InlineData("buscar", "pret", "busqué buscaste buscó buscamos buscasteis buscaron")]
    [InlineData("llegar", "pret", "llegué llegaste llegó llegamos llegasteis llegaron")]
    [InlineData("empezar", "pret", "empecé empezaste empezó empezamos empezasteis empezaron")]
    [InlineData("coger", "pres", "cojo coges coge cogemos cogéis cogen")]
    [InlineData("vencer", "pres", "venzo vences vence vencemos vencéis vencen")]
    [InlineData("leer", "pret", "leí leíste leyó leímos leísteis leyeron")]
    public void ConjugateGeneratesRegularParadigmsByRule(string lemma, string tense, string expected)
    {
        Assert.Equal(expected.Split(' '), Forms(Rules, lemma, tense));
    }

    [Theory]
    [InlineData("comprar", "pres", "compro compras compra compramos compráis compran")]
    [InlineData("comer", "pret", "comí comiste comió comimos comisteis comieron")]
    [InlineData("vivir", "pres", "vivo vives vive vivimos vivís viven")]
    public void ConjugateOfRegularVerbsAgreesBetweenLexiconAndRules(string lemma, string tense, string expected)
    {
        Assert.Equal(expected.Split(' '), Forms(WithSeed, lemma, tense));
    }

    [Theory]
    [InlineData("tener", "pres", "tengo tienes tiene tenemos tenéis tienen")]
    [InlineData("tener", "pret", "tuve tuviste tuvo tuvimos tuvisteis tuvieron")]
    [InlineData("ser", "pres", "soy eres es somos sois son")]
    [InlineData("ir", "pret", "fui fuiste fue fuimos fuisteis fueron")]
    [InlineData("hacer", "pret", "hice hiciste hizo hicimos hicisteis hicieron")]
    [InlineData("saber", "pres", "sé sabes sabe sabemos sabéis saben")]
    [InlineData("dar", "pret", "di diste dio dimos disteis dieron")]
    public void ConjugateTakesIrregularFormsFromTheLexicon(string lemma, string tense, string expected)
    {
        Assert.Equal(expected.Split(' '), Forms(WithSeed, lemma, tense));
    }

    [Fact]
    public void ConjugateReturnsNullWhenNothingCanBeGenerated()
    {
        Assert.Null(WithSeed.Conjugate("tener", "1", "sg", "fut"));
        Assert.Null(WithSeed.Conjugate("comprar", "1", "sg", "impf"));
        Assert.Null(WithSeed.Conjugate("ir", "1", "sg", "pres.nonexistent"));
        Assert.Null(Rules.Conjugate("espada", "1", "sg", "pres"));
        Assert.Null(Rules.Conjugate("ir", "1", "sg", "pres"));
    }

    [Fact]
    public void ConjugateRejectsInvalidPersonOrNumber()
    {
        Assert.Throws<ArgumentException>(() => WithSeed.Conjugate("tener", "4", "sg", "pres"));
        Assert.Throws<ArgumentException>(() => WithSeed.Conjugate("tener", "1", "dual", "pres"));
    }

    [Theory]
    [InlineData("sabo", "saber", "sé")]
    [InlineData("tení", "tener", "tuve")]
    [InlineData("hací", "hacer", "hice")]
    [InlineData("teno", "tener", "tengo")]
    [InlineData("quero", "querer", "quiero")]
    [InlineData("poní", "poner", "puse")]
    [InlineData("ponió", "poner", "puso")]
    [InlineData("decí", "decir", "dije")]
    [InlineData("hació", "hacer", "hizo")]
    [InlineData("estó", "estar", "estuvo")]
    [InlineData("podí", "poder", "pude")]
    [InlineData("costa", "costar", "cuesta")]
    public void DetectsRegularizedIrregularForms(string form, string lemma, string expected)
    {
        Assert.True(WithSeed.IsRegularizedIrregular(form, out var foundLemma, out var foundExpected));
        Assert.Equal(lemma, foundLemma);
        Assert.Equal(expected, foundExpected);
    }

    [Theory]
    [InlineData("sé")]
    [InlineData("tengo")]
    [InlineData("tuve")]
    [InlineData("compro")]
    [InlineData("espada")]
    [InlineData("se")]
    [InlineData("ses")]
    [InlineData("do")]
    [InlineData("xyzzy")]
    [InlineData("saber")]
    [InlineData("sabe")]
    [InlineData("")]
    public void DoesNotFlagKnownOrUnrelatedWords(string form)
    {
        Assert.False(WithSeed.IsRegularizedIrregular(form, out var lemma, out var expected));
        Assert.Equal(string.Empty, lemma);
        Assert.Equal(string.Empty, expected);
    }

    [Fact]
    public void RegularizationNeedsIrregularLemmasFromTheLexicon()
    {
        Assert.False(Rules.IsRegularizedIrregular("sabo", out _, out _));
    }

    [Fact]
    public void GeneratorOutputIsDeterministic()
    {
        var first = new MorphologyGenerator(Lexicon.LoadSeed());
        var second = new MorphologyGenerator(Lexicon.LoadSeed());

        foreach (var form in new[] { "sabo", "tení", "hací", "ponió" })
        {
            first.IsRegularizedIrregular(form, out var lemmaA, out var expectedA);
            second.IsRegularizedIrregular(form, out var lemmaB, out var expectedB);
            Assert.Equal((lemmaA, expectedA), (lemmaB, expectedB));
        }

        Assert.Equal(first.Pluralize("poción"), second.Pluralize("poción"));
    }

    private static string[] Forms(MorphologyGenerator generator, string lemma, string tense)
    {
        string[] persons = ["1", "2", "3"];
        string[] numbers = ["sg", "pl"];
        return numbers.SelectMany(_ => persons, (number, person) => (number, person))
            .OrderBy(cell => cell.number == "sg" ? 0 : 1)
            .ThenBy(cell => cell.person, StringComparer.Ordinal)
            .Select(cell => generator.Conjugate(lemma, cell.person, cell.number, tense) ?? "<null>")
            .ToArray();
    }
}
