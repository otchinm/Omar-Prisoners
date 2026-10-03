using System.Collections.Generic;
using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Net;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 2) Cage slots and avatar spawning, owned by the "session" work package.
    public sealed partial class MatchWorld
    {
        /// <summary>Creates the CageEntity for every cage slot of the map.</summary>
        void BuildCages()
        {
            Cages = new CageEntity[Map.Cages.Count];
            for (int i = 0; i < Cages.Length; i++) Cages[i] = new CageEntity(i, Map.Cages[i]);
        }

        void SpawnAvatars()
        {
            int prisonerIndex = 0;
            foreach (var pl in Session.Players)
            {
                if (pl.Role == PlayerRole.Spectator) continue;
                var st = new PlayerStatus { Id = pl.Id };
                Pose pose;
                if (pl.IsOmar) pose = Map.OmarSpawn;
                else
                {
                    int cage = Mathf.Min(prisonerIndex, Map.PrisonerSpawns.Count - 1);
                    pose = Map.PrisonerSpawns[cage];
                    if (cage < Cages.Length) { st.Life = LifeState.Caged; st.Cage = cage; Cages[cage].Apply(false, pl.Id); }
                    prisonerIndex++;
                }
                Statuses[pl.Id] = st;
                bool local = pl.Id == LocalId;
                bool simulated = local || (pl.IsBot && IsHost);
                var av = Avatar.Spawn(pl, local, simulated, _dynamicRoot, pose);
                Avatars[pl.Id] = av;
                av.ApplyStatus(st);
                if (local)
                {
                    LocalAvatar = av;
                    if (pl.IsOmar) LocalOmar = OmarController.Attach(av, this);
                    else LocalPrisoner = PrisonerController.Attach(av, this);
                }
                else if (pl.IsBot && IsHost)
                {
                    OmarAI.Attach(av, this);
                }
            }
            if (LocalAvatar == null)
            {
                Spectator = SpectatorCamera.Create(this);
            }
        }
    }
}
