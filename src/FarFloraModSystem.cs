using System;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace FarFlora
{
    public class FarFloraModSystem : ModSystem
    {
        private const string HarmonyId = "nick.farflora";
        private const string Version = "1.0.0";

        private ICoreClientAPI capi;
        private Harmony harmony;
        private FarFloraConfig cfg;
        private Retesselator retess;
        private bool patched;

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Client;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            if (api.ModLoader.IsModEnabled("komet"))
            {
                api.Logger.Warning("[farflora] Komet is loaded, FarFlora stands down (Komet has its own far foliage)");
                return;
            }

            try
            {
                cfg = api.LoadModConfig<FarFloraConfig>("farflora.json");
                if (cfg == null)
                {
                    cfg = new FarFloraConfig();
                    api.StoreModConfig(cfg, "farflora.json");
                }
                if (cfg.FarDistanceBlocks < 32) cfg.FarDistanceBlocks = 32;
                if (cfg.TickMs < 50) cfg.TickMs = 50;
                if (cfg.MaxRetessPerTick < 1) cfg.MaxRetessPerTick = 1;
                if (cfg.ExcludePrefixes == null) cfg.ExcludePrefixes = new string[0];
                if (cfg.IncludePrefixes == null) cfg.IncludePrefixes = new string[0];

                retess = new Retesselator(api, cfg);

                harmony = new Harmony(HarmonyId);
                TessPatches.Patch(harmony);
                patched = true;

                api.Event.BlockTexturesLoaded += OnBlockTexturesLoaded;
                api.Event.RegisterGameTickListener(retess.OnTick, cfg.TickMs);
                Commands.Register(api, cfg, retess);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void OnBlockTexturesLoaded()
        {
            try
            {
                int n = FloraTable.Build(capi.World.Blocks, cfg.ExcludePrefixes, cfg.IncludePrefixes);
                capi.Logger.Notification("[farflora] " + Version + " patched=ok flora=" + n + " far=" + TessPatches.FarChunks);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void Fail(Exception e)
        {
            try
            {
                if (harmony != null) harmony.UnpatchAll(HarmonyId);
            }
            catch (Exception)
            {
                // nothing more to do
            }
            patched = false;
            TessPatches.Enabled = false;
            FloraTable.Clear();
            ChunkStateStore.Clear();
            capi.Logger.Error("[farflora] " + Version + " patched=FAIL " + e.Message);
        }

        public override void Dispose()
        {
            try
            {
                if (capi != null) capi.Event.BlockTexturesLoaded -= OnBlockTexturesLoaded;
                if (harmony != null && patched) harmony.UnpatchAll(HarmonyId);
            }
            catch (Exception)
            {
                // ignore on shutdown
            }
            patched = false;
            TessPatches.Enabled = false;
            TessPatches.SnapshotValid = false;
            FloraTable.Clear();
            ChunkStateStore.Clear();
            base.Dispose();
        }
    }
}
