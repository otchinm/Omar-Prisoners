using System;
using System.Collections.Generic;
using System.IO;
using PrisonersOfOmar;
using PrisonersOfOmar.Characters;
using UnityEngine;

namespace PreviewHarness
{
    public static class Program
    {
        static string Out;

        public static int Main(string[] args)
        {
            Out = args[0];
            Directory.CreateDirectory(Out);
            string scenario = args.Length > 1 ? args[1] : "lineup";
            var rest = new List<string>();
            for (int i = 2; i < args.Length; i++) rest.Add(args[i]);
            switch (scenario)
            {
                case "lineup": Lineup(false); break;
                case "idle": Lineup(true); break;
                case "figures": Figures(rest.Count > 0 ? rest[0] : null, rest.Count > 1 ? int.Parse(rest[1]) : 17); break;
                case "stats": Stats(); break;
                case "single":
                {
                    var skin = (CharacterSkin)Enum.Parse(typeof(CharacterSkin), rest[0]);
                    var rig = HumanoidFactory.Build(skin, null);
                    rig.GetComponent<HumanoidAnimator>().enabled = false;
                    var v = rig.BodyRenderer.sharedMesh.vertices;
                    float maxY = -1, minY = 9;
                    var uvs = rig.BodyRenderer.sharedMesh.uv;
                    for (int i = 0; i < v.Length; i++)
                    {
                        var p = v[i];
                        if (p.y > rig.Height + 0.03f) Console.WriteLine($"  high vertex {i} {p} uv {uvs[i]}");
                        maxY = Math.Max(maxY, p.y); minY = Math.Min(minY, p.y);
                    }
                    Console.WriteLine($"mesh y {minY:F3}..{maxY:F3} height {rig.Height}");
                    Dump.Scene(Path.Combine(Out, "single.json"), new[] { rig.gameObject });
                    break;
                }
                default:
                    if (!Scenarios.Run(scenario, rest, Out)) { Console.Error.WriteLine("unknown scenario " + scenario); return 2; }
                    break;
            }
            return 0;
        }

        public static readonly CharacterSkin[] Skins =
            { CharacterSkin.Prisoner1, CharacterSkin.Prisoner2, CharacterSkin.Prisoner3, CharacterSkin.Prisoner4,
              CharacterSkin.Prisoner5, CharacterSkin.Prisoner6, CharacterSkin.Prisoner7, CharacterSkin.Omar };

        static void Lineup(bool animate)
        {
            var root = new GameObject("Lineup");
            var rigs = new List<HumanoidRig>();
            for (int i = 0; i < Skins.Length; i++)
            {
                var holder = new GameObject("Slot" + i);
                holder.transform.SetParent(root.transform, false);
                holder.transform.localPosition = new Vector3((i - (Skins.Length - 1) * 0.5f) * 0.72f, 0, 0);
                var rig = HumanoidFactory.Build(Skins[i], holder.transform);
                rigs.Add(rig);
                if (!animate) rig.GetComponent<HumanoidAnimator>().enabled = false;
            }
            if (animate) for (int f = 0; f < 60; f++) Runtime.Tick(1f / 30f);
            var markers = new List<(string, Vector3, Quaternion)>();
            foreach (var r in rigs)
            {
                markers.Add(("RS", r.RightHandSocket.position, r.RightHandSocket.rotation));
                markers.Add(("LS", r.LeftHandSocket.position, r.LeftHandSocket.rotation));
                markers.Add(("Eye", r.EyePoint.position, r.EyePoint.rotation));
            }
            Dump.Scene(Path.Combine(Out, animate ? "idle.json" : "lineup.json"), new[] { root }, markers);
        }

        static void Figures(string only, int seed)
        {
            var root = new GameObject("Figures");
            int k = 0;
            foreach (FigureKind kind in Enum.GetValues(typeof(FigureKind)))
            {
                if (only != null && kind.ToString() != only) { k++; continue; }
                int p = 0;
                foreach (FigurePose pose in Enum.GetValues(typeof(FigurePose)))
                {
                    var holder = new GameObject("F");
                    holder.transform.SetParent(root.transform, false);
                    float x = -p * 1.5f;
                    holder.transform.localPosition = new Vector3(x, pose == FigurePose.Hanging ? HumanoidFactory.HangingDropHeight + 0.2f : 0f, only != null ? 0f : k * 2.2f);
                    if (pose == FigurePose.LyingOnBack) holder.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    HumanoidFactory.BuildFigure(kind, pose, holder.transform, seed + k * 5 + p);
                    p++;
                }
                k++;
            }
            Dump.Scene(Path.Combine(Out, "figures.json"), new[] { root });
        }

        static void Stats()
        {
            foreach (var s in Skins)
            {
                var rig = HumanoidFactory.Build(s, null);
                var m = rig.BodyRenderer.sharedMesh;
                int tris = 0;
                for (int i = 0; i < m.subMeshCount; i++) tris += m.GetTriangles(i).Length / 3;
                Console.WriteLine($"{s}: verts {m.vertexCount} tris {tris} subs {m.subMeshCount} height {rig.Height:F2} eye {rig.EyePoint.position.y:F2}");
            }
        }
    }
}
