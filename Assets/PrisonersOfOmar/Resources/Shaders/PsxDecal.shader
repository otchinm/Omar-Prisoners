// Alpha blended overlay with depth offset (blood, graffiti, stains, blob shadows). Vertex lit, no depth write.
Shader "PrisonersOfOmar/PSX Decal"
{
    Properties
    {
        _MainTex ("Texture (RGBA)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest+20" "RenderType"="Transparent" "IgnoreProjector"="True" "ForceNoShadowCasting"="True" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual
        Offset -1, -1

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
                float a = tex.a * i.color.a;
                clip(a - 0.004);
                float3 c = tex.rgb * i.color.rgb;
                c = PsxApplyFog(c, i.fogAffine.x);
                return fixed4(c, a);
            }
            ENDCG
        }
    }
    Fallback Off
}
