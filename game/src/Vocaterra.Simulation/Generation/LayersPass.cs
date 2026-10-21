using Vocaterra.Simulation.Tiles;

namespace Vocaterra.Simulation.Generation;

/// <summary>
/// Pass 2: dirt from the surface down to a noisy depth of 12 to 24 tiles, stone below; patches of stone inside the dirt and of dirt inside
/// the stone from thresholded two-dimensional noise (never in the top four tiles, so the surface stays soil).
/// </summary>
internal sealed class LayersPass : IGenerationPass
{
    private const int MinDirtDepth = 12;
    private const int DirtDepthRange = 12;
    private const int DirtDepthWavelength = 40;
    private const int PatchWavelength = 10;
    private const int ProtectedDepth = 4;
    private const int DirtPatchBand = 60;
    private const int StoneInDirtThreshold = 52000;
    private const int DirtInStoneThreshold = 12000;

    public string Name => "layers";

    public void Run(GenerationContext context)
    {
        var width = context.Size.Width;
        var height = context.Size.Height;
        var depthSeed = context.NoiseSeed("layers.depth");
        var patchSeed = context.NoiseSeed("layers.patches");
        var surface = context.Record.SurfaceY;

        for (var x = 0; x < width; x++)
        {
            var top = surface[x];
            var dirtDepth = MinDirtDepth + (ValueNoise.Sample1D(depthSeed, x, DirtDepthWavelength) * DirtDepthRange / (ValueNoise.Max + 1));
            for (var y = top; y < height; y++)
            {
                var depth = y - top;
                var dirt = depth < dirtDepth;
                if (depth >= ProtectedDepth)
                {
                    var patch = ValueNoise.Sample2D(patchSeed, x, y, PatchWavelength);
                    if (dirt && patch > StoneInDirtThreshold)
                    {
                        dirt = false;
                    }
                    else if (!dirt && depth < dirtDepth + DirtPatchBand && patch < DirtInStoneThreshold)
                    {
                        dirt = true;
                    }
                }

                context.Tiles.SetTile(x, y, dirt ? TileId.Dirt : TileId.Stone);
            }
        }
    }
}
