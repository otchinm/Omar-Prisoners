using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>
    /// A lighter / candle flame like the Nun Massacre first-person lighter: one upright pixel tongue that steps through a
    /// few hand-drawn states (Textures/FX/flame_N) at a moderate, slightly irregular pace. It never leans with the
    /// player's turning or walking: the animation is the frames alone, chunky and readable rather than realistic.
    /// Built by <see cref="PsxFx.CreateFlame"/>.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class PsxFlame : MonoBehaviour
    {
        internal PsxSprite Tongue;
        internal PsxBillboard Bill;
        internal float Width = 0.029f, Height = 0.058f;
        /// <summary>Average frame changes per second (the tempo of the flipbook).</summary>
        internal float Rate = 8f;

        float _next;
        int _frame;

        void OnEnable()
        {
            _frame = Random.Range(0, 6);
            _next = 0f;
        }

        void LateUpdate()
        {
            if (Tongue == null) return;
            var frames = Tongue.Frames;
            int n = frames != null ? frames.Length : 0;
            float t = Time.time;
            if (n > 1 && t >= _next)
            {
                // a different state every step, never the same one twice in a row; the hold time wanders a little
                int step = Random.Range(1, n);
                _frame = (_frame + step) % n;
                _next = t + Random.Range(0.75f, 1.3f) / Mathf.Max(Rate, 1f);
            }
            Tongue.Frame = n > 0 ? _frame % n : -1;
            if (Bill != null) Bill.Roll = 0f;
            Tongue.StartSize = Tongue.EndSize = new Vector2(Width, Height);
        }
    }
}
