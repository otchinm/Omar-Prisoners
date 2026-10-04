using System.Collections.Generic;
using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Net;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 2) Revolver, grandmother, kitchen: client side, owned by the "world" work package.
    public sealed partial class MatchWorld
    {
        void RegisterWorldHandlers(NetSession s)
        {
            s.On(Msg.ShootReq, (id, r) => Host?.OnShootReq(id, r));
            s.On(Msg.ShotFx, OnShotFx);
            RegisterGrandmaHandlers(s);
            RegisterKitchenHandlers(s);
            s.On(Msg.CageRattle, OnCageRattle);
        }

        void UnregisterWorldHandlers(NetSession s)
        {
            s.Off(Msg.ShootReq);
            s.Off(Msg.ShotFx);
            UnregisterGrandmaHandlers(s);
            UnregisterKitchenHandlers(s);
            s.Off(Msg.CageRattle);
        }

        partial void RegisterGrandmaHandlers(NetSession s);
        partial void UnregisterGrandmaHandlers(NetSession s);
        partial void RegisterKitchenHandlers(NetSession s);
        partial void UnregisterKitchenHandlers(NetSession s);
        partial void TickGrandma(float dt);
        partial void TickKitchen(float dt);

        /// <summary>Every frame (grandmother, kitchen presentation).</summary>
        void TickWorld(float dt)
        {
            TickGrandma(dt);
            TickKitchen(dt);
        }

        /// <summary>The local prisoner fired the revolver they hold (the host resolves the hit).</summary>
        public void SendShoot(Vector3 origin, Vector3 direction)
        {
            var w = Session.Begin(Msg.ShootReq);
            w.WriteVector3(origin);
            w.WriteVector3(direction.normalized);
            Session.SendToHost(NetChannel.Reliable);
        }

        /// <summary>Shot kinds in <see cref="Msg.ShotFx"/>.</summary>
        public const byte ShotWorld = 0, ShotOmar = 1, ShotGrandma = 2, ShotMiss = 3, ShotPerson = 4;

        void OnShotFx(int sender, NetReader r)
        {
            int shooter = r.ReadByte();
            Vector3 origin = r.ReadVector3();
            Vector3 hit = r.ReadVector3();
            Vector3 normal = r.ReadVector3();
            byte kind = r.ReadByte();
            Vector3 dir = hit - origin;
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.forward;
            if (shooter != LocalId)
            {
                var av = AvatarOf(shooter);
                Vector3 muzzle = av != null ? av.MuzzlePosition : origin;
                AudioManager.Play3D(Snd.GunShot, muzzle, 1f, Random.Range(0.95f, 1.03f), 6f, 95f);
                try { PsxFx.MuzzleFlash(muzzle, dir); } catch { }
                if (av != null) av.PlayAction(CharacterAction.Shoot);
            }
            switch (kind)
            {
                case ShotWorld:
                    try { PsxFx.Sparks(hit, normal); PsxFx.Dust(hit, 0.4f); } catch { }
                    AudioManager.Play3D(Snd.CleaverHitWall, hit, 0.6f, Random.Range(1.2f, 1.4f), 1f, 14f);
                    break;
                case ShotOmar:
                case ShotGrandma:
                case ShotPerson:
                    try { PsxFx.BloodBurst(hit, dir, kind == ShotGrandma ? 1.4f : 0.9f); } catch { }
                    break;
            }
        }
    }
}
