using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PrisonersOfOmar.Net
{
    /// <summary>Little-endian binary writer with a growable buffer. Reuse with Reset().</summary>
    public sealed class NetWriter
    {
        byte[] _buf;
        int _len;

        public NetWriter(int capacity = 256) { _buf = new byte[Math.Max(16, capacity)]; }

        public int Length => _len;
        /// <summary>Underlying buffer (valid bytes: 0..Length).</summary>
        public byte[] Buffer => _buf;

        public NetWriter Reset() { _len = 0; return this; }

        void Ensure(int extra)
        {
            if (_len + extra <= _buf.Length) return;
            int n = _buf.Length * 2;
            while (n < _len + extra) n *= 2;
            Array.Resize(ref _buf, n);
        }

        public void WriteByte(byte v) { Ensure(1); _buf[_len++] = v; }
        public void WriteSByte(sbyte v) => WriteByte((byte)v);
        public void WriteBool(bool v) => WriteByte(v ? (byte)1 : (byte)0);

        public void WriteUShort(ushort v) { Ensure(2); _buf[_len++] = (byte)v; _buf[_len++] = (byte)(v >> 8); }
        public void WriteShort(short v) => WriteUShort((ushort)v);

        public void WriteUInt(uint v)
        {
            Ensure(4);
            _buf[_len++] = (byte)v; _buf[_len++] = (byte)(v >> 8); _buf[_len++] = (byte)(v >> 16); _buf[_len++] = (byte)(v >> 24);
        }
        public void WriteInt(int v) => WriteUInt((uint)v);

        public void WriteULong(ulong v) { WriteUInt((uint)v); WriteUInt((uint)(v >> 32)); }
        public void WriteLong(long v) => WriteULong((ulong)v);

        // Bit casts go through an explicit-layout union so no 'unsafe' code (and no "Allow unsafe code" setting) is needed.
        public void WriteFloat(float v) { var b = new NetBits { F = v }; WriteUInt(b.U); }
        public void WriteDouble(double v) { var b = new NetBits { D = v }; WriteULong(b.UL); }

        /// <summary>UTF8 string with ushort byte-length prefix (null -> empty).</summary>
        public void WriteString(string s)
        {
            if (string.IsNullOrEmpty(s)) { WriteUShort(0); return; }
            int n = Encoding.UTF8.GetByteCount(s);
            if (n > ushort.MaxValue) throw new ArgumentException("string too long");
            WriteUShort((ushort)n);
            Ensure(n);
            Encoding.UTF8.GetBytes(s, 0, s.Length, _buf, _len);
            _len += n;
        }

        public void WriteBytes(byte[] data, int offset, int count)
        {
            if (count <= 0) return;
            Ensure(count);
            System.Buffer.BlockCopy(data, offset, _buf, _len, count);
            _len += count;
        }

        /// <summary>ushort length prefix + bytes (null -> empty). Throws if longer than 65535 bytes.</summary>
        public void WriteByteArray(byte[] data)
        {
            if (data == null) { WriteUShort(0); return; }
            if (data.Length > ushort.MaxValue) throw new ArgumentException("byte array too long (max 65535)");
            WriteUShort((ushort)data.Length);
            WriteBytes(data, 0, data.Length);
        }

        public byte[] ToArray()
        {
            var r = new byte[_len];
            System.Buffer.BlockCopy(_buf, 0, r, 0, _len);
            return r;
        }
    }

    /// <summary>Float/int reinterpretation without unsafe code (works on Mono and IL2CPP).</summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct NetBits
    {
        [FieldOffset(0)] public float F;
        [FieldOffset(0)] public uint U;
        [FieldOffset(0)] public double D;
        [FieldOffset(0)] public ulong UL;
    }
}
