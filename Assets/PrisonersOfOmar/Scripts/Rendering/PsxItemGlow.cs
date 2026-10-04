using UnityEngine;

namespace PrisonersOfOmar.Rendering
{
    /// <summary>
    /// Puppet Combo style pickup highlight: a pulsing, pixelated (ordered-dither) halo drawn AROUND an item, just
    /// behind it as seen from the camera, plus an occasional pixel sparkle. The item itself is never tinted or
    /// outlined. Visible up to <see cref="MaxDistance"/>; stronger while <see cref="Highlighted"/>.
    /// </summary>
    [AddComponentMenu("")]
    public sealed class PsxItemGlow : MonoBehaviour
    {
        /// <summary>Global multiplier (difficulty / settings). 0 disables every glow.</summary>
        public static float GlobalStrength = 1f;
        public const float MaxDistance = 7f;

        /// <summary>Approximate radius of the item in meters (glow size).</summary>
        public float Radius = 0.25f;
        /// <summary>The player is aiming at this item: stronger glow.</summary>
        public bool Highlighted;
        /// <summary>No halo at all (the item lies in a shut drawer).</summary>
        public bool Suppressed;

        Transform _halo, _spark;
        MeshRenderer _haloR, _sparkR;
        MaterialPropertyBlock _mpb;
        Vector3 _centerLocal;
        float _seed, _hl, _sparkAge = -1f, _nextSpark;
        Vector2 _sparkOffset;

        public static PsxItemGlow Attach(GameObject item, float radius = 0.25f)
        {
            if (item == null) return null;
            var g = item.GetComponent<PsxItemGlow>();
            if (g == null) g = item.AddComponent<PsxItemGlow>();
            g.Radius = radius;
            g.Build();
            return g;
        }

        void Build()
        {
            if (_halo != null) return;
            _seed = Random.value * 100f;
            _mpb = new MaterialPropertyBlock();
            // centre / size of the visible model
            bool any = false;
            Bounds b = new Bounds(transform.position, Vector3.one * 0.1f);
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            _centerLocal = transform.InverseTransformPoint(b.center);
            Radius = Mathf.Clamp(Mathf.Max(Radius * 0.5f, b.extents.magnitude), 0.06f, 0.6f);

            int layer = gameObject.layer;
            _halo = MakeQuad("ItemGlow", "item_glow", layer, out _haloR);
            _spark = MakeQuad("ItemSparkle", "item_sparkle", layer, out _sparkR);
            _sparkR.enabled = false;
            _nextSpark = Time.time + Random.Range(0.5f, 3f);
        }

        Transform MakeQuad(string name, string tex, int layer, out MeshRenderer r)
        {
            var go = new GameObject(name);
            go.layer = layer;
            go.transform.SetParent(transform, false);
            PsxFxAssets.AddRenderer(go, PsxFxAssets.QuadCenter, PsxFxAssets.Material(tex, PsxSurface.Additive));
            go.AddComponent<PsxBillboard>();
            r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        void OnDisable()
        {
            if (_haloR != null) _haloR.enabled = false;
            if (_sparkR != null) _sparkR.enabled = false;
        }

        void LateUpdate()
        {
            if (_halo == null) return;
            var cam = PsxRenderDriver.WorldCamera();
            float dt = Time.deltaTime;
            _hl = Mathf.MoveTowards(_hl, Highlighted ? 1f : 0f, dt * 5f);
            if (cam == null || GlobalStrength <= 0f || Suppressed) { _haloR.enabled = false; _sparkR.enabled = false; return; }
            Vector3 c = transform.TransformPoint(_centerLocal);
            Vector3 camPos = cam.transform.position;
            float d = Vector3.Distance(camPos, c);
            float fade = Mathf.Clamp01((MaxDistance - d) / 2f) * Mathf.Clamp01(GlobalStrength);
            if (fade <= 0.001f) { _haloR.enabled = false; _sparkR.enabled = false; return; }

            // just behind the item, so the item covers the middle of the halo and the glow rims it
            Vector3 toCam = (camPos - c) / Mathf.Max(d, 0.001f);
            _halo.position = c - toCam * Radius * 0.7f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.4f + _seed);
            float size = Mathf.Max(Radius * (2.4f + 0.35f * pulse + 0.7f * _hl), d * 0.05f);
            _halo.localScale = Vector3.one * size / Mathf.Max(transform.lossyScale.x, 0.001f);
            float a = fade * (0.16f + 0.1f * pulse + 0.42f * _hl);
            _haloR.enabled = true;
            _mpb.SetColor(PsxShaderIds.Color, new Color(1f, 0.94f, 0.8f, 1f) * a);
            _haloR.SetPropertyBlock(_mpb);

            // a pixel sparkle now and then (more often while aimed at)
            float now = Time.time;
            if (_sparkAge < 0f && now >= _nextSpark)
            {
                _sparkAge = 0f;
                _sparkOffset = Random.insideUnitCircle * 0.8f;
                _nextSpark = now + (Highlighted ? Random.Range(0.4f, 1.2f) : Random.Range(1.6f, 4.5f));
            }
            if (_sparkAge >= 0f)
            {
                _sparkAge += dt;
                float life = 0.32f;
                float k = Mathf.Clamp01(_sparkAge / life);
                float s = Mathf.Sin(k * Mathf.PI);
                var t = cam.transform;
                _spark.position = c + (t.right * _sparkOffset.x + t.up * _sparkOffset.y) * Radius + toCam * Radius * 0.3f;
                _spark.localScale = Vector3.one * Mathf.Max(Radius * 0.55f, d * 0.025f) * s / Mathf.Max(transform.lossyScale.x, 0.001f);
                _sparkR.enabled = true;
                _mpb.SetColor(PsxShaderIds.Color, new Color(1f, 0.97f, 0.85f, 1f) * fade * (0.7f + 0.3f * _hl));
                _sparkR.SetPropertyBlock(_mpb);
                if (k >= 1f) { _sparkAge = -1f; _sparkR.enabled = false; }
            }
        }
    }
}
