#version 330 core
// The chunk passes' vertex stage (B17a, widened in B17b): what AlphaTestEffect's and BasicEffect's do for the chunks, written out once for the opaque faces, the lit pieces, the cut-outs, the
// grass layer, the glow and the water. A vertex is MeshVertex (24 bytes): a position in blocks, a packed RGBA colour (the baked light and the ambient occlusion) and an atlas coordinate.
// The matrices are MonoGame's (row-vector convention, uploaded transposed, so that `uWorldViewProjection[c]` is the c-th column of MonoGame's matrix and `dot(vector, column)` is MonoGame's
// `vector * matrix`), and its projections give a clip depth of 0 to 1 where OpenGL clips -1 to 1, which the depth line below undoes (the same line MonoGame's own translated shaders carry), so
// every pass here and every pass of MonoGame's agree on every depth in the buffer. The passes that meet by depth equality (the opaque faces, the lit pieces and the glow) are this one program.
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec4 aColour;
layout(location = 2) in vec2 aUv;

uniform mat4 uWorldViewProjection;
uniform vec4 uFogVector; // the view-space depth as a fog factor: dot(position, uFogVector) is 0 at the fog's start and 1 at its end (MonoGame's EffectHelpers.SetFogVector)
uniform vec3 uDiffuse;   // the light band's tint (AlphaTestEffect.DiffuseColor), or white for the lit pieces, or the glow's colour
uniform float uFlipY;    // 1 for the back buffer, -1 for a render target (MonoGame's posFixup: its pictures are upside down there, and the pass turns the front face to match)

out vec4 vColour;
out vec2 vUv;
out float vFog;

void main()
{
    vec4 position = vec4(aPosition, 1.0);
    // Four dot products with the matrix's columns, as MonoGame's own translated shaders compute the position (and the matrix is uploaded transposed for it): the glow, which tests depth for
    // "less or equal" against the opaque faces, needs the very same depth values, to the last bit.
    gl_Position = vec4(dot(position, uWorldViewProjection[0]), dot(position, uWorldViewProjection[1]), dot(position, uWorldViewProjection[2]), dot(position, uWorldViewProjection[3]));
    gl_Position.z = gl_Position.z * 2.0 - gl_Position.w;
    gl_Position.y = gl_Position.y * uFlipY;
    vColour = vec4(uDiffuse, 1.0) * aColour;
    vUv = aUv;
    vFog = clamp(dot(position, uFogVector), 0.0, 1.0);
}
