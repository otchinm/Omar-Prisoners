using System.Collections.Generic;
using UnityEngine;

namespace PrisonersOfOmar.Map
{
    /// <summary>
    /// Waypoint graph used by the AI Omar. Nodes are at floor level (feet). Edges are walkable straight lines
    /// (doors count as passable - the AI opens them).
    /// </summary>
    public sealed class NavGraph
    {
        public readonly List<Vector3> Nodes = new List<Vector3>();
        public readonly List<List<int>> Edges = new List<List<int>>();
        /// <summary>Optional area name per node (same strings as ItemSpawnInfo.Area) for patrol variety.</summary>
        public readonly List<string> NodeAreas = new List<string>();

        public int AddNode(Vector3 p, string area = null)
        {
            Nodes.Add(p);
            Edges.Add(new List<int>());
            NodeAreas.Add(area ?? "");
            return Nodes.Count - 1;
        }

        public void Connect(int a, int b)
        {
            if (a == b || a < 0 || b < 0) return;
            if (!Edges[a].Contains(b)) Edges[a].Add(b);
            if (!Edges[b].Contains(a)) Edges[b].Add(a);
        }

        /// <summary>Closest node (3D distance, vertical difference weighted x3 so floors don't mix).</summary>
        public int Nearest(Vector3 p)
        {
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < Nodes.Count; i++)
            {
                Vector3 d = Nodes[i] - p; d.y *= 3f;
                float sq = d.sqrMagnitude;
                if (sq < bestD) { bestD = sq; best = i; }
            }
            return best;
        }

        /// <summary>A* path between the nodes nearest to from/to. Returns world points (excluding <paramref name="from"/>, ending at <paramref name="to"/>). Empty if unreachable.</summary>
        public List<Vector3> FindPath(Vector3 from, Vector3 to)
        {
            var result = new List<Vector3>();
            int s = Nearest(from), g = Nearest(to);
            if (s < 0 || g < 0) return result;
            var path = FindNodePath(s, g);
            if (path == null) return result;
            for (int i = 0; i < path.Count; i++) result.Add(Nodes[path[i]]);
            result.Add(to);
            return result;
        }

        public List<int> FindNodePath(int start, int goal)
        {
            int n = Nodes.Count;
            var gScore = new float[n];
            var came = new int[n];
            var closed = new bool[n];
            for (int i = 0; i < n; i++) { gScore[i] = float.MaxValue; came[i] = -1; }
            gScore[start] = 0;
            var open = new List<int> { start };
            while (open.Count > 0)
            {
                int bi = 0; float bf = float.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    int c = open[i];
                    float f = gScore[c] + Vector3.Distance(Nodes[c], Nodes[goal]);
                    if (f < bf) { bf = f; bi = i; }
                }
                int cur = open[bi];
                open.RemoveAt(bi);
                if (cur == goal)
                {
                    var p = new List<int>();
                    while (cur != -1) { p.Add(cur); cur = came[cur]; }
                    p.Reverse();
                    return p;
                }
                closed[cur] = true;
                var edges = Edges[cur];
                for (int e = 0; e < edges.Count; e++)
                {
                    int nb = edges[e];
                    if (closed[nb]) continue;
                    float tg = gScore[cur] + Vector3.Distance(Nodes[cur], Nodes[nb]);
                    if (tg < gScore[nb])
                    {
                        gScore[nb] = tg; came[nb] = cur;
                        if (!open.Contains(nb)) open.Add(nb);
                    }
                }
            }
            return null;
        }
    }
}
