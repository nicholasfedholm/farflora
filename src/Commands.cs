using System.Threading;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace FarFlora
{
    public static class Commands
    {
        public static void Register(ICoreClientAPI capi, FarFloraConfig cfg, Retesselator ret)
        {
            capi.ChatCommands.Create("farflora")
                .WithDescription("FarFlora far plant LOD: status, or 'on'/'off' to toggle")
                .WithArgs(capi.ChatCommands.Parsers.OptionalWord("mode"))
                .HandleWith(args => Handle(args, cfg, ret));
        }

        private static TextCommandResult Handle(TextCommandCallingArgs args, FarFloraConfig cfg, Retesselator ret)
        {
            string mode = args.Parsers.Count > 0 ? args[0] as string : null;
            if (!string.IsNullOrEmpty(mode))
            {
                string m = mode.ToLowerInvariant();
                if (m == "on" || m == "off")
                {
                    bool on = m == "on";
                    cfg.Enabled = on;
                    TessPatches.Enabled = on;
                    ret.QueueSweep();
                    return TextCommandResult.Success("[farflora] " + (on ? "enabled" : "disabled") + ", re-tess queued for changed chunks");
                }
                return TextCommandResult.Error("Usage: .farflora [on|off]");
            }

            int near, far;
            ChunkStateStore.CountStates(out near, out far);
            string text = string.Format(
                "[farflora] enabled={0} floraTypes={1} farChunks={2} recordedChunks={3} (near={4} far={5}) skippedBlocks={6} retessRequests={7}",
                TessPatches.Enabled,
                FloraTable.Count,
                TessPatches.FarChunks,
                near + far, near, far,
                Interlocked.Read(ref TessPatches.FloraSkippedTotal),
                Interlocked.Read(ref TessPatches.RetessRequestsTotal));
            return TextCommandResult.Success(text);
        }
    }
}
