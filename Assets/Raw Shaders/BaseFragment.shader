#version 430 core

in vec2 vUV;
in vec3 vNormal;
in vec3 vFragPos;

out vec4 FragColor;

uniform vec4 baseColor;
uniform vec4 emissionColor;
uniform float metallic;
uniform float roughness;
uniform int zWrite;
uniform float timeSinceStart;
uniform int objectLayer;

uniform int textureCount;
uniform mat4 model;

uniform sampler2D material_albedo;
uniform vec2 material_albedoScale;
uniform vec2 material_albedoOffset;

uniform sampler2D material_normal;
uniform vec2 material_normalScale;
uniform vec2 material_normalOffset;

uniform sampler2D material_roughness;
uniform vec2 material_roughnessScale;
uniform vec2 material_roughnessOffset;

uniform sampler2DArray shadowMap2D;
uniform samplerCubeArray shadowMapCube;

// Must match GPULight's layout in C# (and LightCulling.shader's copy of this struct) field-for-field.
struct Light
{
    vec4 position;   // xyz = world-space position, w = range
    vec4 color;      // rgb = color (intensity-premultiplied), w = intensity
    vec4 direction;  // xyz = direction, w = type (0=Directional,1=Point,2=Spot,3=Area)
    vec4 params;     // x = innerConeCos, y = outerConeCos, z = shadowStrength, w = renderingLayers
    vec4 size;       // xy = size (area lights), z = shadow near plane, w = shadow far plane
    vec4 shadowA;    // x = shadowIndex (-1 = no shadow), y = bias, z = normalBias, w = soft (0/1)
    mat4 lightSpaceMatrix; // directional/spot only
};

layout(std430, binding = 0) readonly buffer LightBuffer
{
    Light lights[];
};

// Per tile: [0] = light count, [1..MAX_LIGHTS_PER_TILE] = light indices into LightBuffer. Written
// once per frame by LightCulling.shader for the tile grid built from screenSize/tileCountXF below.
layout(std430, binding = 1) readonly buffer TileLightIndices
{
    int tileLights[];
};

// Must match ForwardPlusRenderer.TileSize / MaxLightsPerTile in C#.
#define TILE_SIZE 16
#define MAX_LIGHTS_PER_TILE 256

#define LIGHT_DIRECTIONAL 0
#define LIGHT_POINT 1
#define LIGHT_SPOT 2

// Tap counts - keep both odd or even, doesn't matter for Vogel, but raising them
// directly trades quality for cost. 12 / 16 are good defaults on desktop.
#define SHADOW2D_PCF_TAPS 12
#define SHADOWCUBE_PCF_TAPS 16

// PCF coverage below this = fragment is treated as fully shadowed and receives
// zero direct (Phong/lambert) light from that light. Lower (e.g. 0.2) = thinner,
// lighter shadows; higher (e.g. 0.8) = heavier, darker shadows.
#define SHADOW_HARD_THRESHOLD 0.5

uniform float tileCountXF;

vec3 getFinalNormal()
{
    vec2 uvN = vUV * material_normalScale + material_normalOffset;
    vec3 tangentNormal = texture(material_normal, uvN).xyz * 2.0 - 1.0;

    vec3 worldN = normalize(mat3(model) * vNormal);

    vec3 pos_dx = dFdx(vFragPos);
    vec3 pos_dy = dFdy(vFragPos);
    vec2 tex_dx = dFdx(uvN);
    vec2 tex_dy = dFdy(uvN);

    float det = tex_dx.x * tex_dy.y - tex_dx.y * tex_dy.x;
    if (abs(det) > 1e-8)
    {
        vec3 T = normalize((pos_dx * tex_dy.y - pos_dy * tex_dx.y) / det);
        vec3 B = normalize((pos_dy * tex_dx.x - pos_dx * tex_dy.x) / det);
        mat3 TBN = mat3(T, B, worldN);
        return normalize(TBN * tangentNormal);
    }
    else
    {
        return worldN;
    }
}

// ---------------------------------------------------------------------------
// Noise helpers
// ---------------------------------------------------------------------------

// Cheap per-pixel noise used to rotate PCF kernels. Turning visible banding into
// fine noise is almost always a net visual win.
float InterleavedGradientNoise(vec2 pixel)
{
    return fract(52.9829189 * fract(0.06711056 * pixel.x + 0.00583715 * pixel.y));
}

// NOTE: If you keep the "+ BlueNoise(...)" additions in the shadow samplers
// below, this stays. If you remove them (recommended - see comments there),
// this function becomes unused and can be deleted.
float BlueNoise(vec2 fragCoord)
{
    // Interleaved gradient noise base
    float base = fract(52.9829189 * fract(dot(fragCoord, vec2(0.06711056, 0.00583715))));

    // Add a high-frequency spatial alternating offset (simplified 2-tap/5-tap gradient mix)
    float h1 = fract(sin(dot(fragCoord + vec2(1.0, 0.0), vec2(0.06711056, 0.00583715))) * 43758.5453);
    float h2 = fract(sin(dot(fragCoord + vec2(0.0, 1.0), vec2(0.06711056, 0.00583715))) * 43758.5453);

    // Blend to suppress low-frequency clusters
    float pseudoBlue = base - 0.25 * (h1 + h2 - 0.5);
    return clamp(pseudoBlue, 0.0, 1.0);
}

// ---------------------------------------------------------------------------
// Shadow sampling helpers
// ---------------------------------------------------------------------------

// Vogel (golden-angle) spiral on the unit disk. Low discrepancy for any tap
// count and trivially rotatable per fragment.
vec2 VogelDiskSample(int i, int n, float phase)
{
    float r = sqrt((float(i) + 0.5) / float(n));
    float theta = float(i) * 2.39996323 + phase; // golden angle
    return vec2(r * cos(theta), r * sin(theta));
}

// Slope-scaled depth bias: surfaces nearly parallel to the light direction need
// extra bias to avoid self-shadow acne, but we clamp the magnification so we
// don't wash out legitimate contact shadows on grazing geometry.
float SlopeScaledBias(float rawBias, float ndl)
{
    float c = clamp(ndl, 0.0, 1.0);
    float tanTheta = sqrt(max(1.0 - c * c, 0.0)) / max(c, 1e-3);
    return rawBias * clamp(tanTheta, 1.0, 8.0);
}

// Algorithm: PCF for directional / spot lights.
//   - Hard: single tap with slope-scaled bias.
//   - Soft: 12-tap Vogel disk, rotated per pixel, with a cheap contact-hardening
//     term that widens the kernel as the receiver-to-blocker gap grows. This
//     gets most of the PCSS look without a separate blocker-search pass.
float SampleShadow2D(int shadowIndex, vec4 lightSpacePos, float rawBias, float ndl, bool soft)
{
    vec3 proj = lightSpacePos.xyz / lightSpacePos.w;
    proj = proj * 0.5 + 0.5;

    // Outside the light's shadow volume, or behind its near plane: treat as lit.
    if (proj.z > 1.0 || proj.z < 0.0 ||
        proj.x < 0.0 || proj.x > 1.0 ||
        proj.y < 0.0 || proj.y > 1.0)
        return 1.0;

    float bias = SlopeScaledBias(rawBias, ndl);

    if (!soft)
    {
        float depth = texture(shadowMap2D, vec3(proj.xy, float(shadowIndex))).r;
        return (proj.z - bias > depth) ? 0.0 : 1.0;
    }

    vec2 texel = 1.0 / vec2(textureSize(shadowMap2D, 0).xy);

    // One centre tap to estimate the blocker distance, then widen the PCF disk
    // proportionally to (receiver - blocker). Near contact: sharp. Far: soft.
    float blockerDepth = texture(shadowMap2D, vec3(proj.xy, float(shadowIndex))).r;
    float d = max(proj.z - blockerDepth, 0.0);
    float radius = mix(1.0, 3.5, clamp(d * 80.0, 0.0, 1.0)); // in texels

    float phase = InterleavedGradientNoise(gl_FragCoord.xy) * 6.2831853;

    float shadow = 0.0;
    for (int i = 0; i < SHADOW2D_PCF_TAPS; ++i)
    {
        vec2 off = VogelDiskSample(i, SHADOW2D_PCF_TAPS, phase) * radius * texel;
        float depth = texture(shadowMap2D, vec3(proj.xy + off, float(shadowIndex))).r;
        shadow += (proj.z - bias > depth) ? 0.0 : 1.0;
    }

    // WARNING: adding [0,1] noise to an already-[0,1] shadow term makes lit
    // pixels brighter than 1 and produces per-pixel speckle. Recommended fix is
    // to delete the "+ BlueNoise(...)" and just return the PCF average. If you
    // really want per-pixel dither, jitter the *comparison* instead, e.g.:
    //   float dither = (BlueNoise(gl_FragCoord.xy) - 0.5) * 1e-4;
    //   shadow += (proj.z - bias + dither > depth) ? 0.0 : 1.0;
    return shadow / float(SHADOW2D_PCF_TAPS);
}

// Algorithm: omnidirectional (cubemap) shadow mapping for point lights. The cube
// array stores raw linear distance from the light to the closest occluder per
// direction; a fragment is in shadow when its own distance to the light is
// farther than that stored distance.
float SampleShadowCube(int shadowIndex, vec3 lightToFrag, float farPlane,
                       float rawBias, float ndl, bool soft)
{
    float currentDist = length(lightToFrag);

    // Past the shadow far plane (or exactly on top of the light) the map has no
    // valid data; without this guard the comparison reads as "fully shadowed"
    // and paints a dark halo outside the light's range.
    if (currentDist > farPlane || currentDist < 1e-5)
        return 1.0;

    vec3 dir = lightToFrag / currentDist;
    float bias = SlopeScaledBias(max(rawBias, 1e-4), ndl);

    if (!soft)
    {
        float closest = texture(shadowMapCube, vec4(dir, float(shadowIndex))).r;
        return (currentDist - bias > closest) ? 0.0 : 1.0;
    }

    // Tangent frame around the sample direction so offsets are a constant
    // *angular* radius regardless of distance. (Adding offsets to the raw,
    // non-normalised lightToFrag gave a distance-dependent and unevenly
    // distributed penumbra - the axis-aligned 8-tap pattern was also very
    // visibly patterned.)
    vec3 up = abs(dir.y) < 0.99 ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0);
    vec3 tangent = normalize(cross(up, dir));
    vec3 bitangent = cross(dir, tangent);

    // Angular penumbra radius grows with distance so far-away contacts are soft,
    // while a small floor keeps near contacts reasonably sharp.
    float radius = mix(0.005, 0.05, clamp(currentDist / max(farPlane, 1e-3), 0.0, 1.0));

    float phase = InterleavedGradientNoise(gl_FragCoord.xy) * 6.2831853;

    float shadow = 0.0;
    for (int i = 0; i < SHADOWCUBE_PCF_TAPS; ++i)
    {
        vec2 d = VogelDiskSample(i, SHADOWCUBE_PCF_TAPS, phase) * radius;
        vec3 sampleDir = normalize(dir + tangent * d.x + bitangent * d.y);
        float closest = texture(shadowMapCube, vec4(sampleDir, float(shadowIndex))).r;
        shadow += (currentDist - bias > closest) ? 0.0 : 1.0;
    }

    return shadow / float(SHADOWCUBE_PCF_TAPS);
}

void main()
{
    vec3 normal = getFinalNormal();

    ivec2 tileID = ivec2(gl_FragCoord.xy) / TILE_SIZE;
    int tileCountX = int(tileCountXF);
    int tileIndex = tileID.y * tileCountX + tileID.x;
    int baseOffset = tileIndex * (MAX_LIGHTS_PER_TILE + 1);
    int count = tileLights[baseOffset];

    // Small constant ambient so surfaces not reached by any light aren't pitch black.
    vec3 ambient = vec3(0.005);
    vec3 diffuse = vec3(0.0);

    // Track average visibility across shadow-casting lights
    // so ambient is attenuated by how shadowed the surface is on average.
    float visibilitySum = 0.0;
    int   shadowCastingLights = 0;

    for (int i = 0; i < count; i++)
    {
        int lightIndex = tileLights[baseOffset + 1 + i];
        Light light = lights[lightIndex];
        int type = int(light.direction.w);

        vec3 toLight;
        float atten = 1.0;

        if (type == LIGHT_DIRECTIONAL)
        {
            // Directional lights have no position - light travels along -direction, with no
            // distance falloff.
            toLight = normalize(-light.direction.xyz);
        }
        else
        {
            vec3 delta = light.position.xyz - vFragPos;
            float dist = length(delta);
            toLight = dist > 1e-5 ? delta / dist : vec3(0.0, 1.0, 0.0);
            atten = 1.0 / max(dist * dist, 1e-4);

            if (type == LIGHT_SPOT)
            {
                float cosAngle = dot(-toLight, normalize(light.direction.xyz));
                float innerCos = light.params.x;
                float outerCos = light.params.y;
                float spot = clamp((cosAngle - outerCos) / max(innerCos - outerCos, 1e-4), 0.0, 1.0);
                atten *= spot * spot;
            }
        }

        float ndl = max(dot(normal, toLight), 0.0);
        if (ndl <= 0.0) continue;

        float shadow = 1.0;
        int shadowIndex = int(light.shadowA.x);
        if (shadowIndex >= 0)
        {
            bool soft = light.shadowA.w > 0.5;
            float bias = light.shadowA.y;
            float normalBias = light.shadowA.z;
            vec3 biasedPos = vFragPos + normal * normalBias;

            float rawShadow;
            if (type == LIGHT_POINT)
                rawShadow = SampleShadowCube(shadowIndex, biasedPos - light.position.xyz,
                                             light.size.w, bias, ndl, soft);
            else
                rawShadow = SampleShadow2D(shadowIndex, light.lightSpaceMatrix * vec4(biasedPos, 1.0),
                                            bias, ndl, soft);

            // Apply artist-controlled shadow strength (0 = no shadow, 1 = full shadow)
            float shadowStrength = light.params.z;
            shadow = mix(1.0, rawShadow, shadowStrength);

            // Accumulate visibility for ambient attenuation
            visibilitySum += shadow;
            shadowCastingLights += 1;
        }

        // Color already has intensity baked in (see LightRenderData.Color in C#) - no separate
        // intensity or range multiply needed here.
        diffuse += light.color.rgb * ndl * atten * shadow;
    }

    // Base color drives per-surface tint (default white). A material albedo texture is applied when
    // present, but a fallback keeps the result non-black if no albedo is bound on this pass.
    vec3 albedo = baseColor.rgb * (vec3(texture(material_albedo, vUV)) + vec3(1e-3));

// Ambient is scaled by average shadow visibility so a fully-shadowed fragment
    // goes dark instead of being propped up by the constant floor.
    float ambientVisibility = shadowCastingLights > 0
        ? visibilitySum / float(shadowCastingLights)
        : 1.0;

    vec3 color = albedo * ambient * ambientVisibility + albedo * diffuse;
    color += emissionColor.rgb;

    FragColor = vec4(color, 1.0);

    //FragColor = texture(material_albedo, vUV);
}