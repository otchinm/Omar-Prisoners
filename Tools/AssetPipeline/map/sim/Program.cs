// Offline QA harness for the runtime map builder (no Unity needed):
//   dotnet run -c Release -- <outDir> [seed] [--no-render]
// Builds the map against a managed UnityEngine stub (box-collider physics with Unity query semantics),
// checks determinism, exports MapData + colliders + nav to <outDir>/map.json (read by plan.py) and renders
// perspective views (approximate PS1 vertex lighting from the PsxLights + fog) to <outDir>/views/*.png.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using PrisonersOfOmar;
using PrisonersOfOmar.Map;
using PrisonersOfOmar.Rendering;
using UnityEngine;

static class Program
{
    static readonly CultureInfo IC = CultureInfo.InvariantCulture;

    static int Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : "map_out";
        int seed = args.Length > 1 && int.TryParse(args[1], out int s) ? s : 12345;
        bool render = !args.Contains("--no-render");
        Directory.CreateDirectory(outDir);
        UnityEngine.Debug.Quiet = true;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        UnityEngine.Object.ResetWorld();
        var data = MapBuilder.Build(seed, null);
        Console.WriteLine("build: " + sw.ElapsedMilliseconds + " ms, raycasts " + Physics.RaycastCount + ", overlaps " + Physics.OverlapCount);
        foreach (var line in MapBuilder.LastBuildLog) if (line.StartsWith("[MapBuilder]") || line.Contains(":") && !line.Contains(" verts,")) Console.WriteLine("  " + line);
        string json1 = ExportData(data, false);
        int pi = Array.IndexOf(args, "--probe");
        if (pi >= 0 && pi + 6 < args.Length)
        {
            float F(int k) => float.Parse(args[pi + k], IC);
            Vector3 a = new Vector3(F(1), F(2), F(3)), b = new Vector3(F(4), F(5), F(6));
            int mask = (1 << 8) | 1;
            foreach (float h in new[] { 0.4f, 1.2f })
                foreach (float o in new[] { 0f, -0.3f, 0.3f })
                {
                    var flat = new Vector3(b.x - a.x, 0, b.z - a.z);
                    var side = new Vector3(-flat.z, 0, flat.x).normalized;
                    var from = a + Vector3.up * h + side * o; var to = b + Vector3.up * h + side * o;
                    bool hit1 = Physics.Raycast(from, (to - from).normalized, out var h1, (to - from).magnitude, mask, QueryTriggerInteraction.Ignore);
                    bool hit2 = Physics.Raycast(to, (from - to).normalized, out var h2, (to - from).magnitude, mask, QueryTriggerInteraction.Ignore);
                    Console.WriteLine("probe h=" + h + " o=" + o + ": " + (hit1 ? h1.collider.name + "@" + h1.point.ToString("F2") : "-") + " | " + (hit2 ? h2.collider.name + "@" + h2.point.ToString("F2") : "-"));
                }
            for (int k = 1; k <= 4; k++)
            {
                var p = Vector3.Lerp(a, b, k / 5f);
                bool g = Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var gh, 1f, mask, QueryTriggerInteraction.Ignore);
                Console.WriteLine("floor " + p.ToString("F2") + ": " + (g ? gh.collider.name + " y=" + gh.point.y.ToString("F3") : "NONE"));
            }
        }
        var world = UnityEngine.Object.AllGameObjects.ToList();
        var colliders = Physics.AllColliders.ToList();

        // determinism: same seed => identical MapData lists
        int errorsFirst = UnityEngine.Debug.Errors;
        UnityEngine.Object.ResetWorld();
        var data2 = MapBuilder.Build(seed, null);
        string json2 = ExportData(data2, false);
        Console.WriteLine("determinism (same seed): " + (json1 == json2 ? "OK" : "MISMATCH"));
        if (json1 != json2) File.WriteAllText(Path.Combine(outDir, "mismatch_b.json"), json2);
        UnityEngine.Object.ResetWorld();
        var data3 = MapBuilder.Build(seed + 1, null);
        Console.WriteLine("other seed differs: " + (ExportData(data3, false) != json1));
        Console.WriteLine("iteration 2: cell rooms " + data.CellRooms.Count + " (cage slots " + data.Cages.Count + ", active at build " + data.Cages.Count(c => c.Root != null && c.Root.gameObject.activeSelf)
            + "), gun spots " + data.GunSpots.Count + ", crawlspaces " + data.CrawlSpaces.Count + ", lift beds " + data.HidingSpots.Count(h => h.LiftPivot != null)
            + ", grandma " + (data.Grandma != null ? data.Grandma.Area + " (" + data.Grandma.RoamNodes.Count + " roam nodes)" : "none") + ", kitchen " + (data.Kitchen != null ? data.Kitchen.Area : "none"));
        UnityEngine.Object.ResetWorld();
        var menu = MenuSceneBuilder.Build(null, out var camPose);
        Console.WriteLine("menu room: " + UnityEngine.Object.AllGameObjects.Count + " objects, camera " + camPose.position.ToString("F2"));
        var menuWorld = UnityEngine.Object.AllGameObjects.ToList();

        // full export with colliders (first build)
        File.WriteAllText(Path.Combine(outDir, "map.json"), ExportData(data, true, colliders, world));
        Console.WriteLine("errors: " + errorsFirst + " (first build), warnings total " + UnityEngine.Debug.Warnings);
        foreach (var e in UnityEngine.Debug.ErrorLog.Take(30)) Console.WriteLine("  ERR " + e);
        Console.WriteLine("missing textures requested: " + Resources.Requested.Count);

        if (render)
        {
            var views = Path.Combine(outDir, "views");
            Directory.CreateDirectory(views);
            var scene = Raster.Collect(world);
            var menuScene = Raster.Collect(menuWorld);
            Console.WriteLine("render scene: " + scene.Tris + " triangles");
            foreach (var v in Views())
            {
                Raster.Render(scene, v.eye, v.target, v.lit, 640, 400, 72f, Path.Combine(views, v.name + ".png"));
            }
            var mp = camPose.position;
            Raster.Render(menuScene, mp, mp + camPose.rotation * Vector3.forward, true, 640, 400, 70f, Path.Combine(views, "menu_room.png"), menu: true);
            Raster.Render(menuScene, mp, mp + camPose.rotation * Vector3.forward, false, 640, 400, 70f, Path.Combine(views, "menu_room_debug.png"), menu: true);
            Console.WriteLine("views written: " + views);
        }
        return UnityEngine.Debug.Errors > 0 ? 1 : 0;
    }

    static IEnumerable<(string name, Vector3 eye, Vector3 target, bool lit)> Views()
    {
        Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        yield return ("ext_front_lit", V(7, 3.2f, -31), V(0, 3.5f, -8), true);
        yield return ("ext_front", V(7, 3.2f, -31), V(0, 3.5f, -8), false);
        yield return ("ext_front_close", V(-4, 1.7f, -18), V(2, 2.5f, -8), false);
        yield return ("ext_aerial", V(-70, 75, -105), V(-10, 0, 20), false);
        yield return ("ext_aerial_north", V(60, 60, 120), V(-5, 0, 30), false);
        yield return ("ext_back", V(12, 3, 25), V(0, 4, 4), false);
        yield return ("ext_side_west", V(-30, 4, 5), V(-5, 3, 0), false);
        yield return ("ext_farm", V(0, 3, 15), V(10, 4, 55), false);
        yield return ("ext_farm_lit", V(0, 3, 15), V(10, 4, 55), true);
        yield return ("ext_field_lane", V(-40, 1.7f, 30.2f), V(0, 1.2f, 30.2f), false);
        yield return ("ext_clearing", V(-25, 1.7f, 36), V(-25, 1.2f, 50), false);
        yield return ("ext_barn_outside", V(21, 2, 40), V(21, 4, 60), false);
        yield return ("ext_depot", V(14, 2.5f, 74), V(22, 1, 83), false);
        yield return ("ext_silo", V(30, 3, 50), V(40, 6, 58), false);
        yield return ("ext_windmill", V(-2, 2, 62), V(-8, 9, 74), false);
        yield return ("ext_watertower", V(24, 8, 4), V(38, 10, 18), false);
        yield return ("ext_parking", V(-38, 7, -40), V(-55, 0, -18), false);
        yield return ("ext_parking_lit", V(-38, 7, -40), V(-55, 0, -18), true);
        yield return ("ext_car", V(-49, 1.6f, -27), V(-52, 0.8f, -20), false);
        yield return ("ext_restroom", V(-56, 2, -8), V(-62, 1.5f, 4), false);
        yield return ("ext_road", V(0, 2.5f, -67), V(0, 2, -45), false);
        yield return ("ext_road_lit", V(0, 2.5f, -67), V(0, 2, -45), true);
        yield return ("ext_road_east", V(-6, 1.7f, -60), V(30, 1.2f, -58), false);
        yield return ("ext_gate", V(0, 1.7f, -38), V(0, 1.2f, -46), false);
        yield return ("ext_shed", V(25, 2, -9), V(17, 1.5f, -1), false);
        yield return ("ext_porch", V(-1.5f, 1.8f, -13.5f), V(2, 1.6f, -8.5f), false);
        yield return ("ext_breach", V(21, 2, 72), V(21, 1.5f, 86), false);
        yield return ("int_hall", V(0, 2.2f, -7.2f), V(0, 1.6f, 4), false);
        yield return ("int_hall_lit", V(0, 2.2f, -7.2f), V(0, 1.6f, 4), true);
        yield return ("int_hall_north", V(0.5f, 2.2f, 7.4f), V(0, 1.0f, -4), false);
        yield return ("int_dining", V(-3.6f, 2.4f, -0.6f), V(-10, 0.8f, -6), false);
        yield return ("int_kitchen", V(-3.8f, 2.4f, 1.0f), V(-8.5f, 1.2f, 6), false);
        yield return ("int_kitchen_lit", V(-3.8f, 2.4f, 1.0f), V(-8.5f, 1.2f, 6), true);
        yield return ("int_living", V(3.5f, 2.3f, -7.5f), V(10, 0.8f, -2.5f), false);
        yield return ("int_bathroom", V(7, 2.3f, -0.6f), V(9, 0.8f, 2.6f), false);
        yield return ("int_bathroom2", V(10.4f, 2.3f, -0.5f), V(4, 0.8f, 2.5f), false);
        yield return ("int_clockbedroom", V(4.2f, 2.3f, 7.4f), V(10, 0.8f, 4.5f), false);
        yield return ("int_clockbedroom_lit", V(4.2f, 2.3f, 7.4f), V(10, 0.8f, 4.5f), true);
        yield return ("int_clockbedroom_n", V(9.5f, 2.3f, 3.5f), V(6.5f, 1.3f, 7.9f), false);
        yield return ("int_upperhall", V(0, 5.6f, -7.3f), V(0, 4.6f, 6), false);
        yield return ("int_cageroom", V(-3.6f, 6.1f, 1.6f), V(-9.8f, 4.6f, -3), false);
        yield return ("int_cageroom_lit", V(-3.6f, 6.1f, 1.6f), V(-9.8f, 4.6f, -3), true);
        yield return ("int_storage", V(-3.6f, 5.6f, 7.5f), V(-10, 4.0f, 3), false);
        yield return ("int_radio", V(3.6f, 5.6f, -1.6f), V(10.5f, 4.4f, -5), false);
        yield return ("int_study", V(3.6f, 5.8f, 7.6f), V(9, 4.2f, 4), false);
        yield return ("int_closet", V(7.2f, 5.6f, 2.6f), V(5, 4.4f, -0.8f), false);
        yield return ("int_basement", V(1.5f, -0.9f, -7.4f), V(0, -1.8f, 6), false);
        yield return ("int_basement_lit", V(1.5f, -0.9f, -7.4f), V(0, -1.8f, 6), true);
        yield return ("int_stairs_down", V(-2.3f, 2.2f, 7.3f), V(-2.3f, -1.5f, -1), false);
        yield return ("int_stairs_up", V(2.3f, 1.8f, -4.5f), V(2.3f, 4.5f, 5), false);
        yield return ("int_exhibit", V(-3.5f, -1.0f, 6.5f), V(-10, -1.8f, 3), false);
        yield return ("int_stairs_basement", V(0.8f, -1.2f, -5.5f), V(-2.2f, -1.6f, 2.5f), false);
        yield return ("int_exhibit_lit", V(-3.5f, -1.0f, 6.5f), V(-10, -1.8f, 3), true);
        yield return ("int_antechamber", V(-3.6f, -1.0f, -1.0f), V(-10.9f, -1.6f, -4.5f), false);
        yield return ("int_furnace", V(3.6f, -1.0f, 7.5f), V(10, -1.8f, 3), false);
        yield return ("int_furnace_lit", V(3.6f, -1.0f, 7.5f), V(10, -1.8f, 3), true);
        yield return ("int_generator", V(3.6f, -1.0f, -0.6f), V(9, -2, -5), false);
        yield return ("int_tunnel", V(-12, -1.0f, -4), V(-26, -1.6f, -4), false);
        yield return ("int_tunnel_end", V(-31, -1.0f, 6), V(-44, -1.5f, 6), false);
        yield return ("int_barn", V(21, 2.0f, 53), V(21, 1.5f, 66), false);
        yield return ("int_restroom", V(-62, 1.7f, 2.6f), V(-62, 1.2f, 7.5f), false);
        yield return ("int_shed", V(14.6f, 1.7f, -1.0f), V(21, 1.0f, -1), false);
        yield return ("int_silo", V(37, 1.7f, 58), V(43, 2.5f, 58), false);
    }

    // ===================================================================== export

    sealed class J
    {
        readonly StringBuilder _sb = new StringBuilder();
        public override string ToString() => _sb.ToString();
        public J Raw(string s) { _sb.Append(s); return this; }
        public J F(float f) { _sb.Append(float.IsNaN(f) || float.IsInfinity(f) ? "0" : Math.Round(f, 4).ToString("0.####", IC)); return this; }
        public J S(string s) { _sb.Append('"').Append((s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"'); return this; }
        public J V(Vector3 v) { _sb.Append('['); F(v.x).Raw(",").F(v.y).Raw(",").F(v.z).Raw("]"); return this; }
        public J Q(Quaternion q) { _sb.Append('['); F(q.x).Raw(",").F(q.y).Raw(",").F(q.z).Raw(",").F(q.w).Raw("]"); return this; }
        public J P(Pose p) { _sb.Append("{\"pos\":"); V(p.position).Raw(",\"fwd\":").V(p.rotation * Vector3.forward).Raw("}"); return this; }
        public J B(Bounds b) { _sb.Append("{\"min\":"); V(b.min).Raw(",\"max\":").V(b.max).Raw("}"); return this; }
        public J K(string k) { _sb.Append('"').Append(k).Append("\":"); return this; }
    }

    static Vector3 Pos(Component c) => c != null ? c.transform.position : Vector3.zero;
    static Vector3 ColCenter(Collider c) => c is BoxCollider b ? b.transform.TransformPoint(b.center) : Pos(c);

    static string ExportData(MapData d, bool full, List<Collider> colliders = null, List<GameObject> world = null)
    {
        var j = new J();
        j.Raw("{").K("seed").Raw(d.Seed.ToString(IC));
        j.Raw(",").K("doors").Raw("[");
        for (int i = 0; i < d.Doors.Count; i++)
        {
            var x = d.Doors[i];
            if (i > 0) j.Raw(",");
            float leafW = x.Leaf is BoxCollider lb ? lb.size.x : 0.9f;
            j.Raw("{").K("name").S(x.Name).Raw(",").K("kind").S(x.Kind.ToString()).Raw(",").K("pivot").V(Pos(x.Pivot)).Raw(",").K("leafDir").V(x.Pivot != null ? x.Pivot.right : Vector3.right)
                .Raw(",").K("open").F(x.OpenAngle).Raw(",").K("leafW").F(leafW).Raw(",").K("center").V(x.Center).Raw(",").K("locked").Raw(x.StartsLocked ? "true" : "false")
                .Raw(",").K("key").S(x.KeyItem.ToString()).Raw(",").K("boards").Raw(x.Boards != null ? "true" : "false").Raw(",").K("leaf").V(ColCenter(x.Leaf))
                .Raw(",").K("swing").V(x.SwingDirection).Raw(",").K("areas").Raw("[").S(x.AreaSwingSide).Raw(",").S(x.AreaOtherSide).Raw("]}");
        }
        j.Raw("],").K("hiding").Raw("[");
        for (int i = 0; i < d.HidingSpots.Count; i++)
        {
            var h = d.HidingSpots[i];
            if (i > 0) j.Raw(",");
            j.Raw("{").K("name").S(h.Name).Raw(",").K("kind").S(h.Kind.ToString()).Raw(",").K("root").V(Pos(h.Root)).Raw(",").K("interact").V(ColCenter(h.Interact))
                .Raw(",").K("view").P(h.HiddenView).Raw(",").K("exit").P(h.ExitPose).Raw(",").K("doors").Raw(h.Doors.Length.ToString(IC));
            // iteration 2: beds Omar can tip up (lift hinge + where Omar / the prisoner stand)
            j.Raw(",").K("lift").Raw(h.LiftPivot != null ? "true" : "false");
            if (h.LiftPivot != null) j.Raw(",").K("liftPivot").V(Pos(h.LiftPivot)).Raw(",").K("liftAngle").F(h.LiftAngle).Raw(",").K("lifter").P(h.LifterPose).Raw(",").K("crawl").P(h.CrawlStart);
            j.Raw("}");
        }
        j.Raw("],").K("items").Raw("[");
        for (int i = 0; i < d.ItemSpawns.Count; i++)
        {
            var it = d.ItemSpawns[i];
            if (i > 0) j.Raw(",");
            j.Raw("{").K("p").V(it.Position).Raw(",").K("yaw").F(it.Yaw).Raw(",").K("area").S(it.Area).Raw(",").K("tier").S(it.Tier.ToString()).Raw("}");
        }
        j.Raw("],").K("traps").Raw("[");
        for (int i = 0; i < d.TrapSpots.Count; i++)
        {
            var t = d.TrapSpots[i];
            if (i > 0) j.Raw(",");
            j.Raw("{").K("kind").S(t.Kind.ToString()).Raw(",").K("a").V(t.A).Raw(",").K("b").V(t.B).Raw(",").K("area").S(t.Area).Raw("}");
        }
        j.Raw("],").K("cages").Raw("[");
        for (int i = 0; i < d.Cages.Count; i++)
        {
            var c = d.Cages[i];
            if (i > 0) j.Raw(",");
            j.Raw("{").K("root").V(Pos(c.Root)).Raw(",").K("door").V(Pos(c.DoorPivot)).Raw(",").K("doorDir").V(c.DoorPivot != null ? c.DoorPivot.right : Vector3.zero).Raw(",").K("open").F(c.OpenAngle)
                .Raw(",").K("inside").P(c.Inside).Raw(",").K("outside").P(c.Outside).Raw(",").K("interact").V(ColCenter(c.Interact))
                .Raw(",").K("room").Raw(c.RoomIndex.ToString(IC)).Raw(",").K("active").Raw(c.Root != null && c.Root.gameObject.activeSelf ? "true" : "false").Raw("}");
        }
        j.Raw("],").K("spawns").Raw("[");
        for (int i = 0; i < d.PrisonerSpawns.Count; i++) { if (i > 0) j.Raw(","); j.P(d.PrisonerSpawns[i]); }
        j.Raw("],").K("omar").P(d.OmarSpawn);
        j.Raw(",").K("notes").Raw("[");
        for (int i = 0; i < d.NoteSpots.Count; i++)
        {
            var n = d.NoteSpots[i];
            if (i > 0) j.Raw(",");
            j.Raw("{").K("p").V(n.Position).Raw(",").K("fwd").V(n.Rotation * Vector3.forward).Raw(",").K("wall").Raw(n.OnWall ? "true" : "false").Raw(",").K("area").S(n.Area).Raw("}");
        }
        j.Raw("],").K("ambient").Raw("[");
        for (int i = 0; i < d.AmbientZones.Count; i++) { if (i > 0) j.Raw(","); j.Raw("{").K("type").S(d.AmbientZones[i].Type.ToString()).Raw(",").K("b").B(d.AmbientZones[i].Bounds).Raw("}"); }
        j.Raw("],").K("anomaly").Raw("[");
        for (int i = 0; i < d.AnomalyZones.Count; i++) { var a = d.AnomalyZones[i]; if (i > 0) j.Raw(","); j.Raw("{").K("name").S(a.Name).Raw(",").K("c").V(a.Center).Raw(",").K("r").F(a.Radius).Raw(",").K("s").F(a.Strength).Raw("}"); }
        j.Raw("],").K("emitters").Raw("[");
        for (int i = 0; i < d.SoundEmitters.Count; i++) { var e = d.SoundEmitters[i]; if (i > 0) j.Raw(","); j.Raw("{").K("p").V(e.Position).Raw(",").K("clip").S(e.Clip).Raw(",").K("power").Raw(e.NeedsPower ? "true" : "false").Raw("}"); }
        j.Raw("],").K("mannequins").Raw("[");
        for (int i = 0; i < d.Mannequins.Count; i++)
        {
            var m = d.Mannequins[i];
            if (i > 0) j.Raw(",");
            j.Raw("{").K("p").P(new Pose(Pos(m.Root), m.Root != null ? m.Root.rotation : Quaternion.identity)).Raw(",").K("alts").Raw("[");
            for (int k = 0; k < m.AltPoses.Length; k++) { if (k > 0) j.Raw(","); j.P(m.AltPoses[k]); }
            j.Raw("]}");
        }
        j.Raw("],").K("spectators").Raw("[");
        for (int i = 0; i < d.SpectatorCameras.Count; i++) { if (i > 0) j.Raw(","); j.P(d.SpectatorCameras[i]); }
        j.Raw("],").K("markers").Raw("{");
        int mi = 0;
        foreach (var kv in d.Markers.OrderBy(k => k.Key, StringComparer.Ordinal)) { if (mi++ > 0) j.Raw(","); j.K(kv.Key).P(new Pose(kv.Value.position, kv.Value.rotation)); }
        j.Raw("},").K("areas").Raw("[");
        for (int i = 0; i < d.AreaOrder.Count; i++) { if (i > 0) j.Raw(","); j.Raw("{").K("name").S(d.AreaOrder[i]).Raw(",").K("b").B(d.AreaBounds[d.AreaOrder[i]]).Raw("}"); }
        j.Raw("],").K("gate").Raw("{").K("left").V(Pos(d.MainGate.LeftLeaf)).Raw(",").K("right").V(Pos(d.MainGate.RightLeaf)).Raw(",").K("la").F(d.MainGate.LeftOpenAngle).Raw(",").K("ra").F(d.MainGate.RightOpenAngle)
            .Raw(",").K("padlock").V(ColCenter(d.MainGate.Interact)).Raw(",").K("exit").B(d.MainGate.ExitZone).Raw(",").K("blockers").Raw(d.MainGate.Blockers.Length.ToString(IC)).Raw("}");
        var car = d.Car;
        j.Raw(",").K("car").Raw("{").K("root").P(new Pose(Pos(car.Root), car.Root != null ? car.Root.rotation : Quaternion.identity)).Raw(",").K("fuel").V(ColCenter(car.FuelCap)).Raw(",").K("hood").V(ColCenter(car.Hood))
            .Raw(",").K("driver").V(ColCenter(car.DriverDoor)).Raw(",").K("passengers").Raw("[");
        for (int i = 0; i < car.PassengerDoors.Length; i++) { if (i > 0) j.Raw(","); j.V(ColCenter(car.PassengerDoors[i])); }
        j.Raw("],").K("seats").Raw("[");
        for (int i = 0; i < car.Seats.Length; i++) { if (i > 0) j.Raw(","); j.P(car.Seats[i]); }
        j.Raw("],").K("path").Raw("[");
        for (int i = 0; i < car.DrivePath.Length; i++) { if (i > 0) j.Raw(","); j.V(car.DrivePath[i]); }
        j.Raw("],").K("vgl").V(Pos(car.VehicleGateLeft)).Raw(",").K("vgr").V(Pos(car.VehicleGateRight)).Raw(",").K("vga").F(car.VehicleGateLeftOpenAngle).Raw(",").K("vgb").F(car.VehicleGateRightOpenAngle)
            .Raw(",").K("chain").V(car.VehicleGateChain != null ? car.VehicleGateChain.transform.position : Vector3.zero).Raw(",").K("hoodPivot").V(Pos(car.HoodPivot)).Raw(",").K("headlights").Raw(car.Headlights.Length.ToString(IC)).Raw("}");
        j.Raw(",").K("shelter").Raw("{").K("door").V(Pos(d.Shelter.DoorPivot)).Raw(",").K("dir").V(d.Shelter.DoorPivot != null ? d.Shelter.DoorPivot.right : Vector3.zero).Raw(",").K("open").F(d.Shelter.OpenAngle)
            .Raw(",").K("keypad").V(ColCenter(d.Shelter.Keypad)).Raw(",").K("exit").B(d.Shelter.TunnelExitZone).Raw("}");
        j.Raw(",").K("radio").Raw("{").K("set").V(ColCenter(d.Radio.RadioSet)).Raw(",").K("fuse").V(ColCenter(d.Radio.FuseBox)).Raw(",").K("lz").B(d.Radio.LandingZone).Raw(",").K("heli").V(d.Radio.HelicopterPosition).Raw("}");
        j.Raw(",").K("fire").Raw("{").K("barrels").V(ColCenter(d.FuelDepot.Barrels)).Raw(",").K("center").V(d.FuelDepot.ExplosionCenter).Raw(",").K("breach").Raw(d.FuelDepot.BreachFence != null ? "true" : "false")
            .Raw(",").K("blockers").Raw(d.FuelDepot.BreachBlockers.Length.ToString(IC)).Raw(",").K("exit").B(d.FuelDepot.BreachExitZone).Raw("}");
        j.Raw(",").K("bounds").B(d.PlayableBounds);
        // ---- iteration 2: cell rooms, revolver spots, crawlspaces, grandma, kitchen routine
        j.Raw(",").K("cellRooms").Raw("[");
        for (int i = 0; i < d.CellRooms.Count; i++)
        {
            var r = d.CellRooms[i];
            if (i > 0) j.Raw(",");
            j.Raw("{").K("name").S(r.Name).Raw(",").K("area").S(r.Area).Raw(",").K("c").V(r.Center).Raw(",").K("cages").Raw("[" + string.Join(",", r.CageIndices.Select(x => x.ToString(IC))) + "]}");
        }
        j.Raw("],").K("guns").Raw("[");
        for (int i = 0; i < d.GunSpots.Count; i++)
        {
            var g = d.GunSpots[i];
            if (i > 0) j.Raw(",");
            j.Raw("{").K("p").V(g.Position).Raw(",").K("yaw").F(g.Yaw).Raw(",").K("area").S(g.Area).Raw("}");
        }
        j.Raw("],").K("crawl").Raw("[");
        for (int i = 0; i < d.CrawlSpaces.Count; i++) { if (i > 0) j.Raw(","); j.B(d.CrawlSpaces[i]); }
        j.Raw("]");
        var gm = d.Grandma;
        j.Raw(",").K("grandma");
        if (gm == null) j.Raw("null");
        else
            j.Raw("{").K("area").S(gm.Area).Raw(",").K("room").B(gm.Room).Raw(",").K("chair").P(gm.ChairPose).Raw(",").K("tv").V(Pos(gm.TvScreen)).Raw(",").K("tvSound").V(gm.TvSoundPosition)
                .Raw(",").K("roam").Raw("[" + string.Join(",", gm.RoamNodes.Select(x => x.ToString(IC))) + "]}");
        var kt = d.Kitchen;
        j.Raw(",").K("kitchen");
        if (kt == null) j.Raw("null");
        else
            j.Raw("{").K("area").S(kt.Area).Raw(",").K("chop").P(kt.ChopPose).Raw(",").K("block").V(kt.BlockTop).Raw(",").K("meat").V(Pos(kt.Meat)).Raw(",").K("interact").V(ColCenter(kt.ChopInteract))
                .Raw(",").K("vent").B(kt.VentArea).Raw(",").K("view").P(kt.VentView).Raw("}");
        j.Raw(",").K("power").Raw(d.PowerLights.Count.ToString(IC)).Raw(",").K("radioLights").Raw(d.RadioRoomLights.Count.ToString(IC));
        var nav = d.Nav;
        j.Raw(",").K("nav").Raw("{").K("nodes").Raw("[");
        for (int i = 0; i < nav.Nodes.Count; i++) { if (i > 0) j.Raw(","); j.V(nav.Nodes[i]); }
        j.Raw("],").K("areas").Raw("[");
        for (int i = 0; i < nav.NodeAreas.Count; i++) { if (i > 0) j.Raw(","); j.S(nav.NodeAreas[i]); }
        j.Raw("],").K("edges").Raw("[");
        int ec = 0;
        for (int a = 0; a < nav.Edges.Count; a++)
            foreach (int b in nav.Edges[a])
                if (b > a) { if (ec++ > 0) j.Raw(","); j.Raw("[" + a + "," + b + "," + nav.DoorOnEdge(a, b) + "]"); }
        j.Raw("]}");
        if (full)
        {
            j.Raw(",").K("lights").Raw("[");
            int li = 0;
            foreach (var go in world)
            {
                var l = go.GetComponent<PsxLight>();
                if (l == null) continue;
                if (li++ > 0) j.Raw(",");
                string grp = d.PowerLights.Contains(l) ? "power" : (d.RadioRoomLights.Contains(l) ? "radio" : "none");
                j.Raw("{").K("name").S(go.name).Raw(",").K("p").V(go.transform.position).Raw(",").K("c").Raw("[").F(l.Color.r).Raw(",").F(l.Color.g).Raw(",").F(l.Color.b).Raw("]")
                    .Raw(",").K("i").F(l.Intensity).Raw(",").K("r").F(l.Range).Raw(",").K("type").S(l.Type.ToString()).Raw(",").K("flicker").S(l.Flicker.ToString()).Raw(",").K("on").Raw(l.On ? "true" : "false").Raw(",").K("group").S(grp).Raw("}");
            }
            j.Raw("],").K("colliders").Raw("[");
            int ci = 0;
            foreach (var c in colliders)
            {
                if (!(c is BoxCollider b) || !b.enabled) continue;
                b.WorldBox(out var cc, out var rot, out var half);
                if (ci++ > 0) j.Raw(",");
                var tag = b.GetComponent<SurfaceTag>();
                // a = 0: the collider's GameObject is inactive at build time (e.g. the cage slots gameplay enables per match)
                j.Raw("{").K("n").S(b.gameObject.name).Raw(",").K("l").Raw(b.gameObject.layer.ToString(IC)).Raw(",").K("t").Raw(b.isTrigger ? "1" : "0").Raw(",").K("c").V(cc).Raw(",").K("h").V(half).Raw(",").K("q").Q(rot)
                    .Raw(",").K("s").S(tag != null ? tag.Surface.ToString() : "").Raw(",").K("a").Raw(b.gameObject.activeInHierarchy ? "1" : "0").Raw("}");
            }
            j.Raw("],").K("renderers").Raw("[");
            int ri = 0;
            foreach (var go in world)
            {
                var mf = go.GetComponent<MeshFilter>();
                var mr = go.GetComponent<MeshRenderer>();
                if (mf == null || mr == null || mf.sharedMesh == null) continue;
                if (ri++ > 0) j.Raw(",");
                int tris = 0;
                for (int sIdx = 0; sIdx < mf.sharedMesh.subMeshCount; sIdx++) tris += mf.sharedMesh.GetTriangles(sIdx).Length / 3;
                j.Raw("{").K("n").S(go.name).Raw(",").K("l").Raw(go.layer.ToString(IC)).Raw(",").K("v").Raw(mf.sharedMesh.vertexCount.ToString(IC)).Raw(",").K("t").Raw(tris.ToString(IC)).Raw(",").K("m").Raw(mr.sharedMaterials.Length.ToString(IC)).Raw("}");
            }
            j.Raw("]");
        }
        j.Raw("}");
        return j.ToString();
    }
}

// ===================================================================== tiny software rasterizer (QA previews only)

static class Raster
{
    public sealed class Scene
    {
        public readonly List<Vector3> P = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<Color> C = new List<Color>();       // vertex color * material base
        public readonly List<int> T = new List<int>();           // triangle indices
        public readonly List<byte> Kind = new List<byte>();      // per triangle: 0 opaque, 1 cutout, 2 blend, 3 emissive, 4 additive
        public readonly List<(Vector3 p, Color c, float i, float r, bool spot, Vector3 dir, float angle, bool on)> Lights = new List<(Vector3, Color, float, float, bool, Vector3, float, bool)>();
        public int Tris => T.Count / 3;
    }

    static readonly Dictionary<string, Color> Palette = new Dictionary<string, Color>
    {
        { "wall_wallpaper_blue", new Color(0.55f, 0.62f, 0.82f) }, { "wall_wallpaper_green", new Color(0.5f, 0.56f, 0.44f) }, { "wall_wallpaper_rose", new Color(0.68f, 0.56f, 0.66f) },
        { "wall_wallpaper_yellow", new Color(0.72f, 0.66f, 0.38f) }, { "wall_wainscot", new Color(0.3f, 0.34f, 0.4f) }, { "wall_plaster_dirty", new Color(0.56f, 0.5f, 0.56f) },
        { "wall_brick_basement", new Color(0.72f, 0.68f, 0.64f) }, { "wall_wood_planks", new Color(0.5f, 0.47f, 0.43f) }, { "wall_wood_dark", new Color(0.32f, 0.22f, 0.15f) },
        { "wall_tile_red", new Color(0.66f, 0.3f, 0.24f) }, { "wall_tile_white_dirty", new Color(0.78f, 0.78f, 0.74f) }, { "wall_concrete_block", new Color(0.58f, 0.58f, 0.56f) },
        { "wall_siding_white", new Color(0.82f, 0.82f, 0.8f) }, { "wall_barn_red", new Color(0.55f, 0.16f, 0.12f) }, { "wall_corrugated_metal", new Color(0.5f, 0.45f, 0.4f) },
        { "floor_wood_planks", new Color(0.5f, 0.43f, 0.36f) }, { "floor_wood_dark", new Color(0.32f, 0.24f, 0.18f) }, { "floor_linoleum_dirty", new Color(0.6f, 0.58f, 0.5f) },
        { "floor_concrete", new Color(0.52f, 0.52f, 0.5f) }, { "floor_tile_dirty", new Color(0.6f, 0.6f, 0.56f) }, { "floor_carpet_stained", new Color(0.45f, 0.25f, 0.2f) },
        { "ceiling_plaster_stained", new Color(0.7f, 0.68f, 0.6f) }, { "ceiling_wood", new Color(0.45f, 0.36f, 0.28f) }, { "ground_dirt", new Color(0.36f, 0.3f, 0.24f) },
        { "ground_grass_dead", new Color(0.42f, 0.4f, 0.28f) }, { "ground_gravel", new Color(0.5f, 0.48f, 0.45f) }, { "ground_asphalt", new Color(0.22f, 0.22f, 0.23f) },
        { "ground_asphalt_cracked", new Color(0.28f, 0.28f, 0.28f) }, { "roof_shingles", new Color(0.22f, 0.24f, 0.22f) }, { "roof_tin_rusty", new Color(0.5f, 0.3f, 0.2f) },
        { "metal_rusty", new Color(0.5f, 0.3f, 0.2f) }, { "metal_painted_green", new Color(0.3f, 0.38f, 0.28f) }, { "metal_dark", new Color(0.2f, 0.2f, 0.22f) },
        { "metal_galvanized", new Color(0.6f, 0.6f, 0.6f) }, { "wood_raw", new Color(0.6f, 0.5f, 0.38f) }, { "wood_furniture", new Color(0.35f, 0.22f, 0.14f) },
        { "wood_painted_white", new Color(0.8f, 0.8f, 0.76f) }, { "fabric_mattress_stained", new Color(0.7f, 0.68f, 0.6f) }, { "fabric_sofa", new Color(0.45f, 0.32f, 0.22f) },
        { "fabric_dirty", new Color(0.5f, 0.46f, 0.4f) }, { "concrete_rough", new Color(0.55f, 0.55f, 0.53f) }, { "bark", new Color(0.25f, 0.2f, 0.16f) },
        { "meat", new Color(0.6f, 0.12f, 0.1f) }, { "hay", new Color(0.7f, 0.6f, 0.35f) }, { "chainlink", new Color(0.55f, 0.55f, 0.55f) }, { "barbed_wire", new Color(0.4f, 0.4f, 0.4f) },
        { "wardrobe_front", new Color(0.35f, 0.22f, 0.14f) }, { "dresser_front", new Color(0.38f, 0.25f, 0.16f) }, { "clock_face", new Color(0.85f, 0.8f, 0.65f) }, { "clock_body", new Color(0.35f, 0.22f, 0.14f) },
        { "radiator", new Color(0.55f, 0.5f, 0.45f) }, { "tv_static_0", new Color(0.7f, 0.7f, 0.75f) }, { "tv_body", new Color(0.35f, 0.25f, 0.15f) }, { "fridge_front", new Color(0.8f, 0.78f, 0.7f) },
        { "portrait_omar", new Color(0.4f, 0.35f, 0.25f) }, { "painting_landscape", new Color(0.3f, 0.32f, 0.3f) }, { "sign_fallout", new Color(0.9f, 0.75f, 0.1f) }, { "sign_restricted", new Color(0.9f, 0.9f, 0.88f) },
        { "sign_road", new Color(0.9f, 0.9f, 0.88f) }, { "barrel_water", new Color(0.2f, 0.2f, 0.2f) }, { "barrel_fuel", new Color(0.65f, 0.12f, 0.08f) }, { "bucket_food", new Color(0.8f, 0.82f, 0.75f) },
        { "box_cardboard", new Color(0.6f, 0.48f, 0.32f) }, { "crate_wood", new Color(0.6f, 0.5f, 0.35f) }, { "car_body", new Color(0.4f, 0.1f, 0.12f) }, { "car_body_wreck", new Color(0.45f, 0.28f, 0.18f) },
        { "car_front", new Color(0.4f, 0.35f, 0.35f) }, { "car_rear", new Color(0.4f, 0.3f, 0.3f) }, { "car_side", new Color(0.4f, 0.15f, 0.15f) }, { "car_tire", new Color(0.1f, 0.1f, 0.1f) },
        { "keypad", new Color(0.5f, 0.5f, 0.5f) }, { "fusebox", new Color(0.55f, 0.55f, 0.55f) }, { "radio_set", new Color(0.3f, 0.35f, 0.28f) }, { "generator", new Color(0.3f, 0.4f, 0.3f) },
        { "cage_bars", new Color(0.4f, 0.32f, 0.25f) }, { "flamingo_pink", new Color(0.95f, 0.45f, 0.6f) }, { "water_tower_tank", new Color(0.7f, 0.2f, 0.15f) }, { "silo_metal", new Color(0.6f, 0.6f, 0.58f) },
        { "utility_pole", new Color(0.3f, 0.24f, 0.18f) }, { "window_red_glow", new Color(1f, 0.15f, 0.1f) }, { "window_dark", new Color(0.1f, 0.12f, 0.14f) }, { "window_boarded", new Color(0.45f, 0.38f, 0.3f) },
        { "door_wood", new Color(0.45f, 0.3f, 0.2f) }, { "door_wood_dirty", new Color(0.4f, 0.27f, 0.18f) }, { "door_front", new Color(0.5f, 0.4f, 0.3f) }, { "door_metal_shelter", new Color(0.45f, 0.45f, 0.4f) },
        { "door_metal", new Color(0.4f, 0.4f, 0.42f) }, { "door_planks", new Color(0.5f, 0.42f, 0.33f) }, { "stairs_wood", new Color(0.45f, 0.35f, 0.25f) }, { "bed_frame_metal", new Color(0.3f, 0.3f, 0.32f) },
        { "table_wood", new Color(0.45f, 0.32f, 0.22f) }, { "shelf_metal", new Color(0.12f, 0.12f, 0.12f) }, { "paper_note", new Color(0.85f, 0.82f, 0.7f) }, { "hay_bale", new Color(0.72f, 0.62f, 0.38f) },
        { "meat_slab", new Color(0.65f, 0.15f, 0.12f) }, { "book_spines", new Color(0.4f, 0.25f, 0.2f) }, { "boards_nailed", new Color(0.5f, 0.42f, 0.33f) },
        { "tree_dead_1", new Color(0.18f, 0.18f, 0.17f) }, { "tree_dead_2", new Color(0.18f, 0.18f, 0.17f) }, { "tree_dead_3", new Color(0.18f, 0.18f, 0.17f) }, { "tree_pine_1", new Color(0.1f, 0.14f, 0.1f) },
        { "tree_pine_2", new Color(0.1f, 0.14f, 0.1f) }, { "tree_big", new Color(0.1f, 0.13f, 0.1f) }, { "bush_dark_1", new Color(0.12f, 0.15f, 0.1f) }, { "bush_dark_2", new Color(0.12f, 0.15f, 0.1f) },
        { "grass_tall_1", new Color(0.45f, 0.42f, 0.28f) }, { "grass_tall_2", new Color(0.45f, 0.42f, 0.28f) }, { "cornstalks", new Color(0.5f, 0.45f, 0.3f) }, { "treeline", new Color(0.05f, 0.06f, 0.06f) },
        { "blob_shadow", new Color(0f, 0f, 0f) }, { "fire_0", new Color(1f, 0.55f, 0.2f) },
        { "blood_pool", new Color(0.35f, 0.02f, 0.02f) }, { "blood_splatter_1", new Color(0.4f, 0.03f, 0.03f) }, { "blood_splatter_2", new Color(0.4f, 0.03f, 0.03f) }, { "blood_splatter_3", new Color(0.4f, 0.03f, 0.03f) },
        { "blood_smear", new Color(0.4f, 0.04f, 0.03f) }, { "blood_handprint", new Color(0.45f, 0.04f, 0.03f) }, { "graffiti_scrawl_1", new Color(0.02f, 0.02f, 0.02f) }, { "graffiti_scrawl_2", new Color(0.02f, 0.02f, 0.02f) },
        { "grime", new Color(0.1f, 0.09f, 0.07f) }, { "water_stain", new Color(0.3f, 0.25f, 0.15f) }, { "parking_line", new Color(0.85f, 0.7f, 0.1f) }, { "road_dashes", new Color(0.9f, 0.9f, 0.85f) },
        { "writing_help", new Color(0.5f, 0.03f, 0.03f) }, { "writing_omar", new Color(0.5f, 0.03f, 0.03f) }, { "cracks", new Color(0.1f, 0.1f, 0.1f) },
    };

    static (Color baseColor, byte kind) MaterialInfo(Material m)
    {
        string n = m?.name ?? "color_Lit";
        int us = n.LastIndexOf('_');
        string tex = us > 0 ? n.Substring(0, us) : n;
        string surf = us > 0 ? n.Substring(us + 1) : "Lit";
        string file = tex.Contains("/") ? tex.Substring(tex.LastIndexOf('/') + 1) : tex;
        Color c = Palette.TryGetValue(file, out var p) ? p : new Color(1, 1, 1);
        c = c * m.color;
        byte kind = surf switch { "LitCutout" => 1, "UnlitCutout" => 1, "Decal" => 2, "Transparent" => 2, "Emissive" => 3, "Unlit" => 3, "Additive" => 4, _ => 0 };
        if (surf == "Decal" && file == "blob_shadow") c.a = 0.45f;
        else if (surf == "Decal") c.a = 0.65f;
        return (c, kind);
    }

    public static Scene Collect(List<GameObject> world)
    {
        var s = new Scene();
        foreach (var go in world)
        {
            if (!go.activeInHierarchy) continue;
            var l = go.GetComponent<PsxLight>();
            if (l != null) s.Lights.Add((go.transform.position, l.Color, l.Intensity, l.Range, l.Type == PsxLightType.Spot, go.transform.forward, l.SpotAngle, l.On));
            var mf = go.GetComponent<MeshFilter>();
            var mr = go.GetComponent<MeshRenderer>();
            if (mf == null || mr == null || mf.sharedMesh == null) continue;
            var mesh = mf.sharedMesh;
            var m = go.transform.localToWorldMatrix;
            var rot = go.transform.rotation;
            int baseIdx = s.P.Count;
            for (int i = 0; i < mesh.vertices.Length; i++)
            {
                s.P.Add(m.MultiplyPoint3x4(mesh.vertices[i]));
                s.N.Add((rot * (i < mesh.normals.Length ? mesh.normals[i] : Vector3.up)).normalized);
                s.C.Add(i < mesh.colors32.Length ? (Color)mesh.colors32[i] : Color.white);
            }
            // per-vertex material colors: a vertex can be shared by submeshes, so duplicate color into triangles at draw time
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                var mat = sub < mr.sharedMaterials.Length ? mr.sharedMaterials[sub] : null;
                var (bc, kind) = MaterialInfo(mat);
                var tris = mesh.GetTriangles(sub);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    // store material color in a parallel list via extra vertices (simple and robust)
                    for (int k = 0; k < 3; k++)
                    {
                        int vi = baseIdx + tris[t + k];
                        s.P.Add(s.P[vi]); s.N.Add(s.N[vi]);
                        var vc = s.C[vi] * bc; vc.a = bc.a;
                        s.C.Add(vc);
                        s.T.Add(s.P.Count - 1);
                    }
                    s.Kind.Add(kind);
                }
            }
        }
        return s;
    }

    public static void Render(Scene s, Vector3 eye, Vector3 target, bool lit, int W, int H, float fov, string path, bool menu = false)
    {
        var color = new float[W * H * 3];
        var depth = new float[W * H];
        for (int i = 0; i < depth.Length; i++) depth[i] = float.MaxValue;
        Color bg = lit ? new Color(0.03f, 0.035f, 0.05f) : new Color(0.25f, 0.3f, 0.38f);
        for (int i = 0; i < W * H; i++) { color[i * 3] = bg.r; color[i * 3 + 1] = bg.g; color[i * 3 + 2] = bg.b; }
        var rot = Quaternion.LookRotation((target - eye).normalized, Vector3.up);
        var inv = Quaternion.Inverse(rot);
        float f = 1f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float aspect = (float)W / H;
        float near = 0.05f;
        Vector3 L = new Vector3(0.4f, 0.8f, -0.45f).normalized;
        Color amb = menu ? new Color(0.03f, 0.03f, 0.045f) : new Color(0.05f, 0.055f, 0.08f);
        Color fogC = menu ? new Color(0.01f, 0.012f, 0.018f) : new Color(0.02f, 0.025f, 0.035f);
        float fogS = menu ? 2.5f : 4f, fogE = menu ? 16f : 45f;
        // lights near the camera only (speed)
        var lights = s.Lights.Where(l => l.on && Vector3.Distance(l.p, eye) < 80f + l.r).ToList();
        int n = s.P.Count;
        var vs = new Vector3[n];
        var vcol = new Color[n];
        var done = new bool[n];
        Color Shade(int i, byte kind)
        {
            var c = s.C[i];
            if (kind == 3 || kind == 4) return c;
            Vector3 p = s.P[i], nn = s.N[i];
            if (!lit)
            {
                float d = Mathf.Max(0f, Vector3.Dot(nn, L));
                float k = 0.55f + 0.55f * d;
                return new Color(c.r * k, c.g * k, c.b * k, c.a);
            }
            Color acc = amb;
            foreach (var l in lights)
            {
                Vector3 dv = l.p - p;
                float dist = dv.magnitude;
                if (dist >= l.r) continue;
                float att = 1f - dist / l.r; att *= att;
                Vector3 dir = dist > 1e-4f ? dv / dist : Vector3.up;
                if (l.spot && Vector3.Dot(-dir, l.dir) < Mathf.Cos(l.angle * 0.5f * Mathf.Deg2Rad)) continue;
                float ndl = Mathf.Max(0f, Vector3.Dot(nn, dir)) * 0.75f + 0.25f;
                acc = acc + l.c * (att * l.i * ndl);
            }
            var r = new Color(c.r * acc.r * 1.6f, c.g * acc.g * 1.6f, c.b * acc.b * 1.6f, c.a);
            float fd = Vector3.Distance(p, eye);
            float fog = Mathf.Clamp01((fd - fogS) / (fogE - fogS));
            return Color.Lerp(r, fogC, fog) is var x ? new Color(x.r, x.g, x.b, c.a) : r;
        }
        for (int t = 0; t < s.Tris; t++)
        {
            byte kind = s.Kind[t];
            int i0 = s.T[t * 3], i1 = s.T[t * 3 + 1], i2 = s.T[t * 3 + 2];
            foreach (int i in new[] { i0, i1, i2 })
                if (!done[i]) { vs[i] = inv * (s.P[i] - eye); vcol[i] = Shade(i, kind); done[i] = true; }
            var poly = new List<(Vector3 v, Color c)> { (vs[i0], vcol[i0]), (vs[i1], vcol[i1]), (vs[i2], vcol[i2]) };
            if (poly.All(p => p.v.z < near)) continue;
            if (poly.Any(p => p.v.z < near)) poly = ClipNear(poly, near);
            if (poly.Count < 3) continue;
            var scr = poly.Select(p => (x: (p.v.x * f / aspect / p.v.z * 0.5f + 0.5f) * W, y: (0.5f - p.v.y * f / p.v.z * 0.5f) * H, z: p.v.z, c: p.c)).ToList();
            for (int k = 1; k + 1 < scr.Count; k++) Tri(scr[0], scr[k], scr[k + 1], kind, color, depth, W, H);
        }
        WritePng(path, color, W, H);
    }

    static List<(Vector3 v, Color c)> ClipNear(List<(Vector3 v, Color c)> poly, float near)
    {
        var o = new List<(Vector3, Color)>();
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Count];
            bool ain = a.v.z >= near, bin = b.v.z >= near;
            if (ain) o.Add(a);
            if (ain != bin)
            {
                float t = (near - a.v.z) / (b.v.z - a.v.z);
                o.Add((Vector3.LerpUnclamped(a.v, b.v, t), Color.Lerp(a.c, b.c, t)));
            }
        }
        return o;
    }

    static void Tri((float x, float y, float z, Color c) a, (float x, float y, float z, Color c) b, (float x, float y, float z, Color c) c, byte kind, float[] col, float[] dep, int W, int H)
    {
        float area = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        if (Mathf.Abs(area) < 1e-6f) return;
        // backface culling for opaque lit geometry (front faces are clockwise on screen with y down => area > 0)
        if (kind == 0 && area < 0) return;
        int x0 = Math.Max(0, (int)Math.Floor(Math.Min(a.x, Math.Min(b.x, c.x)))), x1 = Math.Min(W - 1, (int)Math.Ceiling(Math.Max(a.x, Math.Max(b.x, c.x))));
        int y0 = Math.Max(0, (int)Math.Floor(Math.Min(a.y, Math.Min(b.y, c.y)))), y1 = Math.Min(H - 1, (int)Math.Ceiling(Math.Max(a.y, Math.Max(b.y, c.y))));
        float iz0 = 1f / a.z, iz1 = 1f / b.z, iz2 = 1f / c.z;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float w0 = ((b.x - px) * (c.y - py) - (b.y - py) * (c.x - px)) / area;
                float w1 = ((c.x - px) * (a.y - py) - (c.y - py) * (a.x - px)) / area;
                float w2 = 1f - w0 - w1;
                if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                float iz = w0 * iz0 + w1 * iz1 + w2 * iz2;
                float z = 1f / iz;
                int idx = y * W + x;
                if (kind == 1 && ((x + y) & 1) == 0) continue;
                bool blend = kind == 2 || kind == 4;
                if (blend ? z > dep[idx] + 0.02f : z >= dep[idx]) continue;
                float wa = w0 * iz0 * z, wb = w1 * iz1 * z, wc = w2 * iz2 * z;
                float r = a.c.r * wa + b.c.r * wb + c.c.r * wc, g = a.c.g * wa + b.c.g * wb + c.c.g * wc, bl = a.c.b * wa + b.c.b * wb + c.c.b * wc;
                float al = a.c.a;
                if (kind == 2) { col[idx * 3] = col[idx * 3] * (1 - al) + r * al; col[idx * 3 + 1] = col[idx * 3 + 1] * (1 - al) + g * al; col[idx * 3 + 2] = col[idx * 3 + 2] * (1 - al) + bl * al; continue; }
                if (kind == 4) { col[idx * 3] += r * 0.5f; col[idx * 3 + 1] += g * 0.5f; col[idx * 3 + 2] += bl * 0.5f; continue; }
                dep[idx] = z;
                col[idx * 3] = r; col[idx * 3 + 1] = g; col[idx * 3 + 2] = bl;
            }
    }

    static void WritePng(string path, float[] rgb, int W, int H)
    {
        var raw = new byte[(W * 3 + 1) * H];
        for (int y = 0; y < H; y++)
        {
            raw[y * (W * 3 + 1)] = 0;
            for (int x = 0; x < W * 3; x++) raw[y * (W * 3 + 1) + 1 + x] = (byte)Math.Clamp((int)(Math.Pow(Math.Clamp(rgb[y * W * 3 + x], 0f, 1f), 1 / 1.2) * 255f), 0, 255);
        }
        using var fs = File.Create(path);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13];
        WriteBE(ihdr, 0, W); WriteBE(ihdr, 4, H); ihdr[8] = 8; ihdr[9] = 2;
        Chunk(fs, "IHDR", ihdr);
        using (var ms = new MemoryStream())
        {
            using (var z = new ZLibStream(ms, CompressionLevel.Fastest, true)) z.Write(raw, 0, raw.Length);
            Chunk(fs, "IDAT", ms.ToArray());
        }
        Chunk(fs, "IEND", new byte[0]);
    }

    static void WriteBE(byte[] b, int o, int v) { b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v; }

    static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4]; WriteBE(len, 0, data.Length); s.Write(len);
        var t = Encoding.ASCII.GetBytes(type);
        s.Write(t); s.Write(data);
        uint crc = Crc(t, 0xFFFFFFFFu); crc = Crc(data, crc) ^ 0xFFFFFFFFu;
        var c = new byte[4]; WriteBE(c, 0, (int)crc); s.Write(c);
    }

    static uint[] _crcTable;
    static uint Crc(byte[] d, uint c)
    {
        if (_crcTable == null)
        {
            _crcTable = new uint[256];
            for (uint n = 0; n < 256; n++) { uint k = n; for (int i = 0; i < 8; i++) k = (k & 1) != 0 ? 0xEDB88320u ^ (k >> 1) : k >> 1; _crcTable[n] = k; }
        }
        foreach (var b in d) c = _crcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c;
    }
}
