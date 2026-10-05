using UnityEngine;

namespace PrisonersOfOmar
{
    /// <summary>
    /// Finds a doorway lower than a walker on the path ahead with physics rays: the lowest point of the head of the opening
    /// (scanned up from knee height along the path), how deep the frame is, where the opening's middle plane lies, and the
    /// two jamb faces (horizontal rays out from the middle of the opening). Used for Omar (2.5 m) and the 2.2 m doors.
    /// </summary>
    public static class DoorwayProbe
    {
        public struct Result
        {
            /// <summary>Middle of the opening at floor height (between the jambs when both were found).</summary>
            public Vector3 Plane;
            /// <summary>Horizontal, the way through (same side as the probe direction).</summary>
            public Vector3 Normal;
            /// <summary>Lowest point of the opening's head above the floor (m), with the visual frame head allowed for.</summary>
            public float Lintel;
            /// <summary>Thickness of the wall / frame along the way through (m).</summary>
            public float Depth;
            public bool HasLeft, HasRight;
            /// <summary>Points on the jamb faces (at chest height).</summary>
            public Vector3 Left, Right;
            public float Width => HasLeft && HasRight ? Vector3.Distance(new Vector3(Left.x, 0f, Left.z), new Vector3(Right.x, 0f, Right.z)) : 0f;
        }

        /// <summary>Door leaves swing out of the way: only walls / frames count.</summary>
        public static readonly int Mask = Layers.Solid & ~(1 << Layers.Door);

        const float Step = 0.05f, StartY = 1.0f;
        /// <summary>Openings needing less than this much head drop are not doorways for the probe.</summary>
        public const float MinDrop = 0.12f;

        /// <summary>
        /// Scans from 0.3 m behind the feet to <paramref name="ahead"/> m in front along <paramref name="dir"/>. True when a
        /// low head of an opening (under walkerHeight) no deeper than a wall is found; a long low span is a ceiling, not a door.
        /// </summary>
        public static bool Find(Vector3 feet, Vector3 dir, float walkerHeight, float ahead, out Result r)
        {
            r = default;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return false;
            dir.Normalize();
            float reach = walkerHeight + 0.15f - StartY;
            float first = float.NaN, last = float.NaN, lowest = float.MaxValue;
            for (float d = -0.3f; d <= ahead; d += Step)
            {
                Vector3 o = feet + dir * d + Vector3.up * StartY;
                // a beam he only has to dip his head for (porch roof, joists) is left to the simple stoop: only a head that
                // is clearly too low for him counts
                bool low = Physics.Raycast(o, Vector3.up, out var h, reach, Mask, QueryTriggerInteraction.Ignore)
                           && StartY + h.distance < walkerHeight - MinDrop;
                if (low)
                {
                    if (float.IsNaN(first)) first = d;
                    last = d;
                    lowest = Mathf.Min(lowest, StartY + h.distance);
                }
                else if (!float.IsNaN(first)) break;   // the first low span only
            }
            if (float.IsNaN(first)) return false;
            float depth = last - first + Step;
            if (depth > 0.9f) return false;
            Vector3 plane = feet + dir * ((first + last) * 0.5f);
            // jambs: out from the middle of the opening at chest height
            Vector3 right = Vector3.Cross(Vector3.up, dir);
            Vector3 c = new Vector3(plane.x, feet.y + 1.3f, plane.z);
            bool hl = Physics.Raycast(c, -right, out var hL, 1.3f, Mask, QueryTriggerInteraction.Ignore);
            bool hr = Physics.Raycast(c, right, out var hR, 1.3f, Mask, QueryTriggerInteraction.Ignore);
            Vector3 normal = dir;
            if (hl && hr)
            {
                // the frame's own orientation: across the jambs, the way through is perpendicular
                Vector3 along = hR.point - hL.point; along.y = 0f;
                if (along.sqrMagnitude > 0.04f)
                {
                    Vector3 n = Vector3.Cross(along.normalized, Vector3.up);
                    normal = Vector3.Dot(n, dir) >= 0f ? n : -n;
                }
                Vector3 mid = (hL.point + hR.point) * 0.5f;
                plane = new Vector3(mid.x, feet.y, mid.z);
                // keep the plane where the lintel was found along the way through
                plane += normal * Vector3.Dot(feet + dir * ((first + last) * 0.5f) - plane, normal);
            }
            r = new Result
            {
                Plane = new Vector3(plane.x, feet.y, plane.z), Normal = normal,
                // the visual frame head sits a few cm under the wall collider
                Lintel = lowest - 0.04f, Depth = depth,
                HasLeft = hl, HasRight = hr,
                Left = hl ? hL.point : Vector3.zero, Right = hr ? hR.point : Vector3.zero,
            };
            return true;
        }
    }
}
