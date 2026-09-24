#version 430 core

#define TILE_SIZE 16
#define MAX_LIGHTS_PER_TILE 256

layout(local_size_x = TILE_SIZE, local_size_y = TILE_SIZE, local_size_z = 1) in;

struct Light
{
    vec4 position;
    vec4 color;
    vec4 direction;
    vec4 params;
    vec4 size;
    vec4 shadowA;
    mat4 lightSpaceMatrix;
};

layout(std430, binding = 0) readonly buffer LightBuffer
{
    Light lights[];
};

layout(std430, binding = 1) writeonly buffer TileLightIndices
{
    int tileLights[];
};

uniform sampler2D depthTexture;
uniform mat4 view;
uniform mat4 invProjection;
uniform vec2 screenSizeF;
uniform vec2 tileCountF;
uniform int lightCount;
uniform float nearPlane;
uniform float farPlane;

shared uint minDepthUint;
shared uint maxDepthUint;
shared int tileLightCount;
shared int tileLightIndicesLocal[MAX_LIGHTS_PER_TILE];
shared vec4 frustumPlanes[4];

float LinearizeDepth(float depth)
{
    float ndcZ = depth * 2.0 - 1.0;
    return (2.0 * nearPlane * farPlane) / (farPlane + nearPlane - ndcZ * (farPlane - nearPlane));
}

vec4 ComputePlane(vec3 p0, vec3 p1, vec3 p2)
{
    vec3 n = normalize(cross(p1 - p0, p2 - p0));
    return vec4(n, -dot(n, p0));
}

void main()
{
    ivec2 screenSize = ivec2(screenSizeF);
    ivec2 tileCount = ivec2(tileCountF);

    ivec2 tileID = ivec2(gl_WorkGroupID.xy);
    int tileIndex = tileID.y * tileCount.x + tileID.x;
    uint localIndex = gl_LocalInvocationIndex;

    if (localIndex == 0u)
    {
        minDepthUint = 0xFFFFFFFFu;
        maxDepthUint = 0u;
        tileLightCount = 0;
    }
    barrier();

    ivec2 pixelCoord = ivec2(gl_GlobalInvocationID.xy);
    if (pixelCoord.x < screenSize.x && pixelCoord.y < screenSize.y)
    {
        float depthSample = texelFetch(depthTexture, pixelCoord, 0).r;
        if (depthSample < 1.0)
        {
            uint depthAsUint = floatBitsToUint(depthSample);
            atomicMin(minDepthUint, depthAsUint);
            atomicMax(maxDepthUint, depthAsUint);
        }
    }
    barrier();

    bool tileHasGeometry = minDepthUint <= maxDepthUint;
    float minDepth = tileHasGeometry ? uintBitsToFloat(minDepthUint) : 0.0;
    float maxDepth = tileHasGeometry ? uintBitsToFloat(maxDepthUint) : 1.0;

    float nearViewZ = -LinearizeDepth(minDepth);
    float farViewZ = -LinearizeDepth(maxDepth);

    if (localIndex == 0u)
    {
        vec2 tileMinNDC = (vec2(tileID) * float(TILE_SIZE) / vec2(screenSize)) * 2.0 - 1.0;
        vec2 tileMaxNDC = (vec2(tileID + ivec2(1)) * float(TILE_SIZE) / vec2(screenSize)) * 2.0 - 1.0;

        vec2 ndcCorners[4] = vec2[4](
            vec2(tileMinNDC.x, tileMinNDC.y),
            vec2(tileMaxNDC.x, tileMinNDC.y),
            vec2(tileMaxNDC.x, tileMaxNDC.y),
            vec2(tileMinNDC.x, tileMaxNDC.y)
        );

        vec3 corners[4];
        for (int i = 0; i < 4; i++)
        {
            vec4 viewPos = invProjection * vec4(ndcCorners[i], -1.0, 1.0);
            viewPos /= viewPos.w;
            corners[i] = viewPos.xyz;
        }

        vec3 origin = vec3(0.0);

        frustumPlanes[0] = ComputePlane(origin, corners[0], corners[3]);
        frustumPlanes[1] = ComputePlane(origin, corners[2], corners[1]);
        frustumPlanes[2] = ComputePlane(origin, corners[3], corners[2]);
        frustumPlanes[3] = ComputePlane(origin, corners[1], corners[0]);
    }
    barrier();

    for (uint i = localIndex; i < uint(lightCount); i += uint(TILE_SIZE * TILE_SIZE))
    {
        Light light = lights[i];
        int type = int(light.direction.w);
        bool visible = true;

        if (type != 0)
        {
            vec3 lightPosView = (view * vec4(light.position.xyz, 1.0)).xyz;
            float radius = light.position.w;

            for (int p = 0; p < 4 && visible; p++)
            {
                vec4 plane = frustumPlanes[p];
                float dist = dot(plane.xyz, lightPosView) + plane.w;
                if (dist < -radius)
                    visible = false;
            }

            if (visible)
            {
                if (lightPosView.z + radius < farViewZ || lightPosView.z - radius > nearViewZ)
                    visible = false;
            }
        }

        if (visible)
        {
            int slot = atomicAdd(tileLightCount, 1);
            if (slot < MAX_LIGHTS_PER_TILE)
                tileLightIndicesLocal[slot] = int(i);
        }
    }
    barrier();

    if (localIndex == 0u)
    {
        int count = min(tileLightCount, MAX_LIGHTS_PER_TILE);
        int baseOffset = tileIndex * (MAX_LIGHTS_PER_TILE + 1);
        tileLights[baseOffset] = count;
        for (int i = 0; i < count; i++)
            tileLights[baseOffset + 1 + i] = tileLightIndicesLocal[i];
    }
}