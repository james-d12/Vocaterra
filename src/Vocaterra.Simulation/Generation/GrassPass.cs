using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Generation;

/// <summary>
/// Pass 3: every dirt tile with air above it becomes grass. It scans the whole grid, so a later cave pass that runs before it gets grass on
/// cave mouths too; a cave pass that runs after it must decide for itself.
/// </summary>
internal sealed class GrassPass : IGenerationPass
{
    public string Name => "grass";

    public void Run(GenerationContext context)
    {
        var tiles = context.Tiles;
        for (var y = 0; y < tiles.Height; y++)
        {
            for (var x = 0; x < tiles.Width; x++)
            {
                if (tiles.GetTile(x, y) == TileId.Dirt && y > 0 && tiles.GetTile(x, y - 1) == TileId.Air)
                {
                    tiles.SetTile(x, y, TileId.Grass);
                }
            }
        }
    }
}
