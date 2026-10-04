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
        /// <summary>Peeking through a door that is open just a crack: Omar on the other side can not see them.</summary>
        Peek = 1 << 7,
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
        VentCover,     // take one screw out of the cage room vent cover (screwdriver)
        VentHatch,     // push the kitchen ceiling grate out from inside the duct
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
        /// <summary>Screws taken out of the cage room vent cover (4 = the cover is off).</summary>
        public int VentScrews;
        /// <summary>The kitchen ceiling grate has been pushed out (the duct is open into the kitchen).</summary>
        public bool VentHatchOpen;

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
            if (VentHatchOpen) bits |= 4096;
            w.WriteInt(bits);
            w.WriteShort((short)CarDriver);
            w.WriteFloat(RescueAt);
            w.WriteFloat(RescueLeaveAt);
            w.WriteFloat(IgniteAt);
            w.WriteByte((byte)VentScrews);
        }

        public void Read(NetReader r)
        {
            int b = r.ReadInt();
            GateCut = (b & 1) != 0; CarFueled = (b & 2) != 0; CarBatteryOk = (b & 4) != 0; CarStarted = (b & 8) != 0;
            CarGone = (b & 16) != 0; ShelterOpen = (b & 32) != 0; FuseIn = (b & 64) != 0; RadioCalled = (b & 128) != 0;
            RescuePresent = (b & 256) != 0; RescueGone = (b & 512) != 0; BarrelsPoured = (b & 1024) != 0; Exploded = (b & 2048) != 0;
            VentHatchOpen = (b & 4096) != 0;
            CarDriver = r.ReadShort();
            RescueAt = r.ReadFloat();
            RescueLeaveAt = r.ReadFloat();
            IgniteAt = r.ReadFloat();
            VentScrews = r.ReadByte();
        }
    }

    /// <summary>Tuning values in one place.</summary>
    public static class Tuning
    {
        // prisoners
        public const float WalkSpeed = 2.1f, RunSpeed = 4.6f, CrouchSpeed = 1.15f, InjuredSpeedMul = 0.82f;
        // prisoner stamina: set per difficulty by ApplyDifficulty (Nightmare = the original 7 s)
        public static float StaminaSeconds = 12.5f, StaminaRegenDelay = 0.9f, StaminaRegenRate = 0.26f;
        public const float EyeHeight = 1.58f, CrouchEyeHeight = 0.95f;
        public const float InteractRange = 2.3f;
        public static float LighterBurnSeconds = 170f, FlashlightBurnSeconds = 260f;
        // Omar (walk / run / captures / multipliers are set per difficulty by ApplyDifficulty)
        public static float OmarWalkSpeed = 1.45f, OmarRunSpeed = 4.75f;
        public const float OmarStaminaSeconds = 9f;
        /// <summary>m/s per second: the AI Omar accelerates from walk to full run over ~1.2 s.</summary>
        public const float OmarAcceleration = 3f;
        public const float OmarEyeHeight = 2.3f;
        /// <summary>How much lower Omar's head / eyes go when he fully stoops under a door frame (he is ~2.5 m tall).</summary>
        public const float OmarDuckDrop = 0.36f;
        // shorter reach and slower swings; after every swing he stands still for AttackRecover seconds
        public const float AttackRange = 1.65f, AttackCooldown = 2.6f, AttackWindup = 0.66f; // impact = 0.58 of the 1.15 s chop
        public const float AttackRecover = 1.5f;
        public const float ScreamCooldown = 22f, SenseCooldown = 30f, SenseRadius = 16f;
        public const int TripwireCharges = 3, BearTrapCharges = 2;
        public const float TrapRecharge = 100f;
        public const float OmarIntroSeconds = 22f, CagesOpenAt = 6f;
        // detection
        public const float SightConeHalfAngle = 62f, SightMinRange = 4.5f, SightMaxRange = 32f;
        /// <summary>When no prisoner is free but some are caged, the night ends this many seconds later unless one breaks out.</summary>
        public const float AllCagedGrace = 30f;
        // capture
        public static int MaxCaptures = 3;
        /// <summary>Chance per E press (after the first few) that the rusty cage lock gives way.</summary>
        public static float CageBreakChance = 0.035f;
        /// <summary>Chance per lock rattle (every 4 presses) that Omar notices when he is right there (falls to 0 at 36 m).</summary>
        public static float CageNoticeNear = 0.4f;
        /// <summary>For this long after the last rattle Omar may open the cage and kill the prisoner inside.</summary>
        public const float CagePunishWindow = 25f;

        // ---- difficulty (iteration 2, Docs/ITERATION2.md §4) ----
        public static float OmarSightMul = 1f, OmarHearingMul = 1f, DetectFillMul = 1f, SupplyMul = 1f;
        public static int StartWires = 5, StartBears = 3;
        public static float Brightness = 1.7f, GlowStrength = 1f;
        /// <summary>PsxEnvironment.GradeAmount: the olive / yellow tape look of the reference, only above Normal.</summary>
        public static float LightGrade = 0f;
        public static int GrandmaRoamProgress = 2;
        public static float GrandmaRoamNight = 0.5f;
        public static Difficulty CurrentDifficulty = Difficulty.Normal;

        /// <summary>Sets every difficulty-dependent value (identical on every peer: comes from the match settings).</summary>
        public static void ApplyDifficulty(Difficulty d)
        {
            CurrentDifficulty = d;
            float baseRun = 4.75f, lighter = 170f, flash = 260f;
            LightGrade = d == Difficulty.Hard ? 0.75f : d == Difficulty.Nightmare ? 0.9f : 0f;
            switch (d)
            {
                case Difficulty.Easy: StaminaSeconds = 15f; StaminaRegenDelay = 0.8f; StaminaRegenRate = 0.3f; CageBreakChance = 0.05f; CageNoticeNear = 0.25f; break;
                case Difficulty.Hard: StaminaSeconds = 10.5f; StaminaRegenDelay = 1.0f; StaminaRegenRate = 0.23f; CageBreakChance = 0.028f; CageNoticeNear = 0.5f; break;
                case Difficulty.Nightmare: StaminaSeconds = 7f; StaminaRegenDelay = 1.2f; StaminaRegenRate = 0.2f; CageBreakChance = 0.02f; CageNoticeNear = 0.6f; break;
                default: StaminaSeconds = 12.5f; StaminaRegenDelay = 0.9f; StaminaRegenRate = 0.26f; CageBreakChance = 0.035f; CageNoticeNear = 0.4f; break;
            }
            switch (d)
            {
                case Difficulty.Easy:
                    Brightness = 2.2f; OmarWalkSpeed = 1.25f; OmarRunSpeed = baseRun * 0.88f; OmarSightMul = 0.8f; OmarHearingMul = 0.7f;
                    DetectFillMul = 0.6f; MaxCaptures = 4; SupplyMul = 1.5f; LighterBurnSeconds = lighter / 0.6f; FlashlightBurnSeconds = flash / 0.6f;
                    StartWires = 3; StartBears = 1; GlowStrength = 1f; GrandmaRoamProgress = 4; GrandmaRoamNight = 0.7f;
                    break;
                case Difficulty.Hard:
                    Brightness = 1.3f; OmarWalkSpeed = 1.6f; OmarRunSpeed = baseRun * 1.06f; OmarSightMul = 1.15f; OmarHearingMul = 1.25f;
                    DetectFillMul = 1.3f; MaxCaptures = 2; SupplyMul = 0.75f; LighterBurnSeconds = lighter / 1.3f; FlashlightBurnSeconds = flash / 1.3f;
                    StartWires = 6; StartBears = 4; GlowStrength = 0.6f; GrandmaRoamProgress = 1; GrandmaRoamNight = 0.35f;
                    break;
                case Difficulty.Nightmare:
                    Brightness = 1f; OmarWalkSpeed = 1.75f; OmarRunSpeed = baseRun * 1.12f; OmarSightMul = 1.3f; OmarHearingMul = 1.5f;
                    DetectFillMul = 1.6f; MaxCaptures = 2; SupplyMul = 0.5f; LighterBurnSeconds = lighter / 1.6f; FlashlightBurnSeconds = flash / 1.6f;
                    StartWires = 7; StartBears = 5; GlowStrength = 0f; GrandmaRoamProgress = 1; GrandmaRoamNight = 0.2f;
                    break;
                default:
                    Brightness = 1.7f; OmarWalkSpeed = 1.45f; OmarRunSpeed = baseRun; OmarSightMul = 1f; OmarHearingMul = 1f;
                    DetectFillMul = 1f; MaxCaptures = 3; SupplyMul = 1f; LighterBurnSeconds = lighter; FlashlightBurnSeconds = flash;
                    StartWires = 5; StartBears = 3; GlowStrength = 1f; GrandmaRoamProgress = 2; GrandmaRoamNight = 0.5f;
                    break;
            }
        }
        // proximity interference
        public const float InterferenceRadius = 20f;
        // objectives
        public const float RescueDelay = 110f, RescueStay = 70f, IgniteFuse = 8f, ExplosionStunRadius = 16f;
    }
}
