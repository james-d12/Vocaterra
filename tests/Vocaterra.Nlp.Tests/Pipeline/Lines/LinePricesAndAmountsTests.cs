namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>Lines of Spanish graded at the game's own prompts on <c>content/game-v1</c> (the CLI's path): prices, amounts and the question word cuánto.</summary>
public class LinePricesAndAmountsTests
{
    // Prices and amounts: a price is one sum when the amount is money (diez monedas es caro), cuánto and cuántos agree, an amount before ser.
    public static TheoryData<string, string, string, bool, string[]> Rows => new()
    {
        { "villager-open", "diez monedas son mucho dinero", "unknown", false, [] },
        { "villager-open", "quiero pan y yo tengo dos monedas", "unknown", false, [] },
        { "baker-open", "necesito pago", "request_item", true, [] },
        { "farmer-open", "¿cuánto cuestan todo?", "ask_total", true, ["conj.person=cuesta"] },
        { "farmer-open", "¿cuánto son todo?", "ask_total", true, ["conj.person=es"] },
        { "farmer-open", "¿cuánto cuesta todo?", "ask_total", true, [] },
        { "farmer-open", "¿cuánto es todo?", "ask_total", true, [] },
        { "farmer-open", "¿cuánto cuesta eso?", "ask_price", true, [] },
        { "farmer-open", "¿cuánto cuestan las manzanas?", "ask_price", true, [] },
        { "farmer-open", "¿cuánto son las manzanas?", "ask_price", true, [] },
        { "farmer-open", "las manzanas cuesta una moneda", "ask_price", true, ["conj.person=cuestan"] },
        { "baker-open", "los panes cuesta dos monedas", "ask_price", true, ["conj.person=cuestan"] },
        { "farmer-open", "las manzanas cuestan una moneda", "ask_price", true, [] },
        { "farmer-open", "la manzana cuesta una moneda", "ask_price", true, [] },
        { "farmer-open", "cada manzana cuesta una moneda", "ask_price", true, [] },
        { "farmer-open", "dos manzanas cuestan dos monedas", "ask_price", true, [] },
        { "baker-open", "los panes cuestan dos monedas", "ask_price", true, [] },
        { "farmer-open", "los panes cuestan dos monedas", "ask_price", true, [] },
        { "baker-open", "cuesta dos monedas", "ask_price", true, [] },
        { "farmer-open", "¿cuánto cuestas el pan?", "ask_price", true, ["conj.person=cuesta"] },
        { "farmer-open", "¿cuánto cuesta el pan?", "ask_price", true, [] },
        { "villager-open", "esto es caro", "unknown", false, [] },
        { "villager-open", "es cara la manzana", "unknown", false, [] },
        { "farmer-open", "cuanto son todo?", "ask_total", true, ["conj.person=es"] },
        { "farmer-open", "cuanto son todo", "ask_total", true, ["conj.person=es"] },
        { "farmer-open", "cuánto es todo", "ask_total", true, [] },
        { "farmer-open", "cuanto cuesta el pan", "ask_price", true, [] },
        { "farmer-open", "cuanto es todo", "ask_total", true, [] },
        { "farmer-open", "¿cuántos son?", "ask_price", true, [] },
        { "villager-open", "tengo diez años", "unknown", false, [] },
        { "villager-open", "ayer pagué dos monedas", "unknown", false, [] },
        { "baker-open", "¿cuánto cuesta el pan pequeño?", "ask_price", true, [] },
        { "farmer-open", "cuánto cuesta todo", "ask_total", true, [] },
        { "farmer-open", "cuánto cuestan las manzanas", "ask_price", true, [] },
        { "baker-open", "cuánto cuesta el pan", "ask_price", true, [] },
        { "baker-open", "cuánto cuestan los panes", "ask_price", true, [] },
        { "farmer-open", "la cuesta es grande", "describe", true, [] },
        { "villager-open", "diez monedas es mucho dinero", "unknown", false, [] },
        { "villager-open", "dos panes es suficiente", "unknown", false, [] },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void TheLineIsGradedAsItWas(string prompt, string line, string intent, bool communicated, string[] errors) =>
        LineGrading.Check(prompt, line, intent, communicated, errors);
}
