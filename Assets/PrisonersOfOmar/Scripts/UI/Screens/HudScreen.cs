using PrisonersOfOmar.Gameplay;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.UI
{
    /// <summary>
    /// In-match overlay (non modal). Puppet Combo style minimalism: VCR OSD, interaction prompt, short messages,
    /// a tiny inventory strip, the sound meter when equipped; Omar gets his mask view, pings and abilities.
    /// </summary>
    public sealed class HudScreen : UIScreen
    {
        public override bool Modal => false;
        public override bool ShowCursor => false;

        public override void Draw(VhsUI ui, bool input)
        {
            var w = MatchWorld.Instance;
            if (w == null) return;
            if (!w.Running && w.Ending == null) return;
            if (w.LocalOmar != null) DrawOmar(ui, w);
            else if (w.LocalPrisoner != null && w.Spectator == null) DrawPrisoner(ui, w);
            else DrawSpectator(ui, w);
            DrawMessages(ui, w);
            if (Admin.IsAdmin) Admin.DrawOverlay(ui);
        }

        // ------------------------------------------------------------------ prisoner

        void DrawPrisoner(VhsUI ui, MatchWorld w)
        {
            var c = w.LocalPrisoner;
            var st = w.LocalStatus;
            if (st == null) return;

            if (st.Injured && !c.PainkillersActive)
            {
                var blood = UITex.Get("Textures/UI/vignette_blood");
                float a = 0.45f + 0.2f * Mathf.Sin(Time.time * 3f);
                if (blood != null) ui.ImageCover(blood, new Color(1, 1, 1, a));
            }
            UIStyle.Osd(ui, "PLAY ▶", UIStyle.NightClock(w.NightProgress));

            // crosshair: a solid white square, easy to see
            if (!st.Hidden) DrawCrosshair(ui);

            DrawPrompt(ui, c.HasPrompt, c.Prompt, c.HoldProgress);
            if (c.UseHoldItem != ItemType.None)
            {
                float y = ui.Height * 0.5f + 34;
                ui.Text("USING " + ItemDefs.Get(c.UseHoldItem).Name + "...", ui.Width * 0.5f, y, VhsUI.White, 1, Align.Center);
                ui.Bar(new Rect(ui.Width * 0.5f - 40, y + ui.LineHeight() + 2, 80, 4), c.UseHoldProgress, VhsUI.White, new Color(0, 0, 0, 0.6f));
            }

            DrawInventoryStrip(ui, w);

            if (w.Inventory.HeldType == ItemType.SoundMeter) DrawSoundMeter(ui, c.NoiseLevel);

            DrawStamina(ui, c.Stamina);

            if (st.Life == LifeState.Caged && w.Time > Tuning.CagesOpenAt + 2f)
                ui.TextWrapped("YOU ARE LOCKED IN A CAGE. STRUGGLE (E) OR WAIT FOR SOMEONE WITH A KEY.", ui.Width * 0.5f, 28, ui.Width - 24, VhsUI.Dim, 1, Align.Center, ui.TinyFont);
            if (w.Objectives.IgniteAt > 0 && !w.Objectives.Exploded)
            {
                float left = Mathf.Max(0f, w.Objectives.IgniteAt - w.Time);
                ui.Text("THE FUEL WILL BLOW IN " + left.ToString("0"), ui.Width * 0.5f, 40, VhsUI.Red, 1, Align.Center);
            }
        }

        public static void DrawCrosshair(VhsUI ui)
        {
            float cx = Mathf.Round(ui.Width * 0.5f), cy = Mathf.Round(ui.Height * 0.5f);
            ui.Rect(cx - 2, cy - 2, 4, 4, new Color(0, 0, 0, 0.35f));
            ui.Rect(cx - 1, cy - 1, 3, 3, Color.white);
        }

        float _staminaAlpha;

        /// <summary>Like the reference: "STAMINA" and a long thin outlined bar along the bottom left; shows while
        /// running / recovering, fades out when full.</summary>
        void DrawStamina(VhsUI ui, float stamina)
        {
            _staminaAlpha = Mathf.MoveTowards(_staminaAlpha, stamina < 0.985f ? 1f : 0f, Time.unscaledDeltaTime * (stamina < 0.985f ? 6f : 1.2f));
            if (_staminaAlpha <= 0.01f) return;
            // raised off the bottom edge (VHS overscan eats the last lines) and a thick bar you can read at a glance
            float x = Mathf.Round(ui.Width * 0.035f), y = ui.Height - 28;
            var col = new Color(0.92f, 0.92f, 0.9f, _staminaAlpha);
            ui.Text("STAMINA", x + 1, y + 1, new Color(0f, 0f, 0f, 0.6f * _staminaAlpha));
            ui.Text("STAMINA", x, y, col);
            float bx = x + ui.TextWidth("STAMINA") + 6;
            float bw = Mathf.Round(ui.Width * 0.32f), bh = 11;
            float by = y + Mathf.Floor((ui.LineHeight() - bh) * 0.5f);
            ui.Rect(bx + 1, by + 1, bw, bh, new Color(0f, 0f, 0f, 0.4f * _staminaAlpha));   // drop shadow
            ui.Rect(bx, by, bw, bh, new Color(0f, 0f, 0f, 0.5f * _staminaAlpha));
            ui.Frame(new Rect(bx, by, bw, bh), new Color(0.8f, 0.8f, 0.78f, 0.85f * _staminaAlpha));
            float fill = (bw - 4) * Mathf.Clamp01(stamina);
            ui.Rect(bx + 2, by + 2, fill, bh - 4, new Color(0.62f, 0.62f, 0.6f, 0.9f * _staminaAlpha));
            ui.Rect(bx + 2, by + 2, fill, 2, new Color(0.78f, 0.78f, 0.75f, 0.9f * _staminaAlpha));   // lighter top line of the fill
            // the little end marker of the reference bar
            ui.Rect(bx + 2 + fill - 1, by + 1, 2, bh - 2, new Color(0.95f, 0.95f, 0.92f, _staminaAlpha));
        }

        static void DrawPrompt(VhsUI ui, bool has, InteractPrompt p, float progress)
        {
            if (!has || string.IsNullOrEmpty(p.Text)) return;
            // right under the crosshair, like the label on the thing you look at
            float y = ui.Height * 0.5f + 6;
            string key = !p.Enabled ? "" : p.IsDrag ? "[HOLD LMB] " : p.HoldTime > 0 ? "[HOLD E] " : "[E] ";
            ui.Text(key + p.Text, ui.Width * 0.5f, y, p.Enabled ? VhsUI.White : new Color(0.6f, 0.58f, 0.55f), 1, Align.Center);
            if (p.Enabled && p.HoldTime > 0 && progress > 0f)
                ui.Bar(new Rect(ui.Width * 0.5f - 40, y + ui.LineHeight() + 2, 80, 4), progress, VhsUI.White, new Color(0, 0, 0, 0.6f));
        }

        static void DrawInventoryStrip(VhsUI ui, MatchWorld w)
        {
            var inv = w.Inventory;
            float x = Mathf.Round(ui.Width * 0.035f), y = ui.Height - 58, s = 20;   // above the stamina bar
            for (int i = 0; i < inv.Capacity; i++)
            {
                var r = new Rect(x + i * (s + 4), y, s, s);
                bool sel = i == inv.Selected;
                ui.Rect(r, new Color(0, 0, 0, sel ? 0.65f : 0.4f));
                ui.Frame(r, sel ? new Color(1, 1, 1, 0.8f) : new Color(1, 1, 1, 0.22f));
                var it = inv.At(i);
                if (it == null) continue;
                var icon = UITex.Get(it.Def.Icon);
                if (icon != null) ui.Image(icon, new Rect(r.x + 2, r.y + 2, s - 4, s - 4), new Color(1, 1, 1, sel ? 1f : 0.6f));
                else ui.Text(it.Def.Name.Substring(0, 1), r.center.x, r.y + 4, VhsUI.White, 1, Align.Center);
                if (it.Def.HasCharge) ui.Rect(r.x + 1, r.yMax - 2, (s - 2) * it.Charge, 1, it.Charge < 0.2f ? VhsUI.Red : VhsUI.Yellow);
            }
            var held = inv.Held;
            if (held != null) ui.Text(held.Def.Name + (held.Def.HasCharge ? "  " + Mathf.RoundToInt(held.Charge * 100f) + "%" : ""), x, y - ui.LineHeight() - 1, new Color(1, 1, 1, 0.55f));
        }

        static void DrawSoundMeter(VhsUI ui, float level)
        {
            var r = new Rect(ui.Width - 74, ui.Height - 64, 60, 34);
            UIStyle.Panel(ui, r, 0.7f);
            ui.Text("SOUND", r.x + 4, r.y + 2, VhsUI.Dim);
            // dial: arc of ticks + needle
            Vector2 c = new Vector2(r.center.x, r.yMax - 4);
            for (int i = 0; i <= 8; i++)
            {
                float a = Mathf.Lerp(150f, 30f, i / 8f) * Mathf.Deg2Rad;
                Vector2 p = c + new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * 20f;
                ui.Rect(p.x, p.y, 1, 1, i > 5 ? VhsUI.Red : VhsUI.White);
            }
            float na = Mathf.Lerp(150f, 30f, Mathf.Clamp01(level + Random.Range(-0.02f, 0.02f))) * Mathf.Deg2Rad;
            for (int i = 0; i < 17; i++)
            {
                Vector2 p = c + new Vector2(Mathf.Cos(na), -Mathf.Sin(na)) * i;
                ui.Rect(p.x, p.y, 1, 1, level > 0.65f ? VhsUI.Red : VhsUI.Yellow);
            }
        }

        // ------------------------------------------------------------------ Omar

        void DrawOmar(VhsUI ui, MatchWorld w)
        {
            var c = w.LocalOmar;
            var mask = UITex.Get("Textures/UI/vignette_mask");
            if (mask != null) ui.ImageCover(mask, new Color(1, 1, 1, 0.92f));
            else
            {
                // fallback: dark edges
                ui.Rect(0, 0, ui.Width, 18, new Color(0, 0, 0, 0.8f));
                ui.Rect(0, ui.Height - 18, ui.Width, 18, new Color(0, 0, 0, 0.8f));
            }

            UIStyle.Osd(ui, "REC ●", UIStyle.NightClock(w.NightProgress), true);

            // pings
            foreach (var p in w.Pings)
            {
                bool vis = UIStyle.WorldToUI(ui, p.Position, out var sp);
                sp.x = Mathf.Clamp(sp.x, 8, ui.Width - 8);
                sp.y = Mathf.Clamp(sp.y, 20, ui.Height - 20);
                float life = Mathf.Clamp01((p.Expire - Time.time) / 2f);
                float dist = w.LocalAvatar != null ? Vector3.Distance(w.LocalAvatar.Position, p.Position) : 0f;
                switch (p.Kind)
                {
                    case 0: // noise ripple
                        {
                            float rr = 3f + (1f - life) * 8f;
                            var col = new Color(1f, 0.85f, 0.4f, life * 0.8f);
                            ui.Frame(new Rect(sp.x - rr, sp.y - rr, rr * 2, rr * 2), col);
                            break;
                        }
                    case 1:
                        ui.Text("!! ALARM " + Mathf.RoundToInt(dist) + "M", sp.x, sp.y - 6, (Time.time % 0.5f) < 0.3f ? VhsUI.Red : VhsUI.White, 1, Align.Center);
                        break;
                    case 2:
                        ui.Text("TRAP SPRUNG " + Mathf.RoundToInt(dist) + "M", sp.x, sp.y - 6, VhsUI.Red, 1, Align.Center);
                        break;
                    case 3:
                        ui.Text("▼", sp.x, sp.y - 6, new Color(0.9f, 0.1f, 0.1f, Mathf.Clamp01(life + 0.3f)), 1, Align.Center);
                        break;
                    case 4:
                        ui.Text("▼", sp.x, sp.y - 10, VhsUI.Red, 1, Align.Center);
                        break;
                }
            }
            // spotted prisoners: marker above their heads while the chase lasts
            foreach (var id in w.ChaseTargets)
            {
                var a = w.AvatarOf(id);
                if (a == null || !a.Visible) continue;
                if (UIStyle.WorldToUI(ui, a.EyePosition + Vector3.up * 0.4f, out var sp))
                    ui.Text("▼", sp.x, sp.y - 8, (Time.time % 0.4f) < 0.25f ? VhsUI.Red : VhsUI.DarkRed, 1, Align.Center);
            }

            DrawPrompt(ui, c.HasPrompt, c.Prompt, c.HoldProgress);

            if (c.Waking)
            {
                int wsc = ui.FitScale("YOU ARE WAKING UP...", 2, ui.Width - 16, ui.BigFont);
                ui.Text("YOU ARE WAKING UP...", ui.Width * 0.5f, ui.Height * 0.38f, VhsUI.Red, wsc, Align.Center, ui.BigFont);
                ui.Text(Mathf.CeilToInt(c.WakeRemaining).ToString(), ui.Width * 0.5f, ui.Height * 0.38f + ui.BigFont.LineHeight * wsc + 4, VhsUI.White, 1, Align.Center);
            }

            // abilities (compact font)
            ui.FontOverride = ui.TinyFont;
            int lh = ui.LineHeight();
            float x = 22, y = ui.Height - 14 - lh * 3;
            ui.Text("LMB  CLEAVER", x, y, VhsUI.Dim); y += lh;
            ui.Text("RMB  SCREAM " + (c.ScreamCooldown01 > 0 ? Bar(c.ScreamCooldown01) : "READY"), x, y, c.ScreamCooldown01 > 0 ? VhsUI.Dim : VhsUI.Red); y += lh;
            ui.Text("T WIRE x" + c.TripwireCharges + "   G TRAP x" + c.BearTrapCharges + "   Q SENSE " + (c.SenseCooldown01 > 0 ? Bar(c.SenseCooldown01) : "READY"), x, y, VhsUI.Dim);
            // flipped a bed on someone: the surge, blinking above the ability list
            if (c.Rushing && (Time.time % 0.5f) < 0.36f) ui.Text(">> SURGE: FASTER <<", x, ui.Height - 14 - lh * 4 - 2, VhsUI.Red);
            if (c.Stamina < 0.98f) ui.Rect(ui.Width * 0.5f - 30, ui.Height - 10, 60 * c.Stamina, 2, new Color(0.8f, 0.3f, 0.3f, 0.6f));

            // prisoners status (top right)
            float ry = 26;
            foreach (var p in w.Session.Players)
            {
                if (!p.IsPrisoner) continue;
                var st = w.StatusOf(p.Id);
                string s = st == null ? "?" : st.Life == LifeState.Free ? (st.Injured ? "BLEEDING" : "LOOSE") : st.Life == LifeState.Caged ? "CAGED" : st.Life == LifeState.Dead ? "DEAD" : st.Life == LifeState.Escaped ? "ESCAPED" : "GONE";
                Color col = st != null && st.Life == LifeState.Escaped ? VhsUI.Yellow : st != null && st.Life == LifeState.Free ? VhsUI.White : VhsUI.Dim;
                ui.Text(p.Name + " " + s, ui.Width - 22, ry, col, 1, Align.Right);
                ry += lh;
            }
            ui.FontOverride = null;
        }

        static string Bar(float cooldown01)
        {
            int n = Mathf.CeilToInt(cooldown01 * 5f);
            return new string('■', n) + new string('.', 5 - n);
        }

        // ------------------------------------------------------------------ spectator

        void DrawSpectator(VhsUI ui, MatchWorld w)
        {
            UIStyle.Osd(ui, "REC ●", UIStyle.NightClock(w.NightProgress), true);
            var st = w.LocalStatus;
            string head = st == null ? "SPECTATING" : st.Life == LifeState.Escaped ? "YOU ESCAPED" : st.Life == LifeState.Dead ? "YOU DIED" : "SPECTATING";
            ui.Text(head, ui.Width * 0.5f, 26, st != null && st.Life == LifeState.Dead ? VhsUI.Red : VhsUI.White, 1, Align.Center);
            if (w.Spectator != null && w.Spectator.Target != null)
                ui.Text("WATCHING: " + w.Spectator.TargetName + "   [LMB / RMB] SWITCH", ui.Width * 0.5f, ui.Height - 24, VhsUI.Dim, 1, Align.Center);
            var ui2 = UIManager.Instance;
            if (ui2 != null && !ui2.AnyModal && GameInput.PauseToggle) ui2.Push(new PauseScreen());
            if (ui2 != null) GameInput.SetCursorLocked(!ui2.AnyModal);
        }

        static void DrawMessages(VhsUI ui, MatchWorld w)
        {
            float y = ui.Height * 0.68f;
            for (int i = 0; i < w.Messages.Count; i++)
            {
                var m = w.Messages[i];
                float left = m.Value - Time.time;
                float a = Mathf.Clamp01(left * 2f);
                var lines = ui.Wrap(m.Key, ui.Width - 60);
                foreach (var l in lines)
                {
                    ui.Text(l, ui.Width * 0.5f, y, new Color(0.95f, 0.93f, 0.85f, a), 1, Align.Center);
                    y += ui.LineHeight();
                }
                y += 2;
            }
        }
    }
}
