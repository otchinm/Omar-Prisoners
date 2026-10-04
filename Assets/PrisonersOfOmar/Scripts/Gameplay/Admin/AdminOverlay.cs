using PrisonersOfOmar.Rendering;
using PrisonersOfOmar.UI;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>(iteration 2) Admin debug overlays: labels over players, Omar, the grandmother, items, traps, nav nodes,
    /// plus FPS / ping. Drawn by the HUD while <see cref="Admin.IsAdmin"/>.</summary>
    public static class AdminOverlay
    {
        static float _fps = 60f;

        public static void Draw(VhsUI ui)
        {
            if (!Admin.IsAdmin) return;
            _fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.001f), 0.05f);
            var w = MatchWorld.Instance;
            var cam = PsxRenderDriver.WorldCamera();
            if (Admin.IsOn(AdminCmd.ShowNetStats))
            {
                var s = w != null ? w.Session : null;
                float rtt = s != null ? s.GetRtt(s.IsHost ? -1 : 0) * 1000f : 0f;
                ui.Text("FPS " + Mathf.RoundToInt(_fps) + (s != null && !s.IsHost ? "  PING " + Mathf.RoundToInt(rtt) + "MS" : "  HOST"), ui.Width - 6, 4, VhsUI.Yellow, 1, Align.Right, ui.TinyFont);
            }
            if (w == null || cam == null) return;
            Vector3 eye = cam.transform.position;

            if (Admin.IsOn(AdminCmd.ShowPlayers) || Admin.IsOn(AdminCmd.ShowOmar))
                foreach (var kv in w.Avatars)
                {
                    var a = kv.Value;
                    if (a == null || a.IsLocal) continue;
                    if (a.IsOmar ? !Admin.IsOn(AdminCmd.ShowOmar) : !Admin.IsOn(AdminCmd.ShowPlayers)) continue;
                    var st = w.StatusOf(a.Id);
                    string label = (a.IsOmar ? "OMAR" : a.Info.Name) + " " + Mathf.RoundToInt(Vector3.Distance(eye, a.Position)) + "M";
                    if (st != null && !a.IsOmar && st.Life != LifeState.Free) label += " [" + st.Life.ToString().ToUpperInvariant() + "]";
                    else if (st != null && st.Hidden) label += " [HIDDEN]";
                    Label(ui, cam, a.ChestPosition + Vector3.up * 0.9f, label, a.IsOmar ? VhsUI.Red : VhsUI.White);
                }
            if (Admin.IsOn(AdminCmd.ShowGrandma) && w.Grandma != null)
                Label(ui, cam, w.Grandma.Eye + Vector3.up * 0.4f, "GRANDMOTHER " + w.Grandma.Mode.ToString().ToUpperInvariant(), VhsUI.Yellow);
            if (Admin.IsOn(AdminCmd.ShowItems))
                foreach (var it in w.Items)
                {
                    if (!it.InWorld) continue;
                    Vector3 p = it.World.transform.position;
                    if ((p - eye).sqrMagnitude > 40f * 40f) continue;
                    Label(ui, cam, p + Vector3.up * 0.25f, it.Def.Name, new Color(0.6f, 1f, 0.6f));
                }
            if (Admin.IsOn(AdminCmd.ShowTraps))
                foreach (var t in w.Traps)
                {
                    if (t.State != TrapState.Armed) continue;
                    Label(ui, cam, t.InteractPoint + Vector3.up * 0.3f, t.Kind == TrapKind.Tripwire ? "WIRE" : "TRAP", VhsUI.Red);
                }
            if (Admin.IsOn(AdminCmd.ShowNav) && w.Map != null && w.Map.Nav != null)
            {
                var nav = w.Map.Nav;
                for (int i = 0; i < nav.Nodes.Count; i++)
                {
                    if ((nav.Nodes[i] - eye).sqrMagnitude > 30f * 30f) continue;
                    Label(ui, cam, nav.Nodes[i] + Vector3.up * 0.2f, "+", new Color(0.4f, 0.8f, 1f));
                }
            }
        }

        static void Label(VhsUI ui, Camera cam, Vector3 world, string text, Color c)
        {
            Vector3 v = cam.WorldToViewportPoint(world);
            if (v.z <= 0.1f || v.x < -0.1f || v.x > 1.1f || v.y < -0.1f || v.y > 1.1f) return;
            ui.Text(text, v.x * ui.Width, (1f - v.y) * ui.Height, c, 1, Align.Center, ui.TinyFont);
        }
    }
}
