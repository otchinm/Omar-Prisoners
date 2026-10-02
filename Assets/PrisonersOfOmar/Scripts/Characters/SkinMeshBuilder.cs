using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrisonersOfOmar.Characters
{
    /// <summary>Bone indices of the procedural skeleton (also the SkinnedMeshRenderer.bones order).</summary>
    internal enum BoneId
    {
        Hips = 0, Spine, Chest, Neck, Head,
        LUpperArm, LLowerArm, LHand,
        RUpperArm, RLowerArm, RHand,
        LUpperLeg, LLowerLeg, LFoot,
        RUpperLeg, RLowerLeg, RFoot,
    }

    /// <summary>Up to 4 bone influences.</summary>
    internal struct SkinWeight
    {
        public int B0, B1, B2, B3;
        public float W0, W1, W2, W3;

        public static SkinWeight One(BoneId b) => new SkinWeight { B0 = (int)b, W0 = 1f };

        /// <summary>a * (1 - t) + b * t</summary>
        public static SkinWeight Two(BoneId a, BoneId b, float t)
        {
            var w = One(a);
            w.W0 = 1f - t;
            w.B1 = (int)b; w.W1 = t;
            return w.Normalized();
        }

        public SkinWeight Plus(BoneId b, float weight)
        {
            if (weight <= 0f) return this;
            // scale existing down so the new influence gets `weight`
            var w = this;
            float k = 1f - weight;
            w.W0 *= k; w.W1 *= k; w.W2 *= k; w.W3 *= k;
            w.Add((int)b, weight);
            return w.Normalized();
        }

        void Add(int bone, float weight)
        {
            if (W0 > 0 && B0 == bone) { W0 += weight; return; }
            if (W1 > 0 && B1 == bone) { W1 += weight; return; }
            if (W2 > 0 && B2 == bone) { W2 += weight; return; }
            if (W3 > 0 && B3 == bone) { W3 += weight; return; }
            if (W0 <= 0) { B0 = bone; W0 = weight; return; }
            if (W1 <= 0) { B1 = bone; W1 = weight; return; }
            if (W2 <= 0) { B2 = bone; W2 = weight; return; }
            if (W3 <= 0) { B3 = bone; W3 = weight; return; }
            // replace the smallest
            float m = Mathf.Min(Mathf.Min(W0, W1), Mathf.Min(W2, W3));
            if (weight <= m) return;
            if (W0 == m) { B0 = bone; W0 = weight; }
            else if (W1 == m) { B1 = bone; W1 = weight; }
            else if (W2 == m) { B2 = bone; W2 = weight; }
            else { B3 = bone; W3 = weight; }
        }

        public static SkinWeight Lerp(SkinWeight a, SkinWeight b, float t)
        {
            var r = new SkinWeight();
            r.AddScaled(a, 1f - t);
            r.AddScaled(b, t);
            return r.Normalized();
        }

        void AddScaled(SkinWeight o, float k)
        {
            if (k <= 0f) return;
            if (o.W0 > 0) Add(o.B0, o.W0 * k);
            if (o.W1 > 0) Add(o.B1, o.W1 * k);
            if (o.W2 > 0) Add(o.B2, o.W2 * k);
            if (o.W3 > 0) Add(o.B3, o.W3 * k);
        }

        public SkinWeight Normalized()
        {
            var w = this;
            if (w.W0 < 0) w.W0 = 0; if (w.W1 < 0) w.W1 = 0; if (w.W2 < 0) w.W2 = 0; if (w.W3 < 0) w.W3 = 0;
            float s = w.W0 + w.W1 + w.W2 + w.W3;
            if (s <= 1e-6f) return One(BoneId.Hips);
            w.W0 /= s; w.W1 /= s; w.W2 /= s; w.W3 /= s;
            // sort descending (Unity expects weight0 >= weight1 >= ...)
            for (int pass = 0; pass < 3; pass++)
            {
                if (w.W1 > w.W0) { Swap(ref w.B0, ref w.B1); Swap(ref w.W0, ref w.W1); }
                if (w.W2 > w.W1) { Swap(ref w.B1, ref w.B2); Swap(ref w.W1, ref w.W2); }
                if (w.W3 > w.W2) { Swap(ref w.B2, ref w.B3); Swap(ref w.W2, ref w.W3); }
            }
            return w;
        }

        static void Swap<T>(ref T a, ref T b) { T t = a; a = b; b = t; }

        public BoneWeight ToBoneWeight()
        {
            var n = Normalized();
            return new BoneWeight
            {
                boneIndex0 = n.B0, weight0 = n.W0,
                boneIndex1 = n.W1 > 0 ? n.B1 : 0, weight1 = n.W1,
                boneIndex2 = n.W2 > 0 ? n.B2 : 0, weight2 = n.W2,
                boneIndex3 = n.W3 > 0 ? n.B3 : 0, weight3 = n.W3,
            };
        }
    }

    /// <summary>
    /// Accumulates skinned geometry in the character's root space (bind pose). Submesh 0 = opaque body,
    /// submesh 1 = alpha tested double sided parts (hair panels, apron, glasses, sack skirt).
    /// Front faces are clockwise (Unity).
    /// </summary>
    internal sealed class SkinMeshBuilder
    {
        public const int Opaque = 0, Cutout = 1;

        public readonly List<Vector3> V = new List<Vector3>(2048);
        public readonly List<Vector3> N = new List<Vector3>(2048);
        public readonly List<Vector2> UV = new List<Vector2>(2048);
        public readonly List<SkinWeight> W = new List<SkinWeight>(2048);
        public readonly List<int>[] Tris = { new List<int>(4096), new List<int>(1024) };

        int _partV, _partT0, _partT1;

        public int TriangleCount => (Tris[0].Count + Tris[1].Count) / 3;

        public int Add(Vector3 p, Vector2 uv, SkinWeight w) => Add(p, Vector3.zero, uv, w);

        public int Add(Vector3 p, Vector3 n, Vector2 uv, SkinWeight w)
        {
            V.Add(p); N.Add(n); UV.Add(uv); W.Add(w);
            return V.Count - 1;
        }

        public void Tri(int sub, int a, int b, int c)
        {
            var l = Tris[sub];
            l.Add(a); l.Add(b); l.Add(c);
        }

        /// <summary>a = bottom-left, b = top-left, c = top-right, d = bottom-right seen from the front.</summary>
        public void Quad(int sub, int a, int b, int c, int d)
        {
            Tri(sub, a, b, c);
            Tri(sub, a, c, d);
        }

        /// <summary>Flat shaded quad with its own vertices.</summary>
        public void FlatQuad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, SkinWeight w)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-14f) n = Vector3.Cross(c - a, d - a);
            n.Normalize();
            int i0 = Add(a, n, ua, w), i1 = Add(b, n, ub, w), i2 = Add(c, n, uc, w), i3 = Add(d, n, ud, w);
            Quad(sub, i0, i1, i2, i3);
        }

        public void FlatQuad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, AtlasRect r, float u0, float v0, float u1, float v1, SkinWeight w)
            => FlatQuad(sub, a, b, c, d, r.UV(u0, v0), r.UV(u0, v1), r.UV(u1, v1), r.UV(u1, v0), w);

        /// <summary>Starts a smoothing group: <see cref="EndSmoothPart"/> computes averaged normals for vertices added since.</summary>
        public void BeginPart()
        {
            _partV = V.Count;
            _partT0 = Tris[0].Count;
            _partT1 = Tris[1].Count;
        }

        /// <summary>Area weighted smooth normals for the current part; vertices sharing a position share the normal (seams).</summary>
        public void EndSmoothPart()
        {
            int start = _partV;
            for (int i = start; i < V.Count; i++) N[i] = Vector3.zero;
            Accumulate(Tris[0], _partT0, start);
            Accumulate(Tris[1], _partT1, start);
            // merge seams
            var map = new Dictionary<long, Vector3>();
            for (int i = start; i < V.Count; i++)
            {
                long k = Key(V[i]);
                map.TryGetValue(k, out var acc);
                map[k] = acc + N[i];
            }
            for (int i = start; i < V.Count; i++)
            {
                Vector3 n = map[Key(V[i])];
                N[i] = n.sqrMagnitude > 1e-12f ? n.normalized : Vector3.up;
            }
        }

        void Accumulate(List<int> tris, int from, int vStart)
        {
            for (int t = from; t + 2 < tris.Count; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                if (a < vStart || b < vStart || c < vStart) continue;
                Vector3 n = Vector3.Cross(V[b] - V[a], V[c] - V[a]); // clockwise front -> outward (left handed)
                N[a] += n; N[b] += n; N[c] += n;
            }
        }

        static long Key(Vector3 p)
        {
            long x = Mathf.RoundToInt(p.x * 2000f), y = Mathf.RoundToInt(p.y * 2000f), z = Mathf.RoundToInt(p.z * 2000f);
            return (x & 0x1FFFFF) | ((y & 0x1FFFFF) << 21) | ((z & 0x1FFFFF) << 42);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(V);
            mesh.SetNormals(N);
            mesh.SetUVs(0, UV);
            var cols = new Color32[V.Count];
            var bw = new BoneWeight[V.Count];
            for (int i = 0; i < V.Count; i++)
            {
                cols[i] = new Color32(255, 255, 255, 255);
                bw[i] = W[i].ToBoneWeight();
            }
            mesh.colors32 = cols;
            int subs = Tris[1].Count > 0 ? 2 : 1;
            mesh.subMeshCount = subs;
            mesh.SetTriangles(Tris[0], 0, false);
            if (subs > 1) mesh.SetTriangles(Tris[1], 1, false);
            mesh.boneWeights = bw;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
