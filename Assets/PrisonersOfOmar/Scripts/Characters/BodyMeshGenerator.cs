using UnityEngine;

namespace PrisonersOfOmar.Characters
{
    /// <summary>
    /// Builds the bind-pose skinned mesh of a <see cref="BodySpec"/> from ring cross-sections along the skeleton.
    /// Root space: origin between the feet on the ground, +Z forward, +X = character's right.
    /// Ring vertex i of an N-sided tube sits at angle theta_i = 180 - 360 i / N degrees (0 = front, +90 = right),
    /// texture u = i / N (mirrored on the left limbs), v = the ring's table value (see CharacterAtlas).
    /// </summary>
    internal static class BodyMeshGenerator
    {
        /// <summary>Bind positions of the bones (root space).</summary>
        internal struct Skeleton
        {
            public Vector3[] Pos;
            public float ChinY;
            public Vector3 RightSocket, LeftSocket, Eye;
        }

        /// <summary>
        /// Finger bones after the 17 pose bones (not driven by <see cref="PoseBuffer"/>): per hand the relaxed fingers and the
        /// clenched fist, both pivoting at the palm centre. The animator swaps them by scale (folded away inside the palm).
        /// </summary>
        internal const int LFingersBone = 17, LFistBone = 18, RFingersBone = 19, RFistBone = 20, TotalBones = 21;

        /// <summary>Palm centre of a hand (root space, bind pose). side 0 = left, 1 = right.</summary>
        internal static Vector3 PalmCenter(BodySpec b, Skeleton sk, int side)
        {
            float hs = b.HandScale * b.Scale;
            return sk.Pos[(int)BoneId.LHand + (side == 0 ? 0 : 3)] + new Vector3(0, -0.092f * hs * 0.5f - 0.004f * hs, 0.004f * hs);
        }

        public static Skeleton MakeSkeleton(BodySpec b)
        {
            var sk = new Skeleton { Pos = new Vector3[17] };
            float s = b.Scale;
            sk.ChinY = b.Height - b.HeadH;
            var p = sk.Pos;
            p[(int)BoneId.Hips] = new Vector3(0, b.HipsY, 0);
            p[(int)BoneId.Spine] = new Vector3(0, b.SpineY, -0.008f * s);
            p[(int)BoneId.Chest] = new Vector3(0, b.ChestY, -0.012f * s);
            p[(int)BoneId.Neck] = new Vector3(0, b.NeckY, -0.016f * s);
            p[(int)BoneId.Head] = new Vector3(0, sk.ChinY + 0.12f * b.HeadH, -0.02f * s);
            for (int side = 0; side < 2; side++)
            {
                float sx = side == 0 ? -1f : 1f;
                int o = side == 0 ? 0 : 3;
                p[(int)BoneId.LUpperArm + o] = new Vector3(sx * b.ShoulderX, b.ShoulderY, -0.016f * s);
                p[(int)BoneId.LLowerArm + o] = new Vector3(sx * b.ElbowX, b.ElbowY, -0.026f * s);
                p[(int)BoneId.LHand + o] = new Vector3(sx * b.WristX, b.WristY, -0.008f * s);
                p[(int)BoneId.LUpperLeg + o] = new Vector3(sx * b.HipJointX, b.HipJointY, 0f);
                p[(int)BoneId.LLowerLeg + o] = new Vector3(sx * b.HipJointX, b.KneeY, 0.004f * s);
                p[(int)BoneId.LFoot + o] = new Vector3(sx * b.HipJointX, b.AnkleY, -0.006f * s);
            }
            float hs = b.HandScale * s;
            // grip: inside the curled fingers, palm faces the body (-X for the right hand)
            sk.RightSocket = p[(int)BoneId.RHand] + new Vector3(-0.012f * hs, -0.085f * hs, 0.012f * hs);
            sk.LeftSocket = p[(int)BoneId.LHand] + new Vector3(0.012f * hs, -0.085f * hs, 0.012f * hs);
            sk.Eye = new Vector3(0, sk.ChinY + CharacterAtlas.EyeY * b.HeadH, HeadFrontZ(b, CharacterAtlas.EyeY) - 0.025f * s);
            return sk;
        }

        static float HeadAxisZ(BodySpec b) => 0.006f * b.Scale;

        /// <summary>Mouth position on the face (root space, bind pose).</summary>
        internal static Vector3 MouthPoint(BodySpec b) => new Vector3(0f, b.Height - b.HeadH + 0.17f * b.HeadH, HeadFrontZ(b, 0.17f));

        /// <summary>z of the front of the head (face surface) at head height y_rel.</summary>
        public static float HeadFrontZ(BodySpec b, float yRel)
        {
            Ring r = HeadRingAt(b, yRel);
            return HeadAxisZ(b) + (r.C + r.F) * b.HeadH;
        }

        static Ring HeadRingAt(BodySpec b, float yRel)
        {
            var ys = CharacterAtlas.HeadRingY;
            for (int k = 0; k < ys.Length - 1; k++)
            {
                if (yRel <= ys[k + 1])
                {
                    float t = Mathf.InverseLerp(ys[k], ys[k + 1], yRel);
                    Ring a = b.HeadRings[k], c = b.HeadRings[k + 1];
                    return new Ring(Mathf.Lerp(a.W, c.W, t), Mathf.Lerp(a.F, c.F, t), Mathf.Lerp(a.B, c.B, t), Mathf.Lerp(a.C, c.C, t));
                }
            }
            return b.HeadRings[ys.Length - 1];
        }

        // ------------------------------------------------------------------------------------------ helpers
        static float Theta(int i, int n) => 180f - 360f * i / n;

        /// <summary>Superellipse point of a ring at angle theta (degrees), in the ring's (right, forward) plane.</summary>
        static Vector2 RingPoint(Ring r, float thetaDeg, float square)
        {
            float a = thetaDeg * Mathf.Deg2Rad;
            float s = Mathf.Sin(a), c = Mathf.Cos(a);
            float k = 1f;
            if (Mathf.Abs(square - 2f) > 0.01f)
            {
                float sum = Mathf.Pow(Mathf.Abs(s), square) + Mathf.Pow(Mathf.Abs(c), square);
                k = 1f / Mathf.Pow(Mathf.Max(sum, 1e-6f), 1f / square);
            }
            float x = r.W * s * k;
            float z = (c >= 0f ? r.F : r.B) * c * k + r.C;
            return new Vector2(x, z);
        }

        static void Frame(Vector3 axis, out Vector3 right, out Vector3 fwd)
        {
            axis.Normalize();
            fwd = Vector3.forward - axis * Vector3.Dot(Vector3.forward, axis);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.Cross(Vector3.right, axis);
            fwd.Normalize();
            right = Vector3.Cross(axis, fwd).normalized;
        }

        // ------------------------------------------------------------------------------------------ build
        public static void Build(BodySpec b, Skeleton sk, SkinMeshBuilder mb)
        {
            Torso(b, sk, mb);
            Head(b, sk, mb);
            for (int side = 0; side < 2; side++)
            {
                Leg(b, sk, mb, side);
                Arm(b, sk, mb, side);
                Hand(b, sk, mb, side);
                Shoe(b, sk, mb, side);
            }
            switch (b.Hair)
            {
                case HairStyle.Spiky: SpikyHair(b, sk, mb); break;
                case HairStyle.LongWavy: LongHair(b, sk, mb, true); break;
                case HairStyle.LongBangs: LongHair(b, sk, mb, false); break;
                case HairStyle.Curly: CurlyHair(b, sk, mb); break;
            }
            if (b.Glasses != GlassesStyle.None) Glasses(b, sk, mb);
            if (b.SkirtLen > 0f) MiniSkirt(b, sk, mb);
            if (b.Skirt) SackSkirt(b, sk, mb);
            if (b.Noose) Noose(b, sk, mb);
            if (b.Apron) Apron(b, sk, mb);
        }

        // ------------------------------------------------------------------------------------------ torso
        /// <summary>Under-bust ring (women): inserted between torso rings 4 and 5 so the bust gets a real underside.</summary>
        const float UnderBustT = 0.615f;

        static void Torso(BodySpec b, Skeleton sk, SkinMeshBuilder mb)
        {
            int N = Mathf.Max(8, b.TorsoSides);
            var T = CharacterAtlas.TorsoT;
            var reg = CharacterAtlas.Torso;
            bool underBust = b.Bust > 0f && b.TorsoSides > CharacterAtlas.TorsoSides;
            int rings = T.Length + (underBust ? 1 : 0);
            mb.BeginPart();
            int first = mb.V.Count;
            for (int kk = 0; kk < rings; kk++)
            {
                // k = source ring index; the extra ring (k = 4, lerp towards 5) sits at UnderBustT
                bool extra = underBust && kk == 5;
                int k = underBust && kk >= 5 ? kk - 1 : kk;
                float t = extra ? UnderBustT : T[k];
                float y = Mathf.Lerp(b.CrotchY, b.NeckY, t);
                Ring r = b.Torso[k];
                if (extra)
                {
                    float f = Mathf.InverseLerp(T[4], T[5], t);
                    Ring c5 = b.Torso[5];
                    r = new Ring(Mathf.Lerp(r.W, c5.W, f), Mathf.Lerp(r.F, c5.F, f), Mathf.Lerp(r.B, c5.B, f), Mathf.Lerp(r.C, c5.C, f));
                }
                if (k == 7) r.W *= 0.95f;
                if (k == 0) { r.W *= 0.86f; r.F *= 0.9f; }
                for (int i = 0; i <= N; i++)
                {
                    float th = Theta(i, N);
                    float sq = k == 7 ? b.TorsoSquare + 0.2f : k == 0 && b.CrotchSquare > 0f ? b.CrotchSquare : b.TorsoSquare;
                    Vector2 q = RingPoint(r, th, sq);
                    float z = q.y;
                    float ath = Mathf.Abs(th);
                    // bust / belly / buttocks
                    if (b.Bust > 0)
                    {
                        float lobe = Bump(ath, b.BustAngle, b.BustWidth);
                        if (extra) z += b.Bust * 0.42f * lobe;
                        else if (k == 5) z += b.Bust * lobe;
                        else if (k == 4) z += b.Bust * (underBust ? 0.04f : 0.35f) * lobe;
                        else if (k == 6) z += b.Bust * (underBust ? 0.32f : 0.25f) * lobe;
                    }
                    if ((k == 2 || k == 3) && b.Belly > 0) z += b.Belly * Bump(ath, 0f, 45f);
                    if (k == 1) z -= 0.012f * b.Scale * Bump(ath, 150f, 25f);
                    if (b.Butt > 0f)
                    {
                        // two rounded cheeks (|theta| ~ 152) on the hip ring, softer below / above
                        float cheek = Bump(ath, 152f, 20f);
                        if (k == 1) z -= b.Butt * cheek;
                        else if (k == 0) z -= b.Butt * 0.15f * cheek;
                        else if (k == 2) z -= b.Butt * 0.65f * cheek;
                    }
                    float vy = y;
                    if (k == 7) vy -= b.ShoulderSlope * b.Scale * Mathf.Pow(Mathf.Abs(Mathf.Sin(th * Mathf.Deg2Rad)), 4f); // sloping shoulders
                    Vector3 p = new Vector3(q.x, vy, z);
                    SkinWeight w = extra ? SkinWeight.Lerp(TorsoWeight(4, th), TorsoWeight(5, th), 0.5f) : TorsoWeight(k, th);
                    mb.Add(p, reg.UV((float)i / N, t), w);
                }
            }
            int row = N + 1;
            for (int k = 0; k < rings - 1; k++)
                for (int i = 0; i < N; i++)
                {
                    int a = first + k * row + i;
                    mb.Quad(SkinMeshBuilder.Opaque, a, a + row, a + row + 1, a + 1);
                }
            mb.EndSmoothPart();
            // crotch cap (fan), seen from below: its own vertices / smoothing so the bottom ring of the torso is not
            // darkened by the downward facing cap normals
            Ring r0 = b.Torso[0];
            mb.BeginPart();
            int capFirst = mb.V.Count;
            for (int i = 0; i <= N; i++) mb.Add(mb.V[first + i], mb.UV[first + i], mb.W[first + i]);
            int c = mb.Add(new Vector3(0, b.CrotchY + 0.004f * b.Scale, (r0.F * 0.9f - r0.B) * 0.3f), reg.UV(0.5f, 0f), SkinWeight.One(BoneId.Hips));
            for (int i = 0; i < N; i++) mb.Tri(SkinMeshBuilder.Opaque, c, capFirst + i, capFirst + i + 1);
            mb.EndSmoothPart();
        }

        /// <summary>
        /// (iteration 2) Flared mini skirt of a dress (<see cref="BodySpec.SkirtLen"/>): a cone from the waist over the hips
        /// to the hem, textured with the bottom of the torso strip (the waist at v 0.40, the hem at v 0). The hem follows
        /// the thighs (each side weighted to its leg, front / back centre shared) so the legs never poke through.
        /// </summary>
        static void MiniSkirt(BodySpec b, Skeleton sk, SkinMeshBuilder mb)
        {
            int N = Mathf.Max(12, b.TorsoSides);
            var T = CharacterAtlas.TorsoT;
            var reg = CharacterAtlas.Torso;
            float s = b.Scale;
            float hemY = b.CrotchY - b.SkirtLen;
            Ring waist = b.Torso[3], low = b.Torso[2], hip = b.Torso[1];
            float hipW = hip.W * 1.05f;
            // the hem must clear both thighs: thigh centre at HipJointX, radius ~LegRx[7]
            float hemW = Mathf.Max(hipW * 1.06f, b.HipJointX + b.LegRx[7] * 1.25f + 0.02f * s);
            float hemF = Mathf.Max(hip.F * 1.25f, b.LegRz[7] * 1.45f + 0.03f * s);
            float hemB = Mathf.Max(hip.B * 1.12f, b.LegRz[7] * 1.45f + 0.035f * s);
            float[] ys = { Mathf.Lerp(b.CrotchY, b.NeckY, T[3]), Mathf.Lerp(b.CrotchY, b.NeckY, T[2]), Mathf.Lerp(b.CrotchY, b.NeckY, T[1]),
                           b.CrotchY - 0.02f * s, hemY };
            Ring[] rs =
            {
                new Ring(waist.W + 0.004f * s, waist.F + 0.004f * s, waist.B + 0.004f * s, waist.C),
                new Ring(low.W + 0.008f * s, low.F + 0.006f * s, low.B + 0.006f * s, low.C),
                new Ring(hipW, hip.F + 0.012f * s, hip.B + 0.008f * s, hip.C),
                new Ring(Mathf.Lerp(hipW, hemW, 0.55f), Mathf.Lerp(hip.F, hemF, 0.6f), Mathf.Lerp(hip.B, hemB, 0.6f), 0f),
                new Ring(hemW, hemF, hemB, 0.004f * s),
            };
            float[] vs = { 0.40f, 0.30f, 0.20f, 0.10f, 0.0f };
            mb.BeginPart();
            int first = mb.V.Count;
            for (int k = 0; k < rs.Length; k++)
            {
                for (int i = 0; i <= N; i++)
                {
                    float th = Theta(i, N);
                    float ath = Mathf.Abs(th);
                    Vector2 q = RingPoint(rs[k], th, 2.0f);
                    float z = q.y;
                    if (b.Butt > 0f && k >= 1 && k <= 3) z -= b.Butt * (k == 2 ? 1f : 0.6f) * Bump(ath, 152f, 24f) * 0.9f;
                    if (b.Belly > 0f && k <= 1) z += b.Belly * Bump(ath, 0f, 45f);
                    float y = ys[k];
                    if (k == 4) y += 0.006f * s * Mathf.Sin(th * Mathf.Deg2Rad * 6f); // soft flare folds at the hem
                    float side = Mathf.Sin(th * Mathf.Deg2Rad);
                    BoneId leg = side >= 0f ? BoneId.RUpperLeg : BoneId.LUpperLeg;
                    float sa = Mathf.Abs(side);
                    SkinWeight w;
                    switch (k)
                    {
                        case 0: w = SkinWeight.Two(BoneId.Hips, BoneId.Spine, 0.3f); break;
                        case 1: w = SkinWeight.One(BoneId.Hips); break;
                        case 2: w = SkinWeight.One(BoneId.Hips).Plus(leg, 0.12f * sa); break;
                        default:
                        {
                            // sides follow their own thigh, the centre front / back is shared by both thighs
                            float lw = k == 3 ? 0.4f : 0.7f;
                            BoneId other = leg == BoneId.RUpperLeg ? BoneId.LUpperLeg : BoneId.RUpperLeg;
                            w = SkinWeight.One(BoneId.Hips);
                            w.W0 = 1f - lw;
                            w.B1 = (int)leg; w.W1 = lw * (0.5f + 0.5f * sa);
                            w.B2 = (int)other; w.W2 = lw * 0.5f * (1f - sa);
                            break;
                        }
                    }
                    mb.Add(new Vector3(q.x, y, z), reg.UV((float)i / N, vs[k]), w.Normalized());
                }
            }
            int row = N + 1;
            for (int k = 0; k < rs.Length - 1; k++)
                for (int i = 0; i < N; i++)
                {
                    int a = first + (k + 1) * row + i; // lower ring first (bottom-left)
                    mb.Quad(SkinMeshBuilder.Opaque, a, a - row, a - row + 1, a + 1);
                }
            mb.EndSmoothPart();
        }

        static float Bump(float ath, float center, float width)
        {
            float d = (ath - center) / width;
            return Mathf.Exp(-d * d);
        }

        static SkinWeight TorsoWeight(int k, float th)
        {
            float side = Mathf.Sin(th * Mathf.Deg2Rad); // +1 right, -1 left
            float sideAbs = Mathf.Abs(side);
            BoneId leg = side >= 0 ? BoneId.RUpperLeg : BoneId.LUpperLeg;
            BoneId arm = side >= 0 ? BoneId.RUpperArm : BoneId.LUpperArm;
            switch (k)
            {
                case 0: return SkinWeight.One(BoneId.Hips).Plus(leg, 0.35f * sideAbs);
                case 1: return SkinWeight.One(BoneId.Hips).Plus(leg, 0.12f * sideAbs);
                case 2: return SkinWeight.Two(BoneId.Hips, BoneId.Spine, 0.35f);
                case 3: return SkinWeight.Two(BoneId.Spine, BoneId.Hips, 0.2f);
                case 4: return SkinWeight.Two(BoneId.Spine, BoneId.Chest, 0.4f);
                case 5: return SkinWeight.Two(BoneId.Chest, BoneId.Spine, 0.2f);
                case 6: return SkinWeight.One(BoneId.Chest).Plus(arm, 0.18f * Mathf.Pow(sideAbs, 4f));
                case 7: return SkinWeight.One(BoneId.Chest).Plus(arm, 0.4f * Mathf.Pow(sideAbs, 3f));
                default: return SkinWeight.Two(BoneId.Chest, BoneId.Neck, 0.45f);
            }
        }

        // ------------------------------------------------------------------------------------------ head
        static void Head(BodySpec b, Skeleton sk, SkinMeshBuilder mb)
        {
            const int N = CharacterAtlas.HeadSides;
            var Y = CharacterAtlas.HeadRingY;
            var reg = CharacterAtlas.Head;
            float hh = b.HeadH;
            float axisZ = HeadAxisZ(b);
            mb.BeginPart();
            int first = mb.V.Count;
            for (int k = 0; k < Y.Length; k++)
            {
                Ring r = b.HeadRings[k];
                float ringY = sk.ChinY + Y[k] * hh;
                for (int i = 0; i <= N; i++)
                {
                    float y = ringY;
                    float th = Theta(i, N);
                    Vector2 q = RingPoint(r, th, k <= 1 ? 2f : b.HeadSquare);
                    float x = q.x * hh, z = q.y * hh + axisZ;
                    float ath = Mathf.Abs(th);
                    if (b.HeadShape != HeadShape.Sack)
                    {
                        if (i == N / 2)
                        {
                            if (k == 4) z += b.Nose * hh;            // nose tip
                            if (k == 5) z += b.Nose * 0.45f * hh;    // bridge
                            if (k == 3) z += b.Nose * 0.15f * hh;    // lips
                            if (k == 2) z += 0.02f * hh;             // chin
                        }
                        if (k == 5 && (i == N / 2 - 1 || i == N / 2 + 1)) z -= 0.025f * hh; // eye sockets
                        if (b.HeadShape == HeadShape.Human && (k == 4 || k == 5) && ath > 80f && ath < 100f) // ears
                        {
                            x += Mathf.Sign(x) * 0.05f * hh;
                        }
                        // hair volume where the texture shows hair
                        float d = HairThickness(b, th, Y[k]) * b.Scale;
                        if (d > 0f)
                        {
                            Vector2 dir = new Vector2(x, z - axisZ);
                            if (k == Y.Length - 1) dir = new Vector2(0, -0.2f); // crown: push up
                            float len = dir.magnitude;
                            if (len > 1e-5f) { x += dir.x / len * d; z += dir.y / len * d; }
                            if (k == Y.Length - 1) y += d * 0.9f;
                            if (k == Y.Length - 2) y += d * 0.5f;
                        }
                    }
                    else if (k == Y.Length - 1)
                    {
                        // sack: flat-ish top, slightly tilted / crumpled
                        y -= 0.06f * hh;
                    }
                    SkinWeight w;
                    if (k == 0) w = SkinWeight.Two(BoneId.Neck, BoneId.Chest, 0.3f);
                    else if (k == 1) w = SkinWeight.Two(BoneId.Neck, BoneId.Head, 0.25f);
                    else if (k == 2) w = SkinWeight.Two(BoneId.Head, BoneId.Neck, ath > 100f ? 0.35f : 0.1f);
                    else w = SkinWeight.One(BoneId.Head);
                    mb.Add(new Vector3(x, y, z), reg.UV(CharacterAtlas.HeadU[i], CharacterAtlas.HeadRingV[k]), w);
                }
            }
            int row = N + 1;
            for (int k = 0; k < Y.Length - 1; k++)
                for (int i = 0; i < N; i++)
                {
                    int a = first + k * row + i;
                    if (k == Y.Length - 2 && b.HeadShape != HeadShape.Sack)
                    {
                        // pole: one triangle per segment
                        mb.Tri(SkinMeshBuilder.Opaque, a, a + row, a + 1);
                        continue;
                    }
                    mb.Quad(SkinMeshBuilder.Opaque, a, a + row, a + row + 1, a + 1);
                }
            mb.EndSmoothPart();
        }

        /// <summary>Hair volume (meters at 1.8 m scale) on the head, matching the painted hairlines in char_textures.py.</summary>
        static float HairThickness(BodySpec b, float th, float yRel)
        {
            float ath = Mathf.Abs(th);
            float hl, d;
            switch (b.Hair)
            {
                case HairStyle.Spiky:
                    hl = Curve(ath, 0, 0.80f, 30, 0.78f, 55, 0.71f, 68, 0.66f, 86, 0.64f, 102, 0.64f, 110, 0.34f, 140, 0.16f, 180, 0.12f);
                    d = 0.012f;
                    break;
                case HairStyle.Short:
                    hl = Curve(ath, 0, 0.79f, 30, 0.77f, 55, 0.70f, 68, 0.64f, 86, 0.62f, 102, 0.62f, 110, 0.30f, 140, 0.12f, 180, 0.08f);
                    d = 0.011f;
                    break;
                case HairStyle.LongWavy:
                    hl = Curve(ath, 0, 0.80f, 20, 0.78f, 40, 0.70f, 52, 0.55f, 58, 0.20f, 64, -0.2f, 70, -0.6f, 180, -0.6f);
                    d = 0.016f;
                    break;
                case HairStyle.LongBangs:
                    if (b.BobHair && b.FringeSweep > 0f)
                        hl = Curve(ath, 0, 0.62f, 30, 0.62f, 44, 0.60f, 52, 0.50f, 57, 0.22f, 63, 0.02f, 75, -0.06f, 110, -0.12f, 150, -0.2f, 180, -0.22f)
                             + FringeSweep(th, b.FringeSweep);
                    else
                        hl = Curve(ath, 0, 0.62f, 30, 0.62f, 44, 0.60f, 52, 0.50f, 57, 0.20f, 63, -0.2f, 70, -0.6f, 180, -0.6f);
                    d = 0.013f;
                    break;
                case HairStyle.Curly:
                    // a thick mop of curls: the fringe line low on the forehead, over the tops of the ears, down to the nape;
                    // fuller towards the top (the curl clumps of CurlyHair() sit on this)
                    hl = Curve(ath, 0, 0.63f, 25, 0.63f, 40, 0.64f, 55, 0.62f, 68, 0.60f, 78, 0.56f, 96, 0.56f, 108, 0.36f, 135, 0.16f, 180, 0.10f);
                    d = 0.026f * (0.75f + 1.05f * Mathf.Clamp01((yRel - 0.5f) / 0.45f));
                    break;
                default: return 0f;
            }
            if (yRel < hl - 0.02f) return 0f;
            float t = Mathf.Clamp01((yRel - hl + 0.02f) / 0.12f);
            // long hair: thicker on the sides / back, thin near the neck
            if (b.Hair == HairStyle.LongWavy || b.Hair == HairStyle.LongBangs)
            {
                if (yRel < 0f) d *= 0.6f;
                if (ath > 60f && yRel > 0f) d *= 1.4f;
            }
            return d * t;
        }

        /// <summary>Side-swept fringe of the bob: the hairline rises at the parting (theta ~ +22) and sweeps down to the other
        /// temple. MUST match bob_sweep() in Tools/AssetPipeline/characters/char_textures.py.</summary>
        static float FringeSweep(float th, float amount)
        {
            float a = (th - 22f) / 15f, c = (th + 34f) / 16f;
            return amount * (0.17f * Mathf.Exp(-a * a) - 0.04f * Mathf.Exp(-c * c));
        }

        static float Curve(float x, params float[] xy)
        {
            if (x <= xy[0]) return xy[1];
            for (int i = 0; i + 3 < xy.Length; i += 2)
            {
                if (x <= xy[i + 2]) return Mathf.Lerp(xy[i + 1], xy[i + 3], Mathf.InverseLerp(xy[i], xy[i + 2], x));
            }
            return xy[xy.Length - 1];
        }

        // ------------------------------------------------------------------------------------------ limbs
        static void Leg(BodySpec b, Skeleton sk, SkinMeshBuilder mb, int side)
        {
            const int N = CharacterAtlas.LegSides;
            var T = CharacterAtlas.LegT;
            var reg = CharacterAtlas.Leg;
            int o = side == 0 ? 0 : 3;
            BoneId up = BoneId.LUpperLeg + o, lo = BoneId.LLowerLeg + o, ft = BoneId.LFoot + o;
            Vector3 hip = sk.Pos[(int)up], knee = sk.Pos[(int)lo], ankle = sk.Pos[(int)ft];
            Vector3 top = hip + new Vector3(0, 0.02f * b.Scale, 0);
            mb.BeginPart();
            int first = mb.V.Count;
            for (int k = 0; k < T.Length; k++)
            {
                Vector3 c;
                SkinWeight w;
                float t = T[k];
                if (t <= 0.5f)
                {
                    float f = t / 0.5f;
                    c = Vector3.Lerp(ankle, knee, f);
                    w = k == 0 ? SkinWeight.Two(lo, ft, 0.2f)
                        : k == 3 ? SkinWeight.Two(lo, up, 0.12f)
                        : k == 4 ? SkinWeight.Two(lo, up, 0.5f)
                        : SkinWeight.One(lo);
                }
                else
                {
                    float f = (t - 0.5f) / 0.5f;
                    c = Vector3.Lerp(knee, top, f);
                    w = k == 5 ? SkinWeight.Two(up, lo, 0.15f) : k == 8 ? SkinWeight.Two(up, BoneId.Hips, 0.4f) : SkinWeight.One(up);
                }
                float rx = b.LegRx[k], rz = b.LegRz[k];
                float back = k == 2 ? 1.18f : k == 1 ? 1.08f : 1f;   // calf
                if (b.Butt > 0f && k >= 6) back *= 1f + b.Butt / b.Scale * (k == 6 ? 3f : k == 7 ? 8f : 12f); // full back of the thigh under the buttocks
                float front = k == 4 ? 1.06f : 1f;                     // kneecap
                Ring r = new Ring(rx, rz * front, rz * back, k == 2 ? -0.006f * b.Scale : 0f);
                for (int i = 0; i <= N; i++)
                {
                    float th = Theta(i, N);
                    Vector2 q = RingPoint(r, th, 2f);
                    // thighs: inner side flatter
                    float x = q.x;
                    if (k >= 6 && (x * (side == 0 ? 1 : -1)) > 0) x *= b.Butt > 0f ? 0.97f : 0.85f; // (women: thighs touch at the top)
                    float u = (float)i / N;
                    if (side == 0) u = 1f - u;
                    mb.Add(c + new Vector3(x, 0, q.y), reg.UV(u, t), w);
                }
            }
            int row = N + 1;
            for (int k = 0; k < T.Length - 1; k++)
                for (int i = 0; i < N; i++)
                {
                    int a = first + k * row + i;
                    mb.Quad(SkinMeshBuilder.Opaque, a, a + row, a + row + 1, a + 1);
                }
            mb.EndSmoothPart();
        }

        static void Arm(BodySpec b, Skeleton sk, SkinMeshBuilder mb, int side)
        {
            const int N = CharacterAtlas.ArmSides;
            var T = CharacterAtlas.ArmT;
            var reg = CharacterAtlas.Arm;
            int o = side == 0 ? 0 : 3;
            float sx = side == 0 ? -1f : 1f;
            BoneId up = BoneId.LUpperArm + o, lo = BoneId.LLowerArm + o, hd = BoneId.LHand + o;
            Vector3 sh = sk.Pos[(int)up], el = sk.Pos[(int)lo], wr = sk.Pos[(int)hd];
            // round shoulders: the tube ends flush with the shoulder line and is closed by a deltoid dome (no open rim
            // poking above the torso)
            bool round = b.RoundShoulders;
            Vector3 top = sh + new Vector3(-sx * 0.012f * b.Scale, (round ? -0.004f : 0.035f) * b.Scale, 0);
            mb.BeginPart();
            int first = mb.V.Count;
            Vector3 lastC = top, lastAxis = Vector3.up;
            for (int k = 0; k < T.Length; k++)
            {
                float t = T[k];
                Vector3 c, axis;
                SkinWeight w;
                if (t <= 0.5f)
                {
                    float f = t / 0.5f;
                    c = Vector3.Lerp(wr, el, f);
                    axis = el - wr;
                    w = k == 0 ? SkinWeight.Two(lo, hd, 0.3f) : k == 3 ? SkinWeight.Two(lo, up, 0.5f) : SkinWeight.One(lo);
                }
                else
                {
                    float f = (t - 0.5f) / 0.5f;
                    c = Vector3.Lerp(el, top, f);
                    axis = top - el;
                    w = k == 4 ? SkinWeight.Two(up, lo, 0.15f) : k == 6 ? SkinWeight.Two(up, BoneId.Chest, 0.35f) : SkinWeight.One(up);
                }
                Frame(axis, out Vector3 right, out Vector3 fwd);
                float rr = b.ArmR[k];
                float flatX = k <= 2 ? 0.82f : 1f, flatZ = k <= 2 ? 1.18f : 1f;
                Ring r = new Ring(rr * flatX, rr * flatZ, rr * flatZ);
                for (int i = 0; i <= N; i++)
                {
                    float th = Theta(i, N);
                    Vector2 q = RingPoint(r, th, 2f);
                    float u = (float)i / N;
                    if (side == 0) u = 1f - u;
                    mb.Add(c + right * q.x + fwd * q.y, reg.UV(u, t), w);
                }
                lastC = c; lastAxis = axis;
            }
            int rings = T.Length;
            if (round)
            {
                // dome: one smaller ring leaning towards the neck, then the pole
                Vector3 ax = lastAxis.normalized;
                float rr = b.ArmR[T.Length - 1];
                Frame(ax, out Vector3 right, out Vector3 fwd);
                Vector3 inward = new Vector3(-sx, 0, 0);
                Vector3 c1 = lastC + ax * rr * 0.36f + inward * rr * 0.22f;
                var w1 = SkinWeight.Two(up, BoneId.Chest, 0.45f);
                for (int i = 0; i <= N; i++)
                {
                    float th = Theta(i, N);
                    Vector2 q = RingPoint(new Ring(rr * 0.74f, rr * 0.8f, rr * 0.8f), th, 2f);
                    float u = (float)i / N;
                    if (side == 0) u = 1f - u;
                    mb.Add(c1 + right * q.x + fwd * q.y, reg.UV(u, 1f), w1);
                }
                rings++;
            }
            int row = N + 1;
            for (int k = 0; k < rings - 1; k++)
                for (int i = 0; i < N; i++)
                {
                    int a = first + k * row + i;
                    mb.Quad(SkinMeshBuilder.Opaque, a, a + row, a + row + 1, a + 1);
                }
            if (round)
            {
                Vector3 ax = lastAxis.normalized;
                float rr = b.ArmR[T.Length - 1];
                int pole = mb.Add(lastC + ax * rr * 0.52f + new Vector3(-sx, 0, 0) * rr * 0.36f, reg.UV(0.5f, 1f), SkinWeight.Two(up, BoneId.Chest, 0.5f));
                int last = first + (rings - 1) * row;
                for (int i = 0; i < N; i++) mb.Tri(SkinMeshBuilder.Opaque, last + i, pole, last + i + 1);
            }
            mb.EndSmoothPart();
        }

        // ------------------------------------------------------------------------------------------ boxes
        /// <summary>Oriented box; faces get UV sub-rects of region r. uvSide used for +-X, uvFront for +-Z, uvEnd for +-Y.</summary>
        static void Box(SkinMeshBuilder mb, int sub, Vector3 c, Quaternion rot, Vector3 size, SkinWeight w,
            AtlasRect rSide, Vector4 uvSide, AtlasRect rFront, Vector4 uvFront, AtlasRect rEnd, Vector4 uvEnd)
        {
            Vector3 h = size * 0.5f;
            Vector3 X = rot * new Vector3(h.x, 0, 0), Y = rot * new Vector3(0, h.y, 0), Z = rot * new Vector3(0, 0, h.z);
            // +X face (seen from +X: right = +Z), -X face (seen from -X: right = -Z)
            mb.FlatQuad(sub, c + X - Y - Z, c + X + Y - Z, c + X + Y + Z, c + X - Y + Z, rSide, uvSide.x, uvSide.y, uvSide.z, uvSide.w, w);
            mb.FlatQuad(sub, c - X - Y + Z, c - X + Y + Z, c - X + Y - Z, c - X - Y - Z, rSide, uvSide.x, uvSide.y, uvSide.z, uvSide.w, w);
            // +Z face (seen from +Z: right = -X)
            mb.FlatQuad(sub, c + Z - Y + X, c + Z + Y + X, c + Z + Y - X, c + Z - Y - X, rFront, uvFront.x, uvFront.y, uvFront.z, uvFront.w, w);
            mb.FlatQuad(sub, c - Z - Y - X, c - Z + Y - X, c - Z + Y + X, c - Z - Y + X, rFront, uvFront.x, uvFront.y, uvFront.z, uvFront.w, w);
            // +Y / -Y
            mb.FlatQuad(sub, c + Y - X - Z, c + Y - X + Z, c + Y + X + Z, c + Y + X - Z, rEnd, uvEnd.x, uvEnd.y, uvEnd.z, uvEnd.w, w);
            mb.FlatQuad(sub, c - Y - X + Z, c - Y - X - Z, c - Y + X - Z, c - Y + X + Z, rEnd, uvEnd.x, uvEnd.y, uvEnd.z, uvEnd.w, w);
        }

        static void Hand(BodySpec b, Skeleton sk, SkinMeshBuilder mb, int side)
        {
            int o = side == 0 ? 0 : 3;
            float sx = side == 0 ? -1f : 1f;
            BoneId hd = BoneId.LHand + o;
            var w = SkinWeight.One(hd);
            var wOpen = SkinWeight.One((BoneId)(side == 0 ? LFingersBone : RFingersBone));
            var wFist = SkinWeight.One((BoneId)(side == 0 ? LFistBone : RFistBone));
            float hs = b.HandScale * b.Scale;
            var reg = CharacterAtlas.Hand;
            var sw = CharacterAtlas.SwatchSkin;
            var swUV = new Vector4(0.2f, 0.2f, 0.8f, 0.8f);
            // palm: thickness along X, width along Z, length down -Y
            Vector3 palmSize = new Vector3(0.030f, 0.092f, 0.082f) * hs;
            Vector3 palmC = PalmCenter(b, sk, side);
            Box(mb, SkinMeshBuilder.Opaque, palmC, Quaternion.identity, palmSize, w,
                reg, new Vector4(0, 0.5f, 1, 1), sw, swUV, sw, swUV);
            // relaxed: fingers curled a little towards the palm (palm faces the body: -X on the right hand)
            Quaternion curl = Quaternion.AngleAxis(sx * -28f, Vector3.forward);
            Vector3 fSize = new Vector3(0.024f, 0.082f, 0.078f) * hs;
            Vector3 fBase = palmC + new Vector3(0, -palmSize.y * 0.5f, 0);
            Vector3 fC = fBase + curl * new Vector3(0, -fSize.y * 0.5f, 0);
            Box(mb, SkinMeshBuilder.Opaque, fC, curl, fSize, wOpen,
                reg, new Vector4(0, 0, 1, 0.5f), sw, swUV, reg, new Vector4(0, 0, 1, 0.1f));
            // thumb at the front edge, pointing down / forward / inward
            Quaternion tr = Quaternion.AngleAxis(sx * -20f, Vector3.forward) * Quaternion.AngleAxis(-25f, Vector3.right);
            Vector3 tSize = new Vector3(0.024f, 0.062f, 0.024f) * hs;
            Vector3 tC = palmC + new Vector3(-sx * 0.006f * hs, -0.01f * hs, palmSize.z * 0.5f + 0.006f * hs) + tr * new Vector3(0, -tSize.y * 0.4f, 0);
            Box(mb, SkinMeshBuilder.Opaque, tC, tr, tSize, wOpen, sw, swUV, sw, swUV, sw, swUV);

            // clenched fist (as if gripping something): knuckle roll under the palm, the folded fingers against the palm
            // side and the thumb lying across them. Weighted to the fist bone, collapsed until the animator clenches it.
            float palmIn = -sx; // towards the palm side
            Vector3 kC = palmC + new Vector3(palmIn * 0.004f * hs, -palmSize.y * 0.5f - 0.011f * hs, 0f);
            Box(mb, SkinMeshBuilder.Opaque, kC, Quaternion.identity, new Vector3(0.040f, 0.026f, 0.080f) * hs, wFist,
                reg, new Vector4(0, 0.22f, 1, 0.42f), sw, swUV, reg, new Vector4(0, 0.4f, 1, 0.5f));
            Vector3 cC = palmC + new Vector3(palmIn * (palmSize.x * 0.5f + 0.010f * hs), -0.028f * hs, 0f);
            Box(mb, SkinMeshBuilder.Opaque, cC, Quaternion.identity, new Vector3(0.022f, 0.046f, 0.076f) * hs, wFist,
                reg, new Vector4(0, 0.02f, 1, 0.26f), sw, swUV, sw, swUV);
            Vector3 thC = palmC + new Vector3(palmIn * (palmSize.x * 0.5f + 0.024f * hs), -0.026f * hs, 0.014f * hs);
            Box(mb, SkinMeshBuilder.Opaque, thC, Quaternion.AngleAxis(sx * 12f, Vector3.up), new Vector3(0.018f, 0.019f, 0.052f) * hs, wFist,
                sw, swUV, sw, swUV, sw, swUV);
        }

        static void Shoe(BodySpec b, Skeleton sk, SkinMeshBuilder mb, int side)
        {
            int o = side == 0 ? 0 : 3;
            BoneId ft = BoneId.LFoot + o;
            var w = SkinWeight.One(ft);
            Vector3 ankle = sk.Pos[(int)ft];
            float s = b.Scale;
            float L = b.FootLen * Mathf.Lerp(1f, s, 0.5f), W = b.FootW * Mathf.Lerp(1f, s, 0.5f), H = b.ShoeH;
            float zHeel = ankle.z - 0.25f * L, zToe = zHeel + L;
            // sections along z: (z, height, width)
            float[] zs = { zHeel, zHeel + 0.22f * L, zHeel + 0.48f * L, zHeel + 0.75f * L, zToe };
            float[] hs = { H * 0.82f, H, H * 0.72f, H * 0.42f, H * 0.28f };
            float[] ws = { W * 0.78f, W * 0.92f, W, W * 1.02f, W * 0.72f };
            const int P = 6;
            var side_ = CharacterAtlas.FootSide;
            var topR = CharacterAtlas.FootTop;
            var sole = b.Barefoot ? CharacterAtlas.FootSide : CharacterAtlas.SwatchDark;
            float cx = ankle.x;
            Vector3 Pt(int sec, int j)
            {
                float hw = ws[sec] * 0.5f, h = hs[sec];
                switch (j)
                {
                    case 0: return new Vector3(cx - hw, 0.002f, zs[sec]);
                    case 1: return new Vector3(cx - hw, h * 0.55f, zs[sec]);
                    case 2: return new Vector3(cx - hw * 0.55f, h, zs[sec]);
                    case 3: return new Vector3(cx + hw * 0.55f, h, zs[sec]);
                    case 4: return new Vector3(cx + hw, h * 0.55f, zs[sec]);
                    default: return new Vector3(cx + hw, 0.002f, zs[sec]);
                }
            }
            for (int sec = 0; sec < zs.Length - 1; sec++)
            {
                float u0 = (zs[sec] - zHeel) / L, u1 = (zs[sec + 1] - zHeel) / L;
                for (int j = 0; j < P; j++)
                {
                    int jn = (j + 1) % P;
                    // quad seen from outside: profile goes counter-clockwise when seen from the toe (+Z)...
                    Vector3 a = Pt(sec, j), d = Pt(sec + 1, j), bq = Pt(sec, jn), c = Pt(sec + 1, jn);
                    if (j == 2)
                    {
                        // top: toe at v = 0, ankle at v = 1
                        float va = 1f - u0, vb = 1f - u1;
                        mb.FlatQuad(SkinMeshBuilder.Opaque, a, d, c, bq, topR.UV(0f, va), topR.UV(0f, vb), topR.UV(1f, vb), topR.UV(1f, va), w);
                    }
                    else if (j == 5)
                    {
                        mb.FlatQuad(SkinMeshBuilder.Opaque, a, d, c, bq, sole.UV(0.2f, 0.2f), sole.UV(0.2f, 0.8f), sole.UV(0.8f, 0.8f), sole.UV(0.8f, 0.2f), w);
                    }
                    else
                    {
                        float ya = a.y / H, yb = bq.y / H, yd = d.y / H, yc = c.y / H;
                        mb.FlatQuad(SkinMeshBuilder.Opaque, a, d, c, bq, side_.UV(u0, ya), side_.UV(u1, yd), side_.UV(u1, yc), side_.UV(u0, yb), w);
                    }
                }
            }
            // heel and toe caps (fans)
            for (int end = 0; end < 2; end++)
            {
                int sec = end == 0 ? 0 : zs.Length - 1;
                Vector3 n = end == 0 ? Vector3.back : Vector3.forward;
                Vector3 center = new Vector3(cx, hs[sec] * 0.45f, zs[sec]);
                float uc = end == 0 ? 0.02f : 0.98f;
                int ci = mb.Add(center, n, side_.UV(uc, 0.45f), w);
                int[] ids = new int[P];
                for (int j = 0; j < P; j++) ids[j] = mb.Add(Pt(sec, j), n, side_.UV(uc, Pt(sec, j).y / H), w);
                for (int j = 0; j < P; j++)
                {
                    int jn = (j + 1) % P;
                    if (end == 0) mb.Tri(SkinMeshBuilder.Opaque, ci, ids[j], ids[jn]);
                    else mb.Tri(SkinMeshBuilder.Opaque, ci, ids[jn], ids[j]);
                }
            }
        }

        // ------------------------------------------------------------------------------------------ hair
        static void SpikyHair(BodySpec b, Skeleton sk, SkinMeshBuilder mb)
        {
            var w = SkinWeight.One(BoneId.Head);
            var reg = CharacterAtlas.Hair;
            float hh = b.HeadH;
            float axisZ = HeadAxisZ(b);
            // tufts: (theta, yRel, tilt back deg, size)
            float[] tufts =
            {
                5f, 0.86f, 25f, 1.0f,    32f, 0.83f, 34f, 0.85f,  -22f, 0.85f, 28f, 0.95f,
                -8f, 0.97f, 55f, 1.15f,  58f, 0.80f, 50f, 0.75f,  -62f, 0.81f, 42f, 0.8f,
                118f, 0.85f, 72f, 0.8f,  -125f, 0.87f, 66f, 0.9f,  178f, 0.88f, 85f, 0.85f,
                85f, 0.9f, 60f, 0.7f,    -88f, 0.92f, 55f, 0.75f,  150f, 0.95f, 78f, 0.7f,
            };
            for (int t = 0; t < tufts.Length; t += 4)
            {
                float th = tufts[t], yRel = tufts[t + 1], tilt = tufts[t + 2], size = tufts[t + 3] * b.Scale;
                Ring r = HeadRingAt(b, yRel);
                Vector2 q = RingPoint(r, th, b.HeadSquare);
                Vector3 basePos = new Vector3(q.x * hh, sk.ChinY + yRel * hh, q.y * hh + axisZ);
                Vector3 outward = new Vector3(Mathf.Sin(th * Mathf.Deg2Rad), 0, Mathf.Cos(th * Mathf.Deg2Rad));
                Vector3 dir = Vector3.Slerp(Vector3.up, outward, 0.35f);
                dir = Quaternion.AngleAxis(-tilt * 0.5f, Vector3.Cross(Vector3.up, outward).sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, outward).normalized : Vector3.right) * dir;
                dir = (dir + Vector3.back * 0.35f).normalized;
                Vector3 tip = basePos + dir * 0.068f * size;
                Frame(dir, out Vector3 rt, out Vector3 fw);
                float bw = 0.026f * size;
                Vector3 c0 = basePos - dir * 0.01f;
                Vector3 p0 = c0 + rt * bw + fw * bw, p1 = c0 - rt * bw + fw * bw, p2 = c0 - rt * bw - fw * bw, p3 = c0 + rt * bw - fw * bw;
                Vector3[] ps = { p0, p1, p2, p3 };
                for (int j = 0; j < 4; j++)
                {
                    Vector3 a = ps[j], bb = ps[(j + 1) % 4];
                    Vector3 n = Vector3.Cross(tip - a, bb - a).normalized;
                    // make sure it faces outward
                    if (Vector3.Dot(n, (a + bb) * 0.5f - c0) < 0) { var tmp = a; a = bb; bb = tmp; n = -n; }
                    int i0 = mb.Add(a, n, reg.UV(0.1f + 0.2f * j, 0.95f), w);
                    int i1 = mb.Add(tip, n, reg.UV(0.2f + 0.2f * j, 0.35f), w);
                    int i2 = mb.Add(bb, n, reg.UV(0.3f + 0.2f * j, 0.95f), w);
                    mb.Tri(SkinMeshBuilder.Opaque, i0, i1, i2);
                }
            }
        }

        /// <summary>Long hair: back panel + two front locks, double sided cutout (Extra region).</summary>
        static void LongHair(BodySpec b, Skeleton sk, SkinMeshBuilder mb, bool wavy)
        {
            var reg = CharacterAtlas.Extra;
            float hh = b.HeadH, s = b.Scale;
            float axisZ = HeadAxisZ(b);
            Ring head = HeadRingAt(b, 0.5f);
            Ring torsoNeck = b.Torso[8], torsoSh = b.Torso[7], torsoArm = b.Torso[6], torsoChest = b.Torso[5];
            float yShoulder = Mathf.Lerp(b.CrotchY, b.NeckY, CharacterAtlas.TorsoT[7]);
            float yArmpit = Mathf.Lerp(b.CrotchY, b.NeckY, CharacterAtlas.TorsoT[6]);
            float yEnd = wavy ? b.ChestY - 0.05f * s : b.ChestY - 0.02f * s;
            if (b.BobHair)
            {
                // short bob: the panel ends just under the jaw line at the back of the neck
                yShoulder = b.NeckY - 0.01f * s; yArmpit = b.NeckY - 0.035f * s; yEnd = b.NeckY - 0.06f * s;
            }
            // rows: y, half-width, back depth (positive = behind), centre z, weight
            float headW = head.W * hh + 0.022f * s, headB = head.B * hh + 0.022f * s;
            float[] ys = { sk.ChinY + 0.80f * hh, sk.ChinY + 0.42f * hh, sk.ChinY + 0.0f * hh, b.NeckY, yShoulder - 0.03f * s, yArmpit - 0.03f * s, yEnd };
            float[] hw = { headW * 0.92f, headW * 1.02f, headW * 0.95f, 0.125f * s, 0.14f * s, 0.14f * s, wavy ? 0.15f * s : 0.13f * s };
            float[] bd = { headB * 0.95f, headB * 1.02f, headB * 0.9f, torsoNeck.B + 0.06f * s, torsoSh.B + 0.035f * s, torsoArm.B + 0.025f * s, torsoChest.B + 0.03f * s };
            float[] vz = { 1f, 0.85f, 0.65f, 0.5f, 0.36f, 0.2f, 0f };
            BoneId[] wb = { BoneId.Head, BoneId.Head, BoneId.Head, BoneId.Neck, BoneId.Chest, BoneId.Chest, BoneId.Chest };
            bool sweptBob = b.BobHair && b.FringeSweep > 0f;
            if (sweptBob)
            {
                // (iteration 2) jaw-length bob: the back hangs straight down from the skull to the nape, all on the head
                ys = new[] { sk.ChinY + 0.80f * hh, sk.ChinY + 0.42f * hh, sk.ChinY + 0.05f * hh, sk.ChinY - 0.12f * hh,
                             sk.ChinY - 0.24f * hh, sk.ChinY - 0.32f * hh, sk.ChinY - 0.38f * hh };
                hw = new[] { headW * 0.92f, headW * 1.04f, headW * 1.02f, headW * 0.99f, headW * 0.97f, headW * 0.97f, headW * 0.99f };
                bd = new[] { headB * 0.95f, headB * 1.03f, headB * 0.98f, headB * 0.92f, headB * 0.88f, headB * 0.87f, headB * 0.88f };
            }
            const int C = 6;
            int first = mb.V.Count;
            mb.BeginPart();
            for (int r = 0; r < ys.Length; r++)
            {
                SkinWeight w = sweptBob ? (r >= 5 ? SkinWeight.Two(BoneId.Head, BoneId.Neck, 0.2f) : SkinWeight.One(BoneId.Head))
                    : r == 2 ? SkinWeight.Two(BoneId.Head, BoneId.Neck, 0.4f)
                    : r == 3 ? SkinWeight.Two(BoneId.Neck, BoneId.Head, 0.35f)
                    : r == 4 ? SkinWeight.Two(BoneId.Chest, BoneId.Neck, 0.4f)
                    : SkinWeight.One(wb[r]);
                for (int c = 0; c <= C; c++)
                {
                    float f = (float)c / C;              // 0 = right side, 1 = left side
                    float ang = Mathf.Lerp(-95f, 95f, f) * Mathf.Deg2Rad; // around the back
                    float x = Mathf.Sin(ang) * hw[r] * (r >= 3 ? 1f : 1f);
                    float z = -Mathf.Cos(ang) * bd[r] + (r <= 2 || sweptBob ? axisZ : -0.01f * s);
                    if (r >= 3 && !sweptBob) z = Mathf.Lerp(z, -bd[r] * 0.55f, Mathf.Abs(Mathf.Sin(ang)) * 0.5f);
                    if (wavy && r >= 3) x += Mathf.Sin(f * 9f + r) * 0.008f * s;
                    // x mirrored: f=0 is the character's right (+X)
                    mb.Add(new Vector3(-x, ys[r], z), reg.UV(1f - f, vz[r]), w);
                }
            }
            int row = C + 1;
            for (int r = 0; r < ys.Length - 1; r++)
                for (int c = 0; c < C; c++)
                {
                    int a = first + r * row + c;
                    // seen from behind (outside): column 0 is on the viewer's right
                    mb.Quad(SkinMeshBuilder.Cutout, a + row + 1, a + 1, a, a + row);
                }
            mb.EndSmoothPart();
            // front locks over the shoulders
            for (int side = 0; side < 2; side++)
            {
                float sx = side == 0 ? -1f : 1f;
                float yTop = sk.ChinY + 0.62f * hh;
                Vector3 t0 = new Vector3(sx * (head.W * hh + 0.012f * s), yTop, axisZ + 0.01f * s);
                Vector3 t1 = new Vector3(sx * (head.W * hh * 0.9f + 0.016f * s), sk.ChinY + 0.1f * hh, axisZ - 0.005f * s);
                float frontZ = torsoSh.F + 0.03f * s;
                Vector3 t2 = new Vector3(sx * (wavy ? 0.115f : 0.09f) * s, b.NeckY - 0.02f * s, wavy ? frontZ * 0.6f : frontZ * 0.4f);
                Vector3 t3 = new Vector3(sx * (wavy ? 0.12f : 0.1f) * s, yEnd + (wavy ? 0.06f : 0.1f) * s, torsoChest.F * 0.75f + b.Bust * 0.6f + 0.03f * s);
                float[] widths = wavy ? new[] { 0.05f * s, 0.075f * s, 0.095f * s, 0.10f * s } : new[] { 0.045f * s, 0.06f * s, 0.075f * s, 0.07f * s };
                if (b.BobHair && b.FringeSweep > 0f)
                {
                    // jaw-length bob: the side locks frame the cheeks and curl in under the jaw line
                    Ring hc = HeadRingAt(b, 0.25f);
                    t0 = new Vector3(sx * (head.W * hh + 0.012f * s), yTop, axisZ + 0.012f * s);
                    t1 = new Vector3(sx * (hc.W * hh + 0.014f * s), sk.ChinY + 0.25f * hh, axisZ + 0.010f * s);
                    t2 = new Vector3(sx * (hc.W * hh + 0.008f * s), sk.ChinY - 0.04f * hh, axisZ + 0.004f * s);
                    t3 = new Vector3(sx * (hc.W * hh - 0.004f * s), sk.ChinY - 0.15f * hh, axisZ + 0.006f * s);
                    widths = new[] { 0.045f * s, 0.06f * s, 0.06f * s, 0.05f * s };
                }
                Vector3[] spine = { t0, t1, t2, t3 };
                SkinWeight[] ws = { SkinWeight.One(BoneId.Head), SkinWeight.Two(BoneId.Head, BoneId.Neck, 0.3f), SkinWeight.Two(BoneId.Chest, BoneId.Neck, 0.35f), SkinWeight.One(BoneId.Chest) };
                float[] vs = { 0.95f, 0.65f, 0.4f, 0f };
                int f0 = mb.V.Count;
                mb.BeginPart();
                for (int r = 0; r < spine.Length; r++)
                {
                    Vector3 along = r < spine.Length - 1 ? spine[r + 1] - spine[r] : spine[r] - spine[r - 1];
                    // strip faces forward (hair lying over the shoulder / framing the face); the bob's locks lie
                    // flat against the side of the head instead (facing out and a little forward)
                    Vector3 facing = sweptBob ? new Vector3(sx, 0f, 0.45f).normalized : Vector3.forward;
                    Vector3 across = Vector3.Cross(along.normalized, facing);
                    if (across.sqrMagnitude < 1e-6f) across = Vector3.right;
                    across.Normalize();
                    if (sweptBob ? across.z < 0 : across.x < 0) across = -across;
                    float u0 = side == 0 ? 0.0f : 0.6f, u1 = u0 + 0.4f;
                    mb.Add(spine[r] - across * widths[r] * 0.5f, reg.UV(u0, vs[r]), ws[r]);
                    mb.Add(spine[r] + across * widths[r] * 0.5f, reg.UV(u1, vs[r]), ws[r]);
                }
                for (int r = 0; r < spine.Length - 1; r++)
                {
                    int a = f0 + r * 2;
                    mb.Quad(SkinMeshBuilder.Cutout, a + 3, a + 1, a, a + 2);
                }
                mb.EndSmoothPart();
            }
        }

        /// <summary>
        /// Curl clumps of the curly mop: theta (deg, + = right), yRel on the head, radius (m at 1.8 m), height above the
        /// scalp (x radius), droop (deg, tips the clump down the head), stretch along the head (curls hang).
        /// </summary>
        static readonly float[] CurlClumps =
        {
            // fringe hanging unevenly over the forehead to the brows, loose curls lower down
            3f, 0.650f, 0.026f, 0.12f, 50f, 1.42f,     23f, 0.672f, 0.023f, 0.10f, 44f, 1.30f,    -16f, 0.660f, 0.025f, 0.12f, 48f, 1.38f,
            42f, 0.704f, 0.023f, 0.05f, 36f, 1.25f,    -37f, 0.688f, 0.024f, 0.06f, 40f, 1.30f,
            -6f, 0.605f, 0.016f, 0.45f, 62f, 1.50f,    14f, 0.618f, 0.015f, 0.45f, 60f, 1.45f,    -27f, 0.628f, 0.014f, 0.40f, 56f, 1.40f,
            // the tall front of the mop, rising in uneven tiers
            4f, 0.800f, 0.033f, 0.15f, 14f, 1.10f,     30f, 0.815f, 0.032f, 0.10f, 14f, 1.10f,    -26f, 0.808f, 0.033f, 0.12f, 14f, 1.10f,
            55f, 0.780f, 0.030f, 0.08f, 18f, 1.10f,    -56f, 0.786f, 0.030f, 0.08f, 18f, 1.10f,
            16f, 0.900f, 0.035f, 0.20f, 6f, 1.00f,     -14f, 0.905f, 0.035f, 0.20f, 6f, 1.00f,    46f, 0.890f, 0.033f, 0.12f, 8f, 1.00f,
            -48f, 0.886f, 0.033f, 0.12f, 8f, 1.00f,
            // top: a rounded dome of curls
            0f, 0.985f, 0.036f, 0.00f, 0f, 1.00f,      48f, 0.955f, 0.034f, 0.10f, 4f, 1.00f,     -44f, 0.958f, 0.034f, 0.10f, 4f, 1.00f,
            100f, 0.945f, 0.035f, 0.12f, 5f, 1.00f,    -95f, 0.950f, 0.035f, 0.12f, 5f, 1.00f,    140f, 0.952f, 0.034f, 0.10f, 4f, 1.00f,
            -138f, 0.960f, 0.034f, 0.10f, 4f, 1.00f,   178f, 0.945f, 0.035f, 0.12f, 5f, 1.00f,
            // full sides and back
            80f, 0.790f, 0.034f, 0.12f, 18f, 1.10f,    108f, 0.805f, 0.035f, 0.10f, 18f, 1.10f,   135f, 0.790f, 0.035f, 0.08f, 15f, 1.12f,
            160f, 0.815f, 0.035f, 0.06f, 15f, 1.12f,   -172f, 0.785f, 0.035f, 0.06f, 15f, 1.12f,  -146f, 0.810f, 0.035f, 0.08f, 15f, 1.12f,
            -118f, 0.792f, 0.035f, 0.10f, 18f, 1.10f,  -82f, 0.798f, 0.034f, 0.12f, 18f, 1.10f,
            // temples, above the ears (the ears stay visible under the curls)
            64f, 0.660f, 0.027f, 0.12f, 25f, 1.20f,    88f, 0.640f, 0.026f, 0.14f, 25f, 1.20f,    -66f, 0.664f, 0.027f, 0.12f, 25f, 1.20f,
            -89f, 0.642f, 0.026f, 0.14f, 25f, 1.20f,   76f, 0.720f, 0.026f, 0.15f, 20f, 1.10f,    -77f, 0.724f, 0.026f, 0.15f, 20f, 1.10f,
            // back of the head, behind the ears, nape
            120f, 0.560f, 0.031f, 0.06f, 15f, 1.18f,   148f, 0.585f, 0.032f, 0.06f, 15f, 1.18f,   176f, 0.555f, 0.032f, 0.06f, 15f, 1.18f,
            -152f, 0.575f, 0.032f, 0.06f, 15f, 1.18f,  -124f, 0.552f, 0.031f, 0.06f, 15f, 1.18f,
            106f, 0.450f, 0.021f, 0.05f, 20f, 1.20f,   -107f, 0.455f, 0.021f, 0.05f, 20f, 1.20f,
            152f, 0.300f, 0.024f, 0.05f, 20f, 1.20f,   -178f, 0.290f, 0.024f, 0.05f, 20f, 1.20f,  -150f, 0.310f, 0.024f, 0.05f, 20f, 1.20f,
        };

        /// <summary>
        /// (secret, Prisoner8) A mop of tight curls: low-poly curl clumps (6-sided domes whose open base is buried in the
        /// thickened scalp) over the top, sides and back, and a fringe of curls hanging over the forehead to the brows.
        /// Opaque; each clump shows one of the 2 x 2 curl tiles of the Hair region, projected along its axis.
        /// </summary>
        static void CurlyHair(BodySpec b, Skeleton sk, SkinMeshBuilder mb)
        {
            var w = SkinWeight.One(BoneId.Head);
            var reg = CharacterAtlas.Hair;
            float s = b.Scale;
            const int Sides = 6;
            const float Lat1 = 48f * Mathf.Deg2Rad, Lat2 = -14f * Mathf.Deg2Rad;
            mb.BeginPart();
            for (int c = 0, idx = 0; c + 5 < CurlClumps.Length; c += 6, idx++)
            {
                float th = CurlClumps[c], yRel = CurlClumps[c + 1], r = CurlClumps[c + 2] * s;
                float lift = CurlClumps[c + 3], droop = CurlClumps[c + 4] * Mathf.Deg2Rad, stretch = CurlClumps[c + 5];
                Vector3 p = ScalpPoint(b, sk, th, yRel, out Vector3 n);
                // tip the axis down the head: towards the downhill tangent
                Vector3 down = Vector3.down - n * Vector3.Dot(Vector3.down, n);
                Vector3 axis = down.sqrMagnitude > 1e-4f ? (n * Mathf.Cos(droop) + down.normalized * Mathf.Sin(droop)).normalized : n;
                Vector3 t1 = Vector3.Cross(Vector3.up, axis);
                t1 = t1.sqrMagnitude > 1e-4f ? t1.normalized : Vector3.right;
                Vector3 t2 = Vector3.Cross(axis, t1);   // up the head
                Vector3 right = -t1;                    // seen from outside, looking down the axis
                float jit = Hash01(idx * 3 + 1), spin = Hash01(idx * 3 + 2) * Mathf.PI * 2f;
                float rw = r * (0.88f + 0.24f * jit), rh = r * stretch * (1.06f - 0.12f * jit), rd = r * (0.70f + 0.16f * Hash01(idx * 7 + 5));
                Vector3 centre = p + axis * (lift * r);
                int tile = (int)(Hash01(idx * 3 + 3) * 4f) & 3;
                float tu = (tile & 1) * 0.5f, tv = (tile >> 1) * 0.5f;
                Vector2 Uv(float lx, float ly)
                {
                    // planar projection along the axis, the tile turned by spin so neighbouring clumps differ
                    float cs = Mathf.Cos(spin), sn = Mathf.Sin(spin);
                    float x = lx * cs - ly * sn, y = lx * sn + ly * cs;
                    return reg.UV(tu + 0.25f + 0.235f * x, tv + 0.25f + 0.235f * y);
                }
                int pole = mb.Add(centre + axis * rd, Uv(0f, 0f), w);
                int ring1 = mb.V.Count;
                for (int j = 0; j < Sides; j++)
                {
                    float a = (j + 0.5f * (idx & 1)) * Mathf.PI * 2f / Sides;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    mb.Add(centre + axis * (rd * Mathf.Sin(Lat1)) + (right * (ca * rw) + t2 * (sa * rh)) * Mathf.Cos(Lat1), Uv(ca * Mathf.Cos(Lat1), sa * Mathf.Cos(Lat1)), w);
                }
                int ring2 = mb.V.Count;
                for (int j = 0; j < Sides; j++)
                {
                    float a = (j + 0.5f * (idx & 1)) * Mathf.PI * 2f / Sides;
                    float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                    mb.Add(centre + axis * (rd * Mathf.Sin(Lat2)) + (right * (ca * rw) + t2 * (sa * rh)) * Mathf.Cos(Lat2), Uv(ca, sa), w);
                }
                for (int j = 0; j < Sides; j++)
                {
                    int jn = (j + 1) % Sides;
                    // clockwise seen from outside (angles run counter-clockwise on screen)
                    mb.Tri(SkinMeshBuilder.Opaque, pole, ring1 + jn, ring1 + j);
                    mb.Tri(SkinMeshBuilder.Opaque, ring2 + j, ring1 + j, ring1 + jn);
                    mb.Tri(SkinMeshBuilder.Opaque, ring2 + j, ring1 + jn, ring2 + jn);
                }
            }
            mb.EndSmoothPart();
        }

        static float Hash01(int i)
        {
            float x = Mathf.Sin(i * 12.9898f + 4.1414f) * 43758.5453f;
            return x - Mathf.Floor(x);
        }

        /// <summary>Head vertex of Head() at ring k for any theta (the hair push included; no nose / ear bumps).</summary>
        static Vector3 HeadSurface(BodySpec b, Skeleton sk, int k, float th)
        {
            var Y = CharacterAtlas.HeadRingY;
            float hh = b.HeadH, axisZ = HeadAxisZ(b);
            Vector2 q = RingPoint(b.HeadRings[k], th, k <= 1 ? 2f : b.HeadSquare);
            float x = q.x * hh, z = q.y * hh + axisZ, y = sk.ChinY + Y[k] * hh;
            float d = HairThickness(b, th, Y[k]) * b.Scale;
            if (d > 0f)
            {
                Vector2 dir = k == Y.Length - 1 ? new Vector2(0, -0.2f) : new Vector2(x, z - axisZ);
                float len = dir.magnitude;
                if (len > 1e-5f) { x += dir.x / len * d; z += dir.y / len * d; }
                if (k == Y.Length - 1) y += d * 0.9f;
                if (k == Y.Length - 2) y += d * 0.5f;
            }
            return new Vector3(x, y, z);
        }

        /// <summary>Point on the (hair-thickened) head mesh at theta / yRel, interpolated like the mesh's quads, and its outward normal.</summary>
        static Vector3 ScalpPoint(BodySpec b, Skeleton sk, float th, float yRel, out Vector3 normal)
        {
            Vector3 p = ScalpPoint(b, sk, th, yRel);
            float y0 = Mathf.Max(yRel - 0.03f, -0.5f), y1 = Mathf.Min(yRel + 0.03f, 1f);
            Vector3 alongY = ScalpPoint(b, sk, th, y1) - ScalpPoint(b, sk, th, y0);
            float tr = th * Mathf.Deg2Rad;
            Vector3 alongTh = new Vector3(Mathf.Cos(tr), 0f, -Mathf.Sin(tr));   // d/dtheta of (sin, 0, cos)
            normal = Vector3.Cross(alongY, alongTh);
            Vector3 centre = new Vector3(0f, sk.ChinY + 0.5f * b.HeadH, HeadAxisZ(b));
            if (Vector3.Dot(normal, p - centre) < 0f) normal = -normal;
            normal = normal.sqrMagnitude > 1e-10f ? normal.normalized : Vector3.up;
            return p;
        }

        static Vector3 ScalpPoint(BodySpec b, Skeleton sk, float th, float yRel)
        {
            var Y = CharacterAtlas.HeadRingY;
            int k = 0;
            while (k < Y.Length - 2 && yRel > Y[k + 1]) k++;
            float ty = Mathf.Clamp01(Mathf.InverseLerp(Y[k], Y[k + 1], yRel));
            // ring vertices sit every 360 / HeadSides degrees (theta_i = 180 - 30 i)
            float step = 360f / CharacterAtlas.HeadSides;
            float fi = (180f - th) / step;
            int i0 = Mathf.FloorToInt(fi);
            float ti = fi - i0;
            float th0 = 180f - i0 * step, th1 = th0 - step;
            Vector3 a = Vector3.Lerp(HeadSurface(b, sk, k, th0), HeadSurface(b, sk, k, th1), ti);
            Vector3 c = Vector3.Lerp(HeadSurface(b, sk, k + 1, th0), HeadSurface(b, sk, k + 1, th1), ti);
            return Vector3.Lerp(a, c, ty);
        }

        // ------------------------------------------------------------------------------------------ glasses
        static void Glasses(BodySpec b, Skeleton sk, SkinMeshBuilder mb)
        {
            var w = SkinWeight.One(BoneId.Head);
            var reg = CharacterAtlas.Glasses;
            float hh = b.HeadH, s = b.Scale;
            float axisZ = HeadAxisZ(b);
            Ring r = HeadRingAt(b, CharacterAtlas.EyeY);
            float yC = sk.ChinY + (CharacterAtlas.EyeY + 0.01f) * hh;
            float halfH = (b.Glasses == GlassesStyle.Round ? 0.021f : 0.018f) * s;
            float outerX = r.W * hh * 0.98f + 0.004f * s;
            float[] xs = { outerX, outerX * 0.32f, -outerX * 0.32f, -outerX };
            float[] us = { 0f, 0.34f, 0.66f, 1f };
            int first = mb.V.Count;
            for (int i = 0; i < 4; i++)
            {
                float x = xs[i];
                float ex = Mathf.Clamp(x / (r.W * hh), -1f, 1f);
                float z = axisZ + r.C * hh + r.F * hh * Mathf.Sqrt(Mathf.Max(0f, 1f - ex * ex * 0.9f)) + 0.011f * s;
                if (i == 0 || i == 3) z = Mathf.Max(z, axisZ + r.F * hh * 0.35f);
                Vector3 n = new Vector3(x * 0.5f, 0, 1).normalized;
                mb.Add(new Vector3(x, yC - halfH, z), n, reg.UV(us[i], 0f), w);
                mb.Add(new Vector3(x, yC + halfH, z), n, reg.UV(us[i], 1f), w);
            }
            for (int i = 0; i < 3; i++)
            {
                int a = first + i * 2;
                mb.Quad(SkinMeshBuilder.Cutout, a, a + 1, a + 3, a + 2);
            }
            // temple arms back to the ears
            var dark = CharacterAtlas.SwatchDark;
            for (int side = 0; side < 2; side++)
            {
                int i = side == 0 ? 0 : 3;
                float x = xs[i];
                float ex = Mathf.Clamp(x / (r.W * hh), -1f, 1f);
                float z0 = Mathf.Max(axisZ + r.C * hh + r.F * hh * Mathf.Sqrt(Mathf.Max(0f, 1f - ex * ex * 0.9f)) + 0.011f * s, axisZ + r.F * hh * 0.35f);
                float sx = Mathf.Sign(x);
                Vector3 a0 = new Vector3(x, yC + halfH * 0.5f, z0);
                Vector3 a1 = new Vector3(sx * (r.W * hh + 0.006f * s), yC + halfH * 0.3f, axisZ - 0.01f * s);
                float th = 0.0035f * s;
                if (sx > 0)
                    mb.FlatQuad(SkinMeshBuilder.Cutout, a1 + Vector3.down * th, a1 + Vector3.up * th, a0 + Vector3.up * th, a0 + Vector3.down * th,
                        dark, 0.2f, 0.2f, 0.8f, 0.8f, w);
                else
                    mb.FlatQuad(SkinMeshBuilder.Cutout, a0 + Vector3.down * th, a0 + Vector3.up * th, a1 + Vector3.up * th, a1 + Vector3.down * th,
                        dark, 0.2f, 0.2f, 0.8f, 0.8f, w);
            }
        }

        // ------------------------------------------------------------------------------------------ Omar
        static void SackSkirt(BodySpec b, Skeleton sk, SkinMeshBuilder mb)
        {
            const int N = 12;
            var reg = CharacterAtlas.Skirt;
            float hh = b.HeadH, s = b.Scale;
            float axisZ = HeadAxisZ(b);
            Ring neck = b.HeadRings[1];
            float yTop = sk.ChinY - 0.24f * hh;
            float yMid = b.NeckY + 0.005f;
            float yBot = b.NeckY - 0.045f * s;
            Ring top = new Ring(neck.W * hh + 0.006f, neck.F * hh + 0.006f, neck.B * hh + 0.006f, neck.C * hh + axisZ);
            Ring mid = new Ring(0.105f * s, 0.10f * s, 0.10f * s, -0.008f * s);
            Ring bot = new Ring(0.15f * s, 0.125f * s, 0.12f * s, -0.004f * s);
            Ring[] rings = { top, mid, bot };
            float[] ys = { yTop, yMid, yBot };
            float[] vs = { 1f, 0.5f, 0f };
            SkinWeight[] ws = { SkinWeight.Two(BoneId.Neck, BoneId.Head, 0.4f), SkinWeight.Two(BoneId.Chest, BoneId.Neck, 0.5f), SkinWeight.One(BoneId.Chest) };
            mb.BeginPart();
            int first = mb.V.Count;
            for (int k = 0; k < 3; k++)
                for (int i = 0; i <= N; i++)
                {
                    float th = Theta(i, N);
                    Vector2 q = RingPoint(rings[k], th, 2f);
                    float wob = k == 2 ? (Mathf.Sin(i * 2.7f) * 0.008f * s) : 0f;
                    mb.Add(new Vector3(q.x * (1 + wob * 4), ys[k] + wob, q.y), reg.UV((float)i / N, vs[k]), ws[k]);
                }
            int row = N + 1;
            for (int k = 0; k < 2; k++)
                for (int i = 0; i < N; i++)
                {
                    int a = first + (k + 1) * row + i; // lower ring first (bottom-left)
                    mb.Quad(SkinMeshBuilder.Cutout, a, a - row, a - row + 1, a + 1);
                }
            mb.EndSmoothPart();
        }

        static void Noose(BodySpec b, Skeleton sk, SkinMeshBuilder mb)
        {
            var reg = CharacterAtlas.Rope;
            float hh = b.HeadH, s = b.Scale;
            float axisZ = HeadAxisZ(b);
            Ring neck = b.HeadRings[1];
            float y = sk.ChinY - 0.22f * hh;
            float rr = 0.012f * s;
            const int N = 12, M = 5;
            var w = SkinWeight.Two(BoneId.Neck, BoneId.Head, 0.45f);
            Ring ring = new Ring(neck.W * hh + rr * 0.9f, neck.F * hh + rr * 0.9f, neck.B * hh + rr * 0.9f, neck.C * hh + axisZ);
            mb.BeginPart();
            int first = mb.V.Count;
            for (int i = 0; i <= N; i++)
            {
                float th = Theta(i, N);
                Vector2 q = RingPoint(ring, th, 2f);
                Vector3 c = new Vector3(q.x, y + Mathf.Sin(th * Mathf.Deg2Rad * 2f) * 0.004f, q.y);
                Vector3 outward = new Vector3(q.x, 0, q.y - ring.C).normalized;
                for (int j = 0; j <= M; j++)
                {
                    float a = (float)j / M * Mathf.PI * 2f;
                    Vector3 p = c + outward * (Mathf.Cos(a) * rr) + Vector3.up * (Mathf.Sin(a) * rr);
                    mb.Add(p, reg.UV((float)j / M, (float)i / N), w);
                }
            }
            int row = M + 1;
            for (int i = 0; i < N; i++)
                for (int j = 0; j < M; j++)
                {
                    int a = first + i * row + j;
                    mb.Quad(SkinMeshBuilder.Opaque, a, a + 1, a + row + 1, a + row);
                }
            mb.EndSmoothPart();
            // knot at the left-back + dangling end down the back
            Vector2 kq = RingPoint(ring, -140f, 2f);
            Vector3 knot = new Vector3(kq.x, y - 0.005f, kq.y);
            Vector3[] path =
            {
                knot + new Vector3(0, 0.012f, 0), knot + new Vector3(-0.01f, -0.03f, -0.025f) * s,
                knot + new Vector3(0.0f, -0.12f, -0.07f) * s, knot + new Vector3(0.025f, -0.26f, -0.08f) * s,
            };
            SkinWeight[] ws = { w, SkinWeight.Two(BoneId.Neck, BoneId.Chest, 0.4f), SkinWeight.One(BoneId.Chest), SkinWeight.One(BoneId.Chest) };
            float[] radii = { rr * 1.9f, rr * 1.4f, rr, rr * 0.9f };
            mb.BeginPart();
            first = mb.V.Count;
            const int R = 5;
            for (int k = 0; k < path.Length; k++)
            {
                Vector3 axis = k < path.Length - 1 ? path[k + 1] - path[k] : path[k] - path[k - 1];
                Frame(-axis, out Vector3 right, out Vector3 fwd);
                for (int j = 0; j <= R; j++)
                {
                    float a = (float)j / R * Mathf.PI * 2f;
                    mb.Add(path[k] + right * (Mathf.Sin(a) * radii[k]) + fwd * (Mathf.Cos(a) * radii[k]), reg.UV((float)j / R, 1f - k * 0.3f), ws[k]);
                }
            }
            row = R + 1;
            for (int k = 0; k < path.Length - 1; k++)
                for (int j = 0; j < R; j++)
                {
                    int a = first + k * row + j;
                    mb.Quad(SkinMeshBuilder.Opaque, a, a + row, a + row + 1, a + 1);
                }
            mb.EndSmoothPart();
        }

        static void Apron(BodySpec b, Skeleton sk, SkinMeshBuilder mb)
        {
            var reg = CharacterAtlas.Extra;
            float s = b.Scale;
            float yAt(float t) => Mathf.Lerp(b.CrotchY, b.NeckY, t);
            // rows: y, half width, source ring (-1 = below the torso), v
            float yBib = yAt(0.86f);
            float yBottom = b.KneeY - 0.13f * s;
            float[] ys = { yBib, yAt(0.68f), yAt(0.40f), yAt(0.26f), yAt(0.0f), Mathf.Lerp(b.CrotchY, b.KneeY, 0.5f), b.KneeY, yBottom };
            float[] hw = { 0.125f * s, 0.15f * s, 0.23f * s, 0.235f * s, 0.215f * s, 0.205f * s, 0.20f * s, 0.20f * s };
            int[] ringIdx = { 7, 5, 3, 2, -1, -1, -1, -1 };
            const int C = 6;
            mb.BeginPart();
            int first = mb.V.Count;
            float legFront = b.LegRz[7] + 0.09f * s; // hangs clear of the thick thighs
            for (int r = 0; r < ys.Length; r++)
            {
                float v = 1f - (b.NeckY - 0.0f - ys[r]) / (b.NeckY - yBottom);
                v = Mathf.Clamp01((ys[r] - yBottom) / (yBib - yBottom));
                for (int c = 0; c <= C; c++)
                {
                    float f = (float)c / C;           // 0 = character's right
                    float x = Mathf.Lerp(hw[r], -hw[r], f);
                    float z;
                    if (ringIdx[r] >= 0)
                    {
                        Ring rg = b.Torso[ringIdx[r]];
                        float ex = Mathf.Clamp(x / (rg.W * 1.02f), -0.98f, 0.98f);
                        z = rg.C + rg.F * Mathf.Sqrt(1f - ex * ex) + 0.022f * s;
                        if (Mathf.Abs(x) > rg.W) z = rg.C + 0.02f;
                        if (r == 0) z += 0.01f * s;
                    }
                    else
                    {
                        float bulge = 1f - (2f * f - 1f) * (2f * f - 1f);
                        z = legFront + 0.02f * s * bulge - 0.03f * s * (1 - bulge);
                        if (r == 4) z = Mathf.Max(z, b.Torso[1].F + 0.035f * s);
                    }
                    SkinWeight w;
                    BoneId leg = x > 0 ? BoneId.RUpperLeg : BoneId.LUpperLeg;
                    float sideK = Mathf.Abs(x) / hw[r];
                    switch (r)
                    {
                        case 0: w = SkinWeight.One(BoneId.Chest); break;
                        case 1: w = SkinWeight.Two(BoneId.Chest, BoneId.Spine, 0.25f); break;
                        case 2: w = SkinWeight.Two(BoneId.Spine, BoneId.Hips, 0.4f); break;
                        case 3: w = SkinWeight.One(BoneId.Hips); break;
                        case 4: w = SkinWeight.One(BoneId.Hips).Plus(leg, 0.2f * sideK); break;
                        default:
                            // each half of the apron goes with its own leg (no knee poking through when he strides)
                            float lw = Mathf.Lerp(0.8f, 0.97f, sideK) * (r == 5 ? 0.9f : 1f);
                            w = SkinWeight.One(BoneId.Hips).Plus(leg, lw);
                            if (sideK < 0.2f) w = SkinWeight.Two(BoneId.LUpperLeg, BoneId.RUpperLeg, 0.5f).Plus(BoneId.Hips, 0.24f); // symmetric
                            break;
                    }
                    mb.Add(new Vector3(x, ys[r], z), reg.UV(f, v), w);
                }
            }
            int row = C + 1;
            for (int r = 0; r < ys.Length - 1; r++)
                for (int c = 0; c < C; c++)
                {
                    int a = first + (r + 1) * row + c; // lower row
                    mb.Quad(SkinMeshBuilder.Cutout, a, a - row, a - row + 1, a + 1);
                }
            mb.EndSmoothPart();
            // neck straps
            for (int side = 0; side < 2; side++)
            {
                float sx = side == 0 ? -1f : 1f;
                int top = first + (side == 0 ? C : 0);
                Vector3 p0 = mb.V[top];
                Vector3 p1 = new Vector3(sx * 0.075f * s, b.NeckY + 0.015f * s, -0.035f * s);
                float tw = 0.012f * s;
                Vector3 d = new Vector3(0, 0, 0) + Vector3.right * tw;
                mb.FlatQuad(SkinMeshBuilder.Cutout, p0 - d, p1 - d, p1 + d, p0 + d, reg, 0.1f, 0.9f, 0.2f, 1f, SkinWeight.One(BoneId.Chest));
            }
        }
    }
}
