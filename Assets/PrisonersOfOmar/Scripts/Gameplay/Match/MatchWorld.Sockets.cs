using System.Collections.Generic;
using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 3) Item sockets, client side: the prompt ("PUT THE TAPE IN THE VCR"), the use request, and the items shown
    // in the socket as they go in. The host owns the count (MatchHost.Sockets.cs).
    public sealed partial class MatchWorld
    {
        public SocketEntity[] Sockets = new SocketEntity[0];

        /// <summary>A socket took an item (justSolved = it completed with this one). Feature presentation hooks in here.</summary>
        public event System.Action<SocketEntity, bool> SocketChanged;

        void BuildSockets()
        {
            Sockets = new SocketEntity[Map.Sockets.Count];
            for (int i = 0; i < Sockets.Length; i++) Sockets[i] = new SocketEntity(i, Map.Sockets[i]);
        }

        /// <summary>Index of the socket with that <see cref="SocketInfo.Name"/>, -1 if the map has none.</summary>
        public int SocketIndex(string name)
        {
            for (int i = 0; i < Sockets.Length; i++) if (Sockets[i].Info.Name == name) return i;
            return -1;
        }

        void OnSocketState(int sender, NetReader r)
        {
            int id = r.ReadShort();
            int count = r.ReadByte();
            bool solved = r.ReadBool();
            var type = (ItemType)r.ReadByte();
            int player = r.ReadByte();
            if (id < 0 || id >= Sockets.Length) return;
            var s = Sockets[id];
            bool fresh = solved && !s.Solved;
            s.Apply(count, solved, type, _dynamicRoot);
            if (player == LocalId && count > 0) AddMessage(fresh && s.Info.DoneText != null ? s.Info.DoneText : ItemDefs.Get(type).Name + " " + s.Info.Prep + " " + s.Info.Label, 3f);
            SocketChanged?.Invoke(s, fresh);
        }
    }

    /// <summary>(iteration 3) A world object that takes items (see <see cref="SocketInfo"/>).</summary>
    public sealed class SocketEntity : IInteractable
    {
        public readonly int Index;
        public readonly SocketInfo Info;
        public int Count;
        public bool Solved;
        readonly List<GameObject> _shown = new List<GameObject>();
        /// <summary>Feature hooks for a completed socket (the VCR: "WATCH THE TAPE"); null = <see cref="SocketInfo.DoneText"/>.</summary>
        public System.Func<Interactor, InteractPrompt?> SolvedPrompt;
        public System.Action<Interactor> SolvedInteract;

        public SocketEntity(int index, SocketInfo info)
        {
            Index = index; Info = info;
            if (info.Interact != null) InteractableRef.Attach(info.Interact, this);
        }

        public Vector3 InteractPoint => Info.Interact != null ? Info.Interact.bounds.center : Vector3.zero;

        public bool Accepts(ItemType t)
        {
            foreach (var a in Info.Accepts) if (a == t) return true;
            return false;
        }

        /// <summary>The item the local player would put in: the held one if it fits, else the first one that does.</summary>
        public int PickItem(Interactor who)
        {
            if (who.Inventory == null) return -1;
            var held = who.Inventory.Held;
            if (held != null && Accepts(held.Type)) return held.Id;
            foreach (var t in Info.Accepts) { int id = who.ItemId(t); if (id >= 0) return id; }
            return -1;
        }

        string Progress => Info.Needed > 1 ? " (" + Count + "/" + Info.Needed + ")" : "";

        public bool GetPrompt(Interactor who, out InteractPrompt p)
        {
            p = default;
            if (who.IsOmar) return false;
            if (Solved)
            {
                var sp = SolvedPrompt?.Invoke(who);
                if (sp.HasValue) { p = sp.Value; return true; }
                if (Info.DoneText == null) return false;
                p = InteractPrompt.Info(Info.DoneText);
                return true;
            }
            var it = MatchWorld.Instance?.GetItem(PickItem(who));
            if (it == null)
            {
                string needs = Info.Accepts.Length > 0 ? ItemDefs.Get(Info.Accepts[0]).Name : "SOMETHING";
                p = InteractPrompt.Info((Info.EmptyText ?? Info.Label + " - IT NEEDS " + needs) + Progress);
                return true;
            }
            string text = Info.Verb + " THE " + it.Def.Name + " " + Info.Prep + " " + Info.Label + Progress;
            p = Info.HoldTime > 0f ? InteractPrompt.Hold(text, Info.HoldTime, it.Type, Info.InsertNoise * 0.5f) : InteractPrompt.Press(text, it.Type);
            return true;
        }

        public void Interact(Interactor who)
        {
            if (Solved) { SolvedInteract?.Invoke(who); return; }
            int id = PickItem(who);
            if (id >= 0) MatchWorld.Instance?.SendUse(UseTarget.Socket, Index, id);
        }

        /// <summary>Host state arrived: show the newly inserted item(s), play the sounds.</summary>
        public void Apply(int count, bool solved, ItemType type, Transform parent)
        {
            bool added = count > Count;
            bool fresh = solved && !Solved;
            for (int i = _shown.Count; i < count && i < Info.SlotPoses.Length; i++)
            {
                try
                {
                    var t = type != ItemType.None ? type : (Info.Accepts.Length > 0 ? Info.Accepts[0] : ItemType.None);
                    var go = ItemMeshFactory.Build(t);
                    go.transform.SetParent(parent, false);
                    var pose = Info.SlotPoses[i];
                    go.transform.SetPositionAndRotation(pose.position, pose.rotation);
                    GeoUtil.SetLayerRecursive(go, Layers.World);
                    _shown.Add(go);
                }
                catch (System.Exception e) { Debug.LogException(e); }
            }
            Count = count;
            Solved = solved;
            if (added) AudioManager.Play3D(Info.InsertSound ?? Snd.ItemDrop, InteractPoint, 0.8f, Random.Range(0.92f, 1.06f), 1.5f, 14f);
            if (fresh) AudioManager.Play3D(Info.SolveSound ?? Snd.KeyUnlock, InteractPoint, 0.9f, 1f, 2f, 20f);
        }
    }
}
