using Vocaterra.Nlp.Core.Determinism;
using Vocaterra.Nlp.Core.Verdicts;
using Vocaterra.Nlp.Response.Templates;

namespace Vocaterra.Game;

public sealed partial class NpcService
{
    // The guide's own lines (S3, P3 to P6b): the facts of the world she speaks of, rendered from the bundle's pools with the bundle's template
    // engine (CR-016: the core cannot choose between the three cases of a world line, so the game renders them; the facts are host slots of the
    // turn's completion, so the replay record holds the inputs of the line).

    private const string FeedbackLinePrefix = "feedback.";
    private const string PointFound = "guide.point.found";
    private const string PointHere = "guide.point.here";
    private const string PointNone = "guide.point.none";
    private const string IdentifyFound = "guide.identify.found";
    private const string IdentifyNone = "guide.identify.none";
    private const string CraftRecipe = "guide.craft.recipe";
    private const string CraftStationUse = "guide.craft.station.use";
    private const string CraftStationNone = "guide.craft.station.none";
    private const string CraftLackOne = "guide.craft.lack.one";
    private const string CraftLackMany = "guide.craft.lack.many";
    private const string CraftReady = "guide.craft.ready";
    private const string CraftNone = "guide.craft.none";
    private const string CraftPartMass = "guide.craft.part.mass";
    private const string CraftPartCount = "guide.craft.part.count";
    private const string FindSource = "guide.find.source";
    private const string HaveOne = "guide.find.have.one";
    private const string HaveMany = "guide.find.have.many";
    private const string HaveMass = "guide.find.have.mass";
    private const string BlockUnit = "bloque";
    private const string HelpAsk = "guide.help.ask";
    private const string HelpWalk = "guide.help.walk";
    private const string NameRight = "guide.name.correct";
    private const string NameWrong = "guide.name.wrong";
    private const string NameNone = "guide.name.none";
    private const string ReviewAsk = "guide.review.ask";
    private const string ReviewStream = "guide.review";

    private (string Line, GuidePointer? Pointer) Point(Verdict verdict, GuideScene scene, SeededRng rng, Dictionary<string, string> facts)
    {
        var place = verdict.Slots.TryGetValue("place", out var lemma) ? lemma : string.Empty;
        var found = place.Length == 0 ? null : _facts.NearestOfNoun(scene.Player, place);

        // A thing with no tile in the world (the axe) that the player carries: say so instead of "there is none here".
        if (found is null && CountCarried(place, scene) > 0)
        {
            return (Have(place, scene, rng, facts), null);
        }

        return PointAt(place, found, rng, facts);
    }

    // The point-at line for the nearest tile of a noun (or the here / none line), with the arrow.
    private (string Line, GuidePointer? Pointer) PointAt(string place, GuideNearest? found, SeededRng rng, Dictionary<string, string> facts)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["place"] = place };
        if (found is null)
        {
            Merge(facts, values);

            return (Render(PointNone, SlotMap.From(values), rng), null);
        }

        if (found.Blocks == 0)
        {
            Merge(facts, values);
            return (Render(PointHere, SlotMap.From(values), rng), new GuidePointer(place, found.Cell, 0, string.Empty));
        }

        values["direction"] = found.Direction;
        values["count"] = found.Blocks.ToString(System.Globalization.CultureInfo.InvariantCulture);
        values["unit"] = BlockUnit;
        Merge(facts, values);
        return (Render(PointFound, SlotMap.From(values), rng), new GuidePointer(place, found.Cell, found.Blocks, found.Direction));
    }

    // How many of a catalogue noun's items the player carries (zero when the noun is unknown or the scene has no inventory).
    private int CountCarried(string lemma, GuideScene scene)
    {
        if (scene.Inventory is not { } inventory || _nouns.ForLemma(lemma) is not { } noun)
        {
            return 0;
        }

        return noun.Items.Sum(inventory.Count);
    }

    // "Tienes un hacha." / "Tienes dos hachas." / "Tienes tres bloques de madera.": what the player already carries.
    private string Have(string lemma, GuideScene scene, SeededRng rng, Dictionary<string, string> facts)
    {
        var noun = _nouns.ForLemma(lemma)!;
        var count = CountCarried(lemma, scene);
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["thing"] = lemma,
            ["count"] = count.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["unit"] = BlockUnit,
        };
        Merge(facts, values);
        var pool = noun.Mass ? HaveMass : count == 1 ? HaveOne : HaveMany;
        return Render(pool, SlotMap.From(values), rng);
    }

    // The player asks where a material is or where it comes from: what it comes from and the nearest of that (its own tile when it is one).
    private (string Line, GuidePointer? Pointer) Find(Verdict verdict, GuideScene scene, SeededRng rng, Dictionary<string, string> facts)
    {
        var material = verdict.Slots.TryGetValue("material", out var lemma) ? lemma : string.Empty;
        if (CountCarried(material, scene) > 0)
        {
            return (Have(material, scene, rng, facts), null);
        }

        var noun = _nouns.ForLemma(material);
        IReadOnlyList<string> sources = noun is { Sources.Count: > 0 } ? noun.Sources : [material];
        var bestSource = sources[0];
        GuideNearest? best = null;
        foreach (var source in sources)
        {
            var near = _facts.NearestOfNoun(scene.Player, source);
            if (near is not null && (best is null || near.Distance2 < best.Distance2))
            {
                best = near;
                bestSource = source;
            }
        }

        var lines = new List<string>();
        if (noun is not null && !string.Equals(bestSource, material, StringComparison.Ordinal))
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["material"] = material, ["source"] = bestSource, ["many"] = "2" };
            facts["thing"] = material;
            facts["source"] = bestSource;
            lines.Add(Render(FindSource, SlotMap.From(values), rng));
        }

        var (pointLine, pointer) = PointAt(bestSource, best, rng, facts);
        lines.Add(pointLine);
        return (string.Join(' ', lines), pointer);
    }


    // How to make something: the ingredients with their numbers, the station, and what the player still lacks.
    private string Craft(Verdict verdict, GuideScene scene, SeededRng rng, Dictionary<string, string> facts)
    {
        var lemma = verdict.Slots.TryGetValue("item", out var asked) ? asked : string.Empty;
        facts["thing"] = lemma;
        var recipe = lemma.Length == 0 ? null : _recipes.ForOutput(lemma);
        if (recipe is null)
        {
            return Render(CraftNone, SlotMap.Empty, rng);
        }

        var need = Phrase(recipe.Ingredients.Select(static i => (i.Noun, i.Count)).ToList(), rng);
        facts["need"] = need;
        var lines = new List<string> { Render(CraftRecipe, SlotMap.Empty.With("thing", lemma).With("need", need), rng) };
        if (recipe.Station is { } station)
        {
            facts["station"] = station.Lemma;
            lines.Add(Render(CraftStationUse, SlotMap.Empty.With("station", station.Lemma), rng));
        }
        else
        {
            lines.Add(Render(CraftStationNone, SlotMap.Empty, rng));
        }

        if (scene.Inventory is { } inventory)
        {
            var missing = recipe.Ingredients
                .Where(i => inventory.Count(i.Item) < i.Count)
                .Select(i => (i.Noun, Count: i.Count - inventory.Count(i.Item)))
                .ToList();
            if (missing.Count == 0)
            {
                lines.Add(Render(CraftReady, SlotMap.Empty, rng));
            }
            else
            {
                var lack = Phrase(missing, rng);
                facts["lack"] = lack;
                var single = missing.Count == 1 && missing[0].Count == 1;
                lines.Add(Render(single ? CraftLackOne : CraftLackMany, SlotMap.Empty.With("lack", lack), rng));
            }
        }

        return string.Join(' ', lines);
    }

    // "seis bloques de madera", "dos bloques de madera y seis bloques de piedra": each part from the bundle's pools, joined with "y".
    private string Phrase(IReadOnlyList<(NounEntry Noun, int Count)> parts, SeededRng rng)
    {
        var rendered = new List<string>();
        foreach (var (noun, count) in parts)
        {
            var values = SlotMap.Empty
                .With("count", count.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .With("noun", noun.Lemma)
                .With("unit", BlockUnit);
            rendered.Add(Render(noun.Mass ? CraftPartMass : CraftPartCount, values, rng));
        }

        return rendered.Count == 1
            ? rendered[0]
            : string.Join(", ", rendered.Take(rendered.Count - 1)) + " y " + rendered[^1];
    }

    private (string Line, string? Noun) Identify(GuideScene scene, SeededRng rng, Dictionary<string, string> facts)
    {
        var noun = scene.Cursor is { } cursor ? NounAt(cursor) : null;
        if (noun is null)
        {
            return (Render(IdentifyNone, SlotMap.Empty, rng), null);
        }

        facts["target"] = noun;
        return (Render(IdentifyFound, SlotMap.Empty.With("target", noun), rng), noun);
    }

    // The guide's check of a name: the cursor's tile is the expected noun. A name is right when it is that noun and no error was found on it;
    // the core cannot grade against a host value (CR-014), so the game does. The word counts use the check: a name that is not the tile's noun
    // earns no colour (D-game-6 as amended).
    private (string Line, NameCheck Check, string? Target) Name(Verdict verdict, GuideScene scene, SeededRng rng, Dictionary<string, string> facts)
    {
        // A thing may have several nouns (the water is agua and río): the name is right when it is any of them; the guide says the first.
        var names = scene.Cursor is { } cursor ? _facts.NounsAt(cursor) : [];
        var target = names.FirstOrDefault();
        if (target is null)
        {
            return (Render(NameNone, SlotMap.Empty, rng), NameCheck.NoTarget, null);
        }

        facts["target"] = target;
        var named = verdict.Slots.TryGetValue("object", out var lemma) ? lemma : string.Empty;
        var right = names.Contains(named, StringComparer.Ordinal) && verdict.Errors.Count == 0;
        return (Render(right ? NameRight : NameWrong, SlotMap.Empty.With("target", target), rng), right ? NameCheck.Right : NameCheck.Wrong, target);
    }

    private static void Merge(Dictionary<string, string> facts, Dictionary<string, string> values)
    {
        foreach (var (key, value) in values)
        {
            facts[key] = value;
        }
    }

    private string? NounAt(GuideCell cell) => _facts.NounsAt(cell).FirstOrDefault();

    private string Render(string poolId, SlotMap slots, SeededRng rng)
    {
        // The game renders its own line from the bundle's pool with the bundle's template engine (see the type's remarks, CR-016). The draw is
        // always made, so the number of draws per turn does not depend on whether a pool has one variant or three.
        var pool = _bundle.Dialogue.TryGetPool(poolId, out var found)
            ? found
            : throw new InvalidOperationException($"The bundle has no dialogue pool '{poolId}'.");
        var variant = DialogueSelector.Select(pool, rng);
        return _bundle.TemplateEngine.Render(variant, slots);
    }
}
