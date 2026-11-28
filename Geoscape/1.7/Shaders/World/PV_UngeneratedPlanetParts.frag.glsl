#version 450

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec2 vUv;
layout(location = 2) in vec3 vWorldPos;

layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 1) uniform FragmentUniforms
{
    vec4 SunDir;
    mat4 WorldToCamera;
    mat4 ViewProj;
} uFragment;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _DimOverlay; // @default = vec4(0.03921569, 0.05490196, 0.07843138, 0.0)
} uMaterial;

const vec4 MaxSeaDepthColor = vec4(0.173, 0.204, 0.259, 1.0);

const vec3 NightColor = vec3(0.482, 0.603, 0.682);
const vec3 DayColor   = vec3(1.0, 1.0, 1.0);
const float DayNightStart = 0.45;
const float DayNightEnd   = 0.55;

const float WaterSpecularAttenuation = 0.15;
const float WaterShininess = 15.0;

vec3 sunDiffuseLight(vec3 normal, vec3 lightDir)
{
    if (dot(lightDir, lightDir) < 1e-6)
        return DayColor;
    float light = 1.0 - (dot(normal, normalize(lightDir)) * 0.5 + 0.5);
    float t = (light - DayNightStart) / (DayNightEnd - DayNightStart);
    return mix(NightColor, DayColor, clamp(t, 0.0, 1.0));
}

vec4 oceanSpecularColor(vec3 normal, vec3 lightDir)
{
    vec3 viewDirection = -vec3(uFragment.WorldToCamera[0][2], uFragment.WorldToCamera[1][2], uFragment.WorldToCamera[2][2]);
    return vec4(WaterSpecularAttenuation * pow(max(0.0, dot(reflect(-lightDir, normal), viewDirection)), WaterShininess));
}

void main()
{
    vec3 normal = normalize(vWorldPos);
    // applySeaDepthToOceanColor(white, depthFactor = 1.0) collapses to the max-sea-depth colour.
    vec4 fin = MaxSeaDepthColor;
    fin.rgb *= sunDiffuseLight(normal, uFragment.SunDir.xyz);
    fin += oceanSpecularColor(normal, uFragment.SunDir.xyz);
    fin.rgb = clamp(fin.rgb, 0.0, 1.0);
    fin.rgb = mix(fin.rgb, uMaterial._DimOverlay.rgb, clamp(uMaterial._DimOverlay.a, 0.0, 1.0));
    fin.a = 1.0;
    outColor = fin;
}
