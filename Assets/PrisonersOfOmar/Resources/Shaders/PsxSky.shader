// Night sky dome (PsxSky). Unlit cylindrical panorama, drawn first (Background queue, no depth write).
// The dome is re-centered on the rendering camera in the vertex shader and pushed to 40% of the far plane,
// so it never clips and never needs to be moved for correctness. It still snaps / wobbles lightly like the world
// (global anomaly only, scaled down) and fades to the fog color towards the horizon.
Shader "PrisonersOfOmar/PSX Sky"
{
    Properties
    {
        _MainTex ("Panorama (cylindrical)", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _HorizonFog ("Horizon Fog", Range(0,1)) = 0.85
        _HorizonHeight ("Horizon Fog Height", Range(0.01,1)) = 0.3
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "IgnoreProjector"="True" "PreviewType"="Skybox" "ForceNoShadowCasting"="True" }
        LOD 100
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #define PSX_ANOMALY_SCALE 0.35
            #include "PsxCore.cginc"

            float _HorizonFog;
            float _HorizonHeight;

            v2f_psx vert(appdata_psx v)
            {
                v2f_psx o;
                float3 dir = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz);
                dir *= rsqrt(max(dot(dir, dir), 0.000001));
                float3 worldPos = _WorldSpaceCameraPos + dir * (_ProjectionParams.z * 0.4);

                // global anomaly only (zones are local and the dome is far away)
                float anomaly = clamp(_PsxAnomaly.x, 0.0, PSX_MAX_ANOMALY) * _PsxAnomaly.z * PSX_ANOMALY_SCALE;
                float2 uvOffset = float2(0.0, 0.0);
                float viewDist = 0.0;
                o.pos = PsxWorldToClip(worldPos, v.vertex.xyz * 37.0, anomaly, uvOffset, viewDist);

                float2 uv = TRANSFORM_TEX(v.texcoord, _MainTex) + uvOffset * 0.25;
                o.uv = uv;
                o.uvAffine = float3(uv * o.pos.w, o.pos.w);
                o.color = v.color * _Color;

                // fog-tinted horizon; fully fog colored below the horizon
                float horizon = 1.0 - saturate((dir.y + 0.02) / max(_HorizonHeight, 0.01));
                float below = saturate(-dir.y * 8.0);
                float fog = max(horizon * horizon * _HorizonFog, below);
                o.fogAffine = float2(fog, saturate(_PsxAffine * 0.5));
                return o;
            }

            fixed4 frag(v2f_psx i) : SV_Target
            {
                float2 uv = PsxResolveUV(i);
                uv.y = clamp(uv.y, 0.004, 0.996);   // never wrap vertically (panorama top / bottom rows)
                fixed4 tex = tex2D(_MainTex, uv);
                float3 c = tex.rgb * i.color.rgb;
                c = PsxApplyFog(c, i.fogAffine.x);
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
