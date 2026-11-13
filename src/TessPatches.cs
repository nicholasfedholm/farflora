using System;
using System.Threading;
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;

namespace FarFlora
{
    /// <summary>Shared snapshot and counters, plus the Harmony prefix/postfix bodies.</summary>
    public static class TessPatches
    {
        // Player snapshot, written by the main-thread tick, read by the tess thread.
        public static volatile bool SnapshotValid;
        public static volatile int PlayerCx;
        public static volatile int PlayerCz;
        public static volatile int PlayerDim;
        public static volatile int FarChunks = 5;
        public static volatile bool Enabled;

        // Stats (Interlocked).
        public static long FloraSeenTotal;
        public static long FloraSkippedTotal;
        public static long RetessRequestsTotal;

        // Per tess-thread working state.
        [ThreadStatic] private static bool t_active;
        [ThreadStatic] private static bool t_skip;
        [ThreadStatic] private static int t_floraSeen;

        public static void Patch(Harmony harmony)
        {
            Type tctType = typeof(ChunkTesselator);

            Type tessChunkType = AccessTools.TypeByName("Vintagestory.Client.NoObf.TesselatedChunk");
            if (tessChunkType == null) throw new Exception("TesselatedChunk type not found");

            var nowProcess = AccessTools.Method(tctType, "NowProcessChunk",
                new Type[] { typeof(int), typeof(int), typeof(int), tessChunkType, typeof(bool) });
            if (nowProcess == null) throw new Exception("NowProcessChunk(int,int,int,TesselatedChunk,bool) not found");

            var tessBlock = AccessTools.Method(tctType, "TesselateBlock",
                new Type[] { typeof(Block), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int) });
            if (tessBlock == null) throw new Exception("TesselateBlock(Block,int,int,int,int,int) not found");

            harmony.Patch(nowProcess,
                prefix: new HarmonyMethod(typeof(TessPatches), nameof(ChunkPrefix)),
                postfix: new HarmonyMethod(typeof(TessPatches), nameof(ChunkPostfix)));
            harmony.Patch(tessBlock,
                prefix: new HarmonyMethod(typeof(TessPatches), nameof(BlockPrefix)));
        }

        // __0 chunkX, __1 chunkY, __2 chunkZ, __4 skipChunkCenter
        public static void ChunkPrefix(int __0, int __1, int __2, bool __4)
        {
            t_floraSeen = 0;
            t_skip = false;
            t_active = false;

            if (__1 >= 1024) return;
            if (FloraTable.IsFlora == null) return;
            t_active = true;

            if (!Enabled || !SnapshotValid) return;

            byte rec = ChunkStateStore.Get(ChunkStateStore.Pack(__0, __1, __2));
            bool far;
            if (__4 && rec != 0)
            {
                far = rec == ChunkStateStore.Far;
            }
            else
            {
                far = Rule.DesiredFar(true, rec, __0, __2, PlayerCx, PlayerCz, FarChunks);
            }
            t_skip = far;
        }

        public static void ChunkPostfix(int __0, int __1, int __2, bool __4)
        {
            bool active = t_active;
            bool skip = t_skip;
            int seen = t_floraSeen;
            t_active = false;
            t_skip = false;
            t_floraSeen = 0;
            if (!active) return;

            long key = ChunkStateStore.Pack(__0, __1, __2);
            if (seen > 0)
            {
                ChunkStateStore.Set(key, skip ? ChunkStateStore.Far : ChunkStateStore.Near);
                Interlocked.Add(ref FloraSeenTotal, seen);
                if (skip) Interlocked.Add(ref FloraSkippedTotal, seen);
            }
            else if (!__4)
            {
                ChunkStateStore.Remove(key);
            }
        }

        // __0 block, __5 drawType
        public static bool BlockPrefix(Block __0, int __5)
        {
            bool[] table = FloraTable.IsFlora;
            if (table == null) return true;
            int id = __0.BlockId;
            if ((uint)id < (uint)table.Length && table[id] && (__5 == 8 || __5 == 11))
            {
                t_floraSeen++;
                if (t_skip) return false;
            }
            return true;
        }
    }
}
