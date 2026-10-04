using System.Collections.Generic;
using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>
    /// Omar's detection audio. When Omar spots a prisoner the "find" sound plays and a chase bed starts;
    /// his screams during a chase belong to it. The host only ends a chase after the prisoner has been out
    /// of Omar's line of sight for <see cref="GameInfo.ChaseSoundLinger"/> (4) seconds; at that moment every
    /// related sound is stopped here (short fade so it does not click).
    /// </summary>
    public sealed class ChaseAudio : MonoBehaviour
    {
        readonly HashSet<int> _targets = new HashSet<int>();
        readonly Dictionary<int, AudioSource> _find = new Dictionary<int, AudioSource>();
        readonly List<AudioSource> _screams = new List<AudioSource>();
        AudioSource _chaseLoop;
        float _chaseVolume;

        public bool Active => _targets.Count > 0;
        public bool IsTarget(int id) => _targets.Contains(id);

        public void SetChase(int target, bool active)
        {
            var w = MatchWorld.Instance;
            if (w == null) return;
            if (active)
            {
                if (!_targets.Add(target)) return;
                var omar = w.OmarAvatar;
                Vector3 p = omar != null ? omar.ChestPosition : Vector3.zero;
                var src = AudioManager.Play3D(Snd.OmarFind, p, 1f, 1f, 7f, 75f, AudioCategory.Omar, omar != null ? omar.transform : null);
                if (src != null) _find[target] = src;
                if (target == w.LocalId)
                {
                    AudioManager.Play2D(Snd.StingSpotted, 0.9f, 1f, AudioCategory.Stinger);
                    if (w.LocalAvatar != null && !w.LocalIsOmar) AudioManager.Play2D(w.LocalAvatar.Voice(VoiceLine.Gasp), 0.7f);
                    VhsEffect.TriggerGlitch(0.25f, 0.25f);
                    w.AddMessage("HE SEES YOU", 2f);
                }
            }
            else
            {
                if (!_targets.Remove(target)) return;
                if (_find.TryGetValue(target, out var f)) { AudioManager.Stop(f, 0.3f); _find.Remove(target); }
                if (_targets.Count == 0) StopAll(0.4f);
            }
        }

        public void PlayScream(Avatar omar, int index)
        {
            Vector3 p = omar != null ? omar.ChestPosition : Vector3.zero;
            var src = AudioManager.Play3D(Snd.OmarScream(index), p, 1f, 1f, 9f, 95f, AudioCategory.Omar, omar != null ? omar.transform : null);
            AudioManager.Duck(0.3f, 2.5f);
            if (src != null && Active) _screams.Add(src);
        }

        /// <summary>Tripwire siren (Omar's "Alarm" asset): loud, far reaching, independent from line of sight.</summary>
        public void PlayAlarm(Vector3 pos)
        {
            AudioManager.Play3D(Snd.OmarAlarm, pos, 1f, 1f, 6f, 130f, AudioCategory.Omar);
            AudioManager.Duck(0.4f, 4f);
        }

        /// <summary>Stops the chase bed and every find / scream sound tied to the chase.</summary>
        public void StopAll(float fade)
        {
            foreach (var kv in _find) AudioManager.Stop(kv.Value, fade);
            _find.Clear();
            foreach (var s in _screams) AudioManager.Stop(s, fade);
            _screams.Clear();
            _targets.Clear();
            if (_chaseLoop != null) { AudioManager.Stop(_chaseLoop, fade); _chaseLoop = null; }
            _chaseVolume = 0f;
        }

        void Update()
        {
            _screams.RemoveAll(s => s == null || !s.isPlaying);
            var w = MatchWorld.Instance;
            if (w == null) return;

            float want = 0f;
            if (_targets.Count > 0)
            {
                if (_targets.Contains(w.LocalId)) want = 0.85f;
                else if (w.LocalIsOmar) want = 0.5f;
                else
                {
                    var omar = w.OmarAvatar;
                    var me = w.LocalAvatar;
                    if (omar != null && me != null)
                    {
                        float d = Vector3.Distance(omar.Position, me.Position);
                        want = Mathf.Clamp01(1f - d / 30f) * 0.45f;
                    }
                }
            }
            _chaseVolume = Mathf.MoveTowards(_chaseVolume, want, Time.deltaTime * 1.5f);
            if (_chaseVolume > 0.01f && _chaseLoop == null) _chaseLoop = AudioManager.Loop2D(Snd.ChaseLoop, 0f, AudioCategory.Stinger, 0.1f);
            if (_chaseLoop != null)
            {
                AudioManager.SetVolume(_chaseLoop, _chaseVolume);
                if (_chaseVolume <= 0.01f && want <= 0f) { AudioManager.Stop(_chaseLoop, 0.3f); _chaseLoop = null; }
            }
        }
    }
}
