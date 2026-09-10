#version 330 core

in vec2 vUV;
in vec3 vNormal;

out vec4 FragColor;

void main()
{
    FragColor = vec4(1.0, 0.0, 1.0, 1.0);  // Magenta color
}