#version 330 core
// The opaque chunk pass's fragment stage (B17a): the atlas texel times the vertex colour, the alpha test (AlphaTestEffect with CompareFunction.Greater and a reference of 110
// out of 255: a texel passes when its alpha is above it, with half a step of slack as MonoGame has), and the linear fog towards the fog colour (premultiplied by alpha, as the
// effect does).
in vec4 vColour;
in vec2 vUv;
in float vFog;

uniform sampler2D uAtlas;
uniform vec3 uFogColour;
uniform float uAlphaReference; // 0 to 1

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
