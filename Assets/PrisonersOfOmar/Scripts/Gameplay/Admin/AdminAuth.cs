using System;
using System.Security.Cryptography;
using System.Text;

namespace PrisonersOfOmar.Gameplay
{
    /// <summary>
    /// (iteration 2) Admin password check. Only a verifier is stored: SHA-256 of the PBKDF2-HMAC-SHA256 key derived
    /// from the (normalised) password. The password itself is never in the build or the repository. Clients prove
    /// themselves to the host by sending the derived key; the host checks it against the same verifier.
    /// No UnityEngine dependency (tested by Tools/AdminAuthTest).
    /// </summary>
    public static class AdminAuth
    {
        public const int Iterations = 20000;
        public const int KeyLength = 32;
        public const string SaltHex = "f1c54b20818ef7029c23df0b0fbb40e0";
        public const string VerifierHex = "c0b4ec3e637dc53a9a5121a5170953f398ac37dd04ba2082684ac661cbab9c9c";

        /// <summary>Upper case, without spaces and dashes (so "abcd-efgh ijkl" == "ABCDEFGHIJKL").</summary>
        public static string Normalize(string password)
        {
            if (string.IsNullOrEmpty(password)) return "";
            var sb = new StringBuilder(password.Length);
            foreach (char c in password)
            {
                if (c == ' ' || c == '-' || char.IsWhiteSpace(c)) continue;
                sb.Append(char.ToUpperInvariant(c));
            }
            return sb.ToString();
        }

        public static byte[] DeriveKey(string password) => DeriveKey(password, FromHex(SaltHex), Iterations);

        public static byte[] DeriveKey(string password, byte[] salt, int iterations)
            => Pbkdf2Sha256(Encoding.UTF8.GetBytes(Normalize(password)), salt, iterations, KeyLength);

        /// <summary>Does this derived key belong to the admin password?</summary>
        public static bool Verify(byte[] key) => Verify(key, VerifierHex);

        public static bool Verify(byte[] key, string verifierHex)
        {
            if (key == null || key.Length != KeyLength) return false;
            byte[] h;
            using (var sha = SHA256.Create()) h = sha.ComputeHash(key);
            byte[] want = FromHex(verifierHex);
            if (want.Length != h.Length) return false;
            int diff = 0;
            for (int i = 0; i < h.Length; i++) diff |= h[i] ^ want[i]; // constant time
            return diff == 0;
        }

        public static string VerifierOf(byte[] key)
        {
            using (var sha = SHA256.Create()) return ToHex(sha.ComputeHash(key));
        }

        /// <summary>PBKDF2 (RFC 8018) with HMAC-SHA256, written on HMACSHA256 so it runs on every Unity scripting backend.</summary>
        public static byte[] Pbkdf2Sha256(byte[] password, byte[] salt, int iterations, int dkLen)
        {
            var dk = new byte[dkLen];
            using (var hmac = new HMACSHA256(password))
            {
                int blocks = (dkLen + 31) / 32;
                var input = new byte[salt.Length + 4];
                Buffer.BlockCopy(salt, 0, input, 0, salt.Length);
                for (int b = 1; b <= blocks; b++)
                {
                    input[salt.Length] = (byte)(b >> 24);
                    input[salt.Length + 1] = (byte)(b >> 16);
                    input[salt.Length + 2] = (byte)(b >> 8);
                    input[salt.Length + 3] = (byte)b;
                    byte[] u = hmac.ComputeHash(input);
                    byte[] t = (byte[])u.Clone();
                    for (int i = 1; i < iterations; i++)
                    {
                        u = hmac.ComputeHash(u);
                        for (int k = 0; k < t.Length; k++) t[k] ^= u[k];
                    }
                    int off = (b - 1) * 32;
                    Buffer.BlockCopy(t, 0, dk, off, Math.Min(32, dkLen - off));
                }
            }
            return dk;
        }

        public static string ToHex(byte[] data)
        {
            var sb = new StringBuilder(data.Length * 2);
            foreach (byte x in data) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }

        public static byte[] FromHex(string hex)
        {
            if (string.IsNullOrEmpty(hex) || (hex.Length & 1) != 0) return new byte[0];
            var r = new byte[hex.Length / 2];
            for (int i = 0; i < r.Length; i++)
            {
                int hi = Nib(hex[i * 2]), lo = Nib(hex[i * 2 + 1]);
                if (hi < 0 || lo < 0) return new byte[0];
                r[i] = (byte)((hi << 4) | lo);
            }
            return r;
        }

        static int Nib(char c) => c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
    }
}
