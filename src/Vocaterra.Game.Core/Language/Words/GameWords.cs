using System.Text.Json;
using Vocaterra.Game.Core.Content;
using Vocaterra.Nlp.Content;

namespace Vocaterra.Game.Core.Language.Words;

/// <summary>The direction phrases of the guide's answers (<c>adelante</c>, <c>a la izquierda</c>), from where the player faces.</summary>
/// <param name="Ahead">A block in front of the player.</param>
/// <param name="Behind">A block behind the player.</param>
/// <param name="Left">A block to the player's left.</param>
/// <param name="Right">A block to the player's right.</param>
public sealed record DirectionWords(string Ahead, string Behind, string Left, string Right);

/// <summary>The labels of the dialogue panel's speech buttons, in the target language.</summary>
/// <param name="Repeat">Play the last line again (<c>Repetir</c>).</param>
/// <param name="Slower">Play it slowly (<c>Más despacio</c>).</param>
/// <param name="Help">Ask for help (<c>Ayuda</c>).</param>
/// <param name="DontKnow">Say "I don't know" (<c>No sé</c>).</param>
/// <param name="Type">Type the answer (<c>Escribir</c>).</param>
public sealed record ButtonWords(string Repeat, string Slower, string Help, string DontKnow, string Type);

/// <summary>The headings of the support cards (S8, S16), in the target language.</summary>
/// <param name="CardTag">The label on a support card (<c>Puedes decir</c>).</param>
/// <param name="ListenChip">The audio-only chip (<c>Escuchar la frase</c>).</param>
/// <param name="KeyWordsHeading">The heading over the key-word cards (<c>Palabras nuevas</c>).</param>
public sealed record SupportWords(string CardTag, string ListenChip, string KeyWordsHeading);

/// <summary>The near future, <c>ir a</c> + infinitive (S27): the verb, its joiner, and the time words that make a present a plan.</summary>
/// <param name="Verb">The verb in the present (<c>ir</c>).</param>
/// <param name="Joiner">The word between it and the infinitive (<c>a</c>).</param>
/// <param name="TimeWords">The future-time words (<c>mañana</c>) that make a present tense a plan, which is no miss.</param>
public sealed record NearFutureWords(string Verb, string Joiner, IReadOnlyList<string> TimeWords);

/// <summary>
/// The target-language words the game itself uses (CONTENT_PLAN 3.3, C05), read from <c>game/words.json</c>: the guide's directions, the block unit and the
/// list joiner, the speech buttons and the support headings, the pronouns of the conjugation tables, the help and "no sé" lines, the near future, the
/// greeting, the letters the typing help lists, and the nouns the world's logic is about. They were constants in the code; here the grammar check sees
/// them, and a later target language needs no code change for them. The key words an NPC shows first left this file for the NPC's own (schema 2,
/// <see cref="Npc.NpcEntry.KeyWords"/>). Plain values; no clock.
/// </summary>
public sealed class GameWords
{
    /// <summary>The file's path inside the bundle folder.</summary>
    public const string RelativePath = "game/words.json";

    private readonly IReadOnlyDictionary<string, string> _pronouns;
    private readonly IReadOnlyDictionary<string, string> _nouns;

    private GameWords(
        DirectionWords directions,
        string blockUnit,
        string listJoiner,
        ButtonWords buttons,
        string typeOffer,
        SupportWords support,
        string unseen,
        IReadOnlyDictionary<string, string> pronouns,
        string helpLine,
        string dontKnowLine,
        NearFutureWords nearFuture,
        string greeting,
        string letters,
        IReadOnlyDictionary<string, string> nouns)
    {
        Directions = directions;
        BlockUnit = blockUnit;
        ListJoiner = listJoiner;
        Buttons = buttons;
        TypeOffer = typeOffer;
        Support = support;
        Unseen = unseen;
        _pronouns = pronouns;
        HelpLine = helpLine;
        DontKnowLine = dontKnowLine;
        NearFuture = nearFuture;
        Greeting = greeting;
        Letters = letters;
        _nouns = nouns;
    }

    /// <summary>The direction phrases of the guide's answers.</summary>
    public DirectionWords Directions { get; }

    /// <summary>The unit of a distance or a mass in the guide's answers (<c>bloque</c>; the template pluralises it).</summary>
    public string BlockUnit { get; }

    /// <summary>The word that joins the last two parts of a list in the guide's answers (<c>y</c>: <c>dos bloques de madera y seis bloques de piedra</c>).</summary>
    public string ListJoiner { get; }

    /// <summary>The labels of the speech buttons.</summary>
    public ButtonWords Buttons { get; }

    /// <summary>The panel's offer to type when speech is off (<c>¿Escribes?</c>).</summary>
    public string TypeOffer { get; }

    /// <summary>The headings of the support cards.</summary>
    public SupportWords Support { get; }

    /// <summary>The label of a thing not seen yet, and of the beacon's quiz (<c>¿?</c>).</summary>
    public string Unseen { get; }

    /// <summary>The line the help button says (<c>ayuda</c>).</summary>
    public string HelpLine { get; }

    /// <summary>The line the "I don't know" button says (<c>no sé</c>).</summary>
    public string DontKnowLine { get; }

    /// <summary>The near future, <c>ir a</c> + infinitive.</summary>
    public NearFutureWords NearFuture { get; }

    /// <summary>The greeting the mic check asks for and the worker warms up with (<c>hola</c>; also the hello phrase's id).</summary>
    public string Greeting { get; }

    /// <summary>The letters the typing help lists (<c>á é í ó ú ñ ¿ ¡</c>), as the hints write them.</summary>
    public string Letters { get; }

    /// <summary>The subject pronoun the conjugation table writes for a person and number (<c>1sg</c> gives <c>yo</c>), or <see langword="null"/>.</summary>
    /// <param name="person">The person, 1 to 3.</param>
    /// <param name="number"><c>sg</c> or <c>pl</c>.</param>
    public string? Pronoun(string person, string number) => _pronouns.TryGetValue(person + number, out var pronoun) ? pronoun : null;

    /// <summary>
    /// The catalogue noun the game's logic means by a role (<c>river</c> gives <c>río</c>: the beacon to the river, which the bottle is filled at; the other
    /// nouns the logic names are the items' own, from the catalogue).
    /// </summary>
    /// <param name="role">The role.</param>
    /// <exception cref="KeyNotFoundException">The file has no noun for the role.</exception>
    public string Noun(string role) => _nouns[role];

    /// <summary>The roles the game's logic gives a noun.</summary>
    public static IReadOnlyList<string> NounRoles { get; } = ["river"];

    /// <summary>The persons and numbers the conjugation table writes a pronoun for.</summary>
    public static IReadOnlyList<string> PronounKeys { get; } = ["1sg", "2sg", "3sg", "1pl", "3pl"];

    /// <summary>Reads the words from the bundle's verified host file.</summary>
    /// <param name="bundle">The bundle, loaded with hash verification.</param>
    /// <exception cref="InvalidDataException">The bundle has no such host file, or the file is malformed.</exception>
    public static GameWords Load(ContentBundle bundle) => Parse(GameData.HostText(bundle, RelativePath));

    /// <summary>Builds the words from the file's text.</summary>
    /// <param name="json">The contents of <c>game/words.json</c>.</param>
    /// <exception cref="InvalidDataException">The text is malformed or lacks a word.</exception>
    public static GameWords Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.GetProperty("schemaVersion").GetInt32() != 2)
            {
                throw new InvalidDataException($"{RelativePath} must have schemaVersion 2.");
            }

            var directions = root.GetProperty("directions");
            var buttons = root.GetProperty("buttons");
            var support = root.GetProperty("support");
            var nearFuture = root.GetProperty("nearFuture");
            var pronouns = Map(root, "pronouns", PronounKeys);
            var nouns = Map(root, "nouns", NounRoles);
            return new GameWords(
                new DirectionWords(Text(directions, "ahead"), Text(directions, "behind"), Text(directions, "left"), Text(directions, "right")),
                Text(root, "blockUnit"),
                Text(root, "listJoiner"),
                new ButtonWords(Text(buttons, "repeat"), Text(buttons, "slower"), Text(buttons, "help"), Text(buttons, "dontKnow"), Text(buttons, "type")),
                Text(root, "typeOffer"),
                new SupportWords(Text(support, "cardTag"), Text(support, "listenChip"), Text(support, "keyWordsHeading")),
                Text(root, "unseen"),
                pronouns,
                Text(root, "helpLine"),
                Text(root, "dontKnowLine"),
                new NearFutureWords(Text(nearFuture, "verb"), Text(nearFuture, "joiner"), Texts(nearFuture, "timeWords")),
                Text(root, "greeting"),
                Text(root, "letters"),
                nouns);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException($"{RelativePath} is malformed: {ex.Message}", ex);
        }
    }

    /// <summary>Every word of the file, by a path inside it (<c>directions.left</c>), for the grammar check (CONTENT_PLAN C05).</summary>
    public IReadOnlyList<(string Path, string Text)> All()
    {
        var all = new List<(string, string)>
        {
            ("directions.ahead", Directions.Ahead), ("directions.behind", Directions.Behind), ("directions.left", Directions.Left), ("directions.right", Directions.Right),
            ("blockUnit", BlockUnit), ("listJoiner", ListJoiner),
            ("buttons.repeat", Buttons.Repeat), ("buttons.slower", Buttons.Slower), ("buttons.help", Buttons.Help), ("buttons.dontKnow", Buttons.DontKnow), ("buttons.type", Buttons.Type),
            ("typeOffer", TypeOffer), ("support.cardTag", Support.CardTag), ("support.listenChip", Support.ListenChip), ("support.keyWordsHeading", Support.KeyWordsHeading),
            ("helpLine", HelpLine), ("dontKnowLine", DontKnowLine), ("greeting", Greeting),
            ("nearFuture.verb", NearFuture.Verb), ("nearFuture.joiner", NearFuture.Joiner),
        };
        all.AddRange(PronounKeys.Select(key => ("pronouns." + key, _pronouns[key])));
        all.AddRange(NearFuture.TimeWords.Select((word, i) => ("nearFuture.timeWords[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]", word)));
        all.AddRange(NounRoles.Select(role => ("nouns." + role, _nouns[role])));
        return all;
    }

    private static Dictionary<string, string> Map(JsonElement root, string name, IReadOnlyList<string> keys)
    {
        var element = root.GetProperty(name);
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            map[key] = Text(element, key);
        }

        return map;
    }

    private static List<string> Texts(JsonElement element, string name)
    {
        var list = element.GetProperty(name).EnumerateArray().Select(static e => e.GetString() ?? string.Empty).ToList();
        return list.Count > 0 && list.All(static w => w.Trim().Length > 0)
            ? list
            : throw new InvalidDataException($"{RelativePath}: '{name}' must list at least one word, none empty.");
    }

    private static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString()!.Trim().Length > 0
            ? value.GetString()!
            : throw new InvalidDataException($"{RelativePath}: the word '{name}' is missing or empty.");
}
