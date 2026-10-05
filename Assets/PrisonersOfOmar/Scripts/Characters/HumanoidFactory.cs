using System.Collections.Generic;
using PrisonersOfOmar.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrisonersOfOmar.Characters
{
    /// <summary>Builds the procedural low-poly characters (skinned mesh + bones + animator).</summary>
    public static class HumanoidFactory
    {
        /// <summary>
        /// Hanging figures (<see cref="FigurePose.Hanging"/>): the origin is the top of the rope (ceiling hook / beam);
        /// the soles hang this far below it (so under a 2.45 m ceiling the feet are ~20 cm above the floor).
        /// </summary>
        public const float HangingDropHeight = 2.25f;

        /// <summary>
        /// Builds an animated character as a child of <paramref name="parent"/> (local origin, facing +Z).
        /// The returned rig has a <see cref="HumanoidAnimator"/>. Layer of all parts = <paramref name="layer"/>.
        /// Omar comes with his cleaver already in the right hand (child "Cleaver" of RightHandSocket) and
        /// Hold = <see cref="HoldPose.Cleaver"/>.
        /// </summary>
        public static HumanoidRig Build(CharacterSkin skin, Transform parent, int layer = Layers.Player)
        {
            var spec = BodySpec.For(skin);
            var rig = BuildRig("Humanoid_" + skin, spec, parent, layer);
            rig.Skin = skin;
            var anim = rig.gameObject.AddComponent<HumanoidAnimator>();
            if (skin == CharacterSkin.Omar)
            {
                var cleaver = ItemMeshFactory.BuildCleaver();
                cleaver.transform.SetParent(rig.RightHandSocket, false);
                GeoUtil.SetLayerRecursive(cleaver, layer);
                anim.Hold = HoldPose.Cleaver;
            }
            return rig;
        }

        internal static Material[] MaterialsFor(string texture, int subMeshes)
        {
            var opaque = PsxMaterials.Get(texture, PsxSurface.Lit);
            if (subMeshes < 2) return new[] { opaque };
            return new[] { opaque, PsxMaterials.Get(texture, PsxSurface.LitCutout) };
        }

        static readonly BoneId[] Parents =
        {
            (BoneId)(-1), BoneId.Hips, BoneId.Spine, BoneId.Chest, BoneId.Neck,
            BoneId.Chest, BoneId.LUpperArm, BoneId.LLowerArm,
            BoneId.Chest, BoneId.RUpperArm, BoneId.RLowerArm,
            BoneId.Hips, BoneId.LUpperLeg, BoneId.LLowerLeg,
            BoneId.Hips, BoneId.RUpperLeg, BoneId.RLowerLeg,
        };

        static readonly string[] BoneNames =
        {
            "Hips", "Spine", "Chest", "Neck", "Head",
            "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "RightUpperLeg", "RightLowerLeg", "RightFoot",
        };

        internal static HumanoidRig BuildRig(string name, BodySpec spec, Transform parent, int layer)
        {
            var go = new GameObject(name);
            go.layer = layer;
            if (parent != null) go.transform.SetParent(parent, false);
            var rig = go.AddComponent<HumanoidRig>();
            rig.Spec = spec;
            rig.Height = spec.Height;

            var sk = BodyMeshGenerator.MakeSkeleton(spec);
            var bones = new Transform[BodyMeshGenerator.TotalBones];
            for (int i = 0; i < PoseBuffer.BoneCount; i++)
            {
                int pi = (int)Parents[i];
                Transform p = pi < 0 ? go.transform : bones[pi];
                Vector3 local = pi < 0 ? sk.Pos[i] : sk.Pos[i] - sk.Pos[pi];
                bones[i] = GeoUtil.CreateChild(p, BoneNames[i], local, Quaternion.identity, layer);
            }
            rig.BoneArray = bones;
            rig.BindPositions = (Vector3[])sk.Pos.Clone();
            rig.BindHipsPosition = sk.Pos[(int)BoneId.Hips];
            rig.Hips = bones[(int)BoneId.Hips]; rig.Spine = bones[(int)BoneId.Spine]; rig.Chest = bones[(int)BoneId.Chest];
            rig.Neck = bones[(int)BoneId.Neck]; rig.Head = bones[(int)BoneId.Head];
            rig.LeftUpperArm = bones[(int)BoneId.LUpperArm]; rig.LeftLowerArm = bones[(int)BoneId.LLowerArm]; rig.LeftHand = bones[(int)BoneId.LHand];
            rig.RightUpperArm = bones[(int)BoneId.RUpperArm]; rig.RightLowerArm = bones[(int)BoneId.RLowerArm]; rig.RightHand = bones[(int)BoneId.RHand];
            rig.LeftUpperLeg = bones[(int)BoneId.LUpperLeg]; rig.LeftLowerLeg = bones[(int)BoneId.LLowerLeg]; rig.LeftFoot = bones[(int)BoneId.LFoot];
            rig.RightUpperLeg = bones[(int)BoneId.RUpperLeg]; rig.RightLowerLeg = bones[(int)BoneId.RLowerLeg]; rig.RightFoot = bones[(int)BoneId.RFoot];
            rig.RightHandSocket = GeoUtil.CreateChild(rig.RightHand, "RightHandSocket", sk.RightSocket - sk.Pos[(int)BoneId.RHand], Quaternion.identity, layer);
            rig.LeftHandSocket = GeoUtil.CreateChild(rig.LeftHand, "LeftHandSocket", sk.LeftSocket - sk.Pos[(int)BoneId.LHand], Quaternion.identity, layer);
            rig.EyePoint = GeoUtil.CreateChild(rig.Head, "EyePoint", sk.Eye - sk.Pos[(int)BoneId.Head], Quaternion.identity, layer);
            // finger bones (relaxed / fist) at the palm centres, see BodyMeshGenerator.LFingersBone
            for (int side = 0; side < 2; side++)
            {
                int hand = (int)BoneId.LHand + side * 3;
                Vector3 palm = BodyMeshGenerator.PalmCenter(spec, sk, side) - sk.Pos[hand];
                string n = side == 0 ? "Left" : "Right";
                bones[BodyMeshGenerator.LFingersBone + side * 2] = GeoUtil.CreateChild(bones[hand], n + "Fingers", palm, Quaternion.identity, layer);
                bones[BodyMeshGenerator.LFistBone + side * 2] = GeoUtil.CreateChild(bones[hand], n + "Fist", palm, Quaternion.identity, layer);
            }
            rig.LeftFingers = bones[BodyMeshGenerator.LFingersBone]; rig.LeftFist = bones[BodyMeshGenerator.LFistBone];
            rig.RightFingers = bones[BodyMeshGenerator.RFingersBone]; rig.RightFist = bones[BodyMeshGenerator.RFistBone];

            // mesh
            var mb = new SkinMeshBuilder();
            BodyMeshGenerator.Build(spec, sk, mb);
            var mesh = mb.ToMesh(name);
            var body = new GameObject("Body");
            body.layer = layer;
            body.transform.SetParent(go.transform, false);
            var bind = new Matrix4x4[bones.Length];
            Matrix4x4 bodyToWorld = body.transform.localToWorldMatrix;
            for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * bodyToWorld;
            mesh.bindposes = bind;
            // relaxed hands until the animator clenches them (after the bind poses: those need the unscaled bones)
            rig.SetFist(0, 0f);
            rig.SetFist(1, 0f);

            var smr = body.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = rig.Hips;
            smr.sharedMaterials = MaterialsFor(spec.Texture, mesh.subMeshCount);
            smr.updateWhenOffscreen = true;
            smr.localBounds = new Bounds(new Vector3(0, 0, 0), new Vector3(3f, 3.5f, 3f));
            smr.quality = SkinQuality.Bone4;
            smr.shadowCastingMode = ShadowCastingMode.Off;
            smr.receiveShadows = false;
            smr.lightProbeUsage = LightProbeUsage.Off;
            smr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            smr.skinnedMotionVectors = false;
            rig.BodyRenderer = smr;
            rig.Renderers = new Renderer[] { smr };
            return rig;
        }

        /// <summary>
        /// Builds a static (non animated, combined MeshRenderer) human figure: mannequins and corpses used as level decoration.
        /// Origin between the feet (or at the pelvis on the ground for lying poses), facing +Z.
        /// Pose origins: Standing / StandingHandsCrossed: between the feet. LyingOnBack: on the ground under the pelvis,
        /// the body lies along Z with the head towards -Z and the feet towards +Z. Sitting: on the floor under the pelvis,
        /// slumped back against a wall at z = -0.25, legs towards +Z. Hanging: origin = top of the rope (hook), the body
        /// hangs below it, soles <see cref="HangingDropHeight"/> below the origin.
        /// <paramref name="seed"/> varies height, sex (mannequins), head tilt and limb angles.
        /// </summary>
        public static GameObject BuildFigure(FigureKind kind, FigurePose pose, Transform parent, int seed = 0)
        {
            var go = new GameObject("Figure_" + kind);
            go.layer = Layers.Corpse;
            if (parent != null) go.transform.SetParent(parent, false);

            var spec = BodySpec.For(kind, seed);
            var rig = BuildRig("FigureRig", spec, go.transform, Layers.Corpse);
            var rng = new DeterministicRandom(seed, 4242 + (int)kind * 7 + (int)pose);
            var buf = new PoseBuffer();
            HumanoidPoses.Figure(buf, pose, kind, spec, rng);
            if (pose == FigurePose.Hanging)
                rig.transform.localPosition = new Vector3(0, -HangingDropHeight, 0);
            var applier = new PoseApplier(rig);
            applier.Apply(buf);

            var baked = new Mesh { name = "Figure_" + kind };
            rig.BodyRenderer.BakeMesh(baked);
            rig.BodyRenderer.enabled = false;

            // bake into the figure root space (the rig root may be offset, e.g. hanging)
            Transform body = rig.BodyRenderer.transform;
            var verts = baked.vertices;
            var norms = baked.normals;
            var uvs = baked.uv;
            Matrix4x4 toRoot = go.transform.worldToLocalMatrix * body.localToWorldMatrix;
            var vl = new List<Vector3>(verts.Length + 64);
            var nl = new List<Vector3>(verts.Length + 64);
            var ul = new List<Vector2>(verts.Length + 64);
            for (int i = 0; i < verts.Length; i++)
            {
                vl.Add(toRoot.MultiplyPoint3x4(verts[i]));
                nl.Add(toRoot.MultiplyVector(i < norms.Length ? norms[i] : Vector3.up).normalized);
                ul.Add(i < uvs.Length ? uvs[i] : Vector2.zero);
            }
            var sub0 = new List<int>(baked.GetTriangles(0));
            var sub1 = baked.subMeshCount > 1 ? new List<int>(baked.GetTriangles(1)) : new List<int>();
            if (pose == FigurePose.Hanging)
            {
                Vector3 neckTop = go.transform.InverseTransformPoint(rig.Head.position) + new Vector3(0, -0.02f, -0.02f);
                AddRope(vl, nl, ul, sub0, neckTop, Vector3.zero, 0.014f);
            }
            var mesh = new Mesh { name = "Figure_" + kind + "_" + pose };
            mesh.SetVertices(vl);
            mesh.SetNormals(nl);
            mesh.SetUVs(0, ul);
            var cols = new Color32[vl.Count];
            for (int i = 0; i < cols.Length; i++) cols[i] = new Color32(255, 255, 255, 255);
            mesh.colors32 = cols;
            mesh.subMeshCount = sub1.Count > 0 ? 2 : 1;
            mesh.SetTriangles(sub0, 0, false);
            if (sub1.Count > 0) mesh.SetTriangles(sub1, 1, false);
            mesh.RecalculateBounds();

            var mgo = new GameObject("Mesh");
            mgo.layer = Layers.Corpse;
            mgo.transform.SetParent(go.transform, false);
            mgo.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = mgo.AddComponent<MeshRenderer>();
            mr.sharedMaterials = MaterialsFor(spec.Texture, mesh.subMeshCount);
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;

            rig.gameObject.SetActive(false);
            Object.Destroy(rig.gameObject);
            Object.Destroy(baked);
            return go;
        }

        static void AddRope(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tris, Vector3 from, Vector3 to, float r)
        {
            const int S = 5;
            Vector3 axis = (to - from);
            float len = axis.magnitude;
            if (len < 1e-3f) return;
            axis /= len;
            Vector3 side = Vector3.Cross(axis, Vector3.forward);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(axis, Vector3.right);
            side.Normalize();
            Vector3 fwd = Vector3.Cross(side, axis).normalized;
            int first = v.Count;
            var reg = CharacterAtlas.Rope;
            for (int k = 0; k < 2; k++)
            {
                Vector3 c = k == 0 ? from : to;
                for (int j = 0; j <= S; j++)
                {
                    float a = (float)j / S * Mathf.PI * 2f;
                    Vector3 d = side * Mathf.Cos(a) + fwd * Mathf.Sin(a);
                    v.Add(c + d * r);
                    n.Add(d);
                    uv.Add(reg.UV((float)j / S, k == 0 ? 0f : Mathf.Min(1f, len / 0.6f)));
                }
            }
            int row = S + 1;
            for (int j = 0; j < S; j++)
            {
                int a = first + j, b = a + row;
                tris.Add(a); tris.Add(b); tris.Add(b + 1);
                tris.Add(a); tris.Add(b + 1); tris.Add(a + 1);
            }
        }

        /// <summary>Skins whose body + texture atlas exist (the lobby only offers these).</summary>
        public static bool HasSkin(CharacterSkin skin)
            => skin == CharacterSkin.Omar || (skin >= CharacterSkin.Prisoner1 && skin <= CharacterSkin.Prisoner4)
               || (skin >= CharacterSkin.Prisoner5 && skin <= CharacterSkin.Prisoner8);

        public static string DisplayName(CharacterSkin skin)
        {
            switch (skin)
            {
                case CharacterSkin.Prisoner1: return "THE ATHLETE";
                case CharacterSkin.Prisoner2: return "THE GIRL IN RED";
                case CharacterSkin.Prisoner3: return "THE REDHEAD";
                case CharacterSkin.Prisoner4: return "THE NERD";
                case CharacterSkin.Prisoner5: return "THE CAMERAWOMAN";
                case CharacterSkin.Prisoner6: return "THE KID";
                case CharacterSkin.Prisoner7: return "THE FATHER";
                case CharacterSkin.Prisoner8: return "HTN";
                default: return "OMAR";
            }
        }
    }
}
