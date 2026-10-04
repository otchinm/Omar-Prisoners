// =====================================================================================================
//  PsxCore.cginc - shared code of every "PrisonersOfOmar/PSX *" world shader (Built-in Render Pipeline).
//
//  Everything that makes the PS1 look happens per VERTEX (only the texture fetch is per pixel):
//    * vertex snapping of clip-space xy to a coarse virtual pixel grid (the classic jitter / wobble)
//    * affine (not perspective-correct) texture mapping via the uv*w / w trick, blended by _PsxAffine
//    * Gouraud lighting from the custom PsxLight list (Unity Light components are never used)
//    * linear per-vertex distance fog
//    * "anomalies": view/clip-space warping driven by AnomalySystem (never a modelling change)
//
//  GLOBAL UNIFORMS (uploaded every frame by PsxRenderDriver: PsxLightManager / PsxEnvironment / AnomalySystem)
//    float4 _PsxSnapRes                 xy = vertex snap grid in virtual pixels (x = y * aspect). y < 1 disables snapping
//    float  _PsxAffine                  0 = perspective-correct UVs, 1 = full PS1 affine warping
//    float4 _PsxAmbient                 rgb = ambient light
//    float4 _PsxFogColor                rgb = fog color
//    float4 _PsxFogParams               x = fog start (m), y = 1 / (end - start) (0 disables fog), z = max fog (0..1)
//    float  _PsxLightCount              number of valid entries in the light arrays (0..16)
//    float4 _PsxLightPos[16]            xyz = world position, w = range (m)
//    float4 _PsxLightColor[16]          rgb = color * current intensity, w = type (0 point, 1 spot)
//    float4 _PsxLightDir[16]            xyz = spot direction (world, normalized), w = cos(outer half angle)
//    float  _PsxTime                    synchronized time in seconds (AnomalySystem.TimeSource, wrapped to 1 hour)
//    float4 _PsxAnomaly                 x = global intensity (baseline + pulse), y = seed offset, z = visual scale
//    float  _PsxAnomalyZoneCount        number of valid zones (0..8)
//    float4 _PsxAnomalyZones[8]         xyz = zone center, w = radius
//    float4 _PsxAnomalyZoneParams[8]    x = zone strength
//
//  MATERIAL PROPERTIES: _MainTex (+ tiling/offset), _Color (tint), _Cutoff (cutout), _EmissionBoost (emissive).
//  The mesh vertex color multiplies the albedo (the map bakes fake ambient occlusion into it).
//
//  Per-shader switches (#define before including this file):
//    PSX_LIT            apply vertex lighting (otherwise full bright)
//    PSX_TWO_SIDED      light both faces (abs(N.L)) for Cull Off materials
//    PSX_ANOMALY_SCALE  multiplier of the anomaly intensity (default 1)
// =====================================================================================================
#ifndef PSX_CORE_INCLUDED
#define PSX_CORE_INCLUDED

#include "UnityCG.cginc"

#define PSX_MAX_LIGHTS 16
#define PSX_MAX_ZONES 8
#define PSX_WRAP 0.35
#define PSX_LIGHT_CLAMP 2.0
#define PSX_MAX_ANOMALY 1.5

#ifndef PSX_ANOMALY_SCALE
#define PSX_ANOMALY_SCALE 1.0
#endif

// ------------------------------------------------------------------------------------ material
sampler2D _MainTex;
float4 _MainTex_ST;
float4 _Color;
float _Cutoff;
float _EmissionBoost;

// ------------------------------------------------------------------------------------ globals
float4 _PsxSnapRes;
float _PsxAffine;
float4 _PsxAmbient;
float4 _PsxFogColor;
float4 _PsxFogParams;
float _PsxLightCount;
float4 _PsxLightPos[PSX_MAX_LIGHTS];
float4 _PsxLightColor[PSX_MAX_LIGHTS];
float4 _PsxLightDir[PSX_MAX_LIGHTS];
float _PsxTime;
float4 _PsxAnomaly;
float _PsxAnomalyZoneCount;
float4 _PsxAnomalyZones[PSX_MAX_ZONES];
float4 _PsxAnomalyZoneParams[PSX_MAX_ZONES];

// ------------------------------------------------------------------------------------ structs
struct appdata_psx
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 texcoord : TEXCOORD0;
    float4 color : COLOR;
};

struct v2f_psx
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;          // perspective-correct uv
    float3 uvAffine : TEXCOORD1;    // xy = uv * w, z = w  (screen-linear after the divide in the fragment)
    float4 color : TEXCOORD2;       // rgb = vertex color * tint * lighting (0..2), a = alpha. TEXCOORD: COLOR clamps to 0..1 on old targets
    float2 fogAffine : TEXCOORD3;   // x = fog amount, y = affine blend factor
};

// ------------------------------------------------------------------------------------ hashes
// "Hash without sine" (Dave Hoskins): stable across GPUs for moderate input magnitudes.
float PsxHash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float PsxHash13(float3 p3)
{
    p3 = frac(p3 * 0.1031);
    p3 += dot(p3, p3.zyx + 31.32);
    return frac((p3.x + p3.y) * p3.z);
}

// ------------------------------------------------------------------------------------ anomaly intensity
// Same formula as AnomalySystem.GetIntensityAt (baseline + pulse + sum of linear zone falloffs), clamped for visuals.
float PsxAnomalyIntensity(float3 worldPos)
{
    float v = _PsxAnomaly.x;
    int count = (int)clamp(_PsxAnomalyZoneCount, 0.0, 8.0);
    [loop]
    for (int i = 0; i < PSX_MAX_ZONES; i++)
    {
        if (i >= count) break;
        float4 zone = _PsxAnomalyZones[i];
        float d = distance(worldPos, zone.xyz);
        v += _PsxAnomalyZoneParams[i].x * saturate(1.0 - d / max(zone.w, 0.001));
    }
    return clamp(v, 0.0, PSX_MAX_ANOMALY) * _PsxAnomaly.z;
}

// ------------------------------------------------------------------------------------ lighting
// Gouraud lighting: ambient + sum of lights with smooth range falloff (1-(d/r)^2)^2, wrap diffuse, soft spot cones.
// PsxLightManager.SampleIllumination mirrors this (without the N.L term).
float3 PsxVertexLighting(float3 worldPos, float3 worldNormal)
{
    float3 light = _PsxAmbient.rgb;
    int count = (int)clamp(_PsxLightCount, 0.0, 16.0);
    [loop]
    for (int i = 0; i < PSX_MAX_LIGHTS; i++)
    {
        if (i >= count) break;
        float4 lp = _PsxLightPos[i];
        float4 lc = _PsxLightColor[i];
        float4 ld = _PsxLightDir[i];

        float3 toLight = lp.xyz - worldPos;
        float d2 = max(dot(toLight, toLight), 0.000001);
        float r2 = max(lp.w * lp.w, 0.0001);
        float att = saturate(1.0 - d2 / r2);
        att *= att;

        float3 L = toLight * rsqrt(d2);
        float ndl = dot(worldNormal, L);
#if defined(PSX_TWO_SIDED)
        ndl = abs(ndl);
#endif
        float diff = saturate((ndl + PSX_WRAP) / (1.0 + PSX_WRAP));

        // spot: soft edge from cos(outer) to 35% of the way towards the axis
        float cosA = dot(-L, ld.xyz);
        float edge = ld.w + (1.0 - ld.w) * 0.35;
        float spot = smoothstep(ld.w, edge, cosA);
        spot = (lc.w > 0.5) ? spot : 1.0;

        light += lc.rgb * (diff * att * spot);
    }
    return min(light, PSX_LIGHT_CLAMP);
}

// ------------------------------------------------------------------------------------ fog
float PsxFogFactor(float dist)
{
    return saturate((dist - _PsxFogParams.x) * _PsxFogParams.y) * _PsxFogParams.z;
}

float3 PsxApplyFog(float3 c, float fog)
{
    return lerp(c, _PsxFogColor.rgb, fog);
}

// ------------------------------------------------------------------------------------ projection + PS1 artifacts
// Projects a world position to clip space and applies vertex snapping and the anomaly warps:
//   (a) snap grid collapse + grid crawl   (b) screen-space waves + rolling band   (c) depth breathing (dolly-zoom wobble)
//   (e) UV swimming (returned in uvOffset); polygon tearing and texture page corruption were removed
//   objPos   object-space position: seeds per-vertex randomness (coincident vertices move together)
//   anomaly  anomaly intensity at this vertex (0 .. ~1.5)
//   viewDist camera distance before any warp (for fog)
float4 PsxWorldToClip(float3 worldPos, float3 objPos, float anomaly, out float2 uvOffset, out float viewDist)
{
    float t = _PsxTime;
    // anomalies stay subtle: a faint swim / wobble at most, never geometry or texture corruption
    float a = min(anomaly, 0.35);
    float seed = _PsxAnomaly.y;

    float3 viewPos = mul(UNITY_MATRIX_V, float4(worldPos, 1.0)).xyz;
    viewDist = length(viewPos);

    // (c) depth breathing: radial scale around the view axis that varies with depth and time
    float breathe = sin(t * 1.31 - viewDist * 0.35) * 0.65 + sin(t * 0.43 + 1.7) * 0.35;
    viewPos.xy *= 1.0 + a * 0.11 * breathe;

    // (d) polygon tearing removed: yanked vertices read as random shapes popping up on screen, not as a PS1 look
    float4 clip = mul(UNITY_MATRIX_P, float4(viewPos, 1.0));
    float w = max(clip.w, 0.0001);
    float2 ndc = clip.xy / w;

    // (b) screen-space waves rolling through the geometry (heat-haze ripple + a travelling band)
    float bandPos = frac(t * 0.093 + seed * 0.01) * 2.8 - 1.4;
    float bandD = ndc.y - bandPos;
    float band = exp(-bandD * bandD * 9.0);
    float2 wave;
    wave.x = sin(ndc.y * 6.0 + t * 2.3) * 0.6 + sin(ndc.y * 17.0 - t * 4.1) * 0.25 + sin(ndc.y * 41.0 + t * 9.0) * band * 0.6;
    wave.y = sin(ndc.x * 4.0 - t * 1.7) * 0.5;
    ndc += wave * (a * (1.0 + band * 1.5)) * float2(0.035, 0.02);

    // (a) vertex snapping; the grid collapses with the anomaly (1/6 at intensity 1) and crawls at high intensity
    float snapOn = (_PsxSnapRes.y >= 1.0) ? 1.0 : 0.0;
    float collapse = 1.0 / (1.0 + a * a * 5.0);
    float2 grid = max(_PsxSnapRes.xy * 0.5 * collapse, float2(1.0, 1.0));
    float crawlTick = floor(t * 9.0);
    float crawlAmt = saturate(a * 1.5 - 0.15);
    float2 crawl = (float2(PsxHash11(crawlTick + seed), PsxHash11(crawlTick * 1.37 + 5.1 + seed)) - 0.5) * crawlAmt;
    float2 snapped = (floor(ndc * grid + 0.5 + crawl) - crawl) / grid;
    ndc = lerp(ndc, snapped, snapOn);

    clip.xy = (clip.w > 0.0001) ? ndc * clip.w : clip.xy;

    // (e) faint UV swimming
    uvOffset = float2(sin(worldPos.y * 1.9 + worldPos.x * 0.7 + t * 1.7),
                      sin(worldPos.z * 1.6 - worldPos.x * 0.5 + t * 1.3 + 1.3)) * (a * 0.05);
    // texture page corruption removed (it looked like broken textures)
    return clip;
}

// ------------------------------------------------------------------------------------ vertex program
v2f_psx PsxVert(appdata_psx v)
{
    v2f_psx o;
    float3 worldPos = mul(unity_ObjectToWorld, float4(v.vertex.xyz, 1.0)).xyz;
    float anomaly = PsxAnomalyIntensity(worldPos) * PSX_ANOMALY_SCALE;

    float2 uvOffset = float2(0.0, 0.0);
    float viewDist = 0.0;
    o.pos = PsxWorldToClip(worldPos, v.vertex.xyz, anomaly, uvOffset, viewDist);

    float2 uv = TRANSFORM_TEX(v.texcoord, _MainTex) + uvOffset;
    o.uv = uv;
    o.uvAffine = float3(uv * o.pos.w, o.pos.w);

    float4 col = v.color * _Color;
#if defined(PSX_LIT)
    float3 n = mul(v.normal, (float3x3)unity_WorldToObject);   // object -> world normal (inverse transpose)
    n *= rsqrt(max(dot(n, n), 0.00000001));                    // safe normalize (meshes without normals)
    col.rgb *= PsxVertexLighting(worldPos, n);
#endif
    o.color = col;
    o.fogAffine = float2(PsxFogFactor(viewDist), saturate(_PsxAffine + min(anomaly, 0.35) * 0.15));
    return o;
}

// ------------------------------------------------------------------------------------ fragment helpers
// Blend between perspective-correct and affine (screen-linear) texture coordinates.
float2 PsxResolveUV(v2f_psx i)
{
    float2 affineUV = i.uvAffine.xy / max(i.uvAffine.z, 0.00001);
    return lerp(i.uv, affineUV, i.fogAffine.y);
}

#endif // PSX_CORE_INCLUDED
