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
                case ItemType.SmallKey: return "Tunnel|OutsideGate";
                case ItemType.VhsTape: return "House.Living|Tunnel";   // never right next to the VCR
                default: return null;
            }
        }

        /// <summary>Areas a key item has to spawn in (null = anywhere): the cage key and the screwdriver lie somewhere in
        /// the house so the prisoners waking up in the cage room can find them without crossing the whole farm.</summary>
        static string Required(ItemType t)
        {
            switch (t)
            {
                case ItemType.CageKey: return "House.";
                case ItemType.Screwdriver: return "House.";
                case ItemType.VhsTape: return "House.";
                default: return null;
            }
        }

        /// <summary>Fits into a drawer (spots marked Small).</summary>
        static bool FitsDrawer(ItemType t)
        {
            switch (t)
            {
                case ItemType.BoltCutters: case ItemType.GasCan: case ItemType.CarBattery: case ItemType.Crowbar:
                case ItemType.Bottle: case ItemType.LighterFuel: case ItemType.Backpack: return false;
                default: return true;
            }
        }

        /// <summary>Supplies a key padlock guards (small, a reward for finding the key / spending a lockpick).</summary>
        static readonly ItemType[] KeyDrawerLoot = { ItemType.Pills, ItemType.Batteries, ItemType.Bandages, ItemType.Lockpick, ItemType.Pills };

        /// <summary>(iteration 3) Where the home video tape lies tonight (see <see cref="PickTapePlace"/>).</summary>
        public sealed class TapePlace
        {
            /// <summary>A <see cref="TapeSpotInfo.Kind"/>, or "keydrawer" / "codedrawer" (inside a padlocked drawer).</summary>
            public string Kind;
            public string Area;
            public Vector3 Position;
            public float Yaw;
            /// <summary>keydrawer / codedrawer: index into the key / combination padlocked drawer spots; else -1.</summary>
            public int Drawer = -1;
        }

        /// <summary>(iteration 3) Picks tonight's place for the tape among the map's prepared ones allowed on this difficulty
        /// (Hard and Nightmare add "inside a padlocked drawer": a key one - small keys always lie free - or the first
        /// combination one, whose code is in a note; never the one whose code only the tape gives). Its own random stream,
        /// so the rest of the layout does not depend on it. Null = no prepared place: an ordinary key spot.</summary>
        public static TapePlace PickTapePlace(MapData map, int seed, Difficulty difficulty, IList<Vector3> keyLockedSpots, IList<Vector3> codeLockedSpots)
        {
            if (!map.Sockets.Exists(s => s.Name == "Vcr")) return null;
            int bit = 1 << (int)difficulty;
            var cands = new List<TapePlace>();
            foreach (var t in map.TapeSpots)
            {
                if ((t.Difficulties & bit) == 0) continue;
                var p = new TapePlace { Kind = t.Kind, Area = t.Area, Position = t.Position, Yaw = t.Yaw };
                if (t.Drawer)
                {
                    // the nearest drawer that is not padlocked
                    ItemSpawnInfo best = null;
                    float bd = 0.81f;
                    foreach (var s in map.ItemSpawns)
                    {
                        if (!s.Small || IsAny(keyLockedSpots, s.Position) || IsAny(codeLockedSpots, s.Position)) continue;
                        float d = (s.Position - t.Position).sqrMagnitude;
                        if (d < bd) { bd = d; best = s; }
                    }
                    if (best == null) continue;
                    p.Position = best.Position;
                    p.Area = best.Area ?? t.Area;
                }
                cands.Add(p);
            }
            if (difficulty == Difficulty.Hard || difficulty == Difficulty.Nightmare)
            {
                if (keyLockedSpots != null && keyLockedSpots.Count > 0) cands.Add(new TapePlace { Kind = "keydrawer" });
                if (codeLockedSpots != null && codeLockedSpots.Count >= 2) cands.Add(new TapePlace { Kind = "codedrawer" });
            }
            if (cands.Count == 0) return null;
            var rng = DeterministicRandom.For(seed, "tapespot");
            var pick = cands[rng.Range(0, cands.Count)];
            if (pick.Kind == "keydrawer" || pick.Kind == "codedrawer")
            {
                var list = pick.Kind == "keydrawer" ? keyLockedSpots : codeLockedSpots;
                pick.Drawer = pick.Kind == "keydrawer" ? rng.Range(0, list.Count) : 0;
                pick.Position = list[pick.Drawer];
                pick.Area = map.AreaAt(pick.Position);
            }
            return pick;
        }

        static bool IsAny(IList<Vector3> list, Vector3 p)
        {
            if (list != null) foreach (var q in list) if ((q - p).sqrMagnitude < 0.0004f) return true;
            return false;
        }

        /// <param name="keyLockedSpots">(iteration 3) item points of drawers with a key padlock: each gets a supply and one small
        /// key is placed elsewhere for each of them.</param>
        /// <param name="codeLockedSpots">item points of drawers with a combination padlock: each gets one of the key items that
        /// fit a drawer (the code is in the notes, a crowbar pries it open).</param>
        /// <param name="tape">(iteration 3) tonight's place for the tape (<see cref="PickTapePlace"/>); null = an ordinary key spot.</param>
        /// <param name="prisoners">(iteration 3) playing alone, the backpack always lies in the house.</param>
        public static List<Placement> Place(MapData map, int seed, bool needBattery, float supplyMul = 1f,
            IList<Vector3> keyLockedSpots = null, IList<Vector3> codeLockedSpots = null, TapePlace tape = null, int prisoners = 2)
        {
            var rng = DeterministicRandom.For(seed, "items");
            var result = new List<Placement>();
            var keyList = new List<ItemType> { ItemType.BoltCutters, ItemType.CarKeys, ItemType.GasCan, ItemType.Fuse, ItemType.CageKey, ItemType.Crowbar, ItemType.Screwdriver };
            if (needBattery) keyList.Add(ItemType.CarBattery);
            if (map.Sockets.Exists(s => s.Name == "Vcr") && tape == null) keyList.Add(ItemType.VhsTape);   // (iteration 3) the home video
            var common = new List<ItemType>();
            void AddN(ItemType t, int n) { n = Mathf.Max(1, Mathf.RoundToInt(n * supplyMul)); for (int i = 0; i < n; i++) common.Add(t); }
            AddN(ItemType.LighterFuel, 4);
            AddN(ItemType.Bandages, 4);
            AddN(ItemType.Flashlight, 2);
            AddN(ItemType.Batteries, 3);
            AddN(ItemType.SoundMeter, 1);
            AddN(ItemType.Lockpick, 3);
            AddN(ItemType.Bottle, 5);
            AddN(ItemType.Pills, 2);
            if (map.BackpackSpots.Count == 0) AddN(ItemType.Backpack, 1);   // (iteration 3) else at its prepared places, below

            var spots = new List<ItemSpawnInfo>(map.ItemSpawns);
            rng.Shuffle(spots);
            var used = new HashSet<ItemSpawnInfo>();
            // (iteration 3) padlocked drawers: reserve their spots and fill them first. Each key padlock guards a supply and
            // gets its small key somewhere else; the first combination padlock guards the car keys or the fuse, the others
            // one of the small keys (code -> key -> drawer chains). The cage key and the screwdriver stay free: they are
            // the way out of the cage room.
            int smallKeys = 0;
            if (keyLockedSpots != null)
                for (int k = 0; k < keyLockedSpots.Count; k++)
                {
                    var s = SpotAt(spots, keyLockedSpots[k]);
                    if (s == null) continue;
                    used.Add(s);
                    var loot = KeyDrawerLoot[rng.Range(0, KeyDrawerLoot.Length)];
                    if (tape != null && tape.Kind == "keydrawer" && tape.Drawer == k) loot = ItemType.VhsTape;
                    result.Add(new Placement { Type = loot, Position = s.Position, Yaw = s.Yaw + rng.Range(-30f, 30f), Charge = 1f });
                    smallKeys++;
                }
            if (codeLockedSpots != null)
                for (int k = 0; k < codeLockedSpots.Count; k++)
                {
                    var s = SpotAt(spots, codeLockedSpots[k]);
                    if (s == null) continue;
                    used.Add(s);
                    ItemType t = ItemType.None;
                    if (k == 0 && tape != null && tape.Kind == "codedrawer") t = ItemType.VhsTape;   // the car keys / fuse lie free then
                    else if (k > 0 && smallKeys > 0) { t = ItemType.SmallKey; smallKeys--; }
                    else
                    {
                        var fits = keyList.FindAll(x => (x == ItemType.CarKeys || x == ItemType.Fuse) && (Forbidden(x) == null || s.Area == null || !MatchesAny(s.Area, Forbidden(x))));
                        if (fits.Count > 0) { t = fits[rng.Range(0, fits.Count)]; keyList.Remove(t); }
                        else t = KeyDrawerLoot[rng.Range(0, KeyDrawerLoot.Length)];
                    }
                    result.Add(new Placement { Type = t, Position = s.Position, Yaw = s.Yaw, Charge = 1f });
                }
            for (int k = 0; k < smallKeys; k++) common.Insert(0, ItemType.SmallKey);   // first, so they always find a spot
            // (iteration 3) the tape at its prepared place (it takes the item spot there, if any)
            if (tape != null && tape.Drawer < 0)
            {
                ItemSpawnInfo near = null;
                float bd = 0.36f;
                foreach (var s in spots)
                {
                    if (used.Contains(s)) continue;
                    float d = (s.Position - tape.Position).sqrMagnitude;
                    if (d < bd) { bd = d; near = s; }
                }
                if (near != null) used.Add(near);
                result.Add(new Placement { Type = ItemType.VhsTape, Position = tape.Position, Yaw = tape.Yaw, Charge = 1f });
            }
            // (iteration 3) the backpack (5 slots instead of 3) at its prepared places: 2 on Easy, else 1; out in the shed /
            // barn only from Hard up, and always in the house when playing alone
            if (map.BackpackSpots.Count > 0)
            {
                int bit = 1 << (int)Tuning.CurrentDifficulty;
                var bp = map.BackpackSpots.FindAll(b => (b.Difficulties & bit) != 0 && (prisoners > 1 || (b.Area != null && b.Area.StartsWith("House."))));
                if (bp.Count == 0) bp = map.BackpackSpots.FindAll(b => b.Area != null && b.Area.StartsWith("House."));
                var brng = DeterministicRandom.For(seed, "backpack");
                brng.Shuffle(bp);
                int want = Tuning.CurrentDifficulty == Difficulty.Easy ? 2 : 1;
                for (int i = 0; i < want && i < bp.Count; i++)
                {
                    foreach (var s in spots)
                        if (!used.Contains(s) && (s.Position - bp[i].Position).sqrMagnitude < 0.36f) { used.Add(s); break; }
                    result.Add(new Placement { Type = ItemType.Backpack, Position = bp[i].Position, Yaw = bp[i].Yaw, Charge = 1f });
                }
            }
            // (iteration 2) exactly one revolver (2 rounds) at one of the dedicated spots
            if (map.GunSpots.Count > 0)
            {
                var g = map.GunSpots[rng.Range(0, map.GunSpots.Count)];
                result.Add(new Placement { Type = ItemType.Revolver, Position = g.Position, Yaw = g.Yaw, Charge = 1f });
            }
            var usedAreasForKeys = new HashSet<string>();

            // key items: prefer Key/Any tier, distinct areas, respect forbidden areas
            foreach (var t in keyList)
            {
                ItemSpawnInfo pick = null;
                string forb = Forbidden(t);
                string req = Required(t);
                // the cage key never lies in a room with cages (any cell room can hold prisoners)
                if (t == ItemType.CageKey) foreach (var room in map.CellRooms) if (!string.IsNullOrEmpty(room.Area)) forb += "|" + room.Area;
                for (int pass = 0; pass < 3 && pick == null; pass++)
                {
                    foreach (var s in spots)
                    {
                        if (used.Contains(s)) continue;
                        if (s.Small && !FitsDrawer(t)) continue;
                        if (pass == 0 && s.Tier == ItemSpawnTier.Common) continue;
                        if (pass < 2 && usedAreasForKeys.Contains(s.Area)) continue;
                        if (forb != null && pass < 2 && s.Area != null && MatchesAny(s.Area, forb)) continue;
                        if (req != null && pass < 2 && (s.Area == null || !s.Area.StartsWith(req))) continue;
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
                        if (s.Small && !FitsDrawer(t)) continue;
                        if (pass == 0 && s.Tier == ItemSpawnTier.Key) continue;
                        if (t == ItemType.SmallKey && s.Area != null && MatchesAny(s.Area, Forbidden(t))) continue;
                        // nothing an escape depends on lies past the chained gate / behind the shelter door
                        if (pass == 0 && s.Area != null && NoteTexts.LateArea(s.Area)) continue;
                        if (t == ItemType.Backpack && s.Area != null && NoteTexts.LateArea(s.Area)) continue;
                        pick = s; break;
                    }
                if (pick == null) continue; // (a big item finds no free spot that is not a drawer)
                used.Add(pick);
                float charge = t == ItemType.Flashlight ? rng.Range(0.35f, 0.9f) : 1f;
                result.Add(new Placement { Type = t, Position = pick.Position, Yaw = pick.Yaw + rng.Range(-30f, 30f), Charge = charge });
            }
            return result;
        }

        static ItemSpawnInfo SpotAt(List<ItemSpawnInfo> spots, Vector3 p)
        {
            foreach (var s in spots) if (s.Small && (s.Position - p).sqrMagnitude < 0.0004f) return s;
            return null;
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
