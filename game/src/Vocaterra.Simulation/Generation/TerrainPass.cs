using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Generation;

/// <summary>
/// Pass 1: the surface height per column from four octaves of integer value noise, flattened around the middle of the world where the
/// player spawns. Everything from the surface down is filled with stone; the layers pass turns the top into dirt.
/// </summary>
internal sealed class TerrainPass : IGenerationPass
{
    private const int HillWavelength = 96;
    private const int Octaves = 4;
    private const int FlatHalfWidth = 16;
    private const int BlendWidth = 32;

    public string Name => "terrain";

    public void Run(GenerationContext context)
    {
        var width = context.Size.Width;
        var height = context.Size.Height;
        var baseY = WorldGenerator.SurfaceBaseY(height);
        var amplitude = WorldGenerator.SurfaceAmplitude(height);
        var noiseSeed = context.NoiseSeed("terrain");
        var surface = context.Record.SurfaceY;

        var natural = new int[width];
        for (var x = 0; x < width; x++)
        {
            var n = ValueNoise.Fractal1D(noiseSeed, x, HillWavelength, Octaves);
            natural[x] = baseY + (int)(((long)(n - 32768) * amplitude) / 32768);
        }

        var centre = width / 2;
        var flatY = natural[centre];
        for (var x = 0; x < width; x++)
        {
            var distance = Math.Abs(x - centre);
            surface[x] = distance <= FlatHalfWidth
                ? flatY
                : distance >= FlatHalfWidth + BlendWidth
                    ? natural[x]
                    : flatY + (((natural[x] - flatY) * (distance - FlatHalfWidth)) / BlendWidth);
        }

        for (var x = 0; x < width; x++)
        {
            for (var y = surface[x]; y < height; y++)
            {
                context.Tiles.SetTile(x, y, TileId.Stone);
            }
        }
    }
}
