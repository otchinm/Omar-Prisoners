using System;
using PrisonersOfOmar.Net;
using UnityEngine;

namespace PrisonersOfOmar.Gameplay
{
    public enum LifeState : byte
    {
        Free = 0,
        Caged,
        Dead,
        Escaped,
        Gone, // disconnected
    }

    [Flags]
    public enum AvatarFlags : ushort
    {
        None = 0,
        Crouch = 1 << 0,
        Sprint = 1 << 1,
        LighterOn = 1 << 2,
        FlashlightOn = 1 << 3,
        Grounded = 1 << 4,
        Stunned = 1 << 5,
        Struggling = 1 << 6,
    }

    /// <summary>Replicated per-frame state of an avatar (owner -> host -> everyone).</summary>
    public struct AvatarNetState
    {
        public Vector3 Position;
        public float Yaw;
        public float Pitch;
        public AvatarFlags Flags;
        public ItemType Held;
        /// <summary>Incremented on teleports so remote peers snap instead of interpolating.</summary>
        public byte TeleportSeq;

        public void Write(NetWriter w)
        {
            w.WriteVector3(Position);
            w.WriteAngle(Yaw);
            w.WritePitch(Pitch);
            w.WriteUShort((ushort)Flags);
            w.WriteByte((byte)Held);
            w.WriteByte(TeleportSeq);
        }

        public static AvatarNetState Read(NetReader r)
        {
            var s = new AvatarNetState
            {
                Position = r.ReadVector3(),
                Yaw = r.ReadAngle(),
                Pitch = r.ReadPitch(),
                Flags = (AvatarFlags)r.ReadUShort(),
                Held = (ItemType)r.ReadByte(),
                TeleportSeq = r.ReadByte(),
            };
            if (float.IsNaN(s.Position.x) || float.IsNaN(s.Position.y) || float.IsNaN(s.Position.z)) s.Position = Vector3.zero;
            return s;
        }
    }

    /// <summary>Authoritative status of a participant (host -> all on change).</summary>
    public sealed class PlayerStatus
    {
        public int Id;
        public LifeState Life;
        public bool Injured;
        public int Captures;
        public int Cage = -1;
        public int HidingSpot = -1;
        public int TrappedBy = -1;
        public int CarSeat = -1;
        public EscapeRoute Route;
        /// <summary>Incremented when the owner must teleport (caged, pulled out of hiding...).</summary>
        public int TeleportSeq;

        public bool IsFree => Life == LifeState.Free;
        public bool Hidden => HidingSpot >= 0;
        public bool Trapped => TrappedBy >= 0;
        public bool InCar => CarSeat >= 0;

        public void Write(NetWriter w)
        {
            w.WriteByte((byte)Id);
            w.WriteByte((byte)Life);
            w.WriteBool(Injured);
            w.WriteByte((byte)Captures);
            w.WriteSByte((sbyte)Cage);
            w.WriteSByte((sbyte)HidingSpot);
            w.WriteShort((short)TrappedBy);
            w.WriteSByte((sbyte)CarSeat);
            w.WriteByte((byte)Route);
            w.WriteByte((byte)TeleportSeq);
        }

        public void Read(NetReader r)
        {
            Life = (LifeState)r.ReadByte();
            Injured = r.ReadBool();
            Captures = r.ReadByte();
            Cage = r.ReadSByte();
            HidingSpot = r.ReadSByte();
            TrappedBy = r.ReadShort();
            CarSeat = r.ReadSByte();
            Route = (EscapeRoute)r.ReadByte();
            TeleportSeq = r.ReadByte();
        }
    }

    /// <summary>What a use request targets.</summary>
    public enum UseTarget : byte
    {
        Self = 0,      // consume / apply item on yourself
        Door,          // id = door index
        Cage,          // id = cage index
        Gate,          // main gate padlock
        CarFuel,
        CarHood,
        CarDriver,     // start engine / drive
        CarPassenger,  // board
        CarExit,
        FuseBox,
        Radio,
        Barrels,       // pour lighter fuel
        Ignite,        // light the poured drums
        Trap,          // id = trap index (disarm / free a trapped friend)
        Hiding,        // id = spot (enter / leave)
    }

    public enum WorldEventKind : byte
    {
        CagesOpen = 0,
        OmarAwake,
        PowerOut,
        PowerOn,
        AnomalyPulse,
        PhoneRing,
        TvOn,
        Thunder,
        DistantScream,
        MannequinShuffle,
        RescueCalled,
        HelicopterArrive,
        HelicopterLeave,
        Explosion,
        IgniteStarted,
        RadioPowered,
        GateOpened,
        CarCrankFail,
        CarStarted,
        CarNoBattery,
        CarNoFuel,
        ShelterOpened,
        Whispers,
    }

    public enum EndingId : byte
    {
        None = 0,
        LongRoad,     // Road
        Headlights,   // Car
        Underground,  // Shelter
        Signal,       // Radio
        Ashes,        // Fire
        SecondClass,  // nobody escaped
        Dawn,         // time ran out
    }

    public enum TrapState : byte
    {
        Armed = 0,
        Triggered,
        Disarmed,
    }

    /// <summary>Objective progress (host -> all whenever something changes).</summary>
    public sealed class ObjectiveData
    {
        public bool GateCut;
        public bool CarFueled;
        public bool CarBatteryOk;
        public bool CarStarted;
        public int CarDriver = -1;
        public bool CarGone;
        public bool ShelterOpen;
        public bool FuseIn;
        public bool RadioCalled;
        public float RescueAt = -1f;
        public bool RescuePresent;
        public float RescueLeaveAt = -1f;
        public bool RescueGone;
        public bool BarrelsPoured;
        public float IgniteAt = -1f;
        public bool Exploded;

        public void Write(NetWriter w)
        {
            int bits = 0;
            if (GateCut) bits |= 1;
            if (CarFueled) bits |= 2;
            if (CarBatteryOk) bits |= 4;
            if (CarStarted) bits |= 8;
            if (CarGone) bits |= 16;
            if (ShelterOpen) bits |= 32;
            if (FuseIn) bits |= 64;
            if (RadioCalled) bits |= 128;
            if (RescuePresent) bits |= 256;
            if (RescueGone) bits |= 512;
            if (BarrelsPoured) bits |= 1024;
            if (Exploded) bits |= 2048;
            w.WriteInt(bits);
            w.WriteShort((short)CarDriver);
            w.WriteFloat(RescueAt);
            w.WriteFloat(RescueLeaveAt);
            w.WriteFloat(IgniteAt);
        }

        public void Read(NetReader r)
        {
            int b = r.ReadInt();
            GateCut = (b & 1) != 0; CarFueled = (b & 2) != 0; CarBatteryOk = (b & 4) != 0; CarStarted = (b & 8) != 0;
            CarGone = (b & 16) != 0; ShelterOpen = (b & 32) != 0; FuseIn = (b & 64) != 0; RadioCalled = (b & 128) != 0;
            RescuePresent = (b & 256) != 0; RescueGone = (b & 512) != 0; BarrelsPoured = (b & 1024) != 0; Exploded = (b & 2048) != 0;
            CarDriver = r.ReadShort();
            RescueAt = r.ReadFloat();
            RescueLeaveAt = r.ReadFloat();
            IgniteAt = r.ReadFloat();
        }
    }

    /// <summary>Tuning values in one place.</summary>
    public static class Tuning
    {
        // prisoners
        public const float WalkSpeed = 2.1f, RunSpeed = 4.6f, CrouchSpeed = 1.15f, InjuredSpeedMul = 0.82f;
        public const float StaminaSeconds = 6f, StaminaRegenDelay = 1.6f, StaminaRegenRate = 0.22f;
        public const float EyeHeight = 1.58f, CrouchEyeHeight = 0.95f;
        public const float InteractRange = 2.3f;
        public const float LighterBurnSeconds = 170f, FlashlightBurnSeconds = 260f;
        // Omar
        public const float OmarWalkSpeed = 2.0f, OmarRunSpeed = 4.75f, OmarStaminaSeconds = 9f;
        public const float OmarEyeHeight = 1.82f;
        public const float AttackRange = 1.9f, AttackCooldown = 1.25f, AttackWindup = 0.42f;
        public const float ScreamCooldown = 22f, SenseCooldown = 30f, SenseRadius = 16f;
        public const int TripwireCharges = 3, BearTrapCharges = 2;
        public const float TrapRecharge = 100f;
        public const float OmarIntroSeconds = 22f, CagesOpenAt = 6f;
        // detection
        public const float SightConeHalfAngle = 62f, SightMinRange = 4.5f, SightMaxRange = 32f;
        /// <summary>When no prisoner is free but some are caged, the night ends this many seconds later unless one breaks out.</summary>
        public const float AllCagedGrace = 30f;
        // capture
        public const int MaxCaptures = 3;
        // proximity interference
        public const float InterferenceRadius = 20f;
        // objectives
        public const float RescueDelay = 110f, RescueStay = 70f, IgniteFuse = 8f, ExplosionStunRadius = 16f;
    }
}
