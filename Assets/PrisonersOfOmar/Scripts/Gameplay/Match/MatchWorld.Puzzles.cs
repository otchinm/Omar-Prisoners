using System;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 3, Docs/ITERATION3_PLAN.md) Puzzle foundation, client side: item sockets, code locks, locked containers,
    // the notes journal and VHS tapes. This file wires them into the match (build / handlers / tick); each one lives in
    // its own MatchWorld.<Feature>.cs next to its MatchHost.<Feature>.cs.
    public sealed partial class MatchWorld
    {
        void BuildPuzzles()
        {
            try { BuildSockets(); } catch (Exception e) { Debug.LogException(e); }
            try { BuildCodeLocks(); } catch (Exception e) { Debug.LogException(e); }
            try { BuildDrawerLocks(); } catch (Exception e) { Debug.LogException(e); }
            try { BuildTapes(); } catch (Exception e) { Debug.LogException(e); }
        }

        void RegisterPuzzleHandlers(NetSession s)
        {
            s.On(Msg.SocketState, OnSocketState);
            s.On(Msg.CodeReq, (id, r) => Host?.OnCodeReq(id, r));
            s.On(Msg.CodeResult, OnCodeResult);
            s.On(Msg.CodeLockState, OnCodeLockState);
            s.On(Msg.DrawerLock, OnDrawerLock);
        }

        void UnregisterPuzzleHandlers(NetSession s)
        {
            s.Off(Msg.SocketState);
            s.Off(Msg.CodeReq);
            s.Off(Msg.CodeResult);
            s.Off(Msg.CodeLockState);
            s.Off(Msg.DrawerLock);
        }

        void TickPuzzles(float dt)
        {
            TickTapes(dt);
        }
    }
}
