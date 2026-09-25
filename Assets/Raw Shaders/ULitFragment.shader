#version 430 core

in vec2 vUV;
in vec3 vNormal;
in vec3 vFragPos;

out vec4 FragColor;

uniform vec4  baseColor;
uniform vec4  emissionColor;
uniform int   zWrite;
uniform float timeSinceStart;
uniform int   objectLayer;

uniform int  textureCount;
uniform mat4 model;

uniform sampler2D material_albedo;
uniform vec2 material_albedoScale;
uniform vec2 material_albedoOffset;

void main()
    {
        vec2 uvAlbedo = vUV * material_albedoScale + material_albedoOffset;

        vec4 albedoTex = texture(material_albedo, uvAlbedo) + vec4(1e-3, 1e-3, 1e-3, 0.0);
        vec3 albedo    = baseColor.rgb * albedoTex.rgb;

        vec3 color = albedo + emissionColor.rgb;

        FragColor = vec4(color, albedoTex.a * baseColor.a);
    }