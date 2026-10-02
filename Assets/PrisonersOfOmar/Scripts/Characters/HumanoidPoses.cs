using UnityEngine;

namespace PrisonersOfOmar.Characters
{
    /// <summary>Static / persistent full body poses (figures, CagedSit, Dead, Seated...).</summary>
    internal static class HumanoidPoses
    {
        const int L = 0, R = 1;

        static int B(BoneId b) => (int)b;

        /// <summary>Relaxed standing, arms hanging (FK).</summary>
        public static void Rest(PoseBuffer p, BodySpec b)
        {
            p.Clear();
            p.Arm(L, 2f, 5f, 0f, 8f);
            p.Arm(R, 2f, 5f, 0f, 8f);
            p.LegIK = 0f;
        }

        // ------------------------------------------------------------------------------------------ figures
        public static void Figure(PoseBuffer p, FigurePose pose, FigureKind kind, BodySpec b, DeterministicRandom rng)
        {
            Rest(p, b);
            float tilt = rng.Range(-8f, 8f);
            switch (pose)
            {
                case FigurePose.Standing:
                    p.Rot[B(BoneId.Head)] = new Vector3(rng.Range(-4f, 10f), rng.Range(-15f, 15f), tilt);
                    p.Arm(L, rng.Range(-6f, 4f), rng.Range(3f, 9f), 0f, rng.Range(5f, 20f));
                    p.Arm(R, rng.Range(-6f, 4f), rng.Range(3f, 9f), 0f, rng.Range(5f, 20f));
                    if (kind == FigureKind.Corpse) p.Rot[B(BoneId.Chest)].x = 8f;
                    break;
                case FigurePose.StandingHandsCrossed:
                    HandsFolded(p, b);
                    p.Rot[B(BoneId.Head)] = new Vector3(rng.Range(5f, 18f), rng.Range(-10f, 10f), tilt);
                    break;
                case FigurePose.LyingOnBack:
                    Lying(p, b, rng, false);
                    break;
                case FigurePose.Sitting:
                    SlumpedSit(p, b, rng);
                    break;
                case FigurePose.Hanging:
                    p.Rot[B(BoneId.Neck)] = new Vector3(18f, 0f, tilt * 1.5f);
                    p.Rot[B(BoneId.Head)] = new Vector3(rng.Range(22f, 38f), rng.Range(-15f, 15f), (tilt >= 0 ? 1 : -1) * rng.Range(22f, 34f));
                    p.Rot[B(BoneId.Chest)] = new Vector3(4f, 0f, -tilt * 0.3f);
                    p.Arm(L, rng.Range(-8f, 2f), rng.Range(2f, 6f), 10f, rng.Range(8f, 22f));
                    p.Arm(R, rng.Range(-8f, 2f), rng.Range(2f, 6f), 10f, rng.Range(8f, 22f));
                    p.HandFK(L, 15f, 0f, 0f);
                    p.HandFK(R, 15f, 0f, 0f);
                    p.Leg(L, rng.Range(-4f, 2f), 1f, -6f, rng.Range(2f, 10f), 42f);
                    p.Leg(R, rng.Range(-4f, 2f), 1f, -6f, rng.Range(2f, 10f), 40f);
                    p.HipsPos = new Vector3(0, 0.02f, 0);
                    break;
            }
        }

        /// <summary>Hands folded in front of the crotch (burnt mannequin reference).</summary>
        public static void HandsFolded(PoseBuffer p, BodySpec b)
        {
            float s = b.Scale;
            Ring hipRing = b.Torso[1];
            float y = b.CrotchY - 0.02f * s;
            float z = hipRing.F + 0.07f * s;
            p.Arm(L, -20f, 0f, 0f, 60f);
            p.Arm(R, -20f, 0f, 0f, 60f);
            p.SetHandGoal(L, new Vector3(-0.035f * s, y + 0.06f * s, z), 1f);
            p.SetHandGoal(R, new Vector3(0.025f * s, y + 0.055f * s, z + 0.02f * s), 1f);
            p.SetHandRot(L, new Vector3(0f, 70f, 0f), 1f);
            p.SetHandRot(R, new Vector3(0f, -70f, 0f), 1f);
            p.ElbowHintL = new Vector3(-1f, -0.3f, -0.6f);
            p.ElbowHintR = new Vector3(1f, -0.3f, -0.6f);
            p.Rot[B(BoneId.Spine)].x = 2f;
        }

        /// <summary>Lying on the back on the ground; hips bone on the origin. Head towards -Z.</summary>
        public static void Lying(PoseBuffer p, BodySpec b, DeterministicRandom rng, bool deadAnim)
        {
            float s = b.Scale;
            p.Clear();
            p.Rot[B(BoneId.Hips)] = new Vector3(-90f, 0f, rng != null ? rng.Range(-4f, 4f) : 0f);
            p.HipsPos = new Vector3(0f, b.Torso[1].B * 0.92f - b.HipsY, 0f);
            p.Rot[B(BoneId.Spine)] = new Vector3(-3f, 0f, 0f);
            p.Rot[B(BoneId.Chest)] = new Vector3(-4f, rng != null ? rng.Range(-5f, 5f) : 0f, 0f);
            float headYaw = rng != null ? (rng.Chance(0.5f) ? 1f : -1f) * rng.Range(25f, 65f) : 50f;
            p.Rot[B(BoneId.Neck)] = new Vector3(-10f, headYaw * 0.3f, 0f);
            p.Rot[B(BoneId.Head)] = new Vector3(-12f, headYaw * 0.7f, 0f);
            float aL = rng != null ? rng.Range(10f, 55f) : 30f, aR = rng != null ? rng.Range(10f, 55f) : 45f;
            // arms lie on the ground: with the body rotated, arm "flex" towards +90 keeps them on the floor plane
            p.Arm(L, 8f, aL, rng != null ? rng.Range(-30f, 30f) : 10f, rng != null ? rng.Range(5f, 45f) : 20f);
            p.Arm(R, 8f, aR, rng != null ? rng.Range(-30f, 30f) : -10f, rng != null ? rng.Range(5f, 45f) : 30f);
            p.HandFK(L, 10f, 0f, 0f);
            p.HandFK(R, 10f, 0f, 0f);
            float kL = rng != null ? rng.Range(2f, 25f) : 8f, kR = rng != null ? rng.Range(2f, 12f) : 4f;
            p.Leg(L, -kL * 0.5f, rng != null ? rng.Range(4f, 12f) : 8f, -25f, kL, 35f);
            p.Leg(R, -kR * 0.5f, rng != null ? rng.Range(4f, 12f) : 6f, -25f, kR, 30f);
        }

        /// <summary>Slumped against a wall (behind, -Z), sitting on the floor; origin on the floor under the pelvis.</summary>
        public static void SlumpedSit(PoseBuffer p, BodySpec b, DeterministicRandom rng)
        {
            float s = b.Scale;
            p.Clear();
            float lean = -22f;
            p.Rot[B(BoneId.Hips)] = new Vector3(lean, 0f, rng.Range(-3f, 3f));
            p.HipsPos = new Vector3(0f, 0.12f * s - b.HipsY, -0.04f * s);
            p.Rot[B(BoneId.Spine)] = new Vector3(8f, 0f, rng.Range(-4f, 4f));
            p.Rot[B(BoneId.Chest)] = new Vector3(14f, rng.Range(-6f, 6f), rng.Range(-4f, 4f));
            p.Rot[B(BoneId.Neck)] = new Vector3(15f, 0f, 0f);
            p.Rot[B(BoneId.Head)] = new Vector3(rng.Range(20f, 40f), rng.Range(-20f, 20f), rng.Range(-25f, 25f));
            // legs out on the floor (thigh horizontal: local -90 - lean), one knee may be drawn up
            bool kneeUpL = rng.Chance(0.3f), kneeUpR = !kneeUpL && rng.Chance(0.35f);
            float kL = rng.Range(4f, 20f), kR = rng.Range(4f, 20f);
            if (kneeUpL) p.Leg(L, -100f, rng.Range(8f, 16f), -10f, 118f, 10f);
            else p.Leg(L, -90f - lean + kL * 0.5f, rng.Range(6f, 14f), -22f, kL, 18f);
            if (kneeUpR) p.Leg(R, -100f, rng.Range(8f, 16f), -10f, 118f, 10f);
            else p.Leg(R, -90f - lean + kR * 0.5f, rng.Range(6f, 14f), -22f, kR, 18f);
            p.Arm(L, rng.Range(-15f, 5f), rng.Range(10f, 22f), 0f, rng.Range(10f, 40f));
            p.Arm(R, rng.Range(-15f, 5f), rng.Range(10f, 22f), 0f, rng.Range(10f, 40f));
        }

        // ------------------------------------------------------------------------------------------ persistent (animated)
        /// <summary>Sitting on the floor hugging the knees. rock = rocking phase (radians).</summary>
        public static void CagedSit(PoseBuffer p, BodySpec b, float rock, float breathe)
        {
            float s = b.Scale;
            p.Clear();
            float r = Mathf.Sin(rock) * 4f;
            p.Rot[B(BoneId.Hips)] = new Vector3(-28f + r, 0f, 0f);
            p.HipsPos = new Vector3(0f, 0.11f * s - b.HipsY, -0.12f * s);
            p.Rot[B(BoneId.Spine)] = new Vector3(22f + r * 0.5f, 0f, 0f);
            p.Rot[B(BoneId.Chest)] = new Vector3(20f + breathe, 0f, 0f);
            p.Rot[B(BoneId.Neck)] = new Vector3(10f, 0f, 0f);
            p.Rot[B(BoneId.Head)] = new Vector3(12f, 0f, 3f);
            // knees up to the chest
            p.Leg(L, -100f, 9f, -6f, 135f, 8f);
            p.Leg(R, -100f, 9f, -6f, 135f, 8f);
            // arms wrap around the shins
            float kneeZ = 0.42f * s, y = 0.38f * s;
            p.Arm(L, -50f, 10f, 0f, 70f);
            p.Arm(R, -50f, 10f, 0f, 70f);
            p.SetHandGoal(L, new Vector3(0.05f * s, y, kneeZ - 0.04f * s), 1f);
            p.SetHandGoal(R, new Vector3(-0.05f * s, y + 0.03f * s, kneeZ - 0.06f * s), 1f);
            p.ElbowHintL = new Vector3(-1f, -0.2f, 0.3f);
            p.ElbowHintR = new Vector3(1f, -0.2f, 0.3f);
        }

        /// <summary>Prone on the floor, propped on the elbows; crawl = crawl cycle phase (radians), crawlW 0..1.</summary>
        public static void Downed(PoseBuffer p, BodySpec b, float crawl, float crawlW, float breathe)
        {
            float s = b.Scale;
            p.Clear();
            p.Rot[B(BoneId.Hips)] = new Vector3(86f, Mathf.Sin(crawl) * 6f * crawlW, 0f);
            p.HipsPos = new Vector3(0f, b.Torso[1].F + 0.02f * s - b.HipsY, -0.35f * s);
            p.Rot[B(BoneId.Spine)] = new Vector3(-8f, 0f, 0f);
            p.Rot[B(BoneId.Chest)] = new Vector3(-18f + breathe, -Mathf.Sin(crawl) * 8f * crawlW, 0f);
            p.Rot[B(BoneId.Neck)] = new Vector3(-28f, 0f, 0f);
            p.Rot[B(BoneId.Head)] = new Vector3(-22f, 0f, 0f);
            float a = Mathf.Sin(crawl) * crawlW;
            // arms reach forward alternately
            p.Arm(L, -150f + a * 30f, 20f, 30f, 70f - a * 30f);
            p.Arm(R, -150f - a * 30f, 20f, -30f, 70f + a * 30f);
            // frog crawl: the knee slides out to the side along the floor, the foot pushes
            float aL = Mathf.Max(0f, a), aR = Mathf.Max(0f, -a);
            p.Leg(L, -6f - aL * 25f, 10f + aL * 30f, -20f - aL * 40f, 8f + aL * 35f, 60f);
            p.Leg(R, -6f - aR * 25f, 10f + aR * 30f, -20f - aR * 40f, 8f + aR * 35f, 60f);
        }

        public static void Seated(PoseBuffer p, BodySpec b, float breathe)
        {
            float s = b.Scale;
            p.Clear();
            p.Rot[B(BoneId.Hips)] = new Vector3(-10f, 0f, 0f);
            p.HipsPos = new Vector3(0f, 0.50f * s - b.HipsY, 0f);
            p.Rot[B(BoneId.Spine)] = new Vector3(4f, 0f, 0f);
            p.Rot[B(BoneId.Chest)] = new Vector3(4f + breathe, 0f, 0f);
            p.Leg(L, -80f, 6f, -4f, 85f, -5f);
            p.Leg(R, -80f, 6f, -4f, 85f, -5f);
            p.Arm(L, -35f, 8f, 0f, 45f);
            p.Arm(R, -35f, 8f, 0f, 45f);
        }

        /// <summary>Kneeling with the right leg caught in a trap at the origin.</summary>
        public static void Trapped(PoseBuffer p, BodySpec b, float t, float breathe)
        {
            float s = b.Scale;
            p.Clear();
            float pull = Mathf.Sin(t * 5.2f) * Mathf.Max(0f, Mathf.Sin(t * 0.9f));
            p.HipsPos = new Vector3(-0.08f * s, 0.46f * s - b.HipsY + pull * 0.015f, -0.18f * s);
            p.Rot[B(BoneId.Hips)] = new Vector3(15f, -12f, 0f);
            p.Rot[B(BoneId.Spine)] = new Vector3(18f, -5f, 0f);
            p.Rot[B(BoneId.Chest)] = new Vector3(18f + breathe, -8f + pull * 6f, 0f);
            p.Rot[B(BoneId.Neck)] = new Vector3(10f, 0f, 0f);
            p.Rot[B(BoneId.Head)] = new Vector3(18f, 18f, 0f);
            // legs: IK, right foot in the trap, left knee down on the floor
            p.LegIK = 1f;
            p.AnkleR = new Vector3(0.1f * s, b.AnkleY, 0.18f * s);
            p.FootPitchR = 0f;
            p.AnkleL = new Vector3(-0.14f * s, 0.10f * s, -0.52f * s);
            p.FootPitchL = 75f;
            // hands pull at the trap / right shin
            p.SetHandGoal(R, new Vector3(0.14f * s, 0.24f * s + pull * 0.04f, 0.16f * s), 1f);
            p.SetHandGoal(L, new Vector3(0.02f * s, 0.26f * s - pull * 0.03f, 0.2f * s), 1f);
            p.ElbowHintL = new Vector3(-1f, 0f, -0.3f);
            p.ElbowHintR = new Vector3(1f, 0f, -0.3f);
        }
    }
}
