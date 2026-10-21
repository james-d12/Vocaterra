using Vocaterra.Nlp.Content;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.Game;

/// <summary>
/// One running game of the 3D scene (<c>--voxel</c>, S3): the voxel simulation over the bundle's scene, and the guide, the word counts, the
/// arrow and the turn worker around it (the shared part is <see cref="GuideSession"/>). The guide's facts are the blocks of the scene: the
/// nearest block of a noun and its direction from where the player faces (<see cref="WorldFacts3D"/>).
/// </summary>
/// <remarks>
/// The front-end owns the clock and the input: it calls <see cref="Step"/> once per tick with the body's commands, and it reads the simulation,
/// the arrow and the words. Nothing here reads the clock or draws a random number of its own.
/// </remarks>
public sealed class VoxelSession : GuideSession
{
    /// <summary>The arrow is taken down when the player comes within this many blocks of its target (the beacon's own reach).</summary>
    public const int ReachBlocks = 3;

    /// <summary>
    /// Starts a 3D game on a loaded bundle and a built scene.
    /// </summary>
    /// <param name="bundle">The content bundle (<c>content/game-v1</c>).</param>
    /// <param name="worldSeed">The world seed: the guide's random draws are keyed by it (the scene itself is built by the caller from the same seed).</param>
    /// <param name="startedAt">The stored time at tick 0, in Unix seconds.</param>
    /// <param name="nouns">The noun catalogue of the same bundle.</param>
    /// <param name="scene">The built scene; the simulation starts with the player at its spawn.</param>
    /// <param name="credit">The credit per kind of input and the gaps; <see cref="CreditRules.Default"/> when <see langword="null"/>.</param>
    public VoxelSession(ContentBundle bundle, ulong worldSeed, long startedAt, NounCatalog nouns, Scene scene, CreditRules? credit = null)
        : base(
            bundle,
            worldSeed,
            startedAt,
            nouns,
            WorldFacts3D.Live(scene?.Grid ?? throw new ArgumentNullException(nameof(scene)), nouns),
            RecipeBook.Load(bundle, nouns),
            playSession: 1,
            credit)
    {
        Scene = scene;
        Simulation = VoxelSimulation.Create(scene);
        Simulation.SetRecipes(Recipes.Rules);
    }

    /// <summary>The built scene (the world the facts read).</summary>
    public Scene Scene { get; }

    /// <summary>The 3D simulation: the body, the inventory, the tick counter and the events.</summary>
    public VoxelSimulation Simulation { get; }

    /// <summary>The block the crosshair is on (the nearest solid or gatherable block in reach), or <see langword="null"/>.</summary>
    public GuideCell? AimedCell => Simulation.Target is { } hit ? hit.Block : null;

    /// <inheritdoc />
    protected override long Tick => Simulation.Tick;

    /// <inheritdoc />
    protected override GuideViewpoint Viewpoint => new(Simulation.Player.FeetBlock, Simulation.Player.CenterX, Simulation.Player.CenterZ, Simulation.Player.Yaw);

    /// <inheritdoc />
    protected override Inventory LiveInventory => Simulation.Inventory;

    /// <inheritdoc />
    protected override IGuideFacts SnapshotFacts() => WorldFacts3D.Snapshot(Scene.Grid, Nouns);

    /// <inheritdoc />
    protected override string StateDigest() => Simulation.ComputeStateDigest();

    /// <summary>
    /// Advances the simulation one tick with the body's commands, then takes the arrow down when the player is within
    /// <see cref="ReachBlocks"/> of its target. The conversation does not run here (see <see cref="GuideSession.Say"/>).
    /// </summary>
    public void Step(IReadOnlyList<VoxelCommand> commands)
    {
        Simulation.Step(commands);
        if (Pointer is { } pointer)
        {
            const long Unit = VoxelPhysics.UnitsPerBlock;
            var dx = Math.Abs((pointer.Target.X * Unit) + (Unit / 2) - Simulation.Player.CenterX);
            var dz = Math.Abs((pointer.Target.Z * Unit) + (Unit / 2) - Simulation.Player.CenterZ);
            if (dx <= ReachBlocks * Unit && dz <= ReachBlocks * Unit)
            {
                Pointer = null;
            }
        }
    }
}
