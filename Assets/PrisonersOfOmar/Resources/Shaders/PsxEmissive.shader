// Full bright glowing surfaces (lit windows, bulbs, lamp lenses). Only partially fogged so they glow through fog.
Shader "PrisonersOfOmar/PSX Emissive"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _EmissionBoost ("Emission Boost", Range(0,4)) = 1.5
    }
    SubShader
    {
        Tags { "Queue"="Geometry" "RenderType"="Opaque" "IgnoreProjector"="True" }
        LOD 100
        Cull Back
        ZWrite On
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex PsxVert
            #pragma fragment frag
            #pragma target 3.0
            #include "PsxCore.cginc"

            fixed4 frag(v2f_psx i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, PsxResolveUV(i));
                float3 c = tex.rgb * i.color.rgb * _EmissionBoost;
                c = PsxApplyFog(c, i.fogAffine.x * 0.35);
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
