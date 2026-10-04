using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>
    /// A lighter / candle flame like the first-person reference: one tall tongue with a white-yellow core that trails
    /// behind every movement (turning the head swings it), sways lazily and flickers in height.
    /// Built by <see cref="PsxFx.CreateFlame"/>.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class PsxFlame : MonoBehaviour
    {
        internal PsxSprite Tongue, Glow;
        internal PsxBillboard Bill;
        internal float Width = 0.016f, Height = 0.05f;

        Vector3 _lastPos;
        bool _hasLast;
        float _lean, _leanVel, _stretch = 1f, _stretchVel;
        float _seed;

        void OnEnable()
        {
            _seed = Random.value * 50f;
            _hasLast = false;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || Tongue == null) return;
            float s = transform.lossyScale.y;
            Vector3 p = transform.position;
            Vector3 v = _hasLast ? (p - _lastPos) / dt : Vector3.zero;
            _lastPos = p; _hasLast = true;
            if (v.sqrMagnitude > 400f) v = Vector3.zero;

            var cam = PsxRenderDriver.WorldCamera();
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            float lateral = Vector3.Dot(v, right) / Mathf.Max(s, 0.2f);
            float rise = v.y / Mathf.Max(s, 0.2f);

            // trails behind the motion + a slow lazy sway
            float t = Time.time;
            float sway = (Mathf.PerlinNoise(_seed, t * 1.7f) - 0.5f) * 8f + (Mathf.PerlinNoise(_seed + 9f, t * 7f) - 0.5f) * 2.5f;
            float targetLean = Mathf.Clamp(lateral * 14f, -38f, 38f) + sway;
            _lean = Mathf.SmoothDamp(_lean, targetLean, ref _leanVel, 0.07f, 900f, dt);
            float targetStretch = Mathf.Clamp(1f - rise * 0.25f, 0.75f, 1.25f) * (0.93f + 0.14f * Mathf.PerlinNoise(_seed + 3f, t * 9f));
            _stretch = Mathf.SmoothDamp(_stretch, targetStretch, ref _stretchVel, 0.05f, 50f, dt);

            if (Bill != null) Bill.Roll = _lean;
            Tongue.StartSize = Tongue.EndSize = new Vector2(Width * (1.05f - 0.1f * (_stretch - 1f)), Height * _stretch);
        }
    }
}
