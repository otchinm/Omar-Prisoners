using UnityEditor;
using UnityEngine;

namespace PrisonersOfOmar.EditorTools
{
    /// <summary>
    /// Import rules for the game's generated assets: textures are point filtered, uncompressed, no mip maps
    /// (crunchy PS1 look); audio is Vorbis, music streamed, short sounds decompressed on load.
    /// </summary>
    public sealed class ImportSettings : AssetPostprocessor
    {
        const string Root = "Assets/PrisonersOfOmar/Resources/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.filterMode = FilterMode.Point;
            ti.mipmapEnabled = false;
            ti.anisoLevel = 0;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.sRGBTexture = true;
            ti.maxTextureSize = 2048;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            bool clamp = assetPath.Contains("/UI/") || assetPath.Contains("/UIFallback/") || assetPath.Contains("/FX/") && !assetPath.Contains("noise") && !assetPath.Contains("scratches")
                         || assetPath.Contains("/Decals/") || assetPath.Contains("/Characters/") || assetPath.Contains("/Items/");
            bool sky = assetPath.Contains("/Sky/");
            ti.wrapModeU = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            ti.wrapModeV = clamp || sky ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            bool alpha = assetPath.Contains("/UI/") || assetPath.Contains("/UIFallback/") || assetPath.Contains("/FX/") || assetPath.Contains("/Decals/")
                         || assetPath.Contains("/Foliage/") || assetPath.Contains("chainlink") || assetPath.Contains("barbed") || assetPath.Contains("cage_bars")
                         || assetPath.Contains("porch_screen");
            ti.alphaIsTransparency = alpha;
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Root)) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            bool music = assetPath.Contains("/Music/") || assetPath.Contains("screams_long");
            bool loop = assetPath.Contains("_loop");
            s.loadType = music ? AudioClipLoadType.Streaming : loop ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.7f : 0.55f;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            ai.defaultSampleSettings = s;
            ai.forceToMono = false;
            ai.loadInBackground = music;
        }
    }
}
