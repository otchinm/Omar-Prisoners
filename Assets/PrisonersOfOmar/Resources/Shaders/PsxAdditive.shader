// Additive glow (flames, light halos, sparks): no lighting, no depth write, double sided.
// Texture alpha (x tint alpha) scales the contribution; fog fades it out.
Shader "PrisonersOfOmar/PSX Additive"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "ForceNoShadowCasting"="True" }
        LOD 100
        Blend One One
        Cull Off
        ZWrite Off
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
                float a = tex.a * i.color.a;
                float3 c = tex.rgb * i.color.rgb * (a * (1.0 - i.fogAffine.x));
                return fixed4(c, a);
            }
            ENDCG
        }
    }
    Fallback Off
}
