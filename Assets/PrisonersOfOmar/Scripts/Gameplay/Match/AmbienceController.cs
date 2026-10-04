using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>Ambience bed by zone + sparse random creepy one-shots + anomaly drone (client side).</summary>
    public sealed class AmbienceController : MonoBehaviour
    {
        AmbientType _current = (AmbientType)255;
        float _oneShotTimer = 6f;
        AudioSource _anomaly;
        float _anomalyVol;

        void OnDestroy()
        {
            AudioManager.SetAmbience(null);
            AudioManager.Stop(_anomaly, 0.3f);
        }

        void Update()
        {
            var w = MatchWorld.Instance;
            var rig = PsxCameraRig.Instance;
            if (w == null || rig == null || w.Map == null) return;
            if (w.Ending != null)
            {
                // the night is over: let the beds and the drone fade away under the ending screen
                if (_current != (AmbientType)254) { _current = (AmbientType)254; AudioManager.SetAmbience(null); }
                if (_anomaly != null) { AudioManager.Stop(_anomaly, 1f); _anomaly = null; }
                return;
            }
            Vector3 p = rig.transform.position;
            var type = ZoneAt(w.Map, p);
            if (type != _current)
            {
                _current = type;
                AudioManager.SetAmbience(Clip(type), Volume(type), 2.5f);
            }

            _oneShotTimer -= Time.deltaTime;
            if (_oneShotTimer <= 0f)
            {
                _oneShotTimer = Random.Range(7f, 18f);
                PlayRandom(type, p);
            }

            float an = Mathf.Clamp01((AnomalySystem.GetIntensityAt(p) - 0.12f) * 1.6f);
            _anomalyVol = Mathf.MoveTowards(_anomalyVol, an * 0.6f, Time.deltaTime * 0.5f);
            if (_anomalyVol > 0.01f && _anomaly == null) _anomaly = AudioManager.Loop2D(Snd.AnomalyLoop, 0f, AudioCategory.Ambience, 0.2f);
            if (_anomaly != null)
            {
                AudioManager.SetVolume(_anomaly, _anomalyVol);
                if (_anomalyVol <= 0.005f) { AudioManager.Stop(_anomaly, 0.5f); _anomaly = null; }
            }
        }

        public static AmbientType ZoneAt(MapData map, Vector3 p)
        {
            AmbientType best = AmbientType.Exterior;
            float bestVol = float.MaxValue;
            foreach (var z in map.AmbientZones)
            {
                if (z.Type == AmbientType.Exterior || !z.Bounds.Contains(p)) continue;
                float v = z.Bounds.size.x * z.Bounds.size.y * z.Bounds.size.z;
                if (v < bestVol) { bestVol = v; best = z.Type; }
            }
            return best;
        }

        static string Clip(AmbientType t)
        {
            switch (t)
            {
                case AmbientType.House: return Snd.AmbHouse;
                case AmbientType.Basement: return Snd.AmbBasement;
                case AmbientType.Barn: return Snd.AmbBarn;
                case AmbientType.Tunnel: return Snd.AmbTunnel;
                case AmbientType.Restroom: return Snd.AmbRestroom;
                case AmbientType.Shed: return Snd.AmbBarn;
                case AmbientType.Silo: return Snd.AmbTunnel;
                default: return Snd.AmbExterior;
            }
        }

        // quiet beds: like the reference most of the time you only hear the room and your own steps
        static float Volume(AmbientType t) => t == AmbientType.Exterior ? 0.42f : 0.3f;

        static void PlayRandom(AmbientType t, Vector3 p)
        {
            Vector3 off = Random.onUnitSphere * Random.Range(6f, 14f);
            off.y = Mathf.Abs(off.y) * 0.3f;
            Vector3 at = p + off;
            float r = Random.value;
            switch (t)
            {
                case AmbientType.House:
                    if (r < 0.55f) AudioManager.Play3D(AudioManager.Variant(Snd.Creak, 4), at, 0.6f, Random.Range(0.85f, 1.1f), 3f, 25f, AudioCategory.Ambience);
                    else if (r < 0.8f) AudioManager.Play3D(AudioManager.Variant(Snd.Thump, 3), at, 0.55f, 1f, 3f, 25f, AudioCategory.Ambience);
                    else AudioManager.Play3D(AudioManager.Variant(Snd.Whisper, 2), at, 0.3f, 1f, 2f, 12f, AudioCategory.Ambience);
                    break;
                case AmbientType.Basement:
                case AmbientType.Tunnel:
                case AmbientType.Silo:
                    if (r < 0.5f) AudioManager.Play3D(AudioManager.Variant(Snd.Drip, 3), at, 0.5f, Random.Range(0.9f, 1.1f), 2f, 15f, AudioCategory.Ambience);
                    else if (r < 0.8f) AudioManager.Play3D(AudioManager.Variant(Snd.MetalScrape, 2), at, 0.45f, Random.Range(0.8f, 1f), 3f, 25f, AudioCategory.Ambience);
                    else AudioManager.Play3D(AudioManager.Variant(Snd.Whisper, 2), at, 0.3f, 1f, 2f, 12f, AudioCategory.Ambience);
                    break;
                default:
                    if (r < 0.35f) AudioManager.Play3D(AudioManager.Variant(Snd.Creak, 4), at, 0.4f, 0.8f, 4f, 30f, AudioCategory.Ambience);
                    else if (r < 0.55f) AudioManager.Play3D(Snd.DogHowl, p + off.normalized * 80f, 0.5f, Random.Range(0.85f, 1f), 20f, 150f, AudioCategory.Ambience);
                    else if (r < 0.75f) AudioManager.Play3D(AudioManager.Variant(Snd.DistantScream, 3), p + off.normalized * 70f, 0.45f, Random.Range(0.8f, 1f), 20f, 150f, AudioCategory.Ambience);
                    else AudioManager.Play3D(AudioManager.Variant(Snd.MetalScrape, 2), at, 0.35f, 0.7f, 4f, 30f, AudioCategory.Ambience);
                    break;
            }
        }
    }
}
