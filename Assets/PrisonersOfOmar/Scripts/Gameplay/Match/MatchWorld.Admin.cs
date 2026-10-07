using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 2) Admin panel: client side of the match (teleports, spawned items).
    public sealed partial class MatchWorld
    {
        void RegisterAdminHandlers(NetSession s)
        {
            s.On(Msg.AdminTeleport, OnAdminTeleport);
            s.On(Msg.AdminItemSpawn, OnAdminItemSpawn);
        }

        void UnregisterAdminHandlers(NetSession s)
        {
            s.Off(Msg.AdminTeleport);
            s.Off(Msg.AdminItemSpawn);
        }

        /// <summary>Move the local character (admin teleport). Owner-authoritative movement: the host follows.</summary>
        public void TeleportLocal(Vector3 p, float? yaw = null)
        {
            if (LocalPrisoner != null) LocalPrisoner.AdminTeleport(p, yaw);
            else if (LocalOmar != null) LocalOmar.AdminTeleport(p, yaw);
        }

        void OnAdminTeleport(int sender, NetReader r)
        {
            Vector3 p = r.ReadVector3();
            TeleportLocal(p);
            AddMessage("SOMETHING DRAGGED YOU AWAY...", 2.5f);
        }

        void OnAdminItemSpawn(int sender, NetReader r)
        {
            int id = r.ReadShort();
            var type = (ItemType)r.ReadByte();
            Vector3 pos = r.ReadVector3();
            float yaw = r.ReadFloat();
            float charge = r.ReadUnit();
            if (id != Items.Count) { Debug.LogWarning("[Admin] item id mismatch " + id + " vs " + Items.Count); if (id < Items.Count) return; }
            while (Items.Count < id) Items.Add(new ItemEntity { Id = Items.Count, Type = ItemType.None, Consumed = true });
            var e = new ItemEntity { Id = id, Type = type, Charge = charge };
            Items.Add(e);
            try { WorldItem.Create(e, pos, yaw, _dynamicRoot); } catch (System.Exception ex) { Debug.LogException(ex); }
        }
    }
}
