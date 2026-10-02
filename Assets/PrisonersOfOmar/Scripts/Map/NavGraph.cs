using System.Collections.Generic;
using UnityEngine;

namespace PrisonersOfOmar.Map
{
    /// <summary>
    /// Waypoint graph used by the AI Omar. Nodes are at floor level (feet). Edges are walkable straight lines
    /// (doors count as passable - the AI opens them).
    /// Built locally by every peer (only the host's AI uses it): it is NOT part of the networked map identity.
    /// </summary>
    public sealed class NavGraph
    {
        /// <summary>Codes stored in <see cref="EdgeDoors"/> for edges that cross something other than a MapData.Doors entry.</summary>
        public const int EdgeNone = -1, EdgeMainGate = -2, EdgeVehicleGate = -3, EdgeShelter = -4, EdgeBreach = -5;

        public readonly List<Vector3> Nodes = new List<Vector3>();
        public readonly List<List<int>> Edges = new List<List<int>>();
        /// <summary>Optional area name per node (same strings as ItemSpawnInfo.Area) for patrol variety.</summary>
        public readonly List<string> NodeAreas = new List<string>();
        /// <summary>
        /// (addition) Edges that pass through a doorway: key = <see cref="EdgeKey"/>, value = index in MapData.Doors, or one of
        /// EdgeMainGate / EdgeVehicleGate / EdgeShelter (only walkable once that gate / door is open).
        /// </summary>
        public readonly Dictionary<long, int> EdgeDoors = new Dictionary<long, int>();

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

        /// <summary>(addition) Connect and remember which door / gate the edge passes through.</summary>
        public void Connect(int a, int b, int door)
        {
            Connect(a, b);
            if (door != EdgeNone && a != b && a >= 0 && b >= 0) EdgeDoors[EdgeKey(a, b)] = door;
        }

        public static long EdgeKey(int a, int b)
        {
            int lo = a < b ? a : b, hi = a < b ? b : a;
            return ((long)lo << 32) | (uint)hi;
        }

        /// <summary>(addition) Door index (MapData.Doors) or Edge* code crossed by the edge a-b, EdgeNone if none.</summary>
        public int DoorOnEdge(int a, int b) => EdgeDoors.TryGetValue(EdgeKey(a, b), out int d) ? d : EdgeNone;

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

        /// <summary>(addition) All node indices whose area equals <paramref name="area"/> (or starts with it when it ends with '.').</summary>
        public List<int> NodesInArea(string area)
        {
            var list = new List<int>();
            bool prefix = area != null && area.EndsWith(".");
            for (int i = 0; i < NodeAreas.Count; i++)
                if (prefix ? NodeAreas[i].StartsWith(area) : NodeAreas[i] == area) list.Add(i);
            return list;
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

        // binary heap of (f, node)
        readonly List<KeyValuePair<float, int>> _heap = new List<KeyValuePair<float, int>>();

        void Push(float f, int n)
        {
            _heap.Add(new KeyValuePair<float, int>(f, n));
            int i = _heap.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (_heap[p].Key <= _heap[i].Key) break;
                var t = _heap[p]; _heap[p] = _heap[i]; _heap[i] = t;
                i = p;
            }
        }

        int Pop()
        {
            int top = _heap[0].Value;
            int last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < _heap.Count && _heap[l].Key < _heap[m].Key) m = l;
                if (r < _heap.Count && _heap[r].Key < _heap[m].Key) m = r;
                if (m == i) break;
                var t = _heap[m]; _heap[m] = _heap[i]; _heap[i] = t;
                i = m;
            }
            return top;
        }

        public List<int> FindNodePath(int start, int goal)
        {
            int n = Nodes.Count;
            if (start < 0 || goal < 0 || start >= n || goal >= n) return null;
            var gScore = new float[n];
            var came = new int[n];
            var closed = new bool[n];
            for (int i = 0; i < n; i++) { gScore[i] = float.MaxValue; came[i] = -1; }
            gScore[start] = 0;
            _heap.Clear();
            Push(Vector3.Distance(Nodes[start], Nodes[goal]), start);
            while (_heap.Count > 0)
            {
                int cur = Pop();
                if (closed[cur]) continue;
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
                        Push(tg + Vector3.Distance(Nodes[nb], Nodes[goal]), nb);
                    }
                }
            }
            return null;
        }
    }
}
