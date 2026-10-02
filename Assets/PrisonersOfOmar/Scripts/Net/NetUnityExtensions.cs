using UnityEngine;

namespace PrisonersOfOmar.Net
{
    /// <summary>Unity type helpers for NetWriter / NetReader (kept separate so the transport has no UnityEngine dependency).</summary>
    public static class NetUnityExtensions
    {
        public static void WriteVector3(this NetWriter w, Vector3 v) { w.WriteFloat(v.x); w.WriteFloat(v.y); w.WriteFloat(v.z); }
        public static Vector3 ReadVector3(this NetReader r) => new Vector3(r.ReadFloat(), r.ReadFloat(), r.ReadFloat());

        public static void WriteQuaternion(this NetWriter w, Quaternion q) { w.WriteFloat(q.x); w.WriteFloat(q.y); w.WriteFloat(q.z); w.WriteFloat(q.w); }
        public static Quaternion ReadQuaternion(this NetReader r) => new Quaternion(r.ReadFloat(), r.ReadFloat(), r.ReadFloat(), r.ReadFloat());

        /// <summary>Angle in degrees packed to 2 bytes (0..360).</summary>
        public static void WriteAngle(this NetWriter w, float degrees)
        {
            float a = Mathf.Repeat(degrees, 360f);
            w.WriteUShort((ushort)Mathf.RoundToInt(a / 360f * 65535f));
        }
        public static float ReadAngle(this NetReader r) => r.ReadUShort() / 65535f * 360f;

        /// <summary>Signed angle in [-90, 90] packed to 1 byte (pitch).</summary>
        public static void WritePitch(this NetWriter w, float degrees)
        {
            float t = Mathf.InverseLerp(-90f, 90f, Mathf.Clamp(degrees, -90f, 90f));
            w.WriteByte((byte)Mathf.RoundToInt(t * 255f));
        }
        public static float ReadPitch(this NetReader r) => Mathf.Lerp(-90f, 90f, r.ReadByte() / 255f);

        /// <summary>0..1 value packed to a byte.</summary>
        public static void WriteUnit(this NetWriter w, float v) => w.WriteByte((byte)Mathf.RoundToInt(Mathf.Clamp01(v) * 255f));
        public static float ReadUnit(this NetReader r) => r.ReadByte() / 255f;

        public static void WritePose(this NetWriter w, Pose p) { w.WriteVector3(p.position); w.WriteQuaternion(p.rotation); }
        public static Pose ReadPose(this NetReader r) => new Pose(r.ReadVector3(), r.ReadQuaternion());
    }
}
