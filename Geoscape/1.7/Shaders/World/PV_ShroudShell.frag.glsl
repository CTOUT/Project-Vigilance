#version 450

layout(location = 0) in vec4 vColor;
layout(location = 1) in vec3 vWorldPos;

layout(location = 0) out vec4 outColor;

layout(set = 0, binding = 1) uniform FragmentUniforms
{
    vec4 SunDir;
    mat4 WorldToCamera;
    mat4 ViewProj;
} uFragment;

layout(set = 2, binding = 0) uniform MaterialUniforms
{
    vec4 _BaseColor;    // @default = vec4(0.024, 0.031, 0.047, 1.0)
    vec4 _NebulaColor;  // @default = vec4(0.095, 0.165, 0.245, 1.0)
    vec4 _RimColor;     // @default = vec4(0.240, 0.420, 0.560, 1.0)
    vec4 _ShroudParams; // @default = vec4(0.45, 1.0, 1.0, 0.0)
    float _GameSeconds; // global from RimWorld/Waylith
} uMaterial;

float hash31(vec3 p)
{
    p = fract(p * vec3(0.1031, 0.1030, 0.0973));
    p += dot(p, p.yxz + 33.33);
    return fract((p.x + p.y) * p.z);
}

float valueNoise3D(vec3 p)
{
    vec3 i = floor(p);
    vec3 f = fract(p);
    vec3 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);

    float n000 = hash31(i + vec3(0.0, 0.0, 0.0));
    float n100 = hash31(i + vec3(1.0, 0.0, 0.0));
    float n010 = hash31(i + vec3(0.0, 1.0, 0.0));
    float n110 = hash31(i + vec3(1.0, 1.0, 0.0));
    float n001 = hash31(i + vec3(0.0, 0.0, 1.0));
    float n101 = hash31(i + vec3(1.0, 0.0, 1.0));
    float n011 = hash31(i + vec3(0.0, 1.0, 1.0));
    float n111 = hash31(i + vec3(1.0, 1.0, 1.0));

    float nx00 = mix(n000, n100, u.x);
    float nx10 = mix(n010, n110, u.x);
    float nx01 = mix(n001, n101, u.x);
    float nx11 = mix(n011, n111, u.x);

    float nxy0 = mix(nx00, nx10, u.y);
    float nxy1 = mix(nx01, nx11, u.y);

    return mix(nxy0, nxy1, u.z);
}

float fbm3D(vec3 p)
{
    float n1 = valueNoise3D(p);
    float n2 = valueNoise3D(p * 2.07 + vec3(13.7, -9.2, 5.1));
    float n3 = valueNoise3D(p * 4.19 + vec3(-6.3, 17.4, -11.8));
    return n1 * 0.56 + n2 * 0.29 + n3 * 0.15;
}

void main()
{
    if (vColor.a <= 0.001)
    {
        discard;
    }

    // Translucent explored Haze hexes (alpha < 0.98) pass through vertex colour directly.
    if (vColor.a < 0.98)
    {
        outColor = vColor;
        return;
    }

    vec3 normal = normalize(vWorldPos);
    vec3 viewDir = normalize(vec3(uFragment.WorldToCamera[0][2], uFragment.WorldToCamera[1][2], uFragment.WorldToCamera[2][2]));
    float ndotv = clamp(abs(dot(normal, viewDir)), 0.0, 1.0);

    float intensity = clamp(uMaterial._ShroudParams.x, 0.0, 1.0);
    float motionScale = clamp(uMaterial._ShroudParams.y, 0.0, 1.0);
    float limbAAEnabled = uMaterial._ShroudParams.z;

    vec3 col = uMaterial._BaseColor.rgb;

    if (intensity > 0.005)
    {
        float t = uMaterial._GameSeconds * motionScale;
        vec3 p1 = normal * 3.4 + vec3(t * 0.048, -t * 0.031, t * 0.022);
        vec3 p2 = normal * 7.2 + vec3(-t * 0.028, t * 0.036, 7.3);

        float nPrimary = fbm3D(p1);
        float nSecondary = fbm3D(p2);
        float nebula = clamp(nPrimary * 0.68 + nSecondary * 0.32, 0.0, 1.0);
        float wisp = pow(nebula, 1.25);

        // Scale smoothly from subtle obsidian wisps at low intensity to vivid nebula at 100%
        float blend = clamp(wisp * (0.25 + 1.35 * intensity), 0.0, 1.0) * intensity;
        col = mix(col, uMaterial._NebulaColor.rgb, blend);
    }

    // Sun-lit Fresnel atmospheric rim feathering to soften silhouette contrast against PlanetGlow
    float sunTerm = 0.5;
    if (dot(uFragment.SunDir.xyz, uFragment.SunDir.xyz) > 1e-6)
    {
        sunTerm = clamp(1.0 - (dot(normal, normalize(uFragment.SunDir.xyz)) * 0.5 + 0.5), 0.0, 1.0);
    }
    float fresnel = pow(1.0 - ndotv, 4.2);
    float rimStrength = fresnel * mix(0.14, 0.52, sunTerm);
    col = mix(col, uMaterial._RimColor.rgb, clamp(rimStrength, 0.0, 0.65));

    // Screen-space analytic limb anti-aliasing along outermost silhouette pixels
    float alpha = vColor.a;
    if (limbAAEnabled > 0.5)
    {
        float edgeWidth = max(fwidth(ndotv) * 1.75, 0.014);
        alpha *= smoothstep(0.0, edgeWidth, ndotv);
    }

    if (alpha <= 0.001)
    {
        discard;
    }

    outColor = vec4(clamp(col, 0.0, 1.0), alpha);
}
