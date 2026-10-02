using UnityEngine;

namespace PrisonersOfOmar.Characters
{
    internal enum HairStyle { None, Spiky, Short, LongWavy, LongBangs }
    internal enum GlassesStyle { None, Round, Rect }
    internal enum HeadShape { Human, Sack, Featureless }

    /// <summary>Elliptic cross-section: half width (X), front depth (+Z), back depth (-Z), z of the centre. Meters.</summary>
    internal struct Ring
    {
        public float W, F, B, C;
        public Ring(float w, float f, float b, float c = 0f) { W = w; F = f; B = b; C = c; }
        public Ring Scaled(float s) => new Ring(W * s, F * s, B * s, C * s);
    }

    /// <summary>Body proportions + features of one procedural character (bind pose: standing, arms hanging, facing +Z).</summary>
    internal sealed class BodySpec
    {
        public string Texture;
        public float Height = 1.75f;
        public bool Female;
        public bool Heavy;

        // skeleton (absolute heights / offsets in meters, root space)
        public float AnkleY, KneeY, HipJointY, HipJointX, HipsY, SpineY, ChestY;
        public float CrotchY, NeckY;
        public float ShoulderX, ShoulderY, ElbowX, ElbowY, WristX, WristY;
        public float HeadH;                      // chin .. crown (or mask height)

        // shapes
        public Ring[] Torso;                     // 9 rings at CharacterAtlas.TorsoT
        public float Bust, Belly;
        public float[] LegRx, LegRz;             // 9 rings at CharacterAtlas.LegT
        public float[] ArmR;                     // 7 rings at CharacterAtlas.ArmT
        public Ring[] HeadRings;                 // 10 rings in head units (x HeadH)
        public float HeadSquare = 2.2f;          // superellipse exponent of head rings
        public float Nose = 0.07f;               // nose protrusion (head units)
        public float HandScale = 1f;
        public float FootLen = 0.26f, FootW = 0.095f, ShoeH = 0.11f;

        // features
        public HeadShape HeadShape = HeadShape.Human;
        public HairStyle Hair = HairStyle.None;
        public GlassesStyle Glasses = GlassesStyle.None;
        public bool Apron, Noose, Skirt, Rags;
        public bool Barefoot;                    // feet use the skin / plastic texture for the sole too

        public float Scale => Height / 1.8f;

        static readonly Ring[] HumanHead =
        {
            new Ring(0.25f, 0.22f, 0.24f, -0.06f),  // neck base
            new Ring(0.22f, 0.20f, 0.21f, -0.05f),  // neck mid
            new Ring(0.25f, 0.34f, 0.24f, -0.03f),  // chin / jaw
            new Ring(0.29f, 0.40f, 0.33f, 0.00f),   // mouth
            new Ring(0.31f, 0.41f, 0.38f, 0.00f),   // nose
            new Ring(0.325f, 0.39f, 0.42f, 0.00f),  // eyes
            new Ring(0.33f, 0.41f, 0.44f, 0.00f),   // brow
            new Ring(0.32f, 0.37f, 0.43f, 0.00f),   // forehead
            new Ring(0.26f, 0.27f, 0.34f, -0.01f),  // upper skull
            new Ring(0.0f, 0.0f, 0.0f, -0.03f),     // crown (pole)
        };

        static readonly Ring[] SackHead =
        {
            new Ring(0.25f, 0.23f, 0.25f, -0.04f),
            new Ring(0.20f, 0.19f, 0.20f, -0.03f),  // gathered at the rope
            new Ring(0.30f, 0.33f, 0.29f, -0.01f),  // baggy below the chin
            new Ring(0.335f, 0.37f, 0.36f, 0.00f),
            new Ring(0.345f, 0.375f, 0.39f, 0.00f),
            new Ring(0.345f, 0.365f, 0.40f, 0.00f),
            new Ring(0.35f, 0.37f, 0.40f, 0.00f),
            new Ring(0.345f, 0.365f, 0.40f, 0.00f),
            new Ring(0.32f, 0.33f, 0.37f, 0.00f),   // boxy top
            new Ring(0.0f, 0.0f, 0.0f, 0.0f),
        };

        void Skeleton(float h, bool female)
        {
            Height = h;
            Female = female;
            float s = h / 1.8f;
            AnkleY = 0.045f * h;
            KneeY = 0.285f * h;
            HipJointY = 0.52f * h;
            HipJointX = (female ? 0.095f : 0.09f) * s;
            CrotchY = 0.475f * h;
            NeckY = 0.843f * h;
            HipsY = 0.535f * h;
            SpineY = 0.615f * h;
            ChestY = 0.715f * h;
            ShoulderY = NeckY - 0.03f * h;
            ElbowY = ShoulderY - 0.17f * h;
            WristY = ElbowY - 0.143f * h;
            HeadH = (female ? 0.133f : 0.135f) * h;
            HeadRings = HumanHead;
        }

        static float[] Arr(float s, params float[] v)
        {
            for (int i = 0; i < v.Length; i++) v[i] *= s;
            return v;
        }

        static Ring[] Rings(float s, params Ring[] r)
        {
            for (int i = 0; i < r.Length; i++) r[i] = r[i].Scaled(s);
            return r;
        }

        // ------------------------------------------------------------------------------------------ skins
        public static BodySpec For(CharacterSkin skin)
        {
            var b = new BodySpec();
            b.Texture = CharacterAtlas.TexturePath(skin);
            switch (skin)
            {
                case CharacterSkin.Prisoner1: b.Athlete(); break;
                case CharacterSkin.Prisoner2: b.GirlInRed(); break;
                case CharacterSkin.Prisoner3: b.Redhead(); break;
                case CharacterSkin.Prisoner4: b.Nerd(); break;
                default: b.Omar(); break;
            }
            return b;
        }

        void MaleLimbs(float s, float bulk)
        {
            LegRx = Arr(s * bulk, 0.034f, 0.040f, 0.052f, 0.046f, 0.047f, 0.054f, 0.068f, 0.078f, 0.082f);
            LegRz = Arr(s * bulk, 0.040f, 0.045f, 0.058f, 0.050f, 0.050f, 0.057f, 0.071f, 0.080f, 0.085f);
            ArmR = Arr(s * bulk, 0.026f, 0.032f, 0.038f, 0.036f, 0.042f, 0.047f, 0.052f);
        }

        void FemaleLimbs(float s, float bulk)
        {
            LegRx = Arr(s * bulk, 0.030f, 0.036f, 0.048f, 0.042f, 0.044f, 0.052f, 0.068f, 0.080f, 0.084f);
            LegRz = Arr(s * bulk, 0.036f, 0.041f, 0.053f, 0.046f, 0.047f, 0.055f, 0.070f, 0.080f, 0.086f);
            ArmR = Arr(s * bulk, 0.022f, 0.027f, 0.032f, 0.030f, 0.035f, 0.039f, 0.044f);
        }

        void Athlete()
        {
            Skeleton(1.80f, false);
            float s = Scale;
            Torso = Rings(s,
                new Ring(0.150f, 0.085f, 0.095f), new Ring(0.168f, 0.095f, 0.110f), new Ring(0.158f, 0.095f, 0.095f),
                new Ring(0.150f, 0.095f, 0.085f), new Ring(0.168f, 0.112f, 0.090f), new Ring(0.188f, 0.122f, 0.100f),
                new Ring(0.200f, 0.112f, 0.104f), new Ring(0.228f, 0.078f, 0.080f), new Ring(0.064f, 0.055f, 0.060f, -0.01f));
            MaleLimbs(s, 1.08f);
            ArmR = Arr(s, 0.028f, 0.037f, 0.044f, 0.040f, 0.050f, 0.056f, 0.060f); // muscular arms
            ShoulderX = 0.188f * s; ElbowX = 0.212f * s; WristX = 0.222f * s;
            Hair = HairStyle.Spiky;
            HandScale = 1.05f;
        }

        void GirlInRed()
        {
            Skeleton(1.68f, true);
            float s = Scale;
            Torso = Rings(s,
                new Ring(0.150f, 0.080f, 0.100f), new Ring(0.182f, 0.090f, 0.122f), new Ring(0.165f, 0.086f, 0.098f),
                new Ring(0.132f, 0.080f, 0.076f), new Ring(0.142f, 0.090f, 0.080f), new Ring(0.152f, 0.098f, 0.085f),
                new Ring(0.158f, 0.094f, 0.090f), new Ring(0.188f, 0.066f, 0.070f), new Ring(0.056f, 0.048f, 0.052f, -0.01f));
            Bust = 0.040f * s;
            FemaleLimbs(s, 1.0f);
            ShoulderX = 0.152f * s; ElbowX = 0.178f * s; WristX = 0.205f * s;
            Hair = HairStyle.LongWavy;
            HandScale = 0.9f;
            FootLen = 0.24f; FootW = 0.085f; ShoeH = 0.10f;
        }

        void Redhead()
        {
            Skeleton(1.58f, true);
            float s = Scale;
            Torso = Rings(s,
                new Ring(0.150f, 0.080f, 0.098f), new Ring(0.176f, 0.088f, 0.118f), new Ring(0.162f, 0.086f, 0.096f),
                new Ring(0.136f, 0.082f, 0.078f), new Ring(0.145f, 0.090f, 0.082f), new Ring(0.155f, 0.096f, 0.086f),
                new Ring(0.160f, 0.094f, 0.090f), new Ring(0.186f, 0.068f, 0.072f), new Ring(0.056f, 0.048f, 0.052f, -0.01f));
            Bust = 0.026f * s;
            FemaleLimbs(s, 1.02f);
            ShoulderX = 0.152f * s; ElbowX = 0.176f * s; WristX = 0.200f * s;
            Hair = HairStyle.LongBangs;
            Glasses = GlassesStyle.Rect;
            HeadH = 0.138f * Height; // a little big-headed (small young woman)
            HandScale = 0.88f;
            FootLen = 0.235f; FootW = 0.085f; ShoeH = 0.11f;
        }

        void Nerd()
        {
            Skeleton(1.88f, false);
            float s = Scale;
            Torso = Rings(s,
                new Ring(0.138f, 0.076f, 0.085f), new Ring(0.152f, 0.085f, 0.096f), new Ring(0.146f, 0.086f, 0.086f),
                new Ring(0.138f, 0.088f, 0.080f), new Ring(0.143f, 0.092f, 0.080f), new Ring(0.152f, 0.095f, 0.085f),
                new Ring(0.160f, 0.090f, 0.090f), new Ring(0.186f, 0.070f, 0.072f), new Ring(0.054f, 0.050f, 0.055f, -0.01f));
            Belly = 0.008f * s;
            MaleLimbs(s, 0.86f);
            ShoulderX = 0.162f * s; ElbowX = 0.182f * s; WristX = 0.195f * s;
            Hair = HairStyle.Short;
            Glasses = GlassesStyle.Round;
            HeadH = 0.131f * Height;
        }

        void Omar()
        {
            Skeleton(1.95f, false);
            Heavy = true;
            float s = Scale;
            Torso = Rings(s,
                new Ring(0.165f, 0.095f, 0.105f), new Ring(0.195f, 0.115f, 0.125f), new Ring(0.198f, 0.135f, 0.112f),
                new Ring(0.196f, 0.142f, 0.106f), new Ring(0.200f, 0.140f, 0.108f), new Ring(0.214f, 0.132f, 0.114f),
                new Ring(0.224f, 0.120f, 0.118f), new Ring(0.246f, 0.090f, 0.090f), new Ring(0.072f, 0.064f, 0.070f, -0.01f));
            MaleLimbs(s, 1.22f);
            ShoulderX = 0.198f * s; ElbowX = 0.224f * s; WristX = 0.236f * s;
            HeadShape = HeadShape.Sack;
            HeadRings = SackHead;
            HeadSquare = 3.2f;
            Nose = 0.02f;
            HeadH = 0.15f * Height;
            HandScale = 1.25f;
            FootLen = 0.30f; FootW = 0.11f; ShoeH = 0.17f;
            Apron = true; Noose = true; Skirt = true;
        }

        // ------------------------------------------------------------------------------------------ figures
        public static BodySpec For(FigureKind kind, int seed)
        {
            var rng = new DeterministicRandom(seed, 77);
            var b = new BodySpec();
            b.Texture = CharacterAtlas.TexturePath(kind);
            float h = kind == FigureKind.Corpse ? rng.Range(1.68f, 1.82f) : rng.Range(1.62f, 1.84f);
            bool female = kind != FigureKind.Corpse && rng.Chance(0.35f);
            b.Skeleton(h, female);
            float s = b.Scale;
            if (female)
            {
                b.Torso = Rings(s,
                    new Ring(0.148f, 0.080f, 0.098f), new Ring(0.172f, 0.088f, 0.115f), new Ring(0.160f, 0.086f, 0.096f),
                    new Ring(0.132f, 0.080f, 0.076f), new Ring(0.140f, 0.088f, 0.080f), new Ring(0.150f, 0.096f, 0.085f),
                    new Ring(0.156f, 0.092f, 0.090f), new Ring(0.184f, 0.066f, 0.070f), new Ring(0.055f, 0.048f, 0.052f, -0.01f));
                b.Bust = 0.03f * s;
                b.FemaleLimbs(s, 0.98f);
                b.ShoulderX = 0.152f * s; b.ElbowX = 0.176f * s; b.WristX = 0.198f * s;
            }
            else
            {
                float thin = kind == FigureKind.Corpse ? 0.88f : 1f;
                b.Torso = Rings(s,
                    new Ring(0.145f * thin, 0.080f, 0.090f), new Ring(0.160f * thin, 0.090f, 0.102f), new Ring(0.152f * thin, 0.090f * thin, 0.090f),
                    new Ring(0.144f * thin, 0.088f * thin, 0.082f), new Ring(0.155f * thin, 0.100f * thin, 0.086f), new Ring(0.170f * thin, 0.108f, 0.092f),
                    new Ring(0.180f * thin, 0.100f, 0.096f), new Ring(0.205f * thin, 0.074f, 0.076f), new Ring(0.060f, 0.052f, 0.057f, -0.01f));
                b.MaleLimbs(s, thin);
                b.ShoulderX = 0.172f * s * thin; b.ElbowX = 0.192f * s * thin; b.WristX = 0.205f * s * thin;
            }
            b.Barefoot = true;
            if (kind == FigureKind.Corpse)
            {
                b.Hair = HairStyle.Short;
                b.Rags = true;
                b.ShoeH = 0.075f;
            }
            else
            {
                b.HeadShape = HeadShape.Featureless;
                b.Nose = 0.035f;
                b.FootLen = 0.23f; b.ShoeH = 0.08f;
            }
            return b;
        }

        /// <summary>Default locomotion speeds used to scale stride (m/s).</summary>
        public static float WalkSpeedOf(CharacterSkin skin) => skin == CharacterSkin.Omar ? 1.8f : 2.0f;
        public static float RunSpeedOf(CharacterSkin skin) => skin == CharacterSkin.Omar ? 4.8f : 4.5f;
    }
}
