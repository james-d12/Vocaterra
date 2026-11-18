#version 330 core
// The chunk passes' fragment stage (B17a, B17b, B17c): the texel times the vertex colour, the alpha test (AlphaTestEffect with CompareFunction.Greater and a reference of 110 out of 255: a texel passes when its alpha is above it, with half a
// step of slack as MonoGame has; a reference below 0 is no test, as for the water, which BasicEffect drew), the far view's light (the baked day's face shade brought to the sun of the moment by the face's own direction), the water's edge at
// grazing angles, and the fog towards the fog colour (premultiplied by alpha, as both effects do).
in vec4 vColour;
in vec2 vUv;
in float vFog;
in vec3 vWorld;

uniform sampler2D uAtlas; // the block atlas, one frame of the water's art, or the far view's noise
uniform vec3 uFogColour;
uniform float uAlphaReference; // 0 to 1, or -1 for no test
uniform vec3 uEye;
uniform int uWater;
uniform int uWaterShader;
uniform int uRelight;          // 1 for the far view's land: its colour was baked in the Day's sun, and is brought to uSunDirection by the face's direction
uniform vec3 uSunDirection;
uniform vec3 uDayDirection;
uniform vec3 uShadeTint;
uniform vec3 uSunTint;
uniform vec3 uBottomTint;
uniform float uSunHeight;
uniform vec4 uPlayerLight;       // the light round the player (I05b): xyz the position in blocks, w the reach in blocks; 0 for no light (nothing worn, or it is day)
uniform vec3 uPlayerLightColour; // the light's colour times its strength

out vec4 oColour;

vec3 shadeOf(vec3 n, vec3 sun)
{
    return n.y < -0.5 ? uBottomTint : mix(uShadeTint, uSunTint, clamp(dot(n, sun) / uSunHeight, 0.0, 1.0));
}

// The facet's normal from the screen-space derivatives of its position, turned to face the eye.
vec3 facetNormal()
{
    vec3 n = normalize(cross(dFdx(vWorld), dFdy(vWorld)));
    return faceforward(n, vWorld - uEye, n);
}

void main()
{
    vec4 texel = texture(uAtlas, vUv);
    vec4 colour = texel * vColour;
    if (colour.a <= uAlphaReference + 0.5 / 255.0)
    {
        discard;
    }

    if (uRelight == 1)
    {
        vec3 n = facetNormal();
        colour.rgb *= shadeOf(n, uSunDirection) / shadeOf(n, uDayDirection);
    }

    if (uPlayerLight.w > 0.0)
    {
        // The light round the player (I05b): added to the face's own picture by the distance (a falling square, nothing at the reach) and by how squarely the face turns to it, so a face turned away, such as the far side of a wall, gets none.
        // The derivatives are taken under a uniform condition (the reach is a uniform), the distance test after them.
        vec3 n = facetNormal();
        vec3 toLight = uPlayerLight.xyz - vWorld;
        float distanceToLight = length(toLight);
        if (distanceToLight < uPlayerLight.w)
        {
            float fall = 1.0 - (distanceToLight / uPlayerLight.w);
            float facing = max(dot(n, toLight / max(distanceToLight, 0.0001)), 0.0);
            colour.rgb += texel.rgb * uPlayerLightColour * (fall * fall * facing) * colour.a;
        }
    }

    if (uWater == 1 && uWaterShader == 1)
    {
        // The edge: the surface seen at a grazing angle takes the haze's colour (the sky's at the horizon) and turns opaque, so a lake brightens towards the horizon and shows a clear water-side nearby.
        vec3 n = facetNormal();
        float edge = pow(1.0 - clamp(dot(n, normalize(uEye - vWorld)), 0.0, 1.0), 4.0);
        colour.rgb = mix(colour.rgb, uFogColour * colour.a, edge * 0.45);
        colour.a = mix(colour.a, 1.0, edge * 0.3);
    }

    colour.rgb = mix(colour.rgb, uFogColour * colour.a, vFog);
    oColour = colour;
}
