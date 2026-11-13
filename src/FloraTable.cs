using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace FarFlora
{
    public static class FloraTable
    {
        // Indexed by BlockId. Read-only after build; replaced by volatile reference swap.
        public static volatile bool[] IsFlora;
        public static volatile int Count;

        public static int Build(IList<Block> blocks, string[] exclude, string[] include)
        {
            if (blocks == null) return 0;
            int n = blocks.Count;
            int maxId = 0;
            for (int i = 0; i < n; i++)
            {
                Block b = blocks[i];
                if (b != null && b.BlockId > maxId) maxId = b.BlockId;
            }
            bool[] table = new bool[maxId + 1];
            int count = 0;
            for (int i = 0; i < n; i++)
            {
                Block b = blocks[i];
                if (b == null || b.BlockId < 0 || b.Code == null) continue;
                if (b.DrawType != EnumDrawType.Cross && b.DrawType != EnumDrawType.JSON) continue;

                string path = b.Code.Path ?? "";
                string full = (b.Code.Domain ?? "game") + ":" + path;

                bool included = MatchesAny(include, path, full);
                if (!included)
                {
                    if (b.BlockMaterial != EnumBlockMaterial.Plant) continue;
                    if (b.Replaceable < 3000) continue;
                    if (b.LightHsv[2] != 0) continue;
                    if (MatchesAny(exclude, path, full)) continue;
                }
                table[b.BlockId] = true;
                count++;
            }
            Count = count;
            IsFlora = table;
            return count;
        }

        private static bool MatchesAny(string[] prefixes, string path, string full)
        {
            if (prefixes == null) return false;
            for (int i = 0; i < prefixes.Length; i++)
            {
                string p = prefixes[i];
                if (string.IsNullOrEmpty(p)) continue;
                if (p.IndexOf(':') >= 0)
                {
                    if (full.StartsWith(p, StringComparison.Ordinal)) return true;
                }
                else if (path.StartsWith(p, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        public static void Clear()
        {
            IsFlora = null;
            Count = 0;
        }
    }
}
