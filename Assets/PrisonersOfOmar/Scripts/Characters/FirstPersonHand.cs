using UnityEngine;

namespace PrisonersOfOmar.Characters
{
    /// <summary>(iteration 2) Pose of the first person hand mesh.</summary>
    internal enum FpHandPose
    {
        Fist,     // closed round the grip axis (flashlight, lighter, bottle, keys...)
        Relaxed,  // half open (empty hand reaching for doors / items, the free left hand)
        Pistol,   // round the revolver's raked grip, index finger on the trigger, thumb up the side
    }

    /// <summary>
    /// (iteration 2) The first person hand: stylised low poly - a lofted palm, four three-segment fingers and a two-segment
    /// thumb, smooth shaded - built round the held item's grip (hand space: the item pivot is the origin, the grip axis Z).
    /// Right hand anatomy: the back of the hand faces +X, the wrist is below (-Y), the fingers curl over the top of the
    /// grip from right to left and back under it; index finger at -Z (towards the camera), little finger at +Z, the thumb
    /// wraps round the back of the grip over the index finger. side = -1 mirrors it into a left hand.
    /// Texture: the skin atlas Hand region (back of the hand in v 0.5..1, one finger column per finger in v 0..0.5 with
    /// the nail at the tip, see paint_hand in char_textures.py).
    /// </summary>
    internal static class FirstPersonHand
    {
        const int FingerSides = 6;
        const int PalmSides = 10;

        // per finger (index, middle, ring, little): z, length factor, half width, half thickness, knuckle height offset
        static readonly float[] FZ = { -0.027f, -0.009f, 0.009f, 0.026f };
        static readonly float[] FLen = { 0.95f, 1f, 0.96f, 0.80f };
        static readonly float[] FW = { 0.0086f, 0.0090f, 0.0084f, 0.0073f };
        static readonly float[] FT = { 0.0078f, 0.0081f, 0.0076f, 0.0067f };
        static readonly float[] FArch = { 0.001f, 0.003f, 0.001f, -0.004f };

        /// <summary>Wrist centre (hand space, before mirroring) where the forearm attaches, for a pose.</summary>
        public static Vector3 WristPoint(FpHandPose pose, float hs, float side)
        {
            Vector3 w = new Vector3(0.032f, -0.054f, -0.002f) * hs; // a little inside the palm so the sleeve overlaps it
            w = PoseMatrix(pose).MultiplyPoint3x4(w);
            w.x *= side;
            return w;
        }

        /// <summary>The pistol hand is the fist turned so its grip axis runs up the revolver's raked grip.</summary>
        static Matrix4x4 PoseMatrix(FpHandPose pose)
        {
            // relaxed: turned palm forward (pushing a door, pressing a button, reaching for an item)
            if (pose == FpHandPose.Relaxed) return Matrix4x4.TRS(new Vector3(-0.012f, 0f, 0f), Quaternion.Euler(0f, 62f, -8f), Vector3.one);
            if (pose != FpHandPose.Pistol) return Matrix4x4.identity;
            // revolver grip: centre (0, -0.036, -0.012), axis tilted 18 degrees (ItemMeshFactory.Revolver); the back strap
            // sits in the palm, the knuckles at the front strap, the hand turned a little round the grip towards the back
            return Matrix4x4.TRS(new Vector3(0.002f, -0.036f, -0.024f), Quaternion.Euler(108f, 0f, 0f) * Quaternion.Euler(0f, 0f, -16f), Vector3.one);
        }

        public static void Build(MeshBuilder mb, float hs, float side, FpHandPose pose)
        {
            var hand = CharacterAtlas.Hand;
            Matrix4x4 M = PoseMatrix(pose) * Matrix4x4.Scale(Vector3.one * hs);
            var ctx = new Ctx { Mb = mb, M = M, Side = side };
            Palm(ref ctx, hand);
            for (int f = 0; f < 4; f++) Finger(ref ctx, hand, f, pose);
            Thumb(ref ctx, hand, pose);
        }

        struct Ctx
        {
            public MeshBuilder Mb;
            public Matrix4x4 M;
            public float Side;

            public int V(Vector3 p, Vector3 n, Vector2 uv)
            {
                p = M.MultiplyPoint3x4(p);
                n = M.MultiplyVector(n);
                p.x *= Side; n.x *= Side;
                return Mb.AddVertex(p, n.normalized, uv);
            }

            /// <summary>Triangle wound so its face normal agrees with the vertex normals (handles the mirrored left hand).</summary>
            public void T(int a, int b, int c, Vector3 pa, Vector3 pb, Vector3 pc, Vector3 nAvg)
            {
                pa = M.MultiplyPoint3x4(pa); pb = M.MultiplyPoint3x4(pb); pc = M.MultiplyPoint3x4(pc);
                nAvg = M.MultiplyVector(nAvg);
                pa.x *= Side; pb.x *= Side; pc.x *= Side; nAvg.x *= Side;
                Vector3 fn = Vector3.Cross(pb - pa, pc - pa); // clockwise front -> outward (left handed)
                if (Vector3.Dot(fn, nAvg) >= 0f) Mb.AddTriangle(a, b, c);
                else Mb.AddTriangle(a, c, b);
            }
        }

        static Vector2 UV(AtlasRect r, float u, float v) => r.UV(u, v);

        // ------------------------------------------------------------------------------------------ palm
        static void Palm(ref Ctx c, AtlasRect hand)
        {
            // horizontal sections from the wrist up to the knuckles: y, centre x, centre z, half thickness (X), half width (Z)
            float[] ys = { -0.064f, -0.042f, -0.014f, 0.010f, 0.024f };
            float[] cx = { 0.032f, 0.033f, 0.032f, 0.028f, 0.021f };
            float[] cz = { -0.002f, -0.003f, -0.002f, 0.000f, 0.000f };
            float[] tx = { 0.0110f, 0.0135f, 0.0145f, 0.0130f, 0.0095f };
            float[] wz = { 0.0215f, 0.0330f, 0.0380f, 0.0385f, 0.0350f };
            float[] vs = { 1.0f, 0.86f, 0.72f, 0.58f, 0.52f };
            int n = PalmSides;
            var pos = new Vector3[ys.Length, n + 1];
            var nor = new Vector3[ys.Length, n + 1];
            var ids = new int[ys.Length, n + 1];
            for (int k = 0; k < ys.Length; k++)
            {
                for (int j = 0; j <= n; j++)
                {
                    float a = (float)j / n * Mathf.PI * 2f;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    // superellipse-ish rounded box: dorsal (+X) a little flatter, thenar bulge low on the index side (-Z)
                    float e = 2.6f;
                    float k1 = 1f / Mathf.Pow(Mathf.Pow(Mathf.Abs(ca), e) + Mathf.Pow(Mathf.Abs(sa), e), 1f / e);
                    float x = cx[k] + tx[k] * ca * k1;
                    float z = cz[k] + wz[k] * sa * k1;
                    if (k <= 2 && ca < 0f && sa < 0f) { x -= 0.004f * (-ca) * (-sa) * (k == 1 ? 1.4f : 1f); z -= 0.002f * (-sa); }
                    pos[k, j] = new Vector3(x, ys[k], z);
                    nor[k, j] = new Vector3(ca / tx[k], 0f, sa / wz[k]).normalized;
                    ids[k, j] = c.V(pos[k, j], nor[k, j], UV(hand, 0.5f + 0.5f * sa, vs[k]));
                }
            }
            for (int k = 0; k < ys.Length - 1; k++)
                for (int j = 0; j < n; j++)
                {
                    Vector3 nAvg = nor[k, j] + nor[k + 1, j + 1];
                    c.T(ids[k, j], ids[k + 1, j], ids[k + 1, j + 1], pos[k, j], pos[k + 1, j], pos[k + 1, j + 1], nAvg);
                    c.T(ids[k, j], ids[k + 1, j + 1], ids[k, j + 1], pos[k, j], pos[k + 1, j + 1], pos[k, j + 1], nAvg);
                }
            // wrist cap (seen when the forearm bends away from the palm)
            Vector3 wc = new Vector3(cx[0], ys[0] - 0.003f, cz[0]);
            int wi = c.V(wc, Vector3.down, UV(hand, 0.5f, 1f));
            for (int j = 0; j < n; j++)
                c.T(ids[0, j + 1], wi, ids[0, j], pos[0, j + 1], wc, pos[0, j], Vector3.down + nor[0, j] * 0.3f);
            // knuckle cap
            int top = ys.Length - 1;
            Vector3 pole = new Vector3(cx[top] - 0.004f, ys[top] + 0.004f, cz[top]);
            int pi = c.V(pole, Vector3.up, UV(hand, 0.5f, 0.52f));
            for (int j = 0; j < n; j++)
                c.T(ids[top, j], pi, ids[top, j + 1], pos[top, j], pole, pos[top, j + 1], Vector3.up + nor[top, j] * 0.3f);
        }

        // ------------------------------------------------------------------------------------------ fingers
        static void Finger(ref Ctx c, AtlasRect hand, int f, FpHandPose pose)
        {
            float L = FLen[f];
            float[] len = { 0.036f * L, 0.030f * L, 0.020f * L };
            // FK in the XY plane: alpha measured from +Y towards -X (curling over the top of the grip)
            float a0, t1, t2, t3;
            if (pose == FpHandPose.Relaxed) { a0 = 2f + f * 2f; t1 = 20f + f * 6f; t2 = 30f + f * 6f; t3 = 18f + f * 4f; }
            else if (pose == FpHandPose.Pistol && f == 0) { a0 = -4f; t1 = 14f; t2 = 38f; t3 = 26f; }   // trigger finger: along the guard, tip on the trigger
            else { a0 = 8f; t1 = 78f; t2 = 77f + (3 - f) * 1f; t3 = 66f; }
            if (pose != FpHandPose.Relaxed && f == 3) { t1 += 6f; t2 += 6f; }  // little finger curls tighter
            Vector3 mcp = new Vector3(0.020f - (f == 3 ? 0.002f : 0f), 0.024f + FArch[f], FZ[f]);
            float[] alphas = { a0 + t1, a0 + t1 + t2, a0 + t1 + t2 + t3 };
            var pts = new Vector3[4];
            pts[0] = mcp;
            for (int s = 0; s < 3; s++)
            {
                float a = alphas[s] * Mathf.Deg2Rad;
                pts[s + 1] = pts[s] + new Vector3(-Mathf.Sin(a), Mathf.Cos(a), 0f) * len[s];
            }
            // root starts a little inside the palm so the knuckle reads as a bump
            Vector3 dir0 = (pts[1] - pts[0]).normalized;
            Vector3 root = pts[0] - dir0 * 0.006f;
            Vector3[] path = { root, pts[0], pts[1], pts[2], pts[3] };
            float w = FW[f], t = FT[f];
            float[] rw = { w * 1.1f, w * 1.05f, w * 0.96f, w * 0.88f, w * 0.80f };
            float[] rt = { t * 1.15f, t * 1.05f, t * 0.95f, t * 0.86f, t * 0.76f };
            float u0 = f * 0.25f, u1 = u0 + 0.25f;
            Tube(ref c, hand, path, rw, rt, null, u0, u1, 0.52f, 0.0f);
        }

        // ------------------------------------------------------------------------------------------ thumb
        static void Thumb(ref Ctx c, AtlasRect hand, FpHandPose pose)
        {
            Vector3[] path;
            if (pose == FpHandPose.Relaxed)
                path = new[] { new Vector3(0.027f, -0.036f, -0.028f), new Vector3(0.012f, -0.026f, -0.046f), new Vector3(-0.006f, -0.012f, -0.058f),
                               new Vector3(-0.016f, 0.003f, -0.064f), new Vector3(-0.022f, 0.016f, -0.066f) };
            else if (pose == FpHandPose.Pistol)
                // pistol: hand +Y runs along the barrel (see PoseMatrix): the thumb lies along the left of the frame, pointing forward
                path = new[] { new Vector3(0.027f, -0.036f, -0.028f), new Vector3(0.008f, -0.024f, -0.040f), new Vector3(-0.008f, -0.006f, -0.042f),
                               new Vector3(-0.016f, 0.014f, -0.040f), new Vector3(-0.019f, 0.030f, -0.037f) };
            else
                path = new[] { new Vector3(0.027f, -0.036f, -0.028f), new Vector3(0.010f, -0.030f, -0.044f), new Vector3(-0.012f, -0.018f, -0.048f),
                               new Vector3(-0.027f, -0.004f, -0.044f), new Vector3(-0.035f, 0.010f, -0.035f) };
            float[] rw = { 0.0125f, 0.0105f, 0.0097f, 0.0090f, 0.0080f };
            float[] rt = { 0.0110f, 0.0092f, 0.0086f, 0.0080f, 0.0070f };
            // dorsal side of the thumb faces away from the grip axis (and a little backwards, towards the camera)
            var dorsal = new Vector3[path.Length];
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 radial = new Vector3(path[i].x, path[i].y, 0f);
                dorsal[i] = (radial.normalized + new Vector3(0f, 0f, -0.7f)).normalized;
            }
            // the thumb uses the index finger column (its nail lands at the tip)
            Tube(ref c, hand, path, rw, rt, dorsal, 0f, 0.25f, 0.5f, 0f);
        }

        /// <summary>
        /// Smooth tube along a path with elliptic sections (half width rw, half thickness rt along the dorsal direction)
        /// and a rounded tip. UV: u across the column (dorsal side = column centre), v from vRoot to vTip along the length.
        /// dorsal = null: the outside of a curl round the Z axis (fingers).
        /// </summary>
        static void Tube(ref Ctx c, AtlasRect hand, Vector3[] path, float[] rw, float[] rt, Vector3[] dorsal, float u0, float u1, float vRoot, float vTip)
        {
            int n = FingerSides, m = path.Length;
            var pos = new Vector3[m, n + 1];
            var nor = new Vector3[m, n + 1];
            var ids = new int[m, n + 1];
            float total = 0f;
            var acc = new float[m];
            for (int i = 1; i < m; i++) { total += Vector3.Distance(path[i], path[i - 1]); acc[i] = total; }
            Vector3 lastT = Vector3.forward, lastD = Vector3.up;
            for (int i = 0; i < m; i++)
            {
                Vector3 T = i == 0 ? path[1] - path[0] : i == m - 1 ? path[m - 1] - path[m - 2] : path[i + 1] - path[i - 1];
                T.Normalize();
                Vector3 D = dorsal != null ? dorsal[i] : Vector3.Cross(T, Vector3.forward);
                D -= T * Vector3.Dot(D, T);
                if (D.sqrMagnitude < 1e-8f) D = Vector3.up;
                D.Normalize();
                Vector3 S = Vector3.Cross(D, T).normalized;
                lastT = T; lastD = D;
                float v = Mathf.Lerp(vRoot, vTip + 0.06f, total > 0f ? acc[i] / total : 0f);
                for (int j = 0; j <= n; j++)
                {
                    float a = (float)j / n * Mathf.PI * 2f;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    pos[i, j] = path[i] + D * (ca * rt[i]) + S * (sa * rw[i]);
                    nor[i, j] = (D * (ca / rt[i]) + S * (sa / rw[i])).normalized;
                    float u = Mathf.Lerp(u0, u1, 0.5f + 0.42f * sa);
                    ids[i, j] = c.V(pos[i, j], nor[i, j], UV(hand, u, v));
                }
            }
            for (int i = 0; i < m - 1; i++)
                for (int j = 0; j < n; j++)
                {
                    Vector3 nAvg = nor[i, j] + nor[i + 1, j + 1];
                    c.T(ids[i, j], ids[i + 1, j], ids[i + 1, j + 1], pos[i, j], pos[i + 1, j], pos[i + 1, j + 1], nAvg);
                    c.T(ids[i, j], ids[i + 1, j + 1], ids[i, j + 1], pos[i, j], pos[i + 1, j + 1], pos[i, j + 1], nAvg);
                }
            // rounded tip: one smaller ring then the pole (the nail sits on the dorsal side of the last segment)
            int e = m - 1;
            Vector3 tipC = path[e] + lastT * rt[e] * 0.55f;
            Vector3 S2 = Vector3.Cross(lastD, lastT).normalized;
            var ring = new int[n + 1];
            var rp = new Vector3[n + 1];
            var rn = new Vector3[n + 1];
            for (int j = 0; j <= n; j++)
            {
                float a = (float)j / n * Mathf.PI * 2f;
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                rp[j] = tipC + lastD * (ca * rt[e] * 0.72f) + S2 * (sa * rw[e] * 0.72f);
                rn[j] = (lastD * ca + S2 * sa + lastT * 0.8f).normalized;
                ring[j] = c.V(rp[j], rn[j], UV(hand, Mathf.Lerp(u0, u1, 0.5f + 0.3f * sa), vTip + 0.02f));
            }
            for (int j = 0; j < n; j++)
            {
                Vector3 nAvg = nor[e, j] + rn[j + 1];
                c.T(ids[e, j], ring[j], ring[j + 1], pos[e, j], rp[j], rp[j + 1], nAvg);
                c.T(ids[e, j], ring[j + 1], ids[e, j + 1], pos[e, j], rp[j + 1], pos[e, j + 1], nAvg);
            }
            Vector3 pole = path[e] + lastT * rt[e] * 1.0f;
            int pi = c.V(pole, lastT, UV(hand, (u0 + u1) * 0.5f, vTip + 0.01f));
            for (int j = 0; j < n; j++) c.T(ring[j], pi, ring[j + 1], rp[j], pole, rp[j + 1], lastT);
        }
    }
}
