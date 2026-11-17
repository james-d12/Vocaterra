#version 330 core
// The chunk passes' fragment stage (B17a, widened in B17b): the texel times the vertex colour, the alpha test (AlphaTestEffect with CompareFunction.Greater and a reference of 110 out of 255: a
// texel passes when its alpha is above it, with half a step of slack as MonoGame has; a reference below 0 is no test, as for the water, which BasicEffect drew), and the linear fog towards the
// fog colour (premultiplied by alpha, as both effects do).
in vec4 vColour;
in vec2 vUv;
in float vFog;

uniform sampler2D uAtlas; // the block atlas, or one frame of the water's art
uniform vec3 uFogColour;
uniform float uAlphaReference; // 0 to 1, or -1 for no test

out vec4 oColour;

void main()
{
    vec4 colour = texture(uAtlas, vUv) * vColour;
    if (colour.a <= uAlphaReference + 0.5 / 255.0)
    {
        discard;
    }

    colour.rgb = mix(colour.rgb, uFogColour * colour.a, vFog);
    oColour = colour;
}
