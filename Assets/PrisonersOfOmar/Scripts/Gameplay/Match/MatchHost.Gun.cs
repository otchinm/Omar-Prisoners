using System.Collections.Generic;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 2) Host rules for the revolver (and the hooks the grandmother / kitchen packages plug into).
    public sealed partial class MatchHost
    {
        /// <summary>Every host tick (grandmother, kitchen routine).</summary>
        void TickWorld(float dt)
        {
            TickGrandma(dt);
            TickKitchen(dt);
        }

        partial void TickGrandma(float dt);
        partial void TickKitchen(float dt);
        /// <summary>A noise somebody made (the grandmother turns towards it).</summary>
        partial void GrandmaHeard(Vector3 pos, float radius);
        /// <summary>A shot hit <paramref name="c"/>: set <paramref name="kind"/> to MatchWorld.ShotGrandma when it was her.</summary>
        partial void OnShotHit(int shooter, Collider c, Vector3 point, ref byte kind);

        readonly Dictionary<int, float> _lastShot = new Dictionary<int, float>();
        static readonly int ShotMask = Layers.Mask(Layers.World, Layers.Door, Layers.Default, Layers.Omar, Layers.Player, Layers.Corpse);
        readonly RaycastHit[] _shotHits = new RaycastHit[16];

        public void OnShootReq(int sender, NetReader r)
        {
            Vector3 origin = r.ReadVector3();
            Vector3 dir = r.ReadVector3();
            if (!W.Running || !IsPrisoner(sender)) return;
            var st = W.StatusOf(sender);
            if (st == null || st.Life != LifeState.Free || st.Hidden || st.InCar || st.Trapped) return;
            if (float.IsNaN(dir.x) || float.IsNaN(origin.x) || dir.sqrMagnitude < 0.25f) return;
            dir.Normalize();
            if (_lastShot.TryGetValue(sender, out var last) && W.Time - last < 0.45f) return;
            int gunId = FindHeld(sender, ItemType.Revolver);
            var gun = W.GetItem(gunId);
            if (gun == null || gun.Charge < 0.49f) return;
            Vector3 eye = PosOf(sender) + Vector3.up * 1.55f;
            if (Vector3.Distance(origin, eye) > 1.6f) origin = eye;
            _lastShot[sender] = W.Time;
            SetCharge(gunId, Mathf.Max(0f, gun.Charge - 0.5f));

            byte kind = MatchWorld.ShotMiss;
            Vector3 point = origin + dir * 60f, normal = -dir;
            int victim = -1;
            int n = Physics.RaycastNonAlloc(origin, dir, _shotHits, 60f, ShotMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(_shotHits, 0, n, HitComparer.Instance);
            for (int i = 0; i < n; i++)
            {
                var h = _shotHits[i];
                var av = h.collider.GetComponentInParent<Avatar>();
                if (av != null && av.Id == sender) continue;
                point = h.point; normal = h.normal;
                if (av != null)
                {
                    if (!av.Visible) continue;
                    kind = av.IsOmar ? MatchWorld.ShotOmar : MatchWorld.ShotPerson;
                    victim = av.Id;
                }
                else
                {
                    kind = MatchWorld.ShotWorld;
                    OnShotHit(sender, h.collider, h.point, ref kind);
                }
                break;
            }

            var w = S.Begin(Msg.ShotFx);
            w.WriteByte((byte)sender);
            w.WriteVector3(origin);
            w.WriteVector3(point);
            w.WriteVector3(normal);
            w.WriteByte(kind);
            S.SendToAll(NetChannel.Reliable);

            DeliverNoise(origin, 45f);
            if (kind == MatchWorld.ShotOmar)
            {
                // a bullet doesn't kill him - it only makes him stagger for a few seconds
                Stun(victim, 4f);
                Message("HE STAGGERS... RUN!", 2.5f, sender);
            }
        }

        sealed class HitComparer : IComparer<RaycastHit>
        {
            public static readonly HitComparer Instance = new HitComparer();
            public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
        }
    }
}
