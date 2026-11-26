#version 330 core
// The chunk passes' vertex stage (B17a, B17b, B17c): the position, the light of the corner and the fog, written out once for the opaque faces, the lit pieces, the cut-outs, the grass layer, the water and the far view's tiles.
// A vertex is MeshVertex (24 bytes: a position in blocks, a packed RGBA colour, an atlas coordinate) and, for the meshes of the chunks, a second stream of 4 bytes, the corner's light word (LightData: the sky light, the block light, the
// corner occlusion, the face and an extra byte). The colour is the material (the rock's tint, the biome's green, the water's transparency) and the light is data: this stage combines it with the frame's sun (uSunDirection, uSunColour)
// and the glow's strength, so the hour, the weather and the darkening change with no new mesh. The far view's tiles and the self-lit pieces have no light word: their colour is baked, times uDiffuse (uLightMode 0).
// The matrices are MonoGame's (row-vector convention, uploaded transposed, so that `uWorldViewProjection[c]` is the c-th column of MonoGame's matrix and `dot(vector, column)` is MonoGame's `vector * matrix`), and its projections give a
// clip depth of 0 to 1 where OpenGL clips -1 to 1, which the depth line below undoes (the same line MonoGame's own translated shaders carry), so every pass here and every pass of MonoGame's agree on every depth in the buffer.
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec4 aColour;
layout(location = 2) in vec2 aUv;
layout(location = 3) in uvec4 aLight; // sky, block, occlusion | face << 2 | flags << 5, extra

uniform mat4 uWorldViewProjection;
uniform vec4 uFogVector;   // the view-space depth as a fog factor: dot(position, uFogVector) is 0 at the fog's start and 1 at its end (MonoGame's EffectHelpers.SetFogVector)
uniform vec3 uEye;         // the camera's position in blocks (the height fog, the water's edge)
uniform vec3 uDiffuse;     // the light of a baked mesh: the far view's sun light, white for the lit pieces
uniform float uFlipY;      // 1 for the back buffer, -1 for a render target (MonoGame's posFixup: its pictures are upside down there, and the pass turns the front face to match)
uniform int uLightMode;    // 0 baked (the colour times uDiffuse), 1 light as data (the corner's light word)
uniform int uWater;        // 1 in the water pass: the extra byte is the water's depth, the wave flag moves a vertex, a flat face has no grey level
uniform vec3 uSunDirection; // the unit vector towards the sun the faces are shaded by
uniform vec3 uSunColour;    // the sun's light on the world, a multiplier
uniform vec3 uShadeTint;    // the colour of a face turned away from the sun, of one in full sun (uSunTint) and of the underside: ChunkMesher's, so the baked fallback and this stage agree
uniform vec3 uSunTint;
uniform vec3 uBottomTint;
uniform float uSunHeight;   // the sun height the shares are measured against (ChunkMesher.SunDirection.y)
uniform float uAoKeep[4];   // how much of its colour a corner keeps by its occlusion level
uniform vec3 uGlowColour;   // the block light's colour at the moment: amber times the glow's strength
uniform vec4 uHeightFog;    // x: the density at the base height, per block (0 off); y: the base height; z: one over the scale height; w: unused
uniform int uWaterShader;   // 1: the depth tint, the surface's movement and the edge at grazing angles
uniform float uTime;        // seconds of game time, for the surface's movement
uniform float uWaveAmount;  // the surface's movement in blocks

out vec4 vColour;
// The atlas coordinate twice. Under multisampling a pixel the face only partly covers is still shaded once, and without `centroid` at the pixel's centre even when that centre lies outside the face: the
// coordinate is then extrapolated past the face's edge, out of the cell's 0.02-texel inset and into the neighbouring cell of the atlas, which drew dotted lines of a neighbour's colour along the blocks' edges. `centroid` keeps the
// point inside the covered part of the pixel, so vUv stays in the cell; vUvGrad is the same coordinate without it, whose derivatives are smooth across the face, for the choice of the mip level.
centroid out vec2 vUv;
out vec2 vUvGrad;
out float vFog;
out vec3 vWorld;

const vec3 kNormals[6] = vec3[6](vec3(1.0, 0.0, 0.0), vec3(-1.0, 0.0, 0.0), vec3(0.0, 1.0, 0.0), vec3(0.0, -1.0, 0.0), vec3(0.0, 0.0, 1.0), vec3(0.0, 0.0, -1.0));
const vec3 kWaterDeep = vec3(0.56, 0.70, 0.90); // WaterDepthTint.Deep

// The water's colour multiplier by depth (WaterDepthTint.At): white at the shore (under half a block), the deep colour from six blocks, a smoothstep between.
vec3 waterTint(float depth)
{
    float t = clamp((depth - 0.5) / 5.5, 0.0, 1.0);
    return mix(vec3(1.0), kWaterDeep, t * t * (3.0 - 2.0 * t));
}

void main()
{
    vec3 p = aPosition;
    vec3 factor = uDiffuse;
    vec3 glow = vec3(0.0);
    if (uLightMode == 1)
    {
        float sky = float(aLight.x) / 255.0;
        float blockLight = float(aLight.y) / 255.0;
        uint occlusion = aLight.z & 3u;
        uint face = (aLight.z >> 2) & 7u;
        uint flags = aLight.z >> 5;
        float extra = float(aLight.w) / 255.0;
        if (face == 7u)
        {
            factor = vec3(1.0); // a fixed corner: the colour is the finished one
        }
        else
        {
            vec3 shade;
            if (face < 6u)
            {
                float share = clamp(dot(kNormals[face], uSunDirection) / uSunHeight, 0.0, 1.0);
                shade = face == 3u ? uBottomTint : mix(uShadeTint, uSunTint, share);
            }
            else
            {
                shade = vec3(uWater == 1 ? 1.0 : extra);
            }

            factor = shade * (uAoKeep[occlusion] * sky) * uSunColour;
            glow = uGlowColour * blockLight;
            if (uWater == 1 && uWaterShader == 1)
            {
                factor *= waterTint(extra * 8.0);
                if ((flags & 1u) != 0u)
                {
                    p.y += uWaveAmount * (sin((p.x * 1.9) + (uTime * 1.7)) + sin((p.z * 2.3) - (uTime * 1.3)) + sin(((p.x + p.z) * 1.1) + (uTime * 0.9))) * 0.3333;
                }
            }
        }
    }

    vec4 position = vec4(p, 1.0);
    // Four dot products with the matrix's columns, as MonoGame's own translated shaders compute the position (and the matrix is uploaded transposed for it): the passes that test depth for "less or equal" against each other need the very same depth values, to the last bit.
    gl_Position = vec4(dot(position, uWorldViewProjection[0]), dot(position, uWorldViewProjection[1]), dot(position, uWorldViewProjection[2]), dot(position, uWorldViewProjection[3]));
    gl_Position.z = gl_Position.z * 2.0 - gl_Position.w;
    gl_Position.y = gl_Position.y * uFlipY;
    vColour = vec4((aColour.rgb * factor) + glow, aColour.a);
    vUv = aUv;
    vUvGrad = aUv;
    vWorld = p;
    float fog = clamp(dot(position, uFogVector), 0.0, 1.0);
    if (uHeightFog.x > 0.0)
    {
        // The haze that lies in the low land: a density that falls off exponentially with height, integrated along the ray from the eye to the vertex (the closed form), so a valley fills with haze in the distance and a ridge stands above it.
        vec3 ray = p - uEye;
        float k = uHeightFog.z;
        float eyeTerm = clamp(-k * (uEye.y - uHeightFog.y), -20.0, 6.0);
        float slope = k * ray.y;
        float along = abs(slope) < 0.0001 ? 1.0 : (1.0 - exp(clamp(-slope, -20.0, 20.0))) / slope;
        float depth = uHeightFog.x * length(ray) * exp(eyeTerm) * along;
        fog = 1.0 - ((1.0 - fog) * exp(-max(depth, 0.0)));
    }

    vFog = fog;
}
