#version 430 core

in vec3 vWorldPos;

uniform vec3 lightPos;

layout(location = 0) out float FragDistance;

void main()
{
    FragDistance = length(vWorldPos - lightPos);
}