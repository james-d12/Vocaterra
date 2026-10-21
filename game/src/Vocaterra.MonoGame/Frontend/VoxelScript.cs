using System.Globalization;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Voxel;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>The kinds of step the scripted 3D run takes.</summary>
public enum VoxelStepKind
{
    /// <summary>Face a direction (<c>--look yaw,pitch</c> in degrees).</summary>
    Look,

    /// <summary>Walk to a column (<c>--walk-to x,z</c>).</summary>
    WalkTo,

    /// <summary>Hold the gather command on a block (<c>--interact x,y,z</c>).</summary>
    Interact,

    /// <summary>Place an item in a cell (<c>--place item@x,y,z</c>).</summary>
    Place,

    /// <summary>Select a hotbar slot (<c>--select 1..10</c>).</summary>
    Select,
}

/// <summary>One step of the scripted 3D run, in plain values.</summary>
/// <param name="Kind">What the step does.</param>
/// <param name="Yaw">For <see cref="VoxelStepKind.Look"/>: the yaw in degrees (0 north, 90 east).</param>
/// <param name="Pitch">For <see cref="VoxelStepKind.Look"/>: the pitch in degrees (positive up).</param>
/// <param name="X">The block column (east) of a walk, interact or place.</param>
/// <param name="Y">The block layer (up) of an interact or place.</param>
/// <param name="Z">The block row (south) of a walk, interact or place.</param>
/// <param name="Item">For <see cref="VoxelStepKind.Place"/>: the item as typed (a Spanish noun such as <c>madera</c>, or the item's key such as <c>wood</c>).</param>
/// <param name="Slot">For <see cref="VoxelStepKind.Select"/>: the hotbar slot, 1 to 10 as the number keys give them.</param>
public sealed record VoxelStep(VoxelStepKind Kind, double Yaw = 0, double Pitch = 0, int X = 0, int Y = 0, int Z = 0, string Item = "", int Slot = 0)
{
    /// <summary>A step that faces a direction.</summary>
    public static VoxelStep Look(double yawDegrees, double pitchDegrees) => new(VoxelStepKind.Look, Yaw: yawDegrees, Pitch: pitchDegrees);

    /// <summary>A step that walks to a column.</summary>
    public static VoxelStep WalkTo(int x, int z) => new(VoxelStepKind.WalkTo, X: x, Z: z);

    /// <summary>A step that gathers a block.</summary>
    public static VoxelStep Interact(int x, int y, int z) => new(VoxelStepKind.Interact, X: x, Y: y, Z: z);

    /// <summary>A step that places an item.</summary>
    public static VoxelStep Place(string item, int x, int y, int z) => new(VoxelStepKind.Place, X: x, Y: y, Z: z, Item: item);

    /// <summary>A step that selects a hotbar slot (1 to 10).</summary>
    public static VoxelStep Select(int slot) => new(VoxelStepKind.Select, Slot: slot);

    /// <summary>Reads <c>x,z</c> (two non-negative integers).</summary>
    public static bool TryParseColumn(string value, out int x, out int z)
    {
        x = 0;
        z = 0;
        var parts = value.Split(',');
        return parts.Length == 2 && TryInt(parts[0], out x) && TryInt(parts[1], out z);
    }

    /// <summary>Reads <c>x,y,z</c> (three non-negative integers).</summary>
    public static bool TryParseBlock(string value, out int x, out int y, out int z)
    {
        x = 0;
        y = 0;
        z = 0;
        var parts = value.Split(',');
        return parts.Length == 3 && TryInt(parts[0], out x) && TryInt(parts[1], out y) && TryInt(parts[2], out z);
    }

    private static bool TryInt(string text, out int value) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}

/// <summary>
/// The scripted 3D run: the steps of <c>--look</c>, <c>--walk-to</c>, <c>--interact</c>, <c>--place</c> and <c>--select</c> in the order given, one
/// at a time, producing the simulation's commands each tick and reporting each outcome as a line. It walks with <c>Move</c> (world axes),
/// gathers by holding <c>Interact</c> until the block changes, and gives up on a step after a tick limit, so a blocked walk can never hang the
/// run. Nothing here reads a clock or draws a random number: it only looks at the simulation.
/// </summary>
public sealed class VoxelScript
{
    /// <summary>The distance in blocks at which a walk counts as arrived.</summary>
    public const double ArrivedDistance = 0.5;

    /// <summary>The most ticks a walk may take (60 seconds).</summary>
    public const int WalkTickLimit = 3600;

    /// <summary>The most ticks an interact may hold (10 seconds).</summary>
    public const int InteractTickLimit = 600;

    private const int StuckWindow = 20;
    private const double StuckDistance = 0.02;

    private readonly VoxelSimulation _sim;
    private readonly LookControl _look;
    private readonly IReadOnlyList<VoxelStep> _steps;
    private readonly Func<string, ItemId?> _resolveItem;
    private readonly Action<string> _log;
    private int _index;
    private bool _started;
    private int _stepTicks;
    private (double X, double Z) _windowStart;
    private int _windowTicks;
    private int _unstickTicks;
    private int _unstickCount;
    private BlockId _interactBlock;
    private int _interactDrops;

    /// <summary>Creates the script over a simulation and the look the game sends.</summary>
    /// <param name="sim">The simulation to read (the script never steps it).</param>
    /// <param name="look">The look control the <c>Look</c> steps set and the interact and place steps aim.</param>
    /// <param name="steps">The steps in order.</param>
    /// <param name="resolveItem">Maps the item as typed to an item (a noun or an item key), or <see langword="null"/>.</param>
    /// <param name="log">Where the outcome lines go.</param>
    public VoxelScript(VoxelSimulation sim, LookControl look, IReadOnlyList<VoxelStep> steps, Func<string, ItemId?> resolveItem, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(sim);
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(resolveItem);
        ArgumentNullException.ThrowIfNull(log);
        _sim = sim;
        _look = look;
        _steps = steps;
        _resolveItem = resolveItem;
        _log = log;
    }

    /// <summary>Whether every step has finished.</summary>
    public bool IsDone => _index >= _steps.Count;

    /// <summary>
    /// Fills <paramref name="commands"/> (cleared first) with the commands of the tick that is about to run, from the simulation as it stands
    /// before the tick. Steps that take no time (look, select) finish within the call, so several can happen in one tick.
    /// </summary>
    public void Fill(List<VoxelCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        commands.Clear();
        while (!IsDone)
        {
            var step = _steps[_index];
            if (!_started)
            {
                _started = true;
                _stepTicks = 0;
                Begin(step);
            }

            var finished = Advance(step, commands);
            _stepTicks++;
            if (!finished)
            {
                return;
            }

            _index++;
            _started = false;
            if (commands.Count > 0)
            {
                return; // a step that sent a command takes its tick
            }
        }
    }

    private static (double X, double Z) FeetOf(PlayerBody3D body) =>
        (body.CenterX / (double)VoxelPhysics.UnitsPerBlock, body.CenterZ / (double)VoxelPhysics.UnitsPerBlock);

    private void Begin(VoxelStep step)
    {
        switch (step.Kind)
        {
            case VoxelStepKind.WalkTo:
                _windowStart = FeetOf(_sim.Player);
                _windowTicks = 0;
                _unstickTicks = 0;
                _unstickCount = 0;
                break;
            case VoxelStepKind.Interact:
                _interactBlock = _sim.Grid.GetBlock(step.X, step.Y, step.Z);
                _interactDrops = DropCount(_interactBlock);
                AimAt(step.X, step.Y, step.Z);
                break;
            default:
                break;
        }
    }

    private bool Advance(VoxelStep step, List<VoxelCommand> commands)
    {
        switch (step.Kind)
        {
            case VoxelStepKind.Look:
                _look.SetDegrees(step.Yaw, step.Pitch);
                _log(string.Create(CultureInfo.InvariantCulture, $"look: yaw {step.Yaw:0.#}, pitch {step.Pitch:0.#}"));
                return true;
            case VoxelStepKind.Select:
                commands.Add(VoxelCommand.SelectSlot(step.Slot - 1));
                _log($"select: slot {step.Slot}");
                return true;
            case VoxelStepKind.WalkTo:
                return WalkTick(step, commands);
            case VoxelStepKind.Interact:
                return InteractTick(step, commands);
            default:
                return PlaceTick(step, commands);
        }
    }

    private bool WalkTick(VoxelStep step, List<VoxelCommand> commands)
    {
        var (x, z) = FeetOf(_sim.Player);
        var targetX = step.X + 0.5;
        var targetZ = step.Z + 0.5;
        var ex = targetX - x;
        var ez = targetZ - z;
        var distance = Math.Sqrt((ex * ex) + (ez * ez));
        if (distance <= ArrivedDistance)
        {
            _log(string.Create(CultureInfo.InvariantCulture, $"walk-to {step.X},{step.Z}: arrived at ({x:0.00}, {z:0.00}) after {_stepTicks} ticks"));
            return true;
        }

        if (_stepTicks >= WalkTickLimit)
        {
            _log(string.Create(CultureInfo.InvariantCulture, $"walk-to {step.X},{step.Z}: gave up at ({x:0.00}, {z:0.00}), {distance:0.0} blocks away, after {_stepTicks} ticks"));
            return true;
        }

        // Stuck: no progress over a window of ticks. Jump and sidestep for a while, to the other side each time.
        if (++_windowTicks >= StuckWindow)
        {
            var moved = Math.Sqrt(Math.Pow(x - _windowStart.X, 2) + Math.Pow(z - _windowStart.Z, 2));
            _windowStart = (x, z);
            _windowTicks = 0;
            if (moved < StuckDistance && _unstickTicks == 0)
            {
                _unstickTicks = 24 + (_unstickCount * 12);
                _unstickCount++;
            }
        }

        var dx = Math.Abs(ex) > 0.1 ? Math.Sign(ex) : 0;
        var dz = Math.Abs(ez) > 0.1 ? Math.Sign(ez) : 0;
        if (_unstickTicks > 0)
        {
            _unstickTicks--;
            var side = _unstickCount % 2 == 0 ? 1 : -1;
            if (Math.Abs(ex) >= Math.Abs(ez))
            {
                dz = side; // blocked going east or west: slide north or south
            }
            else
            {
                dx = side;
            }

            commands.Add(VoxelCommand.Jump);
        }

        commands.Add(VoxelCommand.Move(dx, dz));
        return false;
    }

    private bool InteractTick(VoxelStep step, List<VoxelCommand> commands)
    {
        var now = _sim.Grid.GetBlock(step.X, step.Y, step.Z);
        if (_interactBlock == BlockId.Air)
        {
            _log($"interact {step.X},{step.Y},{step.Z}: nothing there (air)");
            return true;
        }

        if (now != _interactBlock)
        {
            var gained = DropCount(_interactBlock) - _interactDrops;
            var drop = BlockDefinition.Get(_interactBlock).Drop;
            _log($"interact {step.X},{step.Y},{step.Z}: gathered {BlockDefinition.Get(_interactBlock).Name} in {_stepTicks} ticks (now {BlockDefinition.Get(now).Name}{(gained > 0 && drop is { } item ? $", +{gained} {ItemDefinition.Get(item).Name}" : string.Empty)})");
            return true;
        }

        if (_stepTicks >= InteractTickLimit)
        {
            _log($"interact {step.X},{step.Y},{step.Z}: not gathered after {_stepTicks} ticks (out of reach, or not gatherable)");
            return true;
        }

        commands.Add(VoxelCommand.Interact(step.X, step.Y, step.Z));
        return false;
    }

    private bool PlaceTick(VoxelStep step, List<VoxelCommand> commands)
    {
        if (_resolveItem(step.Item) is not { } item)
        {
            _log($"place: no item called '{step.Item}'");
            return true;
        }

        if (_stepTicks == 0)
        {
            AimAt(step.X, step.Y, step.Z);
            commands.Add(VoxelCommand.Place(item, step.X, step.Y, step.Z));
            return false;
        }

        var block = _sim.Grid.GetBlock(step.X, step.Y, step.Z);
        _log(block == BlockId.Air
            ? $"place {step.Item}@{step.X},{step.Y},{step.Z}: refused (out of reach, no {step.Item}, or not placeable there)"
            : $"place {step.Item}@{step.X},{step.Y},{step.Z}: placed {BlockDefinition.Get(block).Name}");
        return true;
    }

    // How many of the block's drop the inventory holds now.
    private int DropCount(BlockId block) => BlockDefinition.Get(block).Drop is { } drop ? _sim.Inventory.Count(drop) : 0;

    private void AimAt(int x, int y, int z)
    {
        var body = _sim.Player;
        const double U = VoxelPhysics.UnitsPerBlock;
        _look.LookAt(body.EyeX / U, body.EyeY / U, body.EyeZ / U, x + 0.5, y + 0.5, z + 0.5);
    }
}
