using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace PreviewHarness
{
    /// <summary>Writes every visible renderer of the scene (world space) as JSON for render.py.</summary>
    public static class Dump
    {
        public static void Scene(string path, IEnumerable<GameObject> roots = null, List<(string, Vector3, Quaternion)> markers = null,
            Matrix4x4? view = null)
        {
            var w = new SceneWriter();
            Matrix4x4 V = view ?? Matrix4x4.identity;
            w.Add(roots, V);
            if (markers != null) foreach (var m in markers) w.Marker(m.Item1, m.Item2, m.Item3, V);
            w.Save(path);
        }
    }

    /// <summary>Accumulates meshes (several frames / objects, each with an extra transform) into one JSON scene.</summary>
    public sealed class SceneWriter
    {
        static string F(float f) => f.ToString("0.#####", CultureInfo.InvariantCulture);
        readonly StringBuilder sb = new StringBuilder();
        readonly StringBuilder mk = new StringBuilder();
        bool first = true, firstMk = true;

        public SceneWriter() { sb.Append("{\"meshes\":["); }

        public void Save(string path) => File.WriteAllText(path, sb + "],\"markers\":[" + mk + "]}");

        public void Marker(string name, Vector3 p, Quaternion r, Matrix4x4 V)
        {
            p = V.MultiplyPoint3x4(p);
            Vector3 fx = V.MultiplyVector(r * Vector3.right), fy = V.MultiplyVector(r * Vector3.up), fz = V.MultiplyVector(r * Vector3.forward);
            if (!firstMk) mk.Append(',');
            firstMk = false;
            mk.Append("{\"name\":\"").Append(name).Append("\",\"p\":[").Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z))
                .Append("],\"x\":[").Append(F(fx.x)).Append(',').Append(F(fx.y)).Append(',').Append(F(fx.z))
                .Append("],\"y\":[").Append(F(fy.x)).Append(',').Append(F(fy.y)).Append(',').Append(F(fy.z))
                .Append("],\"z\":[").Append(F(fz.x)).Append(',').Append(F(fz.y)).Append(',').Append(F(fz.z)).Append("]}");
        }

        public void Add(GameObject root, Matrix4x4 V) => Add(new[] { root }, V);

        public void Add(IEnumerable<GameObject> roots, Matrix4x4 V)
        {
            var list = new List<GameObject>();
            if (roots == null) list.AddRange(Runtime.All);
            else foreach (var r in roots) Collect(r.transform, list);
            foreach (var go in list)
            {
                if (!go.activeInHierarchy) continue;
                Mesh mesh = null;
                Renderer rend = null;
                Matrix4x4 m = Matrix4x4.identity;
                var smr = go.GetComponent<SkinnedMeshRenderer>();
                if (smr != null && smr.enabled && smr.sharedMesh != null)
                {
                    mesh = new Mesh();
                    smr.BakeMesh(mesh);
                    rend = smr;
                    m = go.transform.localToWorldMatrix;
                }
                else
                {
                    var mr = go.GetComponent<MeshRenderer>();
                    var mf = go.GetComponent<MeshFilter>();
                    if (mr != null && mr.enabled && mf != null && mf.sharedMesh != null)
                    {
                        mesh = mf.sharedMesh; rend = mr; m = go.transform.localToWorldMatrix;
                    }
                }
                if (mesh == null) continue;
                m = V * m;
                if (!first) sb.Append(',');
                first = false;
                var v = mesh.vertices; var n = mesh.normals; var uv = mesh.uv;
                sb.Append("{\"name\":\"").Append(go.name).Append("\",\"layer\":").Append(go.layer).Append(",\"v\":[");
                for (int i = 0; i < v.Length; i++)
                {
                    var p = m.MultiplyPoint3x4(v[i]);
                    if (i > 0) sb.Append(',');
                    sb.Append(F(p.x)).Append(',').Append(F(p.y)).Append(',').Append(F(p.z));
                }
                sb.Append("],\"n\":[");
                for (int i = 0; i < v.Length; i++)
                {
                    var q = i < n.Length ? m.MultiplyVector(n[i]).normalized : Vector3.up;
                    if (i > 0) sb.Append(',');
                    sb.Append(F(q.x)).Append(',').Append(F(q.y)).Append(',').Append(F(q.z));
                }
                sb.Append("],\"uv\":[");
                for (int i = 0; i < v.Length; i++)
                {
                    var q = i < uv.Length ? uv[i] : Vector2.zero;
                    if (i > 0) sb.Append(',');
                    sb.Append(F(q.x)).Append(',').Append(F(q.y));
                }
                sb.Append("],\"subs\":[");
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    var mat = s < rend.sharedMaterials.Length ? rend.sharedMaterials[s] : null;
                    string tex = mat != null && mat.mainTexture != null ? mat.mainTexture.name : "white";
                    string surf = mat != null ? mat.name.Substring(mat.name.LastIndexOf('_') + 1) : "Lit";
                    Color c = mat != null ? mat.color : Color.white;
                    if (s > 0) sb.Append(',');
                    sb.Append("{\"tex\":\"").Append(tex).Append("\",\"surface\":\"").Append(surf).Append("\",\"tint\":[")
                        .Append(F(c.r)).Append(',').Append(F(c.g)).Append(',').Append(F(c.b)).Append(',').Append(F(c.a)).Append("],\"t\":[");
                    var t = mesh.GetTriangles(s);
                    for (int i = 0; i < t.Length; i++) { if (i > 0) sb.Append(','); sb.Append(t[i]); }
                    sb.Append("]}");
                }
                sb.Append("]}");
            }
        }

        static void Collect(Transform t, List<GameObject> l)
        {
            l.Add(t.gameObject);
            for (int i = 0; i < t.childCount; i++) Collect(t.GetChild(i), l);
        }
    }
}
