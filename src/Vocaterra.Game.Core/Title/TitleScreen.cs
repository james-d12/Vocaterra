using System.Globalization;
using Vocaterra.Game.Core.Input;
using Vocaterra.Game.Core.Menu;
using Vocaterra.Game.Core.Worlds;

namespace Vocaterra.Game.Core.Title;

/// <summary>
/// The title screen's state machine (Q01), shown before any world is built: the main page (New game, Load game, Settings, Quit), the New game page (the world's name, seed and type, Create), the Load
/// page (the worlds, newest played first, with Load and Delete), the confirmation before a delete, and the settings page, which is the Esc menu's own (<see cref="MenuScreen"/>, opened directly) so a
/// change there is made and kept the same way. The pages are chosen by keys and by the mouse; the front-end draws them (<see cref="ITitleMetrics"/> tells where things are) and reads
/// <see cref="Choice"/> to know that the player quit or chose a world. A world is made on disk when Create is chosen (<see cref="WorldStore.Create"/>) and deleted when the confirmation is.
/// The random seed is the front-end's: it gives the function. Terrain options (Q03) go between <see cref="TitleSlot.WorldType"/> and <see cref="TitleSlot.Create"/> on the New page.
/// </summary>
public sealed class TitleScreen
{
    private readonly WorldStore _store;
    private readonly ITitleMetrics _metrics;
    private readonly MenuScreen _settings;
    private readonly Func<ulong> _randomSeed;
    private readonly Func<int, string> _defaultName;
    private readonly TextEditKeys _edits = new();
    private IReadOnlyList<WorldEntry> _worlds = [];
    private IReadOnlyList<string> _names = [];
    private (int X, int Y)? _lastPointer;
    private bool _freshName;
    private WorldEntry? _pendingDelete;

    /// <summary>Creates the title screen.</summary>
    /// <param name="store">The worlds on disk.</param>
    /// <param name="metrics">How the pages are drawn.</param>
    /// <param name="settings">The settings page's menu (its rows over the player's settings).</param>
    /// <param name="randomSeed">Draws a random seed (the Random button, and a blank seed field).</param>
    /// <param name="defaultName">The default name of the world number N ("World N", the locale's words).</param>
    public TitleScreen(WorldStore store, ITitleMetrics metrics, MenuScreen settings, Func<ulong> randomSeed, Func<int, string> defaultName)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(randomSeed);
        ArgumentNullException.ThrowIfNull(defaultName);
        _store = store;
        _metrics = metrics;
        _settings = settings;
        _randomSeed = randomSeed;
        _defaultName = defaultName;
        Refresh();
    }

    /// <summary>The page shown.</summary>
    public TitlePage Page { get; private set; }

    /// <summary>The chosen thing: a <see cref="TitleSlot"/> on the main, New and confirmation pages, the chosen world's index on the Load page.</summary>
    public int Selected { get; private set; }

    /// <summary>What the pointer is on (a slot, or a world's index on the Load page), or <see langword="null"/>.</summary>
    public int? Hover { get; private set; }

    /// <summary>The first world shown on the Load page.</summary>
    public int LoadScroll { get; private set; }

    /// <summary>The worlds on disk, newest played first (read when a page that lists or names them opens).</summary>
    public IReadOnlyList<WorldEntry> Worlds => _worlds;

    /// <summary>Whether there is a world to load (Load game is greyed when there is none).</summary>
    public bool HasWorlds => _worlds.Count > 0;

    /// <summary>The New page's world name field.</summary>
    public TextField Name { get; } = new(WorldNames.MaxLength);

    /// <summary>The New page's seed field.</summary>
    public TextField Seed { get; } = new(40);

    /// <summary>The New page's world type.</summary>
    public WorldKind WorldType { get; private set; }

    /// <summary>Why the name typed is refused, or <see cref="NameProblem.None"/>.</summary>
    public NameProblem NameIssue => WorldNames.Check(Name.Text, _names);

    /// <summary>The line shown under the page (a refusal, a failure, a world deleted), or <see langword="null"/>.</summary>
    public TitleNotice? Notice { get; private set; }

    /// <summary>The world the confirmation page asks about, or <see langword="null"/>.</summary>
    public WorldEntry? PendingDelete => _pendingDelete;

    /// <summary>What the player chose, once they have: Quit, or a world to start. The front-end ends the title when this is set.</summary>
    public TitleChoice? Choice { get; private set; }

    /// <summary>The settings page's menu (drawn by the Esc menu's own page).</summary>
    public MenuScreen SettingsMenu => _settings;

    /// <summary>Whether the front-end should deliver typed characters (<see cref="Type"/>): a text field has the focus.</summary>
    public bool TakesText => Page == TitlePage.New && Selected is TitleSlot.NameField or TitleSlot.SeedField;

    /// <summary>Opens the title on a page (the start of the title, and a scripted run's page).</summary>
    /// <param name="page">The page.</param>
    /// <param name="notice">A line to show on it (a world that did not load), or <see langword="null"/>.</param>
    public void Start(TitlePage page, TitleNotice? notice = null)
    {
        Refresh();
        switch (page)
        {
            case TitlePage.New:
                OpenNew();
                break;
            case TitlePage.Load when HasWorlds:
                OpenLoad();
                break;
            case TitlePage.Settings:
                OpenSettings();
                break;
            case TitlePage.ConfirmDelete when HasWorlds:
                OpenLoad();
                AskDelete();
                break;
            default:
                GoTo(TitlePage.Main, HasWorlds && page == TitlePage.Load ? TitleSlot.LoadGame : TitleSlot.NewGame);
                break;
        }

        Notice = notice ?? Notice;
    }

    /// <summary>Fills the New page's fields (a scripted run's picture of a refusal): the name and the seed's text.</summary>
    /// <param name="name">The name, or <see langword="null"/> to keep the default.</param>
    /// <param name="seed">The seed's text, or <see langword="null"/> to leave it blank.</param>
    public void Prefill(string? name, string? seed)
    {
        if (name is not null)
        {
            Name.Set(name);
            _freshName = false;
        }

        if (seed is not null)
        {
            Seed.Set(seed);
        }
    }

    /// <summary>A character the window typed: it goes in the field that has the focus on the New page, and is ignored anywhere else.</summary>
    /// <param name="c">The character.</param>
    public void Type(char c)
    {
        if (Choice is not null || Page != TitlePage.New || char.IsControl(c))
        {
            return;
        }

        if (Selected == TitleSlot.NameField)
        {
            if (_freshName)
            {
                Name.Set(string.Empty); // the default name is replaced by what is typed over it
                _freshName = false;
            }

            _ = Name.Insert(c);
        }
        else if (Selected == TitleSlot.SeedField)
        {
            _ = Seed.Insert(c);
        }
    }

    /// <summary>Takes one frame of the keyboard and the mouse.</summary>
    /// <param name="input">The frame's input.</param>
    public void HandleInput(IInputFrame input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (Choice is not null)
        {
            return;
        }

        if (Page == TitlePage.Settings)
        {
            _settings.HandleInput(input);
            if (!_settings.IsOpen)
            {
                GoTo(TitlePage.Main, TitleSlot.Settings);
            }

            return;
        }

        var edits = _edits.Step(input);
        var page = Page;
        HandleMouse(input);
        if (Choice is not null || Page != page)
        {
            return; // a click chose, or changed the page: the keys of this frame belong to the page it left
        }

        if (input.Pressed(GameKey.Escape))
        {
            Back();
            return;
        }

        switch (Page)
        {
            case TitlePage.Main:
                KeysOnMain(input);
                break;
            case TitlePage.New:
                KeysOnNew(input, edits);
                break;
            case TitlePage.Load:
                KeysOnLoad(input);
                break;
            default:
                KeysOnConfirm(input);
                break;
        }
    }

    private static int Move(IInputFrame input) => (input.Pressed(GameKey.Down) ? 1 : 0) - (input.Pressed(GameKey.Up) ? 1 : 0);

    private void Refresh()
    {
        _worlds = _store.List(probeSaves: true);
        _names = [.. _worlds.Select(static w => w.Name)];
        if (Page == TitlePage.Load)
        {
            LoadScroll = Math.Clamp(LoadScroll, 0, MaxLoadScroll()); // the metrics are asked only on the page that has a list
        }
    }

    private void GoTo(TitlePage page, int selected)
    {
        Page = page;
        Selected = selected;
        Hover = null;
        _lastPointer = null;
        Notice = null;
        if (page != TitlePage.ConfirmDelete)
        {
            _pendingDelete = null;
        }
    }

    private void OpenNew()
    {
        Refresh();
        GoTo(TitlePage.New, TitleSlot.NameField);
        Name.Set(WorldNames.Next(_names, _defaultName));
        _freshName = true;
        Seed.Set(string.Empty);
    }

    private void OpenLoad()
    {
        Refresh();
        GoTo(TitlePage.Load, 0);
        LoadScroll = 0;
    }

    private void OpenSettings()
    {
        _settings.OpenSettings();
        GoTo(TitlePage.Settings, 0);
    }

    private void Back()
    {
        switch (Page)
        {
            case TitlePage.New:
                GoTo(TitlePage.Main, TitleSlot.NewGame);
                break;
            case TitlePage.Load:
                GoTo(TitlePage.Main, TitleSlot.LoadGame);
                break;
            case TitlePage.ConfirmDelete:
                var index = _pendingDelete is { } asked ? Math.Max(0, _worlds.ToList().IndexOf(asked)) : 0;
                GoTo(TitlePage.Load, Math.Min(index, Math.Max(0, _worlds.Count - 1)));
                KeepVisible();
                break;
            default:
                break; // the main page has no page behind it: Quit is its button
        }
    }

    // ---- the mouse ----

    private void HandleMouse(IInputFrame input)
    {
        var pointer = (input.PointerX, input.PointerY);
        var moved = _lastPointer != pointer;
        _lastPointer = pointer;
        if (Page == TitlePage.Load && input.Wheel != 0)
        {
            LoadScroll = Math.Clamp(LoadScroll - Math.Sign(input.Wheel), 0, MaxLoadScroll());
        }

        var slot = _metrics.SlotAt(Page, LoadScroll, _worlds.Count, input.PointerX, input.PointerY);
        Hover = slot;
        if (slot is { } over && moved && over >= 0 && Page != TitlePage.New && !(Page == TitlePage.Main && over == TitleSlot.LoadGame && !HasWorlds))
        {
            Selected = over; // moving the pointer over a button or a world chooses it (not on the New page: a field keeps the focus while the pointer wanders)
        }

        if (!input.LeftClicked || slot is not { } clicked)
        {
            return;
        }

        Activate(clicked, byKey: false);
    }

    // ---- the keys ----

    private void KeysOnMain(IInputFrame input)
    {
        var step = Move(input);
        if (step != 0)
        {
            var next = Selected;
            for (var i = 0; i < TitleSlot.MainCount; i++)
            {
                next = (next + step + TitleSlot.MainCount) % TitleSlot.MainCount;
                if (next != TitleSlot.LoadGame || HasWorlds)
                {
                    break; // Load game is greyed when there is no world to load: the cursor passes it
                }
            }

            Selected = next;
        }

        if (input.Pressed(GameKey.Enter) || input.Pressed(GameKey.Space))
        {
            Activate(Selected, byKey: true);
        }
    }

    private void KeysOnNew(IInputFrame input, TextEdits edits)
    {
        var step = Move(input);
        if (input.Pressed(GameKey.Tab))
        {
            step = input.Shift ? -1 : 1;
        }

        if (step != 0)
        {
            Focus((Selected + step + TitleSlot.NewCount) % TitleSlot.NewCount);
        }

        if (Selected is TitleSlot.NameField or TitleSlot.SeedField)
        {
            EditField(Selected == TitleSlot.NameField ? Name : Seed, input, edits);
        }
        else if (Selected == TitleSlot.WorldType)
        {
            var change = (input.Pressed(GameKey.Right) ? 1 : 0) - (input.Pressed(GameKey.Left) ? 1 : 0);
            if (change != 0)
            {
                CycleType();
            }
        }

        if (input.Pressed(GameKey.Enter) || (input.Pressed(GameKey.Space) && Selected is not (TitleSlot.NameField or TitleSlot.SeedField)))
        {
            Activate(Selected, byKey: true);
        }
    }

    private void EditField(TextField field, IInputFrame input, TextEdits edits)
    {
        if ((edits.Backspace || edits.Delete) && Selected == TitleSlot.NameField && _freshName)
        {
            Name.Set(string.Empty);
            _freshName = false;
            return;
        }

        if (edits.Backspace)
        {
            field.Backspace();
        }

        if (edits.Delete)
        {
            field.Delete();
        }

        if (edits.Left)
        {
            field.Left();
        }

        if (edits.Right)
        {
            field.Right();
        }

        if (input.Pressed(GameKey.Home))
        {
            field.Home();
        }

        if (input.Pressed(GameKey.End))
        {
            field.End();
        }
    }

    private void KeysOnLoad(IInputFrame input)
    {
        if (_worlds.Count == 0)
        {
            return;
        }

        var step = Move(input) + (input.Pressed(GameKey.PageDown) ? Math.Max(1, _metrics.LoadCapacity - 1) : 0) - (input.Pressed(GameKey.PageUp) ? Math.Max(1, _metrics.LoadCapacity - 1) : 0);
        if (input.Pressed(GameKey.Home))
        {
            step = -Selected;
        }
        else if (input.Pressed(GameKey.End))
        {
            step = _worlds.Count - 1 - Selected;
        }

        if (step != 0)
        {
            Selected = Math.Clamp(Selected + step, 0, _worlds.Count - 1);
            KeepVisible();
        }

        if (input.Pressed(GameKey.Enter) || input.Pressed(GameKey.Space))
        {
            Activate(TitleSlot.LoadAction, byKey: true);
        }
        else if (input.Pressed(GameKey.Delete))
        {
            Activate(TitleSlot.DeleteAction, byKey: true);
        }
    }

    private void KeysOnConfirm(IInputFrame input)
    {
        if (Move(input) != 0 || input.Pressed(GameKey.Left) || input.Pressed(GameKey.Right) || input.Pressed(GameKey.Tab))
        {
            Selected = Selected == TitleSlot.ConfirmDelete ? TitleSlot.ConfirmKeep : TitleSlot.ConfirmDelete;
        }

        if (input.Pressed(GameKey.Enter) || input.Pressed(GameKey.Space))
        {
            Activate(Selected, byKey: true);
        }
    }

    // ---- what a slot does ----

    private void Activate(int slot, bool byKey)
    {
        if (slot == TitleSlot.Back)
        {
            Back();
            return;
        }

        switch (Page)
        {
            case TitlePage.Main:
                ActivateMain(slot);
                break;
            case TitlePage.New:
                ActivateNew(slot, byKey);
                break;
            case TitlePage.Load:
                ActivateLoad(slot);
                break;
            default:
                ActivateConfirm(slot);
                break;
        }
    }

    private void ActivateMain(int slot)
    {
        switch (slot)
        {
            case TitleSlot.NewGame:
                OpenNew();
                break;
            case TitleSlot.LoadGame:
                if (HasWorlds)
                {
                    OpenLoad();
                }
                else
                {
                    Notice = new TitleNotice("titleNoWorlds");
                }

                break;
            case TitleSlot.Settings:
                OpenSettings();
                break;
            case TitleSlot.Quit:
                Choice = new TitleChoice(TitleChoiceKind.Quit);
                break;
            default:
                break;
        }
    }

    private void ActivateNew(int slot, bool byKey)
    {
        switch (slot)
        {
            case TitleSlot.NameField:
                Focus(byKey ? TitleSlot.SeedField : TitleSlot.NameField); // Enter in the name goes on to the seed; a click puts the caret there
                break;
            case TitleSlot.SeedField:
                if (byKey)
                {
                    Create();
                }
                else
                {
                    Focus(TitleSlot.SeedField);
                }

                break;
            case TitleSlot.RandomSeed:
                Focus(TitleSlot.RandomSeed);
                Seed.Set(_randomSeed().ToString(CultureInfo.InvariantCulture));
                break;
            case TitleSlot.WorldType:
                Focus(TitleSlot.WorldType);
                CycleType();
                break;
            case TitleSlot.Create:
                Create();
                break;
            case TitleSlot.NewBack:
                Back();
                break;
            default:
                break;
        }
    }

    private void ActivateLoad(int slot)
    {
        if (slot >= 0)
        {
            if (slot < _worlds.Count)
            {
                Selected = slot; // a click on a world chooses it
            }

            return;
        }

        if (_worlds.Count == 0)
        {
            return;
        }

        if (slot == TitleSlot.LoadAction)
        {
            var entry = _worlds[Selected];
            if (!entry.Loadable)
            {
                Notice = new TitleNotice("titleCannotLoad", entry.Name, entry.Problem ?? string.Empty); // a world whose save fails to load says why and is not loaded
                return;
            }

            Choice = new TitleChoice(TitleChoiceKind.LoadWorld, entry);
        }
        else if (slot == TitleSlot.DeleteAction)
        {
            AskDelete();
        }
    }

    private void ActivateConfirm(int slot)
    {
        if (slot == TitleSlot.ConfirmKeep)
        {
            Back();
            return;
        }

        if (slot != TitleSlot.ConfirmDelete || _pendingDelete is not { } entry)
        {
            return;
        }

        TitleNotice notice;
        try
        {
            _store.Delete(entry);
            notice = new TitleNotice("titleDeleted", entry.Name);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            notice = new TitleNotice("titleDeleteFailed", entry.Name, ex.Message);
        }

        var index = Math.Max(0, _worlds.ToList().IndexOf(entry));
        Refresh();
        if (_worlds.Count == 0)
        {
            GoTo(TitlePage.Main, TitleSlot.NewGame);
        }
        else
        {
            GoTo(TitlePage.Load, Math.Min(index, _worlds.Count - 1));
            KeepVisible();
        }

        Notice = notice;
    }

    private void AskDelete()
    {
        if (_worlds.Count == 0)
        {
            return;
        }

        _pendingDelete = _worlds[Math.Clamp(Selected, 0, _worlds.Count - 1)];
        var entry = _pendingDelete;
        GoTo(TitlePage.ConfirmDelete, TitleSlot.ConfirmKeep); // the safer button has the cursor
        _pendingDelete = entry;
    }

    private void Create()
    {
        var issue = NameIssue;
        if (issue != NameProblem.None)
        {
            Focus(TitleSlot.NameField);
            Notice = new TitleNotice(TitleText.KeyOf(issue), WorldNames.Clean(Name.Text));
            return;
        }

        var name = WorldNames.Clean(Name.Text);
        var seed = SeedText.Parse(Seed.Text, _randomSeed);
        try
        {
            // Q03: the terrain options chosen on this page go in here as the world's terrain values.
            var entry = _store.Create(name, seed.Seed, WorldType);
            Choice = new TitleChoice(TitleChoiceKind.NewWorld, entry);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Notice = new TitleNotice("titleCreateFailed", name, ex.Message);
        }
    }

    private void CycleType() => WorldType = WorldType == WorldKind.Streamed ? WorldKind.Valley : WorldKind.Streamed;

    private void Focus(int slot)
    {
        if (Selected == TitleSlot.NameField && slot != TitleSlot.NameField)
        {
            _freshName = false; // leaving the name keeps what it says
        }

        Selected = slot;
    }

    private int MaxLoadScroll() => Math.Max(0, _worlds.Count - Math.Max(1, _metrics.LoadCapacity));

    private void KeepVisible()
    {
        var capacity = Math.Max(1, _metrics.LoadCapacity);
        if (Selected < LoadScroll)
        {
            LoadScroll = Selected;
        }
        else if (Selected >= LoadScroll + capacity)
        {
            LoadScroll = Selected - capacity + 1;
        }

        LoadScroll = Math.Clamp(LoadScroll, 0, MaxLoadScroll());
    }
}
