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

// Must match GPULight's layout in C# (and LightCulling.shader's copy of this struct) field-for-field.
struct Light
{
    vec4 position;   // xyz = world-space position, w = range
    vec4 color;      // rgb = color (intensity-premultiplied), w = intensity
    vec4 direction;  // xyz = direction, w = type (0=Directional,1=Point,2=Spot,3=Area)
    vec4 params;     // x = innerConeCos, y = outerConeCos, z = shadowStrength, w = renderingLayers
    vec4 size;       // xy = size (area lights), zw unused
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

void main()
{
    vec3 normal = getFinalNormal();

    ivec2 tileID = ivec2(gl_FragCoord.xy) / TILE_SIZE;
    int tileCountX = int(tileCountXF);
    int tileIndex = tileID.y * tileCountX + tileID.x;
    int baseOffset = tileIndex * (MAX_LIGHTS_PER_TILE + 1);
    int count = tileLights[baseOffset];

    // Small constant ambient so surfaces not reached by any point light aren't pitch black.
    vec3 ambient = vec3(0.001);

    // Direct diffuse from the tile's assigned lights (intensity + color aware).
    vec3 diffuse = vec3(0.0);
    for (int i = 0; i < count; i++)
    {
        int lightIndex = tileLights[baseOffset + 1 + i];
        Light light = lights[lightIndex];
        vec3 toLight = normalize(light.position.xyz - vFragPos);
        float ndl = max(dot(normal, toLight), 0.0);
        // Attenuate with the light's range so far/out-of-range light bleeds realistically.
        float dist = length(light.position.xyz - vFragPos);
        float atten = 1.0 / max(dist * dist, 1e-4);
        diffuse += light.color.rgb * light.position.w * ndl * atten;
    }

    // Base color drives per-surface tint (default white). A material albedo texture is applied when
    // present, but a fallback keeps the result non-black if no albedo is bound on this pass.
    vec3 albedo = baseColor.rgb * (vec3(texture(material_albedo, vUV)) + vec3(1e-3));

    vec3 color = albedo * ambient + albedo * diffuse;
    color += emissionColor.rgb;

    FragColor = vec4(color, 1.0);
}
