using System;
using System.Text;

namespace PrisonersOfOmar.Net
{
    /// <summary>Reader matching <see cref="NetWriter"/>. Reading past the end throws <see cref="NetReadException"/>.</summary>
    public sealed class NetReader
    {
        byte[] _buf;
        int _pos, _end;

        public NetReader() { }
        public NetReader(byte[] data) { SetBuffer(data, 0, data.Length); }
        public NetReader(byte[] data, int offset, int count) { SetBuffer(data, offset, count); }

        public void SetBuffer(byte[] data, int offset, int count) { _buf = data; _pos = offset; _end = offset + count; }

        public int Remaining => _end - _pos;
        /// <summary>Absolute index of the next byte in the underlying buffer.</summary>
        public int Position => _pos;
        /// <summary>The underlying buffer (transport internal: used to slice messages without copying).</summary>
        internal byte[] Data => _buf;

        void Need(int n) { if (n < 0 || _pos + n > _end) throw new NetReadException("read past end"); }

        public byte ReadByte() { Need(1); return _buf[_pos++]; }
        /// <summary>The next byte without consuming it.</summary>
        public byte PeekByte() { Need(1); return _buf[_pos]; }
        public sbyte ReadSByte() => (sbyte)ReadByte();
        public bool ReadBool() => ReadByte() != 0;
        public ushort ReadUShort() { Need(2); ushort v = (ushort)(_buf[_pos] | (_buf[_pos + 1] << 8)); _pos += 2; return v; }
        public short ReadShort() => (short)ReadUShort();
        public uint ReadUInt()
        {
            Need(4);
            uint v = (uint)(_buf[_pos] | (_buf[_pos + 1] << 8) | (_buf[_pos + 2] << 16) | (_buf[_pos + 3] << 24));
            _pos += 4; return v;
        }
        public int ReadInt() => (int)ReadUInt();
        public ulong ReadULong() { ulong lo = ReadUInt(); ulong hi = ReadUInt(); return lo | (hi << 32); }
        public long ReadLong() => (long)ReadULong();
        public float ReadFloat() { var b = new NetBits { U = ReadUInt() }; return b.F; }
        public double ReadDouble() { var b = new NetBits { UL = ReadULong() }; return b.D; }

        public string ReadString()
        {
            int n = ReadUShort();
            if (n == 0) return "";
            Need(n);
            string s = Encoding.UTF8.GetString(_buf, _pos, n);
            _pos += n;
            return s;
        }

        public byte[] ReadByteArray()
        {
            int n = ReadUShort();
            Need(n);
            var r = new byte[n];
            Buffer.BlockCopy(_buf, _pos, r, 0, n);
            _pos += n;
            return r;
        }

        /// <summary>Copy <paramref name="count"/> raw bytes (no length prefix) into <paramref name="dest"/>.</summary>
        public void ReadBytes(byte[] dest, int offset, int count)
        {
            Need(count);
            Buffer.BlockCopy(_buf, _pos, dest, offset, count);
            _pos += count;
        }

        public void Skip(int n) { Need(n); _pos += n; }
    }

    public sealed class NetReadException : Exception
    {
        public NetReadException(string msg) : base(msg) { }
    }
}
