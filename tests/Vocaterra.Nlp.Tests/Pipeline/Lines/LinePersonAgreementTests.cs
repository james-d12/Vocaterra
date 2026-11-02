namespace Vocaterra.Nlp.Pipeline.Tests;

/// <summary>Lines of Spanish graded at the game's own prompts on <c>content/game-v1</c> (the CLI's path): the verb agrees with its subject.</summary>
public class LinePersonAgreementTests
{
    // Person agreement: the verb agrees with its subject, coordinated, inverted or implied; correct lines stay clean, a wrong person keeps its tag.
    public static TheoryData<string, string, string, bool, string[]> Rows => new()
    {
        { "villager-open", "luz hizo pan y yo fuimos al río", "state_action", true, ["conj.person=fui"] },
        { "villager-open", "tú y yo fui al río", "state_action", true, ["conj.person=fuimos"] },
        { "villager-open", "luz y yo somos amigas", "unknown", false, [] },
        { "villager-open", "ella y yo somos altas", "unknown", false, [] },
        { "villager-open", "tú y ella son altas", "unknown", false, [] },
        { "villager-open", "usted y yo vamos al río", "state_action", true, [] },
        { "farmer-open", "usted vender manzanas", "state_action", true, ["conj.person=vende"] },
        { "baker-open", "yo comprar pan", "state_action", true, ["conj.person=compro"] },
        { "villager-open", "tú o yo vamos", "state_action", true, [] },
        { "villager-open", "tú o yo voy", "state_action", true, [] },
        { "villager-open", "luz o mar va", "state_action", true, [] },
        { "villager-open", "luz o mar van", "state_action", true, [] },
        { "baker-open", "los panes costa dos monedas", "ask_price", true, ["conj.person=cuestan", "conj.irregular=cuesta"] },
        { "villager-open", "ellas dónde está", "unknown", false, ["conj.person=están"] },
        { "villager-open", "ella qué quieres", "unknown", false, ["conj.person=quiere"] },
        { "villager-open", "¿dónde estás yo?", "unknown", false, ["conj.person=estoy"] },
        { "villager-open", "¿dónde está tú?", "unknown", false, ["conj.person=estás"] },
        { "villager-open", "¿cómo está tú?", "greet", true, ["conj.person=estás"] },
        { "villager-open", "¿cómo estás usted?", "greet", true, ["conj.person=está"] },
        { "villager-open", "¿qué quiere tú?", "unknown", false, ["conj.person=quieres"] },
        { "villager-open", "¿dónde estás la herrería?", "ask_directions", true, ["conj.person=está"] },
        { "villager-open", "yo podo ir al río", "state_action", true, ["conj.irregular=puedo"] },
        { "villager-open", "no podo ir", "state_action", true, ["conj.irregular=puedo"] },
        { "villager-open", "tú podes venir", "unknown", false, ["conj.irregular=puedes"] },
        { "villager-open", "él pode venir mañana", "unknown", false, ["conj.irregular=puede"] },
        { "villager-open", "yo esto cansado", "unknown", false, ["conj.irregular=estoy"] },
        { "villager-open", "esto cansado", "unknown", false, ["conj.irregular=estoy"] },
        { "villager-open", "yo esto en casa", "unknown", false, ["conj.irregular=estoy"] },
        { "villager-open", "yo esto bien", "unknown", false, ["conj.irregular=estoy"] },
        { "villager-open", "luz son alta", "unknown", false, ["conj.person=es"] },
        { "villager-open", "pan son caro", "unknown", false, ["conj.person=es"] },
        { "villager-open", "ella compro pan", "state_action", true, ["conj.person=compra"] },
        { "lesson-like-bread", "yo gusto el pan", "state_like", true, ["conj.person=me gusta"] },
        { "lesson-like-bread", "me gusto el pan", "state_like", true, ["conj.person=gusta"] },
        { "lesson-like-apples", "me gusta las manzanas", "state_like", true, ["conj.person=gustan"] },
        { "farmer-open", "yo gusto el pan", "state_like", true, ["conj.person=me gusta"] },
        { "farmer-open", "me gusto el pan", "state_like", true, ["conj.person=gusta"] },
        { "villager-open", "me encanto el pan", "unknown", false, ["conj.person=encanta"] },
        { "villager-open", "yo encanto el pan", "unknown", false, ["conj.person=me encanta"] },
        { "lesson-will-fetch", "buscé madera", "state_action", true, ["conj.irregular=busqué"] },
        { "lesson-mar-fished", "mar pescé en el río", "state_action", true, ["conj.irregular=pesqué"] },
        { "villager-open", "ayer llegé al río", "unknown", false, ["conj.irregular=llegué"] },
        { "villager-open", "ayer empezé a trabajar", "unknown", false, ["conj.irregular=empecé"] },
        { "villager-open", "ayer pagé dos monedas", "unknown", false, ["conj.irregular=pagué"] },
        { "villager-open", "ayer jugé en el río", "unknown", false, ["conj.irregular=jugué"] },
        { "villager-open", "ayer tocé la puerta", "unknown", false, ["conj.irregular=toqué"] },
        { "smith-open", "qué tú hace", "ask_activity", true, ["conj.person=haces"] },
        { "villager-open", "él y yo somos altos", "unknown", false, [] },
        { "villager-open", "tú y ella sois altas", "unknown", false, [] },
        { "villager-open", "ella y él son altos", "unknown", false, [] },
        { "villager-open", "ella y yo estamos cansadas", "unknown", false, [] },
        { "villager-open", "tú y yo somos amigos", "unknown", false, [] },
        { "villager-open", "luz y yo estamos cansados", "unknown", false, [] },
        { "villager-open", "la casa y la tienda son bonitas", "unknown", false, [] },
        { "villager-open", "los niños y las niñas son altos", "unknown", false, [] },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void TheLineIsGradedAsItWas(string prompt, string line, string intent, bool communicated, string[] errors) =>
        LineGrading.Check(prompt, line, intent, communicated, errors);
}
