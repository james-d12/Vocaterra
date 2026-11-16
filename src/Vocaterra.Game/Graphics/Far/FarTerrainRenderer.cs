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
/// Draws the far view's tiles (B07, B07c; D-game-34 items 21 and 25): after the sky and before the voxel chunks, in a pass of its own with its own depth range (a near plane of a few blocks and a far plane at the
/// view's range, so the depth buffer's precision is spent on the distance and the land at 16 km does not fight itself), with the haze that blends it into the sky; then the depth buffer is cleared so the
/// voxel chunks, drawn over it with their own range, always stand in front of the far land that lies under them. Plain <see cref="BasicEffect"/> with vertex colours and the noise texture (<see cref="FarNoise"/>): the tiles are baked in the
/// day's light and tinted by the light of the moment. Three passes over the tiles in view: the opaque land, the blocks that give light (in no tint, so lava glows at night) and the water, translucent and drawn from the far to the near.
/// A tile is uploaded the first frame it is drawn (a few a frame).
/// </summary>
public sealed class FarTerrainRenderer : IDisposable
{
    /// <summary>The far pass's least near plane in blocks.</summary>
    public const float NearPlane = 12f;

    /// <summary>The far pass's near plane for a voxel view distance: a seventh of it, at most 64 blocks (the land closer than this is the voxel chunks' for sure, and the depth precision at the horizon is five times better than at 12).</summary>
    /// <param name="voxelViewDistance">The voxel zone's radius in blocks.</param>
    public static float NearPlaneFor(int voxelViewDistance) => Math.Clamp(voxelViewDistance / 7f, NearPlane, 64f);

    // The most bytes of meshes one frame hands to the card: the coarse levels first, so a settled view reaches its horizon in a few frames, and a walk's few new tiles never make a hitch.
    private const long MaxUploadBytesPerFrame = 8L * 1024 * 1024;

    private readonly GraphicsDevice _device;
    private readonly FarView _view;
    private readonly BasicEffect _effect;
    private readonly AlphaTestEffect _solid;
    private readonly Texture2D _noise;
    private readonly Texture2D _atlas;
    private readonly SamplerState _atlasSampler;
    private readonly List<FarTile> _visible = [];
    private readonly List<FarTile> _uploads = [];
    private readonly BoundingFrustum _frustum = new(Matrix.Identity);
    private readonly IEffectMatrices[] _matrices;
    private int _drawn;

    /// <summary>Creates the renderer of a far view.</summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="view">The tiles to draw.</param>
    /// <param name="atlas">The block atlas's texture, which the textured tiles are drawn with.</param>
    public FarTerrainRenderer(GraphicsDevice device, FarView view, Texture2D atlas)
    {
        _device = device;
        _view = view;
        _atlas = atlas;
        // As the chunks sample the atlas (crisp texels up close, blended between mips), but never past the mip level at which one texel is a whole cell (deeper levels average neighbouring cells, and the cut-out alpha of the average fails the alpha test).
        _atlasSampler = new SamplerState
        {
            Filter = TextureFilter.PointMipLinear,
            AddressU = TextureAddressMode.Clamp,
            AddressV = TextureAddressMode.Clamp,
            MaxMipLevel = (int)Math.Log2(Blocks.BlockAtlasLayout.CellSize),
        };
        _noise = FarNoise.Create(device);
        _effect = new BasicEffect(device)
        {
            VertexColorEnabled = true,
            TextureEnabled = true,
            Texture = _noise,
            LightingEnabled = false,
            FogEnabled = true,
        };

        // The opaque land and the lit blocks are cut out like the chunks' faces (leaves have holes in their art).
        _solid = new AlphaTestEffect(device)
        {
            VertexColorEnabled = true,
            FogEnabled = true,
            AlphaFunction = CompareFunction.Greater,
            ReferenceAlpha = 110,
        };
        _matrices = [_effect, _solid];
    }

    /// <summary>The tiles the last <see cref="Draw"/> drew.</summary>
    public int DrawnTiles => _drawn;

    /// <summary>The perspective projection the far pass uses: the camera's field of view and aspect, its own near and far plane.</summary>
    /// <param name="camera">The camera.</param>
    /// <param name="range">The far view's range in blocks.</param>
    /// <param name="near">The near plane.</param>
    public static Matrix ProjectionFor(FirstPersonCamera camera, float range, float near = NearPlane)
    {
        var aspect = camera.Projection.M22 / camera.Projection.M11;
        return Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(camera.FieldOfView), aspect, near, (range * 1.05f) + 64f);
    }

    /// <summary>What the far range is multiplied by for the choice of tiles: the fog is planar and the choice radial, so at the side of the picture land needs a reach of range over the cosine of the angle to end in full fog.</summary>
    /// <param name="camera">The camera.</param>
    public static double ReachFor(FirstPersonCamera camera)
    {
        var halfWidth = camera.Projection.M11 > 0 ? 1.0 / camera.Projection.M11 : 1.0; // tan of the half horizontal field of view
        return Math.Sqrt(1 + (halfWidth * halfWidth));
    }

    /// <summary>Draws the tiles in view from the camera, then clears the depth buffer for the voxel pass.</summary>
    /// <param name="camera">The camera of the frame.</param>
    /// <param name="state">The light and the haze.</param>
    public void Draw(FirstPersonCamera camera, in FarDrawState state)
    {
        _drawn = 0;
        _visible.Clear();
        var settings = _view.Settings;
        if (!settings.Enabled || _view.Draw.Count == 0)
        {
            return;
        }

        var projection = ProjectionFor(camera, settings.Range, NearPlaneFor(_view.VoxelViewDistance));
        _view.ReachScale = ReachFor(camera);
        _frustum.Matrix = camera.View * projection;
        foreach (var effect in _matrices)
        {
            effect.World = Matrix.Identity;
            effect.View = camera.View;
            effect.Projection = projection;
        }

        _solid.DiffuseColor = state.Tint;
        _solid.FogColor = state.FogColour;
        _solid.FogStart = state.FogStart;
        _solid.FogEnd = state.FogEnd;
        _effect.DiffuseColor = state.Tint;
        _effect.FogColor = state.FogColour;
        _effect.FogStart = state.FogStart;
        _effect.FogEnd = state.FogEnd;
        _device.DepthStencilState = DepthStencilState.Default;
        _device.BlendState = BlendState.Opaque;
        UploadPending(settings.TopLevel);
        _device.RasterizerState = RasterizerState.CullClockwise;
        for (var i = 0; i < _view.Draw.Count; i++)
        {
            var tile = _view.Draw[i];
            if (tile.Buffer is null)
            {
                continue;
            }

            if (!_frustum.Intersects(tile.Box))
            {
                continue;
            }

            _visible.Add(tile);
            if (tile.Runs.Opaque == 0)
            {
                continue;
            }

            // Levels never overlap (a stand-in covers its leaves alone, until they are all on the card), so no depth bias is needed or used.
            Bind(tile, tile.Textured, solid: true);
            _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, tile.Runs.Opaque / 3);
            _drawn++;
        }

        _device.RasterizerState = RasterizerState.CullClockwise;

        // The blocks that give light, in no tint: they glow at night.
        _solid.DiffuseColor = Vector3.One;
        foreach (var tile in _visible)
        {
            if (tile.Runs.Lit > 0)
            {
                Bind(tile, tile.Textured, solid: true);
                _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, tile.Runs.Opaque, tile.Runs.Lit / 3);
            }
        }

        // The water, translucent, from the far to the near, reading the depth and not writing it.
        _device.BlendState = BlendState.NonPremultiplied;
        _device.DepthStencilState = DepthStencilState.DepthRead;
        for (var i = _visible.Count - 1; i >= 0; i--)
        {
            var tile = _visible[i];
            if (tile.Runs.Water > 0)
            {
                Bind(tile, false, solid: false);
                _device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, tile.Runs.Opaque + tile.Runs.Lit, tile.Runs.Water / 3);
            }
        }

        _device.BlendState = BlendState.Opaque;
        _device.DepthStencilState = DepthStencilState.Default;
        _device.Clear(ClearOptions.DepthBuffer, Color.Transparent, 1f, 0);
    }

    // Uploads what the workers made, within the frame's bytes (see FarUploads.Choose).
    private void UploadPending(int topLevel)
    {
        FarUploads.Choose(_view.PendingTiles, topLevel, MaxUploadBytesPerFrame, _uploads);
        foreach (var tile in _uploads)
        {
            Upload(tile);
        }
    }

    private void Bind(FarTile tile, bool textured, bool solid)
    {
        var texture = textured ? _atlas : _noise;
        _device.SamplerStates[0] = textured ? _atlasSampler : FarNoise.Sampler;
        if (solid)
        {
            _solid.Texture = texture;
            _solid.CurrentTechnique.Passes[0].Apply();
        }
        else
        {
            _effect.Texture = texture;
            _effect.CurrentTechnique.Passes[0].Apply();
        }

        _device.SetVertexBuffer(tile.Buffer);
        _device.Indices = tile.Indices;
    }

    /// <summary>Frees a tile's buffers.</summary>
    /// <param name="tile">The tile.</param>
    public static void Release(FarTile tile)
    {
        tile.Buffer?.Dispose();
        tile.Buffer = null;
        tile.Indices?.Dispose();
        tile.Indices = null;
        tile.Pending = null;
        tile.OnCard = false;
        tile.OnCard = false;
    }

    private void Upload(FarTile tile)
    {
        var mesh = tile.Pending!;
        tile.Buffer?.Dispose(); // a tile made again replaces its old buffers
        tile.Indices?.Dispose();
        tile.Adopt(mesh);
        var buffer = new VertexBuffer(_device, VertexPositionColorTexture.VertexDeclaration, mesh.Vertices.Length, BufferUsage.WriteOnly);
        buffer.SetData(mesh.Vertices);
        tile.Buffer = buffer;
        if (mesh.Indices16 is { } narrow)
        {
            var indices = new IndexBuffer(_device, IndexElementSize.SixteenBits, narrow.Length, BufferUsage.WriteOnly);
            indices.SetData(narrow);
            tile.Indices = indices;
        }
        else
        {
            var wide = mesh.Indices32!;
            var indices = new IndexBuffer(_device, IndexElementSize.ThirtyTwoBits, wide.Length, BufferUsage.WriteOnly);
            indices.SetData(wide);
            tile.Indices = indices;
        }

        tile.Pending = null;
        tile.OnCard = true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _noise.Dispose();
        _atlasSampler.Dispose();
        _effect.Dispose();
        _solid.Dispose();
    }
}
