using Vocaterra.Simulation;
using Vocaterra.Simulation.Tiles;

namespace Vocaterra.MonoGame.Frontend;

/// <summary>
/// The mouse and the number keys as world commands (P6), in plain values. Once per frame the game sets what the devices show: the tile under
/// the mouse, whether the left button is held, whether the right button was pressed, and the hotbar slot a number key or the wheel picked.
/// Then each simulation tick takes the commands it needs: a held mine follows the tile under the mouse on every tick, while a placement and a
/// slot change are taken once, by the first tick that runs after they were set.
/// </summary>
public sealed class LiveWorldInput
{
    private TilePoint? _mine;
    private TilePoint? _place;
    private int? _slot;

    /// <summary>Sets the frame's device state.</summary>
    /// <param name="hoveredTile">The tile under the mouse, or <see langword="null"/> when the mouse is outside the world view.</param>
    /// <param name="mineHeld">Whether the left button is held (mining follows the tile under the mouse).</param>
    /// <param name="placePressed">Whether the right button was pressed this frame (places on the tile under the mouse).</param>
    /// <param name="selectSlot">The hotbar slot (0 to 9) a number key or the wheel picked this frame, or <see langword="null"/>.</param>
    public void SetFrame(TilePoint? hoveredTile, bool mineHeld, bool placePressed, int? selectSlot)
    {
        _mine = mineHeld ? hoveredTile : null;
        if (placePressed && hoveredTile is { } placeTile)
        {
            _place = placeTile;
        }

        if (selectSlot is { } slot)
        {
            _slot = slot;
        }
    }

    /// <summary>Adds this tick's world commands to <paramref name="commands"/> (after the movement commands).</summary>
    public void Fill(List<PlayerCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        if (_slot is { } slot)
        {
            commands.Add(PlayerCommand.SelectSlot(slot));
            _slot = null;
        }

        if (_mine is { } mine)
        {
            commands.Add(PlayerCommand.Mine(mine.X, mine.Y));
        }

        if (_place is { } place)
        {
            commands.Add(PlayerCommand.Place(place.X, place.Y));
            _place = null;
        }
    }
}
