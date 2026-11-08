using Vocaterra.Game.Core.Voxel;
using Vocaterra.Nlp.Es.Normalization;

namespace Vocaterra.Game.Core.Npc;

/// <summary>
/// A block of the 3D scene the guide talks about (x east, y up, z south). The guide never reads the axes itself: it passes cells between the
/// facts (<see cref="IGuideFacts"/>) and the front-end, which puts the beacon or the arrow on them.
/// </summary>
/// <param name="X">The x of the block.</param>
/// <param name="Y">The y of the block.</param>
/// <param name="Z">The z of the block.</param>
public readonly record struct GuideCell(int X, int Y, int Z)
{
    /// <summary>A block as a cell.</summary>
    public static implicit operator GuideCell(BlockPos block) => new(block.X, block.Y, block.Z);

    /// <summary>The block this cell is.</summary>
    public BlockPos ToBlock() => new(X, Y, Z);
}

/// <summary>
/// Where the player stands, as the guide's facts need it: the block the middle of the body is in (<see cref="Cell"/>), the exact centre of the
/// body in 1/4096-block units (<see cref="CentreX"/>, <see cref="CentreZ"/>) and the facing (<see cref="Yaw"/>, angle steps: 0 north, 1024 east),
/// so the direction the guide gives is relative to where the player looks.
/// </summary>
/// <param name="Cell">The block under the middle of the player's body.</param>
/// <param name="CentreX">The centre of the body on x in 1/4096 block units.</param>
/// <param name="CentreZ">The centre of the body on z in 1/4096 block units.</param>
/// <param name="Yaw">The facing in angle steps; 0 is north.</param>
public readonly record struct GuideViewpoint(GuideCell Cell, long CentreX = 0, long CentreZ = 0, int Yaw = 0)
{
    /// <summary>A cell as a viewpoint (the centre and the facing are then zero).</summary>
    public static implicit operator GuideViewpoint(GuideCell cell) => new(cell);
}

/// <summary>
/// The nearest place of a catalogue noun from a viewpoint: where it is, how many blocks away and in which direction the player has to go, in
/// Spanish. <see cref="Blocks"/> is the distance the guide says ("a ocho bloques"); <see cref="Distance2"/> is the squared distance the nearest
/// choice compares, in the facts' own units.
/// </summary>
/// <param name="Cell">The tile or block found.</param>
/// <param name="Blocks">The distance in blocks, rounded.</param>
/// <param name="Direction">The direction as a Spanish phrase (<c>a la derecha</c>, <c>adelante, a la izquierda</c>); empty when it is here.</param>
/// <param name="Distance2">The squared distance the nearest choice used (tiles: the squared straight-line distance; blocks: the squared horizontal distance in units).</param>
public sealed record GuideNearest(GuideCell Cell, int Blocks, string Direction, long Distance2)
{
    /// <summary>The distance in blocks spelled in Spanish, for example <c>ocho</c>.</summary>
    public string BlocksInWords => SpanishNumbers.ToWords(Blocks);
}

/// <summary>
/// What the guide can say about the world: the nearest place of a noun, and the noun of the thing at a cell. The 3D scene implements it with
/// <see cref="WorldFacts"/> (blocks); the guide service reads it through this interface only.
/// </summary>
public interface IGuideFacts
{
    /// <summary>The nearest place of a catalogue noun from a viewpoint, or <see langword="null"/> when the world has none of it.</summary>
    /// <param name="from">Where the player is.</param>
    /// <param name="lemma">The catalogue noun (<c>árbol</c>).</param>
    GuideNearest? NearestOfNoun(GuideViewpoint from, string lemma);

    /// <summary>
    /// The nearest place of a catalogue noun from a viewpoint among the blocks joined to <paramref name="body"/> through blocks of the noun (face to face): one
    /// body of it, as the river is the water its point lies in and not the water of a well (village-plan V04). The nearest is chosen as in
    /// <see cref="NearestOfNoun"/>; <see langword="null"/> when the block at <paramref name="body"/> is not the noun.
    /// </summary>
    /// <param name="from">Where the player is.</param>
    /// <param name="lemma">The catalogue noun (<c>río</c>).</param>
    /// <param name="body">A block of the body (the river's point).</param>
    GuideNearest? NearestOfNounJoinedTo(GuideViewpoint from, string lemma, GuideCell body);

    /// <summary>
    /// The way from a viewpoint to a given block: its distance and its direction, as <see cref="NearestOfNoun"/> gives them for the block it finds. The guide says
    /// it for a place the scene names (#43 follow-up: the stall's point for <c>tienda</c>, <see cref="Navigation.ScenePlaces.Resolve"/>), which is no noun's nearest block.
    /// </summary>
    /// <param name="from">Where the player is.</param>
    /// <param name="cell">The block.</param>
    GuideNearest Toward(GuideViewpoint from, GuideCell cell);

    /// <summary>The catalogue nouns that name the thing at a cell (the water names <c>agua</c> and <c>río</c>), in lemma order; empty for air.</summary>
    /// <param name="cell">The tile or block.</param>
    IReadOnlyList<string> NounsAt(GuideCell cell);
}
