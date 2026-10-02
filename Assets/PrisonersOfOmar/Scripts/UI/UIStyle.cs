using PrisonersOfOmar.Gameplay;
using UnityEngine;

namespace PrisonersOfOmar.UI
{
    /// <summary>Shared VHS-style UI pieces: OSD, title, panels.</summary>
    public static class UIStyle
    {
        public static void Dim(VhsUI ui, float alpha) => ui.Rect(0, 0, ui.Width, ui.Height, new Color(0, 0, 0, alpha));

        public static Rect Panel(VhsUI ui, Rect r, float alpha = 0.72f)
        {
            ui.Rect(r, new Color(0.02f, 0.02f, 0.025f, alpha));
            ui.Frame(r, new Color(0.6f, 0.6f, 0.58f, 0.5f));
            return r;
        }

        /// <summary>VCR on-screen display: mode at top-left, timecode at bottom-right.</summary>
        public static void Osd(VhsUI ui, string mode, string counter, bool blinkRed = false)
        {
            bool on = (Time.unscaledTime % 1.2f) < 0.8f;
            if (!string.IsNullOrEmpty(mode))
            {
                Color c = blinkRed ? (on ? VhsUI.Red : new Color(0.5f, 0.05f, 0.05f)) : VhsUI.White;
                ui.Text(mode, 14, 10, c);
            }
            if (!string.IsNullOrEmpty(counter)) ui.Text(counter, ui.Width - 14, ui.Height - 10 - ui.LineHeight(), VhsUI.White, 1, Align.Right);
        }

        /// <summary>"AM 1:00:00" .. "AM 6:00:00" for night progress 0..1.</summary>
        public static string NightClock(float progress)
        {
            float hours = 1f + Mathf.Clamp01(progress) * 5f;
            int h = Mathf.FloorToInt(hours);
            int m = Mathf.FloorToInt((hours - h) * 60f);
            int s = Mathf.FloorToInt(((hours - h) * 60f - m) * 60f);
            return "AM " + h + ":" + m.ToString("00") + ":" + s.ToString("00");
        }

        public static string TapeCounter()
        {
            float t = Time.unscaledTime;
            int h = (int)(t / 3600f) % 10, m = (int)(t / 60f) % 60, s = (int)t % 60;
            return "SP  " + h + ":" + m.ToString("00") + ":" + s.ToString("00");
        }

        /// <summary>Game title (logo texture or big red text). Returns the bottom y.</summary>
        public static float Title(VhsUI ui, float y, float maxWidth, float maxHeight)
        {
            var logo = UITex.Get("Textures/UI/title_logo");
            if (logo != null)
            {
                var r = ui.ImageFit(logo, new Rect((ui.Width - maxWidth) * 0.5f, y, maxWidth, maxHeight), Color.white);
                return r.yMax;
            }
            int scale = maxWidth > 260 ? 2 : 1;
            var font = ui.BigFont;
            float lh = font.LineHeight * scale;
            ui.Text("THE PRISONERS", ui.Width * 0.5f, y, VhsUI.Red, scale, Align.Center, font);
            ui.Text("OF OMAR", ui.Width * 0.5f, y + lh, VhsUI.Red, scale, Align.Center, font);
            return y + lh * 2;
        }

        public static void Header(VhsUI ui, string text, float y)
        {
            ui.Text("- " + text + " -", ui.Width * 0.5f, y, VhsUI.White, 1, Align.Center, VhsFont.Small);
        }

        public static void Footer(VhsUI ui, string text)
        {
            var f = VhsFont.Tiny;
            ui.Text(text, ui.Width * 0.5f, ui.Height - f.LineHeight - 4, VhsUI.Dim, 1, Align.Center, f);
        }

        public static bool BackPressed() => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) && !Input.GetKey(KeyCode.LeftShift);

        public static string RoleName(PlayerInfo p)
        {
            if (p.Role == PlayerRole.Omar) return p.IsBot ? "OMAR (AI)" : "OMAR";
            if (p.Role == PlayerRole.Spectator) return "SPECTATOR";
            return Characters.HumanoidFactory.DisplayName(p.Skin);
        }

        /// <summary>Projects a world point into low-res UI pixels. Returns false when behind the camera.</summary>
        public static bool WorldToUI(VhsUI ui, Vector3 world, out Vector2 p)
        {
            p = Vector2.zero;
            var rig = Rendering.PsxCameraRig.Instance;
            if (rig == null || rig.WorldCamera == null) return false;
            Vector3 v = rig.WorldCamera.WorldToViewportPoint(world);
            if (v.z <= 0.05f) { p = new Vector2((1f - v.x) * ui.Width, ui.Height - 8); return false; }
            p = new Vector2(v.x * ui.Width, (1f - v.y) * ui.Height);
            return true;
        }
    }
}
