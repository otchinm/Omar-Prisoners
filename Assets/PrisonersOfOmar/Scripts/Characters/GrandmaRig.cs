using UnityEngine;

namespace PrisonersOfOmar.Characters
{
    /// <summary>(iteration 2) What the grandmother is doing (drives her animation).</summary>
    public enum GrandmaMode : byte
    {
        WatchingTv = 0, // slumped in the wheelchair, head bobbing, facing the TV
        Roaming,        // pushing the wheels, rolling through the house
        Screaming,      // spotted a prisoner: leans forward, mouth open, arms up, shaking
        Dead,           // shot: slumped forward over her knees, blood
    }

    /// <summary>
    /// (iteration 2) The grandmother in her wheelchair (see the "granny" references): one rigid prop + simple
    /// procedural animation. Origin = floor under the wheelchair centre, facing +Z.
    /// Placeholder (grey boxes) until the characters pass replaces the visuals; the API is the contract.
    /// </summary>
    public sealed class GrandmaRig : MonoBehaviour
    {
        public GrandmaMode Mode { get; private set; }
        /// <summary>Head height in world space (eye line for line-of-sight tests).</summary>
        public Vector3 EyePosition => transform.position + transform.up * 1.15f + transform.forward * 0.1f;

        public static GrandmaRig Create(Transform parent, int layer = Layers.Corpse)
        {
            var go = new GameObject("Grandma");
            go.layer = layer;
            go.transform.SetParent(parent, false);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(body.GetComponent<Collider>());
            body.name = "PlaceholderBody";
            body.layer = layer;
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            body.transform.localScale = new Vector3(0.6f, 1.2f, 0.7f);
            return go.AddComponent<GrandmaRig>();
        }

        public void SetMode(GrandmaMode mode) => Mode = mode;
        /// <summary>Rolling speed in m/s (wheel spin + hand pushing animation).</summary>
        public void SetMoveSpeed(float metersPerSecond) { }
        /// <summary>Turn the head towards a point (null = look ahead / at the TV).</summary>
        public void LookAt(Vector3? target) { }
    }
}
