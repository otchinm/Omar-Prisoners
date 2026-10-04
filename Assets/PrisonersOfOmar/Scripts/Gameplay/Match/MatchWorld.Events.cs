using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Net;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // Random / scripted world events (host decides, everyone presents).
    public sealed partial class MatchWorld
    {
        void OnWorldEvent(int sender, NetReader r)
        {
            var kind = (WorldEventKind)r.ReadByte();
            float a = r.ReadFloat();
            float b = r.ReadFloat();
            switch (kind)
            {
                case WorldEventKind.CagesOpen:
                    VhsEffect.TriggerGlitch(0.4f, 0.4f);
                    AudioManager.Play2D(Snd.CageOpen, 0.5f);
                    if (!LocalIsOmar) AddMessage("THE CAGE LOCKS CLICK OPEN ALL AT ONCE. RUN. HIDE. GET OUT.", 6f);
                    break;
                case WorldEventKind.OmarAwake:
                    if (LocalIsOmar) AddMessage("HUNT THEM. NOBODY LEAVES THE BASE OF THE SECOND CLASS.", 5f);
                    else
                    {
                        AudioManager.Play2D(AudioManager.Variant(Snd.Growl, 2), 0.55f, 0.8f, AudioCategory.Omar);
                        AddMessage("SOMETHING HEAVY IS COMING UP FROM THE BASEMENT...", 5f);
                    }
                    break;
                case WorldEventKind.PowerOut:
                    SetPower(false);
                    AudioManager.Play2D(Snd.PowerOff, 0.7f);
                    AddMessage("THE LIGHTS DIE", 3f);
                    break;
                case WorldEventKind.PowerOn:
                    SetPower(true);
                    AudioManager.Play2D(Snd.PowerOn, 0.5f);
                    break;
                case WorldEventKind.AnomalyPulse:
                    AnomalySystem.Pulse(a, b);
                    VhsEffect.TriggerGlitch(Mathf.Clamp01(a), 0.5f + b * 0.2f);
                    if (a > 0.6f) VhsEffect.TriggerRoll(0.6f);
                    AudioManager.Play2D(AudioManager.Variant(Snd.StingAnomaly, 2), Mathf.Clamp01(0.35f + a * 0.6f), 1f, AudioCategory.Stinger);
                    AudioManager.Duck(0.4f, b);
                    ShuffleMannequins((int)(a * 1000f + b * 10f) ^ Seed);
                    break;
                case WorldEventKind.PhoneRing:
                    PlayAtMarker("PhoneKitchen", Snd.PhoneRing, 0.9f, 30f);
                    break;
                case WorldEventKind.TvOn:
                    if (PlayAtMarker("TvLivingRoom", Snd.TvOn, 1f, 25f) && LocalAvatar != null && Map.Markers.TryGetValue("TvLivingRoom", out var tv) && tv != null
                        && Vector3.Distance(tv.position, LocalAvatar.Position) < 12f)
                        VhsEffect.TriggerGlitch(0.6f, 0.5f);
                    break;
                case WorldEventKind.Thunder:
                    AudioManager.Play2D(AudioManager.Variant(Snd.Thunder, 2), 0.55f, Random.Range(0.85f, 1.05f), AudioCategory.Ambience);
                    break;
                case WorldEventKind.DistantScream:
                    AudioManager.Play3D(AudioManager.Variant(Snd.DistantScream, 3), new Vector3(a, 2f, b), 1f, Random.Range(0.85f, 1f), 10f, 120f, AudioCategory.Ambience);
                    break;
                case WorldEventKind.MannequinShuffle:
                    ShuffleMannequins((int)a);
                    break;
                case WorldEventKind.CarCrankFail:
                case WorldEventKind.CarNoBattery:
                case WorldEventKind.CarNoFuel:
                    if (Map.Car != null && Map.Car.Root != null)
                        AudioManager.Play3D(kind == WorldEventKind.CarNoBattery ? Snd.KeyUnlock : Snd.CarCrankFail, Map.Car.Root.position, 1f, 1f, 3f, 60f);
                    if ((int)a == LocalId)
                    {
                        if (kind == WorldEventKind.CarNoBattery) AddMessage("CLICK... CLICK... THE BATTERY IS DEAD", 4f);
                        else if (kind == WorldEventKind.CarNoFuel) AddMessage("IT CRANKS BUT WON'T CATCH. NO GAS.", 4f);
                        else AddMessage("IT WON'T CATCH... TRY AGAIN!", 3f);
                    }
                    break;
                case WorldEventKind.Whispers:
                    if (!LocalIsOmar) AudioManager.Play2D(AudioManager.Variant(Snd.Whisper, 2), 0.35f, 1f, AudioCategory.Ambience);
                    break;
            }
        }

        bool PlayAtMarker(string marker, string clip, float volume, float maxDist)
        {
            if (!Map.Markers.TryGetValue(marker, out var t) || t == null) return false;
            AudioManager.Play3D(clip, t.position, volume, 1f, 2f, maxDist);
            return true;
        }

        void SetPower(bool on)
        {
            PowerOn = on;
            foreach (var l in Map.PowerLights) if (l != null) l.On = on;
            if (on && !Objectives.FuseIn) foreach (var l in Map.RadioRoomLights) if (l != null) l.On = false;
            for (int i = 0; i < _emitters.Count; i++)
                if (_emitterPowered[i] && _emitters[i] != null)
                    AudioManager.SetVolume(_emitters[i], on && i < Map.SoundEmitters.Count ? Map.SoundEmitters[i].Volume : 0f);
        }

        void ShuffleMannequins(int seed)
        {
            var rng = new DeterministicRandom(seed, 7);
            foreach (var m in Map.Mannequins)
            {
                if (m == null || m.Root == null || m.AltPoses == null || m.AltPoses.Length == 0) continue;
                if (!rng.Chance(0.6f)) continue;
                // never move one the local player is looking straight at
                if (LocalAvatar != null && PsxCameraRig.Instance != null)
                {
                    var cam = PsxCameraRig.Instance.transform;
                    Vector3 to = m.Root.position + Vector3.up - cam.position;
                    if (to.magnitude < 10f && Vector3.Dot(to.normalized, cam.forward) > 0.75f) continue;
                }
                var p = m.AltPoses[rng.Range(0, m.AltPoses.Length)];
                m.Root.SetPositionAndRotation(p.position, p.rotation);
            }
        }
    }

    /// <summary>Low-poly rescue helicopter (dark fuselage, spinning rotor).</summary>
    public static class HelicopterVisual
    {
        public static GameObject Build(Transform parent, Vector3 position)
        {
            var root = new GameObject("Helicopter");
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var mb = new MeshBuilder();
            mb.SetMaterial(PsxMaterials.Get("Textures/Env/metal_painted_green", PsxSurface.Lit, new Color(0.45f, 0.5f, 0.45f)));
            mb.AddBox(new Vector3(0, 0, 0), new Vector3(2.2f, 2f, 4.2f), BoxUV.Local, 2f);
            mb.AddBox(new Vector3(0, 0.3f, -4.2f), new Vector3(0.5f, 0.5f, 4.5f), BoxUV.Local, 2f);
            mb.AddBox(new Vector3(0, 1.1f, -6.3f), new Vector3(0.15f, 1.6f, 0.8f), BoxUV.Local, 2f);
            mb.AddBox(new Vector3(-0.9f, -1.35f, 0), new Vector3(0.12f, 0.12f, 3.6f), BoxUV.Local, 2f);
            mb.AddBox(new Vector3(0.9f, -1.35f, 0), new Vector3(0.12f, 0.12f, 3.6f), BoxUV.Local, 2f);
            mb.SetMaterial(PsxMaterials.Get("Textures/Props/window_dark", PsxSurface.Lit));
            mb.AddBox(new Vector3(0, 0.15f, 2.2f), new Vector3(1.9f, 1.3f, 0.4f), BoxUV.PerFace);
            mb.Build("Body", root.transform, Layers.World);
            var rotor = new GameObject("Rotor");
            rotor.transform.SetParent(root.transform, false);
            rotor.transform.localPosition = new Vector3(0, 1.2f, 0);
            var rb = new MeshBuilder();
            rb.SetMaterial(PsxMaterials.Get("Textures/Env/metal_dark", PsxSurface.LitDoubleSided));
            rb.AddBox(Vector3.zero, new Vector3(11f, 0.05f, 0.35f), BoxUV.Local, 2f);
            rb.AddBox(Vector3.zero, new Vector3(0.35f, 0.05f, 11f), BoxUV.Local, 2f);
            rb.Build("Blades", rotor.transform, Layers.World);
            rotor.AddComponent<Spinner>().DegreesPerSecond = 900f;
            var blink = PsxLight.Create(root.transform, new Vector3(0, -1.1f, 0), new Color(1f, 0.15f, 0.1f), 1.5f, 6f, PsxFlicker.Strobe);
            blink.FlickerSpeed = 1.5f;
            return root;
        }
    }

    /// <summary>Rotates its transform around local Y.</summary>
    public sealed class Spinner : MonoBehaviour
    {
        public float DegreesPerSecond = 90f;
        void Update() => transform.Rotate(0f, DegreesPerSecond * Time.deltaTime, 0f, Space.Self);
    }
}
