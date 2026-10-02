using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>
    /// Omar proximity for the viewing prisoner: lo-fi static audio + VHS interference grows as he gets close
    /// (stronger with line of sight), random static bursts, heartbeat.
    /// </summary>
    public sealed class ProximityFx : MonoBehaviour
    {
        AudioSource _static, _heavy;
        float _level;
        float _burstTimer = 3f;
        float _beatTimer;
        float _terror;

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

            float target = 0f;
            Vector3 viewer;
            bool prisonerView = TryGetViewer(w, out viewer);
            var omar = w.OmarAvatar;
            if (prisonerView && omar != null && omar.Visible && w.Ending == null)
            {
                float d = Vector3.Distance(viewer, omar.ChestPosition);
                float t = Mathf.Clamp01(1f - d / Tuning.InterferenceRadius);
                if (t > 0f)
                {
                    bool los = !Physics.Linecast(viewer, omar.ChestPosition, Layers.SightBlockers, QueryTriggerInteraction.Ignore);
                    target = Mathf.Pow(t, 1.3f) * (los ? 1f : 0.6f);
                }
            }
            target = Mathf.Max(target, _terror * 0.6f);
            _level = Mathf.MoveTowards(_level, target, dt * (target > _level ? 1.8f : 0.8f));
            VhsEffect.Interference = _level;

            // static loops
            float sv = Mathf.Clamp01(_level * 1.1f) * 0.7f;
            float hv = Mathf.Clamp01((_level - 0.55f) / 0.45f) * 0.8f;
            if (sv > 0.01f && _static == null) _static = AudioManager.Loop2D(Snd.StaticLoop, 0f, AudioCategory.Stinger, 0.05f);
            if (hv > 0.01f && _heavy == null) _heavy = AudioManager.Loop2D(Snd.StaticHeavyLoop, 0f, AudioCategory.Stinger, 0.05f);
            if (_static != null) { AudioManager.SetVolume(_static, sv); if (sv <= 0.005f) { AudioManager.Stop(_static, 0.2f); _static = null; } }
            if (_heavy != null) { AudioManager.SetVolume(_heavy, hv); if (hv <= 0.005f) { AudioManager.Stop(_heavy, 0.2f); _heavy = null; } }

            // bursts
            if (_level > 0.35f)
            {
                _burstTimer -= dt;
                if (_burstTimer <= 0f)
                {
                    _burstTimer = Random.Range(1.5f, 5f) * (1.3f - _level);
                    AudioManager.Play2D(AudioManager.Variant(Snd.StaticBurst, 3), 0.35f + _level * 0.5f, Random.Range(0.85f, 1.15f), AudioCategory.Stinger);
                    VhsEffect.TriggerGlitch(0.3f + _level * 0.6f, Random.Range(0.1f, 0.35f));
                    if (_level > 0.7f && Random.value < 0.3f) VhsEffect.TriggerRoll(0.25f);
                }
            }

            // heartbeat
            bool chased = w.Chase != null && w.Chase.IsTarget(w.LocalId);
            float fear = Mathf.Max(_level, chased ? 0.85f : 0f);
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
            if (w.Spectator != null && w.Spectator.Target != null)
            {
                viewer = w.Spectator.Target.EyePosition;
                return true;
            }
            return false;
        }
    }
}
