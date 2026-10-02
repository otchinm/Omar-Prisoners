// Alpha tested, double sided, vertex lit (foliage, chain-link, cage bars). See PsxCore.cginc.
Shader "PrisonersOfOmar/PSX Lit Cutout"
{
    Properties
    {
        _MainTex ("Texture (RGBA)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        LOD 100
        Cull Off
        ZWrite On
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
                clip(tex.a * i.color.a - _Cutoff);
                float3 c = tex.rgb * i.color.rgb;
                c = PsxApplyFog(c, i.fogAffine.x);
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
