using System.Collections.Generic;
using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>
    /// Minimal MonoBehaviour-driven sprite / particle: flipbook frames, color + size over life, simple ballistic motion.
    /// Visuals go through a MaterialPropertyBlock (_MainTex, _Color) so materials stay shared.
    /// One-shot sprites come from <see cref="PsxSpritePool"/>; persistent ones (Lifetime &lt; 0) are plain children.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class PsxSprite : MonoBehaviour
    {
        // ---- appearance
        public Texture2D[] Frames;
        public float Fps = 12f;
        /// <summary>false: play the frames once over the lifetime.</summary>
        public bool LoopFrames = true;
        public Color StartColor = Color.white;
        public Color EndColor = Color.white;
        /// <summary>Fraction of the life after which the color starts going from StartColor to EndColor.</summary>
        public float FadeStart;
        public Vector2 StartSize = Vector2.one;
        public Vector2 EndSize = Vector2.one;
        /// <summary>0..1 random brightness flicker.</summary>
        public float Flicker;
        /// <summary>0..1 random size wobble.</summary>
        public float SizeJitter;

        // ---- motion (world space)
        public Vector3 Velocity;
        /// <summary>Downwards acceleration (m/s^2). Negative values make the sprite rise (embers).</summary>
        public float Gravity;
        /// <summary>Linear damping (1/s).</summary>
        public float Drag;
        /// <summary>The sprite stops falling at this world height (ground found by a raycast at spawn).</summary>
        public float FloorY = float.NegativeInfinity;

        // ---- life
        /// <summary>Seconds; &lt; 0 = persistent.</summary>
        public float Lifetime = -1f;

        internal bool Pooled;
        internal MeshFilter Filter;
        internal MeshRenderer Renderer;

        MaterialPropertyBlock _mpb;
        float _age, _frameOffset, _seed;

        public float Age => _age;

        void Awake() { EnsureInit(); }

        internal void EnsureInit()
        {
            if (_mpb != null) return;
            _mpb = new MaterialPropertyBlock();
            if (Filter == null) Filter = GetComponent<MeshFilter>();
            if (Renderer == null) Renderer = GetComponent<MeshRenderer>();
            _seed = (GetInstanceID() & 0xFFFF) * 0.0137f;
        }

        /// <summary>Resets every setting to its default (pooled sprites are reused).</summary>
        internal void ResetSettings()
        {
            Frames = null; Fps = 12f; LoopFrames = true;
            StartColor = EndColor = Color.white; FadeStart = 0f;
            StartSize = EndSize = Vector2.one; Flicker = 0f; SizeJitter = 0f;
            Velocity = Vector3.zero; Gravity = 0f; Drag = 0f; FloorY = float.NegativeInfinity;
            Lifetime = -1f;
            _age = 0f;
            // a reused sprite must not keep the flipbook frame of its previous life over its new material
            if (_mpb != null) { _mpb.Clear(); if (Renderer != null) Renderer.SetPropertyBlock(_mpb); }
        }

        /// <summary>Call after configuring: restarts the life and applies the first frame immediately.</summary>
        public void Play()
        {
            EnsureInit();
            _age = 0f;
            _frameOffset = (Frames != null && Frames.Length > 1 && LoopFrames) ? (_seed * 7.31f) % 1f * Frames.Length / Mathf.Max(Fps, 0.01f) : 0f;
            Apply();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (Lifetime > 0f && _age >= Lifetime) { Finish(); return; }

            if (Gravity != 0f || Velocity.x != 0f || Velocity.y != 0f || Velocity.z != 0f)
            {
                Velocity.y -= Gravity * dt;
                if (Drag > 0f) Velocity *= 1f / (1f + Drag * dt);
                Vector3 p = transform.position + Velocity * dt;
                if (p.y < FloorY)
                {
                    p.y = FloorY;
                    Velocity = Vector3.zero;
                    Gravity = 0f;
                }
                transform.position = p;
            }
            Apply();
        }

        void Apply()
        {
            float life = Lifetime > 0f ? Mathf.Clamp01(_age / Lifetime) : 0f;
            Vector2 s = Vector2.Lerp(StartSize, EndSize, life);
            if (SizeJitter > 0f) s *= 1f + SizeJitter * (Mathf.PerlinNoise(_seed, _age * 9f) - 0.5f);
            transform.localScale = new Vector3(s.x, s.y, 1f);

            float cl = FadeStart > 0f ? Mathf.Clamp01((life - FadeStart) / Mathf.Max(1f - FadeStart, 0.001f)) : life;
            Color c = Color.Lerp(StartColor, EndColor, cl);
            if (Flicker > 0f)
            {
                float f = 1f - Flicker * Mathf.PerlinNoise(_seed + 3.7f, _age * 13f);
                c.r *= f; c.g *= f; c.b *= f;
            }
            _mpb.SetColor(PsxShaderIds.Color, c);

            var frames = Frames;
            if (frames != null && frames.Length > 0)
            {
                int idx;
                if (!LoopFrames && Lifetime > 0f) idx = Mathf.Min(frames.Length - 1, (int)(life * frames.Length));
                else idx = (int)((_age + _frameOffset) * Fps) % frames.Length;
                if (idx < 0) idx = 0;
                var tex = frames[idx];
                if (tex != null) _mpb.SetTexture(PsxShaderIds.MainTex, tex);
            }
            if (Renderer != null) Renderer.SetPropertyBlock(_mpb);
        }

        void Finish()
        {
            if (Pooled) PsxSpritePool.Release(this);
            else Destroy(gameObject);
        }
    }

    /// <summary>Pool of one-shot sprites (blood spray, sparks, smoke puffs...). Lives under a hidden DontDestroyOnLoad root.</summary>
    public static class PsxSpritePool
    {
        /// <summary>Maximum simultaneously active pooled sprites; further spawns are skipped.</summary>
        public const int MaxActive = 500;

        static readonly Stack<PsxSprite> _free = new Stack<PsxSprite>(64);
        static Transform _root;
        static int _active;

        internal static Transform Root
        {
            get
            {
                if (_root != null) return _root;
                var go = new GameObject("PsxFxPool");
                go.hideFlags = HideFlags.HideInHierarchy;
                if (Application.isPlaying) Object.DontDestroyOnLoad(go);
                _root = go.transform;
                _free.Clear();
                _active = 0;
                return _root;
            }
        }

        public static int ActiveCount => _active;

        /// <summary>Gets a reset sprite (inactive settings, zero scale until Play). Null when the pool is exhausted.</summary>
        public static PsxSprite Spawn(Vector3 position, Material material, Mesh mesh, int layer, bool billboard, bool cylindrical)
        {
            var root = Root;
            if (_active >= MaxActive) return null;
            PsxSprite s = null;
            while (s == null && _free.Count > 0) s = _free.Pop();
            if (s == null)
            {
                var go = new GameObject("FxSprite");
                go.SetActive(false);
                go.transform.SetParent(root, false);
                PsxFxAssets.AddRenderer(go, mesh, material);
                go.AddComponent<PsxBillboard>();
                s = go.AddComponent<PsxSprite>();
                s.Filter = go.GetComponent<MeshFilter>();
                s.Renderer = go.GetComponent<MeshRenderer>();
            }
            var g = s.gameObject;
            g.layer = layer;
            var t = s.transform;
            t.SetParent(root, false);
            t.position = position;
            t.rotation = Quaternion.identity;
            t.localScale = Vector3.zero;
            s.Filter.sharedMesh = mesh;
            s.Renderer.sharedMaterial = material;
            var bb = g.GetComponent<PsxBillboard>();
            bb.enabled = billboard;
            bb.Cylindrical = cylindrical;
            bb.Roll = 0f;
            s.EnsureInit();
            s.ResetSettings();
            s.Pooled = true;
            g.SetActive(true);
            _active++;
            return s;
        }

        internal static void Release(PsxSprite s)
        {
            if (s == null) return;
            s.gameObject.SetActive(false);
            _free.Push(s);
            _active = Mathf.Max(0, _active - 1);
        }
    }
}
