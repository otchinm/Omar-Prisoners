using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // The cage room ceiling vent (client side): unscrew the cover with a screwdriver, crouch through the duct and push
    // the grate out of the kitchen ceiling - it hisses down in a cloud of dust and crashes onto the floor.
    public sealed partial class MatchWorld
    {
        void BuildVent()
        {
            var v = Map != null ? Map.CeilingVent : null;
            if (v == null) return;
            if (v.CoverInteract != null) InteractableRef.Attach(v.CoverInteract, new VentCover(v));
            if (v.HatchInteract != null) InteractableRef.Attach(v.HatchInteract, new VentGrate(v));
        }

        void OnVentState(ObjectiveData prev, ObjectiveData o)
        {
            var v = Map != null ? Map.CeilingVent : null;
            if (v == null) return;
            if (o.VentScrews != prev.VentScrews)
            {
                for (int i = 0; i < v.Screws.Length; i++)
                {
                    var s = v.Screws[i];
                    if (s == null) continue;
                    bool gone = i < o.VentScrews;
                    if (gone && s.gameObject.activeSelf)
                        AudioManager.Play3D(Snd.VentScrewDrop, s.position + Vector3.down * 0.3f, 0.6f, Random.Range(0.92f, 1.08f), 1f, 9f);
                    s.gameObject.SetActive(!gone);
                }
                if (o.VentScrews >= 4 && prev.VentScrews < 4) StartCoroutine(TipVentCover(v));
            }
            if (o.VentHatchOpen && !prev.VentHatchOpen) StartCoroutine(DropVentGrate(v));
        }

        /// <summary>The last screw is out: the cover leans out of its frame and claps flat onto the floor.</summary>
        System.Collections.IEnumerator TipVentCover(CeilingVentInfo v)
        {
            if (v.CoverCollider != null) v.CoverCollider.enabled = false;
            var tr = v.Cover;
            if (tr == null) yield break;
            Quaternion r0 = tr.rotation;
            AudioManager.Play3D(Snd.VentCoverOff, tr.position + Vector3.up * 0.5f, 0.85f, 1f, 1.5f, 16f);
            yield return new WaitForSeconds(0.05f);
            const float dur = 0.37f;   // the slap in the sound is at 0.42 s
            for (float t = 0f; t < dur; t += UnityEngine.Time.deltaTime)
            {
                float k = t / dur;
                tr.rotation = Quaternion.AngleAxis(v.CoverTipAngle * k * k, v.CoverTipAxis) * r0;
                yield return null;
            }
            try { PsxFx.Dust(tr.position + Quaternion.AngleAxis(v.CoverTipAngle, v.CoverTipAxis) * Vector3.up * 0.6f, 0.6f); } catch { }
            for (float b = 0f; b < 0.22f; b += UnityEngine.Time.deltaTime)
            {
                float hop = Mathf.Sin(b / 0.22f * Mathf.PI) * 4f;
                tr.rotation = Quaternion.AngleAxis(v.CoverTipAngle - hop, v.CoverTipAxis) * r0;
                yield return null;
            }
            tr.rotation = Quaternion.AngleAxis(v.CoverTipAngle, v.CoverTipAxis) * r0;
        }

        /// <summary>The kitchen grate pops loose, falls (tumbling half over) and bounces flat on the floor.</summary>
        System.Collections.IEnumerator DropVentGrate(CeilingVentInfo v)
        {
            if (v.HatchCollider != null) v.HatchCollider.enabled = false;
            if (v.HatchInteract != null) v.HatchInteract.enabled = false;
            var tr = v.Hatch;
            if (tr == null) yield break;
            Vector3 p0 = tr.position, p1 = v.HatchRest.position;
            Quaternion r0 = tr.rotation, r1 = v.HatchRest.rotation;
            AudioManager.Play3D(Snd.VentGrateFall, p1 + Vector3.up * 0.6f, 1f, 1f, 3f, 34f);
            try { PsxFx.Dust(p0 + Vector3.down * 0.1f, 1.0f); } catch { }
            // hangs for a moment, then free fall: lands ~0.84 s after the pop (matches the clang in the sound)
            const float hang = 0.06f;
            float fall = Mathf.Sqrt(2f * Mathf.Max(0.2f, p0.y - p1.y) / 9.81f);
            for (float t = 0f; t < hang + fall; t += UnityEngine.Time.deltaTime)
            {
                float k = Mathf.Clamp01((t - hang) / fall);
                Vector3 p = Vector3.Lerp(p0, p1, k);
                p.y = Mathf.Lerp(p0.y, p1.y, k * k);
                tr.SetPositionAndRotation(p, Quaternion.Slerp(r0, r1, Mathf.SmoothStep(0f, 1f, k)));
                yield return null;
            }
            try { PsxFx.Dust(p1 + Vector3.up * 0.05f, 1.3f); } catch { }
            Quaternion tilt = r1 * Quaternion.Euler(0f, 0f, 10f);
            for (float b = 0f; b < 0.42f; b += UnityEngine.Time.deltaTime)
            {
                float k = b / 0.42f;
                float hop = Mathf.Abs(Mathf.Sin(k * Mathf.PI * 2.5f)) * (1f - k);
                tr.SetPositionAndRotation(p1 + Vector3.up * (0.06f * hop), Quaternion.Slerp(r1, tilt, hop));
                yield return null;
            }
            tr.SetPositionAndRotation(p1, r1);
        }

        /// <summary>The cover on the duct entrance in the cage room: four screws, one at a time.</summary>
        sealed class VentCover : IInteractable
        {
            readonly CeilingVentInfo _v;
            public VentCover(CeilingVentInfo v) { _v = v; }
            public Vector3 InteractPoint => _v.CoverInteract.bounds.center;

            public bool GetPrompt(Interactor who, out InteractPrompt p)
            {
                p = default;
                var w = Instance;
                if (who.IsOmar || w == null || w.Objectives.VentScrews >= 4) return false;
                int left = 4 - w.Objectives.VentScrews;
                if (who.Has(ItemType.Screwdriver))
                    p = InteractPrompt.Hold("UNSCREW THE VENT COVER (" + left + " LEFT)", 1.4f, ItemType.Screwdriver, 1.5f);
                else
                    p = InteractPrompt.Info(left == 4 ? "A VENT COVER, FOUR RUSTY SCREWS. I NEED A SCREWDRIVER"
                        : "IT HANGS ON " + left + (left == 1 ? " SCREW" : " SCREWS") + ". I NEED A SCREWDRIVER");
                return true;
            }

            public void Interact(Interactor who)
            {
                int item = who.ItemId(ItemType.Screwdriver);
                if (item >= 0) Instance?.SendUse(UseTarget.VentCover, 0, item);
            }
        }

        /// <summary>The kitchen ceiling grate seen from inside the duct (from the kitchen it can not be opened).</summary>
        sealed class VentGrate : IInteractable
        {
            readonly CeilingVentInfo _v;
            public VentGrate(CeilingVentInfo v) { _v = v; }
            public Vector3 InteractPoint => _v.HatchInteract.bounds.center;

            public bool GetPrompt(Interactor who, out InteractPrompt p)
            {
                p = default;
                var w = Instance;
                if (who.IsOmar || w == null || w.Objectives.VentHatchOpen) return false;
                if (!MatchHost.InsideVent(_v, who.Position)) return false;
                p = InteractPrompt.Hold("PUSH THE GRATE OUT - IT WILL BE LOUD", 1.2f, ItemType.None, 3f);
                return true;
            }

            public void Interact(Interactor who) => Instance?.SendUse(UseTarget.VentHatch, 0, -1);
        }
    }
}
