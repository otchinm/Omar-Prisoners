using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Map
{
    /// <summary>
    /// Swaps a renderer between "lit" (emissive) and "dark" materials following a probe <see cref="PsxLight"/>'s On flag,
    /// so bulbs, lamp lenses and red windows go dark together with the lights during a power outage
    /// (gameplay only switches the PsxLights in MapData.PowerLights / RadioRoomLights).
    /// </summary>
    public sealed class PoweredGlow : MonoBehaviour
    {
        public PsxLight Probe;
        public Renderer Target;
        public Material[] OnMaterials = new Material[0];
        public Material[] OffMaterials = new Material[0];
        bool _shown = true;
        bool _initialized;

        public void Setup(PsxLight probe, Renderer target, Material[] on, Material[] off)
        {
            Probe = probe; Target = target; OnMaterials = on; OffMaterials = off;
            _initialized = false;
            Apply();
        }

        void LateUpdate() => Apply();

        void Apply()
        {
            if (Target == null) return;
            bool on = Probe == null || (Probe.On && Probe.isActiveAndEnabled);
            if (_initialized && on == _shown) return;
            _initialized = true;
            _shown = on;
            var mats = on ? OnMaterials : OffMaterials;
            if (mats != null && mats.Length > 0) Target.sharedMaterials = mats;
        }
    }

    /// <summary>CRT screen cycling static frames (tv_static_0..3). Goes black when its probe light is off (power outage).</summary>
    public sealed class TvScreen : MonoBehaviour
    {
        public PsxLight PowerProbe;
        public Renderer Screen;
        public float FrameTime = 0.07f;
        Material _mat, _off;
        Texture2D[] _frames;
        float _t;
        int _frame;
        bool _wasOn = true;

        void Start()
        {
            if (Screen == null) Screen = GetComponent<Renderer>();
            _frames = new Texture2D[4];
            for (int i = 0; i < 4; i++) _frames[i] = PsxMaterials.Texture(Tex.Props + "tv_static_" + i);
            _mat = PsxMaterials.Create(Tex.TvStatic0, PsxSurface.Unlit);
            _off = PsxMaterials.GetColor(new Color(0.02f, 0.025f, 0.03f), PsxSurface.Lit);
            _frame = Mathf.Abs(GetInstanceID()) % 4;
            if (Screen != null) Screen.sharedMaterial = _mat;
        }

        void Update()
        {
            if (Screen == null || _mat == null) return;
            bool on = PowerProbe == null || PowerProbe.On;
            if (on != _wasOn)
            {
                Screen.sharedMaterial = on ? _mat : _off;
                _wasOn = on;
            }
            if (!on) return;
            _t += Time.deltaTime;
            if (_t < FrameTime) return;
            _t = 0f;
            _frame = (_frame + 1 + (Time.frameCount % 3 == 0 ? 1 : 0)) % _frames.Length;
            _mat.mainTexture = _frames[_frame];
        }
    }

    /// <summary>Slowly turning windmill rotor (rotates around its local Z axis), with gusts.</summary>
    public sealed class WindmillRotor : MonoBehaviour
    {
        public float Speed = 28f;
        public float Phase;

        void Update()
        {
            float gust = 0.6f + 0.4f * Mathf.Sin(Time.time * 0.21f + Phase) * Mathf.Sin(Time.time * 0.057f + Phase * 2f);
            transform.Rotate(0f, 0f, Speed * gust * Time.deltaTime, Space.Self);
        }
    }
}
