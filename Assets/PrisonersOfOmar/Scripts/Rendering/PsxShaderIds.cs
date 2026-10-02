using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>Cached shader property ids (global uniforms documented in Resources/Shaders/PsxCore.cginc and VHS.shader).</summary>
    internal static class PsxShaderIds
    {
        // ---- world shaders (globals)
        public static readonly int SnapRes = Shader.PropertyToID("_PsxSnapRes");
        public static readonly int Affine = Shader.PropertyToID("_PsxAffine");
        public static readonly int Ambient = Shader.PropertyToID("_PsxAmbient");
        public static readonly int FogColor = Shader.PropertyToID("_PsxFogColor");
        public static readonly int FogParams = Shader.PropertyToID("_PsxFogParams");
        public static readonly int LightCount = Shader.PropertyToID("_PsxLightCount");
        public static readonly int LightPos = Shader.PropertyToID("_PsxLightPos");
        public static readonly int LightColor = Shader.PropertyToID("_PsxLightColor");
        public static readonly int LightDir = Shader.PropertyToID("_PsxLightDir");
        public static readonly int Time = Shader.PropertyToID("_PsxTime");
        public static readonly int Anomaly = Shader.PropertyToID("_PsxAnomaly");
        public static readonly int AnomalyZoneCount = Shader.PropertyToID("_PsxAnomalyZoneCount");
        public static readonly int AnomalyZones = Shader.PropertyToID("_PsxAnomalyZones");
        public static readonly int AnomalyZoneParams = Shader.PropertyToID("_PsxAnomalyZoneParams");

        // ---- material
        public static readonly int MainTex = Shader.PropertyToID("_MainTex");
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int Cutoff = Shader.PropertyToID("_Cutoff");
        public static readonly int EmissionBoost = Shader.PropertyToID("_EmissionBoost");

        // ---- VHS
        public static readonly int VhsScreen = Shader.PropertyToID("_VhsScreen");
        public static readonly int VhsTime = Shader.PropertyToID("_VhsTime");
        public static readonly int VhsTape = Shader.PropertyToID("_VhsTape");
        public static readonly int VhsTape2 = Shader.PropertyToID("_VhsTape2");
        public static readonly int VhsGrade = Shader.PropertyToID("_VhsGrade");
        public static readonly int VhsTint = Shader.PropertyToID("_VhsTint");
        public static readonly int VhsShadow = Shader.PropertyToID("_VhsShadow");
        public static readonly int VhsFx = Shader.PropertyToID("_VhsFx");
        public static readonly int VhsFx2 = Shader.PropertyToID("_VhsFx2");
        public static readonly int VhsLens = Shader.PropertyToID("_VhsLens");
    }
}
