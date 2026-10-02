using System.Collections.Generic;
using UnityEngine;

namespace PrisonersOfOmar
{
    /// <summary>
    /// PCG32 random generator. Produces identical sequences on every machine / runtime
    /// (Mono, IL2CPP), which the multiplayer code relies on: every peer builds the map,
    /// places items and schedules events from the same match seed.
    /// Never use UnityEngine.Random or System.Random for anything that must match across peers.
    /// </summary>
    public sealed class DeterministicRandom
    {
        ulong _state;
        readonly ulong _inc;

        public DeterministicRandom(int seed, int stream = 0)
        {
            _inc = ((ulong)(uint)stream << 1) | 1UL;
            _state = 0;
            NextUInt();
            _state += (ulong)(uint)seed * 0x9E3779B97F4A7C15UL;
            NextUInt();
        }

        /// <summary>Derive an independent generator (e.g. one per subsystem) from a seed and a name.</summary>
        public static DeterministicRandom For(int seed, string purpose)
        {
            return new DeterministicRandom(seed ^ StableHash(purpose), StableHash(purpose) & 0x7FFF);
        }

        /// <summary>String hash that is identical on every platform (unlike string.GetHashCode).</summary>
        public static int StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                if (s != null)
                    for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619; }
                return (int)h;
            }
        }

        public uint NextUInt()
        {
            unchecked
            {
                ulong old = _state;
                _state = old * 6364136223846793005UL + _inc;
                uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
                int rot = (int)(old >> 59);
                return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
            }
        }

        /// <summary>[0,1)</summary>
        public float NextFloat() => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>[min,max)</summary>
        public float Range(float min, float max) => min + (max - min) * NextFloat();

        /// <summary>[min,max) integer range, like UnityEngine.Random.Range(int,int).</summary>
        public int Range(int min, int max)
        {
            if (max <= min) return min;
            return min + (int)(NextUInt() % (uint)(max - min));
        }

        public bool Chance(float probability) => NextFloat() < probability;

        public Vector2 InsideUnitCircle()
        {
            float a = NextFloat() * Mathf.PI * 2f;
            float r = Mathf.Sqrt(NextFloat());
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        public Vector3 OnUnitSphere()
        {
            float z = Range(-1f, 1f);
            float a = NextFloat() * Mathf.PI * 2f;
            float r = Mathf.Sqrt(1f - z * z);
            return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z);
        }

        public T Pick<T>(IList<T> list) => list[Range(0, list.Count)];

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }
    }
}
