using System.Collections.Generic;
using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Net;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 2) Revolver, grandmother, kitchen: client side, owned by the "world" work package.
    public sealed partial class MatchWorld
    {
        /// <summary>The local prisoner fired the revolver they hold (the host resolves the hit).</summary>
        public void SendShoot(Vector3 origin, Vector3 direction)
        {
            var w = Session.Begin(Msg.ShootReq);
            w.WriteVector3(origin);
            w.WriteVector3(direction.normalized);
            Session.SendToHost(NetChannel.Reliable);
        }
    }
}
