using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // Drawers of dressers, nightstands, sideboards and desks: E slides one open (anything lying in it rides along), E again
    // pushes it shut. The host owns the state; everybody animates it.
    public sealed partial class MatchWorld
    {
        public DrawerEntity[] Drawers = new DrawerEntity[0];

        void BuildDrawers()
        {
            Drawers = new DrawerEntity[Map.Drawers.Count];
            for (int i = 0; i < Drawers.Length; i++) Drawers[i] = new DrawerEntity(i, Map.Drawers[i]);
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
    }

    public sealed class DrawerEntity : IInteractable
    {
        public readonly int Index;
        public readonly DrawerInfo Info;
        public bool Open;
        float _t, _target;
        readonly Vector3 _shut;

        public DrawerEntity(int index, DrawerInfo info)
        {
            Index = index; Info = info;
            _shut = info.Drawer != null ? info.Drawer.position : Vector3.zero;
            if (info.Interact != null) InteractableRef.Attach(info.Interact, this);
        }

        public Vector3 InteractPoint => Info.Interact != null ? Info.Interact.bounds.center : _shut;

        public bool GetPrompt(Interactor who, out InteractPrompt p)
        {
            p = default;
            if (who.IsOmar) return false;
            p = InteractPrompt.Press(Open ? "CLOSE THE DRAWER" : "OPEN THE DRAWER");
            return true;
        }

        public void Interact(Interactor who) => MatchWorld.Instance?.SendDrawer(Index, !Open);

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
