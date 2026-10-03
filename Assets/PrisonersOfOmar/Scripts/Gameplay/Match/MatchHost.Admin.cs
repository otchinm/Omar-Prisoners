using System.Collections.Generic;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 2) Admin panel commands, executed by the host for authenticated admins ("session" work package).
    public sealed partial class MatchHost
    {
        /// <summary>Msg.AdminCmdReq from <paramref name="sender"/>; ignored unless the sender is an authenticated admin.</summary>
        public void OnAdminCmdReq(int sender, NetReader r) { }

        // Hooks implemented by the packages that own the systems (partial methods: calls vanish while unimplemented).
        // "player" package (MatchHost.Player.cs): OpenAllDoors / CloseAllDoors / UnlockAllDoors.
        partial void AdminDoors(AdminCmd cmd);
        // "world" package: Omar commands (FreezeOmar handled through AdminState; Stun / teleports / scream / hunt / sleep / chop).
        partial void AdminOmar(AdminCmd cmd, int a, float f, Vector3 adminPos);
        // "world" package: grandmother commands (GrandmaRoam / GrandmaReturn / GrandmaKill / GrandmaScream / GrandmaDisable).
        partial void AdminGrandma(AdminCmd cmd);
    }
}
