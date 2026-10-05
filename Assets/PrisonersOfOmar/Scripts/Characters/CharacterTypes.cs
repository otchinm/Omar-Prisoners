using UnityEngine;

namespace PrisonersOfOmar.Characters
{
    /// <summary>How the character holds the equipped item (third person + first person).</summary>
    public enum HoldPose : byte
    {
        None = 0,
        Lighter,       // right hand raised in front of the chest, flame up
        Flashlight,    // right hand forward, pointing
        OneHandSmall,  // keys, pills, fuse, meter... held low in right hand
        TwoHanded,     // gas can, car battery, bolt cutters, crowbar: both hands
        Cleaver,       // Omar: cleaver in right hand, arm hanging
        Bottle,        // ready to throw
        Pistol,        // (iteration 2) revolver held forward in the right hand
    }

    /// <summary>One-shot animations. Values are sent over the network.</summary>
    public enum CharacterAction : byte
    {
        None = 0,
        Pickup,     // bend / reach down
        Interact,   // hands forward (doors, switches, keypad)
        UseItem,    // generic use (bandage wrap, pills)
        Attack,     // Omar: overhead cleaver chop (impact event at the down swing)
        Scream,     // Omar: head back, arms spread, shaking
        Search,     // Omar: look left/right, tilt head
        Grab,       // Omar: reach and grab a prisoner (capture)
        PlaceTrap,  // Omar: crouch, hands to the ground
        Stunned,    // stagger back holding face (explosion, crowbar)
        HitReact,   // flinch when hit
        Struggle,   // wriggle (bear trap, grabbed)
        Pour,       // tilt container (gas can, lighter fuel)
        Throw,      // throw a bottle
        Cut,        // bolt cutters squeeze
        Heal,       // wrap bandage around arm
        Wave,       // lobby / idle flavor
        // ---- iteration 2
        BedLift,    // Omar: stand beside a bed, grab the frame with both hands and tip it up on one side (~1.6 s)
        ChopMeat,   // Omar: one big overhead cleaver slam onto the butcher table (loopable, ~1.1 s)
        CrawlUnder, // prisoner: drop down and slide under a bed (~0.9 s, ends lying flat)
        CrawlOut,   // prisoner: slide out from under a bed and stand up (~0.9 s)
        Shoot,      // prisoner: fire the revolver (recoil)
        Cower,      // prisoner: flinch back raising both arms to protect the head
        Push,       // shoulder / hand push (Omar shoving a door open while walking)
        PeekUnder,  // Omar: drops onto one knee and jerks his head down sideways to look under a bed (~0.85 s)
    }

    /// <summary>Persistent body states.</summary>
    public enum CharacterPose : byte
    {
        Normal = 0,
        CagedSit,  // sitting on the floor hugging knees (inside a cage)
        Downed,    // lying on the floor, crawling slightly
        Dead,      // limp on the floor
        Hidden,    // renderers hidden (inside a hiding spot)
        Seated,    // in a car seat
        Trapped,   // one leg caught (bear trap), kneeling
    }

    /// <summary>
    /// Omar squeezing through a doorway lower than he is: where the opening is (from a physics probe), how far his head has
    /// to go down, where his hands take hold of the frame, and which of the passing styles he uses. World space; the
    /// animator turns it into a pose (<see cref="HumanoidAnimator.Doorway"/>).
    /// </summary>
    public struct DoorwayPass
    {
        public const int Variants = 4;

        public bool Active;
        /// <summary>0 brace (both hands on the jambs at the shoulders), 1 hook (left hand high in the top corner, right low,
        /// leaning into it), 2 crouch (knees deep, hands low on the jambs), 3 lintel (left hand pulls on the head of the
        /// frame, right hand on the jamb, ducking under the arm).</summary>
        public int Variant;
        /// <summary>Signed distance (m) of the feet past the doorway plane along <see cref="Normal"/>: below 0 approaching.</summary>
        public float Progress;
        /// <summary>How far the top of the head must come down to pass under the lintel (m).</summary>
        public float Drop;
        public Vector3 Plane, Normal;
        public bool HasLeft, HasRight;
        /// <summary>Where each hand takes hold (wrist goals, world).</summary>
        public Vector3 LeftGrip, RightGrip;
        /// <summary>The lintel grip variant: the left hand is above the opening, palm up under the frame head.</summary>
        public bool LeftOnLintel;

        /// <summary>
        /// Builds the pass for a probed opening: plane point (floor, centred), normal (the way through), the lintel height
        /// above the floor, the depth of the frame and the jamb faces (points at any height on each side, with which side
        /// was found). Grip heights / positions depend on the variant and the walker's height.
        /// </summary>
        public static DoorwayPass Make(int variant, Vector3 plane, Vector3 normal, float lintel, float depth, float walkerHeight,
            bool hasLeft, Vector3 leftFace, bool hasRight, Vector3 rightFace)
        {
            normal.y = 0f;
            normal = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.forward;
            var d = new DoorwayPass
            {
                Active = true, Variant = ((variant % Variants) + Variants) % Variants, Plane = plane, Normal = normal,
                Drop = Mathf.Max(0f, walkerHeight + 0.07f - lintel), HasLeft = hasLeft, HasRight = hasRight,
            };
            // the hands close round the near edge of the jambs (the corner on the approach side), a palm's width in
            float nearEdge = -(Mathf.Clamp(depth, 0.08f, 0.5f) * 0.5f) + 0.05f;
            float hl, hr;   // grip heights, as a fraction of the lintel height
            switch (d.Variant)
            {
                case 1: hl = 0.9f; hr = 0.56f; break;
                case 2: hl = 0.5f; hr = 0.47f; break;
                case 3: hl = 1f; hr = 0.66f; break;
                default: hl = 0.7f; hr = 0.68f; break;
            }
            float baseY = plane.y;
            Vector3 Grip(Vector3 face, float frac)
            {
                Vector3 p = face;
                // pull the face point back onto the plane through the doorway centre, then to the near edge
                p -= normal * Vector3.Dot(p - plane, normal);
                p += normal * nearEdge;
                p.y = baseY + lintel * frac;
                // wrists sit a little off the frame (the palm is between)
                Vector3 inward = plane - p; inward.y = 0f;
                return p + (inward.sqrMagnitude > 1e-6f ? inward.normalized : Vector3.zero) * 0.045f;
            }
            d.LeftGrip = hasLeft ? Grip(leftFace, hl) : Vector3.zero;
            d.RightGrip = hasRight ? Grip(rightFace, hr) : Vector3.zero;
            if (d.Variant == 3)
            {
                // left hand up under the head of the frame, a third of the way from the left jamb
                Vector3 l = hasLeft ? leftFace : plane - Vector3.Cross(Vector3.up, normal) * 0.45f;
                Vector3 c = plane + (new Vector3(l.x, plane.y, l.z) - plane) * 0.55f + normal * nearEdge;
                c.y = baseY + lintel - 0.06f;
                d.LeftGrip = c;
                d.HasLeft = true;
                d.LeftOnLintel = true;
            }
            return d;
        }
    }
}
