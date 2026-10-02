// Minimal managed re-implementation of the UnityEngine math types used by Scripts/Characters, so the real
// character code can run offline (dotnet) for previews. Semantics follow Unity (left handed, Y up,
// Quaternion.Euler = Y * X * Z, column vectors, Matrix4x4.TRS = T * R * S). NOT shipped with the game.
using System;
using System.Globalization;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public float magnitude => Mathf.Sqrt(x * x + y * y);
        public float sqrMagnitude => x * x + y * y;
        public Vector2 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, float d) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator *(float d, Vector2 a) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator /(Vector2 a, float d) => new Vector2(a.x / d, a.y / d);
        public static bool operator ==(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 1e-10f;
        public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);
        public override bool Equals(object o) => o is Vector2 v && v == this;
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0);
        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { t = Mathf.Clamp01(t); return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t); }
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
        public float this[int i] { get => i == 0 ? x : y; set { if (i == 0) x = value; else y = value; } }
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:F2}, {1:F2})", x, y);
    }

    public struct Vector2Int
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
    }

    public struct Vector3Int
    {
        public int x, y, z;
        public Vector3Int(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 down => new Vector3(0, -1, 0);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 left => new Vector3(-1, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 back => new Vector3(0, 0, -1);
        public float magnitude => Mathf.Sqrt(x * x + y * y + z * z);
        public float sqrMagnitude => x * x + y * y + z * z;
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public void Normalize() { this = normalized; }
        public float this[int i]
        {
            get => i == 0 ? x : i == 1 ? y : z;
            set { if (i == 0) x = value; else if (i == 1) y = value; else z = value; }
        }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);
        public static bool operator ==(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-10f;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public override bool Equals(object o) => o is Vector3 v && v == this;
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2);
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => LerpUnclamped(a, b, Mathf.Clamp01(t));
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        public static Vector3 Normalize(Vector3 v) => v.normalized;
        public static Vector3 Project(Vector3 v, Vector3 n) { float s = Dot(n, n); return s < 1e-12f ? zero : n * (Dot(v, n) / s); }
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) => v - Project(v, n);
        public static float Angle(Vector3 a, Vector3 b)
        {
            float d = Mathf.Sqrt(a.sqrMagnitude * b.sqrMagnitude);
            if (d < 1e-15f) return 0;
            return Mathf.Acos(Mathf.Clamp(Dot(a, b) / d, -1, 1)) * Mathf.Rad2Deg;
        }
        public static float SignedAngle(Vector3 a, Vector3 b, Vector3 axis)
        {
            float u = Angle(a, b);
            float s = Mathf.Sign(Dot(axis, Cross(a, b)));
            return u * s;
        }
        public static Vector3 ClampMagnitude(Vector3 v, float m) => v.sqrMagnitude > m * m ? v.normalized * m : v;
        public static Vector3 Min(Vector3 a, Vector3 b) => new Vector3(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z));
        public static Vector3 Max(Vector3 a, Vector3 b) => new Vector3(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z));
        public static Vector3 MoveTowards(Vector3 c, Vector3 t, float d)
        {
            Vector3 v = t - c; float m = v.magnitude;
            if (m <= d || m == 0) return t;
            return c + v / m * d;
        }
        public static Vector3 Slerp(Vector3 a, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            float ma = a.magnitude, mb = b.magnitude;
            if (ma < 1e-6f || mb < 1e-6f) return LerpUnclamped(a, b, t);
            Vector3 na = a / ma, nb = b / mb;
            float dot = Mathf.Clamp(Dot(na, nb), -1, 1);
            float th = Mathf.Acos(dot) * t;
            Vector3 rel = (nb - na * dot);
            if (rel.sqrMagnitude < 1e-10f) return LerpUnclamped(a, b, t);
            rel.Normalize();
            return (na * Mathf.Cos(th) + rel * Mathf.Sin(th)) * Mathf.Lerp(ma, mb, t);
        }
        public static Vector3 SmoothDamp(Vector3 c, Vector3 t, ref Vector3 vel, float smoothTime, float maxSpeed, float dt)
        {
            float vx = vel.x, vy = vel.y, vz = vel.z;
            var r = new Vector3(Mathf.SmoothDamp(c.x, t.x, ref vx, smoothTime, maxSpeed, dt),
                Mathf.SmoothDamp(c.y, t.y, ref vy, smoothTime, maxSpeed, dt), Mathf.SmoothDamp(c.z, t.z, ref vz, smoothTime, maxSpeed, dt));
            vel = new Vector3(vx, vy, vz);
            return r;
        }
        public static Vector3 SmoothDamp(Vector3 c, Vector3 t, ref Vector3 vel, float smoothTime) => SmoothDamp(c, t, ref vel, smoothTime, float.PositiveInfinity, Time.deltaTime);
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:F3}, {1:F3}, {2:F3})", x, y, z);
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Vector4 zero => new Vector4(0, 0, 0, 0);
        public static implicit operator Vector4(Vector3 v) => new Vector4(v.x, v.y, v.z, 0);
        public static implicit operator Vector3(Vector4 v) => new Vector3(v.x, v.y, v.z);
        public float this[int i]
        {
            get => i == 0 ? x : i == 1 ? y : i == 2 ? z : w;
            set { if (i == 0) x = value; else if (i == 1) y = value; else if (i == 2) z = value; else w = value; }
        }
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0, 0, 0, 1);

        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
            a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);

        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            float x2 = q.x * 2f, y2 = q.y * 2f, z2 = q.z * 2f;
            float xx = q.x * x2, yy = q.y * y2, zz = q.z * z2, xy = q.x * y2, xz = q.x * z2, yz = q.y * z2;
            float wx = q.w * x2, wy = q.w * y2, wz = q.w * z2;
            return new Vector3(
                (1f - (yy + zz)) * v.x + (xy - wz) * v.y + (xz + wy) * v.z,
                (xy + wz) * v.x + (1f - (xx + zz)) * v.y + (yz - wx) * v.z,
                (xz - wy) * v.x + (yz + wx) * v.y + (1f - (xx + yy)) * v.z);
        }

        public static float Dot(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        public static Quaternion Inverse(Quaternion q) => new Quaternion(-q.x, -q.y, -q.z, q.w);
        public Quaternion normalized
        {
            get
            {
                float m = Mathf.Sqrt(x * x + y * y + z * z + w * w);
                return m < 1e-8f ? identity : new Quaternion(x / m, y / m, z / m, w / m);
            }
        }
        public static Quaternion Normalize(Quaternion q) => q.normalized;

        public static Quaternion AngleAxis(float angle, Vector3 axis)
        {
            axis = axis.normalized;
            if (axis.sqrMagnitude < 1e-10f) return identity;
            float h = angle * Mathf.Deg2Rad * 0.5f;
            float s = Mathf.Sin(h);
            return new Quaternion(axis.x * s, axis.y * s, axis.z * s, Mathf.Cos(h));
        }

        public static Quaternion Euler(float x, float y, float z) =>
            AngleAxis(y, Vector3.up) * AngleAxis(x, Vector3.right) * AngleAxis(z, Vector3.forward);
        public static Quaternion Euler(Vector3 e) => Euler(e.x, e.y, e.z);

        public Vector3 eulerAngles
        {
            get
            {
                // inverse of Y * X * Z
                Matrix4x4 m = Matrix4x4.Rotate(this);
                float sx = -m.m12;
                float ex, ey, ez;
                if (Mathf.Abs(sx) < 0.99999f)
                {
                    ex = Mathf.Asin(sx);
                    ey = Mathf.Atan2(m.m02, m.m22);
                    ez = Mathf.Atan2(m.m10, m.m11);
                }
                else
                {
                    ex = sx > 0 ? Mathf.PI / 2 : -Mathf.PI / 2;
                    ey = Mathf.Atan2(-m.m20, m.m00);
                    ez = 0;
                }
                return new Vector3(Mathf.Repeat(ex * Mathf.Rad2Deg, 360), Mathf.Repeat(ey * Mathf.Rad2Deg, 360), Mathf.Repeat(ez * Mathf.Rad2Deg, 360));
            }
        }

        public static Quaternion LookRotation(Vector3 forward) => LookRotation(forward, Vector3.up);
        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            Vector3 z = forward.normalized;
            if (z.sqrMagnitude < 1e-10f) return identity;
            Vector3 xv = Vector3.Cross(up, z);
            if (xv.sqrMagnitude < 1e-10f) xv = Vector3.Cross(Math.Abs(z.y) < 0.9f ? Vector3.up : Vector3.right, z);
            xv.Normalize();
            Vector3 yv = Vector3.Cross(z, xv);
            return FromBasis(xv, yv, z);
        }

        internal static Quaternion FromBasis(Vector3 X, Vector3 Y, Vector3 Z)
        {
            float m00 = X.x, m01 = Y.x, m02 = Z.x, m10 = X.y, m11 = Y.y, m12 = Z.y, m20 = X.z, m21 = Y.z, m22 = Z.z;
            float tr = m00 + m11 + m22;
            Quaternion q;
            if (tr > 0)
            {
                float s = Mathf.Sqrt(tr + 1f) * 2f;
                q = new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s);
            }
            else if (m00 > m11 && m00 > m22)
            {
                float s = Mathf.Sqrt(1f + m00 - m11 - m22) * 2f;
                q = new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
            }
            else if (m11 > m22)
            {
                float s = Mathf.Sqrt(1f + m11 - m00 - m22) * 2f;
                q = new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s);
            }
            else
            {
                float s = Mathf.Sqrt(1f + m22 - m00 - m11) * 2f;
                q = new Quaternion((m02 + m20) / s, (m12 + m21) / s, 0.25f * s, (m10 - m01) / s);
            }
            return q.normalized;
        }

        public static Quaternion FromToRotation(Vector3 from, Vector3 to)
        {
            Vector3 a = from.normalized, b = to.normalized;
            if (a.sqrMagnitude < 1e-10f || b.sqrMagnitude < 1e-10f) return identity;
            float d = Vector3.Dot(a, b);
            if (d > 0.999999f) return identity;
            if (d < -0.999999f)
            {
                Vector3 axis = Vector3.Cross(Vector3.right, a);
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(Vector3.up, a);
                return AngleAxis(180f, axis);
            }
            Vector3 c = Vector3.Cross(a, b);
            return new Quaternion(c.x, c.y, c.z, 1f + d).normalized;
        }

        public static Quaternion SlerpUnclamped(Quaternion a, Quaternion b, float t)
        {
            float d = Dot(a, b);
            if (d < 0) { b = new Quaternion(-b.x, -b.y, -b.z, -b.w); d = -d; }
            if (d > 0.9995f) return LerpUnclamped(a, b, t);
            float th = Mathf.Acos(Mathf.Clamp(d, -1, 1));
            float s = Mathf.Sin(th);
            float wa = Mathf.Sin((1 - t) * th) / s, wb = Mathf.Sin(t * th) / s;
            return new Quaternion(a.x * wa + b.x * wb, a.y * wa + b.y * wb, a.z * wa + b.z * wb, a.w * wa + b.w * wb).normalized;
        }
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => SlerpUnclamped(a, b, Mathf.Clamp01(t));
        static Quaternion LerpUnclamped(Quaternion a, Quaternion b, float t)
        {
            if (Dot(a, b) < 0) b = new Quaternion(-b.x, -b.y, -b.z, -b.w);
            return new Quaternion(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t).normalized;
        }
        public static Quaternion Lerp(Quaternion a, Quaternion b, float t) => LerpUnclamped(a, b, Mathf.Clamp01(t));
        public static float Angle(Quaternion a, Quaternion b)
        {
            float d = Mathf.Min(Mathf.Abs(Dot(a, b)), 1f);
            return d > 0.999999f ? 0f : Mathf.Acos(d) * 2f * Mathf.Rad2Deg;
        }
        public static Quaternion RotateTowards(Quaternion from, Quaternion to, float maxDeg)
        {
            float a = Angle(from, to);
            if (a == 0) return to;
            return SlerpUnclamped(from, to, Mathf.Min(1f, maxDeg / a));
        }
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:F3}, {1:F3}, {2:F3}, {3:F3})", x, y, z, w);
    }

    public struct Matrix4x4
    {
        public float m00, m10, m20, m30, m01, m11, m21, m31, m02, m12, m22, m32, m03, m13, m23, m33;

        public float this[int r, int c]
        {
            get
            {
                switch (r + c * 4)
                {
                    case 0: return m00; case 1: return m10; case 2: return m20; case 3: return m30;
                    case 4: return m01; case 5: return m11; case 6: return m21; case 7: return m31;
                    case 8: return m02; case 9: return m12; case 10: return m22; case 11: return m32;
                    case 12: return m03; case 13: return m13; case 14: return m23; default: return m33;
                }
            }
            set
            {
                switch (r + c * 4)
                {
                    case 0: m00 = value; break; case 1: m10 = value; break; case 2: m20 = value; break; case 3: m30 = value; break;
                    case 4: m01 = value; break; case 5: m11 = value; break; case 6: m21 = value; break; case 7: m31 = value; break;
                    case 8: m02 = value; break; case 9: m12 = value; break; case 10: m22 = value; break; case 11: m32 = value; break;
                    case 12: m03 = value; break; case 13: m13 = value; break; case 14: m23 = value; break; default: m33 = value; break;
                }
            }
        }

        public static Matrix4x4 identity { get { var m = new Matrix4x4(); m.m00 = m.m11 = m.m22 = m.m33 = 1; return m; } }

        public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b)
        {
            var r = new Matrix4x4();
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                {
                    float s = 0;
                    for (int k = 0; k < 4; k++) s += a[i, k] * b[k, j];
                    r[i, j] = s;
                }
            return r;
        }

        public static Matrix4x4 Translate(Vector3 v) { var m = identity; m.m03 = v.x; m.m13 = v.y; m.m23 = v.z; return m; }
        public static Matrix4x4 Scale(Vector3 v) { var m = identity; m.m00 = v.x; m.m11 = v.y; m.m22 = v.z; return m; }
        public static Matrix4x4 Rotate(Quaternion q)
        {
            float x = q.x * 2, y = q.y * 2, z = q.z * 2;
            float xx = q.x * x, yy = q.y * y, zz = q.z * z, xy = q.x * y, xz = q.x * z, yz = q.y * z, wx = q.w * x, wy = q.w * y, wz = q.w * z;
            var m = identity;
            m.m00 = 1 - (yy + zz); m.m10 = xy + wz; m.m20 = xz - wy;
            m.m01 = xy - wz; m.m11 = 1 - (xx + zz); m.m21 = yz + wx;
            m.m02 = xz + wy; m.m12 = yz - wx; m.m22 = 1 - (xx + yy);
            return m;
        }
        public static Matrix4x4 TRS(Vector3 t, Quaternion r, Vector3 s) => Translate(t) * Rotate(r) * Scale(s);

        public Vector3 MultiplyPoint3x4(Vector3 p) => new Vector3(
            m00 * p.x + m01 * p.y + m02 * p.z + m03, m10 * p.x + m11 * p.y + m12 * p.z + m13, m20 * p.x + m21 * p.y + m22 * p.z + m23);
        public Vector3 MultiplyPoint(Vector3 p)
        {
            Vector3 r = MultiplyPoint3x4(p);
            float w = m30 * p.x + m31 * p.y + m32 * p.z + m33;
            return Mathf.Abs(w) > 1e-12f ? r / w : r;
        }
        public Vector3 MultiplyVector(Vector3 v) => new Vector3(
            m00 * v.x + m01 * v.y + m02 * v.z, m10 * v.x + m11 * v.y + m12 * v.z, m20 * v.x + m21 * v.y + m22 * v.z);

        public Vector4 GetColumn(int c) => new Vector4(this[0, c], this[1, c], this[2, c], this[3, c]);
        public Vector3 GetPosition() => new Vector3(m03, m13, m23);

        public Matrix4x4 transpose
        {
            get { var r = new Matrix4x4(); for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) r[i, j] = this[j, i]; return r; }
        }

        public Matrix4x4 inverse
        {
            get
            {
                // Gauss-Jordan
                var a = new double[4, 8];
                for (int i = 0; i < 4; i++)
                {
                    for (int j = 0; j < 4; j++) a[i, j] = this[i, j];
                    for (int j = 0; j < 4; j++) a[i, 4 + j] = i == j ? 1 : 0;
                }
                for (int c = 0; c < 4; c++)
                {
                    int piv = c;
                    for (int r = c + 1; r < 4; r++) if (Math.Abs(a[r, c]) > Math.Abs(a[piv, c])) piv = r;
                    if (Math.Abs(a[piv, c]) < 1e-12) return new Matrix4x4();
                    if (piv != c) for (int j = 0; j < 8; j++) { double t = a[c, j]; a[c, j] = a[piv, j]; a[piv, j] = t; }
                    double d = a[c, c];
                    for (int j = 0; j < 8; j++) a[c, j] /= d;
                    for (int r = 0; r < 4; r++)
                    {
                        if (r == c) continue;
                        double f = a[r, c];
                        if (f == 0) continue;
                        for (int j = 0; j < 8; j++) a[r, j] -= f * a[c, j];
                    }
                }
                var m = new Matrix4x4();
                for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) m[i, j] = (float)a[i, 4 + j];
                return m;
            }
        }
    }

    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;
        public const float Epsilon = 1.401298E-45f;
        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Tan(float f) => (float)Math.Tan(f);
        public static float Asin(float f) => (float)Math.Asin(f);
        public static float Acos(float f) => (float)Math.Acos(f);
        public static float Atan(float f) => (float)Math.Atan(f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Abs(float f) => Math.Abs(f);
        public static int Abs(int f) => Math.Abs(f);
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(params float[] v) { float m = v[0]; foreach (var x in v) m = Math.Min(m, x); return m; }
        public static float Max(params float[] v) { float m = v[0]; foreach (var x in v) m = Math.Max(m, x); return m; }
        public static float Pow(float f, float p) => (float)Math.Pow(f, p);
        public static float Exp(float p) => (float)Math.Exp(p);
        public static float Log(float f) => (float)Math.Log(f);
        public static float Log(float f, float b) => (float)Math.Log(f, b);
        public static float Log10(float f) => (float)Math.Log10(f);
        public static float Ceil(float f) => (float)Math.Ceiling(f);
        public static float Floor(float f) => (float)Math.Floor(f);
        public static float Round(float f) => (float)Math.Round(f, MidpointRounding.ToEven);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);
        public static int FloorToInt(float f) => (int)Math.Floor(f);
        public static int RoundToInt(float f) => (int)Math.Round(f, MidpointRounding.ToEven);
        public static float Sign(float f) => f >= 0f ? 1f : -1f;
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
        public static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float LerpAngle(float a, float b, float t) { float d = Repeat(b - a, 360); if (d > 180) d -= 360; return a + d * Clamp01(t); }
        public static float MoveTowards(float c, float t, float d) => Math.Abs(t - c) <= d ? t : c + Sign(t - c) * d;
        public static float MoveTowardsAngle(float c, float t, float d) { float dd = DeltaAngle(c, t); if (-d < dd && dd < d) return t; t = c + dd; return MoveTowards(c, t, d); }
        public static float SmoothStep(float from, float to, float t) { t = Clamp01(t); t = -2f * t * t * t + 3f * t * t; return to * t + from * (1f - t); }
        public static float InverseLerp(float a, float b, float v) => a != b ? Clamp01((v - a) / (b - a)) : 0f;
        public static float Repeat(float t, float len) => Clamp(t - Floor(t / len) * len, 0f, len);
        public static float PingPong(float t, float len) { t = Repeat(t, len * 2f); return len - Abs(t - len); }
        public static float DeltaAngle(float c, float t) { float d = Repeat(t - c, 360f); if (d > 180f) d -= 360f; return d; }
        public static bool Approximately(float a, float b) => Abs(b - a) < Max(1E-06f * Max(Abs(a), Abs(b)), Epsilon * 8f);
        public static float SmoothDamp(float current, float target, ref float vel, float smoothTime, float maxSpeed, float deltaTime)
        {
            smoothTime = Max(0.0001f, smoothTime);
            float num = 2f / smoothTime;
            float num2 = num * deltaTime;
            float num3 = 1f / (1f + num2 + 0.48f * num2 * num2 + 0.235f * num2 * num2 * num2);
            float num4 = current - target;
            float num5 = target;
            float num6 = maxSpeed * smoothTime;
            num4 = Clamp(num4, -num6, num6);
            target = current - num4;
            float num7 = (vel + num * num4) * deltaTime;
            vel = (vel - num * num7) * num3;
            float num8 = target + (num4 + num7) * num3;
            if (num5 - current > 0f == num8 > num5) { num8 = num5; vel = (num8 - num5) / deltaTime; }
            return num8;
        }
        public static float SmoothDamp(float current, float target, ref float vel, float smoothTime) => SmoothDamp(current, target, ref vel, smoothTime, Infinity, Time.deltaTime);
        public static float SmoothDamp(float current, float target, ref float vel, float smoothTime, float maxSpeed) => SmoothDamp(current, target, ref vel, smoothTime, maxSpeed, Time.deltaTime);
        public static float SmoothDampAngle(float c, float t, ref float v, float st, float ms, float dt) { t = c + DeltaAngle(c, t); return SmoothDamp(c, t, ref v, st, ms, dt); }
        public static float PerlinNoise(float x, float y)
        {
            // not Unity's exact noise; smooth value noise in [0,1]
            int xi = FloorToInt(x), yi = FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float H(int a, int b) { unchecked { int n = a * 374761393 + b * 668265263; n = (n ^ (n >> 13)) * 1274126177; return ((n ^ (n >> 16)) & 0xFFFF) / 65535f; } }
            float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
            return Lerp(Lerp(H(xi, yi), H(xi + 1, yi), u), Lerp(H(xi, yi + 1), H(xi + 1, yi + 1), u), v);
        }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1, 1);
        public static Color black => new Color(0, 0, 0, 1);
        public static Color gray => new Color(0.5f, 0.5f, 0.5f, 1);
        public static Color grey => gray;
        public static Color red => new Color(1, 0, 0, 1);
        public static Color green => new Color(0, 1, 0, 1);
        public static Color blue => new Color(0, 0, 1, 1);
        public static Color yellow => new Color(1, 0.92f, 0.016f, 1);
        public static Color clear => new Color(0, 0, 0, 0);
        public float grayscale => 0.299f * r + 0.587f * g + 0.114f * b;
        public static Color operator *(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a * f);
        public static Color operator *(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
        public static Color operator +(Color a, Color b) => new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
        public static Color Lerp(Color a, Color b, float t) { t = Mathf.Clamp01(t); return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t); }
        public static implicit operator Color(Color32 c) => new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static implicit operator Color32(Color c) => new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * 255), 0, 255));
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public float xMin => x;
        public float yMin => y;
        public float xMax => x + width;
        public float yMax => y + height;
        public Vector2 center => new Vector2(x + width / 2, y + height / 2);
        public Vector2 min => new Vector2(xMin, yMin);
        public Vector2 max => new Vector2(xMax, yMax);
        public Vector2 size => new Vector2(width, height);
    }

    public struct Bounds
    {
        public Vector3 center, extents;
        public Bounds(Vector3 c, Vector3 size) { center = c; extents = size * 0.5f; }
        public Vector3 size { get => extents * 2; set => extents = value * 0.5f; }
        public Vector3 min => center - extents;
        public Vector3 max => center + extents;
        public void Encapsulate(Vector3 p)
        {
            Vector3 mn = Vector3.Min(min, p), mx = Vector3.Max(max, p);
            center = (mn + mx) * 0.5f; extents = (mx - mn) * 0.5f;
        }
        public void SetMinMax(Vector3 mn, Vector3 mx) { center = (mn + mx) * 0.5f; extents = (mx - mn) * 0.5f; }
    }

    public struct BoneWeight
    {
        public int boneIndex0, boneIndex1, boneIndex2, boneIndex3;
        public float weight0, weight1, weight2, weight3;
    }

    public static class ColorUtility
    {
        public static string ToHtmlStringRGBA(Color c)
        {
            Color32 k = c;
            return string.Format("{0:X2}{1:X2}{2:X2}{3:X2}", k.r, k.g, k.b, k.a);
        }
    }

    public static class Time
    {
        public static float deltaTime = 1f / 30f;
        public static float time;
        public static float unscaledDeltaTime => deltaTime;
        public static float unscaledTime => time;
        public static int frameCount;
        public static float timeScale = 1f;
    }

    public static class Debug
    {
        public static void Log(object o) => Console.Error.WriteLine("[Log] " + o);
        public static void LogWarning(object o) => Console.Error.WriteLine("[Warn] " + o);
        public static void LogError(object o) => Console.Error.WriteLine("[Error] " + o);
        public static void DrawLine(Vector3 a, Vector3 b, Color c, float d = 0) { }
    }
}
