using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>User settings persisted in PlayerPrefs.</summary>
    public static class Settings
    {
        public static float MasterVolume = 1f;
        public static float MusicVolume = 0.65f;
        public static float SfxVolume = 1f;
        public static float MouseSensitivity = 2f;
        public static bool InvertY;
        public static float FieldOfView = 70f;
        public static int InternalHeight = 240;
        public static float VhsIntensity = 1f;
        public static bool Fullscreen = true;
        public static bool VSync = true;
        public static string PlayerName = "";
        public static string LastAddress = "127.0.0.1";
        public static int Port = GameInfo.DefaultPort;
        public static int NightMinutes = 20;
        public static bool AiOmar = true;

        public static readonly int[] InternalHeights = { 180, 240, 300, 360, 480 };

        const string Prefix = "poc.";
        static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            MasterVolume = PlayerPrefs.GetFloat(Prefix + "master", MasterVolume);
            MusicVolume = PlayerPrefs.GetFloat(Prefix + "music", MusicVolume);
            SfxVolume = PlayerPrefs.GetFloat(Prefix + "sfx", SfxVolume);
            MouseSensitivity = PlayerPrefs.GetFloat(Prefix + "sens", MouseSensitivity);
            InvertY = PlayerPrefs.GetInt(Prefix + "invertY", 0) == 1;
            FieldOfView = PlayerPrefs.GetFloat(Prefix + "fov", FieldOfView);
            InternalHeight = PlayerPrefs.GetInt(Prefix + "res", InternalHeight);
            VhsIntensity = PlayerPrefs.GetFloat(Prefix + "vhs", VhsIntensity);
            Fullscreen = PlayerPrefs.GetInt(Prefix + "fullscreen", Fullscreen ? 1 : 0) == 1;
            VSync = PlayerPrefs.GetInt(Prefix + "vsync", 1) == 1;
            PlayerName = PlayerPrefs.GetString(Prefix + "name", "");
            LastAddress = PlayerPrefs.GetString(Prefix + "address", LastAddress);
            Port = PlayerPrefs.GetInt(Prefix + "port", Port);
            NightMinutes = PlayerPrefs.GetInt(Prefix + "night", NightMinutes);
            AiOmar = PlayerPrefs.GetInt(Prefix + "aiOmar", 1) == 1;
            if (string.IsNullOrWhiteSpace(PlayerName)) PlayerName = "PRISONER" + Random.Range(10, 99);
            Sanitize();
        }

        public static void Save()
        {
            Sanitize();
            PlayerPrefs.SetFloat(Prefix + "master", MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "music", MusicVolume);
            PlayerPrefs.SetFloat(Prefix + "sfx", SfxVolume);
            PlayerPrefs.SetFloat(Prefix + "sens", MouseSensitivity);
            PlayerPrefs.SetInt(Prefix + "invertY", InvertY ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "fov", FieldOfView);
            PlayerPrefs.SetInt(Prefix + "res", InternalHeight);
            PlayerPrefs.SetFloat(Prefix + "vhs", VhsIntensity);
            PlayerPrefs.SetInt(Prefix + "fullscreen", Fullscreen ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "vsync", VSync ? 1 : 0);
            PlayerPrefs.SetString(Prefix + "name", PlayerName);
            PlayerPrefs.SetString(Prefix + "address", LastAddress);
            PlayerPrefs.SetInt(Prefix + "port", Port);
            PlayerPrefs.SetInt(Prefix + "night", NightMinutes);
            PlayerPrefs.SetInt(Prefix + "aiOmar", AiOmar ? 1 : 0);
            PlayerPrefs.Save();
        }

        static void Sanitize()
        {
            MasterVolume = Mathf.Clamp01(MasterVolume);
            MusicVolume = Mathf.Clamp01(MusicVolume);
            SfxVolume = Mathf.Clamp01(SfxVolume);
            MouseSensitivity = Mathf.Clamp(MouseSensitivity, 0.2f, 8f);
            FieldOfView = Mathf.Clamp(FieldOfView, 55f, 95f);
            VhsIntensity = Mathf.Clamp(VhsIntensity, 0.25f, 1.5f);
            NightMinutes = Mathf.Clamp(NightMinutes, 10, 40);
            Port = Mathf.Clamp(Port, 1024, 65535);
            if (System.Array.IndexOf(InternalHeights, InternalHeight) < 0) InternalHeight = 240;
            PlayerName = CleanName(PlayerName);
        }

        public static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "PRISONER";
            var sb = new System.Text.StringBuilder();
            foreach (char c in name.ToUpperInvariant())
            {
                if (sb.Length >= 14) break;
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == ' ' || c == '.') sb.Append(c);
            }
            string s = sb.ToString().Trim();
            return s.Length == 0 ? "PRISONER" : s;
        }

        /// <summary>Applies display / quality settings (not audio: AudioManager reads volumes every frame).</summary>
        public static void ApplyDisplay()
        {
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : 120;
            if (Screen.fullScreen != Fullscreen)
            {
                if (Fullscreen) Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
                else Screen.SetResolution(Mathf.Max(960, Display.main.systemWidth * 2 / 3), Mathf.Max(540, Display.main.systemHeight * 2 / 3), FullScreenMode.Windowed);
            }
            var rig = Rendering.PsxCameraRig.Instance;
            if (rig != null) rig.InternalHeight = InternalHeight;
            Rendering.VhsEffect.UserIntensity = VhsIntensity;
        }
    }
}
