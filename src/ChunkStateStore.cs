using System.Collections.Generic;

namespace FarFlora
{
    public static class ChunkStateStore
    {
        public const byte Near = 1;
        public const byte Far = 2;

        private const long Mask = (1L << 21) - 1;
        private static readonly object StoreLock = new object();
        private static readonly Dictionary<long, byte> Map = new Dictionary<long, byte>(32768);

        public static long Pack(int cx, int cy, int cz)
        {
            return (((long)cy & Mask) << 42) | (((long)cz & Mask) << 21) | ((long)cx & Mask);
        }

        public static int UnpackX(long key) { return (int)(key & Mask); }
        public static int UnpackZ(long key) { return (int)((key >> 21) & Mask); }
        public static int UnpackY(long key) { return (int)((key >> 42) & Mask); }

        // Returns 0 when not recorded.
        public static byte Get(long key)
        {
            lock (StoreLock)
            {
                byte v;
                return Map.TryGetValue(key, out v) ? v : (byte)0;
            }
        }

        public static void Set(long key, byte state)
        {
            lock (StoreLock) { Map[key] = state; }
        }

        public static void Remove(long key)
        {
            lock (StoreLock) { Map.Remove(key); }
        }

        public static void Clear()
        {
            lock (StoreLock) { Map.Clear(); }
        }

        public static void CountStates(out int near, out int far)
        {
            int n = 0, f = 0;
            lock (StoreLock)
            {
                foreach (KeyValuePair<long, byte> kv in Map)
                {
                    if (kv.Value == Far) f++; else n++;
                }
            }
            near = n;
            far = f;
        }

        /// <summary>
        /// Copies keys whose desired far-ness differs from the recorded state into buffer.
        /// Returns the number copied (capped at buffer.Length). No allocation.
        /// </summary>
        public static int CollectChanged(int pcx, int pcz, int farChunks, bool enabled, int pruneChunks, long[] buffer)
        {
            int count = 0;
            lock (StoreLock)
            {
                foreach (KeyValuePair<long, byte> kv in Map)
                {
                    if (count >= buffer.Length) break;
                    int cx = UnpackX(kv.Key);
                    int cz = UnpackZ(kv.Key);
                    bool desiredFar = Rule.DesiredFar(enabled, kv.Value, cx, cz, pcx, pcz, farChunks);
                    if (desiredFar != (kv.Value == Far))
                    {
                        buffer[count++] = kv.Key;
                    }
                    else
                    {
                        // Also surface keys well outside the view so the sweep can drop unloaded chunks.
                        int dx = cx - pcx; if (dx < 0) dx = -dx;
                        int dz = cz - pcz; if (dz < 0) dz = -dz;
                        if ((dx > dz ? dx : dz) > pruneChunks) buffer[count++] = kv.Key;
                    }
                }
            }
            return count;
        }
    }

    public static class Rule
    {
        // Same hysteresis rule used by the tess-thread prefix and the sweep.
        public static bool DesiredFar(bool enabled, byte rec, int cx, int cz, int pcx, int pcz, int farChunks)
        {
            if (!enabled) return false;
            int dx = cx - pcx; if (dx < 0) dx = -dx;
            int dz = cz - pcz; if (dz < 0) dz = -dz;
            int d = dx > dz ? dx : dz;
            return rec == ChunkStateStore.Far ? d >= farChunks : d > farChunks;
        }
    }
}
