using System;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Map
{
    // =====================================================================================================
    //  THE BASE OF THE SECOND CLASS - final layout (meters; +X east, +Z north, Y up; exterior ground y = 0)
    //  The map root must sit at the world origin (all MapData positions are world positions).
    //
    //  FENCE  chain-link 2.6 m + 3 barbed strands, x[-75,50] z[-45,85]. Openings:
    //         MAIN GATE x[-3,3] @ z=-45 (2 leaves, hinges x=±3, chain+padlock at x=0, opens inward/north)
    //         VEHICLE GATE x[-60,-52] @ z=-45 (2 leaves, chained, smashed outward/south by the car)
    //         BREACH segment x[15,27] @ z=85 is a separate object (FireInfo.BreachFence)
    //         forest billboards 3..30 m beyond the fence, ground to ~±190 m, treeline rings r=165/182 around (-12,20)
    //         invisible boundary walls x=-110 / x=95 / z=-85 / z=125
    //  HOUSE  walls on x=±11, z=±8 (ext 0.2 m, int 0.14 m). Basement floor -2.6 / ceiling 0.4; ground floor 0.6 / 3.6;
    //         upper floor 3.8 / 6.6; attic slab 6.6-6.8; gable roof ridge along X at y=10, chimney (-7,3).
    //         The exterior ground colliders leave the footprint x[-11.1,11.1] z[-8.1,8.1] open (basement air space).
    //    GROUND  hall x[-3,3] (front door (0,-8), back door (0,8)); stairs UP x[1.6,2.93] z=-2 (y0.6) -> z=5 (y3.8);
    //            stairs DOWN x[-2.93,-1.6] z=5 (y0.6) -> z=-2 (y-2.6), solid underneath.
    //            dining x[-11,-3] z[-8,0] door (-3,-5) | kitchen/butchery x[-11,-3] z[0,8] doors (-3,6.5) (-7,0) side (-11,4)
    //            living x[3,11] z[-8,-1] door (3,-5) | bathroom x[3,11] z[-1,3] BOARDED door (7,-1)
    //            clock bedroom x[3,11] z[3,8] door (3,6.5) | portrait of Omar on the hall west wall (-2.93,2.25,-6.9)
    //    UPPER   hall + stairwell railing | cage room x[-11,-3] z[-8,2] door (-3,-5), 4 cages x[-10.8,-8.8] z=-6.55/-4.15/-1.75/0.65
    //            storage x[-11,-3] z[2,8] door (-3,6.5) | radio room x[3,11] z[-8,-1] door (3,-5) LOCKED (lockpick)
    //            closet x[3,11] z[-1,3] door from the study (7,3) | study x[3,11] z[3,8] door (3,6.5)
    //    BASEMENT corridor x[-3,3] | generator x[3,11] z[-8,0] door (3,-5), fuse box (5.5,-1.1,-0.07)
    //            furnace x[3,11] z[0,8] door (3,6) - OMAR SPAWN (6.2,-2.6,4.4) | antechamber x[-11,-3] z[-8,0] door (-3,-6),
    //            SHELTER door (-11,-4) + keypad (-10.9,-1.25,-5.15) | exhibit x[-11,-3] z[0,8] door (-3,6.5), 6 burnt mannequins
    //    TUNNEL  floor -2.6, ceiling -0.3, 2.2 wide: x -11..-27 @ z=-4, north to z=6 @ x=-26, west to x=-40, chamber
    //            x[-44,-40] z[3.5,8.5] with the ladder; TunnelExitZone x[-44,-42.4] z[4.9,7.1]
    //  PORCH x[-6,6] z[-11,-8.1] deck 0.6, steps x[-1.2,1.2] to z=-12.2 | back stoop z[8.1,9.6] steps to z=10.8 |
    //        side stoop x[-12.6,-11.1] steps to x=-13.8
    //  YARD  gravel driveway x[-2,2] from the steps to the road, 15 flamingos, big tree (-15,-20), floodlight (9,-15),
    //        pickup wreck (-10,-30), concrete barriers at the gate (x=±2, z=-42.6), utility pole lines
    //  SHED  x[14,21] z[-4,2], door (14,-1) | RESTROOM x[-66,-58] z[2,8], steel door (-62,2)
    //  PARKING LOT x[-70,-40] z[-32,-2], sodium lamp (-43.2,-15), THE CAR at (-52,-20) facing south, 3 wrecks
    //  ROAD  asphalt x[-150,150] z[-62.5,-55.5] (GateInfo.ExitZone), road sign (6.2,-53.8)
    //  NORTH dirt path back door -> barn; corn field x[-45,5] z[22,62], lanes z[29,31.5], x[-26.25,-23.75], z[47,49.5];
    //        clearing x[-33,-17] z[40,56] = RadioInfo.LandingZone (helicopter at y=18)
    //        barn x[12,30] z[52,68] (open south door 5 m, BOARDED north door (21,68)); fuel drums (21,82.6);
    //        silo r=4 h=14 at (40,58) door west; windmill (-8,74) rotor at y=12.45; red water tower (38,18) tank y=12..16.5
    //
    //  Doors (index = network id): 0 FrontDoor 1 BackDoor 2 Dining 3 Kitchen 4 KitchenDining 5 KitchenSide 6 Living
    //  7 Bathroom(boarded) 8 ClockBedroom 9 CageRoom 10 Storage 11 RadioRoom(locked) 12 Study 13 SupplyCloset
    //  14 Generator 15 FurnaceRoom 16 Antechamber 17 Exhibit 18 Shed 19 Restroom 20 BarnNorth(boarded) 21 Silo.
    //  The shelter door, cage doors and gates are NOT in Doors (see ShelterInfo / CageInfo / GateInfo / CarInfo).
    // =====================================================================================================

    /// <summary>
    /// Builds "the Base of the Second Class" (the only map) at runtime. Deterministic for a given seed.
    /// Also sets the environment (fog / ambient / sky) for the match. Never throws: every section is guarded.
    /// </summary>
    public static class MapBuilder
    {
        /// <summary>Diagnostics of the last build (mesh sizes, nav statistics, counts). For debug overlays / logs.</summary>
        public static readonly System.Collections.Generic.List<string> LastBuildLog = new System.Collections.Generic.List<string>();

        public static MapData Build(int seed, Transform parent)
        {
            LastBuildLog.Clear();
            var root = new GameObject("Map_BaseOfTheSecondClass").transform;
            root.SetParent(parent, false);
            if (parent != null && (parent.position != Vector3.zero || parent.rotation != Quaternion.identity))
                Debug.LogWarning("[MapBuilder] parent is not at the origin; MapData positions assume an identity map root.");
            var data = new MapData { Root = root, Seed = seed, Nav = new NavGraph() };
            data.MainGate = new GateInfo();
            data.Car = new CarInfo();
            data.Shelter = new ShelterInfo();
            data.Radio = new RadioInfo();
            data.FuelDepot = new FireInfo();
            data.OmarSpawn = new Pose(new Vector3(6.2f, HouseBuilder.FB, 4.4f), Quaternion.LookRotation(Vector3.left));
            data.PlayableBounds = MapMath.MinMax(new Vector3(ExteriorBuilder.BoundX0, -4f, ExteriorBuilder.BoundZ0), new Vector3(ExteriorBuilder.BoundX1, 30f, ExteriorBuilder.BoundZ1));
            MapContext ctx = null;
            try { ctx = new MapContext(data, seed); }
            catch (Exception e) { Debug.LogError("[MapBuilder] context failed: " + e); return data; }

            Section(ctx, "environment", () => Environment(ctx));
            Section(ctx, "house", () => HouseBuilder.Build(ctx));
            Section(ctx, "exterior", () => ExteriorBuilder.Build(ctx));
            Section(ctx, "farm", () => FarmBuilder.Build(ctx));
            Section(ctx, "zones", () => Zones(ctx));
            Section(ctx, "nav grid", () => ExteriorBuilder.NavGrid(ctx));
            Section(ctx, "nav", () => data.Nav = NavBuilder.Build(ctx) ?? data.Nav);
            Section(ctx, "validate", () => Validate(ctx));
            LastBuildLog.AddRange(ctx.BuildLog);
            return data;
        }

        static void Section(MapContext ctx, string name, Action a)
        {
            try { a(); }
            catch (Exception e) { Debug.LogError("[MapBuilder] section '" + name + "' failed: " + e); }
            finally
            {
                try { ctx.Flush(); }
                catch (Exception e) { Debug.LogError("[MapBuilder] flush after '" + name + "' failed: " + e); }
            }
        }

        // ------------------------------------------------------------------ environment

        static void Environment(MapContext ctx)
        {
            PsxEnvironment.Set(new Color(0.056f, 0.057f, 0.062f), new Color(0.026f, 0.027f, 0.029f), 4f, 45f); // gray, bleak
            try
            {
                var sky = PsxSky.Create("Textures/Sky/sky_night", new Color(0.55f, 0.6f, 0.7f));
                if (sky != null) sky.transform.SetParent(ctx.Root, true);
            }
            catch (Exception e) { Debug.LogError("[MapBuilder] sky failed: " + e); }
            // a very weak moon fill (not switchable)
            var moon = ctx.Light(new Vector3(70f, 230f, 60f), new Color(0.36f, 0.37f, 0.41f), 0.22f, 650f, PsxFlicker.None, LightGroup.None, "MoonFill");
            moon.Priority = -1;
            ctx.Data.AmbientZones.Add(new AmbientZoneInfo { Type = AmbientType.Exterior, Bounds = MapMath.MinMax(new Vector3(-220f, -10f, -200f), new Vector3(200f, 120f, 230f)) });
            ctx.Ambient(AmbientType.House, new Vector3(-11.2f, HouseBuilder.FG - 0.1f, -8.2f), new Vector3(11.2f, HouseBuilder.Ridge, 8.2f));
        }

        // ------------------------------------------------------------------ zones, cameras, markers

        static void Zones(MapContext ctx)
        {
            var d = ctx.Data;
            ctx.Anomaly("CageRoom", new Vector3(-7f, HouseBuilder.FU + 1.2f, -3f), 6f, 0.15f);
            ctx.Area("Yard", new Vector3(ExteriorBuilder.FenceX0, -1f, ExteriorBuilder.FenceZ0), new Vector3(ExteriorBuilder.FenceX1, 20f, ExteriorBuilder.FenceZ1));
            ctx.Spectator(new Vector3(7f, 3.2f, -31f), new Vector3(0f, 3.5f, -8f));
            ctx.Spectator(new Vector3(-8f, 13f, 16f), new Vector3(-25f, 0f, 48f));
            ctx.Spectator(new Vector3(33f, 5f, 40f), new Vector3(21f, 3f, 60f));
            ctx.Spectator(new Vector3(-38f, 7f, -40f), new Vector3(-55f, 0f, -18f));
            ctx.Spectator(new Vector3(-3.6f, HouseBuilder.FU + 2.3f, 1.6f), new Vector3(-9.8f, HouseBuilder.FU + 0.8f, -3f));
            ctx.Spectator(new Vector3(1.5f, HouseBuilder.FB + 1.7f, -7.4f), new Vector3(0f, HouseBuilder.FB + 0.8f, 6f));
            ctx.Spectator(new Vector3(-3.8f, HouseBuilder.FG + 2.4f, 1.0f), new Vector3(-8.5f, HouseBuilder.FG + 0.6f, 6f));
            ctx.Spectator(new Vector3(24f, 8f, 4f), new Vector3(38f, 10f, 18f));
            ctx.Spectator(new Vector3(0f, 2.5f, -67f), new Vector3(0f, 2f, -45f));
            ctx.Marker("HouseFront", new Vector3(0f, 1.7f, -14f), Quaternion.LookRotation(Vector3.forward));
            ctx.Marker("Road", new Vector3(0f, 0.1f, -59f), Quaternion.LookRotation(Vector3.right));
            ctx.Marker("Breach", new Vector3(21f, 1f, ExteriorBuilder.FenceZ1), Quaternion.LookRotation(Vector3.forward));
        }

        // ------------------------------------------------------------------ validation + fallbacks

        static void Validate(MapContext ctx)
        {
            var d = ctx.Data;
            // fallbacks so gameplay never sees missing essentials
            while (d.PrisonerSpawns.Count < GameInfo.MaxPrisoners)
                d.PrisonerSpawns.Add(new Pose(new Vector3(-7f, HouseBuilder.FU, -6.5f + d.PrisonerSpawns.Count * 2.4f), Quaternion.LookRotation(Vector3.right)));
            if (d.Radio.RadioSet == null) d.Radio.RadioSet = ctx.Interact(null, new Vector3(10.42f, HouseBuilder.FU + 1f, -4.35f), new Vector3(0.6f, 0.5f, 0.8f), Quaternion.identity, "RadioSet_Fallback");
            if (d.Radio.FuseBox == null) d.Radio.FuseBox = ctx.Interact(null, new Vector3(5.5f, HouseBuilder.FB + 1.5f, -0.4f), new Vector3(0.6f, 0.7f, 0.3f), Quaternion.identity, "FuseBox_Fallback");
            if (d.Shelter.Keypad == null) d.Shelter.Keypad = ctx.Interact(null, new Vector3(-10.8f, HouseBuilder.FB + 1.35f, -5.15f), new Vector3(0.2f, 0.36f, 0.3f), Quaternion.identity, "Keypad_Fallback");
            if (d.MainGate.Interact == null) d.MainGate.Interact = ctx.Interact(null, new Vector3(0f, 1.02f, -45f), new Vector3(0.5f, 0.55f, 0.5f), Quaternion.identity, "Padlock_Fallback");
            if (d.FuelDepot.Barrels == null) d.FuelDepot.Barrels = ctx.Interact(null, new Vector3(21f, 1f, 82.7f), new Vector3(5f, 2f, 1.9f), Quaternion.identity, "Drums_Fallback");

            int keys = 0, wires = 0, bears = 0;
            foreach (var s in d.ItemSpawns) if (s.Tier == ItemSpawnTier.Key) keys++;
            foreach (var t in d.TrapSpots) if (t.Kind == TrapKind.Tripwire) wires++; else bears++;
            string summary = "[MapBuilder] seed " + d.Seed + ": doors " + d.Doors.Count + ", hiding " + d.HidingSpots.Count + ", items " + d.ItemSpawns.Count
                             + " (" + keys + " key), tripwires " + wires + ", bear traps " + bears + ", cages " + d.Cages.Count + ", notes " + d.NoteSpots.Count
                             + ", mannequins " + d.Mannequins.Count + ", emitters " + d.SoundEmitters.Count + ", lights " + PsxLightCount(ctx)
                             + " (power " + d.PowerLights.Count + ", radio " + d.RadioRoomLights.Count + "), nav " + (d.Nav != null ? d.Nav.Nodes.Count : 0)
                             + " nodes, renderers " + ctx.RendererCount + ", vertices " + ctx.TotalVertices;
            ctx.BuildLog.Add(summary);
            Debug.Log(summary);
            if (d.Doors.Count < 20 || d.HidingSpots.Count < 10 || d.ItemSpawns.Count < 75 || wires < 14 || bears < 10 || d.Cages.Count != 4 || d.NoteSpots.Count < 12)
                Debug.LogWarning("[MapBuilder] content below target counts: " + summary);
        }

        static int PsxLightCount(MapContext ctx) => ctx.Lights != null ? ctx.Lights.GetComponentsInChildren<PsxLight>(true).Length : 0;
    }
}
