#version 450

layout(location = 0) in vec3 inPosition; // @vertex Position required
layout(location = 2) in vec4 inColor; // @vertex Color optional @default = vec4(1.0, 1.0, 1.0, 1.0)

layout(push_constant) uniform Push { mat4 modelMatrix; } push;

layout(location = 0) out vec4 vColor;
layout(location = 1) out vec3 vWorldPos;

layout(set = 0, binding = 0) uniform VertexUniforms
{
    mat4 ViewProj;
} uVertex;

void main()
{
    vec4 world = push.modelMatrix * vec4(inPosition, 1.0);
    gl_Position = uVertex.ViewProj * world;
    vColor = inColor;
    vWorldPos = world.xyz;
}
