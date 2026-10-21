using Vocaterra.Game;
using Vocaterra.Simulation;
using Vocaterra.Simulation.Crafting;
using Vocaterra.Simulation.Items;
using Vocaterra.Simulation.Physics;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>What one step of the scripted run does.</summary>
public enum ScriptStepKind
{
    /// <summary>Type a line and send it (<c>--say</c>).</summary>
    Say,

    /// <summary>Move the stored time on without stepping the world (<c>--skip</c>).</summary>
    Skip,

    /// <summary>Close the dialogue and talk to the guide again, so that she may ask a review question (<c>--talk</c>).</summary>
    Talk,

    /// <summary>Walk until the player stands in the given column (<c>--walk</c>).</summary>
    Walk,

    /// <summary>Hold the mine command on the tile until it is gone, or give up (<c>--mine</c>).</summary>
    Mine,

    /// <summary>Select the hotbar slot that holds the item and place it on the tile (<c>--place</c>).</summary>
    Place,

    /// <summary>Select a hotbar slot (<c>--select</c>).</summary>
    Select,

    /// <summary>Open the inventory screen and keep it open for the screenshot (<c>--inventory</c>).</summary>
    Inventory,

    /// <summary>Craft one item from the inventory and the stations in reach (<c>--craft</c>).</summary>
    Craft,

    /// <summary>Open the crafting panel and keep it open for the screenshot (<c>--crafting</c>).</summary>
    CraftingPanel,
}

/// <summary>One step of the scripted run, in the order the command line gave it.</summary>
/// <param name="Kind">What the step does.</param>
/// <param name="Line">For <see cref="ScriptStepKind.Say"/>, the line.</param>
/// <param name="Minutes">For <see cref="ScriptStepKind.Skip"/>, the minutes of stored time to skip.</param>
/// <param name="Tile">For <see cref="ScriptStepKind.Walk"/> (the column is used), <see cref="ScriptStepKind.Mine"/> and <see cref="ScriptStepKind.Place"/>, the tile.</param>
/// <param name="Item">For <see cref="ScriptStepKind.Place"/> and <see cref="ScriptStepKind.Craft"/>, the item's noun (<c>madera</c>, <c>mesa</c>).</param>
/// <param name="Slot">For <see cref="ScriptStepKind.Select"/>, the hotbar slot as the number keys give it (1 to 10).</param>
public sealed record ScriptStep(ScriptStepKind Kind, string Line = "", int Minutes = 0, TilePoint? Tile = null, string Item = "", int Slot = 0)
{
    /// <summary>A step that says a line.</summary>
    public static ScriptStep Say(string line) => new(ScriptStepKind.Say, line);

    /// <summary>A step that skips stored time.</summary>
    public static ScriptStep Skip(int minutes) => new(ScriptStepKind.Skip, Minutes: minutes);

    /// <summary>A step that talks to the guide again.</summary>
    public static ScriptStep Talk() => new(ScriptStepKind.Talk);

    /// <summary>A step that walks to a column.</summary>
    public static ScriptStep Walk(int column) => new(ScriptStepKind.Walk, Tile: new TilePoint(column, 0));

    /// <summary>A step that mines a tile.</summary>
    public static ScriptStep Mine(TilePoint tile) => new(ScriptStepKind.Mine, Tile: tile);

    /// <summary>A step that places an item (by its noun) on a tile.</summary>
    public static ScriptStep Place(string item, TilePoint tile) => new(ScriptStepKind.Place, Item: item, Tile: tile);

    /// <summary>A step that selects a hotbar slot (1 to 10).</summary>
    public static ScriptStep Select(int slot) => new(ScriptStepKind.Select, Slot: slot);

    /// <summary>A step that opens the inventory screen.</summary>
    public static ScriptStep Inventory() => new(ScriptStepKind.Inventory);

    /// <summary>A step that crafts one item (by its noun).</summary>
    public static ScriptStep Craft(string item) => new(ScriptStepKind.Craft, Item: item);

    /// <summary>A step that opens the crafting panel.</summary>
    public static ScriptStep CraftingPanel() => new(ScriptStepKind.CraftingPanel);
}

/// <summary>
/// The scripted run for the screenshot: it walks the player to the guide when the script starts with a line (or a talk), waits for the body
/// to settle, opens the dialogue panel, types each line into the text box and asks the game to send it, one line at a time. The world steps
/// (<c>--walk</c>, <c>--mine</c>, <c>--place</c>, <c>--select</c>, <c>--inventory</c>) run in the same order, and each one closes the dialogue
/// first when it is open. It is an <see cref="IInputSource"/> because it is polled once per tick like the keyboard: it returns the movement it
/// wants, and <see cref="Commands"/> holds the world commands of the tick. The typing goes through the same <see cref="DialogueModel"/> calls a
/// person's keys make, and the send is done by the game between ticks (the model's <see cref="DialogueModel.SubmitRequested"/>), never from
/// inside a step. <see cref="ScriptStepKind.Skip"/> moves stored time on (the world does not step), and <see cref="ScriptStepKind.Talk"/> closes
/// and reopens the dialogue (which is when the guide may ask a review question).
/// </summary>
public sealed class ScriptedDialogue : IInputSource
{
    /// <summary>The walk gives up after this many ticks (the panel then opens wherever the player stands), so a blocked path cannot hang a run.</summary>
    public const int MaxWalkTicks = 900;

    /// <summary>A mining step gives up after this many ticks of holding (the tile is then left as it is and the run goes on).</summary>
    public const int MaxMineTicks = 1200;

    /// <summary>Ticks a walk may stand still before it jumps (a ledge in the way).</summary>
    private const int StuckTicks = 12;

    /// <summary>The reach of the player in tiles (the simulation's <see cref="GameSimulation.ReachInTiles"/>), for the report of a mining step.</summary>
    private const int Reach = GameSimulation.ReachInTiles;

    private readonly GameSession _session;
    private readonly DialogueModel _dialogue;
    private readonly IReadOnlyList<ScriptStep> _steps;
    private readonly int _settleTicks;
    private readonly int _gapTicks;
    private readonly List<PlayerCommand> _commands = [];
    private Phase _phase = Phase.Running;
    private int _wait;
    private int _walked;
    private int _next;
    private int _target;
    private TilePoint _tile;
    private int _mined;
    private string _minedName = string.Empty;
    private int _lastX;
    private int _stuck;
    private ItemId? _placed;
    private int _placedBefore;
    private bool? _inventoryRequest;
    private bool _inventoryShown;
    private bool? _craftingRequest;
    private bool _craftingShown;
    private int _craftIndex;
    private int _craftBefore;

    private enum Phase
    {
        Walking,
        Settling,
        Opening,
        Running,
        Sending,
        WalkingTo,
        Approaching,
        Mining,
        Placing,
        Crafting,
        Done,
    }

    /// <summary>Creates the script.</summary>
    /// <param name="session">The running game (for the guide's place and the player's body).</param>
    /// <param name="dialogue">The panel to drive.</param>
    /// <param name="steps">What to do, in order.</param>
    /// <param name="settleTicks">Ticks to wait after arriving at the guide, so the body has stopped.</param>
    /// <param name="gapTicks">Ticks between steps, so a screenshot taken mid-script shows each step.</param>
    public ScriptedDialogue(GameSession session, DialogueModel dialogue, IReadOnlyList<ScriptStep> steps, int settleTicks = 20, int gapTicks = 6)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(dialogue);
        ArgumentNullException.ThrowIfNull(steps);
        _session = session;
        _dialogue = dialogue;
        _steps = steps;
        _settleTicks = settleTicks;
        _gapTicks = gapTicks;
        _wait = gapTicks;
        if (steps.Count == 0 || steps[0].Kind is ScriptStepKind.Say or ScriptStepKind.Talk)
        {
            // A dialogue script (and an empty one) first walks to the guide and opens the panel, as it always has; a world script starts
            // where the player stands.
            _phase = Phase.Walking;
        }
    }

    /// <summary>Creates the script for lines only: one <see cref="ScriptStepKind.Say"/> step per line.</summary>
    public ScriptedDialogue(GameSession session, DialogueModel dialogue, IReadOnlyList<string> lines, int settleTicks = 20, int gapTicks = 6)
        : this(session, dialogue, (lines ?? throw new ArgumentNullException(nameof(lines))).Select(ScriptStep.Say).ToList(), settleTicks, gapTicks)
    {
    }

    /// <summary>Whether every step has been done.</summary>
    public bool IsDone => _phase == Phase.Done;

    /// <summary>The world commands of the tick the last <see cref="Poll"/> was for (mine, place, select). Read them after the poll.</summary>
    public IReadOnlyList<PlayerCommand> Commands => _commands;

    /// <summary>
    /// The inventory screen the script wants since the last call: <see langword="true"/> to open it, <see langword="false"/> to close it, or
    /// <see langword="null"/> for no change. Reading it clears it.
    /// </summary>
    public bool? TakeInventoryRequest()
    {
        var request = _inventoryRequest;
        _inventoryRequest = null;
        return request;
    }

    /// <summary>
    /// The crafting panel the script wants since the last call: <see langword="true"/> to open it, <see langword="false"/> to close it, or
    /// <see langword="null"/> for no change. Reading it clears it.
    /// </summary>
    public bool? TakeCraftingRequest()
    {
        var request = _craftingRequest;
        _craftingRequest = null;
        return request;
    }

    /// <inheritdoc />
    public InputActions Poll(long tick)
    {
        _commands.Clear();
        switch (_phase)
        {
            case Phase.Walking:
                return WalkToGuide();
            case Phase.Settling:
                if (--_wait <= 0)
                {
                    _phase = Phase.Opening;
                    _wait = _gapTicks;
                }

                break;
            case Phase.Opening:
                if (--_wait <= 0)
                {
                    _dialogue.Open(_session.BeginTalk()?.Question);
                    _phase = _steps.Count == 0 ? Phase.Done : Phase.Running;
                    _wait = _gapTicks;
                }

                break;
            case Phase.Running:
                if (--_wait <= 0)
                {
                    RunStep(_steps[_next]);
                }

                break;
            case Phase.Sending:
                if (!_dialogue.SubmitRequested)
                {
                    Advance();
                }

                break;
            case Phase.WalkingTo:
                return StepWalk();
            case Phase.Approaching:
                return StepApproach();
            case Phase.Mining:
                StepMine();
                break;
            case Phase.Placing:
                FinishPlace();
                break;
            case Phase.Crafting:
                FinishCraft();
                break;
            default:
                break;
        }

        return InputActions.None;
    }

    private void RunStep(ScriptStep step)
    {
        if (step.Kind is not ScriptStepKind.Inventory && _inventoryShown)
        {
            _inventoryShown = false;
            _inventoryRequest = false;
        }

        if (step.Kind is not ScriptStepKind.CraftingPanel && _craftingShown)
        {
            _craftingShown = false;
            _craftingRequest = false;
        }

        switch (step.Kind)
        {
            case ScriptStepKind.Say:
                if (!_dialogue.IsOpen)
                {
                    _dialogue.Open(_session.BeginTalk()?.Question);
                }

                _dialogue.Entry.Insert(step.Line);
                _dialogue.RequestSubmit();
                _phase = Phase.Sending;
                break;
            case ScriptStepKind.Skip:
                _session.SkipTime(step.Minutes * 60L);
                Console.WriteLine($"skip: {step.Minutes} min, {_session.SkippedSeconds} s skipped in all");
                Advance();
                break;
            case ScriptStepKind.Talk:
                _dialogue.Close();
                _session.EndReview();
                var review = _session.BeginTalk();
                _dialogue.Open(review?.Question);
                Console.WriteLine(review is null
                    ? "talk: no review"
                    : $"review: \"{review.Question}\" pointing at {review.Target.X},{review.Target.Y}");
                Advance();
                break;
            case ScriptStepKind.Walk:
                CloseForWorld();
                _target = step.Tile!.Value.X;
                _walked = 0;
                _stuck = 0;
                _lastX = _session.Simulation.Player.X;
                _phase = Phase.WalkingTo;
                break;
            case ScriptStepKind.Mine:
                CloseForWorld();
                _tile = step.Tile!.Value;
                _mined = 0;
                _walked = 0;
                _phase = Phase.Approaching;
                break;
            case ScriptStepKind.Place:
                CloseForWorld();
                StartPlace(step);
                break;
            case ScriptStepKind.Select:
                _commands.Add(PlayerCommand.SelectSlot(step.Slot - 1));
                Console.WriteLine($"select: slot {step.Slot}");
                Advance();
                break;
            case ScriptStepKind.Craft:
                CloseForWorld();
                StartCraft(step);
                break;
            case ScriptStepKind.CraftingPanel:
                CloseForWorld();
                _craftingShown = true;
                _craftingRequest = true;
                Console.WriteLine("crafting: open");
                Advance();
                break;
            default:
                _inventoryShown = true;
                _inventoryRequest = true;
                Console.WriteLine("inventory: open");
                Advance();
                break;
        }
    }

    private void CloseForWorld()
    {
        if (_dialogue.IsOpen)
        {
            _dialogue.Close();
            _session.EndReview();
        }
    }

    // The walk to the guide that a dialogue script starts with (the settle and the opening follow it).
    private InputActions WalkToGuide()
    {
        if (_session.IsNearGuide || ++_walked > MaxWalkTicks)
        {
            _phase = Phase.Settling;
            _wait = _settleTicks;
            return InputActions.None;
        }

        var player = _session.PlayerTile;
        return player.X < _session.Guide.Feet.X ? InputActions.Right : InputActions.Left;
    }

    private InputActions StepWalk()
    {
        var column = _session.PlayerTile.X;
        if (Math.Abs(column - _target) <= 1 || ++_walked > MaxWalkTicks)
        {
            Console.WriteLine($"walk: column {column} (wanted {_target}) after {_walked} ticks");
            Advance();
            return InputActions.None;
        }

        // A walk that makes no progress for a while has met a ledge: it jumps (held) until it is over the ledge.
        var x = _session.Simulation.Player.X;
        _stuck = x == _lastX ? _stuck + 1 : 0;
        _lastX = x;
        var direction = column < _target ? InputActions.Right : InputActions.Left;
        return _stuck >= StuckTicks ? direction | InputActions.Jump : direction;
    }

    // A mining step first walks (sideways, on the ground) until the tile is within the player's reach, then holds the mine command.
    private InputActions StepApproach()
    {
        if (InReach(_tile))
        {
            _minedName = TileDefinition.Get(_session.Simulation.World.GetTile(_tile.X, _tile.Y)).Name;
            _phase = Phase.Mining;
            return InputActions.None;
        }

        if (++_walked > MaxWalkTicks)
        {
            Console.WriteLine($"mine: {_tile.X},{_tile.Y} out of reach, the walk gave up");
            Advance();
            return InputActions.None;
        }

        return _tile.X > _session.PlayerTile.X ? InputActions.Right : InputActions.Left;
    }

    /// <summary>Whether the tile is within the reach the simulation allows (from the middle of the body to the middle of the tile).</summary>
    private bool InReach(TilePoint tile)
    {
        var body = _session.Simulation.Player;
        long dx = (tile.X * PhysicsConstants.UnitsPerTile) + (PhysicsConstants.UnitsPerTile / 2) - (body.X + (PhysicsConstants.PlayerWidth / 2));
        long dy = (tile.Y * PhysicsConstants.UnitsPerTile) + (PhysicsConstants.UnitsPerTile / 2) - (body.Y + (PhysicsConstants.PlayerHeight / 2));
        long reach = (long)Reach * PhysicsConstants.UnitsPerTile;
        return (dx * dx) + (dy * dy) <= reach * reach;
    }

    private void StepMine()
    {
        var world = _session.Simulation.World;
        var kind = world.GetTile(_tile.X, _tile.Y);
        if (kind == TileId.Air)
        {
            Console.WriteLine($"mine: {_tile.X},{_tile.Y} ({_minedName}) gone after {_mined} ticks");
            Advance();
            return;
        }

        if (_mined >= MaxMineTicks)
        {
            var far = Math.Max(Math.Abs(_session.PlayerTile.X - _tile.X), Math.Abs(_session.PlayerTile.Y - _tile.Y)) > Reach;
            Console.WriteLine($"mine: {_tile.X},{_tile.Y} ({TileDefinition.Get(kind).Name}) not mined after {_mined} ticks{(far ? ", out of reach" : string.Empty)}");
            Advance();
            return;
        }

        _commands.Add(PlayerCommand.Mine(_tile.X, _tile.Y));
        _mined++;
    }

    private void StartPlace(ScriptStep step)
    {
        var tile = step.Tile!.Value;
        var noun = _session.Nouns.ForLemma(step.Item);
        var item = noun?.Items.FirstOrDefault();
        var inventory = _session.Simulation.Inventory;
        var slot = -1;
        for (var i = 0; item is not null && i < Inventory.HotbarSize; i++)
        {
            if (!inventory.Slots[i].IsEmpty && inventory.Slots[i].Item == item)
            {
                slot = i;
                break;
            }
        }

        if (item is null || slot < 0)
        {
            Console.WriteLine($"place: no {step.Item} on the hotbar");
            Advance();
            return;
        }

        _placed = item;
        _placedBefore = inventory.Count(item.Value);
        _tile = tile;
        _commands.Add(PlayerCommand.SelectSlot(slot));
        _commands.Add(PlayerCommand.Place(tile.X, tile.Y));
        _phase = Phase.Placing;
    }

    // The tick after the place command: the tile says whether it went in.
    private void FinishPlace()
    {
        var item = _placed!.Value;
        var placed = ItemDefinition.Get(item).PlacesTile;
        var kind = _session.Simulation.World.GetTile(_tile.X, _tile.Y);
        var ok = placed is not null && kind == placed && _session.Simulation.Inventory.Count(item) == _placedBefore - 1;
        Console.WriteLine(ok
            ? $"place: {_tile.X},{_tile.Y} {TileDefinition.Get(kind).Name} from {item}"
            : $"place: refused at {_tile.X},{_tile.Y} (the tile is {TileDefinition.Get(kind).Name})");
        Advance();
    }

    // A craft step sends the craft command and reports on the next tick what the inventory shows.
    private void StartCraft(ScriptStep step)
    {
        _craftIndex = _session.Recipes.IndexOf(step.Item);
        if (_craftIndex < 0)
        {
            Console.WriteLine($"craft: no recipe for {step.Item}");
            Advance();
            return;
        }

        var item = _session.Recipes.Entries[_craftIndex].Rule.Output;
        _craftBefore = _session.Simulation.Inventory.Count(item);
        var result = _session.Simulation.CanCraft(_craftIndex);
        if (result != CraftResult.Done)
        {
            Console.WriteLine($"craft: {step.Item} refused ({result})");
            Advance();
            return;
        }

        _commands.Add(PlayerCommand.Craft(_craftIndex));
        _phase = Phase.Crafting;
    }

    // The tick after the craft command: the inventory says whether it went in.
    private void FinishCraft()
    {
        var entry = _session.Recipes.Entries[_craftIndex];
        var after = _session.Simulation.Inventory.Count(entry.Rule.Output);
        Console.WriteLine($"craft: {entry.Output.Lemma} made ({entry.Output.Lemma} {_craftBefore} -> {after})");
        Advance();
    }

    private void Advance()
    {
        _next++;
        if (_next < _steps.Count)
        {
            _phase = Phase.Running;
            _wait = _gapTicks;
        }
        else
        {
            _phase = Phase.Done;
        }
    }
}
