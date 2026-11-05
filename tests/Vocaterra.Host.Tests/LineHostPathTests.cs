using Vocaterra.Game.Core.Npc;
using Vocaterra.Nlp.Core.Determinism;

namespace Vocaterra.Host.Tests;

/// <summary>
/// Lines graded through the host's path (<c>--transcript</c>): a greeting that opens a line, or the listener's name said as a vocative, is taken out before the
/// core grades the rest, so some lines the core alone does not understand (luz quiero pan) are understood here. The core's view of the same lines is in the core's tests.
/// </summary>
public sealed class LineHostPathTests
{
    private static readonly StoredTimestamp Now = new(1_760_000_000);

    private static readonly Dictionary<string, string> Names = NpcRoster.Load(GameContent.Bundle).All
        .Where(static npc => npc.Name is not null)
        .ToDictionary(static npc => npc.Id, static npc => npc.Name!, StringComparer.Ordinal);

    // Correct Spanish stays clean; the deliberate mistakes keep their tags.
    public static TheoryData<string, string, string, bool, string[]> Rows => new()
    {
        { "baker-open", "hola luz quiero dos panes", "request_item", true, [] },
        { "farmer-open", "puedo pagar con fruta y con pan", "ask_pay", true, [] },
        { "smith-open", "me llamo domingo", "ask_name", true, [] },
        { "baker-open", "buenos días, quiero un pan", "request_item", true, [] },
        { "villager-open", "buenas tardes", "greet", true, [] },
        { "villager-open", "hola buenas tardes", "greet", true, [] },
        { "villager-open", "buenos días", "greet", true, [] },
        { "villager-open", "buenas noches", "greet", true, [] },
        { "villager-open", "buenos días senor", "greet", true, [] },
        { "villager-open", "hola me llamo marta", "greet", true, [] },
        { "villager-open", "buenos días, señor", "unknown", false, [] },
        { "farmer-open", "buenos días", "greet", true, [] },
        { "baker-open", "buenas tardes", "greet", true, [] },
        { "lesson-hello", "buenos días", "greet", true, [] },
        { "villager-open", "hola", "greet", true, [] },
        { "baker-open", "luz quiero pan", "request_item", true, [] },
        { "baker-open", "buenos días luz", "greet", true, [] },
        { "baker-open", "luz hará pan", "state_action", true, [] },
        { "baker-open", "luz y mar compraron pan", "state_action", true, [] },
        { "baker-open", "ayer compré pan para luz", "state_action", true, [] },
        { "baker-open", "gracias luz", "thank", true, [] },
        { "fisher-open", "mar pescaste en el río", "state_action", true, [] },
        { "fisher-open", "hola mar", "greet", true, [] },
        { "fisher-open", "mar está en el río", "unknown", false, [] },
        { "fisher-open", "mañana vendrá mar", "unknown", false, [] },
        { "smith-open", "domingo trabaja en la herrería", "state_action", true, [] },
        { "smith-open", "domingo tengo madera", "state_have", true, [] },
        { "smith-open", "qué hace domingo", "ask_activity", true, [] },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    public void TheLineIsGradedAsItWasThroughTheHost(string prompt, string line, string intent, bool communicated, string[] errors)
    {
        var verdict = new ConversationSession(GameContent.Bundle, listenerNames: Names).Evaluate(line, prompt, Now).Verdict;

        var actual = string.Join(' ', verdict.Errors.Select(static e => e.Expected.Length == 0 ? e.Tag : e.Tag + "=" + e.Expected));
        var expected = string.Join(' ', errors.Select(static e => e));
        Assert.Equal(
            $"{prompt} | {line} => {intent} communicated={communicated} errors=[{expected}]",
            $"{prompt} | {line} => {verdict.Intent.Value} communicated={verdict.Communicated} errors=[{actual}]");
    }
}
