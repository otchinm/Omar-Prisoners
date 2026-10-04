using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>
    /// Omar proximity for the viewing prisoner: lo-fi static audio + VHS interference only while he is in sight (it
    /// kicks in the moment he appears and grows as he gets close), random static bursts, heartbeat (also when unseen).
    /// </summary>
    public sealed class ProximityFx : MonoBehaviour
    {
        AudioSource _static, _heavy;
        float _level;
        float _burstTimer = 3f;
        float _beatTimer;
        float _terror;
        bool _seen;

        /// <summary>VhsEffect.Interference when Omar is right in front of the viewer.</summary>
        const float MaxInterference = 0.35f;

        /// <summary>Extra fear for a few seconds (Omar screamed nearby).</summary>
        public void AddTerror(float amount) => _terror = Mathf.Clamp01(_terror + amount);

        void OnDestroy()
        {
            AudioManager.Stop(_static, 0.2f);
            AudioManager.Stop(_heavy, 0.2f);
            VhsEffect.Interference = 0f;
        }

        void Update()
        {
            var w = MatchWorld.Instance;
            if (w == null) return;
            float dt = Time.deltaTime;
            _terror = Mathf.MoveTowards(_terror, 0f, dt * 0.15f);

            // like the reference: silence while you just walk around; the interference only appears the moment Omar is
            // actually SEEN (in front of the camera, nothing in between) and grows as he gets closer
            float target = 0f;
            Vector3 viewer;
            bool prisonerView = TryGetViewer(w, out viewer);
            var omar = w.OmarAvatar;
            bool seen = false;
            if (prisonerView && omar != null && omar.Visible && w.Ending == null)
            {
                Vector3 to = omar.ChestPosition - viewer;
                float d = to.magnitude;
                float t = Mathf.Clamp01(1f - d / Tuning.InterferenceRadius);
                var rig = PsxCameraRig.Instance;
                Vector3 fwd = rig != null ? rig.transform.forward : to;
                bool inView = d < 2.5f || Vector3.Dot(fwd, to / Mathf.Max(d, 0.01f)) > 0.62f;
                if (t > 0f && inView && !Physics.Linecast(viewer, omar.ChestPosition, Layers.SightBlockers, QueryTriggerInteraction.Ignore))
                {
                    seen = true;
                    target = Mathf.Lerp(0.25f, 1f, Mathf.Pow(t, 1.2f));
                }
            }
            if (seen && !_seen)
            {
                // the moment he appears: a short hit of static + a picture glitch
                AudioManager.Play2D(AudioManager.Variant(Snd.StaticBurst, 3), 0.1f + target * 0.12f, Random.Range(0.9f, 1.1f), AudioCategory.Stinger);
                VhsEffect.TriggerGlitch(0.12f + target * 0.15f, 0.18f);
                _level = Mathf.Max(_level, target * 0.7f);
            }
            _seen = seen;
            if (w.Ending != null) { target = 0f; _level = 0f; _terror = 0f; }
            _level = Mathf.MoveTowards(_level, target, dt * (target > _level ? 2.5f : 0.9f));
            // the picture only degrades a little: the player must always see what is in front of them
            VhsEffect.Interference = _level * MaxInterference;

            // quiet, textured static, only while he is in sight
            float sv = Mathf.Clamp01(_level * 1.1f) * 0.12f;
            float hv = Mathf.Clamp01((_level - 0.7f) / 0.3f) * 0.06f;
            if (sv > 0.01f && _static == null) _static = AudioManager.Loop2D(Snd.StaticLoop, 0f, AudioCategory.Stinger, 0.05f);
            if (hv > 0.01f && _heavy == null) _heavy = AudioManager.Loop2D(Snd.StaticHeavyLoop, 0f, AudioCategory.Stinger, 0.05f);
            if (_static != null) { AudioManager.SetVolume(_static, sv); if (sv <= 0.005f) { AudioManager.Stop(_static, 0.2f); _static = null; } }
            if (_heavy != null) { AudioManager.SetVolume(_heavy, hv); if (hv <= 0.005f) { AudioManager.Stop(_heavy, 0.2f); _heavy = null; } }

            // bursts
            if (_level > 0.45f)
            {
                _burstTimer -= dt;
                if (_burstTimer <= 0f)
                {
                    _burstTimer = Random.Range(4f, 9f) * (1.4f - _level);
                    AudioManager.Play2D(AudioManager.Variant(Snd.StaticBurst, 3), 0.08f + _level * 0.1f, Random.Range(0.85f, 1.15f), AudioCategory.Stinger);
                    VhsEffect.TriggerGlitch(0.08f + _level * 0.12f, Random.Range(0.06f, 0.15f));
                }
            }

            // heartbeat
            bool chased = w.Chase != null && w.Chase.IsTarget(w.LocalId);
            float fear = Mathf.Max(Mathf.Max(_level, _terror * 0.6f), chased ? 0.85f : 0f);
            if (prisonerView && fear > 0.22f)
            {
                _beatTimer -= dt;
                if (_beatTimer <= 0f)
                {
                    float rate = Mathf.Lerp(0.9f, 2.3f, fear);
                    _beatTimer = 1f / rate;
                    AudioManager.Play2D(Snd.Heartbeat, Mathf.Lerp(0.25f, 0.9f, fear), 1f, AudioCategory.Stinger);
                }
            }
        }

        static bool TryGetViewer(MatchWorld w, out Vector3 viewer)
        {
            viewer = Vector3.zero;
            if (w.LocalIsOmar) return false;
            if (w.LocalPrisoner != null && w.LocalAvatar != null)
            {
                var st = w.LocalStatus;
                if (st != null && (st.Life == LifeState.Free || st.Life == LifeState.Caged))
                {
                    viewer = w.LocalAvatar.EyePosition;
                    return true;
                }
            }
            if (w.Spectator != null && w.Spectator.Target != null && !w.Spectator.Target.IsOmar)
            {
                viewer = w.Spectator.Target.EyePosition;
                return true;
            }
            return false;
        }
    }
}
