using System.Collections.Generic;
using UnityEngine;

namespace PrisonersOfOmar.Audio
{
    public enum AudioCategory
    {
        Music = 0,   // menu theme only (never during gameplay)
        Sfx,         // world / player / item sounds
        Ambience,    // beds and emitters
        Ui,          // menu clicks, tape mechanics
        Omar,        // Omar's voice / alarm / find (never ducked)
        Stinger,     // jumpscares, static bursts
    }

    /// <summary>
    /// Central audio: pooled 2D/3D one-shots, loops, menu music, ambience crossfades, ducking and listener
    /// filters (muffle while hiding, distortion on capture). Clips load from Resources by path
    /// (e.g. "Audio/Omar/find"). Volumes = clip volume * category * settings * duck, applied every frame.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        sealed class Voice
        {
            public AudioSource Src;
            public AudioCategory Category;
            public float BaseVolume = 1f;
            public float Fade = 1f, FadeTarget = 1f, FadeSpeed = 4f;
            public bool StopWhenSilent;
            public Transform Follow;
            public bool Persistent; // loops / music: not auto-recycled
            public bool InUse;
            public float StartedAt;
        }

        public static AudioManager Instance { get; private set; }

        readonly List<Voice> _voices = new List<Voice>(64);
        readonly Dictionary<AudioSource, Voice> _bySource = new Dictionary<AudioSource, Voice>();
        static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        static readonly HashSet<string> _missing = new HashSet<string>();
        static AnimationCurve _rolloff;

        Transform _listener;
        AudioLowPassFilter _lowpass;
        AudioDistortionFilter _distortion;
        Voice _music, _ambA, _ambB;
        bool _ambUseA = true;
        float _duck = 1f, _duckTarget = 1f, _duckUntil;
        float _muffle, _muffleTarget;
        float _distort, _distortTarget;

        /// <summary>Base per-category gain (balance).</summary>
        // Omar stays at full gain; everything else sits a little under him so he is the loudest thing in the night
        static readonly float[] CategoryGain = { 1f, 0.85f, 0.7f, 0.7f, 1f, 0.85f };

        public static AudioManager Create(Transform parent)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AudioManager");
            go.transform.SetParent(parent, false);
            return go.AddComponent<AudioManager>();
        }

        void Awake()
        {
            Instance = this;
            var lg = new GameObject("AudioListener");
            lg.transform.SetParent(transform, false);
            _listener = lg.transform;
            lg.AddComponent<AudioListener>();
            _lowpass = lg.AddComponent<AudioLowPassFilter>();
            _lowpass.cutoffFrequency = 22000f;
            _distortion = lg.AddComponent<AudioDistortionFilter>();
            _distortion.distortionLevel = 0f;
            _distortion.enabled = false;
            for (int i = 0; i < 48; i++) CreateVoice();
            _rolloff = new AnimationCurve(
                new Keyframe(0f, 1f), new Keyframe(0.06f, 0.9f), new Keyframe(0.2f, 0.55f),
                new Keyframe(0.45f, 0.22f), new Keyframe(0.75f, 0.06f), new Keyframe(1f, 0f));
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        Voice CreateVoice()
        {
            var go = new GameObject("Voice");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.dopplerLevel = 0f;
            var v = new Voice { Src = src };
            _voices.Add(v);
            _bySource[src] = v;
            return v;
        }

        // ------------------------------------------------------------------ clips

        public static AudioClip Clip(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (_clips.TryGetValue(path, out var c) && c != null) return c;
            c = Resources.Load<AudioClip>(path);
            if (c == null)
            {
                if (_missing.Add(path)) Debug.LogWarning("[Audio] missing clip " + path);
                return null;
            }
            _clips[path] = c;
            return c;
        }

        /// <summary>"Audio/Steps/wood" + count 4 -> "Audio/Steps/wood_N" with N random 1..count.</summary>
        public static string Variant(string basePath, int count) => basePath + "_" + Random.Range(1, count + 1);

        // ------------------------------------------------------------------ playback

        Voice Acquire()
        {
            for (int i = 0; i < _voices.Count; i++)
                if (!_voices[i].InUse) return _voices[i];
            if (_voices.Count < 96) return CreateVoice();
            // steal the oldest non persistent voice
            Voice oldest = null;
            for (int i = 0; i < _voices.Count; i++)
            {
                var v = _voices[i];
                if (v.Persistent) continue;
                if (oldest == null || v.StartedAt < oldest.StartedAt) oldest = v;
            }
            if (oldest != null) { oldest.Src.Stop(); Release(oldest); return oldest; }
            return CreateVoice();
        }

        void Release(Voice v)
        {
            v.InUse = false; v.Follow = null; v.Persistent = false; v.StopWhenSilent = false;
            // Replace the AudioSource so callers still holding the old one cannot stop a recycled sound
            // (their reference becomes a destroyed object == null).
            var go = v.Src != null ? v.Src.gameObject : null;
            _bySource.Remove(v.Src);
            if (v.Src != null) { v.Src.Stop(); Destroy(v.Src); }
            if (go == null) { go = new GameObject("Voice"); go.transform.SetParent(transform, false); }
            v.Src = go.AddComponent<AudioSource>();
            v.Src.playOnAwake = false;
            v.Src.dopplerLevel = 0f;
            _bySource[v.Src] = v;
        }

        Voice Setup(AudioClip clip, float volume, float pitch, AudioCategory cat, bool spatial, Vector3 pos, float minDist, float maxDist, bool loop, Transform follow)
        {
            var v = Acquire();
            v.InUse = true; v.Category = cat; v.BaseVolume = volume; v.Fade = 1f; v.FadeTarget = 1f; v.FadeSpeed = 4f;
            v.StopWhenSilent = false; v.Follow = follow; v.Persistent = loop; v.StartedAt = Time.unscaledTime;
            var s = v.Src;
            s.clip = clip; s.loop = loop; s.pitch = pitch;
            s.spatialBlend = spatial ? 1f : 0f;
            s.transform.position = follow != null ? follow.position : pos;
            if (spatial)
            {
                s.rolloffMode = AudioRolloffMode.Custom;
                s.SetCustomCurve(AudioSourceCurveType.CustomRolloff, _rolloff);
                s.minDistance = minDist;
                s.maxDistance = Mathf.Max(minDist + 0.1f, maxDist);
                s.spread = 30f;
            }
            s.volume = ComputeVolume(v);
            s.Play();
            return v;
        }

        public static AudioSource Play2D(string path, float volume = 1f, float pitch = 1f, AudioCategory cat = AudioCategory.Sfx)
        {
            var clip = Clip(path);
            if (clip == null || Instance == null) return null;
            return Instance.Setup(clip, volume, pitch, cat, false, Vector3.zero, 1, 1, false, null).Src;
        }

        public static AudioSource Play3D(string path, Vector3 position, float volume = 1f, float pitch = 1f,
            float minDistance = 1.5f, float maxDistance = 25f, AudioCategory cat = AudioCategory.Sfx, Transform follow = null)
        {
            var clip = Clip(path);
            if (clip == null || Instance == null) return null;
            return Instance.Setup(clip, volume, pitch, cat, true, position, minDistance, maxDistance, false, follow).Src;
        }

        public static AudioSource Loop2D(string path, float volume = 1f, AudioCategory cat = AudioCategory.Ambience, float fadeIn = 0.5f)
        {
            var clip = Clip(path);
            if (clip == null || Instance == null) return null;
            var v = Instance.Setup(clip, volume, 1f, cat, false, Vector3.zero, 1, 1, true, null);
            FadeIn(v, fadeIn);
            return v.Src;
        }

        public static AudioSource Loop3D(string path, Vector3 position, float volume = 1f, float maxDistance = 15f,
            AudioCategory cat = AudioCategory.Ambience, Transform follow = null, float fadeIn = 0.5f)
        {
            var clip = Clip(path);
            if (clip == null || Instance == null) return null;
            var v = Instance.Setup(clip, volume, 1f, cat, true, position, 1f, maxDistance, true, follow);
            v.Src.time = Random.Range(0f, clip.length * 0.9f);
            FadeIn(v, fadeIn);
            return v.Src;
        }

        static void FadeIn(Voice v, float time)
        {
            if (time <= 0f) return;
            v.Fade = 0f; v.FadeTarget = 1f; v.FadeSpeed = 1f / time;
        }

        /// <summary>Fade out and release a source returned by Play*/Loop*. Safe with null / foreign sources.</summary>
        public static void Stop(AudioSource src, float fade = 0.2f)
        {
            if (src == null || Instance == null) return;
            if (!Instance._bySource.TryGetValue(src, out var v) || !v.InUse) return;
            if (fade <= 0f) { v.Src.Stop(); Instance.Release(v); return; }
            v.FadeTarget = 0f; v.FadeSpeed = 1f / fade; v.StopWhenSilent = true;
        }

        public static bool IsPlaying(AudioSource src) => src != null && src.isPlaying;

        /// <summary>Change the clip-level volume of a managed source (loops driven by gameplay parameters).</summary>
        public static void SetVolume(AudioSource src, float volume)
        {
            if (src == null || Instance == null) return;
            if (Instance._bySource.TryGetValue(src, out var v)) v.BaseVolume = volume;
        }

        public static void SetPitch(AudioSource src, float pitch) { if (src != null) src.pitch = pitch; }

        // ------------------------------------------------------------------ music & ambience

        /// <summary>Menu music. Never call during gameplay.</summary>
        public static void PlayMusic(string path, float fadeIn = 2f, float volume = 0.8f)
        {
            if (Instance == null) return;
            var clip = Clip(path);
            if (clip == null) return;
            var m = Instance._music;
            if (m != null && m.InUse && m.Src.clip == clip && m.FadeTarget > 0f) return;
            if (m != null && m.InUse) Stop(m.Src, 0.5f);
            var v = Instance.Setup(clip, volume, 1f, AudioCategory.Music, false, Vector3.zero, 1, 1, true, null);
            FadeIn(v, fadeIn);
            Instance._music = v;
        }

        public static void StopMusic(float fadeOut = 2f)
        {
            if (Instance == null || Instance._music == null) return;
            Stop(Instance._music.Src, fadeOut);
            Instance._music = null;
        }

        public static bool MusicPlaying => Instance != null && Instance._music != null && Instance._music.InUse;

        /// <summary>Crossfade the 2D ambience bed (null = silence).</summary>
        public static void SetAmbience(string path, float volume = 0.6f, float fade = 2.5f)
        {
            if (Instance == null) return;
            var cur = Instance._ambUseA ? Instance._ambA : Instance._ambB;
            if (cur != null && cur.InUse && path != null && cur.Src.clip == Clip(path) && cur.FadeTarget > 0f)
            {
                cur.BaseVolume = volume;
                return;
            }
            if (cur != null && cur.InUse) Stop(cur.Src, fade);
            Instance._ambUseA = !Instance._ambUseA;
            if (string.IsNullOrEmpty(path)) { if (Instance._ambUseA) Instance._ambA = null; else Instance._ambB = null; return; }
            var src = Loop2D(path, volume, AudioCategory.Ambience, fade);
            Voice nv = null;
            if (src != null) Instance._bySource.TryGetValue(src, out nv);
            if (Instance._ambUseA) Instance._ambA = nv; else Instance._ambB = nv;
        }

        /// <summary>Temporarily lower music + ambience (stingers, screams).</summary>
        public static void Duck(float level, float seconds)
        {
            if (Instance == null) return;
            Instance._duckTarget = Mathf.Min(Instance._duckTarget, Mathf.Clamp01(level));
            Instance._duckUntil = Mathf.Max(Instance._duckUntil, Time.unscaledTime + seconds);
        }

        /// <summary>0 = clear, 1 = heavily muffled (hiding in a wardrobe, under water...).</summary>
        public static void SetMuffle(float amount) { if (Instance != null) Instance._muffleTarget = Mathf.Clamp01(amount); }

        /// <summary>0..1 listener distortion (capture, explosion deafness).</summary>
        public static void SetDistortion(float amount) { if (Instance != null) Instance._distortTarget = Mathf.Clamp01(amount); }

        /// <summary>Stop everything except music (between matches).</summary>
        public static void StopAllSfx()
        {
            if (Instance == null) return;
            foreach (var v in Instance._voices)
            {
                if (!v.InUse || v.Category == AudioCategory.Music) continue;
                v.Src.Stop();
                Instance.Release(v);
            }
            Instance._ambA = Instance._ambB = null;
            Instance._muffleTarget = 0; Instance._distortTarget = 0;
        }

        // ------------------------------------------------------------------ update

        float ComputeVolume(Voice v)
        {
            float cat;
            switch (v.Category)
            {
                case AudioCategory.Music: cat = Gameplay.Settings.MusicVolume; break;
                default: cat = Gameplay.Settings.SfxVolume; break;
            }
            float duck = (v.Category == AudioCategory.Music || v.Category == AudioCategory.Ambience) ? _duck : 1f;
            return Mathf.Clamp01(v.BaseVolume * CategoryGain[(int)v.Category] * cat * duck * v.Fade);
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            AudioListener.volume = Gameplay.Settings.MasterVolume;

            var rig = Rendering.PsxCameraRig.Instance;
            if (rig != null)
            {
                _listener.position = rig.transform.position;
                _listener.rotation = rig.transform.rotation;
            }

            if (Time.unscaledTime > _duckUntil) _duckTarget = 1f;
            _duck = Mathf.MoveTowards(_duck, _duckTarget, dt * (_duckTarget < _duck ? 6f : 0.8f));

            _muffle = Mathf.MoveTowards(_muffle, _muffleTarget, dt * 3f);
            _lowpass.cutoffFrequency = Mathf.Lerp(22000f, 750f, Mathf.Pow(_muffle, 0.6f));
            _distort = Mathf.MoveTowards(_distort, _distortTarget, dt * 2f);
            _distortion.enabled = _distort > 0.01f;
            _distortion.distortionLevel = _distort * 0.85f;

            for (int i = 0; i < _voices.Count; i++)
            {
                var v = _voices[i];
                if (!v.InUse) continue;
                if (v.Src == null) { v.InUse = false; continue; }
                if (v.Fade != v.FadeTarget) v.Fade = Mathf.MoveTowards(v.Fade, v.FadeTarget, dt * v.FadeSpeed);
                if (v.StopWhenSilent && v.Fade <= 0.001f) { v.Src.Stop(); Release(v); continue; }
                if (v.Follow != null) v.Src.transform.position = v.Follow.position;
                v.Src.volume = ComputeVolume(v);
                if (!v.Persistent && !v.Src.isPlaying && Time.unscaledTime - v.StartedAt > 0.1f) Release(v);
            }
        }
    }
}
