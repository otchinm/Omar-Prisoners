using System.Collections.Generic;
using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>Deterministic item placement (every peer computes the same list from the seed).</summary>
    public static class ItemSpawner
    {
        public struct Placement
        {
            public ItemType Type;
            public Vector3 Position;
            public float Yaw;
            public float Charge;
        }

        /// <summary>Areas where a key item must NOT spawn (too close to where it is used, or behind the door it is needed to open).
        /// The tunnel only opens with the shelter code, which is already an escape route.</summary>
        static string Forbidden(ItemType t)
        {
            switch (t)
            {
                case ItemType.BoltCutters: return "Yard|Porch|Tunnel";
                case ItemType.CarKeys: return "ParkingLot|Restroom|Tunnel";
                case ItemType.GasCan: return "ParkingLot|Tunnel";
                case ItemType.CarBattery: return "ParkingLot|Tunnel";
                case ItemType.Fuse: return "Basement.Generator|House.RadioRoom|Tunnel";
                case ItemType.CageKey: return "House.CageRoom|Tunnel";
                case ItemType.Crowbar: return "House.Bathroom|Tunnel";   // the bathroom is boarded up
                default: return null;
            }
        }

        public static List<Placement> Place(MapData map, int seed, bool needBattery)
        {
            var rng = DeterministicRandom.For(seed, "items");
            var result = new List<Placement>();
            var keyList = new List<ItemType> { ItemType.BoltCutters, ItemType.CarKeys, ItemType.GasCan, ItemType.Fuse, ItemType.CageKey, ItemType.Crowbar };
            if (needBattery) keyList.Add(ItemType.CarBattery);
            var common = new List<ItemType>();
            void AddN(ItemType t, int n) { for (int i = 0; i < n; i++) common.Add(t); }
            AddN(ItemType.LighterFuel, 4);
            AddN(ItemType.Bandages, 4);
            AddN(ItemType.Flashlight, 2);
            AddN(ItemType.Batteries, 3);
            AddN(ItemType.SoundMeter, 1);
            AddN(ItemType.Lockpick, 3);
            AddN(ItemType.Bottle, 5);
            AddN(ItemType.Pills, 2);

            var spots = new List<ItemSpawnInfo>(map.ItemSpawns);
            rng.Shuffle(spots);
            var used = new HashSet<ItemSpawnInfo>();
            var usedAreasForKeys = new HashSet<string>();

            // key items: prefer Key/Any tier, distinct areas, respect forbidden areas
            foreach (var t in keyList)
            {
                ItemSpawnInfo pick = null;
                string forb = Forbidden(t);
                for (int pass = 0; pass < 3 && pick == null; pass++)
                {
                    foreach (var s in spots)
                    {
                        if (used.Contains(s)) continue;
                        if (pass == 0 && s.Tier == ItemSpawnTier.Common) continue;
                        if (pass < 2 && usedAreasForKeys.Contains(s.Area)) continue;
                        if (forb != null && pass < 2 && s.Area != null && MatchesAny(s.Area, forb)) continue;
                        pick = s; break;
                    }
                }
                if (pick == null) continue;
                used.Add(pick);
                if (pick.Area != null) usedAreasForKeys.Add(pick.Area);
                result.Add(new Placement { Type = t, Position = pick.Position, Yaw = pick.Yaw, Charge = 1f });
            }

            foreach (var t in common)
            {
                ItemSpawnInfo pick = null;
                for (int pass = 0; pass < 2 && pick == null; pass++)
                    foreach (var s in spots)
                    {
                        if (used.Contains(s)) continue;
                        if (pass == 0 && s.Tier == ItemSpawnTier.Key) continue;
                        pick = s; break;
                    }
                if (pick == null) break;
                used.Add(pick);
                float charge = t == ItemType.Flashlight ? rng.Range(0.35f, 0.9f) : 1f;
                result.Add(new Placement { Type = t, Position = pick.Position, Yaw = pick.Yaw + rng.Range(-30f, 30f), Charge = charge });
            }
            return result;
        }

        static bool MatchesAny(string area, string patterns)
        {
            foreach (var p in patterns.Split('|'))
                if (area == p || area.StartsWith(p + ".") || area.StartsWith(p)) return true;
            return false;
        }
    }

    /// <summary>A thrown bottle flying with physics. The thrower's copy reports the impact to the host.</summary>
    public sealed class ThrownBottle : MonoBehaviour
    {
        int _itemId;
        bool _report;
        bool _done;
        float _life;

        public static ThrownBottle Spawn(Vector3 from, Vector3 velocity, int reportItemId, Transform parent)
        {
            var go = new GameObject("ThrownBottle");
            go.transform.SetParent(parent, false);
            go.transform.position = from;
            go.layer = Layers.Item;
            try
            {
                var model = ItemMeshFactory.Build(ItemType.Bottle);
                model.transform.SetParent(go.transform, false);
                GeoUtil.SetLayerRecursive(model, Layers.Item);
            }
            catch { }
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.06f;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.AddForce(velocity, ForceMode.VelocityChange);
            rb.AddTorque(new Vector3(8f, 3f, 5f), ForceMode.VelocityChange);
            // don't collide with players
            Physics.IgnoreLayerCollision(Layers.Item, Layers.Player, true);
            Physics.IgnoreLayerCollision(Layers.Item, Layers.Omar, true);
            var b = go.AddComponent<ThrownBottle>();
            b._itemId = reportItemId;
            b._report = reportItemId >= 0;
            return b;
        }

        void Update()
        {
            _life += Time.deltaTime;
            if (_life > 6f && !_done) Shatter();
        }

        void OnCollisionEnter(Collision c)
        {
            if (_done || _life < 0.05f) return;
            Shatter();
        }

        void Shatter()
        {
            _done = true;
            if (_report && MatchWorld.Instance != null) MatchWorld.Instance.SendBottleImpact(_itemId, transform.position);
            Destroy(gameObject);
        }
    }
}
