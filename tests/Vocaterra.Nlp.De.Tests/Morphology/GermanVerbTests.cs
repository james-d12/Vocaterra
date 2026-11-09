using Vocaterra.Nlp.De.Morphology;
using Vocaterra.Nlp.Language.Morphology;

namespace Vocaterra.Nlp.De.Tests.Morphology;

/// <summary>Tests for the verb side of <see cref="GermanMorphology"/> (M38): the paradigm, the Perfekt, the separable split and the regularised forms.</summary>
public class GermanVerbTests
{
    private static readonly GermanMorphology Morphology = new(TestSupport.FullLexicon);

    // Fifty frequent verbs: lemma, ich, du, er (present), the Perfekt participle and its auxiliary.
    private static readonly (string Lemma, string Ich, string Du, string Er, string Participle, string Auxiliary)[] Verbs =
    [
        ("sein", "bin", "bist", "ist", "gewesen", "sein"), ("haben", "habe", "hast", "hat", "gehabt", "haben"), ("werden", "werde", "wirst", "wird", "geworden", "sein"),
        ("können", "kann", "kannst", "kann", "gekonnt", "haben"), ("müssen", "muss", "musst", "muss", "gemusst", "haben"), ("wollen", "will", "willst", "will", "gewollt", "haben"),
        ("gehen", "gehe", "gehst", "geht", "gegangen", "sein"), ("kommen", "komme", "kommst", "kommt", "gekommen", "sein"), ("fahren", "fahre", "fährst", "fährt", "gefahren", "sein"),
        ("lesen", "lese", "liest", "liest", "gelesen", "haben"), ("nehmen", "nehme", "nimmst", "nimmt", "genommen", "haben"), ("geben", "gebe", "gibst", "gibt", "gegeben", "haben"),
        ("sehen", "sehe", "siehst", "sieht", "gesehen", "haben"), ("essen", "esse", "isst", "isst", "gegessen", "haben"), ("trinken", "trinke", "trinkst", "trinkt", "getrunken", "haben"),
        ("schlafen", "schlafe", "schläfst", "schläft", "geschlafen", "haben"), ("laufen", "laufe", "läufst", "läuft", "gelaufen", "sein"),
        ("sprechen", "spreche", "sprichst", "spricht", "gesprochen", "haben"), ("helfen", "helfe", "hilfst", "hilft", "geholfen", "haben"), ("wissen", "weiß", "weißt", "weiß", "gewusst", "haben"),
        ("machen", "mache", "machst", "macht", "gemacht", "haben"), ("sagen", "sage", "sagst", "sagt", "gesagt", "haben"), ("spielen", "spiele", "spielst", "spielt", "gespielt", "haben"),
        ("arbeiten", "arbeite", "arbeitest", "arbeitet", "gearbeitet", "haben"), ("lernen", "lerne", "lernst", "lernt", "gelernt", "haben"), ("kaufen", "kaufe", "kaufst", "kauft", "gekauft", "haben"),
        ("wohnen", "wohne", "wohnst", "wohnt", "gewohnt", "haben"), ("heißen", "heiße", "heißt", "heißt", "geheißen", "haben"), ("finden", "finde", "findest", "findet", "gefunden", "haben"),
        ("bleiben", "bleibe", "bleibst", "bleibt", "geblieben", "sein"), ("schreiben", "schreibe", "schreibst", "schreibt", "geschrieben", "haben"),
        ("bringen", "bringe", "bringst", "bringt", "gebracht", "haben"), ("denken", "denke", "denkst", "denkt", "gedacht", "haben"), ("liegen", "liege", "liegst", "liegt", "gelegen", "haben"),
        ("sitzen", "sitze", "sitzt", "sitzt", "gesessen", "haben"), ("rufen", "rufe", "rufst", "ruft", "gerufen", "haben"), ("tragen", "trage", "trägst", "trägt", "getragen", "haben"),
        ("fallen", "falle", "fällst", "fällt", "gefallen", "sein"), ("lassen", "lasse", "lässt", "lässt", "gelassen", "haben"), ("halten", "halte", "hältst", "hält", "gehalten", "haben"),
        ("leben", "lebe", "lebst", "lebt", "gelebt", "haben"), ("brauchen", "brauche", "brauchst", "braucht", "gebraucht", "haben"), ("fragen", "frage", "fragst", "fragt", "gefragt", "haben"),
        ("hören", "höre", "hörst", "hört", "gehört", "haben"), ("öffnen", "öffne", "öffnest", "öffnet", "geöffnet", "haben"), ("reisen", "reise", "reist", "reist", "gereist", "sein"),
        ("telefonieren", "telefoniere", "telefonierst", "telefoniert", "telefoniert", "haben"), ("verstehen", "verstehe", "verstehst", "versteht", "verstanden", "haben"),
        ("bekommen", "bekomme", "bekommst", "bekommt", "bekommen", "haben"), ("aufstehen", "stehe", "stehst", "steht", "aufgestanden", "sein"),
        ("anrufen", "rufe", "rufst", "ruft", "angerufen", "haben"), ("einkaufen", "kaufe", "kaufst", "kauft", "eingekauft", "haben"),
    ];

    [Fact]
    public void FiftyVerbsHaveTheRightPresentParticipleAndAuxiliary()
    {
        var failures = new List<string>();
        foreach (var (lemma, ich, du, er, participle, auxiliary) in Verbs)
        {
            Check(failures, lemma + " ich", ich, Morphology.Conjugate(lemma, "1", "sg", "pres"));
            Check(failures, lemma + " du", du, Morphology.Conjugate(lemma, "2", "sg", "pres"));
            Check(failures, lemma + " er", er, Morphology.Conjugate(lemma, "3", "sg", "pres"));
            Check(failures, lemma + " participle", participle, Morphology.Participle(lemma));
            Check(failures, lemma + " auxiliary", auxiliary, Morphology.Auxiliary(lemma));
            Check(failures, lemma + " perf er", (auxiliary == "sein" ? "ist " : "hat ") + participle, Morphology.Conjugate(lemma, "3", "sg", "perf"));
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void ThePluralPersonsAndThePreteriteOfTheAuxiliariesAndModals()
    {
        Assert.Equal("sind", Morphology.Conjugate("sein", "1", "pl", "pres"));
        Assert.Equal("seid", Morphology.Conjugate("sein", "2", "pl", "pres"));
        Assert.Equal("sind", Morphology.Conjugate("sein", "3", "pl", "pres"));
        Assert.Equal("war", Morphology.Conjugate("sein", "1", "sg", "pret"));
        Assert.Equal("warst", Morphology.Conjugate("sein", "2", "sg", "pret"));
        Assert.Equal("waren", Morphology.Conjugate("sein", "3", "pl", "pret"));
        Assert.Equal("hatte", Morphology.Conjugate("haben", "3", "sg", "pret"));
        Assert.Equal("hattet", Morphology.Conjugate("haben", "2", "pl", "pret"));
        Assert.Equal("konnte", Morphology.Conjugate("können", "1", "sg", "pret"));
        Assert.Equal("musste", Morphology.Conjugate("müssen", "3", "sg", "pret"));
        Assert.Equal("wollten", Morphology.Conjugate("wollen", "1", "pl", "pret"));
        Assert.Equal("ging", Morphology.Conjugate("gehen", "1", "sg", "pret"));
        Assert.Equal("gingst", Morphology.Conjugate("gehen", "2", "sg", "pret"));
        Assert.Equal("können", Morphology.Conjugate("können", "3", "pl", "pres"));
    }

    [Fact]
    public void TheImperativeHasTheStemChangeAndNoE()
    {
        Assert.Equal("fahr", Morphology.Conjugate("fahren", "2", "sg", "imp"));
        Assert.Equal("fahrt", Morphology.Conjugate("fahren", "2", "pl", "imp"));
        Assert.Equal("lies", Morphology.Conjugate("lesen", "2", "sg", "imp"));
        Assert.Equal("nimm", Morphology.Conjugate("nehmen", "2", "sg", "imp"));
        Assert.Equal("sei", Morphology.Conjugate("sein", "2", "sg", "imp"));
        Assert.Equal("arbeite", Morphology.Conjugate("arbeiten", "2", "sg", "imp"));
        Assert.Equal("gehen", Morphology.Conjugate("gehen", "3", "pl", "imp"));
        Assert.Equal("gehen", Morphology.Conjugate("gehen", "1", "pl", "imp"));
        Assert.Null(Morphology.Conjugate("gehen", "3", "sg", "imp"));
        Assert.Equal("steh", Morphology.Conjugate("aufstehen", "2", "sg", "imp"));
    }

    [Fact]
    public void AnUnknownTenseOrVerbGivesNull()
    {
        Assert.Null(Morphology.Conjugate("gehen", "1", "sg", "fut"));
        Assert.Null(Morphology.Conjugate("xy", "1", "sg", "pres"));
        Assert.Null(Morphology.Conjugate("gehen", "4", "sg", "pres"));
    }

    [Fact]
    public void AWeakVerbTheLexiconLacksGetsTheRegularForms()
    {
        Assert.Equal("blorke", Morphology.Conjugate("blorken", "1", "sg", "pres"));
        Assert.Equal("blorkst", Morphology.Conjugate("blorken", "2", "sg", "pres"));
        Assert.Equal("blorkt", Morphology.Conjugate("blorken", "3", "sg", "pres"));
        Assert.Equal("blorken", Morphology.Conjugate("blorken", "3", "pl", "pres"));
        Assert.Equal("blorkte", Morphology.Conjugate("blorken", "1", "sg", "pret"));
        Assert.Equal("blorktest", Morphology.Conjugate("blorken", "2", "sg", "pret"));
        Assert.Equal("geblorkt", Morphology.Participle("blorken"));
        Assert.Equal("hat geblorkt", Morphology.Conjugate("blorken", "3", "sg", "perf"));
        Assert.Equal("blork", Morphology.Conjugate("blorken", "2", "sg", "imp"));
        Assert.Equal("blarbeitest", Morphology.Conjugate("blarbeiten", "2", "sg", "pres"));
        Assert.Equal("blarbeitete", Morphology.Conjugate("blarbeiten", "3", "sg", "pret"));
        Assert.Equal("gebarbeitet", Morphology.Participle("barbeiten"));
        Assert.Equal("reist", Morphology.Conjugate("reisen", "2", "sg", "pres"));
        Assert.Equal("sammle", Morphology.Conjugate("sammeln", "1", "sg", "pres"));
        Assert.Equal("studiert", Morphology.Participle("studieren"));
    }

    [Fact]
    public void TheParadigmIsPresentPreteritePerfektForSixPersons()
    {
        var paradigm = Morphology.Paradigm;

        Assert.Equal(["pres", "pret", "perf"], paradigm.Tenses);
        Assert.Equal(6, paradigm.Persons.Count);
        Assert.Equal(new ParadigmPerson("3", "pl"), paradigm.Persons[5]);

        var cells = Morphology.ParadigmOf("fahren");
        Assert.Equal(18, cells.Count);
        Assert.Equal(["fahre", "fährst", "fährt", "fahren", "fahrt", "fahren"], cells.Where(c => c.Tense == "pres").Select(c => c.Form));
        Assert.Equal(["fuhr", "fuhrst", "fuhr", "fuhren", "fuhrt", "fuhren"], cells.Where(c => c.Tense == "pret").Select(c => c.Form));
        Assert.Equal(["bin gefahren", "bist gefahren", "ist gefahren", "sind gefahren", "seid gefahren", "sind gefahren"], cells.Where(c => c.Tense == "perf").Select(c => c.Form));
        Assert.Equal(new ParadigmPerson("2", "pl"), cells[4].Person);
        Assert.Equal(cells.Select(c => c.Form), ((IMorphology)Morphology).ParadigmOf("fahren").Select(c => c.Form));
    }

    [Fact]
    public void TheParadigmOfASeparableVerbShowsTheParticleAfterTheFiniteForm()
    {
        var cells = Morphology.ParadigmOf("aufstehen");

        Assert.Equal("stehe … auf", cells[0].Form);
        Assert.Equal("steht … auf", cells[4].Form);
        Assert.Equal("stand … auf", cells[6].Form);
        Assert.Equal("bin aufgestanden", cells[12].Form);
        Assert.Equal("sind aufgestanden", cells[17].Form);
    }

    [Fact]
    public void TheSeparableVerbSplitsIntoTheFiniteFormAndTheParticle()
    {
        Assert.Equal("auf", Morphology.SeparableParticle("aufstehen"));
        Assert.Equal("an", Morphology.SeparableParticle("anrufen"));
        Assert.Null(Morphology.SeparableParticle("verstehen"));
        Assert.Null(Morphology.SeparableParticle("gehen"));
        Assert.Null(Morphology.SeparableParticle("blorken"));
        Assert.Equal(new GermanSeparableForm("stehe", "auf"), Morphology.Split("aufstehen", "1", "sg", "pres"));
        Assert.Equal(new GermanSeparableForm("stand", "auf"), Morphology.Split("aufstehen", "3", "sg", "pret"));
        Assert.Equal(new GermanSeparableForm("rufst", "an"), Morphology.Split("anrufen", "2", "sg", "pres"));
        Assert.Equal(new GermanSeparableForm("gehe", null), Morphology.Split("gehen", "1", "sg", "pres"));
        Assert.Equal(new GermanSeparableForm("bin aufgestanden", null), Morphology.Split("aufstehen", "1", "sg", "perf"));
        Assert.Null(Morphology.Split("aufstehen", "1", "sg", "fut"));
    }

    [Theory]
    [InlineData("fahrst", "fahren", "fährst", "2", "sg", "pres", GermanMorphology.StemChangeMissed)]
    [InlineData("laufst", "laufen", "läufst", "2", "sg", "pres", GermanMorphology.StemChangeMissed)]
    [InlineData("gehte", "gehen", "ging", "1", "sg", "pret", GermanMorphology.WeakPreterite)]
    [InlineData("gehtest", "gehen", "gingst", "2", "sg", "pret", GermanMorphology.WeakPreterite)]
    [InlineData("habte", "haben", "hatte", "1", "sg", "pret", GermanMorphology.WeakPreterite)]
    [InlineData("gegeht", "gehen", "gegangen", "", "", "perf", GermanMorphology.WeakParticiple)]
    [InlineData("gefahrt", "fahren", "gefahren", "", "", "perf", GermanMorphology.WeakParticiple)]
    [InlineData("aufgesteht", "aufstehen", "aufgestanden", "", "", "perf", GermanMorphology.WeakParticiple)]
    public void ARegularisedStrongFormIsReadAsTheRightLemma(string typed, string lemma, string expected, string person, string number, string tense, string kind)
    {
        Assert.True(Morphology.TryGetGermanRegularization(typed, out var german), typed);
        Assert.Equal(new RegularizedForm(lemma, expected, person, number, tense), german.Form);
        Assert.Equal(kind, german.Kind);
        Assert.True(Morphology.TryGetRegularization(typed, out var plain));
        Assert.Equal(german.Form, plain);
        Assert.True(Morphology.IsRegularizedIrregular(typed, out var lemmaOut, out var expectedOut));
        Assert.Equal(lemma, lemmaOut);
        Assert.Equal(expected, expectedOut);
        Assert.False(Morphology.TryGetRegularizationOfWord(typed, out _));
    }

    [Theory]
    [InlineData("fahrt", "fahren", "fährt", "3", "sg")]
    [InlineData("lest", "lesen", "liest", "2", "sg")]
    public void AStemChangeMissedThatIsAnotherWordIsReportedAsAWord(string typed, string lemma, string expected, string person, string number)
    {
        Assert.True(Morphology.TryGetGermanRegularizationOfWord(typed, out var result));
        Assert.Equal(new RegularizedForm(lemma, expected, person, number, "pres"), result.Form);
        Assert.Equal(GermanMorphology.StemChangeMissed, result.Kind);
        Assert.True(Morphology.TryGetRegularizationOfWord(typed, out var viaInterface));
        Assert.Equal(result.Form, viaInterface);
        Assert.False(Morphology.TryGetRegularization(typed, out _));
    }

    [Theory]
    [InlineData("fährt")]
    [InlineData("ging")]
    [InlineData("gegangen")]
    [InlineData("geht")]
    [InlineData("gemacht")]
    [InlineData("habt")]
    [InlineData("sagte")]
    [InlineData("seit")]
    [InlineData("könnte")]
    [InlineData("tisch")]
    [InlineData("xyz")]
    public void ARealFormOrAWordThatIsNoSlipIsNotRegularised(string typed)
    {
        Assert.False(Morphology.TryGetRegularization(typed, out _));
        Assert.False(Morphology.IsRegularizedIrregular(typed, out _, out _));
        if (typed is not ("habt" or "seit"))
        {
            Assert.False(Morphology.TryGetRegularizationOfWord(typed, out _));
        }
    }

    [Fact]
    public void GenerationNeverProducesARegularisedForm()
    {
        var failures = new List<string>();
        foreach (var (lemma, _, _, _, _, _) in Verbs)
        {
            foreach (var cell in Morphology.ParadigmOf(lemma).Where(static c => c.Form is not null))
            {
                foreach (var token in cell.Form!.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(static t => t != "…"))
                {
                    if (Morphology.TryGetGermanRegularization(token, out var slip))
                    {
                        failures.Add($"{lemma} {cell.Tense}: '{token}' is a slip for {slip.Form.Expected}");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void EveryGeneratedFormOfEveryLexiconVerbIsAFormOfTheLexicon()
    {
        var lexicon = TestSupport.FullLexicon;
        var failures = new List<string>();
        var verbs = lexicon.Entries.Where(static e => e.Pos == "verb" && e.Features.VerbForm == "inf").Select(static e => e.Lemma).Distinct(StringComparer.Ordinal).ToList();
        Assert.True(verbs.Count > 2000);
        var cellCount = 0;
        var nullCount = 0;
        foreach (var lemma in verbs)
        {
            var participle = Morphology.Participle(lemma);
            if (participle is not null && !lexicon.Lookup(participle).Any(r => r.Lemma == lemma))
            {
                failures.Add($"{lemma} participle {participle}");
            }

            foreach (var cell in Morphology.ParadigmOf(lemma))
            {
                var tense = cell.Tense;
                cellCount++;
                if (cell.Form is null)
                {
                    nullCount++;
                    continue;
                }

                var tokens = cell.Form.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(static t => t != "…").ToArray();
                if (Morphology.SeparableParticle(lemma) is not null && tense is "pres" or "pret" && tokens.Length > 1)
                {
                    tokens = tokens[..^1];
                }

                var ruleFilled = cell.Tense != "perf" && !lexicon.EntriesForLemma(lemma).Any(e => e.Pos == "verb" && e.Features.Tense == cell.Tense && e.Features.Mood == "ind");
                if (!ruleFilled && tokens.Any(t => lexicon.Lookup(t).Count == 0))
                {
                    failures.Add($"{lemma} {cell.Tense} {cell.Person.Person}{cell.Person.Number}: {cell.Form}");
                }
            }

            foreach (var tense in new[] { "pres", "pret" })
            {
                if (!lexicon.EntriesForLemma(lemma).Any(e => e.Pos == "verb" && e.Features.Tense == tense && e.Features.Mood == "ind"))
                {
                    continue; // a weak verb with no form of the tense at all gets the regular ones (jungen: jungte); no lexicon form to compare
                }

                foreach (var person in Morphology.Paradigm.Persons)
                {
                    var finite = Morphology.Conjugate(lemma, person.Person, person.Number, tense);
                    if (finite is not null && !lexicon.Lookup(finite).Any(r => r.Lemma == lemma))
                    {
                        failures.Add($"{lemma} {tense} {person.Person}{person.Number}: {finite} is not a form of {lemma}");
                    }
                }
            }
        }

        Assert.True(failures.Count == 0, failures.Count + " failures:" + Environment.NewLine + string.Join(Environment.NewLine, failures.Take(40)));
        Assert.True(nullCount * 100 < cellCount, $"{nullCount} of {cellCount} cells are empty");
    }

    [Fact]
    public void ThePackCreatesTheMorphologyAndItsLayoutMatches()
    {
        var created = GermanLanguagePack.Instance.CreateMorphology(TestSupport.FullLexicon);

        Assert.IsType<GermanMorphology>(created);
        Assert.Equal(["pres", "pret", "perf"], created.Paradigm.Tenses);
        Assert.Equal("fährt", created.Conjugate("fahren", "3", "sg", "pres"));
        Assert.Equal("der", created.Article(Definiteness.Definite, "m", "sg", "tisch", "nom"));
        Assert.Equal("den", created.Article(Definiteness.Definite, "m", "sg", "tisch", "acc"));
        Assert.Equal("Häuser", created.Pluralize("Haus"));
        Assert.Equal("Haus", created.Singularize("Häuser"));
        Assert.Equal("Kinder", created.InflectNoun("kind", "pl"));
    }

    private static void Check(List<string> failures, string what, string? expected, string? actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            failures.Add($"{what}: expected '{expected}', got '{actual}'");
        }
    }
}
