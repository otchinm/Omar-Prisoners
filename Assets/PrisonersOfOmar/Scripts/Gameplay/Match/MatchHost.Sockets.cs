using PrisonersOfOmar.Map;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 3) Item sockets, host side: a prisoner puts an accepted item in (it is consumed and shown in the socket);
    // the Needed-th item completes it, which runs its PuzzleResult and any feature hook keyed on its name.
    public sealed partial class MatchHost
    {
        void UseSocket(int p, int id, int itemId)
        {
            if (id < 0 || id >= W.Sockets.Length) return;
            var s = W.Sockets[id];
            if (s.Solved || s.Count >= s.Info.Needed || !Near(p, s.InteractPoint, 3.2f)) return;
            var it = W.GetItem(itemId);
            if (it == null || !s.Accepts(it.Type) || !Holds(p, itemId, it.Type)) return;
            Consume(p, itemId);
            int count = s.Count + 1;
            bool solved = count >= s.Info.Needed;
            BroadcastSocket(id, count, solved, it.Type, p);
            DeliverNoise(s.InteractPoint, solved ? Mathf.Max(s.Info.SolveNoise, s.Info.InsertNoise) : s.Info.InsertNoise);
            if (solved) SocketSolved(id, p);
        }

        void BroadcastSocket(int id, int count, bool solved, ItemType type, int player)
        {
            var w = S.Begin(Msg.SocketState);
            w.WriteShort((short)id);
            w.WriteByte((byte)Mathf.Clamp(count, 0, 255));
            w.WriteBool(solved);
            w.WriteByte((byte)type);
            w.WriteByte((byte)Mathf.Clamp(player, 0, 255));
            S.SendToAll(NetChannel.Reliable);
        }

        /// <summary>Completes a socket outright (admin): fills it with its first accepted item type.</summary>
        void ForceSolveSocket(int id)
        {
            if (id < 0 || id >= W.Sockets.Length || W.Sockets[id].Solved) return;
            var s = W.Sockets[id];
            BroadcastSocket(id, s.Info.Needed, true, s.Info.Accepts.Length > 0 ? s.Info.Accepts[0] : ItemType.None, 255);
            DeliverNoise(s.InteractPoint, s.Info.SolveNoise);
            SocketSolved(id, -1);
        }

        /// <summary>The socket is complete: its generic result, then whatever the feature that owns it does.</summary>
        void SocketSolved(int id, int player)
        {
            var info = W.Sockets[id].Info;
            ApplyPuzzleResult(info.Result, info.ResultDoor, info.ResultItem, info.ResultPose);
            OnSocketSolvedFeature(info.Name, id, player);
        }

        /// <summary>The generic part of a solved puzzle (socket complete / code lock opened).</summary>
        void ApplyPuzzleResult(PuzzleResult result, int door, ItemType item, Pose pose)
        {
            switch (result)
            {
                case PuzzleResult.UnlockDoor:
                    if (door >= 0 && door < W.Doors.Length)
                    {
                        var d = W.Doors[door];
                        if (d.Locked) BroadcastDoor(d, false, d.Boarded, d.Angle, 0f);
                    }
                    break;
                case PuzzleResult.DropItem:
                    if (item != ItemType.None) SpawnItem(item, pose.position, pose.rotation.eulerAngles.y, 1f);
                    break;
            }
        }

        /// <summary>Feature results keyed on the socket name (iteration 3 puzzles add their cases here).</summary>
        void OnSocketSolvedFeature(string name, int id, int player)
        {
            switch (name)
            {
                default: break;
            }
        }
    }
}
