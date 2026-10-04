using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>
    /// (iteration 2) The grandmother on every peer: her wheelchair rig, a solid body, the TV she watches, her voice.
    /// The host moves her (MatchHost.Grandma.cs) and streams the pose; clients follow smoothly.
    /// </summary>
    public sealed class GrandmaEntity
    {
        /// <summary>Voice events carried by Msg.GrandmaState.</summary>
        public const byte VoiceNone = 0, VoiceSpot = 1, VoiceScream = 2, VoiceDeath = 3;

        public readonly GrandmaInfo Info;
        public readonly Transform Root;
        public readonly GrandmaRig Rig;
        public readonly CapsuleCollider Body;
        public GrandmaMode Mode { get; private set; }
        public byte Stage;
        public int Target = -1;
        public Vector3 Position;
        public float Yaw;
        public bool Dead => Mode == GrandmaMode.Dead;
        public Vector3 Eye => Rig != null ? Rig.EyePosition : Position + Vector3.up * 1.15f;
        public Vector3 Forward => Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward;

        Vector3 _netPos;
        float _netYaw;
        bool _hasNet;
        Vector3 _lastShownPos;
        float _speed;
        float _nextMutter;
        AudioSource _tv, _wheel;

        public GrandmaEntity(GrandmaInfo info, Transform parent)
        {
            Info = info;
            Position = _netPos = _lastShownPos = info.ChairPose.position;
            Yaw = _netYaw = info.ChairPose.rotation.eulerAngles.y;
            Root = GeoUtil.CreateChild(parent, "GrandmaRoot", Position, Quaternion.Euler(0f, Yaw, 0f), Layers.Corpse);
            try { Rig = GrandmaRig.Create(Root, Layers.Corpse); } catch (System.Exception e) { Debug.LogException(e); }
            var bodyGo = new GameObject("GrandmaBody");
            bodyGo.layer = Layers.Corpse;
            bodyGo.transform.SetParent(Root, false);
            Body = bodyGo.AddComponent<CapsuleCollider>();
            Body.radius = 0.36f;
            Body.height = 1.3f;
            Body.center = new Vector3(0f, 0.65f, 0.02f);
            _tv = AudioManager.Loop3D(Snd.GrandmaTvLoop, info.TvSoundPosition, 0.6f, 13f, AudioCategory.Ambience, null, 1.5f);
            _nextMutter = Time.time + Random.Range(6f, 15f);
        }

        public void Destroy()
        {
            AudioManager.Stop(_tv, 0.3f);
            AudioManager.Stop(_wheel, 0.2f);
            if (Root != null) Object.Destroy(Root.gameObject);
        }

        /// <summary>Reliable state from the host (mode, stage, pose, a voice event, who she stares at).</summary>
        public void ApplyState(GrandmaMode mode, byte stage, Vector3 pos, float yaw, byte voice, int target, bool authority)
        {
            if (Mode != mode)
            {
                Mode = mode;
                if (Rig != null) Rig.SetMode(mode);
            }
            Stage = stage;
            Target = target;
            if (!authority) SetNet(pos, yaw, true);
            Vector3 head = Eye;
            switch (voice)
            {
                case VoiceSpot:
                    AudioManager.Play3D(Snd.GrandmaSpot, head, 1f, Random.Range(0.96f, 1.04f), 4f, 60f, AudioCategory.Stinger);
                    break;
                case VoiceScream:
                    AudioManager.Play3D(AudioManager.Variant(Snd.GrandmaScream, 3), head, 1f, Random.Range(0.94f, 1.05f), 4f, 70f, AudioCategory.Stinger);
                    break;
                case VoiceDeath:
                    AudioManager.Play3D(Snd.GrandmaDeath, head, 1f, 1f, 3f, 45f, AudioCategory.Stinger);
                    AudioManager.Stop(_wheel, 0.1f); _wheel = null;
                    break;
            }
        }

        public void SetNet(Vector3 pos, float yaw, bool snapIfFar = false)
        {
            _netPos = pos;
            _netYaw = yaw;
            if (!_hasNet || (snapIfFar && (pos - Position).sqrMagnitude > 9f)) { Position = pos; Yaw = yaw; }
            _hasNet = true;
        }

        /// <summary>Every frame. <paramref name="authority"/> = the host moves her itself.</summary>
        public void Tick(float dt, bool authority, MatchWorld w)
        {
            if (!authority && _hasNet)
            {
                Position = Vector3.Lerp(Position, _netPos, 1f - Mathf.Exp(-dt * 10f));
                Yaw = Mathf.LerpAngle(Yaw, _netYaw, 1f - Mathf.Exp(-dt * 10f));
            }
            if (Root != null) Root.SetPositionAndRotation(Position, Quaternion.Euler(0f, Yaw, 0f));
            Vector3 d = Position - _lastShownPos; d.y = 0f;
            _lastShownPos = Position;
            float inst = dt > 0f ? d.magnitude / dt : 0f;
            _speed = Mathf.Lerp(_speed, inst, 1f - Mathf.Exp(-dt * 6f));
            if (Rig != null)
            {
                Rig.SetMoveSpeed(_speed);
                Avatar t = Target >= 0 ? w.AvatarOf(Target) : null;
                Rig.LookAt(t != null && !Dead ? t.EyePosition : (Vector3?)null);
            }

            // the wheels squeak while she rolls
            float wheelVol = Dead ? 0f : Mathf.Clamp01(_speed / 0.8f) * 0.7f;
            if (wheelVol > 0.02f)
            {
                if (_wheel == null) _wheel = AudioManager.Loop3D(Snd.WheelchairLoop, Position, 0f, 16f, AudioCategory.Sfx, Root, 0.1f);
                AudioManager.SetVolume(_wheel, wheelVol);
                AudioManager.SetPitch(_wheel, 0.85f + 0.3f * Mathf.Clamp01(_speed));
            }
            else if (_wheel != null) { AudioManager.Stop(_wheel, 0.3f); _wheel = null; }

            // she mutters to the TV
            if (!Dead && Mode != GrandmaMode.Screaming && Time.time >= _nextMutter)
            {
                _nextMutter = Time.time + Random.Range(10f, 26f);
                AudioManager.Play3D(AudioManager.Variant(Snd.GrandmaMutter, 3), Eye, 0.7f, Random.Range(0.92f, 1.05f), 1.5f, 12f);
            }
        }
    }
}
