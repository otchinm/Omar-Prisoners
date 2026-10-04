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

        /// <summary>Cage slot each prisoner started in (player id -> cage index); the host locks them back up there first.</summary>
        public readonly Dictionary<int, int> StartCages = new Dictionary<int, int>();

        /// <summary>
        /// One cage per prisoner, in random cell rooms (spread over different rooms while there are enough of them, a random
        /// free slot inside each). Solo play therefore shows exactly one cage. Deterministic from the match seed, so every
        /// client picks the same slots without a message.
        /// </summary>
        List<int> PickStartCages(int prisoners)
        {
            var result = new List<int>();
            if (Cages.Length == 0 || prisoners <= 0) return result;
            var rng = DeterministicRandom.For(Seed, "start-cages");
            var rooms = new List<List<int>>();
            foreach (var room in Map.CellRooms)
            {
                var slots = new List<int>();
                foreach (int c in room.CageIndices) if (c >= 0 && c < Cages.Length) slots.Add(c);
                if (slots.Count > 0) rooms.Add(slots);
            }
            if (rooms.Count == 0)
            {
                var all = new List<int>();
                for (int i = 0; i < Cages.Length; i++) all.Add(i);
                rooms.Add(all);
            }
            for (int i = rooms.Count - 1; i > 0; i--) { int j = rng.Range(0, i + 1); var t = rooms[i]; rooms[i] = rooms[j]; rooms[j] = t; }
            for (int p = 0; p < prisoners; p++)
            {
                int pick = -1;
                for (int k = 0; k < rooms.Count && pick < 0; k++)
                {
                    var slots = rooms[(p + k) % rooms.Count];
                    if (slots.Count == 0) continue;
                    int s = rng.Range(0, slots.Count);
                    pick = slots[s];
                    slots.RemoveAt(s);
                }
                if (pick < 0) break;
                result.Add(pick);
            }
            return result;
        }

        void SpawnAvatars()
        {
            int prisonerIndex = 0;
            int prisonerCount = 0;
            foreach (var pl in Session.Players) if (pl.Role != PlayerRole.Spectator && !pl.IsOmar) prisonerCount++;
            var startCages = PickStartCages(prisonerCount);
            StartCages.Clear();
            foreach (var pl in Session.Players)
            {
                if (pl.Role == PlayerRole.Spectator) continue;
                var st = new PlayerStatus { Id = pl.Id };
                Pose pose;
                if (pl.IsOmar) pose = Map.OmarSpawn;
                else
                {
                    if (prisonerIndex < startCages.Count)
                    {
                        int cage = startCages[prisonerIndex];
                        pose = Cages[cage].Info.Inside;
                        st.Life = LifeState.Caged; st.Cage = cage;
                        Cages[cage].Apply(false, pl.Id);
                        StartCages[pl.Id] = cage;
                    }
                    else pose = Map.PrisonerSpawns[Mathf.Min(prisonerIndex, Map.PrisonerSpawns.Count - 1)];
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
