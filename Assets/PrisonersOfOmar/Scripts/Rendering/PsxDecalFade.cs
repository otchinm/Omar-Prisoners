using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>Pooled ground decal (blood drops): holds for Hold seconds, fades out over Fade seconds, then deactivates.</summary>
    [AddComponentMenu("")]
    public sealed class PsxDecalFade : MonoBehaviour
    {
        public float Hold = 60f;
        public float Fade = 6f;
        public Color Color = Color.white;

        MeshRenderer _renderer;
        MaterialPropertyBlock _mpb;
        float _age;

        internal void Restart(Color color, float hold, float fade)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            Color = color; Hold = hold; Fade = Mathf.Max(0.1f, fade);
            _age = 0f;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            SetAlpha(1f);
        }

        void SetAlpha(float k)
        {
            if (_renderer == null) return;
            Color c = Color;
            c.a *= k;
            _mpb.SetColor(PsxShaderIds.Color, c);
            _renderer.SetPropertyBlock(_mpb);
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age < Hold) return;
            float k = 1f - (_age - Hold) / Fade;
            if (k <= 0f) { gameObject.SetActive(false); return; }
            SetAlpha(k);
        }
    }
}
