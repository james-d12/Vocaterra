namespace Vocaterra.Nlp.Es.Tests.Lexicon;

using Vocaterra.Nlp.Es.Tests.Analysis;
using Vocaterra.Nlp.Language.Lexicon;

public class SeedLexiconTests
{
    private static readonly string[] PlanVerbs =
    [
        "ser", "estar", "tener", "querer", "comprar", "vender", "ir", "hacer", "poder", "dar",
        "saber", "comer", "hablar", "vivir", "necesitar", "buscar", "ver", "decir", "venir", "poner",
    ];

    private static Lexicon Seed => Fixtures.Seed;

    private static IEnumerable<LexiconEntry> OfLemma(string lemma, string pos)
    {
        return Seed.EntriesForLemma(lemma).Where(e => e.Pos == pos);
    }

    [Fact]
    public void EmbeddedSeedLoadsAndIsHandAuthored()
    {
        Assert.Equal("hand-authored", Seed.GeneratedBy);
        Assert.True(Seed.Lemmas.Count >= 150, $"only {Seed.Lemmas.Count} lemmas");
        Assert.True(Seed.Entries.Count > 600);
    }

    [Fact]
    public void CommittedFileMatchesEmbeddedResource()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "content", "lexicon", "seed.json");

        Assert.True(File.Exists(path), $"Missing {path}");
        Assert.Equal(SpanishLexicon.ReadSeedJson(), File.ReadAllText(path));
    }

    [Fact]
    public void EntriesAreOrdinalSorted()
    {
        var forms = Seed.Entries.Select(e => e.Form).ToArray();

        for (var i = 1; i < forms.Length; i++)
        {
            Assert.True(string.CompareOrdinal(forms[i - 1], forms[i]) <= 0, $"{forms[i - 1]} after {forms[i]}");
        }

        var lemmas = Seed.Lemmas.ToArray();
        Assert.Equal(lemmas.OrderBy(l => l, StringComparer.Ordinal), lemmas);
    }

    [Theory]
    [MemberData(nameof(VerbData))]
    public void PlanVerbsHaveFullPresentAndPreteriteParadigms(string lemma)
    {
        var verbs = OfLemma(lemma, "verb").ToList();

        Assert.Contains(verbs, e => e.Form == lemma && e.Features.Tense is null);
        foreach (var tense in new[] { "pres", "pret" })
        {
            foreach (var number in new[] { "sg", "pl" })
            {
                foreach (var person in new[] { "1", "2", "3" })
                {
                    Assert.Contains(verbs, e => e.Features is { Mood: "ind" } f && f.Tense == tense && f.Number == number && f.Person == person);
                }
            }
        }
    }

    public static TheoryData<string> VerbData()
    {
        var data = new TheoryData<string>();
        foreach (var verb in PlanVerbs)
        {
            data.Add(verb);
        }

        return data;
    }

    [Fact]
    public void IrregularParadigmsHaveTheExpectedFormsForTener()
    {
        var morphology = Fixtures.Morphology;

        Assert.Equal(
            "tengo tienes tiene tenemos tenéis tienen",
            string.Join(' ', Cells().Select(c => morphology.Conjugate("tener", c.Person, c.Number, "pres"))));
        Assert.Equal(
            "tuve tuviste tuvo tuvimos tuvisteis tuvieron",
            string.Join(' ', Cells().Select(c => morphology.Conjugate("tener", c.Person, c.Number, "pret"))));
    }

    [Fact]
    public void ArticlesAreComplete()
    {
        foreach (var form in new[] { "el", "la", "los", "las", "un", "una", "unos", "unas" })
        {
            Assert.Contains(Seed.Lookup(form), r => r.Pos == "art");
        }

        Assert.Equal("m", Seed.Lookup("el").First(r => r.Pos == "art").Gender);
        Assert.Equal("pl", Seed.Lookup("las").First(r => r.Pos == "art").Number);
    }

    [Fact]
    public void SubjectPronounsCarryPersonAndNumber()
    {
        var expected = new Dictionary<string, (string Person, string Number)>
        {
            ["yo"] = ("1", "sg"),
            ["tú"] = ("2", "sg"),
            ["él"] = ("3", "sg"),
            ["ella"] = ("3", "sg"),
            ["usted"] = ("3", "sg"),
            ["nosotros"] = ("1", "pl"),
            ["vosotros"] = ("2", "pl"),
            ["ellos"] = ("3", "pl"),
            ["ellas"] = ("3", "pl"),
            ["ustedes"] = ("3", "pl"),
        };

        foreach (var (form, cell) in expected.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var reading = Assert.Single(Seed.Lookup(form), r => r.Pos == "pron");
            Assert.Equal(cell.Person, reading.Person);
            Assert.Equal(cell.Number, reading.Number);
        }
    }

    [Fact]
    public void NumeralsOneToTwentyAreNumsWithDigitLemmas()
    {
        var words = new[]
        {
            "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve", "diez",
            "once", "doce", "trece", "catorce", "quince", "dieciséis", "diecisiete", "dieciocho", "diecinueve", "veinte",
        };

        for (var i = 0; i < words.Length; i++)
        {
            var reading = Assert.Single(Seed.Lookup(words[i]), r => r.Pos == "num");
            Assert.Equal((i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), reading.Lemma);
        }

        Assert.Equal("1", Seed.Lookup("una").First(r => r.Pos == "num").Lemma);
        Assert.Equal("2", Seed.Lookup("2").First().Lemma);
    }

    [Fact]
    public void HasAtLeastFortyNounsWithGenderAndPlural()
    {
        var nouns = Seed.Lemmas
            .Where(l => OfLemma(l, "noun").Any())
            .ToList();

        Assert.True(nouns.Count >= 40, $"only {nouns.Count} nouns");
        foreach (var lemma in nouns.Where(l => l != "dinero"))
        {
            var entries = OfLemma(lemma, "noun").ToList();
            Assert.All(entries, e => Assert.NotNull(e.Features.Gender));
            Assert.Contains(entries, e => e.Features.Number == "sg");
            Assert.Contains(entries, e => e.Features.Number == "pl");
        }
    }

    [Theory]
    [InlineData("espada", "f", "espadas")]
    [InlineData("escudo", "m", "escudos")]
    [InlineData("arco", "m", "arcos")]
    [InlineData("flecha", "f", "flechas")]
    [InlineData("poción", "f", "pociones")]
    [InlineData("casco", "m", "cascos")]
    [InlineData("armadura", "f", "armaduras")]
    [InlineData("antorcha", "f", "antorchas")]
    [InlineData("cuerda", "f", "cuerdas")]
    [InlineData("llave", "f", "llaves")]
    [InlineData("mapa", "m", "mapas")]
    [InlineData("moneda", "f", "monedas")]
    [InlineData("piedra", "f", "piedras")]
    [InlineData("madera", "f", "maderas")]
    [InlineData("hierro", "m", "hierros")]
    [InlineData("oro", "m", "oros")]
    [InlineData("plata", "f", "platas")]
    [InlineData("perro", "m", "perros")]
    [InlineData("dragón", "m", "dragones")]
    [InlineData("taberna", "f", "tabernas")]
    [InlineData("queso", "m", "quesos")]
    [InlineData("pan", "m", "panes")]
    public void RequiredNounsHaveGenderAndPlural(string lemma, string gender, string plural)
    {
        var entries = OfLemma(lemma, "noun").ToList();

        Assert.Contains(entries, e => e.Form == lemma && e.Features.Number == "sg" && e.Features.Gender == gender);
        Assert.Contains(entries, e => e.Form == plural && e.Features.Number == "pl" && e.Features.Gender == gender);
    }

    [Fact]
    public void HasAtLeastTwentyAdjectivesWithFourForms()
    {
        var adjectives = Seed.Lemmas.Where(l => OfLemma(l, "adj").Any()).ToList();
        foreach (var lemma in new[] { "cansado", "rojo", "grande", "nuevo", "viejo", "fuerte", "bueno", "malo", "caro", "barato" })
        {
            Assert.Contains(lemma, adjectives);
        }

        Assert.True(adjectives.Count >= 20, $"only {adjectives.Count} adjectives");
        foreach (var lemma in adjectives.Where(l => l is not ("varios" or "mi" or "tu" or "su")))
        {
            var cells = OfLemma(lemma, "adj")
                .Select(e => (e.Features.Gender, e.Features.Number))
                .Distinct()
                .ToList();
            Assert.Contains(("m", "sg"), cells);
            Assert.Contains(("f", "sg"), cells);
            Assert.Contains(("m", "pl"), cells);
            Assert.Contains(("f", "pl"), cells);
        }
    }

    [Theory]
    [InlineData("hola", "interj")]
    [InlineData("gracias", "interj")]
    [InlineData("por favor", "interj")]
    [InlineData("sí", "interj")]
    [InlineData("no", "adv")]
    [InlineData("mucho", "adv")]
    [InlineData("aquí", "adv")]
    [InlineData("en", "prep")]
    [InlineData("y", "conj")]
    public void CommonFunctionWordsArePresent(string form, string pos)
    {
        Assert.Contains(Seed.Lookup(form), r => r.Pos == pos);
    }

    [Fact]
    public void SeedHasAmbiguousFormsWithSeveralReadings()
    {
        var vino = Seed.Lookup("vino");
        Assert.Contains(vino, r => r.Pos == "noun");
        Assert.Contains(vino, r => r is { Pos: "verb", Lemma: "venir" });

        var fue = Seed.Lookup("fue");
        Assert.Contains(fue, r => r.Lemma == "ser");
        Assert.Contains(fue, r => r.Lemma == "ir");
    }

    [Fact]
    public void RegularVerbsFollowTheRulesExactly()
    {
        var rules = Fixtures.RulesOnly;
        var regular = new[]
        {
            "comprar", "vender", "comer", "hablar", "vivir", "necesitar", "buscar", "mirar", "llevar", "usar",
            "ayudar", "llamar", "gustar", "abrir", "beber", "correr", "escribir",
        };

        foreach (var lemma in regular)
        {
            foreach (var tense in new[] { "pres", "pret" })
            {
                foreach (var (person, number) in Cells())
                {
                    Assert.Equal(Fixtures.Morphology.Conjugate(lemma, person, number, tense), rules.Conjugate(lemma, person, number, tense));
                }
            }
        }
    }

    [Fact]
    public void SeedNounPluralsMatchTheSpellingRules()
    {
        var mismatches = new List<string>();
        foreach (var lemma in Seed.Lemmas)
        {
            foreach (var singular in OfLemma(lemma, "noun").Where(e => e.Features.Number == "sg"))
            {
                var plurals = OfLemma(lemma, "noun").Where(e => e.Features.Number == "pl" && e.Features.Gender == singular.Features.Gender).ToList();
                if (plurals.Count == 0)
                {
                    continue;
                }

                var byRule = Fixtures.RulesOnly.Pluralize(singular.Form);
                if (byRule != plurals[0].Form)
                {
                    mismatches.Add($"{singular.Form}: rule {byRule}, lexicon {plurals[0].Form}");
                }

                var back = Fixtures.RulesOnly.Singularize(plurals[0].Form);
                if (back != singular.Form)
                {
                    mismatches.Add($"{plurals[0].Form}: singularized {back}, lexicon {singular.Form}");
                }
            }
        }

        // Invariant weekdays cannot be singularized by rule alone; the lexicon covers them.
        var unexpected = mismatches.Where(m => !m.StartsWith("lunes", StringComparison.Ordinal)
            && !m.StartsWith("martes", StringComparison.Ordinal)
            && !m.StartsWith("miércoles", StringComparison.Ordinal)
            && !m.StartsWith("jueves", StringComparison.Ordinal)
            && !m.StartsWith("viernes", StringComparison.Ordinal)
            && !m.StartsWith("países", StringComparison.Ordinal)).ToList();
        Assert.True(unexpected.Count == 0, string.Join(Environment.NewLine, unexpected));
    }

    private static IEnumerable<(string Person, string Number)> Cells()
    {
        yield return ("1", "sg");
        yield return ("2", "sg");
        yield return ("3", "sg");
        yield return ("1", "pl");
        yield return ("2", "pl");
        yield return ("3", "pl");
    }
}
