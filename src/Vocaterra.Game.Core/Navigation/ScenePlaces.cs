using Vocaterra.Game.Core.Npc;
using Vocaterra.Game.Core.Voxel;
using Vocaterra.Game.Core.World;

namespace Vocaterra.Game.Core.Navigation;

/// <summary>A place an NPC's directions found (village-plan V02): the scene's name for it, the word the beacon shows and the block the beacon stands on.</summary>
/// <param name="Name">The scene's name of the place (<c>river</c>, <c>stall</c>), or the catalogue noun when no label named it (<c>árbol</c>).</param>
/// <param name="Label">The word the beacon shows (<c>río</c>, <c>tienda</c>).</param>
/// <param name="Cell">The block the beacon stands on.</param>
public sealed record FoundPlace(string Name, string Label, GuideCell Cell);

/// <summary>
/// The places of the scene a word can name (village-plan V02): its named points (the sites, the points and the named props) and the word each shows (its label,
/// <c>río</c>, <c>tienda</c>). An NPC's directions to the place the learner asked for are resolved here (<see cref="NpcEffectKind.PointAtSlot"/>):
/// <list type="number">
/// <item>a word a label shows names the labelled point nearest the player (three houses show <c>casa</c>); when the word is also a catalogue noun of the block at
/// that point, the place is made of those blocks (the river's point lies in its water) and the beacon goes to the nearest of the blocks joined to the point, as the
/// villager's <c>point_at_noun</c> sent it to the river before V02 (since V04 the village's well holds water too, and is not the river);</item>
/// <item>a catalogue noun no label shows is the nearest block of the noun (<c>árbol</c>);</item>
/// <item>any other word, or a noun the world has none of, is no place: the NPC answers <c>&lt;npc&gt;.unknown_place</c>.</item>
/// </list>
/// Plain values read once from the built scene, so the turn worker reads it as freely as the game's thread.
/// </summary>
public sealed class ScenePlaces
{
    private readonly IReadOnlyDictionary<string, BlockPos> _points;
    private readonly IReadOnlyDictionary<string, string> _labels;
    private readonly Dictionary<string, List<string>> _byLabel = new(StringComparer.Ordinal);

    private ScenePlaces(IReadOnlyDictionary<string, BlockPos> points, IReadOnlyDictionary<string, string> labels)
    {
        _points = points;
        _labels = labels;
        foreach (var name in points.Keys.Order(StringComparer.Ordinal))
        {
            var label = LabelOf(name);
            if (!_byLabel.TryGetValue(label, out var names))
            {
                _byLabel[label] = names = [];
            }

            names.Add(name);
        }
    }

    /// <summary>No places: every word is a catalogue noun or nothing (the tests' NPCs with no scene).</summary>
    public static ScenePlaces None { get; } = new(new Dictionary<string, BlockPos>(StringComparer.Ordinal), new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>The places of a built scene.</summary>
    /// <param name="scene">The scene.</param>
    public static ScenePlaces Of(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return new ScenePlaces(scene.Points, scene.Labels);
    }

    /// <summary>The block of a named place, or <see langword="null"/> when the scene has no such place.</summary>
    /// <param name="name">The scene's name (<c>stall</c>).</param>
    public BlockPos? Point(string name) => _points.TryGetValue(name, out var point) ? point : null;

    /// <summary>
    /// The scene's name of a place found when the beacon stands on its point (<c>stall</c>), which its arrival is logged with; <see langword="null"/> for the nearest block of
    /// the river or of a noun, which the arrival names by the word, as a save would name it.
    /// </summary>
    /// <param name="found">A place <see cref="Resolve"/> found.</param>
    public string? PointNameOf(FoundPlace found)
    {
        ArgumentNullException.ThrowIfNull(found);
        return Point(found.Name) == found.Cell.ToBlock() ? found.Name : null;
    }

    /// <summary>The word a named place shows: its label, or its own name when it has none.</summary>
    /// <param name="name">The scene's name.</param>
    public string LabelOf(string name) => _labels.TryGetValue(name, out var label) ? label : name;

    /// <summary>
    /// The scene's name of the place an arrow on <paramref name="cell"/> showing <paramref name="label"/> points at (a loaded save keeps the word and the block, not
    /// the name), or <see langword="null"/> when no named place lies there: the arrow was on a noun's block. Ties go to the first name in ordinal order.
    /// </summary>
    /// <param name="cell">The arrow's block.</param>
    /// <param name="label">The word it shows.</param>
    public string? NameAt(GuideCell cell, string label) =>
        _byLabel.TryGetValue(label, out var names) ? names.FirstOrDefault(name => _points[name] == cell.ToBlock()) : null;

    /// <summary>The place a word names, seen from where the player stands (the rules are the type's), or <see langword="null"/> for no place.</summary>
    /// <param name="word">The word, as the verdict's slot holds it (its lemma: <c>río</c>).</param>
    /// <param name="from">Where the player stands.</param>
    /// <param name="facts">The world's blocks: the nouns of a block and the nearest block of a noun.</param>
    public FoundPlace? Resolve(string word, GuideViewpoint from, IGuideFacts facts)
    {
        ArgumentNullException.ThrowIfNull(word);
        ArgumentNullException.ThrowIfNull(facts);
        if (_byLabel.TryGetValue(word, out var names))
        {
            var name = Nearest(names, from.Cell);
            var point = (GuideCell)_points[name];
            if (facts.NounsAt(point).Contains(word, StringComparer.Ordinal) && facts.NearestOfNounJoinedTo(from, word, point) is { } block)
            {
                return new FoundPlace(name, word, block.Cell); // the river is its water: the nearest of the water its point lies in, not its one point nor a well's
            }

            return new FoundPlace(name, word, point);
        }

        return facts.NearestOfNoun(from, word) is { } nearest ? new FoundPlace(word, word, nearest.Cell) : null;
    }

    // The place of the names nearest the player on the ground (the squared distance in blocks); a tie goes to the first name, as the names are in ordinal order.
    private string Nearest(List<string> names, GuideCell from)
    {
        var best = names[0];
        var bestDistance = long.MaxValue;
        foreach (var name in names)
        {
            var point = _points[name];
            long dx = point.X - from.X;
            long dz = point.Z - from.Z;
            var distance = (dx * dx) + (dz * dz);
            if (distance < bestDistance)
            {
                best = name;
                bestDistance = distance;
            }
        }

        return best;
    }
}
