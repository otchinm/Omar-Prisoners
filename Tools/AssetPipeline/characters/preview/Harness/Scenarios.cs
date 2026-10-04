using System;
using System.Collections.Generic;
using System.IO;
using PrisonersOfOmar;
using PrisonersOfOmar.Characters;
using UnityEngine;

namespace PreviewHarness
{
    /// <summary>Additional preview scenarios (animation strips, items, first person arms).</summary>
    public static partial class Scenarios
    {
        public static bool Run(string name, List<string> args, string outDir)
        {
            switch (name)
            {
                case "anim": Anim(args, outDir); return true;
                case "allanims": AllAnims(args, outDir); return true;
                case "items": Items(outDir); return true;
                case "fparms": FpArms(args, outDir); return true;
                case "perf": Perf(); return true;
                case "grandma": Grandma(outDir); return true;
            }
            return false;
        }

        /// <summary>
        /// anim SKIN MODE [frames] [span] [view]
        /// MODE: idle | walk | run | sprint | crouch | crouchwalk | injured | back | strafe | air
        ///       | hold:Lighter | action:Attack | pose:CagedSit  (combine with '+', e.g. walk+hold:Flashlight)
        /// view: side (frames laid out along Z) or front (along -X)
        /// </summary>
        static void Anim(List<string> a, string outDir)
        {
            var skin = (CharacterSkin)Enum.Parse(typeof(CharacterSkin), a[0]);
            string mode = a.Count > 1 ? a[1] : "idle";
            int frames = a.Count > 2 ? int.Parse(a[2]) : 8;
            float span = a.Count > 3 ? float.Parse(a[3], System.Globalization.CultureInfo.InvariantCulture) : 1f;
            string view = a.Count > 4 ? a[4] : "side";
            string file = a.Count > 5 ? a[5] : "anim.json";
            var w = new SceneWriter();
            RunAnim(skin, mode, frames, span, view, w, 0f, true);
            w.Save(Path.Combine(outDir, file));
        }

        static void AllAnims(List<string> a, string outDir)
        {
            // one row per mode (front view), several frames each
            var skin = (CharacterSkin)Enum.Parse(typeof(CharacterSkin), a[0]);
            int frames = a.Count > 2 ? int.Parse(a[2]) : 6;
            var modes = a[1].Split(',');
            int row = 0;
            foreach (var m in modes)
            {
                var w = new SceneWriter();
                RunAnim(skin, m, frames, -1f, a.Count > 3 ? a[3] : "front", w, 0f, false);
                w.Save(Path.Combine(outDir, "row_" + row.ToString("00") + "_" + m.Replace(':', '-').Replace('+', '_') + ".json"));
                row++;
            }
        }

        static void RunAnim(CharacterSkin skin, string mode, int frames, float span, string view, SceneWriter w, float rowOffset, bool log)
        {
            foreach (var go in Runtime.All.ToArray()) UnityEngine.Object.Destroy(go);
            var holder = new GameObject("Char");
            var rig = HumanoidFactory.Build(skin, holder.transform);
            var an = rig.GetComponent<HumanoidAnimator>();
            Vector3 vel = Vector3.zero;
            CharacterAction action = CharacterAction.None;
            GameObject held = null;
            foreach (var part in mode.Split('+'))
            {
                float walk = an.WalkSpeed, run = an.RunSpeed;
                if (skin == CharacterSkin.Omar) { walk = 1.8f; run = 4.8f; }
                switch (part)
                {
                    case "idle": break;
                    case "walk": vel = new Vector3(0, 0, walk); break;
                    case "slow": vel = new Vector3(0, 0, walk * 0.5f); break;
                    case "run": vel = new Vector3(0, 0, run); break;
                    case "sprint": vel = new Vector3(0, 0, run); an.Sprinting = true; break;
                    case "crouch": an.Crouching = true; break;
                    case "crouchwalk": an.Crouching = true; vel = new Vector3(0, 0, 1.2f); break;
                    case "injured": an.Injured = true; vel = new Vector3(0, 0, walk * 0.7f); break;
                    case "back": vel = new Vector3(0, 0, -walk * 0.7f); break;
                    case "strafe": vel = new Vector3(walk * 0.7f, 0, 0); break;
                    case "air": an.Grounded = false; break;
                    case "lookdown": an.LookPitch = 45f; break;
                    case "lookup": an.LookPitch = -45f; break;
                    case "duck": an.Duck = 1f; break;
                    case "duckwalk": an.Duck = 1f; vel = new Vector3(0, 0, walk); break;
                    default:
                        if (part.StartsWith("hold:"))
                        {
                            an.Hold = (HoldPose)Enum.Parse(typeof(HoldPose), part.Substring(5));
                            held = HeldModelFor(an.Hold);
                            if (held != null) held.transform.SetParent(rig.RightHandSocket, false);
                        }
                        else if (part.StartsWith("action:")) action = (CharacterAction)Enum.Parse(typeof(CharacterAction), part.Substring(7));
                        else if (part.StartsWith("pose:")) an.Pose = (CharacterPose)Enum.Parse(typeof(CharacterPose), part.Substring(5));
                        else throw new ArgumentException("unknown mode " + part);
                        break;
                }
            }
            // Omar gets his cleaver from HumanoidFactory.Build
            an.Velocity = vel;
            const float dt = 1f / 60f;
            var steps = new List<string>();
            var footHist = new List<(float t, Vector3 l, Vector3 r)>();
            float t = 0f;
            an.Footstep += f => steps.Add($"t={t:F3} foot={f}");
            an.ActionImpact += act => { if (log) Console.WriteLine($"  impact {act} at t={t:F3}"); };
            // warm up
            for (int i = 0; i < 90; i++) { Step(holder, vel, dt); t += dt; }
            steps.Clear();
            if (action != CharacterAction.None) an.Play(action);
            float total = span > 0 ? span : (action != CharacterAction.None ? HumanoidAnimator.DurationOf(action) : 1.2f);
            int perFrame = Mathf.Max(1, Mathf.RoundToInt(total / dt / Mathf.Max(1, frames - 1)));
            float spacing = 1.1f;
            for (int f = 0; f < frames; f++)
            {
                Vector3 off = view == "side" ? new Vector3(0, 0, f * spacing) : new Vector3(-f * spacing * 0.85f, 0, 0);
                off += view == "side" ? new Vector3(0, -rowOffset, 0) : new Vector3(0, -rowOffset, 0);
                Matrix4x4 V = Matrix4x4.Translate(off - new Vector3(holder.transform.position.x, 0, holder.transform.position.z));
                w.Add(holder, V);
                if (f < frames - 1)
                    for (int i = 0; i < perFrame; i++)
                    {
                        Step(holder, vel, dt); t += dt;
                        footHist.Add((t, rig.LeftFoot.position, rig.RightFoot.position));
                    }
            }
            if (log)
            {
                Console.WriteLine($"{skin} {mode}: {steps.Count} footsteps: " + string.Join(", ", steps));
                // planted foot drift: for each footstep, how far the foot moves in the next 0.2 s (world space)
                foreach (var st in steps)
                {
                    var parts = st.Split(' ');
                    float ts = float.Parse(parts[0].Substring(2), System.Globalization.CultureInfo.InvariantCulture);
                    int foot = parts[1].EndsWith("0") ? 0 : 1;
                    Vector3? start = null; float maxd = 0;
                    foreach (var h in footHist)
                    {
                        if (h.t < ts + 0.04f || h.t > ts + 0.24f) continue;
                        Vector3 p = foot == 0 ? h.l : h.r;
                        if (start == null) start = p;
                        maxd = Mathf.Max(maxd, Vector3.Distance(p, start.Value));
                    }
                    Console.WriteLine($"   foot {foot} at {ts:F2}: drift over next 0.2s = {maxd * 100:F1} cm");
                }
            }
        }

        static GameObject HeldModelFor(HoldPose h)
        {
            switch (h)
            {
                case HoldPose.Lighter: return ItemMeshFactory.Build(ItemType.Lighter);
                case HoldPose.Flashlight: return ItemMeshFactory.Build(ItemType.Flashlight);
                case HoldPose.OneHandSmall: return ItemMeshFactory.Build(ItemType.CarKeys);
                case HoldPose.TwoHanded: return ItemMeshFactory.Build(ItemType.GasCan);
                case HoldPose.Bottle: return ItemMeshFactory.Build(ItemType.Bottle);
                case HoldPose.Cleaver: return ItemMeshFactory.BuildCleaver();
            }
            return null;
        }

        static void Step(GameObject holder, Vector3 vel, float dt)
        {
            holder.transform.position += vel * dt;
            Runtime.Tick(dt);
        }

        static void Items(string outDir)
        {
            var w = new SceneWriter();
            int i = 0;
            foreach (ItemType it in Enum.GetValues(typeof(ItemType)))
            {
                if (it == ItemType.None) continue;
                var go = ItemMeshFactory.Build(it);
                go.transform.localRotation = ItemMeshFactory.RestRotation(it);
                go.transform.localPosition = ItemMeshFactory.RestOffset(it);
                float minY = 9, maxY = -9; Vector3 mn = Vector3.one * 9, mx = -Vector3.one * 9; int tris = 0;
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                {
                    var m = mf.transform.localToWorldMatrix;
                    foreach (var v in mf.sharedMesh.vertices) { var p = m.MultiplyPoint3x4(v); mn = Vector3.Min(mn, p); mx = Vector3.Max(mx, p); }
                    tris += mf.sharedMesh.triangles.Length / 3;
                }
                Console.WriteLine($"{it,-12} rest minY {mn.y * 100:F2} cm  size {(mx - mn) * 100} cm  center xz ({(mn.x + mx.x) * 50:F1},{(mn.z + mx.z) * 50:F1}) tris {tris}");
                w.Add(go, Matrix4x4.Translate(new Vector3((i % 6) * 0.6f, 0, -(i / 6) * 0.6f)));
                i++;
            }
            foreach (var extra in new[] { ItemMeshFactory.BuildCleaver(), ItemMeshFactory.BuildTripwireStake(), ItemMeshFactory.BuildBearTrap() })
            {
                w.Add(extra, Matrix4x4.Translate(new Vector3((i % 6) * 0.45f, extra.name == "Cleaver" ? 0.05f : 0f, -(i / 6) * 0.45f)));
                i++;
            }
            w.Save(Path.Combine(outDir, "items.json"));
        }

        /// <summary>fparms SKIN ITEM [action] [t01]: first person view model in camera space.</summary>
        static void FpArms(List<string> a, string outDir)
        {
            var skin = (CharacterSkin)Enum.Parse(typeof(CharacterSkin), a[0]);
            var item = a.Count > 1 ? (ItemType)Enum.Parse(typeof(ItemType), a[1]) : ItemType.Lighter;
            var cam = new GameObject("Camera");
            var arms = FirstPersonArms.Create(skin, cam.transform);
            arms.SetHeld(item);
            if (item == ItemType.Lighter) ItemMeshFactory.SetLighterLid(arms.HeldModel, 1f); // shown lit / open
            const float dt = 1f / 60f;
            for (int i = 0; i < 60; i++) Runtime.Tick(dt);
            if (a.Count > 2 && a[2] != "none")
            {
                var act = (CharacterAction)Enum.Parse(typeof(CharacterAction), a[2]);
                float t01 = a.Count > 3 ? float.Parse(a[3], System.Globalization.CultureInfo.InvariantCulture) : 0.5f;
                arms.Play(act);
                int n = Mathf.RoundToInt(HumanoidAnimator.DurationOf(act) * t01 / dt);
                for (int i = 0; i < n; i++) Runtime.Tick(dt);
            }
            var w = new SceneWriter();
            w.Add(arms.gameObject, cam.transform.worldToLocalMatrix);
            w.Save(Path.Combine(outDir, a.Count > 4 ? a[4] : "fparms.json"));
        }

        /// <summary>grandma: the grandmother in every mode side by side.</summary>
        static void Grandma(string outDir)
        {
            var w = new SceneWriter();
            var modes = new[] { GrandmaMode.WatchingTv, GrandmaMode.Roaming, GrandmaMode.Screaming, GrandmaMode.Dead };
            for (int i = 0; i < modes.Length; i++)
            {
                var rig = GrandmaRig.Create(null);
                rig.SetMode(modes[i]);
                rig.SetMoveSpeed(modes[i] == GrandmaMode.Roaming ? 1f : 0f);
                for (int f = 0; f < 50; f++) Runtime.Tick(1f / 30f);
                w.Add(rig.gameObject, Matrix4x4.Translate(new Vector3((i - 1.5f) * 0.9f, 0, 0)));
            }
            w.Save(Path.Combine(outDir, "grandma.json"));
        }

        static void Perf()
        {
            var rigs = new List<HumanoidAnimator>();
            for (int i = 0; i < 5; i++)
            {
                var r = HumanoidFactory.Build((CharacterSkin)i, null);
                var an = r.GetComponent<HumanoidAnimator>();
                an.Velocity = new Vector3(0, 0, 2f + i * 0.5f);
                rigs.Add(an);
            }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int f = 0; f < 600; f++) Runtime.Tick(1f / 60f);
            long after = GC.GetAllocatedBytesForCurrentThread();
            Console.WriteLine($"600 frames x 5 characters: {sw.ElapsedMilliseconds} ms (shim, includes shim overhead), alloc {(after - before) / 1024} KB");
        }
    }
}
