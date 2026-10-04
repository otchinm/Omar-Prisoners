// Minimal managed re-implementation of the UnityEngine math types used by the map builder.
// ONLY for the offline QA harness (Tools/AssetPipeline/map/sim). Unity never compiles this folder.
using System;
using System.Globalization;

namespace UnityEngine
{
    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public const float Epsilon = 1.401298E-45f;
        public const float Infinity = float.PositiveInfinity;
        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Tan(float f) => (float)Math.Tan(f);
        public static float Asin(float f) => (float)Math.Asin(f);
        public static float Acos(float f) => (float)Math.Acos(f);
        public static float Atan(float f) => (float)Math.Atan(f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Abs(float f) => Math.Abs(f);
        public static int Abs(int v) => Math.Abs(v);
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Pow(float f, float p) => (float)Math.Pow(f, p);
        public static float Exp(float p) => (float)Math.Exp(p);
        public static float Log(float f) => (float)Math.Log(f);
        public static float Floor(float f) => (float)Math.Floor(f);
        public static float Ceil(float f) => (float)Math.Ceiling(f);
        public static float Round(float f) => (float)Math.Round(f);
        public static int FloorToInt(float f) => (int)Math.Floor(f);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);
        public static int RoundToInt(float f) => (int)Math.Round(f);
        public static float Sign(float f) => f >= 0f ? 1f : -1f;
        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float InverseLerp(float a, float b, float v) => a != b ? Clamp01((v - a) / (b - a)) : 0f;
        public static float SmoothStep(float from, float to, float t) { t = Clamp01(t); t = -2f * t * t * t + 3f * t * t; return to * t + from * (1f - t); }
        public static bool Approximately(float a, float b) => Math.Abs(b - a) < Math.Max(1E-06f * Math.Max(Math.Abs(a), Math.Abs(b)), 1.1E-44f);
        public static float Repeat(float t, float length) => Clamp(t - Floor(t / length) * length, 0f, length);
        public static float PingPong(float t, float length) { t = Repeat(t, length * 2f); return length - Math.Abs(t - length); }
        public static float MoveTowards(float c, float t, float d) => Math.Abs(t - c) <= d ? t : c + Sign(t - c) * d;
        public static float DeltaAngle(float c, float t) { float d = Repeat(t - c, 360f); if (d > 180f) d -= 360f; return d; }
        public static float PerlinNoise(float x, float y) => 0.5f;
        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime) => SmoothDamp(current, target, ref currentVelocity, smoothTime, Infinity, Time.deltaTime);
        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed) => SmoothDamp(current, target, ref currentVelocity, smoothTime, maxSpeed, Time.deltaTime);
        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            // Unity's critically damped spring (UnityCsReference Mathf.SmoothDamp)
            smoothTime = Max(0.0001f, smoothTime);
            float omega = 2f / smoothTime;
            float x = omega * deltaTime;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);
            float change = current - target;
            float originalTo = target;
            float maxChange = maxSpeed * smoothTime;
            change = Clamp(change, -maxChange, maxChange);
            target = current - change;
            float temp = (currentVelocity + omega * change) * deltaTime;
            currentVelocity = (currentVelocity - omega * temp) * exp;
            float output = target + (change + temp) * exp;
            if (originalTo - current > 0f == output > originalTo)
            {
                output = originalTo;
                currentVelocity = (output - originalTo) / deltaTime;
            }
            return output;
        }
    }

    public struct Vector2 : IEquatable<Vector2>
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public static Vector2 up => new Vector2(0, 1);
        public static Vector2 right => new Vector2(1, 0);
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
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0);
        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { t = Mathf.Clamp01(t); return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t); }
        public bool Equals(Vector2 o) => x == o.x && y == o.y;
        public override bool Equals(object o) => o is Vector2 v && Equals(v);
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);
        public override string ToString() => ToString("F2");
        public string ToString(string f) => "(" + x.ToString(f, CultureInfo.InvariantCulture) + ", " + y.ToString(f, CultureInfo.InvariantCulture) + ")";
    }

    public struct Vector3 : IEquatable<Vector3>
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0; }
        public float this[int i] { get => i == 0 ? x : (i == 1 ? y : z); set { if (i == 0) x = value; else if (i == 1) y = value; else z = value; } }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 down => new Vector3(0, -1, 0);
        public static Vector3 left => new Vector3(-1, 0, 0);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 back => new Vector3(0, 0, -1);
        public float magnitude => Mathf.Sqrt(x * x + y * y + z * z);
        public float sqrMagnitude => x * x + y * y + z * z;
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public void Normalize() { this = normalized; }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);
        public static bool operator ==(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 9.99999944E-11f;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t); }
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        public static Vector3 Min(Vector3 a, Vector3 b) => new Vector3(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Min(a.z, b.z));
        public static Vector3 Max(Vector3 a, Vector3 b) => new Vector3(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y), Mathf.Max(a.z, b.z));
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3 Project(Vector3 v, Vector3 n) { float s = Dot(n, n); return s < 1e-12f ? zero : n * (Dot(v, n) / s); }
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) => v - Project(v, n);
        public static Vector3 Reflect(Vector3 d, Vector3 n) => d - 2f * Dot(n, d) * n;
        public static float Angle(Vector3 a, Vector3 b) { float d = Mathf.Sqrt(a.sqrMagnitude * b.sqrMagnitude); return d < 1e-15f ? 0f : Mathf.Acos(Mathf.Clamp(Dot(a, b) / d, -1f, 1f)) * Mathf.Rad2Deg; }
        public static Vector3 MoveTowards(Vector3 c, Vector3 t, float d) { var v = t - c; float m = v.magnitude; return m <= d || m == 0f ? t : c + v / m * d; }
        /// <summary>Unity semantics: direction interpolated on the sphere, magnitude linearly.</summary>
        public static Vector3 Slerp(Vector3 a, Vector3 b, float t) => SlerpUnclamped(a, b, Mathf.Clamp01(t));
        public static Vector3 SlerpUnclamped(Vector3 a, Vector3 b, float t)
        {
            float la = a.magnitude, lb = b.magnitude;
            if (la < 1e-6f || lb < 1e-6f) return LerpUnclamped(a, b, t);
            Vector3 da = a / la, db = b / lb;
            float d = Mathf.Clamp(Dot(da, db), -1f, 1f);
            float len = la + (lb - la) * t;
            if (d > 0.9999f) return LerpUnclamped(da, db, t).normalized * len;
            Vector3 rel = db - da * d;
            if (rel.sqrMagnitude < 1e-10f)
            {
                rel = Cross(da, Vector3.right);
                if (rel.sqrMagnitude < 1e-6f) rel = Cross(da, Vector3.up);
            }
            rel = rel.normalized;
            float th = Mathf.Acos(d) * t;
            return (da * Mathf.Cos(th) + rel * Mathf.Sin(th)) * len;
        }
        public bool Equals(Vector3 o) => x == o.x && y == o.y && z == o.z;
        public override bool Equals(object o) => o is Vector3 v && Equals(v);
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2);
        public override string ToString() => ToString("F2");
        public string ToString(string f) => "(" + x.ToString(f, CultureInfo.InvariantCulture) + ", " + y.ToString(f, CultureInfo.InvariantCulture) + ", " + z.ToString(f, CultureInfo.InvariantCulture) + ")";
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Vector4 zero => new Vector4(0, 0, 0, 0);
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

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0, 0, 0, 1);

        public static Quaternion operator *(Quaternion l, Quaternion r) => new Quaternion(
            l.w * r.x + l.x * r.w + l.y * r.z - l.z * r.y,
            l.w * r.y + l.y * r.w + l.z * r.x - l.x * r.z,
            l.w * r.z + l.z * r.w + l.x * r.y - l.y * r.x,
            l.w * r.w - l.x * r.x - l.y * r.y - l.z * r.z);

        public static Vector3 operator *(Quaternion q, Vector3 p)
        {
            float x = q.x * 2f, y = q.y * 2f, z = q.z * 2f;
            float xx = q.x * x, yy = q.y * y, zz = q.z * z, xy = q.x * y, xz = q.x * z, yz = q.y * z, wx = q.w * x, wy = q.w * y, wz = q.w * z;
            return new Vector3(
                (1f - (yy + zz)) * p.x + (xy - wz) * p.y + (xz + wy) * p.z,
                (xy + wz) * p.x + (1f - (xx + zz)) * p.y + (yz - wx) * p.z,
                (xz - wy) * p.x + (yz + wx) * p.y + (1f - (xx + yy)) * p.z);
        }

        public static bool operator ==(Quaternion a, Quaternion b) => Dot(a, b) > 0.999999f;
        public static bool operator !=(Quaternion a, Quaternion b) => !(a == b);
        public override bool Equals(object o) => o is Quaternion q && this == q;
        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode() ^ w.GetHashCode();

        public static float Dot(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;

        public static Quaternion AngleAxis(float angle, Vector3 axis)
        {
            axis = axis.normalized;
            float h = angle * Mathf.Deg2Rad * 0.5f, s = Mathf.Sin(h);
            return new Quaternion(axis.x * s, axis.y * s, axis.z * s, Mathf.Cos(h));
        }

        /// <summary>Unity order: rotate around Z, then X, then Y.</summary>
        public static Quaternion Euler(float x, float y, float z) => AngleAxis(y, Vector3.up) * AngleAxis(x, Vector3.right) * AngleAxis(z, Vector3.forward);
        public static Quaternion Euler(Vector3 e) => Euler(e.x, e.y, e.z);

        public static Quaternion Inverse(Quaternion q)
        {
            float n = q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w;
            if (n < 1e-12f) return identity;
            return new Quaternion(-q.x / n, -q.y / n, -q.z / n, q.w / n);
        }

        public Quaternion normalized
        {
            get
            {
                float n = Mathf.Sqrt(x * x + y * y + z * z + w * w);
                return n < 1e-9f ? identity : new Quaternion(x / n, y / n, z / n, w / n);
            }
        }

        public static Quaternion LookRotation(Vector3 forward) => LookRotation(forward, Vector3.up);

        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            if (forward.sqrMagnitude < 1e-12f) return identity;
            Vector3 zf = forward.normalized;
            Vector3 xr = Vector3.Cross(up, zf);
            if (xr.sqrMagnitude < 1e-10f) return FromToRotation(Vector3.forward, zf);
            xr = xr.normalized;
            Vector3 yu = Vector3.Cross(zf, xr);
            return FromBasis(xr, yu, zf);
        }

        static Quaternion FromBasis(Vector3 X, Vector3 Y, Vector3 Z)
        {
            float m00 = X.x, m01 = Y.x, m02 = Z.x, m10 = X.y, m11 = Y.y, m12 = Z.y, m20 = X.z, m21 = Y.z, m22 = Z.z;
            float tr = m00 + m11 + m22;
            Quaternion q;
            if (tr > 0f)
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

        public static float Angle(Quaternion a, Quaternion b)
        {
            float d = Mathf.Min(Mathf.Abs(Dot(a, b)), 1f);
            return d > 0.999999f ? 0f : Mathf.Acos(d) * 2f * Mathf.Rad2Deg;
        }

        public static Quaternion Slerp(Quaternion a, Quaternion b, float t)
        {
            t = Mathf.Clamp01(t);
            float d = Dot(a, b);
            if (d < 0) { b = new Quaternion(-b.x, -b.y, -b.z, -b.w); d = -d; }
            if (d > 0.9995f) return new Quaternion(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t).normalized;
            float th = Mathf.Acos(d), s = Mathf.Sin(th);
            float wa = Mathf.Sin((1 - t) * th) / s, wb = Mathf.Sin(t * th) / s;
            return new Quaternion(a.x * wa + b.x * wb, a.y * wa + b.y * wb, a.z * wa + b.z * wb, a.w * wa + b.w * wb);
        }

        public Vector3 eulerAngles
        {
            get
            {
                // returns approximate yaw/pitch/roll (only used for debugging)
                Vector3 f = this * Vector3.forward;
                float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                float pitch = -Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
                return new Vector3(pitch, yaw, 0f);
            }
        }

        public override string ToString() => "(" + x.ToString("F3", CultureInfo.InvariantCulture) + ", " + y.ToString("F3", CultureInfo.InvariantCulture) + ", " + z.ToString("F3", CultureInfo.InvariantCulture) + ", " + w.ToString("F3", CultureInfo.InvariantCulture) + ")";
    }

    public struct Matrix4x4
    {
        public float m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23, m30, m31, m32, m33;

        public float this[int r, int c]
        {
            get
            {
                switch (r * 4 + c)
                {
                    case 0: return m00; case 1: return m01; case 2: return m02; case 3: return m03;
                    case 4: return m10; case 5: return m11; case 6: return m12; case 7: return m13;
                    case 8: return m20; case 9: return m21; case 10: return m22; case 11: return m23;
                    case 12: return m30; case 13: return m31; case 14: return m32; default: return m33;
                }
            }
            set
            {
                switch (r * 4 + c)
                {
                    case 0: m00 = value; break; case 1: m01 = value; break; case 2: m02 = value; break; case 3: m03 = value; break;
                    case 4: m10 = value; break; case 5: m11 = value; break; case 6: m12 = value; break; case 7: m13 = value; break;
                    case 8: m20 = value; break; case 9: m21 = value; break; case 10: m22 = value; break; case 11: m23 = value; break;
                    case 12: m30 = value; break; case 13: m31 = value; break; case 14: m32 = value; break; default: m33 = value; break;
                }
            }
        }

        public static Matrix4x4 identity => new Matrix4x4 { m00 = 1, m11 = 1, m22 = 1, m33 = 1 };

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

        public Vector3 MultiplyPoint3x4(Vector3 p) => new Vector3(
            m00 * p.x + m01 * p.y + m02 * p.z + m03,
            m10 * p.x + m11 * p.y + m12 * p.z + m13,
            m20 * p.x + m21 * p.y + m22 * p.z + m23);

        public Vector3 MultiplyPoint(Vector3 p)
        {
            var v = MultiplyPoint3x4(p);
            float w = m30 * p.x + m31 * p.y + m32 * p.z + m33;
            return Mathf.Abs(w) > 1e-12f ? v / w : v;
        }

        public Vector3 MultiplyVector(Vector3 v) => new Vector3(
            m00 * v.x + m01 * v.y + m02 * v.z,
            m10 * v.x + m11 * v.y + m12 * v.z,
            m20 * v.x + m21 * v.y + m22 * v.z);

        public static Matrix4x4 Translate(Vector3 t) { var m = identity; m.m03 = t.x; m.m13 = t.y; m.m23 = t.z; return m; }
        public static Matrix4x4 Scale(Vector3 s) { var m = identity; m.m00 = s.x; m.m11 = s.y; m.m22 = s.z; return m; }

        public static Matrix4x4 Rotate(Quaternion q)
        {
            Vector3 X = q * Vector3.right, Y = q * Vector3.up, Z = q * Vector3.forward;
            var m = identity;
            m.m00 = X.x; m.m10 = X.y; m.m20 = X.z;
            m.m01 = Y.x; m.m11 = Y.y; m.m21 = Y.z;
            m.m02 = Z.x; m.m12 = Z.y; m.m22 = Z.z;
            return m;
        }

        public static Matrix4x4 TRS(Vector3 t, Quaternion q, Vector3 s) => Translate(t) * Rotate(q) * Scale(s);

        public Matrix4x4 transpose
        {
            get
            {
                var r = new Matrix4x4();
                for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) r[i, j] = this[j, i];
                return r;
            }
        }

        public Matrix4x4 inverse
        {
            get
            {
                var a = new double[4, 8];
                for (int i = 0; i < 4; i++) { for (int j = 0; j < 4; j++) a[i, j] = this[i, j]; a[i, 4 + i] = 1; }
                for (int c = 0; c < 4; c++)
                {
                    int p = c;
                    for (int r = c + 1; r < 4; r++) if (Math.Abs(a[r, c]) > Math.Abs(a[p, c])) p = r;
                    if (Math.Abs(a[p, c]) < 1e-14) return new Matrix4x4();
                    if (p != c) for (int k = 0; k < 8; k++) { var t = a[c, k]; a[c, k] = a[p, k]; a[p, k] = t; }
                    double d = a[c, c];
                    for (int k = 0; k < 8; k++) a[c, k] /= d;
                    for (int r = 0; r < 4; r++)
                    {
                        if (r == c) continue;
                        double f = a[r, c];
                        if (f == 0) continue;
                        for (int k = 0; k < 8; k++) a[r, k] -= f * a[c, k];
                    }
                }
                var m = new Matrix4x4();
                for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) m[i, j] = (float)a[i, 4 + j];
                return m;
            }
        }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) : this(r, g, b, 1f) { }
        public static Color white => new Color(1, 1, 1, 1);
        public static Color black => new Color(0, 0, 0, 1);
        public static Color gray => new Color(0.5f, 0.5f, 0.5f, 1);
        public static Color grey => gray;
        public static Color clear => new Color(0, 0, 0, 0);
        public static Color red => new Color(1, 0, 0, 1);
        public static Color green => new Color(0, 1, 0, 1);
        public static Color blue => new Color(0, 0, 1, 1);
        public static Color yellow => new Color(1, 0.92f, 0.016f, 1);
        public float grayscale => 0.299f * r + 0.587f * g + 0.114f * b;
        public static Color operator *(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
        public static Color operator *(Color a, float f) => new Color(a.r * f, a.g * f, a.b * f, a.a * f);
        public static Color operator +(Color a, Color b) => new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
        public static Color Lerp(Color a, Color b, float t) { t = Mathf.Clamp01(t); return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t); }
        public static implicit operator Color32(Color c) => new Color32((byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255), (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * 255f), 0, 255));
        public static implicit operator Vector4(Color c) => new Vector4(c.r, c.g, c.b, c.a);
        public override string ToString() => "RGBA(" + r.ToString("F3", CultureInfo.InvariantCulture) + ", " + g.ToString("F3", CultureInfo.InvariantCulture) + ", " + b.ToString("F3", CultureInfo.InvariantCulture) + ", " + a.ToString("F3", CultureInfo.InvariantCulture) + ")";
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static implicit operator Color(Color32 c) => new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
    }

    public struct Rect
    {
        float _x, _y, _w, _h;
        public Rect(float x, float y, float width, float height) { _x = x; _y = y; _w = width; _h = height; }
        public static Rect MinMaxRect(float xmin, float ymin, float xmax, float ymax) => new Rect(xmin, ymin, xmax - xmin, ymax - ymin);
        public float x { get => _x; set => _x = value; }
        public float y { get => _y; set => _y = value; }
        public float width { get => _w; set => _w = value; }
        public float height { get => _h; set => _h = value; }
        public float xMin { get => Mathf.Min(_x, _x + _w); set { float xm = xMax; _x = value; _w = xm - _x; } }
        public float yMin { get => Mathf.Min(_y, _y + _h); set { float ym = yMax; _y = value; _h = ym - _y; } }
        public float xMax { get => Mathf.Max(_x, _x + _w); set => _w = value - _x; }
        public float yMax { get => Mathf.Max(_y, _y + _h); set => _h = value - _y; }
        public Vector2 center => new Vector2(_x + _w * 0.5f, _y + _h * 0.5f);
        public Vector2 min => new Vector2(xMin, yMin);
        public Vector2 max => new Vector2(xMax, yMax);
        public Vector2 size => new Vector2(_w, _h);
        public bool Contains(Vector2 p) => p.x >= xMin && p.x < xMax && p.y >= yMin && p.y < yMax;
        public bool Overlaps(Rect o) => o.xMax > xMin && o.xMin < xMax && o.yMax > yMin && o.yMin < yMax;
        public override string ToString() => "(x:" + _x + ", y:" + _y + ", width:" + _w + ", height:" + _h + ")";
    }

    public struct Bounds
    {
        public Vector3 center;
        public Vector3 extents;
        public Bounds(Vector3 center, Vector3 size) { this.center = center; extents = size * 0.5f; }
        public Vector3 size { get => extents * 2f; set => extents = value * 0.5f; }
        public Vector3 min => center - extents;
        public Vector3 max => center + extents;
        public void SetMinMax(Vector3 min, Vector3 max) { extents = (max - min) * 0.5f; center = min + extents; }
        public void Encapsulate(Vector3 p) => SetMinMax(Vector3.Min(min, p), Vector3.Max(max, p));
        public void Encapsulate(Bounds b) { Encapsulate(b.center - b.extents); Encapsulate(b.center + b.extents); }
        public bool Contains(Vector3 p) => p.x >= min.x && p.x <= max.x && p.y >= min.y && p.y <= max.y && p.z >= min.z && p.z <= max.z;
        public bool Intersects(Bounds b) => min.x <= b.max.x && max.x >= b.min.x && min.y <= b.max.y && max.y >= b.min.y && min.z <= b.max.z && max.z >= b.min.z;
    }

    public struct Pose
    {
        public Vector3 position;
        public Quaternion rotation;
        public Pose(Vector3 position, Quaternion rotation) { this.position = position; this.rotation = rotation; }
        public static Pose identity => new Pose(Vector3.zero, Quaternion.identity);
        public Vector3 forward => rotation * Vector3.forward;
    }

    public static class ColorUtility
    {
        public static string ToHtmlStringRGBA(Color c)
        {
            Color32 k = c;
            return k.r.ToString("X2") + k.g.ToString("X2") + k.b.ToString("X2") + k.a.ToString("X2");
        }
    }
}
