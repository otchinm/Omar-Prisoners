using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>
    /// A lighter / candle flame like the first-person reference: two tall tongues with a white-yellow core that trail
    /// behind every movement (turning the head swings them), flicker in height and now and then split into a "V".
    /// Built by <see cref="PsxFx.CreateFlame"/>.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class PsxFlame : MonoBehaviour
    {
        internal PsxSprite TongueA, TongueB, Glow;
        internal PsxBillboard BillA, BillB;
        internal float Width = 0.016f, Height = 0.05f;

        Vector3 _lastPos;
        bool _hasLast;
        float _lean, _leanVel, _stretch = 1f, _stretchVel;
        float _split, _splitUntil, _nextSplit;
        float _seed;

        void OnEnable()
        {
            _seed = Random.value * 50f;
            _hasLast = false;
            _nextSplit = Time.time + Random.Range(0.4f, 1.6f);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || TongueA == null) return;
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
            float sway = (Mathf.PerlinNoise(_seed, t * 1.7f) - 0.5f) * 10f + (Mathf.PerlinNoise(_seed + 9f, t * 7f) - 0.5f) * 3f;
            float targetLean = Mathf.Clamp(lateral * 16f, -42f, 42f) + sway;
            _lean = Mathf.SmoothDamp(_lean, targetLean, ref _leanVel, 0.07f, 900f, dt);
            float targetStretch = Mathf.Clamp(1f - rise * 0.25f, 0.75f, 1.25f) * (0.92f + 0.16f * Mathf.PerlinNoise(_seed + 3f, t * 9f));
            _stretch = Mathf.SmoothDamp(_stretch, targetStretch, ref _stretchVel, 0.05f, 50f, dt);

            // now and then the flame tears into two tongues for a moment
            if (t >= _nextSplit)
            {
                _splitUntil = t + Random.Range(0.12f, 0.45f);
                _nextSplit = t + Random.Range(0.6f, 2.8f);
            }
            float splitTarget = t < _splitUntil ? 1f : Mathf.Clamp01(Mathf.Abs(lateral) * 0.6f) * 0.5f;
            _split = Mathf.MoveTowards(_split, splitTarget, dt * 7f);

            float h = Height * _stretch;
            float off = Width * 0.32f * _split;
            TongueA.transform.position = p - right * off * s;
            TongueB.transform.position = p + right * off * s;
            if (BillA != null) BillA.Roll = _lean + 9f * _split;
            if (BillB != null) BillB.Roll = _lean - 9f * _split;
            TongueA.StartSize = TongueA.EndSize = new Vector2(Width * (1f - 0.2f * _split), h);
            TongueB.StartSize = TongueB.EndSize = new Vector2(Width * (0.85f - 0.15f * _split), h * (0.9f - 0.08f * _split));
        }
    }
}
