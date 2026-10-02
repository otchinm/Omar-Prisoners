using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    public enum VhsMode
    {
        Menu = 0,      // strong tape look, slow tracking drift
        Prisoner,      // default in-game look
        Omar,          // Omar's view: harsher, reddish, more contrast
        Spectator,     // washed out, timestamp "REC"
    }

    /// <summary>
    /// Parameters of the VHS / analog post-process. Gameplay writes these every frame (or triggers bursts);
    /// the camera rig reads them when rendering. All values are 0..1 unless noted.
    /// </summary>
    public static class VhsEffect
    {
        public static VhsMode Mode = VhsMode.Menu;

        /// <summary>User setting multiplier for every distortion (accessibility). 0..1.5</summary>
        public static float UserIntensity = 1f;

        /// <summary>Omar proximity: static snow, rolling bars, horizontal tearing, color loss.</summary>
        public static float Interference;

        /// <summary>Injury / pain: red pulse, blur, darkening at the edges.</summary>
        public static float Damage;

        /// <summary>1 = the whole picture is replaced by static snow (capture, tape cuts).</summary>
        public static float StaticOverride;

        /// <summary>1 = black screen (fades, loading).</summary>
        public static float Blackout;

        /// <summary>Extra desaturation (spectating, dying).</summary>
        public static float Desaturate;

        /// <summary>Muffled / hiding vignette (inside a wardrobe).</summary>
        public static float Hiding;

        static float _glitchUntil, _glitchStrength, _rollUntil;

        /// <summary>Short tracking glitch burst (jumpscare, item pickup, anomaly).</summary>
        public static void TriggerGlitch(float strength, float duration)
        {
            _glitchStrength = Mathf.Max(_glitchStrength * (Time.unscaledTime < _glitchUntil ? 1f : 0f), strength);
            _glitchUntil = Mathf.Max(_glitchUntil, Time.unscaledTime + duration);
        }

        /// <summary>Vertical hold loss: the image rolls for a moment.</summary>
        public static void TriggerRoll(float duration)
        {
            _rollUntil = Mathf.Max(_rollUntil, Time.unscaledTime + duration);
        }

        /// <summary>Current burst glitch strength (0 when none active).</summary>
        public static float CurrentGlitch => Time.unscaledTime < _glitchUntil ? _glitchStrength : 0f;
        public static bool Rolling => Time.unscaledTime < _rollUntil;

        /// <summary>Reset transient gameplay values (between matches).</summary>
        public static void ResetTransient()
        {
            Interference = 0; Damage = 0; StaticOverride = 0; Blackout = 0; Desaturate = 0; Hiding = 0;
            _glitchUntil = 0; _rollUntil = 0; _glitchStrength = 0;
        }
    }
}
