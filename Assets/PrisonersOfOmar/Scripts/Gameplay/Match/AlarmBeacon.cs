using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>
    /// (iteration 2) A tripwire that set off the siren lights its spot with a pulsing red light for a while, so everybody
    /// can see where the alarm went off. Client side only (every peer spawns it from the trap event).
    /// </summary>
    public sealed class AlarmBeacon : MonoBehaviour
    {
        PsxLight _light;
        float _age, _life;

        public static AlarmBeacon Spawn(Transform parent, Vector3 position, float seconds)
        {
            var go = new GameObject("AlarmBeacon");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var b = go.AddComponent<AlarmBeacon>();
            b._life = seconds;
            b._light = PsxLight.Create(go.transform, Vector3.zero, new Color(1f, 0.08f, 0.05f), 2.4f, 8f, PsxFlicker.None, "AlarmLight");
            b._light.Priority = 8;
            return b;
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _life || _light == null) { Destroy(gameObject); return; }
            float fade = Mathf.Clamp01((_life - _age) / 3f);              // dies away over the last seconds
            float pulse = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(_age * 3.4f)); // a slow siren beat
            _light.Intensity = 2.4f * pulse * fade;
        }
    }
}
