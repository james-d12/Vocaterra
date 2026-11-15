using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vocaterra.Game.Graphics.Camera;

namespace Vocaterra.Game.Graphics.Far;

/// <summary>What the far pass needs of the frame's light and haze: the fog's colour, where it starts and ends, and the tint that brings the day-baked land to the light of the moment.</summary>
/// <param name="FogColour">The haze colour.</param>
/// <param name="FogStart">Where the haze starts, in blocks of view depth.</param>
/// <param name="FogEnd">Where the land is wholly haze.</param>
/// <param name="Tint">The light of the moment as a colour multiplier (the day band's tint of <c>LightBands</c>).</param>
public readonly record struct FarDrawState(Vector3 FogColour, float FogStart, float FogEnd, Vector3 Tint);

/// <summary>
/// Draws the far view's tiles (B07, D-game-34 item 21): after the sky and before the voxel chunks, in a pass of its own with its own depth range (a near plane of a few blocks and a far plane at the
/// view's range, so the depth buffer's precision is spent on the distance and the land at 16 km does not fight itself), with the haze that blends it into the sky; then the depth buffer is cleared so the
/// voxel chunks, drawn over it with their own range, always stand in front of the far land that lies under them. Plain <see cref="BasicEffect"/> with vertex colours, no texture: the tiles are baked in the
/// day's light and tinted by the light of the moment. A tile is uploaded the first frame it is drawn (a few a frame).
/// </summary>
public sealed class FarTerrainRenderer : IDisposable
{
    /// <summary>The far pass's near plane in blocks: the land closer than this is the voxel chunks' (or a hole in them, which a tile this near would show for a few frames).</summary>
    public const float NearPlane = 12f;

    private const int MaxUploadsPerFrame = 8;

    private static readonly RasterizerState Fallback = new() { CullMode = CullMode.CullClockwiseFace, DepthBias = 2f, SlopeScaleDepthBias = 2f };

    private readonly GraphicsDevice _device;
    private readonly FarView _view;
    private readonly BasicEffect _effect;
    private IndexBuffer? _indices;
    private int _indexCells;
    private int _drawn;

    /// <summary>Creates the renderer of a far view.</summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="view">The tiles to draw.</param>
    public FarTerrainRenderer(GraphicsDevice device, FarView view)
    {
        _device = device;
        _view = view;
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            TextureEnabled = false,
            LightingEnabled = false,
            FogEnabled = true,
        };
    }

    /// <summary>The tiles the last <see cref="Draw"/> drew.</summary>
    public int DrawnTiles => _drawn;

    /// <summary>The perspective projection the far pass uses: the camera's field of view and aspect, its own near and far plane.</summary>
    /// <param name="camera">The camera.</param>
    /// <param name="range">The far view's range in blocks.</param>
    public static Matrix ProjectionFor(FirstPersonCamera camera, float range)
    {
        var aspect = camera.Projection.M22 / camera.Projection.M11;
        return Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(camera.FieldOfView), aspect, NearPlane, (range * 1.05f) + 64f);
    }

    /// <summary>Draws the tiles in view from the camera, then clears the depth buffer for the voxel pass.</summary>
    /// <param name="camera">The camera of the frame.</param>
    /// <param name="state">The light and the haze.</param>
    public void Draw(FirstPersonCamera camera, in FarDrawState state)
    {
        _drawn = 0;
        var settings = _view.Settings;
        if (!settings.Enabled || _view.Draw.Count == 0)
        {
            return;
        }

        EnsureIndices(settings.TileCells);
        var projection = ProjectionFor(camera, settings.Range);
        var frustum = new BoundingFrustum(camera.View * projection);
        _effect.World = Matrix.Identity;
        _effect.View = camera.View;
        _effect.Projection = projection;
        _effect.DiffuseColor = state.Tint;
        _effect.FogColor = state.FogColour;
        _effect.FogStart = state.FogStart;
        _effect.FogEnd = state.FogEnd;
        _device.DepthStencilState = DepthStencilState.Default;
        _device.BlendState = BlendState.Opaque;
        _device.Indices = _indices;
        var triangles = FarTileTopology.TriangleCount(settings.TileCells);
        var uploads = 0;
        var standIns = _view.FallbackCount;
        for (var i = 0; i < _view.Draw.Count; i++)
        {
            var tile = _view.Draw[i];
            if (tile.Buffer is null)
            {
                if (tile.Pending is null || uploads >= MaxUploadsPerFrame)
                {
                    continue;
                }

                Upload(tile);
                uploads++;
            }

            if (!frustum.Intersects(tile.Box))
            {
                continue;
            }

            // The fallbacks (an ancestor standing in for a leaf that is not built) come first in the list and are drawn a little behind, so a finer tile beside or over one wins.
            _device.RasterizerState = i < standIns ? Fallback : RasterizerState.CullClockwise;
            _effect.CurrentTechnique.Passes[0].Apply();
            _device.SetVertexBuffer(tile.Buffer);
            _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, triangles);
            _drawn++;
        }

        _device.RasterizerState = RasterizerState.CullClockwise;
        _device.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1f, 0);
    }

    /// <summary>Frees a tile's buffer.</summary>
    /// <param name="tile">The tile.</param>
    public static void Release(FarTile tile)
    {
        tile.Buffer?.Dispose();
        tile.Buffer = null;
        tile.Pending = null;
    }

    private void Upload(FarTile tile)
    {
        var mesh = tile.Pending!;
        var buffer = new VertexBuffer(_device, VertexPositionColor.VertexDeclaration, mesh.Vertices.Length, BufferUsage.WriteOnly);
        buffer.SetData(mesh.Vertices);
        tile.Buffer = buffer;
        tile.Pending = null;
    }

    private void EnsureIndices(int cells)
    {
        if (_indices is not null && _indexCells == cells)
        {
            return;
        }

        _indices?.Dispose();
        var indices = FarTileTopology.Indices(cells);
        _indices = new IndexBuffer(_device, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
        _indices.SetData(indices);
        _indexCells = cells;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _indices?.Dispose();
        _effect.Dispose();
    }
}
