using UnityEngine;

namespace PrisonersOfOmar.Characters
{
    /// <summary>
    /// A full-body pose in a blendable parametric form (no allocations when blending).
    /// Rotations are degrees. Axial bones (hips, spine, chest, neck, head) use Unity Euler order (Y * X * Z):
    /// x = bend forward (+), y = turn right (+), z = lean left (+).
    /// Limbs use Rz * Rx * Ry: x = flex (arms / thighs: negative swings forward, knees: positive bends,
    /// elbows: negative bends, feet: positive = toes down), y = twist, z = side swing (+ = towards +X).
    /// IK goals are in the character root space. Hand goals can instead follow the chest (HandSpace = 1).
    /// </summary>
    internal sealed class PoseBuffer
    {
        public const int BoneCount = 17;
        public readonly Vector3[] Rot = new Vector3[BoneCount];
        /// <summary>Offset of the hips bone from its bind position (root space).</summary>
        public Vector3 HipsPos;

        public Vector3 AnkleL, AnkleR;          // ankle goals (root space)
        public float FootPitchL, FootPitchR;    // degrees, + = toes down
        public float FootYawL, FootYawR;
        public float LegIK;                     // 0 = FK legs, 1 = IK legs

        public Vector3 HandL, HandR;            // wrist goals, root space
        public Vector3 HandLC, HandRC;          // wrist goals, chest space
        public float HandSpaceL, HandSpaceR;    // 0 = root goal, 1 = chest goal
        public float HandIKL, HandIKR;
        public Vector3 HandRotL, HandRotR;      // hand orientation relative to the root (Euler Y*X*Z)
        public float HandRotWL, HandRotWR;
        public Vector3 ElbowHintL = new Vector3(-1, -1, -0.6f), ElbowHintR = new Vector3(1, -1, -0.6f);

        public void Clear()
        {
            for (int i = 0; i < BoneCount; i++) Rot[i] = Vector3.zero;
            HipsPos = Vector3.zero;
            AnkleL = AnkleR = Vector3.zero;
            FootPitchL = FootPitchR = FootYawL = FootYawR = 0f;
            LegIK = 0f;
            HandL = HandR = HandLC = HandRC = Vector3.zero;
            HandSpaceL = HandSpaceR = 0f;
            HandIKL = HandIKR = 0f;
            HandRotL = HandRotR = Vector3.zero;
            HandRotWL = HandRotWR = 0f;
            ElbowHintL = new Vector3(-1, -1, -0.6f);
            ElbowHintR = new Vector3(1, -1, -0.6f);
        }

        public void CopyFrom(PoseBuffer o)
        {
            for (int i = 0; i < BoneCount; i++) Rot[i] = o.Rot[i];
            HipsPos = o.HipsPos;
            AnkleL = o.AnkleL; AnkleR = o.AnkleR;
            FootPitchL = o.FootPitchL; FootPitchR = o.FootPitchR; FootYawL = o.FootYawL; FootYawR = o.FootYawR;
            LegIK = o.LegIK;
            HandL = o.HandL; HandR = o.HandR; HandLC = o.HandLC; HandRC = o.HandRC;
            HandSpaceL = o.HandSpaceL; HandSpaceR = o.HandSpaceR;
            HandIKL = o.HandIKL; HandIKR = o.HandIKR;
            HandRotL = o.HandRotL; HandRotR = o.HandRotR;
            HandRotWL = o.HandRotWL; HandRotWR = o.HandRotWR;
            ElbowHintL = o.ElbowHintL; ElbowHintR = o.ElbowHintR;
        }

        /// <summary>this = lerp(this, o, t)</summary>
        public void LerpTo(PoseBuffer o, float t)
        {
            if (t <= 0f) return;
            if (t >= 1f) { CopyFrom(o); return; }
            for (int i = 0; i < BoneCount; i++) Rot[i] = Vector3.LerpUnclamped(Rot[i], o.Rot[i], t);
            HipsPos = Vector3.LerpUnclamped(HipsPos, o.HipsPos, t);
            // goals: blend positions only where both sides use them, otherwise take the active one
            AnkleL = BlendGoal(AnkleL, LegIK, o.AnkleL, o.LegIK, t);
            AnkleR = BlendGoal(AnkleR, LegIK, o.AnkleR, o.LegIK, t);
            FootPitchL = Mathf.LerpUnclamped(FootPitchL, o.FootPitchL, t);
            FootPitchR = Mathf.LerpUnclamped(FootPitchR, o.FootPitchR, t);
            FootYawL = Mathf.LerpUnclamped(FootYawL, o.FootYawL, t);
            FootYawR = Mathf.LerpUnclamped(FootYawR, o.FootYawR, t);
            LegIK = Mathf.LerpUnclamped(LegIK, o.LegIK, t);
            HandL = BlendGoal(HandL, HandIKL * (1 - HandSpaceL), o.HandL, o.HandIKL * (1 - o.HandSpaceL), t);
            HandR = BlendGoal(HandR, HandIKR * (1 - HandSpaceR), o.HandR, o.HandIKR * (1 - o.HandSpaceR), t);
            HandLC = BlendGoal(HandLC, HandIKL * HandSpaceL, o.HandLC, o.HandIKL * o.HandSpaceL, t);
            HandRC = BlendGoal(HandRC, HandIKR * HandSpaceR, o.HandRC, o.HandIKR * o.HandSpaceR, t);
            HandSpaceL = BlendScalarWeighted(HandSpaceL, HandIKL, o.HandSpaceL, o.HandIKL, t);
            HandSpaceR = BlendScalarWeighted(HandSpaceR, HandIKR, o.HandSpaceR, o.HandIKR, t);
            HandIKL = Mathf.LerpUnclamped(HandIKL, o.HandIKL, t);
            HandIKR = Mathf.LerpUnclamped(HandIKR, o.HandIKR, t);
            HandRotL = BlendGoal(HandRotL, HandRotWL, o.HandRotL, o.HandRotWL, t);
            HandRotR = BlendGoal(HandRotR, HandRotWR, o.HandRotR, o.HandRotWR, t);
            HandRotWL = Mathf.LerpUnclamped(HandRotWL, o.HandRotWL, t);
            HandRotWR = Mathf.LerpUnclamped(HandRotWR, o.HandRotWR, t);
            ElbowHintL = Vector3.LerpUnclamped(ElbowHintL, o.ElbowHintL, t);
            ElbowHintR = Vector3.LerpUnclamped(ElbowHintR, o.ElbowHintR, t);
        }

        static Vector3 BlendGoal(Vector3 a, float wa, Vector3 b, float wb, float t)
        {
            float ka = wa * (1f - t), kb = wb * t;
            float s = ka + kb;
            if (s < 1e-4f) return Vector3.LerpUnclamped(a, b, t);
            return (a * ka + b * kb) / s;
        }

        static float BlendScalarWeighted(float a, float wa, float b, float wb, float t)
        {
            float ka = wa * (1f - t), kb = wb * t;
            float s = ka + kb;
            if (s < 1e-4f) return Mathf.LerpUnclamped(a, b, t);
            return (a * ka + b * kb) / s;
        }

        // ---------------------------------------------------------------- authoring helpers (side: 0 = left, 1 = right)
        public void Arm(int side, float flex, float abduct, float twist, float elbow)
        {
            float m = side == 0 ? -1f : 1f;
            Rot[side == 0 ? (int)BoneId.LUpperArm : (int)BoneId.RUpperArm] = new Vector3(flex, twist * m, abduct * m);
            Rot[side == 0 ? (int)BoneId.LLowerArm : (int)BoneId.RLowerArm] = new Vector3(-elbow, 0f, 0f);
        }

        public void HandFK(int side, float flex, float twist, float dev)
        {
            float m = side == 0 ? -1f : 1f;
            Rot[side == 0 ? (int)BoneId.LHand : (int)BoneId.RHand] = new Vector3(flex, twist * m, dev * m);
        }

        public void Leg(int side, float flex, float abduct, float twist, float knee, float foot)
        {
            float m = side == 0 ? -1f : 1f;
            int o = side == 0 ? 0 : 3;
            Rot[(int)BoneId.LUpperLeg + o] = new Vector3(flex, twist * m, abduct * m);
            Rot[(int)BoneId.LLowerLeg + o] = new Vector3(knee, 0f, 0f);
            Rot[(int)BoneId.LFoot + o] = new Vector3(foot, 0f, 0f);
        }

        public void SetHandGoal(int side, Vector3 rootPos, float w)
        {
            if (side == 0) { HandL = rootPos; HandIKL = w; HandSpaceL = 0f; }
            else { HandR = rootPos; HandIKR = w; HandSpaceR = 0f; }
        }

        public void SetHandGoalChest(int side, Vector3 chestPos, float w)
        {
            if (side == 0) { HandLC = chestPos; HandIKL = w; HandSpaceL = 1f; }
            else { HandRC = chestPos; HandIKR = w; HandSpaceR = 1f; }
        }

        public void SetHandRot(int side, Vector3 euler, float w)
        {
            if (side == 0) { HandRotL = euler; HandRotWL = w; }
            else { HandRotR = euler; HandRotWR = w; }
        }
    }

    /// <summary>Applies a <see cref="PoseBuffer"/> to a rig: FK rotations, then two-bone IK for legs and arms.</summary>
    internal sealed class PoseApplier
    {
        readonly HumanoidRig _rig;
        readonly Transform _root;
        readonly Transform[] _b = new Transform[PoseBuffer.BoneCount];
        readonly Vector3 _hipsBind;
        readonly float _legUpper, _legLower, _armUpperL, _armLowerL, _armUpperR, _armLowerR;

        public PoseApplier(HumanoidRig rig)
        {
            _rig = rig;
            _root = rig.transform;
            var bones = rig.BoneArray;
            for (int i = 0; i < _b.Length; i++) _b[i] = bones[i];
            _hipsBind = rig.BindHipsPosition;
            _legUpper = Vector3.Distance(rig.BindPositions[(int)BoneId.RUpperLeg], rig.BindPositions[(int)BoneId.RLowerLeg]);
            _legLower = Vector3.Distance(rig.BindPositions[(int)BoneId.RLowerLeg], rig.BindPositions[(int)BoneId.RFoot]);
            _armUpperL = Vector3.Distance(rig.BindPositions[(int)BoneId.LUpperArm], rig.BindPositions[(int)BoneId.LLowerArm]);
            _armLowerL = Vector3.Distance(rig.BindPositions[(int)BoneId.LLowerArm], rig.BindPositions[(int)BoneId.LHand]);
            _armUpperR = _armUpperL; _armLowerR = _armLowerL;
        }

        public Transform Bone(BoneId id) => _b[(int)id];

        static bool IsLimb(int i) => i >= (int)BoneId.LUpperArm;

        public static Quaternion Compose(int bone, Vector3 e)
        {
            if (!IsLimb(bone)) return Quaternion.Euler(e.x, e.y, e.z);
            return Quaternion.AngleAxis(e.z, Vector3.forward) * Quaternion.AngleAxis(e.x, Vector3.right) * Quaternion.AngleAxis(e.y, Vector3.up);
        }

        /// <summary>Forward kinematics only.</summary>
        public void ApplyFK(PoseBuffer p)
        {
            for (int i = 0; i < _b.Length; i++) _b[i].localRotation = Compose(i, p.Rot[i]);
            _b[(int)BoneId.Hips].localPosition = _hipsBind + p.HipsPos;
        }

        public void Apply(PoseBuffer p)
        {
            ApplyFK(p);
            if (p.LegIK > 0.001f)
            {
                SolveLeg(p, 0, p.AnkleL, p.FootPitchL, p.FootYawL);
                SolveLeg(p, 1, p.AnkleR, p.FootPitchR, p.FootYawR);
            }
            Transform chest = _b[(int)BoneId.Chest];
            for (int side = 0; side < 2; side++)
            {
                float w = side == 0 ? p.HandIKL : p.HandIKR;
                float rw = side == 0 ? p.HandRotWL : p.HandRotWR;
                if (w > 0.001f)
                {
                    Vector3 rootGoal = _root.TransformPoint(side == 0 ? p.HandL : p.HandR);
                    Vector3 chestGoal = chest.TransformPoint(side == 0 ? p.HandLC : p.HandRC);
                    Vector3 goal = Vector3.Lerp(rootGoal, chestGoal, side == 0 ? p.HandSpaceL : p.HandSpaceR);
                    Vector3 hint = _root.TransformDirection(side == 0 ? p.ElbowHintL : p.ElbowHintR);
                    int o = side == 0 ? 0 : 3;
                    SolveTwoBone(_b[(int)BoneId.LUpperArm + o], _b[(int)BoneId.LLowerArm + o], _b[(int)BoneId.LHand + o],
                        goal, hint, side == 0 ? _armUpperL : _armUpperR, side == 0 ? _armLowerL : _armLowerR, w);
                }
                if (rw > 0.001f)
                {
                    Transform hand = _b[side == 0 ? (int)BoneId.LHand : (int)BoneId.RHand];
                    Vector3 e = side == 0 ? p.HandRotL : p.HandRotR;
                    Quaternion target = _root.rotation * Quaternion.Euler(e.x, e.y, e.z);
                    hand.rotation = Quaternion.Slerp(hand.rotation, target, Mathf.Clamp01(rw));
                }
            }
        }

        /// <summary>Re-solves one arm towards a world space wrist goal (e.g. the left hand on a two handed item's grip).</summary>
        public void SolveArmWorld(int side, Vector3 goal, Vector3 hintWorld, float w)
        {
            if (w <= 0.001f) return;
            int o = side == 0 ? 0 : 3;
            SolveTwoBone(_b[(int)BoneId.LUpperArm + o], _b[(int)BoneId.LLowerArm + o], _b[(int)BoneId.LHand + o],
                goal, hintWorld, side == 0 ? _armUpperL : _armUpperR, side == 0 ? _armLowerL : _armLowerR, w);
        }

        void SolveLeg(PoseBuffer p, int side, Vector3 ankleRoot, float pitch, float yaw)
        {
            int o = side == 0 ? 0 : 3;
            Transform up = _b[(int)BoneId.LUpperLeg + o], lo = _b[(int)BoneId.LLowerLeg + o], ft = _b[(int)BoneId.LFoot + o];
            Quaternion footFK = ft.rotation;
            Vector3 goal = _root.TransformPoint(ankleRoot);
            // knees point forward (and slightly out)
            Vector3 hint = _root.TransformDirection(new Vector3(side == 0 ? -0.12f : 0.12f, 0f, 1f));
            SolveTwoBone(up, lo, ft, goal, hint, _legUpper, _legLower, p.LegIK);
            Quaternion target = _root.rotation * Quaternion.Euler(0f, side == 0 ? -yaw : yaw, 0f) * Quaternion.AngleAxis(pitch, Vector3.right);
            ft.rotation = Quaternion.Slerp(footFK, target, Mathf.Clamp01(p.LegIK));
        }

        /// <summary>Aim-style two bone IK (keeps the FK twist), blended with the FK pose by w.</summary>
        static void SolveTwoBone(Transform a, Transform b, Transform c, Vector3 goal, Vector3 hint, float l1, float l2, float w)
        {
            Quaternion aFK = a.localRotation, bFK = b.localRotation;
            Vector3 pa = a.position;
            Vector3 t = goal - pa;
            float d = t.magnitude;
            if (d < 1e-4f) return;
            float maxReach = (l1 + l2) * 0.9995f;
            float minReach = Mathf.Abs(l1 - l2) + 0.01f;
            float dc = Mathf.Clamp(d, minReach, maxReach);
            Vector3 dir = t / d;
            // bend plane
            Vector3 bendDir = hint - dir * Vector3.Dot(hint, dir);
            if (bendDir.sqrMagnitude < 1e-8f)
            {
                Vector3 cur = b.position - pa;
                bendDir = cur - dir * Vector3.Dot(cur, dir);
                if (bendDir.sqrMagnitude < 1e-8f) bendDir = Vector3.Cross(dir, Vector3.right);
            }
            bendDir.Normalize();
            float cosA = Mathf.Clamp((l1 * l1 + dc * dc - l2 * l2) / (2f * l1 * dc), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 kneePos = pa + dir * (l1 * cosA) + bendDir * (l1 * sinA);
            // aim upper bone
            Vector3 curUpper = b.position - pa;
            a.rotation = Quaternion.FromToRotation(curUpper, kneePos - pa) * a.rotation;
            // aim lower bone
            Vector3 pb = b.position;
            Vector3 curLower = c.position - pb;
            Vector3 endGoal = pa + dir * dc;
            b.rotation = Quaternion.FromToRotation(curLower, endGoal - pb) * b.rotation;
            if (w < 0.999f)
            {
                a.localRotation = Quaternion.Slerp(aFK, a.localRotation, w);
                b.localRotation = Quaternion.Slerp(bFK, b.localRotation, w);
            }
        }
    }
}
