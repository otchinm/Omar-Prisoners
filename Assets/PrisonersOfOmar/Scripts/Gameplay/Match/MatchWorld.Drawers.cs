using System.Collections.Generic;
using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // Drawers of dressers, nightstands, sideboards and desks: E slides one open (anything lying in it rides along), E again
    // pushes it shut. The host owns the state; everybody animates it.
    // (iteration 3) A few drawers in the house are padlocked (chosen from the seed): a small key or a lockpick opens a key
    // padlock, a combination padlock needs its 3 digits (hint notes), and a crowbar pries either open - loudly.
    public sealed partial class MatchWorld
    {
        public DrawerEntity[] Drawers = new DrawerEntity[0];

        void BuildDrawers()
        {
            Drawers = new DrawerEntity[Map.Drawers.Count];
            for (int i = 0; i < Drawers.Length; i++) Drawers[i] = new DrawerEntity(i, Map.Drawers[i]);
        }

        /// <summary>Padlocks: key-locked drawers, then combination-locked ones, all in the house, at least 1.5 m apart and only
        /// drawers deep enough to hold an item (so there is something behind every lock). Same on every peer.</summary>
        void BuildDrawerLocks()
        {
            var rng = DeterministicRandom.For(Seed, "drawerlocks");
            var cands = new List<int>();
            for (int i = 0; i < Drawers.Length; i++)
            {
                var info = Drawers[i].Info;
                if (info.Drawer == null || info.Interact == null) continue;
                string area = Map.AreaAt(info.ItemPoint);
                if (!area.StartsWith("House.") || ItemSpotAt(info.ItemPoint) == null) continue;
                cands.Add(i);
            }
            rng.Shuffle(cands);
            int keyLocks = cands.Count >= 10 ? 3 : cands.Count >= 5 ? 2 : cands.Count >= 2 ? 1 : 0;
            int codeLocks = cands.Count >= 8 ? 2 : cands.Count >= 3 ? 1 : 0;
            var picked = new List<int>();
            foreach (int i in cands)
            {
                if (picked.Count >= keyLocks + codeLocks) break;
                bool near = false;
                foreach (int j in picked) if (Vector3.Distance(Drawers[i].Info.ItemPoint, Drawers[j].Info.ItemPoint) < 1.5f) near = true;
                if (near) continue;
                picked.Add(i);
            }
            for (int k = 0; k < picked.Count; k++)
            {
                var d = Drawers[picked[k]];
                if (k < keyLocks) { d.SetLock(DrawerLockKind.Key, null); continue; }
                var info = new CodeLockInfo
                {
                    Name = "Drawer" + d.Index, Area = Map.AreaAt(d.Info.ItemPoint), Kind = CodeKind.Digits, Length = 3, Label = "COMBINATION LOCK",
                    Interact = d.Info.Interact, WrongNoise = 2f, OpenNoise = 3f,
                };
                d.SetLock(DrawerLockKind.Code, AddCodeLock(info, CodeLockEntity.RandomCode(CodeKind.Digits, 3, 0, rng), true));
            }
        }

        ItemSpawnInfo ItemSpotAt(Vector3 p)
        {
            foreach (var s in Map.ItemSpawns) if (s.Small && (s.Position - p).sqrMagnitude < 0.0004f) return s;
            return null;
        }

        /// <summary>Item points of the padlocked drawers (the spawner puts loot there, and never a small key).</summary>
        void LockedDrawerSpots(out List<Vector3> keySpots, out List<Vector3> codeSpots)
        {
            keySpots = new List<Vector3>();
            codeSpots = new List<Vector3>();
            foreach (var d in Drawers)
            {
                if (d.Lock == DrawerLockKind.Key) keySpots.Add(d.Info.ItemPoint);
                else if (d.Lock == DrawerLockKind.Code) codeSpots.Add(d.Info.ItemPoint);
            }
        }

        public DrawerEntity DrawerWithCodeLock(int codeLock)
        {
            foreach (var d in Drawers) if (d.CodeLock != null && d.CodeLock.Index == codeLock) return d;
            return null;
        }

        float _drawerContentsAt;

        void TickDrawers(float dt)
        {
            for (int i = 0; i < Drawers.Length; i++) Drawers[i].Tick(dt, Items);
            if (UnityEngine.Time.time < _drawerContentsAt) return;
            _drawerContentsAt = UnityEngine.Time.time + 0.4f;
            for (int i = 0; i < Drawers.Length; i++) Drawers[i].UpdateContents(Items);
        }

        public void SendDrawer(int drawer, bool open)
        {
            var w = Session.Begin(Msg.DrawerReq);
            w.WriteShort((short)drawer);
            w.WriteBool(open);
            Session.SendToHost(NetChannel.Reliable);
        }

        void OnDrawerState(int sender, NetReader r)
        {
            int id = r.ReadShort();
            bool open = r.ReadBool();
            if (id >= 0 && id < Drawers.Length) Drawers[id].Apply(open);
        }

        void OnDrawerLock(int sender, NetReader r)
        {
            int id = r.ReadShort();
            int how = r.ReadByte();
            if (id >= 0 && id < Drawers.Length) Drawers[id].Unlock(how);
        }
    }

    public enum DrawerLockKind : byte { None = 0, Key, Code }

    public sealed class DrawerEntity : IInteractable
    {
        public readonly int Index;
        public readonly DrawerInfo Info;
        public bool Open;
        /// <summary>(iteration 3) Padlocked: can't be pulled out until unlocked.</summary>
        public bool Locked;
        public DrawerLockKind Lock;
        public CodeLockEntity CodeLock;
        GameObject _padlock;
        float _t, _target;
        readonly Vector3 _shut;

        public DrawerEntity(int index, DrawerInfo info)
        {
            Index = index; Info = info;
            _shut = info.Drawer != null ? info.Drawer.position : Vector3.zero;
            if (info.Interact != null) InteractableRef.Attach(info.Interact, this);
        }

        public Vector3 InteractPoint => Info.Interact != null ? Info.Interact.bounds.center : _shut;

        /// <summary>Unit direction the drawer slides out (towards whoever opens it).</summary>
        Vector3 OutDir => Info.OpenOffset.sqrMagnitude > 1e-6f ? Info.OpenOffset.normalized : Vector3.forward;

        /// <summary>Padlocks it (built with the match, the same drawers on every peer) and hangs the lock on the front.</summary>
        public void SetLock(DrawerLockKind kind, CodeLockEntity codeLock)
        {
            Lock = kind;
            CodeLock = codeLock;
            Locked = kind != DrawerLockKind.None;
            if (!Locked || Info.Drawer == null || Info.Interact == null) return;
            try
            {
                _padlock = ItemMeshFactory.BuildPadlock(kind == DrawerLockKind.Code);
                var b = Info.Interact.bounds;
                Vector3 front = b.center + OutDir * 0.012f + Vector3.up * Mathf.Min(0.03f, b.extents.y * 0.4f);
                _padlock.transform.SetParent(Info.Drawer, true);
                _padlock.transform.SetPositionAndRotation(front, Quaternion.LookRotation(OutDir, Vector3.up));
                GeoUtil.SetLayerRecursive(_padlock, Layers.World);
            }
            catch (System.Exception e) { Debug.LogException(e); }
        }

        public bool GetPrompt(Interactor who, out InteractPrompt p)
        {
            p = default;
            if (who.IsOmar) return false;
            if (!Locked)
            {
                p = InteractPrompt.Press(Open ? "CLOSE THE DRAWER" : "OPEN THE DRAWER");
                return true;
            }
            // a crowbar in hand always means "pry it" (loud); otherwise the lock decides
            if (who.Held == ItemType.Crowbar) { p = InteractPrompt.Hold("PRY THE DRAWER OPEN", 2.5f, ItemType.Crowbar, 14f); return true; }
            if (Lock == DrawerLockKind.Code)
            {
                p = InteractPrompt.Press("A COMBINATION PADLOCK - TRY A CODE");
                return true;
            }
            if (who.Has(ItemType.SmallKey)) { p = InteractPrompt.Hold("UNLOCK IT WITH THE SMALL KEY", 0.8f, ItemType.SmallKey); return true; }
            if (who.Has(ItemType.Lockpick)) { p = InteractPrompt.Hold("PICK THE PADLOCK", 3f, ItemType.Lockpick, 2f); return true; }
            p = InteractPrompt.Info(who.Has(ItemType.Crowbar) ? "PADLOCKED. (HOLD THE CROWBAR TO PRY IT)" : "PADLOCKED. A SMALL KEYHOLE...");
            return true;
        }

        public void Interact(Interactor who)
        {
            var w = MatchWorld.Instance;
            if (w == null) return;
            if (!Locked) { w.SendDrawer(Index, !Open); return; }
            if (who.Held == ItemType.Crowbar) { w.SendUse(UseTarget.DrawerLock, Index, who.ItemId(ItemType.Crowbar)); return; }
            if (Lock == DrawerLockKind.Code) { if (CodeLock != null) w.OpenCodeLock(CodeLock); return; }
            int key = who.ItemId(ItemType.SmallKey);
            w.SendUse(UseTarget.DrawerLock, Index, key >= 0 ? key : who.ItemId(ItemType.Lockpick));
        }

        /// <summary>Host: unlocked (how: 0 small key, 1 lockpick, 2 code, 3 pried with a crowbar, 4 admin).</summary>
        public void Unlock(int how)
        {
            if (!Locked) return;
            Locked = false;
            if (_padlock != null) Object.Destroy(_padlock);
            _padlock = null;
            string snd = how == 1 ? Snd.Lockpick : how == 3 ? Snd.WoodBreak : Snd.KeyUnlock;
            AudioManager.Play3D(snd, InteractPoint, how == 3 ? 1f : 0.7f, Random.Range(0.94f, 1.06f), 1.5f, how == 3 ? 25f : 10f);
        }

        public void Apply(bool open)
        {
            if (open == Open) return;
            Open = open;
            _target = open ? 1f : 0f;
            AudioManager.Play3D(open ? Snd.DrawerOpen : Snd.DrawerClose, InteractPoint, 0.6f, Random.Range(0.94f, 1.06f), 1f, 9f);
        }

        /// <summary>Items in a shut drawer can't be picked up through the top and don't glow.</summary>
        public void UpdateContents(System.Collections.Generic.List<ItemEntity> items)
        {
            if (Info.Drawer == null) return;
            bool reachable = _t > 0.5f;
            var inside = Info.InsideLocal;
            inside.Expand(0.02f);
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (!it.InWorld || !inside.Contains(Info.Drawer.InverseTransformPoint(it.World.transform.position))) continue;
                var col = it.World.GetComponent<Collider>();
                if (col != null) col.enabled = reachable;
                if (it.World.Glow != null) it.World.Glow.Suppressed = !reachable;
            }
        }

        /// <summary>Slides the drawer; items lying inside move with it.</summary>
        public void Tick(float dt, System.Collections.Generic.List<ItemEntity> items)
        {
            if (Info.Drawer == null || Mathf.Approximately(_t, _target)) return;
            _t = Mathf.MoveTowards(_t, _target, dt / 0.32f);
            float e = _target > 0.5f ? 1f - (1f - _t) * (1f - _t) : _t * _t;   // fast start, soft stop when pulled out
            Vector3 next = _shut + Info.OpenOffset * e;
            Vector3 delta = next - Info.Drawer.position;
            if (delta.sqrMagnitude < 1e-10f) return;
            var inside = Info.InsideLocal;
            inside.Expand(0.02f);
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (!it.InWorld) continue;
                var tr = it.World.transform;
                if (inside.Contains(Info.Drawer.InverseTransformPoint(tr.position))) tr.position += delta;
            }
            Info.Drawer.position = next;
        }
    }
}
