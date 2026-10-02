using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PrisonersOfOmar.Map
{
    /// <summary>
    /// Turns the NavPlan collected while building into the NavGraph, using physics queries against the freshly built
    /// colliders (World + Default only, so door leaves / gates are passable):
    ///  1. drop nodes without floor under them or overlapping solid geometry (fixed nodes: stairs, doorways are trusted)
    ///  2. auto-connect nodes on the same level closer than MaxEdge when 6 rays (0.4 m / 1.2 m high, 3 lanes) are clear
    ///     and the floor is continuous under the segment
    ///  3. add the explicit links (stairs, steps, doorways, shelter door)
    ///  4. keep only what is reachable from Omar's spawn (BFS), logging what was dropped
    /// </summary>
    internal static class NavBuilder
    {
        public const float MaxEdge = 7.6f, MaxDy = 0.3f, Lane = 0.3f;

        public static NavGraph Build(MapContext ctx)
        {
            Physics.SyncTransforms();
            int mask = Layers.Mask(Layers.World, Layers.Default);
            var plan = ctx.Nav;
            int n = plan.Pos.Count;
            var pos = new Vector3[n];
            var valid = new bool[n];
            var area = new string[n];
            int droppedFloor = 0, droppedSolid = 0;
            var log = new StringBuilder();
            for (int i = 0; i < n; i++)
            {
                var p = plan.Pos[i];
                area[i] = string.IsNullOrEmpty(plan.Area[i]) ? ctx.Data.AreaAt(p + Vector3.up * 0.5f) : plan.Area[i];
                if (plan.Fixed[i]) { pos[i] = p; valid[i] = true; continue; }
                if (!Physics.Raycast(p + Vector3.up * 0.6f, Vector3.down, out var hit, 1.2f, mask, QueryTriggerInteraction.Ignore) || Mathf.Abs(hit.point.y - p.y) > 0.3f)
                {
                    droppedFloor++;
                    if (droppedFloor <= 12) log.Append(" nofloor@").Append(p.ToString("F1"));
                    continue;
                }
                p.y = hit.point.y;
                if (Physics.CheckCapsule(p + Vector3.up * 0.45f, p + Vector3.up * 1.5f, 0.27f, mask, QueryTriggerInteraction.Ignore))
                {
                    droppedSolid++;
                    if (droppedSolid <= 40) log.Append(" solid@").Append(p.ToString("F1"));
                    continue;
                }
                pos[i] = p;
                valid[i] = true;
            }

            // auto edges (spatial hash)
            var edges = new List<Vector3Int>();
            var cells = new Dictionary<long, List<int>>();
            long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;
            for (int i = 0; i < n; i++)
            {
                if (!valid[i]) continue;
                int cx = Mathf.FloorToInt(pos[i].x / MaxEdge), cz = Mathf.FloorToInt(pos[i].z / MaxEdge);
                long k = Key(cx, cz);
                if (!cells.TryGetValue(k, out var l)) cells[k] = l = new List<int>();
                l.Add(i);
            }
            int tested = 0;
            for (int i = 0; i < n; i++)
            {
                if (!valid[i]) continue;
                int cx = Mathf.FloorToInt(pos[i].x / MaxEdge), cz = Mathf.FloorToInt(pos[i].z / MaxEdge);
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (!cells.TryGetValue(Key(cx + dx, cz + dz), out var l)) continue;
                        foreach (int j in l)
                        {
                            if (j <= i) continue;
                            Vector3 a = pos[i], b = pos[j];
                            if (Mathf.Abs(a.y - b.y) > MaxDy) continue;
                            float flat = GeoUtil.FlatDistance(a, b);
                            if (flat > MaxEdge || flat < 0.05f) continue;
                            tested++;
                            if (!Clear(a, b, mask)) continue;
                            edges.Add(new Vector3Int(i, j, GateCode(plan, a, b)));
                        }
                    }
            }
            foreach (var e in plan.Links)
                if (e.x >= 0 && e.y >= 0 && e.x < n && e.y < n && valid[e.x] && valid[e.y]) edges.Add(e);

            // adjacency + BFS from Omar's spawn
            var adj = new List<int>[n];
            for (int i = 0; i < n; i++) adj[i] = new List<int>();
            foreach (var e in edges) { adj[e.x].Add(e.y); adj[e.y].Add(e.x); }
            int start = -1; float best = float.MaxValue;
            var spawn = ctx.Data.OmarSpawn.position;
            for (int i = 0; i < n; i++)
            {
                if (!valid[i]) continue;
                Vector3 d = pos[i] - spawn; d.y *= 3f;
                if (d.sqrMagnitude < best) { best = d.sqrMagnitude; start = i; }
            }
            var reach = new bool[n];
            if (start >= 0)
            {
                var q = new Queue<int>();
                q.Enqueue(start); reach[start] = true;
                while (q.Count > 0)
                {
                    int c = q.Dequeue();
                    foreach (int nb in adj[c]) if (!reach[nb]) { reach[nb] = true; q.Enqueue(nb); }
                }
            }
            var graph = new NavGraph();
            var map = new int[n];
            int unreachable = 0;
            for (int i = 0; i < n; i++)
            {
                map[i] = -1;
                if (!valid[i]) continue;
                if (!reach[i])
                {
                    unreachable++;
                    if (unreachable <= 25) log.Append(" unreachable@").Append(pos[i].ToString("F1")).Append('[').Append(area[i]).Append(']');
                    continue;
                }
                map[i] = graph.AddNode(pos[i], area[i]);
            }
            int edgeCount = 0;
            foreach (var e in edges)
            {
                int a = map[e.x], b = map[e.y];
                if (a < 0 || b < 0) continue;
                graph.Connect(a, b, e.z);
                edgeCount++;
            }
            string summary = "[MapBuilder] nav: " + graph.Nodes.Count + " nodes, " + edgeCount + " edges (" + tested + " pairs tested), dropped "
                             + droppedFloor + " without floor, " + droppedSolid + " inside geometry, " + unreachable + " unreachable." + log;
            ctx.BuildLog.Add(summary);
            if (unreachable > 0 || droppedFloor > 0) Debug.LogWarning(summary);
            else Debug.Log(summary);
            return graph;
        }

        /// <summary>Walkable straight line between two floor points (same level).</summary>
        static bool Clear(Vector3 a, Vector3 b, int mask)
        {
            Vector3 d = b - a;
            Vector3 flat = new Vector3(d.x, 0, d.z);
            float len = flat.magnitude;
            Vector3 side = new Vector3(-flat.z, 0, flat.x) / len;
            float[] heights = { 0.4f, 1.2f };
            float[] lanes = { 0f, -Lane, Lane };
            foreach (float h in heights)
                foreach (float o in lanes)
                {
                    Vector3 from = a + Vector3.up * h + side * o;
                    Vector3 to = b + Vector3.up * h + side * o;
                    Vector3 r = to - from;
                    if (Physics.Raycast(from, r.normalized, r.magnitude, mask, QueryTriggerInteraction.Ignore)) return false;
                    if (Physics.Raycast(to, -r.normalized, r.magnitude, mask, QueryTriggerInteraction.Ignore)) return false;
                }
            int samples = Mathf.Max(1, Mathf.FloorToInt(len / 0.9f));
            for (int s = 1; s <= samples; s++)
            {
                float t = s / (samples + 1f);
                Vector3 p = Vector3.Lerp(a, b, t);
                if (!Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hit, 1.0f, mask, QueryTriggerInteraction.Ignore)) return false;
                if (Mathf.Abs(hit.point.y - p.y) > 0.35f) return false;
            }
            return true;
        }

        static int GateCode(NavPlan plan, Vector3 a, Vector3 b)
        {
            for (int i = 0; i < plan.GateLines.Count; i++)
            {
                var g = plan.GateLines[i].Key;
                if (SegmentsCross(new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(g.x, g.y), new Vector2(g.z, g.w)))
                    return plan.GateLines[i].Value;
            }
            return NavGraph.EdgeNone;
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static bool SegmentsCross(Vector2 p, Vector2 p2, Vector2 q, Vector2 q2)
        {
            Vector2 r = p2 - p, s = q2 - q;
            float den = Cross(r, s);
            if (Mathf.Abs(den) < 1e-6f) return false;
            float t = Cross(q - p, s) / den, u = Cross(q - p, r) / den;
            return t >= 0f && t <= 1f && u >= 0f && u <= 1f;
        }
    }
}
