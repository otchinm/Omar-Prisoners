// Alpha blended, vertex lit (both faces), no depth write (dirty glass, puddles, smoke, blood spray).
Shader "PrisonersOfOmar/PSX Transparent"
{
    Properties
    {
        _MainTex ("Texture (RGBA)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "ForceNoShadowCasting"="True" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            CGPROGRAM
            #pragma vertex PsxVert
            #pragma fragment frag
            #pragma target 3.0
            #define PSX_LIT 1
            #define PSX_TWO_SIDED 1
            #include "PsxCore.cginc"

            fixed4 frag(v2f_psx i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, PsxResolveUV(i));
                float3 c = tex.rgb * i.color.rgb;
                c = PsxApplyFog(c, i.fogAffine.x);
                return fixed4(c, tex.a * i.color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
