using System;
using System.Threading;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.Client.NoObf;

namespace FarFlora
{
    /// <summary>Main-thread tick listener: player snapshot plus boundary-crossing retess sweep.</summary>
    public class Retesselator
    {
        private readonly ICoreClientAPI capi;
        private readonly FarFloraConfig cfg;
        private readonly ClientWorldMap worldMap;
        private readonly bool retessAvailable;

        private readonly long[] buffer = new long[32768];
        private int bufCount;
        private int bufPos;
        private bool moreMaybe;
        private bool pending;
        private int lastCx = int.MinValue;
        private int lastCz = int.MinValue;
        private float statsAccum;

        public Retesselator(ICoreClientAPI capi, FarFloraConfig cfg)
        {
            this.capi = capi;
            this.cfg = cfg;

            ClientMain main = capi.World as ClientMain;
            worldMap = main != null ? main.WorldMap : null;
            var m = AccessTools.Method(typeof(ClientWorldMap), "MarkChunkDirty",
                new Type[] { typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(Action), typeof(bool), typeof(bool) });
            retessAvailable = worldMap != null && m != null;
            if (!retessAvailable)
            {
                capi.Logger.Warning("[farflora] MarkChunkDirty not available, boundary re-tess disabled");
            }

            TessPatches.FarChunks = ComputeFarChunks();
            TessPatches.Enabled = cfg.Enabled;
        }

        public int ComputeFarChunks()
        {
            int f = (cfg.FarDistanceBlocks + 31) / 32;
            return f < 1 ? 1 : f;
        }

        public void QueueSweep()
        {
            pending = true;
        }

        public void OnTick(float dt)
        {
            IPlayer player = capi.World.Player;
            if (player == null || player.Entity == null) return;
            EntityPos pos = player.Entity.Pos;
            if (pos == null) return;

            int dim = pos.Dimension;
            TessPatches.PlayerDim = dim;
            if (dim != 0) return;

            int far = ComputeFarChunks();
            TessPatches.FarChunks = far;
            int cx = (int)Math.Floor(pos.X / 32.0);
            int cz = (int)Math.Floor(pos.Z / 32.0);
            bool moved = cx != lastCx || cz != lastCz;
            if (moved)
            {
                lastCx = cx;
                lastCz = cz;
                TessPatches.PlayerCx = cx;
                TessPatches.PlayerCz = cz;
                TessPatches.SnapshotValid = true;
                pending = true;
            }

            if (retessAvailable)
            {
                Sweep(cx, cz, far);
            }

            if (cfg.LogStats)
            {
                statsAccum += dt;
                if (statsAccum >= 10f)
                {
                    statsAccum = 0f;
                    int near, farCount;
                    ChunkStateStore.CountStates(out near, out farCount);
                    capi.Logger.Notification("[farflora] stats chunks near={0} far={1} seen={2} skipped={3} retess={4}",
                        near, farCount,
                        Interlocked.Read(ref TessPatches.FloraSeenTotal),
                        Interlocked.Read(ref TessPatches.FloraSkippedTotal),
                        Interlocked.Read(ref TessPatches.RetessRequestsTotal));
                }
            }
        }

        private void Sweep(int pcx, int pcz, int far)
        {
            bool enabled = TessPatches.Enabled;

            if (pending)
            {
                pending = false;
                int prune = ClientSettings.ViewDistance / 32 + 8;
                if (prune < far + 8) prune = far + 8;
                bufCount = ChunkStateStore.CollectChanged(pcx, pcz, far, enabled, prune, buffer);
                bufPos = 0;
                moreMaybe = bufCount >= buffer.Length;
            }

            if (bufPos >= bufCount)
            {
                if (moreMaybe)
                {
                    moreMaybe = false;
                    pending = true;
                }
                return;
            }

            int budget = cfg.MaxRetessPerTick;
            int scanned = 0;
            IBlockAccessor accessor = capi.World.BlockAccessor;
            while (bufPos < bufCount && budget > 0 && scanned < 4096)
            {
                long key = buffer[bufPos++];
                scanned++;
                int cx = ChunkStateStore.UnpackX(key);
                int cy = ChunkStateStore.UnpackY(key);
                int cz = ChunkStateStore.UnpackZ(key);

                if (accessor.GetChunk(cx, cy, cz) == null)
                {
                    ChunkStateStore.Remove(key);
                    continue;
                }

                byte rec = ChunkStateStore.Get(key);
                if (rec == 0) continue;
                bool desiredFar = Rule.DesiredFar(enabled, rec, cx, cz, pcx, pcz, far);
                if (desiredFar == (rec == ChunkStateStore.Far)) continue;

                worldMap.MarkChunkDirty(cx, cy, cz, false, false, null, false, false);
                Interlocked.Increment(ref TessPatches.RetessRequestsTotal);
                budget--;
            }
        }
    }
}
