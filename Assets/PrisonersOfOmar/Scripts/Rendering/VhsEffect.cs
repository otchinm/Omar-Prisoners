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

        /// <summary>Scanline strength multiplier (0 = none). Scanlines follow the low-res lines and fade out
        /// automatically when the window is too small to show them without moire.</summary>
        public static float Scanlines = 1f;

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
            _roll = 0f;
        }

        // ------------------------------------------------------------------ grading per mode

        struct Grade
        {
            public float Tape, Tracking, Noise, Contrast, Saturation, Crush, ShadowAmount, Lift, Vignette;
            public float ChromaShift, ChromaBlur, Ghost, HeadSwitch, Barrel;
            public Color Tint, Shadow; // Shadow: luma-normalized tint color of the shadows / lifted blacks

            public static Grade Lerp(Grade a, Grade b, float t)
            {
                return new Grade
                {
                    Tape = Mathf.Lerp(a.Tape, b.Tape, t),
                    Tracking = Mathf.Lerp(a.Tracking, b.Tracking, t),
                    Noise = Mathf.Lerp(a.Noise, b.Noise, t),
                    Contrast = Mathf.Lerp(a.Contrast, b.Contrast, t),
                    Saturation = Mathf.Lerp(a.Saturation, b.Saturation, t),
                    Crush = Mathf.Lerp(a.Crush, b.Crush, t),
                    ShadowAmount = Mathf.Lerp(a.ShadowAmount, b.ShadowAmount, t),
                    Lift = Mathf.Lerp(a.Lift, b.Lift, t),
                    Vignette = Mathf.Lerp(a.Vignette, b.Vignette, t),
                    ChromaShift = Mathf.Lerp(a.ChromaShift, b.ChromaShift, t),
                    ChromaBlur = Mathf.Lerp(a.ChromaBlur, b.ChromaBlur, t),
                    Ghost = Mathf.Lerp(a.Ghost, b.Ghost, t),
                    HeadSwitch = Mathf.Lerp(a.HeadSwitch, b.HeadSwitch, t),
                    Barrel = Mathf.Lerp(a.Barrel, b.Barrel, t),
                    Tint = Color.Lerp(a.Tint, b.Tint, t),
                    Shadow = Color.Lerp(a.Shadow, b.Shadow, t),
                };
            }
        }

        static readonly Color Teal = new Color(0.78f, 1.08f, 1.05f);

        static Grade GradeFor(VhsMode mode)
        {
            switch (mode)
            {
                case VhsMode.Menu:
                    return new Grade
                    {
                        Tape = 1.35f, Tracking = 1f, Noise = 1.3f, Contrast = 1.12f, Saturation = 0.85f, Crush = 0.03f,
                        ShadowAmount = 0.35f, Lift = 0.025f, Vignette = 0.55f,
                        ChromaShift = 1.8f, ChromaBlur = 1.8f, Ghost = 0.14f, HeadSwitch = 1f, Barrel = 0.1f,
                        Tint = new Color(1f, 0.97f, 0.95f), Shadow = Teal,
                    };
                case VhsMode.Omar:
                    return new Grade
                    {
                        Tape = 1.1f, Tracking = 0.5f, Noise = 1.15f, Contrast = 1.35f, Saturation = 0.9f, Crush = 0.06f,
                        ShadowAmount = 0.3f, Lift = 0.012f, Vignette = 0.65f,
                        ChromaShift = 1.6f, ChromaBlur = 1.6f, Ghost = 0.12f, HeadSwitch = 1f, Barrel = 0.08f,
                        Tint = new Color(1.15f, 0.86f, 0.8f), Shadow = new Color(1.6f, 0.8f, 0.75f),
                    };
                case VhsMode.Spectator:
                    return new Grade
                    {
                        Tape = 0.85f, Tracking = 0.5f, Noise = 0.9f, Contrast = 0.8f, Saturation = 0.38f, Crush = 0f,
                        ShadowAmount = 0.3f, Lift = 0.06f, Vignette = 0.4f,
                        ChromaShift = 1.2f, ChromaBlur = 1.4f, Ghost = 0.1f, HeadSwitch = 0.8f, Barrel = 0.08f,
                        Tint = new Color(0.97f, 1f, 1.03f), Shadow = new Color(0.85f, 1.03f, 1.12f),
                    };
                default: // Prisoner
                    return new Grade
                    {
                        Tape = 1f, Tracking = 0.35f, Noise = 1f, Contrast = 1.08f, Saturation = 0.72f, Crush = 0.035f,
                        ShadowAmount = 0.5f, Lift = 0.022f, Vignette = 0.5f,
                        ChromaShift = 1.3f, ChromaBlur = 1.5f, Ghost = 0.1f, HeadSwitch = 0.85f, Barrel = 0.08f,
                        Tint = new Color(0.95f, 1f, 0.97f), Shadow = Teal,
                    };
            }
        }

        static Grade _grade;
        static bool _gradeInit;

        /// <summary>CRT barrel amount currently used by the VHS pass (PsxCameraRig.ScreenToLowRes compensates for it).</summary>
        internal static float CurrentBarrel => _gradeInit ? _grade.Barrel : GradeFor(Mode).Barrel;
        static float _roll;
        static int _lastApplyFrame = -1;

        /// <summary>Uploads every VHS parameter to <paramref name="m"/> (called by the camera rig's presenter each frame).</summary>
        internal static void Apply(Material m, RenderTexture lowRes)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            bool newFrame = _lastApplyFrame != Time.frameCount;
            _lastApplyFrame = Time.frameCount;

            // smooth mode transitions
            Grade target = GradeFor(Mode);
            if (!_gradeInit) { _grade = target; _gradeInit = true; }
            else if (newFrame) _grade = Grade.Lerp(_grade, target, 1f - Mathf.Exp(-dt * 6f));
            var g = _grade;

            // vertical hold: keeps rolling until the frame locks again (offset wraps back to 0)
            if (newFrame && (Rolling || _roll > 0f))
            {
                _roll += dt * 1.8f;
                if (_roll >= 1f) _roll = Rolling ? _roll - 1f : 0f;
            }

            float u = Mathf.Clamp(UserIntensity, 0f, 1.5f);
            float interference = Mathf.Clamp01(Interference);
            float damage = Mathf.Clamp01(Damage);
            float glitch = Mathf.Clamp01(CurrentGlitch) * Mathf.Max(u, 0.35f);

            int lowW = lowRes != null ? lowRes.width : 426;
            int lowH = lowRes != null ? lowRes.height : 240;
            float sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
            float scan = Mathf.Clamp01((sh / lowH - 1.4f) / 1.6f) * 0.85f * Mathf.Max(0f, Scanlines);

            double ut = Time.unscaledTimeAsDouble;
            float t = (float)(ut % 1000.0);
            float field = (float)(System.Math.Floor(ut * 30.0) % 1024.0);

            // heartbeat for the damage pulse (double thump, faster when hurt badly)
            float hb = (float)((ut * (1.1 + damage * 0.9)) % 1.0);
            float pulse = Mathf.Exp(-hb * 9f) + (hb > 0.2f ? 0.65f * Mathf.Exp(-(hb - 0.2f) * 9f) : 0f);

            float sat = g.Saturation * (1f - Mathf.Clamp01(Desaturate)) * (1f - interference * 0.85f);

            m.SetVector(PsxShaderIds.VhsScreen, new Vector4(sw, sh, sw / sh, scan));
            m.SetVector(PsxShaderIds.VhsTime, new Vector4(t, field, _roll, Time.frameCount & 1));
            m.SetVector(PsxShaderIds.VhsTape, new Vector4(0.55f * g.Tape * u, 0.5f * g.Tape * u, g.Tracking * u, g.Noise * u));
            m.SetVector(PsxShaderIds.VhsTape2, new Vector4(g.ChromaShift * (0.4f + 0.6f * u), g.ChromaBlur, g.Ghost * Mathf.Min(u, 1f), g.HeadSwitch * u));
            m.SetVector(PsxShaderIds.VhsGrade, new Vector4(g.Contrast, sat, g.Crush, 1f));
            m.SetVector(PsxShaderIds.VhsTint, new Vector4(g.Tint.r, g.Tint.g, g.Tint.b, g.ShadowAmount));
            m.SetVector(PsxShaderIds.VhsShadow, new Vector4(g.Shadow.r, g.Shadow.g, g.Shadow.b, g.Lift));
            m.SetVector(PsxShaderIds.VhsFx, new Vector4(interference, damage, Mathf.Clamp01(StaticOverride), Mathf.Clamp01(Blackout)));
            m.SetVector(PsxShaderIds.VhsFx2, new Vector4(Mathf.Clamp01(Hiding), glitch, Mathf.Clamp01(pulse), 0f));
            m.SetVector(PsxShaderIds.VhsLens, new Vector4(g.Barrel, g.Vignette, 0.06f, 0.025f));
        }
    }
}
