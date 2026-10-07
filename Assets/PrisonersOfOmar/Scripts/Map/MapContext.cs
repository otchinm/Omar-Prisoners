using System.Collections.Generic;
using PrisonersOfOmar.Rendering;
using UnityEngine;

namespace PrisonersOfOmar.Map
{
    internal enum LightGroup
    {
        /// <summary>Not switchable (moon, fire, oil lantern).</summary>
        None = 0,
        /// <summary>Electric light: MapData.PowerLights (goes dark during outages).</summary>
        Power,
        /// <summary>Radio room light: MapData.RadioRoomLights (starts Off, turns on with the fuse).</summary>
        Radio,
    }

    /// <summary>Navigation plan collected while building; turned into the NavGraph by NavBuilder after all colliders exist.</summary>
    internal sealed class NavPlan
    {
        public readonly List<Vector3> Pos = new List<Vector3>();
        public readonly List<string> Area = new List<string>();
        /// <summary>Fixed nodes (stairs, steps, door sides) skip the "inside a collider" validation.</summary>
        public readonly List<bool> Fixed = new List<bool>();
        /// <summary>Explicit links (stairs, steps, doorways). Door = MapData.Doors index or a NavGraph.Edge* code, -1 = none.</summary>
        public readonly List<Vector3Int> Links = new List<Vector3Int>();
        /// <summary>Segments (XZ) that are gates: auto edges crossing them get the code.</summary>
        public readonly List<KeyValuePair<Vector4, int>> GateLines = new List<KeyValuePair<Vector4, int>>();

        public int Add(Vector3 p, string area, bool fixedNode = false)
        {
            Pos.Add(p); Area.Add(area ?? ""); Fixed.Add(fixedNode);
            return Pos.Count - 1;
        }

        public void Link(int a, int b, int door = -1)
        {
            if (a < 0 || b < 0 || a == b) return;
            Links.Add(new Vector3Int(a, b, door));
        }

        /// <summary>Chain of nodes linked in order (stairs).</summary>
        public void Chain(params int[] nodes)
        {
            for (int i = 0; i + 1 < nodes.Length; i++) Link(nodes[i], nodes[i + 1]);
        }

        public void GateLine(Vector2 a, Vector2 b, int code) => GateLines.Add(new KeyValuePair<Vector4, int>(new Vector4(a.x, a.y, b.x, b.y), code));
    }

    /// <summary>
    /// Shared state while building the map: the MapData being filled, the deterministic RNG streams,
    /// combined static MeshBuilders (flushed into GameObjects per section), and registration helpers.
    /// All positions are WORLD positions (the map root is expected to sit at the origin).
    /// </summary>
    internal sealed class MapContext
    {
        public readonly MapData Data;
        public readonly Transform Root;
        public readonly int Seed;
        public readonly Transform Static, Colliders, Dynamic, Lights, Triggers;
        public readonly NavPlan Nav = new NavPlan();

        sealed class Pending
        {
            public MeshBuilder Mb; public string Name; public Transform Parent; public int Layer; public string GlowGroup;
        }

        readonly List<Pending> _pending = new List<Pending>();
        readonly Dictionary<string, PsxLight> _glowProbe = new Dictionary<string, PsxLight>();
        readonly Dictionary<Material, Material> _glowOff = new Dictionary<Material, Material>();
        readonly Dictionary<string, MeshBuilder> _glowBuilders = new Dictionary<string, MeshBuilder>();
        readonly Dictionary<string, MeshBuilder> _shared = new Dictionary<string, MeshBuilder>();

        public int TotalVertices;
        public int RendererCount;
        public readonly List<string> BuildLog = new List<string>();

        public MapContext(MapData data, int seed)
        {
            Data = data; Root = data.Root; Seed = seed;
            Static = GeoUtil.CreateChild(Root, "Static");
            Colliders = GeoUtil.CreateChild(Root, "Colliders");
            Dynamic = GeoUtil.CreateChild(Root, "Dynamic");
            Lights = GeoUtil.CreateChild(Root, "Lights");
            Triggers = GeoUtil.CreateChild(Root, "Triggers");
        }

        public DeterministicRandom Rng(string purpose) => DeterministicRandom.For(Seed, purpose);

        // ------------------------------------------------------------------ static meshes

        /// <summary>New combined static mesh; becomes a GameObject on <see cref="Flush"/>.</summary>
        public MeshBuilder NewBuilder(string name, int layer = Layers.World, Transform parent = null)
        {
            var mb = new MeshBuilder();
            _pending.Add(new Pending { Mb = mb, Name = name, Parent = parent != null ? parent : Static, Layer = layer });
            return mb;
        }

        /// <summary>Named builder shared by several helpers until the next <see cref="Flush"/>.</summary>
        public MeshBuilder SharedBuilder(string name)
        {
            if (_shared.TryGetValue(name, out var mb)) return mb;
            mb = NewBuilder(name);
            _shared[name] = mb;
            return mb;
        }

        /// <summary>Emissive geometry (bulbs, lit windows, lamp lenses) that turns dark when the probe light of
        /// <paramref name="group"/> is switched off. One builder per group.</summary>
        public MeshBuilder GlowBuilder(string group)
        {
            if (_glowBuilders.TryGetValue(group, out var mb)) return mb;
            mb = new MeshBuilder();
            _glowBuilders[group] = mb;
            _pending.Add(new Pending { Mb = mb, Name = "Glow_" + group, Parent = Static, Layer = Layers.World, GlowGroup = group });
            return mb;
        }

        /// <summary>Emissive material with a matching dark "power off" version.</summary>
        public Material GlowMat(string tex, Color tint, Color offTint)
        {
            var on = PsxMaterials.Get(tex, PsxSurface.Emissive, tint);
            if (!_glowOff.ContainsKey(on)) _glowOff[on] = PsxMaterials.Get(tex, PsxSurface.Lit, offTint);
            return on;
        }

        public Material GlowColor(Color tint) => GlowMat(null, tint, new Color(tint.r * 0.15f + 0.05f, tint.g * 0.15f + 0.05f, tint.b * 0.15f + 0.05f, 1f));

        public void SetGlowProbe(string group, PsxLight light)
        {
            if (light != null && !_glowProbe.ContainsKey(group)) _glowProbe[group] = light;
        }

        /// <summary>Builds every pending MeshBuilder into a GameObject (skips empty ones).</summary>
        public void Flush()
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                var p = _pending[i];
                if (p.Mb == null || p.Mb.IsEmpty) continue;
                try
                {
                    var go = p.Mb.Build(p.Name, p.Parent, p.Layer);
                    TotalVertices += p.Mb.VertexCount;
                    RendererCount++;
                    BuildLog.Add(p.Name + ": " + p.Mb.VertexCount + " verts, " + p.Mb.UsedMaterials().Length + " mats");
                    if (p.GlowGroup != null)
                    {
                        var r = go.GetComponent<MeshRenderer>();
                        var on = r.sharedMaterials;
                        var off = new Material[on.Length];
                        for (int m = 0; m < on.Length; m++) off[m] = on[m] != null && _glowOff.TryGetValue(on[m], out var o) ? o : on[m];
                        var pg = go.AddComponent<PoweredGlow>();
                        _glowProbe.TryGetValue(p.GlowGroup, out var probe);
                        pg.Setup(probe, r, on, off);
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError("[MapBuilder] mesh '" + p.Name + "' failed: " + e);
                }
            }
            _pending.Clear();
            _glowBuilders.Clear();
            _shared.Clear();
        }

        /// <summary>Registers a renderer built outside of the pending list (dynamic parts) for statistics.</summary>
        public void CountRenderer(MeshBuilder mb)
        {
            if (mb == null) return;
            TotalVertices += mb.VertexCount;
            RendererCount++;
        }

        // ------------------------------------------------------------------ colliders

        public BoxCollider Solid(Vector3 center, Vector3 size, SurfaceType surface = SurfaceType.Default, string name = "Col")
            => GeoUtil.AddBox(Colliders, center, size, Quaternion.identity, Layers.World, surface, false, name);

        public BoxCollider Solid(Vector3 center, Vector3 size, Quaternion rot, SurfaceType surface = SurfaceType.Default, string name = "Col")
            => GeoUtil.AddBox(Colliders, center, size, rot, Layers.World, surface, false, name);

        public BoxCollider SolidMinMax(Vector3 min, Vector3 max, SurfaceType surface = SurfaceType.Default, string name = "Col")
        {
            Vector3 a = Vector3.Min(min, max), b = Vector3.Max(min, max);
            return Solid((a + b) * 0.5f, b - a, surface, name);
        }

        /// <summary>Rotated (yaw) box given in a prop's local space.</summary>
        public BoxCollider SolidLocal(Vector3 propPos, float yaw, Vector3 localCenter, Vector3 size, SurfaceType surface = SurfaceType.Default, string name = "Col")
        {
            Quaternion r = MapMath.Yaw(yaw);
            return Solid(propPos + r * localCenter, size, r, surface, name);
        }

        /// <summary>Interaction trigger (Layers.Interactable).</summary>
        public BoxCollider Interact(Transform parent, Vector3 worldCenter, Vector3 size, Quaternion rot, string name)
        {
            var bc = GeoUtil.AddBox(parent != null ? parent : Triggers, Vector3.zero, size, Quaternion.identity, Layers.Interactable, SurfaceType.Default, true, name);
            bc.transform.position = worldCenter;
            bc.transform.rotation = rot;
            return bc;
        }

        // ------------------------------------------------------------------ lights

        public PsxLight Light(Vector3 pos, Color color, float intensity, float range, PsxFlicker flicker, LightGroup group,
            string name, float flickerAmount = 0.3f, float flickerSpeed = 8f, string glowGroup = null)
        {
            var l = PsxLight.Create(Lights, pos, color, intensity, range, flicker, name);
            l.FlickerAmount = flickerAmount;
            l.FlickerSpeed = flickerSpeed;
            if (group == LightGroup.Power) Data.PowerLights.Add(l);
            else if (group == LightGroup.Radio) { l.On = false; Data.RadioRoomLights.Add(l); }
            if (glowGroup != null) SetGlowProbe(glowGroup, l);
            return l;
        }

        public PsxLight Spot(Vector3 pos, Vector3 dir, Color color, float intensity, float range, float angle, PsxFlicker flicker,
            LightGroup group, string name, float flickerAmount = 0.2f, string glowGroup = null)
        {
            var l = PsxLight.CreateSpot(Lights, pos, Quaternion.LookRotation(dir.normalized, Vector3.up), color, intensity, range, angle, flicker, name);
            l.FlickerAmount = flickerAmount;
            if (group == LightGroup.Power) Data.PowerLights.Add(l);
            else if (group == LightGroup.Radio) { l.On = false; Data.RadioRoomLights.Add(l); }
            if (glowGroup != null) SetGlowProbe(glowGroup, l);
            return l;
        }

        // ------------------------------------------------------------------ gameplay registration

        public void Item(string area, Vector3 surfacePoint, ItemSpawnTier tier, float yaw = float.NaN, bool small = false)
        {
            if (float.IsNaN(yaw)) yaw = Mathf.Floor(Shade.Hash(surfacePoint, 11) * 8f) * 45f;
            Data.ItemSpawns.Add(new ItemSpawnInfo { Position = surfacePoint, Yaw = yaw, Area = area, Tier = tier, Small = small });
        }

        public void Key(string area, Vector3 p) => Item(area, p, ItemSpawnTier.Key);
        public void Common(string area, Vector3 p) => Item(area, p, ItemSpawnTier.Common);
        /// <summary>A place where the single revolver may lie (one of these is chosen per match).</summary>
        public void Gun(string area, Vector3 p, float yaw = 0f)
            => Data.GunSpots.Add(new ItemSpawnInfo { Position = p, Yaw = yaw, Area = area, Tier = ItemSpawnTier.Key });

        /// <summary>(iteration 3) A prepared place for the home video tape (<see cref="TapeSpotInfo"/>); <paramref name="difficulties"/> =
        /// bit (1 &lt;&lt; (int)Difficulty) per difficulty it may be picked on.</summary>
        public void TapeSpot(string kind, string area, Vector3 p, float yaw, int difficulties, bool drawer = false)
            => Data.TapeSpots.Add(new TapeSpotInfo { Kind = kind, Area = area, Position = p, Yaw = yaw, Difficulties = difficulties, Drawer = drawer });

        public void Tripwire(string area, Vector3 a, Vector3 b)
            => Data.TrapSpots.Add(new TrapSpotInfo { Kind = TrapKind.Tripwire, A = a, B = b, Area = area });

        public void BearTrap(string area, Vector3 p)
            => Data.TrapSpots.Add(new TrapSpotInfo { Kind = TrapKind.BearTrap, A = p, B = p, Area = area });

        /// <summary>Note on a wall (forward = out of the wall) or lying on furniture (forward = up).</summary>
        public void WallNote(string area, Vector3 p, Vector3 outward)
            => Data.NoteSpots.Add(new NoteSpotInfo { Position = p, Rotation = Quaternion.LookRotation(outward.normalized, Vector3.up), OnWall = true, Area = area });

        public void DeskNote(string area, Vector3 p, float yaw = 0f)
            => Data.NoteSpots.Add(new NoteSpotInfo { Position = p, Rotation = Quaternion.LookRotation(Vector3.up, MapMath.Yaw(yaw) * Vector3.forward), OnWall = false, Area = area });

        public Transform Marker(string name, Vector3 pos, Quaternion rot)
        {
            var t = GeoUtil.CreateChild(Root, "Marker_" + name, pos, rot, Layers.Default);
            Data.Markers[name] = t;
            return t;
        }

        public void Emitter(Vector3 p, string clip, float volume, float maxDistance, bool needsPower)
            => Data.SoundEmitters.Add(new SoundEmitterInfo { Position = p, Clip = clip, Volume = volume, MaxDistance = maxDistance, NeedsPower = needsPower });

        public void Ambient(AmbientType type, Vector3 min, Vector3 max)
            => Data.AmbientZones.Add(new AmbientZoneInfo { Type = type, Bounds = MapMath.MinMax(min, max) });

        public void Anomaly(string name, Vector3 center, float radius, float strength)
            => Data.AnomalyZones.Add(new AnomalyZoneInfo { Name = name, Center = center, Radius = radius, Strength = strength });

        public void Spectator(Vector3 eye, Vector3 target) => Data.SpectatorCameras.Add(MapMath.LookPose(eye, target));

        /// <summary>Registers a named area's bounds (register specific rooms before the zones containing them).</summary>
        /// <summary>(iteration 2) Registers a room prisoners can start / be locked up in; returns its index for <see cref="Dyn.Cage"/>.</summary>
        public int CellRoom(string name, string area, Vector3 center)
        {
            Data.CellRooms.Add(new CellRoomInfo { Name = name, Area = area, Center = center });
            return Data.CellRooms.Count - 1;
        }

        public void Area(string name, Vector3 min, Vector3 max)
        {
            if (Data.AreaBounds.ContainsKey(name))
            {
                var b = Data.AreaBounds[name];
                b.Encapsulate(MapMath.MinMax(min, max));
                Data.AreaBounds[name] = b;
                return;
            }
            Data.AreaBounds[name] = MapMath.MinMax(min, max);
            Data.AreaOrder.Add(name);
        }
    }
}
