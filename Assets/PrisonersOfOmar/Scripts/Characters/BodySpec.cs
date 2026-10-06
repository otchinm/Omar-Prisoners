using UnityEngine;

namespace PrisonersOfOmar.Characters
{
    internal enum HairStyle { None, Spiky, Short, LongWavy, LongBangs, Curly }
    internal enum GlassesStyle { None, Round, Rect }
    internal enum HeadShape { Human, Sack, Featureless }

    /// <summary>
    /// (secret, Prisoner8) A finer, sculpted head instead of the classic 12-sided one: profile rings at any heights,
    /// front-weighted vertex angles (dense round the face, sparse at the back under the hair), face relief (sockets,
    /// cheekbones, lips, chin) and a separate low-poly nose and ears (BodyMeshGenerator.SculptedHead). Same texture
    /// mapping as the classic head (u from theta, v from the height), so the painted face lines up; the ears use
    /// the Extra region of the atlas (painted by char_textures.py, "extra": "ear").
    /// </summary>
    internal sealed class HeadSculpt
    {
        public float[] Y;        // ring heights in head units (chin 0, crown 1); the last one is the crown pole
        public Ring[] Rings;     // profile of each ring (head units)
        public float[] Square;   // superellipse exponent of each ring
        public float[] Theta;    // vertex angles, 180 down to -180 (0 = front, + = character's right)
    }

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
        public float TorsoSquare = 2.2f;         // superellipse exponent of torso rings (2 = round, higher = boxy)
        public int TorsoSides = 12;              // ring vertex count of the torso tube (women: 16, for the bust)
        public float ShoulderSlope = 0.022f;     // how much the shoulder line drops towards the arms (x Scale)
        public float Nose = 0.07f;               // nose protrusion (head units)
        public float HandScale = 1f;
        public float FootLen = 0.26f, FootW = 0.095f, ShoeH = 0.11f;
        // (iteration 2) feminine shape: bust lobes centred at +-BustAngle degrees (width BustWidth), an extra
        // under-bust ring, rounder buttocks (meters at 1.8 m scale, like Bust)
        public float BustAngle = 30f, BustWidth = 22f;
        public float Butt;
        /// <summary>Superellipse exponent of the crotch ring (0 = TorsoSquare); boxier so both thigh tops fit under it.</summary>
        public float CrotchSquare;
        /// <summary>Arm strip t (0 = wrist) where a long sleeve starts (0 = bare arms); the first person forearm gets a cuff there.</summary>
        public float SleeveT;
        /// <summary>(iteration 2) Flared mini skirt of a dress, from the waist to this far below the crotch (m, 0 = none).
        /// Textured with the bottom of the torso strip (v 0.40 at the waist .. 0 at the hem).</summary>
        public float SkirtLen;
        /// <summary>(iteration 2) The arm tubes end flush with the shoulder line under a rounded deltoid cap (no square corners).</summary>
        public bool RoundShoulders;

        // features
        public HeadShape HeadShape = HeadShape.Human;
        public HairStyle Hair = HairStyle.None;
        public GlassesStyle Glasses = GlassesStyle.None;
        public bool Apron, Noose, Skirt, Rags;
        public bool Barefoot;                    // feet use the skin / plastic texture for the sole too
        public bool BobHair;                     // LongBangs cut short at the jaw / neck (the grandmother, the camerawoman)
        /// <summary>(iteration 2) Bob with a side-swept fringe and jaw-length side locks (texture: hairline "bob" + bob_sweep).</summary>
        public float FringeSweep;
        /// <summary>(secret) Sculpted head (null = the classic head of <see cref="HeadRings"/>).</summary>
        public HeadSculpt Sculpt;

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

        /// <summary>Young woman: slimmer neck, soft narrow jaw and small pointed chin, full cheeks high up, rounder skull.</summary>
        static readonly Ring[] FemaleHead =
        {
            new Ring(0.215f, 0.195f, 0.205f, -0.06f), // neck base
            new Ring(0.185f, 0.175f, 0.185f, -0.05f), // neck mid
            new Ring(0.200f, 0.315f, 0.215f, -0.03f), // chin / jaw: narrow V
            new Ring(0.262f, 0.375f, 0.310f, -0.005f),// mouth
            new Ring(0.298f, 0.395f, 0.365f, 0.00f),  // nose / cheekbones
            new Ring(0.318f, 0.380f, 0.415f, 0.00f),  // eyes
            new Ring(0.324f, 0.395f, 0.435f, 0.00f),  // brow
            new Ring(0.316f, 0.365f, 0.430f, 0.00f),  // forehead
            new Ring(0.258f, 0.275f, 0.345f, -0.01f), // upper skull
            new Ring(0.0f, 0.0f, 0.0f, -0.03f),       // crown (pole)
        };

        /// <summary>Boy: round chubby cheeks, short soft chin, slightly bigger skull.</summary>
        static readonly Ring[] KidHead =
        {
            new Ring(0.225f, 0.20f, 0.21f, -0.06f),
            new Ring(0.20f, 0.185f, 0.195f, -0.05f),
            new Ring(0.255f, 0.325f, 0.235f, -0.03f), // round soft jaw
            new Ring(0.305f, 0.395f, 0.33f, -0.005f), // chubby cheeks
            new Ring(0.322f, 0.405f, 0.38f, 0.00f),
            new Ring(0.330f, 0.385f, 0.425f, 0.00f),
            new Ring(0.335f, 0.40f, 0.445f, 0.00f),
            new Ring(0.330f, 0.375f, 0.445f, 0.00f),
            new Ring(0.272f, 0.285f, 0.355f, -0.01f),
            new Ring(0.0f, 0.0f, 0.0f, -0.03f),
        };

        /// <summary>Lean young man: strong neck, narrow defined jaw and chin, high cheekbones, long straight nose.</summary>
        static readonly Ring[] LeanHead =
        {
            new Ring(0.238f, 0.222f, 0.242f, -0.06f), // neck base
            new Ring(0.198f, 0.190f, 0.204f, -0.05f), // neck mid: narrower than the jaw
            new Ring(0.214f, 0.352f, 0.236f, -0.03f), // chin / jaw: narrow defined jaw, the chin forward
            new Ring(0.264f, 0.396f, 0.322f, -0.005f),// mouth
            new Ring(0.292f, 0.410f, 0.376f, 0.00f),  // nose / high cheekbones
            new Ring(0.312f, 0.390f, 0.418f, 0.00f),  // eyes
            new Ring(0.326f, 0.408f, 0.438f, 0.00f),  // brow
            new Ring(0.318f, 0.370f, 0.430f, 0.00f),  // forehead
            new Ring(0.262f, 0.272f, 0.342f, -0.01f), // upper skull
            new Ring(0.0f, 0.0f, 0.0f, -0.03f),       // crown (pole)
        };

        /// <summary>Prisoner8's sculpted head: lean oval face, a narrow defined jaw over a clean jaw line, a firm chin,
        /// high cheekbones, eyes set in under a straight brow; nose and ears are their own parts
        /// (BodyMeshGenerator.SculptedNose / SculptedEars).</summary>
        static readonly HeadSculpt LeanSculpt = new HeadSculpt
        {
            Y = new[] { -0.50f, -0.22f, -0.08f, 0.00f, 0.07f, 0.13f, 0.19f, 0.27f, 0.36f, 0.45f, 0.54f, 0.64f, 0.77f, 0.89f, 1.00f },
            Rings = new[]
            {
                new Ring(0.236f, 0.218f, 0.240f, -0.06f),  // neck base
                new Ring(0.196f, 0.186f, 0.202f, -0.05f),  // neck
                new Ring(0.192f, 0.204f, 0.210f, -0.045f), // throat, under the jaw (the jaw line casts its shadow here)
                new Ring(0.205f, 0.330f, 0.222f, -0.035f), // jaw line, bottom of the chin
                new Ring(0.224f, 0.360f, 0.252f, -0.025f), // chin
                new Ring(0.236f, 0.346f, 0.280f, -0.015f), // under the lower lip
                new Ring(0.246f, 0.368f, 0.302f, -0.01f),  // mouth
                new Ring(0.262f, 0.380f, 0.338f, 0.00f),   // upper lip, base of the nose
                new Ring(0.282f, 0.378f, 0.378f, 0.00f),   // cheekbones
                new Ring(0.296f, 0.366f, 0.410f, 0.00f),   // eyes (set in)
                new Ring(0.302f, 0.384f, 0.432f, 0.00f),   // brow
                new Ring(0.300f, 0.366f, 0.432f, 0.00f),   // forehead
                new Ring(0.278f, 0.322f, 0.408f, -0.005f), // top of the forehead
                new Ring(0.214f, 0.240f, 0.326f, -0.01f),  // skull
                new Ring(0.0f, 0.0f, 0.0f, -0.03f),        // crown (pole)
            },
            Square = new[] { 2f, 2f, 2f, 2.5f, 2.4f, 2.2f, 2.1f, 2.1f, 2.2f, 2.2f, 2.2f, 2.2f, 2.2f, 2.2f, 2f },
            Theta = new[] { 180f, 150f, 118f, 90f, 68f, 48f, 30f, 14f, 0f, -14f, -30f, -48f, -68f, -90f, -118f, -150f, -180f },
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
                case CharacterSkin.Prisoner5: b.Camerawoman(); break;
                case CharacterSkin.Prisoner6: b.Kid(); break;
                case CharacterSkin.Prisoner7: b.Father(); break;
                case CharacterSkin.Prisoner8: b.Htn(); break;
                default: b.Omar(); break;
            }
            return b;
        }

        /// <summary>(iteration 2) Slim, clearly feminine limbs for the playable women: slender ankles / wrists, rounded calves, full thighs.</summary>
        void WomanLimbs(float s, float bulk)
        {
            LegRx = Arr(s * bulk, 0.027f, 0.033f, 0.047f, 0.040f, 0.042f, 0.051f, 0.071f, 0.088f, 0.078f);
            LegRz = Arr(s * bulk, 0.033f, 0.038f, 0.053f, 0.044f, 0.046f, 0.054f, 0.072f, 0.074f, 0.062f);
            ArmR = Arr(s * bulk, 0.0205f, 0.025f, 0.029f, 0.027f, 0.032f, 0.036f, 0.041f);
        }

        /// <summary>(iteration 2) Shared look of the playable women: soft narrow face, 16-sided torso with a real bust.</summary>
        void Woman(float bust, float butt)
        {
            HeadRings = FemaleHead;
            HeadSquare = 2.05f;
            Nose = 0.052f;
            TorsoSides = 16;
            TorsoSquare = 2.05f;
            BustAngle = 24f; BustWidth = 17f;
            Bust = bust * Scale;
            Butt = butt * Scale;
            ShoulderSlope = 0.026f;
            RoundShoulders = true;
            HipJointX = 0.100f * Scale; // wide pelvis: the thighs carry the hip line down without a step
            CrotchSquare = 3.2f;
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
            // curvy: full bust in the tight crop top, small waist, wide hips (CzRaIQcVIAAElQD.jpg)
            Skeleton(1.68f, true);
            float s = Scale;
            Torso = Rings(s,
                new Ring(0.220f, 0.092f, 0.098f), new Ring(0.198f, 0.094f, 0.112f), new Ring(0.172f, 0.084f, 0.100f),
                new Ring(0.124f, 0.074f, 0.070f), new Ring(0.132f, 0.082f, 0.074f), new Ring(0.146f, 0.088f, 0.082f),
                new Ring(0.154f, 0.086f, 0.086f), new Ring(0.178f, 0.062f, 0.066f), new Ring(0.049f, 0.043f, 0.047f, -0.01f));
            Woman(0.054f, 0.018f);
            WomanLimbs(s, 1.0f);
            ShoulderX = 0.150f * s; ElbowX = 0.182f * s; WristX = 0.226f * s;
            Hair = HairStyle.LongWavy;
            HandScale = 0.88f;
            SleeveT = 0.05f;
            FootLen = 0.24f; FootW = 0.085f; ShoeH = 0.10f;
        }

        void Redhead()
        {
            // petite and slim, softer curves under the sweater
            Skeleton(1.58f, true);
            float s = Scale;
            Torso = Rings(s,
                new Ring(0.216f, 0.092f, 0.098f), new Ring(0.194f, 0.092f, 0.110f), new Ring(0.170f, 0.086f, 0.098f),
                new Ring(0.128f, 0.078f, 0.074f), new Ring(0.136f, 0.084f, 0.078f), new Ring(0.148f, 0.090f, 0.084f),
                new Ring(0.155f, 0.088f, 0.088f), new Ring(0.178f, 0.064f, 0.068f), new Ring(0.050f, 0.044f, 0.048f, -0.01f));
            Woman(0.040f, 0.014f);
            WomanLimbs(s, 1.02f);
            ShoulderX = 0.150f * s; ElbowX = 0.180f * s; WristX = 0.222f * s;
            Hair = HairStyle.LongBangs;
            Glasses = GlassesStyle.Rect;
            HeadH = 0.138f * Height; // a little big-headed (small young woman)
            HandScale = 0.86f;
            SleeveT = 0.05f;
            FootLen = 0.235f; FootW = 0.085f; ShoeH = 0.11f;
        }

        /// <summary>(iteration 2) Prisoner5: slim young woman in a grey plaid long-sleeve mini dress, auburn bob, mary-janes.</summary>
        void Camerawoman()
        {
            Skeleton(1.65f, true);
            float s = Scale;
            Torso = Rings(s,
                new Ring(0.216f, 0.092f, 0.098f), new Ring(0.194f, 0.092f, 0.110f), new Ring(0.170f, 0.084f, 0.098f),
                new Ring(0.124f, 0.074f, 0.070f), new Ring(0.134f, 0.082f, 0.076f), new Ring(0.147f, 0.088f, 0.083f),
                new Ring(0.154f, 0.087f, 0.087f), new Ring(0.178f, 0.063f, 0.067f), new Ring(0.049f, 0.043f, 0.047f, -0.01f));
            Woman(0.044f, 0.014f);
            WomanLimbs(s, 0.98f);
            ShoulderX = 0.150f * s; ElbowX = 0.180f * s; WristX = 0.222f * s;
            Hair = HairStyle.LongBangs;
            BobHair = true;
            FringeSweep = 1f;
            HandScale = 0.86f;
            SleeveT = 0.05f;
            SkirtLen = 0.13f;
            FootLen = 0.235f; FootW = 0.08f; ShoeH = 0.075f; // flat mary-janes
        }

        /// <summary>(iteration 2) Prisoner6: a stocky boy (~12) - big round head, short neck, soft belly, short limbs.</summary>
        void Kid()
        {
            Skeleton(1.48f, false);
            RoundShoulders = true;
            float s = Scale;
            HeadH = 0.152f * Height;
            NeckY = 0.832f * Height;
            ShoulderY = NeckY - 0.03f * Height;
            ChestY = 0.705f * Height;
            ElbowY = ShoulderY - 0.166f * Height;
            WristY = ElbowY - 0.140f * Height;
            Torso = Rings(s,
                new Ring(0.152f, 0.088f, 0.098f), new Ring(0.168f, 0.100f, 0.110f), new Ring(0.166f, 0.106f, 0.098f),
                new Ring(0.160f, 0.108f, 0.090f), new Ring(0.160f, 0.106f, 0.090f), new Ring(0.166f, 0.104f, 0.094f),
                new Ring(0.170f, 0.098f, 0.096f), new Ring(0.192f, 0.074f, 0.078f), new Ring(0.060f, 0.054f, 0.058f, -0.01f));
            TorsoSquare = 2.05f;
            Belly = 0.014f * s;
            MaleLimbs(s, 1.02f);
            ArmR = Arr(s, 0.026f, 0.031f, 0.036f, 0.034f, 0.040f, 0.044f, 0.049f);
            ShoulderX = 0.164f * s; ElbowX = 0.190f * s; WristX = 0.206f * s;
            ShoulderSlope = 0.028f;
            HeadRings = KidHead;
            HeadSquare = 2.05f;
            Nose = 0.048f;
            Hair = HairStyle.Short;
            HandScale = 0.86f;
            SleeveT = 0.05f;
            FootLen = 0.235f; FootW = 0.09f; ShoeH = 0.10f;
        }

        /// <summary>(iteration 2) Prisoner7: the father - average build, a bit of a belly, white dress shirt, dark slacks.</summary>
        void Father()
        {
            Skeleton(1.83f, false);
            RoundShoulders = true;
            float s = Scale;
            Torso = Rings(s,
                new Ring(0.148f, 0.082f, 0.094f), new Ring(0.166f, 0.094f, 0.108f), new Ring(0.160f, 0.104f, 0.094f),
                new Ring(0.156f, 0.108f, 0.088f), new Ring(0.162f, 0.108f, 0.090f), new Ring(0.180f, 0.112f, 0.098f),
                new Ring(0.190f, 0.104f, 0.100f), new Ring(0.214f, 0.074f, 0.078f), new Ring(0.062f, 0.054f, 0.058f, -0.01f));
            Belly = 0.016f * s;
            MaleLimbs(s, 1.02f);
            ShoulderX = 0.180f * s; ElbowX = 0.202f * s; WristX = 0.214f * s;
            Hair = HairStyle.Short;
            HandScale = 1.02f;
            SleeveT = 0.06f;
            FootLen = 0.27f; FootW = 0.095f; ShoeH = 0.095f;
        }

        /// <summary>
        /// (secret, code HTN) Prisoner8: a tall young man with a swimmer's V - broad shoulders and lats over a narrow waist,
        /// lean muscular arms - and a big mop of dark curls falling over his brows; navy tee, black straight-leg trousers,
        /// chunky white sneakers.
        /// </summary>
        void Htn()
        {
            Skeleton(1.84f, false);
            RoundShoulders = true;   // rounded delts, no box corners
            float s = Scale;
            Torso = Rings(s,
                new Ring(0.144f, 0.082f, 0.092f),            // crotch
                new Ring(0.146f, 0.088f, 0.100f),            // hips (the tee hangs loose over the waistband)
                new Ring(0.140f, 0.085f, 0.087f),            // low belly
                new Ring(0.130f, 0.086f, 0.080f),            // waist: narrow
                new Ring(0.156f, 0.100f, 0.088f),            // lower ribs
                new Ring(0.202f, 0.120f, 0.102f),            // chest
                new Ring(0.226f, 0.114f, 0.110f),            // upper chest, lats
                new Ring(0.240f, 0.082f, 0.086f),            // shoulders: broad
                new Ring(0.066f, 0.058f, 0.062f, -0.01f));   // neck: strong
            TorsoSquare = 2.25f;
            ArmR = Arr(s, 0.026f, 0.039f, 0.047f, 0.041f, 0.051f, 0.057f, 0.060f);   // lean, muscular forearms and biceps
            // straight-leg trousers: a wide hem at the shoe instead of a tapering ankle, loose over the knee
            LegRx = Arr(s, 0.050f, 0.051f, 0.055f, 0.052f, 0.053f, 0.057f, 0.068f, 0.078f, 0.083f);
            LegRz = Arr(s, 0.054f, 0.055f, 0.060f, 0.055f, 0.055f, 0.060f, 0.072f, 0.081f, 0.086f);
            ShoulderX = 0.200f * s; ElbowX = 0.222f * s; WristX = 0.230f * s;
            ShoulderSlope = 0.018f;
            HeadRings = LeanHead;      // profile for the hair / eye helpers; the mesh is the sculpted head below
            HeadH = 0.139f * Height;   // a long lean face
            Sculpt = LeanSculpt;
            Hair = HairStyle.Curly;
            HandScale = 1.04f;
            FootLen = 0.29f; FootW = 0.105f; ShoeH = 0.115f;   // chunky white sneakers
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
            SleeveT = 0.045f;
        }

        void Omar()
        {
            // a real giant: ~2.3 m, so he has to stoop under door frames (the avatar bends him, HumanoidAnimator.Duck);
            // round and massive rather than boxy - deep barrel chest, a heavy gut, thick limbs, huge hands,
            // long arms hanging out past the belly
            Skeleton(2.5f, false);
            Heavy = true;
            float s = Scale;
            Torso = Rings(s,
                new Ring(0.200f, 0.130f, 0.140f), new Ring(0.236f, 0.168f, 0.160f), new Ring(0.244f, 0.214f, 0.150f),
                new Ring(0.246f, 0.226f, 0.142f), new Ring(0.244f, 0.206f, 0.146f), new Ring(0.254f, 0.180f, 0.158f),
                new Ring(0.262f, 0.160f, 0.160f), new Ring(0.268f, 0.122f, 0.128f), new Ring(0.092f, 0.084f, 0.090f, -0.01f));
            TorsoSquare = 2.0f;
            Belly = 0.045f * s;
            MaleLimbs(s, 1.55f);
            ArmR = Arr(s, 0.040f, 0.050f, 0.058f, 0.057f, 0.067f, 0.074f, 0.080f);
            ShoulderX = 0.236f * s; ElbowX = 0.284f * s; WristX = 0.296f * s;
            ShoulderY = NeckY - 0.052f * Height; // massive sloping shoulders, no square pads
            ShoulderSlope = 0.06f;
            ElbowY = ShoulderY - 0.176f * Height;
            WristY = ElbowY - 0.156f * Height;
            HeadShape = HeadShape.Sack;
            HeadRings = SackHead;
            HeadSquare = 2.5f;
            Nose = 0.02f;
            HeadH = 0.148f * Height;
            HandScale = 1.4f;
            FootLen = 0.30f; FootW = 0.11f; ShoeH = 0.17f;
            Apron = true; Noose = true; Skirt = true;
            SleeveT = 0.04f;
        }

        /// <summary>(iteration 2) The grandmother: small, frail and stooped, thin bony limbs, a white bob, floral dress.</summary>
        public static BodySpec Grandma(bool dead)
        {
            var b = new BodySpec();
            b.Texture = dead ? "Textures/Characters/grandma_dead" : "Textures/Characters/grandma";
            b.Skeleton(1.52f, true);
            float s = b.Scale;
            b.Torso = Rings(s,
                new Ring(0.150f, 0.090f, 0.100f), new Ring(0.168f, 0.100f, 0.112f), new Ring(0.158f, 0.104f, 0.096f),
                new Ring(0.140f, 0.098f, 0.084f), new Ring(0.140f, 0.094f, 0.086f), new Ring(0.146f, 0.090f, 0.094f),
                new Ring(0.150f, 0.084f, 0.100f), new Ring(0.172f, 0.064f, 0.080f), new Ring(0.046f, 0.040f, 0.046f, -0.01f));
            b.TorsoSquare = 2.0f;
            b.Bust = 0.018f * s;
            b.Belly = 0.012f * s;
            b.FemaleLimbs(s, 0.78f);
            b.ShoulderX = 0.146f * s; b.ElbowX = 0.164f * s; b.WristX = 0.178f * s;
            b.ShoulderSlope = 0.035f;
            b.Hair = HairStyle.LongBangs;
            b.BobHair = true;
            b.FringeSweep = 1f;           // white bob with a side-swept fringe over one eye (the reference)
            b.SkirtLen = 0.10f;           // a long house dress over her lap and knees
            b.SleeveT = 0.06f;            // long sleeves to the wrist
            b.RoundShoulders = true;
            b.HeadH = 0.142f * b.Height; // old, shrunken body: the head looks big
            b.Nose = 0.085f;
            b.HandScale = 0.86f;
            b.FootLen = 0.22f; b.FootW = 0.08f; b.ShoeH = 0.07f;
            return b;
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
