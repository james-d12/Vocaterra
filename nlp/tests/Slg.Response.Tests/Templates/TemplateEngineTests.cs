namespace Slg.Response.Tests.Templates;

using Slg.Response.Templates;

public class TemplateEngineTests
{
    private static string Render(string template, params (string Key, string Value)[] slots)
    {
        return TestSupport.Engine.Render(TestSupport.Template(template), TestSupport.Slots(slots));
    }

    [Fact]
    public void PlainSlotKeepsDigits()
    {
        Assert.Equal("¿Tienes 2 espadas?", Render("¿Tienes {qty} {item:plural-if(qty)}?", ("qty", "2"), ("item", "espada")));
    }

    [Fact]
    public void WordsFilterSpellsDigits()
    {
        Assert.Equal("¿Tienes dos espadas?", Render("¿Tienes {qty:words} {item:plural-if(qty)}?", ("qty", "2"), ("item", "espada")));
    }

    [Theory]
    [InlineData("1", "espada", "una espada")]
    [InlineData("2", "espada", "dos espadas")]
    [InlineData("0", "espada", "cero espadas")]
    [InlineData("dos", "espada", "dos espadas")]
    [InlineData("un", "libro", "un libro")]
    [InlineData("3", "poción", "tres pociones")]
    [InlineData("21", "moneda", "veintiuna monedas")]
    public void QuantityWordsAgreeWithTheItem(string qty, string item, string expected)
    {
        Assert.Equal(expected, Render("{qty:words(item)} {item:plural-if(qty)}", ("qty", qty), ("item", item)));
    }

    [Theory]
    [InlineData("espada", "def", "la espada")]
    [InlineData("espada", "indef", "una espada")]
    [InlineData("libro", "def", "el libro")]
    [InlineData("libro", "indef", "un libro")]
    [InlineData("agua", "def", "el agua")]
    [InlineData("agua", "indef", "un agua")]
    [InlineData("espadas", "def", "las espadas")]
    [InlineData("libros", "indef", "unos libros")]
    public void ArticleAgreesWithGenderAndNumber(string item, string kind, string expected)
    {
        Assert.Equal(expected, Render("{item:article(" + kind + ")}", ("item", item)));
    }

    [Theory]
    [InlineData("1", "una espada")]
    [InlineData("2", "unas espadas")]
    [InlineData("dos", "unas espadas")]
    [InlineData("0", "unas espadas")]
    public void ArticleCombinesWithPluralIf(string qty, string expected)
    {
        Assert.Equal(expected, Render("{item:article(indef):plural-if(qty)}", ("qty", qty), ("item", "espada")));
        Assert.Equal(expected, Render("{item:plural-if(qty):article(indef)}", ("qty", qty), ("item", "espada")));
    }

    [Fact]
    public void PluralIfFollowsTheQuantityNotTheSlotForm()
    {
        Assert.Equal("espada", Render("{item:plural-if(qty)}", ("qty", "1"), ("item", "espadas")));
        Assert.Equal("espadas", Render("{item:plural-if(qty)}", ("qty", "7"), ("item", "espadas")));
        Assert.Equal("pociones", Render("{item:plural-if(qty)}", ("qty", "2"), ("item", "poción")));
    }

    [Theory]
    [InlineData("tener", "1", "sg", "pres", "tengo")]
    [InlineData("tener", "3", "sg", "pres", "tiene")]
    [InlineData("tener", "3", "pl", "pres", "tienen")]
    [InlineData("tener", "1", "sg", "pret", "tuve")]
    [InlineData("comprar", "1", "sg", "pres", "compro")]
    [InlineData("vender", "2", "sg", "pres", "vendes")]
    [InlineData("comprar", "1", "pl", "pret", "compramos")]
    public void ConjFilterConjugatesTheLemma(string lemma, string person, string number, string tense, string expected)
    {
        Assert.Equal(expected, Render("{verb:conj(" + person + "," + number + "," + tense + ")}", ("verb", lemma)));
    }

    [Fact]
    public void ConjFilterAcceptsAnInflectedFormAndSpaces()
    {
        Assert.Equal("tengo", Render("{verb:conj(1, sg, pres)}", ("verb", "tiene")));
    }

    [Fact]
    public void WordsFilterGenderArguments()
    {
        Assert.Equal("veintiún", Render("{n:words(m)}", ("n", "21")));
        Assert.Equal("veintiuna", Render("{n:words(f)}", ("n", "21")));
        Assert.Equal("treinta y un", Render("{n:words(m)}", ("n", "31")));
        Assert.Equal("veintidós", Render("{n:words(m)}", ("n", "22")));
        Assert.Equal("uno", Render("{n:words}", ("n", "1")));
        Assert.Equal("ciento veintitrés", Render("{n:words}", ("n", "123")));
    }

    [Fact]
    public void CapFilterCapitalizesTheFirstLetter()
    {
        Assert.Equal("Una espada", Render("{item:article(indef):cap}", ("item", "espada")));
        Assert.Equal("Dos", Render("{qty:words:cap}", ("qty", "2")));
    }

    [Fact]
    public void UnknownSlotThrowsWithThePlaceholderName()
    {
        var error = Assert.Throws<TemplateException>(() => Render("¿Tienes {qty} {item:plural-if(qty)}?", ("qty", "2")));
        Assert.Equal("item", error.Placeholder);
        Assert.Contains("item", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownSlotInsideAFilterArgumentThrows()
    {
        var error = Assert.Throws<TemplateException>(() => Render("{item:plural-if(qty)}", ("item", "espada")));
        Assert.Equal("qty", error.Placeholder);
    }

    [Theory]
    [InlineData("{item")]
    [InlineData("hola }")]
    [InlineData("{}")]
    [InlineData("{item:}")]
    [InlineData("{item:unknown}")]
    [InlineData("{item:article}")]
    [InlineData("{item:article(def,indef)}")]
    [InlineData("{item:article(none)}")]
    [InlineData("{item:plural-if(qty)")]
    [InlineData("{item:plural-if(qty}")]
    [InlineData("{item:article(def):article(indef)}")]
    [InlineData("{verb:conj(1,sg)}")]
    [InlineData("{verb:conj(9,sg,pres)}")]
    [InlineData("{verb:conj(1,dual,pres)}")]
    [InlineData("{verb:conj(1,sg,future)}")]
    [InlineData("{item:article(def):words}")]
    [InlineData("{ite m}")]
    public void MalformedTemplatesThrow(string template)
    {
        Assert.Throws<TemplateException>(() => Render(template, ("item", "espada"), ("qty", "2"), ("verb", "tener")));
    }

    [Fact]
    public void ValuesThatCannotBeInflectedThrow()
    {
        Assert.Throws<TemplateException>(() => Render("{qty:words}", ("qty", "muchos")));
        Assert.Throws<TemplateException>(() => Render("{item:plural-if(qty)}", ("qty", "xyz"), ("item", "espada")));
        Assert.Throws<TemplateException>(() => Render("{item:article(def)}", ("item", "espada larga")));
        Assert.Throws<TemplateException>(() => Render("{verb:conj(1,sg,fut)}", ("verb", "tener")));
    }

    [Fact]
    public void DoubledBracesAreLiteral()
    {
        Assert.Equal("{item} es espada", Render("{{item}} es {item}", ("item", "espada")));
    }

    [Fact]
    public void QuantityWordsAreAcceptedAsSpelledQuantities()
    {
        Assert.Equal("espadas", Render("{item:plural-if(qty)}", ("qty", "varias"), ("item", "espada")));
        Assert.Equal("espada", Render("{item:plural-if(qty)}", ("qty", "una"), ("item", "espada")));
    }

    [Fact]
    public void ReferencedSlotsListsNamesAndFilterArguments()
    {
        var template = TestSupport.Template("¿{qty:words(item)} {item:plural-if(qty)} para {who}? {verb:conj(1,sg,pres)}");
        Assert.Equal(["item", "qty", "verb", "who"], TemplateEngine.ReferencedSlots(template));
    }

    [Fact]
    public void RenderingIsDeterministicAndNormalizesToNfc()
    {
        var first = Render("¿Tienes {item}?", ("item", "poción"));
        var decomposed = Render("¿Tienes {item}?", ("item", "poción"));
        Assert.Equal(first, decomposed);
        Assert.Equal(first, Render("¿Tienes {item}?", ("item", "poción")));
    }

    [Theory]
    [InlineData("Hola ¿qué tal?", "Hola ¿qué tal?")]
    [InlineData("Tienes dos espadas?", "¿Tienes dos espadas?")]
    [InlineData("Qué bien!", "¡Qué bien!")]
    [InlineData("¡Hola!  ¿Tienes   espadas ?", "¡Hola! ¿Tienes espadas?")]
    [InlineData("¿ Tienes espadas ? ¡ Perfecto !", "¿Tienes espadas? ¡Perfecto!")]
    [InlineData("Hola. Tienes espadas?", "Hola. ¿Tienes espadas?")]
    [InlineData("¿Tienes espadas", "¿Tienes espadas?")]
    [InlineData("¡Qué bien", "¡Qué bien!")]
    [InlineData("¿Tienes dos espadas? ¡Perfecto!", "¿Tienes dos espadas? ¡Perfecto!")]
    [InlineData("Bueno , sí .", "Bueno, sí.")]
    public void PunctuationIsBalancedAndSpaced(string template, string expected)
    {
        Assert.Equal(expected, Render(template));
    }

    [Fact]
    public void SlotValuesAreTrimmedIntoPlaceWithoutDoubleSpaces()
    {
        Assert.Equal("¿Tienes espadas?", Render("¿Tienes {item} ?", ("item", "espadas")));
        Assert.Equal("Dos", Render("{n}", ("n", "Dos")));
    }
}
