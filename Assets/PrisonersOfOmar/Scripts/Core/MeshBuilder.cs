using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PrisonersOfOmar
{
    public enum BoxUV
    {
        /// <summary>Each face tiles by its own size / metersPerTile, starting at 0.</summary>
        Local = 0,
        /// <summary>Each face gets the full 0..1 texture.</summary>
        PerFace,
        /// <summary>UVs come from world-space position projected on the face axis: seamless across adjacent pieces.</summary>
        WorldAligned,
    }

    [Flags]
    public enum BoxFaces
    {
        None = 0,
        PosX = 1, NegX = 2, PosY = 4, NegY = 8, PosZ = 16, NegZ = 32,
        All = 63,
        Sides = PosX | NegX | PosZ | NegZ,
        NoBottom = All & ~NegY,
        NoTopBottom = Sides,
    }

    /// <summary>Per-face UV rectangles for <see cref="MeshBuilder.AddBox(Vector3,Vector3,BoxUVRects)"/>.</summary>
    public struct BoxUVRects
    {
        public Rect PosX, NegX, PosY, NegY, PosZ, NegZ;

        public static BoxUVRects All(Rect r) => new BoxUVRects { PosX = r, NegX = r, PosY = r, NegY = r, PosZ = r, NegZ = r };

        /// <summary>Front (-Z, faces the viewer standing in front of an unrotated prop) gets <paramref name="front"/>, everything else <paramref name="other"/>.</summary>
        public static BoxUVRects Front(Rect front, Rect other) => new BoxUVRects { PosX = other, NegX = other, PosY = other, NegY = other, PosZ = other, NegZ = front };
    }

    /// <summary>
    /// Accumulates low-poly geometry (one submesh per material) with a transform stack.
    /// Conventions (Unity): left-handed, Y up, front faces are CLOCKWISE when seen from the front.
    /// Quads are given as a,b,c,d = bottom-left, top-left, top-right, bottom-right as seen from the front;
    /// their default UVs are (0,0),(0,1),(1,1),(1,0).
    /// Every Add* call is transformed by the current matrix (see <see cref="Push(Vector3,Quaternion)"/>).
    /// </summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> _verts = new List<Vector3>(1024);
        readonly List<Vector3> _normals = new List<Vector3>(1024);
        readonly List<Vector2> _uvs = new List<Vector2>(1024);
        readonly List<Color32> _colors = new List<Color32>(1024);
        readonly List<Material> _materials = new List<Material>();
        readonly List<List<int>> _indices = new List<List<int>>();
        readonly Stack<Matrix4x4> _stack = new Stack<Matrix4x4>();
        Matrix4x4 _matrix = Matrix4x4.identity;
        Matrix4x4 _normalMatrix = Matrix4x4.identity;
        int _current = -1;

        /// <summary>Vertex color applied to vertices added from now on.</summary>
        public Color32 Color = new Color32(255, 255, 255, 255);

        public int VertexCount => _verts.Count;
        public bool IsEmpty => _verts.Count == 0;
        public IReadOnlyList<Material> Materials => _materials;
        public Matrix4x4 Matrix => _matrix;

        /// <summary>Select the material (submesh) that subsequent triangles go to.</summary>
        public Material Material
        {
            get => _current >= 0 ? _materials[_current] : null;
            set => SetMaterial(value);
        }

        public MeshBuilder SetMaterial(Material m)
        {
            int idx = _materials.IndexOf(m);
            if (idx < 0)
            {
                _materials.Add(m);
                _indices.Add(new List<int>(256));
                idx = _materials.Count - 1;
            }
            _current = idx;
            return this;
        }

        public MeshBuilder SetColor(Color c) { Color = c; return this; }

        // ------------------------------------------------------------------ transform stack

        public void PushMatrix(Matrix4x4 m)
        {
            _stack.Push(_matrix);
            _matrix = _matrix * m;
            _normalMatrix = _matrix.inverse.transpose;
        }

        public void Push(Vector3 position, Quaternion rotation) => PushMatrix(Matrix4x4.TRS(position, rotation, Vector3.one));
        public void Push(Vector3 position, Quaternion rotation, Vector3 scale) => PushMatrix(Matrix4x4.TRS(position, rotation, scale));
        public void Push(Vector3 position) => PushMatrix(Matrix4x4.Translate(position));

        public void PopMatrix()
        {
            _matrix = _stack.Count > 0 ? _stack.Pop() : Matrix4x4.identity;
            _normalMatrix = _matrix.inverse.transpose;
        }

        public void Pop() => PopMatrix();

        public Vector3 TransformPoint(Vector3 p) => _matrix.MultiplyPoint3x4(p);

        // ------------------------------------------------------------------ primitives

        public int AddVertex(Vector3 p, Vector3 n, Vector2 uv) => AddVertex(p, n, uv, Color);

        public int AddVertex(Vector3 p, Vector3 n, Vector2 uv, Color32 c)
        {
            _verts.Add(_matrix.MultiplyPoint3x4(p));
            Vector3 wn = _normalMatrix.MultiplyVector(n);
            float len = wn.magnitude;
            _normals.Add(len > 1e-6f ? wn / len : Vector3.up);
            _uvs.Add(uv);
            _colors.Add(c);
            return _verts.Count - 1;
        }

        /// <summary>Add a vertex whose position is already in builder-output space (no matrix applied).</summary>
        public int AddRawVertex(Vector3 p, Vector3 n, Vector2 uv, Color32 c)
        {
            _verts.Add(p); _normals.Add(n); _uvs.Add(uv); _colors.Add(c);
            return _verts.Count - 1;
        }

        public void AddTriangle(int a, int b, int c)
        {
            if (_current < 0) throw new InvalidOperationException("MeshBuilder: call SetMaterial before adding geometry");
            var list = _indices[_current];
            list.Add(a); list.Add(b); list.Add(c);
        }

        /// <summary>Flat shaded triangle, clockwise when seen from the front.</summary>
        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc)
        {
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int i0 = AddVertex(a, n, ua), i1 = AddVertex(b, n, ub), i2 = AddVertex(c, n, uc);
            AddTriangle(i0, i1, i2);
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            => AddQuad(a, b, c, d, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0));

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Rect uv)
            => AddQuad(a, b, c, d, new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMax, uv.yMin));

        /// <summary>Flat shaded quad. a=bottom-left, b=top-left, c=top-right, d=bottom-right seen from the front.</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(c - a, d - a);
            n.Normalize();
            int i0 = AddVertex(a, n, ua), i1 = AddVertex(b, n, ub), i2 = AddVertex(c, n, uc), i3 = AddVertex(d, n, ud);
            AddTriangle(i0, i1, i2);
            AddTriangle(i0, i2, i3);
        }

        /// <summary>Quad visible from both sides (two quads with opposite normals).</summary>
        public void AddQuadDoubleSided(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Rect uv)
        {
            AddQuad(a, b, c, d, uv);
            // back side: mirror horizontally so the texture is not reversed when seen from behind
            AddQuad(d, c, b, a, new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMin, uv.yMax), new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMax, uv.yMin));
        }

        /// <summary>
        /// Subdivided plane. <paramref name="origin"/> is the bottom-left corner, <paramref name="right"/> and
        /// <paramref name="up"/> span the full extent. The front face direction is Cross(up, right).
        /// Subdivision matters: lighting is per vertex (PS1 style), so large surfaces need a grid.
        /// </summary>
        public void AddGrid(Vector3 origin, Vector3 right, Vector3 up, int segX, int segY, Vector2 uvMin, Vector2 uvMax)
        {
            segX = Mathf.Max(1, segX); segY = Mathf.Max(1, segY);
            Vector3 n = Vector3.Cross(up, right).normalized;
            int start = _verts.Count;
            for (int y = 0; y <= segY; y++)
            {
                float fy = (float)y / segY;
                for (int x = 0; x <= segX; x++)
                {
                    float fx = (float)x / segX;
                    Vector3 p = origin + right * fx + up * fy;
                    Vector2 uv = new Vector2(Mathf.Lerp(uvMin.x, uvMax.x, fx), Mathf.Lerp(uvMin.y, uvMax.y, fy));
                    AddVertex(p, n, uv);
                }
            }
            int row = segX + 1;
            for (int y = 0; y < segY; y++)
                for (int x = 0; x < segX; x++)
                {
                    int a = start + y * row + x;      // bottom-left
                    int b = a + row;                  // top-left
                    int c = b + 1;                    // top-right
                    int d = a + 1;                    // bottom-right
                    AddTriangle(a, b, c);
                    AddTriangle(a, c, d);
                }
        }

        /// <summary>
        /// Subdivided plane with UVs in "tiles": the texture repeats every <paramref name="metersPerTile"/> meters.
        /// Segments are chosen so no cell is longer than <paramref name="maxSegment"/> meters.
        /// </summary>
        public void AddPlane(Vector3 origin, Vector3 right, Vector3 up, float metersPerTile = 2f, float maxSegment = 1.5f, Vector2 uvOffset = default)
        {
            float w = right.magnitude, h = up.magnitude;
            int sx = maxSegment > 0 ? Mathf.Max(1, Mathf.CeilToInt(w / maxSegment)) : 1;
            int sy = maxSegment > 0 ? Mathf.Max(1, Mathf.CeilToInt(h / maxSegment)) : 1;
            float mpt = Mathf.Max(0.01f, metersPerTile);
            AddGrid(origin, right, up, sx, sy, uvOffset, uvOffset + new Vector2(w / mpt, h / mpt));
        }

        /// <summary>Axis aligned (in local/builder space) box with tiling UVs.</summary>
        public void AddBox(Vector3 center, Vector3 size, BoxUV uvMode = BoxUV.Local, float metersPerTile = 1f, float maxSegment = 0f, BoxFaces faces = BoxFaces.All)
        {
            Vector3 h = size * 0.5f;
            float mpt = Mathf.Max(0.01f, metersPerTile);
            // +X face: seen from +X, right = -Z... (origin bottom-left as seen from outside)
            if ((faces & BoxFaces.PosX) != 0) BoxFace(center + new Vector3(h.x, -h.y, -h.z), new Vector3(0, 0, size.z), new Vector3(0, size.y, 0), uvMode, mpt, maxSegment);
            if ((faces & BoxFaces.NegX) != 0) BoxFace(center + new Vector3(-h.x, -h.y, h.z), new Vector3(0, 0, -size.z), new Vector3(0, size.y, 0), uvMode, mpt, maxSegment);
            if ((faces & BoxFaces.PosZ) != 0) BoxFace(center + new Vector3(h.x, -h.y, h.z), new Vector3(-size.x, 0, 0), new Vector3(0, size.y, 0), uvMode, mpt, maxSegment);
            if ((faces & BoxFaces.NegZ) != 0) BoxFace(center + new Vector3(-h.x, -h.y, -h.z), new Vector3(size.x, 0, 0), new Vector3(0, size.y, 0), uvMode, mpt, maxSegment);
            if ((faces & BoxFaces.PosY) != 0) BoxFace(center + new Vector3(-h.x, h.y, -h.z), new Vector3(size.x, 0, 0), new Vector3(0, 0, size.z), uvMode, mpt, maxSegment);
            if ((faces & BoxFaces.NegY) != 0) BoxFace(center + new Vector3(-h.x, -h.y, h.z), new Vector3(size.x, 0, 0), new Vector3(0, 0, -size.z), uvMode, mpt, maxSegment);
        }

        /// <summary>Box with an explicit UV rectangle per face (e.g. a label on the front).</summary>
        public void AddBox(Vector3 center, Vector3 size, BoxUVRects uv, BoxFaces faces = BoxFaces.All)
        {
            Vector3 h = size * 0.5f;
            if ((faces & BoxFaces.PosX) != 0) RectFace(center + new Vector3(h.x, -h.y, -h.z), new Vector3(0, 0, size.z), new Vector3(0, size.y, 0), uv.PosX);
            if ((faces & BoxFaces.NegX) != 0) RectFace(center + new Vector3(-h.x, -h.y, h.z), new Vector3(0, 0, -size.z), new Vector3(0, size.y, 0), uv.NegX);
            if ((faces & BoxFaces.PosZ) != 0) RectFace(center + new Vector3(h.x, -h.y, h.z), new Vector3(-size.x, 0, 0), new Vector3(0, size.y, 0), uv.PosZ);
            if ((faces & BoxFaces.NegZ) != 0) RectFace(center + new Vector3(-h.x, -h.y, -h.z), new Vector3(size.x, 0, 0), new Vector3(0, size.y, 0), uv.NegZ);
            if ((faces & BoxFaces.PosY) != 0) RectFace(center + new Vector3(-h.x, h.y, -h.z), new Vector3(size.x, 0, 0), new Vector3(0, 0, size.z), uv.PosY);
            if ((faces & BoxFaces.NegY) != 0) RectFace(center + new Vector3(-h.x, -h.y, h.z), new Vector3(size.x, 0, 0), new Vector3(0, 0, -size.z), uv.NegY);
        }

        void RectFace(Vector3 origin, Vector3 right, Vector3 up, Rect uv)
        {
            AddQuad(origin, origin + up, origin + up + right, origin + right, uv);
        }

        void BoxFace(Vector3 origin, Vector3 right, Vector3 up, BoxUV mode, float mpt, float maxSeg)
        {
            float w = right.magnitude, hgt = up.magnitude;
            int sx = maxSeg > 0 ? Mathf.Max(1, Mathf.CeilToInt(w / maxSeg)) : 1;
            int sy = maxSeg > 0 ? Mathf.Max(1, Mathf.CeilToInt(hgt / maxSeg)) : 1;
            switch (mode)
            {
                case BoxUV.PerFace:
                    AddGrid(origin, right, up, sx, sy, Vector2.zero, Vector2.one);
                    break;
                case BoxUV.Local:
                    AddGrid(origin, right, up, sx, sy, Vector2.zero, new Vector2(w / mpt, hgt / mpt));
                    break;
                default:
                    WorldAlignedGrid(origin, right, up, sx, sy, mpt);
                    break;
            }
        }

        void WorldAlignedGrid(Vector3 origin, Vector3 right, Vector3 up, int sx, int sy, float mpt)
        {
            Vector3 nLocal = Vector3.Cross(up, right).normalized;
            Vector3 nWorld = _normalMatrix.MultiplyVector(nLocal).normalized;
            float ax = Mathf.Abs(nWorld.x), ay = Mathf.Abs(nWorld.y), az = Mathf.Abs(nWorld.z);
            int start = _verts.Count;
            for (int y = 0; y <= sy; y++)
            {
                float fy = (float)y / sy;
                for (int x = 0; x <= sx; x++)
                {
                    float fx = (float)x / sx;
                    Vector3 p = origin + right * fx + up * fy;
                    Vector3 w = _matrix.MultiplyPoint3x4(p);
                    Vector2 uv;
                    if (ax >= ay && ax >= az) uv = new Vector2(nWorld.x > 0 ? -w.z : w.z, w.y);
                    else if (az >= ay) uv = new Vector2(nWorld.z > 0 ? w.x : -w.x, w.y);
                    else uv = new Vector2(w.x, w.z);
                    AddVertex(p, nLocal, uv / mpt);
                }
            }
            int row = sx + 1;
            for (int y = 0; y < sy; y++)
                for (int x = 0; x < sx; x++)
                {
                    int a = start + y * row + x, b = a + row, c = b + 1, d = a + 1;
                    AddTriangle(a, b, c);
                    AddTriangle(a, c, d);
                }
        }

        /// <summary>
        /// Cylinder / cone frustum standing on <paramref name="bottomCenter"/> along local +Y.
        /// UV: u wraps around (uvRect.x..xMax), v goes bottom..top (uvRect.y..yMax).
        /// Caps use a planar mapping into the same rect.
        /// </summary>
        public void AddCylinder(Vector3 bottomCenter, float radiusBottom, float radiusTop, float height, int sides,
            bool capTop = true, bool capBottom = false, Rect? uvRect = null, bool smooth = true, int heightSegments = 1)
        {
            sides = Mathf.Max(3, sides);
            heightSegments = Mathf.Max(1, heightSegments);
            Rect r = uvRect ?? new Rect(0, 0, 1, 1);
            float slope = (radiusBottom - radiusTop) / Mathf.Max(0.0001f, height);
            if (smooth)
            {
                int start = _verts.Count;
                for (int s = 0; s <= heightSegments; s++)
                {
                    float fy = (float)s / heightSegments;
                    float rad = Mathf.Lerp(radiusBottom, radiusTop, fy);
                    for (int i = 0; i <= sides; i++)
                    {
                        float a = (float)i / sides * Mathf.PI * 2f;
                        Vector3 dir = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
                        Vector3 n = new Vector3(dir.x, slope, dir.z).normalized;
                        AddVertex(bottomCenter + dir * rad + Vector3.up * (height * fy), n,
                            new Vector2(Mathf.Lerp(r.xMin, r.xMax, (float)i / sides), Mathf.Lerp(r.yMin, r.yMax, fy)));
                    }
                }
                int row = sides + 1;
                for (int s = 0; s < heightSegments; s++)
                    for (int i = 0; i < sides; i++)
                    {
                        int a = start + s * row + i, b = a + row, c = b + 1, d = a + 1;
                        // angle increases to the viewer's right when seen from outside -> clockwise a,b,c
                        AddTriangle(a, b, c);
                        AddTriangle(a, c, d);
                    }
            }
            else
            {
                for (int i = 0; i < sides; i++)
                {
                    float a0 = (float)i / sides * Mathf.PI * 2f, a1 = (float)(i + 1) / sides * Mathf.PI * 2f;
                    Vector3 d0 = new Vector3(-Mathf.Sin(a0), 0, Mathf.Cos(a0)), d1 = new Vector3(-Mathf.Sin(a1), 0, Mathf.Cos(a1));
                    Vector3 b0 = bottomCenter + d0 * radiusBottom, b1 = bottomCenter + d1 * radiusBottom;
                    Vector3 t0 = bottomCenter + d0 * radiusTop + Vector3.up * height, t1 = bottomCenter + d1 * radiusTop + Vector3.up * height;
                    float u0 = Mathf.Lerp(r.xMin, r.xMax, (float)i / sides), u1 = Mathf.Lerp(r.xMin, r.xMax, (float)(i + 1) / sides);
                    AddQuad(b0, t0, t1, b1, new Vector2(u0, r.yMin), new Vector2(u0, r.yMax), new Vector2(u1, r.yMax), new Vector2(u1, r.yMin));
                }
            }
            if (capTop && radiusTop > 0.0001f) Cap(bottomCenter + Vector3.up * height, radiusTop, sides, true, r);
            if (capBottom && radiusBottom > 0.0001f) Cap(bottomCenter, radiusBottom, sides, false, r);
        }

        void Cap(Vector3 center, float radius, int sides, bool up, Rect r)
        {
            Vector3 n = up ? Vector3.up : Vector3.down;
            int c = AddVertex(center, n, r.center);
            int first = _verts.Count;
            for (int i = 0; i <= sides; i++)
            {
                float a = (float)i / sides * Mathf.PI * 2f;
                Vector3 dir = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
                AddVertex(center + dir * radius, n, new Vector2(r.center.x + dir.x * r.width * 0.5f, r.center.y + dir.z * r.height * 0.5f));
            }
            for (int i = 0; i < sides; i++)
            {
                if (up) AddTriangle(c, first + i + 1, first + i);
                else AddTriangle(c, first + i, first + i + 1);
            }
        }

        /// <summary>Low-poly UV sphere / ellipsoid.</summary>
        public void AddSphere(Vector3 center, Vector3 radii, int longitude = 8, int latitude = 6, Rect? uvRect = null)
        {
            longitude = Mathf.Max(3, longitude); latitude = Mathf.Max(2, latitude);
            Rect r = uvRect ?? new Rect(0, 0, 1, 1);
            int start = _verts.Count;
            for (int lat = 0; lat <= latitude; lat++)
            {
                float v = (float)lat / latitude;
                float theta = v * Mathf.PI; // 0 at bottom
                float y = -Mathf.Cos(theta), ring = Mathf.Sin(theta);
                for (int lon = 0; lon <= longitude; lon++)
                {
                    float u = (float)lon / longitude;
                    float phi = u * Mathf.PI * 2f;
                    Vector3 unit = new Vector3(-Mathf.Sin(phi) * ring, y, Mathf.Cos(phi) * ring);
                    Vector3 p = center + Vector3.Scale(unit, radii);
                    Vector3 n = new Vector3(unit.x / Mathf.Max(radii.x, 1e-4f), unit.y / Mathf.Max(radii.y, 1e-4f), unit.z / Mathf.Max(radii.z, 1e-4f)).normalized;
                    AddVertex(p, n, new Vector2(Mathf.Lerp(r.xMin, r.xMax, u), Mathf.Lerp(r.yMin, r.yMax, v)));
                }
            }
            int row = longitude + 1;
            for (int lat = 0; lat < latitude; lat++)
                for (int lon = 0; lon < longitude; lon++)
                {
                    int a = start + lat * row + lon, b = a + row, c = b + 1, d = a + 1;
                    AddTriangle(a, b, c);
                    AddTriangle(a, c, d);
                }
        }

        /// <summary>
        /// Vertical crossed quads (foliage / tree billboards). Each plane is double sided.
        /// </summary>
        public void AddCrossQuads(Vector3 baseCenter, float width, float height, int planes, Rect uv, float rotationDeg = 0f)
        {
            planes = Mathf.Max(1, planes);
            for (int i = 0; i < planes; i++)
            {
                float ang = (rotationDeg + 180f * i / planes) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * (width * 0.5f);
                Vector3 a = baseCenter - dir, d = baseCenter + dir;
                Vector3 b = a + Vector3.up * height, c = d + Vector3.up * height;
                AddQuadDoubleSided(a, b, c, d, uv);
            }
        }

        /// <summary>Square beam from a to b (posts, rails, wires, pipes).</summary>
        public void AddBeam(Vector3 a, Vector3 b, float thickness, BoxUV uvMode = BoxUV.Local, float metersPerTile = 1f)
        {
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 1e-5f) return;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, dir / len);
            Push((a + b) * 0.5f, rot);
            AddBox(Vector3.zero, new Vector3(thickness, len, thickness), uvMode, metersPerTile);
            Pop();
        }

        /// <summary>Append another mesh (all its submeshes go to the current material).</summary>
        public void Append(Mesh mesh, Matrix4x4 local)
        {
            if (mesh == null) return;
            PushMatrix(local);
            var v = mesh.vertices; var n = mesh.normals; var uv = mesh.uv; var col = mesh.colors32;
            int baseIndex = _verts.Count;
            for (int i = 0; i < v.Length; i++)
                AddVertex(v[i], n.Length > i ? n[i] : Vector3.up, uv.Length > i ? uv[i] : Vector2.zero, col.Length > i ? col[i] : Color);
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                for (int i = 0; i < tris.Length; i += 3) AddTriangle(baseIndex + tris[i], baseIndex + tris[i + 1], baseIndex + tris[i + 2]);
            }
            Pop();
        }

        // ------------------------------------------------------------------ output

        public Mesh ToMesh(string name = "mesh")
        {
            var mesh = new Mesh { name = name };
            if (_verts.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetColors(_colors);
            int used = 0;
            for (int i = 0; i < _indices.Count; i++) if (_indices[i].Count > 0) used++;
            mesh.subMeshCount = Mathf.Max(1, used);
            int sub = 0;
            for (int i = 0; i < _indices.Count; i++)
            {
                if (_indices[i].Count == 0) continue;
                mesh.SetTriangles(_indices[i], sub++, false);
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Materials matching the submeshes of <see cref="ToMesh"/> (empty submeshes skipped).</summary>
        public Material[] UsedMaterials()
        {
            var list = new List<Material>();
            for (int i = 0; i < _indices.Count; i++) if (_indices[i].Count > 0) list.Add(_materials[i]);
            return list.ToArray();
        }

        /// <summary>Create a GameObject with MeshFilter + MeshRenderer under <paramref name="parent"/> (world space geometry, identity transform).</summary>
        public GameObject Build(string name, Transform parent, int layer = Layers.World)
        {
            var go = new GameObject(name);
            go.layer = layer;
            if (parent != null) go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = ToMesh(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = UsedMaterials();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return go;
        }

        public void Clear()
        {
            _verts.Clear(); _normals.Clear(); _uvs.Clear(); _colors.Clear();
            _materials.Clear(); _indices.Clear(); _stack.Clear();
            _matrix = Matrix4x4.identity; _normalMatrix = Matrix4x4.identity;
            _current = -1;
            Color = new Color32(255, 255, 255, 255);
        }
    }
}
