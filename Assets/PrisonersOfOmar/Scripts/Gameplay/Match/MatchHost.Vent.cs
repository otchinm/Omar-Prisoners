using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // The cage room ceiling vent (host side): screws come out one at a time, the kitchen grate only opens from inside.
    public sealed partial class MatchHost
    {
        float _ventScrewAt;

        /// <summary>One screw out of the vent cover (the 4th one and the cover tips over onto the floor).</summary>
        void UseVentCover(int p, int itemId)
        {
            var v = W.Map.CeilingVent;
            if (v == null || v.CoverInteract == null || W.Objectives.VentScrews >= 4) return;
            if (!Holds(p, itemId, ItemType.Screwdriver)) return;
            Vector3 at = v.CoverInteract.bounds.center;
            if (!Near(p, at, 2.6f) || W.Time < _ventScrewAt) return;
            _ventScrewAt = W.Time + 0.8f;
            var o = EditObj();
            o.VentScrews++;
            CommitObj(o);
            DeliverNoise(at, o.VentScrews >= 4 ? 9f : 3f);
        }

        /// <summary>Pushed the kitchen ceiling grate out from inside the duct: it crashes onto the kitchen floor.</summary>
        void UseVentHatch(int p)
        {
            var v = W.Map.CeilingVent;
            if (v == null || v.Hatch == null || W.Objectives.VentHatchOpen || W.Objectives.VentScrews < 4) return;
            if (!InsideVent(v, PosOf(p)) || !Near(p, v.Hatch.position, 2.4f)) return;
            var o = EditObj();
            o.VentHatchOpen = true;
            CommitObj(o);
            DeliverNoise(v.HatchRest.position, 18f);
        }

        internal static bool InsideVent(Map.CeilingVentInfo v, Vector3 feet)
        {
            var b = v.Inside;
            b.Expand(new Vector3(0.6f, 1.0f, 0.6f));
            return b.Contains(feet + Vector3.up * 0.3f);
        }
    }
}
