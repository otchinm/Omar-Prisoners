using PrisonersOfOmar.Audio;
using PrisonersOfOmar.Characters;
using PrisonersOfOmar.Net;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    // Client-side handling of host events (runs on every peer, including the host).
    public sealed partial class MatchWorld
    {
        static readonly Msg[] HandledMessages =
        {
            Msg.Snapshot, Msg.AvatarStateReq, Msg.Action, Msg.ActionReq, Msg.PlayerStatus, Msg.ItemPicked, Msg.ItemDropped,
            Msg.ItemConsumed, Msg.ItemCharge, Msg.BottleShatter, Msg.CageState, Msg.TrapState,
            Msg.TrapSpawned, Msg.AttackFx, Msg.ChaseState, Msg.Scream, Msg.OmarStun, Msg.Noise, Msg.ObjectiveState,
            Msg.Escaped, Msg.KeypadResult, Msg.WorldEvent, Msg.MatchEnd, Msg.Message, Msg.CarDrive, Msg.TrapCharges,
            // requests (host)
            Msg.NoiseReq, Msg.PickupReq, Msg.DropReq, Msg.UseReq, Msg.ThrowReq, Msg.BottleImpact, Msg.StruggleReq,
            Msg.TrapTriggerReq, Msg.TrapPlaceReq, Msg.AttackReq, Msg.DetectReq, Msg.ScreamReq, Msg.SearchReq, Msg.KeypadReq, Msg.EscapeReq,
        };

        void RegisterHandlers()
        {
            var s = Session;
            s.On(Msg.Snapshot, OnSnapshot);
            s.On(Msg.Action, OnAction);
            s.On(Msg.PlayerStatus, OnPlayerStatus);
            s.On(Msg.ItemPicked, OnItemPicked);
            s.On(Msg.ItemDropped, OnItemDropped);
            s.On(Msg.ItemConsumed, OnItemConsumed);
            s.On(Msg.ItemCharge, OnItemCharge);
            s.On(Msg.BottleShatter, OnBottleShatter);
            s.On(Msg.CageState, OnCageState);
            s.On(Msg.TrapState, OnTrapState);
            s.On(Msg.TrapSpawned, OnTrapSpawned);
            s.On(Msg.AttackFx, OnAttackFx);
            s.On(Msg.ChaseState, OnChaseState);
            s.On(Msg.Scream, OnScream);
            s.On(Msg.OmarStun, OnOmarStun);
            s.On(Msg.Noise, OnNoise);
            s.On(Msg.ObjectiveState, OnObjectiveState);
            s.On(Msg.Escaped, OnEscaped);
            s.On(Msg.KeypadResult, OnKeypadResult);
            s.On(Msg.WorldEvent, OnWorldEvent);
            s.On(Msg.MatchEnd, OnMatchEnd);
            s.On(Msg.Message, OnMessage);
            s.On(Msg.CarDrive, OnCarDrive);
            s.On(Msg.TrapCharges, (id, r) => { int wires = r.ReadByte(), bears = r.ReadByte(); LocalOmar?.SetTrapCharges(wires, bears); });

            // host side requests are forwarded to MatchHost (ignored on clients)
            s.On(Msg.AvatarStateReq, (id, r) => Host?.OnAvatarState(id, r));
            s.On(Msg.ActionReq, (id, r) => Host?.OnActionReq(id, r));
            s.On(Msg.NoiseReq, (id, r) => Host?.OnNoiseReq(id, r));
            s.On(Msg.PickupReq, (id, r) => Host?.OnPickupReq(id, r));
            s.On(Msg.DropReq, (id, r) => Host?.OnDropReq(id, r));
            s.On(Msg.UseReq, (id, r) => Host?.OnUseReq(id, r));
            s.On(Msg.ThrowReq, (id, r) => Host?.OnThrowReq(id, r));
            s.On(Msg.BottleImpact, (id, r) => Host?.OnBottleImpact(id, r));
            s.On(Msg.StruggleReq, (id, r) => Host?.OnStruggleReq(id, r));
            s.On(Msg.TrapTriggerReq, (id, r) => Host?.OnTrapTriggerReq(id, r));
            s.On(Msg.TrapPlaceReq, (id, r) => Host?.OnTrapPlaceReq(id, r));
            s.On(Msg.AttackReq, (id, r) => Host?.OnAttackReq(id, r));
            s.On(Msg.DetectReq, (id, r) => Host?.OnDetectReq(id, r));
            s.On(Msg.ScreamReq, (id, r) => Host?.OnScreamReq(id, r));
            s.On(Msg.SearchReq, (id, r) => Host?.OnSearchReq(id, r));
            s.On(Msg.KeypadReq, (id, r) => Host?.OnKeypadReq(id, r));
            s.On(Msg.EscapeReq, (id, r) => Host?.OnEscapeReq(id, r));
            RegisterPlayerHandlers(s);
            RegisterWorldHandlers(s);
        }

        void UnregisterHandlers()
        {
            foreach (var m in HandledMessages) Session.Off(m);
            UnregisterPlayerHandlers(Session);
            UnregisterWorldHandlers(Session);
        }

        // ------------------------------------------------------------------ avatars

        void OnSnapshot(int sender, NetReader r)
        {
            float hostTime = r.ReadFloat();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadByte();
                var st = AvatarNetState.Read(r);
                if (id == LocalId) continue;
                var av = AvatarOf(id);
                if (av != null && !av.Simulated) av.PushRemoteState(st);
            }
            if (!IsHost && Running)
            {
                float target = hostTime + Session.GetRtt(0) * 0.5f;
                float diff = target - _clock;
                if (Mathf.Abs(diff) > 1.5f) _clock = target;
                else _clock += diff * 0.1f;
            }
        }

        System.Collections.IEnumerator SwingSoundLater(Avatar av, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (av != null) AudioManager.Play3D(AudioManager.Variant(Snd.CleaverSwing, 2), av.ChestPosition, 0.9f, Random.Range(0.9f, 1.05f), 2f, 18f, AudioCategory.Omar);
        }

        void OnAction(int sender, NetReader r)
        {
            int id = r.ReadByte();
            var a = (CharacterAction)r.ReadByte();
            if (id == LocalId) return;
            var av = AvatarOf(id);
            if (av == null) return;
            av.PlayAction(a);
            if (av.IsOmar && a == CharacterAction.Attack)
            {
                // a heavy grunt as he cocks the cleaver, the whoosh comes with the downswing
                AudioManager.Play3D(AudioManager.Variant(Snd.OmarWindup, 2), av.EyePosition, 1f, Random.Range(0.92f, 1.04f), 2.5f, 24f, AudioCategory.Omar);
                StartCoroutine(SwingSoundLater(av, 0.45f));
            }
        }

        void OnPlayerStatus(int sender, NetReader r)
        {
            int id = r.ReadByte();
            var st = StatusOf(id);
            if (st == null) { st = new PlayerStatus { Id = id }; Statuses[id] = st; }
            var before = new PlayerStatus();
            before.Life = st.Life; before.Injured = st.Injured; before.Cage = st.Cage; before.HidingSpot = st.HidingSpot; before.TrappedBy = st.TrappedBy; before.CarSeat = st.CarSeat; before.Captures = st.Captures;
            st.Read(r);
            var av = AvatarOf(id);
            if (av != null) av.ApplyStatus(st);

            if (id == LocalId)
            {
                if (LocalPrisoner != null) LocalPrisoner.OnStatusChanged(before, st);
                if (st.Life == LifeState.Escaped || st.Life == LifeState.Dead)
                {
                    if (Spectator == null && Ending == null) Spectator = SpectatorCamera.Create(this);
                }
            }
            else if (av != null)
            {
                if (!before.Injured && st.Injured && st.Life == LifeState.Free)
                    AudioManager.Play3D(AudioManager.Variant(Snd.Hurt, 3), av.ChestPosition, 0.9f, Random.Range(0.9f, 1.1f), 2f, 25f);
                if (before.Life == LifeState.Free && st.Life == LifeState.Caged)
                    AddMessage(Session.Find(id)?.Name + " WAS DRAGGED BACK TO THE PENS", 4f);
                if (before.Life == LifeState.Caged && st.Life == LifeState.Free)
                    AddMessage(Session.Find(id)?.Name + " IS OUT OF THE CAGE", 3f);
                if (st.Life == LifeState.Dead && before.Life != LifeState.Dead)
                    AddMessage(Session.Find(id)?.Name + " IS GONE", 5f);
            }
        }

        // ------------------------------------------------------------------ items

        void OnItemPicked(int sender, NetReader r)
        {
            int itemId = r.ReadShort();
            int player = r.ReadByte();
            int slot = r.ReadByte();
            var it = GetItem(itemId);
            if (it == null) return;
            Vector3 pos = it.World != null ? it.World.transform.position : Vector3.zero;
            if (it.World != null) { Destroy(it.World.gameObject); it.World = null; }
            it.Holder = player; it.Slot = slot;
            if (player == LocalId)
            {
                Inventory.Set(slot, itemId);
                Inventory.Select(slot);
                AudioManager.Play2D(Snd.ItemPickup, 0.8f);
                AddMessage("TOOK " + it.Def.Name, 2.5f);
                VhsEffect.TriggerGlitch(0.15f, 0.12f);
            }
            else AudioManager.Play3D(Snd.ItemPickup, pos, 0.5f, 1f, 1f, 8f);
        }

        void OnItemDropped(int sender, NetReader r)
        {
            int itemId = r.ReadShort();
            Vector3 pos = r.ReadVector3();
            float yaw = r.ReadAngle();
            float charge = r.ReadUnit();
            var it = GetItem(itemId);
            if (it == null) return;
            if (it.Holder == LocalId) Inventory.Remove(itemId);
            it.Holder = -1; it.Slot = -1; it.Charge = charge; it.Consumed = false;
            if (it.World != null) Destroy(it.World.gameObject);
            WorldItem.Create(it, pos, yaw, _dynamicRoot);
            AudioManager.Play3D(Snd.ItemDrop, pos, 0.7f, Random.Range(0.9f, 1.1f), 1f, 12f);
        }

        void OnItemConsumed(int sender, NetReader r)
        {
            int itemId = r.ReadShort();
            int by = r.ReadByte();
            byte how = r.ReadByte(); // 0 used, 1 broke, 2 thrown
            Vector3 from = Vector3.zero, vel = Vector3.zero;
            if (how == 2) { from = r.ReadVector3(); vel = r.ReadVector3(); }
            var it = GetItem(itemId);
            if (it == null) return;
            bool wasLocal = it.Holder == LocalId;
            if (wasLocal) Inventory.Remove(itemId);
            it.Consumed = true; it.Holder = -1;
            if (it.World != null) { Destroy(it.World.gameObject); it.World = null; }
            if (how == 2 && by != LocalId) ThrownBottle.Spawn(from, vel, -1, _dynamicRoot);
            if (by == LocalId && LocalPrisoner != null) LocalPrisoner.OnItemConsumed(it, how);
        }

        void OnItemCharge(int sender, NetReader r)
        {
            int itemId = r.ReadShort();
            float c = r.ReadUnit();
            var it = GetItem(itemId);
            if (it != null) it.Charge = c;
        }

        void OnBottleShatter(int sender, NetReader r)
        {
            Vector3 p = r.ReadVector3();
            AudioManager.Play3D(Snd.GlassBreak, p, 1f, Random.Range(0.9f, 1.1f), 2f, 35f);
            try { PsxFx.GlassShatter(p); } catch { }
        }

        // ------------------------------------------------------------------ world objects

        void OnCageState(int sender, NetReader r)
        {
            int cage = r.ReadByte();
            bool open = r.ReadBool();
            int occ = r.ReadByte(); if (occ == 255) occ = -1;
            if (cage < 0 || cage >= Cages.Length) return;
            Cages[cage].Apply(open, occ);
        }

        void OnTrapState(int sender, NetReader r)
        {
            int id = r.ReadShort();
            var state = (TrapState)r.ReadByte();
            int victim = r.ReadByte(); if (victim == 255) victim = -1;
            bool fired = r.ReadBool();
            if (id < 0 || id >= Traps.Count) return;
            var t = Traps[id];
            t.Apply(state, victim);
            if (!fired) return;
            Vector3 p = t.InteractPoint + Vector3.up * 0.3f;
            if (t.Kind == TrapKind.Tripwire)
            {
                AudioManager.Play3D(Snd.TripwireSnap, p, 0.9f, 1f, 1.5f, 15f);
                // the siren: loud and far reaching (Omar's alarm)
                Chase.PlayAlarm(p);
                if (LocalIsOmar) Pings.Add(new OmarPing { Position = p, Expire = UnityEngine.Time.time + 10f, Kind = 1 });
                if (victim == LocalId) { VhsEffect.TriggerGlitch(0.8f, 0.4f); AddMessage("A WIRE! THE SIREN!", 3f); }
            }
            else
            {
                AudioManager.Play3D(Snd.TrapSnap, p, 1f, 1f, 2f, 30f);
                if (victim >= 0)
                {
                    var av = AvatarOf(victim);
                    if (av != null) try { PsxFx.BloodBurst(av.Position + Vector3.up * 0.2f, Vector3.up, 0.8f); } catch { }
                }
                if (LocalIsOmar) Pings.Add(new OmarPing { Position = p, Expire = UnityEngine.Time.time + 12f, Kind = 2 });
                if (victim == LocalId)
                {
                    AudioManager.Play2D(Snd.StingJumpscare, 0.8f, 1f, AudioCategory.Stinger);
                    VhsEffect.TriggerGlitch(1f, 0.5f);
                    AddMessage("A BEAR TRAP! MASH E TO PULL FREE", 4f);
                }
            }
        }

        void OnTrapSpawned(int sender, NetReader r)
        {
            int id = r.ReadShort();
            var kind = (TrapKind)r.ReadByte();
            Vector3 a = r.ReadVector3(), b = r.ReadVector3();
            if (id != Traps.Count) return; // out of order (should not happen on a reliable channel)
            Traps.Add(new TrapEntity(id, kind, a, b, _dynamicRoot));
            AudioManager.Play3D(Snd.TrapPlace, a, 0.6f, 1f, 1f, 8f, AudioCategory.Omar);
        }

        // ------------------------------------------------------------------ Omar

        void OnAttackFx(int sender, NetReader r)
        {
            int omar = r.ReadByte();
            int target = r.ReadByte();
            byte result = r.ReadByte(); // 0 miss, 1 injured, 2 captured, 3 crowbar save
            var oa = AvatarOf(omar);
            if (result == 0) return;
            var ta = AvatarOf(target);
            Vector3 hp = ta != null ? ta.ChestPosition : (oa != null ? oa.ChestPosition : Vector3.zero);
            if (result == 3)
            {
                AudioManager.Play3D(Snd.CrowbarPry, hp, 1f, 0.8f, 2f, 25f);
                try { PsxFx.Sparks(hp, Vector3.up); } catch { }
                if (target == LocalId) AddMessage("YOU SMASHED HIM WITH THE CROWBAR! RUN!", 4f);
                return;
            }
            AudioManager.Play3D(AudioManager.Variant(Snd.CleaverHit, 2), hp, 1f, Random.Range(0.9f, 1.05f), 2f, 25f, AudioCategory.Omar);
            try { PsxFx.BloodBurst(hp, oa != null ? (hp - oa.ChestPosition).normalized : Vector3.up, result == 2 ? 1.5f : 1f); } catch { }
            if (target == LocalId && LocalPrisoner != null) LocalPrisoner.OnHit(result == 2);
        }

        void OnChaseState(int sender, NetReader r)
        {
            int target = r.ReadByte();
            bool active = r.ReadBool();
            if (active) ChaseTargets.Add(target); else ChaseTargets.Remove(target);
            Chase.SetChase(target, active);
            if (active && LocalIsOmar)
            {
                var av = AvatarOf(target);
                if (av != null) Pings.Add(new OmarPing { Position = av.Position, Expire = UnityEngine.Time.time + 2f, Kind = 4 });
                // chases the host starts itself (pulled out of a hiding spot) must also end 4 s after losing sight
                if (LocalOmar != null && LocalOmar.Detection != null && Ending == null)
                    LocalOmar.Detection.ForceSpot(target, av != null ? av.Position : Vector3.zero, (id, spotted) => SendDetect(id, spotted));
            }
        }

        void OnScream(int sender, NetReader r)
        {
            int omar = r.ReadByte();
            int index = r.ReadByte();
            var oa = AvatarOf(omar);
            if (oa != null && omar != LocalId) oa.PlayAction(CharacterAction.Scream);
            Chase.PlayScream(oa, index);
            AnomalySystem.Pulse(0.35f, 2.5f);
            if (LocalAvatar != null && oa != null && !LocalIsOmar && Vector3.Distance(oa.Position, LocalAvatar.Position) < 26f)
            {
                VhsEffect.TriggerGlitch(0.3f, 0.35f);
                LocalPrisoner?.Terrify();
            }
        }

        void OnOmarStun(int sender, NetReader r)
        {
            float seconds = r.ReadFloat();
            if (LocalOmar != null) LocalOmar.Stun(seconds);
            var oa = OmarAvatar;
            if (oa != null)
            {
                oa.PlayAction(CharacterAction.Stunned);
                if (LocalOmar == null) AudioManager.Play3D(Snd.OmarStunned, oa.ChestPosition, 1f, Random.Range(0.9f, 1.02f), 3f, 40f, AudioCategory.Omar);
            }
        }

        void OnNoise(int sender, NetReader r)
        {
            Vector3 p = r.ReadVector3();
            float radius = r.ReadByte();
            if (!LocalIsOmar || LocalAvatar == null) return;
            if (Vector3.Distance(LocalAvatar.Position, p) > radius * 1.4f) return;
            Pings.Add(new OmarPing { Position = p, Expire = UnityEngine.Time.time + 2.2f, Kind = 0, Radius = radius });
        }

        // ------------------------------------------------------------------ objectives

        void OnObjectiveState(int sender, NetReader r)
        {
            var prev = new ObjectiveData();
            CopyObjectives(Objectives, prev);
            Objectives.Read(r);
            var o = Objectives;

            if (!prev.GateCut && o.GateCut && Map.MainGate != null)
            {
                Vector3 p = Map.MainGate.Interact != null ? Map.MainGate.Interact.bounds.center : Vector3.zero;
                AudioManager.Play3D(Snd.BoltCut, p, 1f, 1f, 2f, 40f);
                AudioManager.Play3D(Snd.ChainDrop, p, 0.9f, 1f, 2f, 30f);
                AudioManager.Play3D(Snd.GateCreak, p, 0.9f, 1f, 2f, 40f);
                if (Map.MainGate.Padlock != null) Map.MainGate.Padlock.SetActive(false);
                foreach (var c in Map.MainGate.Blockers) if (c != null) c.enabled = false;
                StartCoroutine(SwingGate());
                AddMessage(LocalIsOmar ? "THE MAIN GATE IS OPEN!" : "THE GATE IS OPEN - RUN DOWN THE ROAD!", 5f);
            }
            if (!prev.CarFueled && o.CarFueled && Map.Car != null && Map.Car.FuelCap != null)
                AudioManager.Play3D(Snd.GasPour, Map.Car.FuelCap.bounds.center, 0.4f, 1.2f, 1f, 10f);
            if (!prev.CarStarted && o.CarStarted && Map.Car != null && Map.Car.Root != null)
            {
                AudioManager.Play3D(Snd.CarStart, Map.Car.Root.position, 1f, 1f, 3f, 70f);
                _carIdle = AudioManager.Loop3D(Snd.CarIdleLoop, Map.Car.Root.position, 0.8f, 45f, AudioCategory.Sfx, Map.Car.Root, 1f);
                foreach (var h in Map.Car.Headlights)
                    if (h != null) { var l = PsxLight.CreateSpot(h, Vector3.zero, Quaternion.identity, new Color(1f, 0.95f, 0.75f), 2.4f, 26f, 55f); l.Priority = 6; }
                AddMessage("THE ENGINE IS RUNNING! GET IN THE CAR!", 5f);
            }
            if (!prev.ShelterOpen && o.ShelterOpen && Map.Shelter != null)
            {
                Vector3 p = Map.Shelter.DoorPivot != null ? Map.Shelter.DoorPivot.position : Vector3.zero;
                AudioManager.Play3D(Snd.ShelterDoorOpen, p, 1f, 1f, 3f, 30f);
                if (Map.Shelter.DoorCollider != null) Map.Shelter.DoorCollider.enabled = false;
                StartCoroutine(SwingShelter());
                AddMessage("THE SHELTER DOOR GROANS OPEN", 4f);
            }
            if (!prev.FuseIn && o.FuseIn)
            {
                foreach (var l in Map.RadioRoomLights) if (l != null) l.On = true;
                if (Map.Radio != null && Map.Radio.FuseBox != null) AudioManager.Play3D(Snd.PowerOn, Map.Radio.FuseBox.bounds.center, 1f, 1f, 2f, 25f);
                if (Map.Radio != null && Map.Radio.RadioSet != null) _radioLoop = AudioManager.Loop3D(Snd.RadioStaticLoop, Map.Radio.RadioSet.bounds.center, 0.5f, 10f, AudioCategory.Sfx, null, 2f);
                AddMessage("SOMEWHERE UPSTAIRS, A RADIO CRACKLES TO LIFE", 4f);
            }
            if (!prev.RadioCalled && o.RadioCalled && Map.Radio != null && Map.Radio.RadioSet != null)
            {
                AudioManager.Play3D(Snd.RadioVoice, Map.Radio.RadioSet.bounds.center, 1f, 1f, 2f, 20f);
                AddMessage(LocalIsOmar ? "SOMEONE USED YOUR RADIO. THEY CALLED FOR HELP." : "\"...WE HEAR YOU. HOLD ON. LAND IN THE CORN FIELD CLEARING...\"", 6f);
            }
            if (!prev.RescuePresent && o.RescuePresent) SpawnHelicopter();
            if (prev.RescuePresent && !o.RescuePresent) RemoveHelicopter();
            if (!prev.BarrelsPoured && o.BarrelsPoured && Map.FuelDepot != null && Map.FuelDepot.Barrels != null)
                AudioManager.Play3D(Snd.FuelPour, Map.FuelDepot.Barrels.bounds.center, 0.8f, 1f, 1.5f, 10f);
            if (prev.IgniteAt < 0 && o.IgniteAt > 0 && Map.FuelDepot != null)
            {
                AudioManager.Play3D(Snd.FireWhoosh, Map.FuelDepot.ExplosionCenter, 1f, 1f, 2f, 30f);
                try { _fireFx = PsxFx.CreateFire(_dynamicRoot, Map.FuelDepot.ExplosionCenter, 0.6f); } catch { }
                AddMessage("THE FUEL IS BURNING - GET AWAY FROM THE DRUMS!", 4f);
            }
            if (!prev.Exploded && o.Exploded) Explode();
        }

        static void CopyObjectives(ObjectiveData a, ObjectiveData b)
        {
            b.GateCut = a.GateCut; b.CarFueled = a.CarFueled; b.CarBatteryOk = a.CarBatteryOk; b.CarStarted = a.CarStarted;
            b.CarDriver = a.CarDriver; b.CarGone = a.CarGone; b.ShelterOpen = a.ShelterOpen; b.FuseIn = a.FuseIn;
            b.RadioCalled = a.RadioCalled; b.RescueAt = a.RescueAt; b.RescuePresent = a.RescuePresent; b.RescueLeaveAt = a.RescueLeaveAt;
            b.RescueGone = a.RescueGone; b.BarrelsPoured = a.BarrelsPoured; b.IgniteAt = a.IgniteAt; b.Exploded = a.Exploded;
        }

        System.Collections.IEnumerator SwingGate()
        {
            var g = Map.MainGate;
            Quaternion l0 = g.LeftLeaf != null ? g.LeftLeaf.localRotation : Quaternion.identity;
            Quaternion r0 = g.RightLeaf != null ? g.RightLeaf.localRotation : Quaternion.identity;
            for (float t = 0; t < 1f; t += UnityEngine.Time.deltaTime / 2.5f)
            {
                float e = Mathf.SmoothStep(0, 1, t);
                if (g.LeftLeaf != null) g.LeftLeaf.localRotation = l0 * Quaternion.Euler(0, g.LeftOpenAngle * e, 0);
                if (g.RightLeaf != null) g.RightLeaf.localRotation = r0 * Quaternion.Euler(0, g.RightOpenAngle * e, 0);
                yield return null;
            }
        }

        System.Collections.IEnumerator SwingShelter()
        {
            var s = Map.Shelter;
            if (s.DoorPivot == null) yield break;
            Quaternion q0 = s.DoorPivot.localRotation;
            for (float t = 0; t < 1f; t += UnityEngine.Time.deltaTime / 3.5f)
            {
                s.DoorPivot.localRotation = q0 * Quaternion.Euler(0, s.OpenAngle * Mathf.SmoothStep(0, 1, t), 0);
                yield return null;
            }
        }

        void SpawnHelicopter()
        {
            if (Map.Radio == null) return;
            Vector3 hp = Map.Radio.HelicopterPosition;
            _helicopter = HelicopterVisual.Build(_dynamicRoot, hp);
            _heliLight = PsxLight.CreateSpot(_helicopter.transform, Vector3.down * 0.8f, Quaternion.Euler(90, 0, 0), new Color(0.95f, 0.97f, 1f), 3f, 40f, 30f, PsxFlicker.None, "Searchlight");
            _heliLight.Priority = 7;
            _heliLoop = AudioManager.Loop3D(Snd.HelicopterLoop, hp, 1f, 160f, AudioCategory.Sfx, _helicopter.transform, 3f);
            try { PsxFx.CreateFlare(_dynamicRoot, Map.Radio.LandingZone.center - Vector3.up * (Map.Radio.LandingZone.extents.y - 0.1f)); } catch { }
            AddMessage(LocalIsOmar ? "A HELICOPTER IS LANDING IN THE CORN FIELD!" : "THE HELICOPTER IS HERE! GET TO THE CLEARING IN THE CORN!", 6f);
        }

        void RemoveHelicopter()
        {
            AudioManager.Stop(_heliLoop, 3f);
            if (_helicopter != null) Destroy(_helicopter, 0.1f);
            if (Map.Radio != null) AudioManager.Play3D(Snd.HelicopterFlyby, Map.Radio.HelicopterPosition, 1f, 1f, 5f, 160f);
            AddMessage("THE HELICOPTER IS LEAVING...", 4f);
        }

        void Explode()
        {
            var f = Map.FuelDepot;
            if (f == null) return;
            AudioManager.Play3D(Snd.Explosion, f.ExplosionCenter, 1f, 1f, 8f, 250f, AudioCategory.Stinger);
            AudioManager.Play3D(Snd.FenceBreach, f.ExplosionCenter, 1f, 1f, 4f, 60f);
            try { PsxFx.Explosion(f.ExplosionCenter, 2f); } catch { }
            try { PsxFx.LightFlash(f.ExplosionCenter, new Color(1f, 0.6f, 0.25f), 6f, 60f, 1.2f); } catch { }
            if (f.BreachFence != null) f.BreachFence.SetActive(false);
            foreach (var c in f.BreachBlockers) if (c != null) c.enabled = false;
            if (f.BarrelsRoot != null) f.BarrelsRoot.gameObject.SetActive(false);
            if (_fireFx == null) try { _fireFx = PsxFx.CreateFire(_dynamicRoot, f.ExplosionCenter, 1.5f); } catch { }
            AnomalySystem.Pulse(0.8f, 3f);
            if (LocalAvatar != null)
            {
                float d = Vector3.Distance(LocalAvatar.Position, f.ExplosionCenter);
                float k = Mathf.Clamp01(1f - d / 60f);
                VhsEffect.TriggerGlitch(k, 1f);
                if (d < 30f) { AudioManager.SetDistortion(0.6f * k); Invoke(nameof(ClearDistortion), 2.5f); }
            }
            AddMessage(LocalIsOmar ? "THE FUEL DEPOT EXPLODED. THE NORTH FENCE IS GONE." : "THE FENCE IS GONE! RUN THROUGH THE FIRE!", 6f);
        }

        void ClearDistortion() => AudioManager.SetDistortion(0f);

        void OnEscaped(int sender, NetReader r)
        {
            int id = r.ReadByte();
            var route = (EscapeRoute)r.ReadByte();
            var name = Session.Find(id)?.Name ?? "SOMEONE";
            if (id == LocalId)
            {
                AudioManager.Play2D(Snd.StingEscape, 1f, 1f, AudioCategory.Stinger);
                AddMessage("YOU ESCAPED - " + Endings.RouteName(route), 6f);
                VhsEffect.TriggerGlitch(0.5f, 0.6f);
            }
            else AddMessage(name + " ESCAPED", 4f);
        }

        void OnKeypadResult(int sender, NetReader r)
        {
            bool ok = r.ReadBool();
            var kp = UI.UIManager.Instance != null ? UI.UIManager.Instance.Find<UI.KeypadScreen>() : null;
            kp?.Result(ok);
        }

        void OnMessage(int sender, NetReader r)
        {
            string text = r.ReadString();
            float sec = r.ReadFloat();
            AddMessage(text, sec);
        }

        void OnCarDrive(int sender, NetReader r)
        {
            float t = r.ReadFloat();
            _carDriveStart = t;
            if (Map.Car != null && Map.Car.Root != null)
                AudioManager.Play3D(Snd.CarDriveAway, Map.Car.Root.position, 1f, 1f, 4f, 90f, AudioCategory.Sfx, Map.Car.Root);
        }

        void OnMatchEnd(int sender, NetReader r)
        {
            var res = EndingResult.Read(r);
            ShowEnding(res);
        }
    }
}
