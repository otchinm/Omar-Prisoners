using System.Collections.Generic;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // (iteration 2) Admin panel commands, executed by the host for authenticated admins (Admin checks the sender).
    public sealed partial class MatchHost
    {
        /// <summary>Msg.AdminCmdReq is decoded and authorised by <see cref="Admin"/>; this runs it in the match.</summary>
        public void OnAdminCmdReq(int sender, NetReader r) { }

        // Hooks implemented next to the systems they touch.
        partial void AdminDoors(AdminCmd cmd);
        partial void AdminOmar(AdminCmd cmd, int a, float f, Vector3 adminPos);
        partial void AdminGrandma(AdminCmd cmd);

        int OmarId { get { var o = S.FindOmar(); return o != null ? o.Id : -1; } }

        public void OnAdminCmd(int sender, AdminCmd cmd, int a, int b, float f, string s, Vector3 point, Vector3 normal)
        {
            Vector3 me = PosOf(sender);
            switch (cmd)
            {
                // ---------------------------------------------------------------- self
                case AdminCmd.GodMode: if (a != 0) AdminState.GodMode.Add(sender); else AdminState.GodMode.Remove(sender); Admin.HostLog(sender, a != 0 ? "GOD MODE ON" : "GOD MODE OFF"); break;
                case AdminCmd.Invisible: if (a != 0) AdminState.Invisible.Add(sender); else AdminState.Invisible.Remove(sender); Admin.HostLog(sender, a != 0 ? "INVISIBLE" : "VISIBLE"); break;
                case AdminCmd.HealSelf: AdminHeal(sender); break;

                // ---------------------------------------------------------------- players
                case AdminCmd.BringPlayer: AdminTeleport(a, me + RandomFlat(1.2f)); break;
                case AdminCmd.FreePlayer: AdminFree(a, me); break;
                case AdminCmd.CagePlayer: if (IsPrisoner(a) && W.StatusOf(a)?.Life == LifeState.Free) Capture(a); break;
                case AdminCmd.KillPlayer:
                    if (IsPrisoner(a))
                    {
                        var st = Edit(a);
                        // free whatever they occupied, like a disconnect does (hiding spot, cage, trap, the driver's seat)
                        if (st.HidingSpot >= 0) BroadcastHide(st.HidingSpot, -1, false);
                        if (st.Cage >= 0 && st.Cage < W.Cages.Length) BroadcastCage(st.Cage, W.Cages[st.Cage].Open, -1);
                        if (st.TrappedBy >= 0 && st.TrappedBy < W.Traps.Count) BroadcastTrap(W.Traps[st.TrappedBy], TrapState.Disarmed, -1, false);
                        if (W.Objectives.CarDriver == a) { var o = EditObj(); o.CarDriver = -1; CommitObj(o); }
                        st.Life = LifeState.Dead; st.HidingSpot = -1; st.Cage = -1; st.TrappedBy = -1; st.CarSeat = -1;
                        Commit(st);
                        SetChase(a, false);
                    }
                    break;
                case AdminCmd.InjurePlayer: if (IsPrisoner(a)) { var st = Edit(a); st.Injured = true; Commit(st); } break;
                case AdminCmd.HealPlayer: AdminHeal(a); break;
                case AdminCmd.GiveItemToPlayer: AdminGive(sender, a, (ItemType)b); break;

                // ---------------------------------------------------------------- items
                case AdminCmd.GiveItem: AdminGive(sender, sender, (ItemType)a); break;
                case AdminCmd.SpawnItemAtCrosshair: if (a > 0) SpawnItem((ItemType)a, point + normal * 0.02f, Random.Range(0f, 360f), 1f); break;
                case AdminCmd.RefillLights:
                    if (_inv.TryGetValue(sender, out var rs))
                        foreach (int id in rs) { var it = W.GetItem(id); if (it != null && it.Def.HasCharge) SetCharge(id, 1f); }
                    Admin.HostLog(sender, "REFILLED");
                    break;
                case AdminCmd.ClearInventory:
                    if (_inv.TryGetValue(sender, out var cs))
                        for (int i = 0; i < cs.Length; i++) if (cs[i] >= 0) DropItem(sender, cs[i], me + new Vector3(i * 0.3f - 0.6f, 0f, 0.5f), 0f, W.GetItem(cs[i])?.Charge ?? 1f);
                    break;

                // ---------------------------------------------------------------- Omar
                case AdminCmd.FreezeOmar:
                    AdminState.OmarFrozen = a != 0;
                    if (OmarId >= 0) { if (a != 0) Stun(OmarId, 3600f); else Unstun(); }
                    break;
                case AdminCmd.StunOmar: if (OmarId >= 0) Stun(OmarId, Mathf.Clamp(f <= 0 ? 10f : f, 1f, 600f)); break;
                case AdminCmd.OmarBlind: AdminState.OmarBlind = a != 0; break;
                case AdminCmd.OmarDeaf: AdminState.OmarDeaf = a != 0; break;
                case AdminCmd.TeleportOmarToMe: if (OmarId >= 0) AdminTeleport(OmarId, me + RandomFlat(2.5f)); break;
                case AdminCmd.TeleportOmarAway:
                    if (OmarId >= 0)
                    {
                        Vector3 away = W.Map.OmarSpawn.position;
                        if (W.Map.AreaBounds.TryGetValue("Basement.Furnace", out var fb) && W.Map.Nav != null)
                        {
                            var nodes = W.Map.Nav.NodesInArea("Basement.Furnace");
                            if (nodes.Count > 0) away = W.Map.Nav.Nodes[nodes[0]];
                        }
                        AdminTeleport(OmarId, away);
                    }
                    break;
                case AdminCmd.OmarScream: if (OmarId >= 0) DoScream(OmarId, true); break;
                case AdminCmd.OmarHuntPlayer: AdminState.OmarHuntTarget = a; foreach (var ai in _ais) if (ai != null) ai.AdminHunt(a); break;
                case AdminCmd.OmarSleep: AdminState.OmarSleepUntil = W.Time + Mathf.Clamp(f <= 0 ? 60f : f, 5f, 3600f); break;
                case AdminCmd.OmarChopMeat: foreach (var ai in _ais) if (ai != null) ai.AdminChop(); break;

                // ---------------------------------------------------------------- grandmother
                case AdminCmd.GrandmaRoam: case AdminCmd.GrandmaReturn: case AdminCmd.GrandmaKill: case AdminCmd.GrandmaScream:
                    AdminGrandma(cmd);
                    break;
                case AdminCmd.GrandmaDisable: AdminState.GrandmaDisabled = a != 0; break;

                // ---------------------------------------------------------------- world
                case AdminCmd.OpenAllDoors: case AdminCmd.CloseAllDoors: case AdminCmd.UnlockAllDoors: AdminDoors(cmd); break;
                case AdminCmd.OpenAllCages:
                    for (int i = 0; i < W.Cages.Length; i++)
                    {
                        if (!W.Cages[i].Active) continue;
                        int occ = W.Cages[i].Occupant;
                        BroadcastCage(i, true, -1);
                        if (occ >= 0) { var st = Edit(occ); if (st.Life == LifeState.Caged) { st.Life = LifeState.Free; st.Cage = -1; Commit(st); } }
                    }
                    break;
                case AdminCmd.DisarmAllTraps:
                    foreach (var t in W.Traps)
                    {
                        if (t.Victim >= 0) { var st = Edit(t.Victim); st.TrappedBy = -1; Commit(st); }
                        if (t.State != TrapState.Disarmed) BroadcastTrap(t, TrapState.Disarmed, -1, false);
                    }
                    break;
                case AdminCmd.SpawnTripwire:
                    {
                        var rig = (point - me); rig.y = 0f;
                        Vector3 side = rig.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, rig.normalized) : Vector3.right;
                        Vector3 c = point + Vector3.up * 0.12f;
                        Vector3 aa = c - side * 0.7f, bb = c + side * 0.7f;
                        if (Physics.Raycast(c, -side, out var hl, 1.8f, Layers.Solid, QueryTriggerInteraction.Ignore)) aa = hl.point + side * 0.03f;
                        if (Physics.Raycast(c, side, out var hr, 1.8f, Layers.Solid, QueryTriggerInteraction.Ignore)) bb = hr.point - side * 0.03f;
                        SpawnTrapDirect(TrapKind.Tripwire, aa, bb);
                        break;
                    }
                case AdminCmd.SpawnBearTrap: SpawnTrapDirect(TrapKind.BearTrap, point, point); break;
                case AdminCmd.PowerOff: Event(WorldEventKind.PowerOut); break;
                case AdminCmd.PowerOn: Event(WorldEventKind.PowerOn); break;
                case AdminCmd.TriggerAlarm:
                    foreach (var ai in _ais) if (ai != null) ai.OnAlarm(point);
                    DeliverNoise(point, 40f);
                    ScheduleScream(0.6f, true);
                    break;

                // ---------------------------------------------------------------- objectives
                case AdminCmd.ShowShelterCode:
                    Admin.HostLog(sender, "SHELTER CODE: " + W.ShelterCode[0] + W.ShelterCode[1] + W.ShelterCode[2] + W.ShelterCode[3]);
                    break;
                case AdminCmd.CutGate: { var o = EditObj(); o.GateCut = true; CommitObj(o); Event(WorldEventKind.GateOpened); break; }
                case AdminCmd.ReadyCar: { var o = EditObj(); o.CarFueled = true; o.CarBatteryOk = true; CommitObj(o); break; }
                case AdminCmd.InsertFuse: { var o = EditObj(); o.FuseIn = true; CommitObj(o); Event(WorldEventKind.RadioPowered); break; }
                case AdminCmd.CallRadio:
                    {
                        var o = EditObj();
                        if (!o.RadioCalled) { o.FuseIn = true; o.RadioCalled = true; o.RescueAt = W.Time + 60f; CommitObj(o); Event(WorldEventKind.RescueCalled); }
                        break;
                    }
                case AdminCmd.PrimeDrums: { var o = EditObj(); o.BarrelsPoured = true; CommitObj(o); break; }
                case AdminCmd.ExplodeDrums: if (!W.Objectives.Exploded) Explode(); break;
                case AdminCmd.OpenShelter: { var o = EditObj(); o.ShelterOpen = true; CommitObj(o); break; }
                // ---- iteration 3 puzzles
                case AdminCmd.ShowCodes:
                    if (W.CodeLocks.Count == 0) Admin.HostLog(sender, "NO CODE LOCKS");
                    foreach (var lk in W.CodeLocks)
                        Admin.HostLog(sender, (lk.Info.Label ?? lk.Info.Name) + " (" + NoteTexts.PlaceName(lk.Info.Area) + "): " + lk.Pretty()
                            + (lk.Open ? " - OPEN" : "") + (lk == W.TapeLock ? " - ON THE TAPE" : ""));
                    break;
                case AdminCmd.SolveSockets:
                    for (int i = 0; i < W.Sockets.Length; i++) ForceSolveSocket(i);
                    Admin.HostLog(sender, W.Sockets.Length + " SOCKET(S) DONE");
                    break;
                case AdminCmd.OpenCodeLocks:
                    for (int i = 0; i < W.CodeLocks.Count; i++) OpenCodeLockHost(i, sender);
                    break;
                case AdminCmd.UnlockDrawers:
                    foreach (var d in W.Drawers) if (d.Locked) UnlockDrawer(d, 4, 0f);
                    break;

                // ---------------------------------------------------------------- events / match
                case AdminCmd.TriggerEvent: Event((WorldEventKind)a); break;
                case AdminCmd.AddMinutes: W.AdminAddTime(Mathf.Clamp(f <= 0 ? 10f : f, 0.5f, 120f) * 60f); break;
                case AdminCmd.NearDawn: W.AdminSetTime(Mathf.Max(0f, W.NightLength - 60f)); break;
                case AdminCmd.PauseClock: AdminState.ClockPaused = a != 0; break;
                case AdminCmd.EndMatch: if (!_ended) EndMatch(false, (EndingId)a); break;
                case AdminCmd.ReturnToLobby: S.HostReturnToLobby(); break;
            }
        }

        static Vector3 RandomFlat(float r) { var v = Random.insideUnitCircle.normalized * r; return new Vector3(v.x, 0f, v.y); }

        void AdminHeal(int id)
        {
            if (!IsPrisoner(id)) return;
            var st = Edit(id);
            st.Injured = false;
            Commit(st);
        }

        /// <summary>Out of a cage / trap, back on their feet - even from the dead (brought next to the admin).</summary>
        void AdminFree(int id, Vector3 adminPos)
        {
            if (!IsPrisoner(id)) return;
            var st = Edit(id);
            bool revive = st.Life == LifeState.Dead || st.Life == LifeState.Escaped;
            if (st.Life == LifeState.Gone) return;
            if (st.Cage >= 0 && st.Cage < W.Cages.Length) BroadcastCage(st.Cage, true, -1);
            if (st.TrappedBy >= 0 && st.TrappedBy < W.Traps.Count) BroadcastTrap(W.Traps[st.TrappedBy], TrapState.Disarmed, -1, false);
            st.Life = LifeState.Free; st.Cage = -1; st.TrappedBy = -1; st.Injured = false;
            if (revive) { st.Captures = 0; st.TeleportSeq++; }
            Commit(st);
            if (revive) AdminTeleport(id, adminPos + RandomFlat(1.2f));
        }

        /// <summary>Move a player: the AI Omar directly, everybody else by asking their machine to move itself.</summary>
        void AdminTeleport(int id, Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 5f, Layers.Solid, QueryTriggerInteraction.Ignore)) p = hit.point;
            var av = W.AvatarOf(id);
            if (av != null && av.Simulated && id != S.LocalId)
            {
                var ai = av.GetComponent<OmarAI>();
                if (ai != null) { ai.TeleportTo(p); return; }
            }
            if (id == S.LocalId) { W.TeleportLocal(p); return; }
            var w = S.Begin(Msg.AdminTeleport);
            w.WriteVector3(p);
            S.SendTo(id, NetChannel.Reliable);
        }

        /// <summary>Create a brand-new item lying in the world (all peers). Returns its id.</summary>
        int SpawnItem(ItemType type, Vector3 pos, float yaw, float charge)
        {
            int id = W.Items.Count;
            var w = S.Begin(Msg.AdminItemSpawn);
            w.WriteShort((short)id);
            w.WriteByte((byte)type);
            w.WriteVector3(pos);
            w.WriteFloat(yaw);
            w.WriteUnit(charge);
            S.SendToAll(NetChannel.Reliable);
            return id;
        }

        void AdminGive(int admin, int player, ItemType type)
        {
            if (type == ItemType.None || !IsPrisoner(player)) { Admin.HostLog(admin, "ONLY PRISONERS CARRY ITEMS"); return; }
            if (!_inv.TryGetValue(player, out var slots)) return;
            int id = SpawnItem(type, PosOf(player) + Vector3.up * 0.05f, 0f, 1f);
            if (ItemDefs.IsWorn(type))
            {
                if (!Wear(player, id)) Admin.HostLog(admin, "ALREADY WEARS ONE - DROPPED AT THEIR FEET");
                return;
            }
            int slot = FreeSlot(player);
            if (slot < 0) { Admin.HostLog(admin, "POCKETS FULL - DROPPED AT THEIR FEET"); return; }
            slots[slot] = id;
            BroadcastPicked(id, player, slot);
        }

        /// <summary>A trap that doesn't cost Omar a charge (admin).</summary>
        void SpawnTrapDirect(TrapKind kind, Vector3 a, Vector3 b)
        {
            int id = W.Traps.Count;
            var w = S.Begin(Msg.TrapSpawned);
            w.WriteShort((short)id);
            w.WriteByte((byte)kind);
            w.WriteVector3(a);
            w.WriteVector3(b);
            S.SendToAll(NetChannel.Reliable);
        }

        /// <summary>End a stun / freeze early (admin).</summary>
        void Unstun()
        {
            _omarStunUntil = 0f;
            foreach (var ai in _ais) if (ai != null) ai.ClearStun();
            var w = S.Begin(Msg.OmarStun);
            w.WriteFloat(-1f);
            S.SendToAll(NetChannel.Reliable);
        }
    }
}
