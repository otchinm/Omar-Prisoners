// Opaque, vertex lit (PS1 Gouraud), fog, vertex snapping, affine UVs, anomalies. See PsxCore.cginc.
Shader "PrisonersOfOmar/PSX Lit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
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
            #define PSX_LIT 1
            #include "PsxCore.cginc"

            fixed4 frag(v2f_psx i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, PsxResolveUV(i));
                float3 c = tex.rgb * i.color.rgb;
                c = PsxApplyFog(c, i.fogAffine.x);
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
