# FarFlora 1.0.0

Client-only Vintagestory 1.22.x mod for Optimum setups. It skips tessellating small plants (tall grass, flowers, ferns, mushrooms, berry bushes and similar) in distant chunks, which shrinks chunk meshes and draw cost. Plants near the player are untouched.

Independent implementation of a far-foliage LOD idea; no code from Komet or Optimum.

## How it works

- On `BlockTexturesLoaded` it builds a flora table: blocks with draw type Cross or JSON, material Plant, `Replaceable >= 3000`, no glow, and a code path not matching `ExcludePrefixes` (entries containing `:` match `domain:path`). `IncludePrefixes` entries force a block in (still needs draw type Cross or JSON).
- A main-thread tick (every `TickMs`) snapshots the player chunk. Chunks farther than `FarDistanceBlocks` (rounded up to whole 32-block chunks, with one chunk of hysteresis) are tessellated without flora.
- Harmony patches `ChunkTesselator.NowProcessChunk` (per chunk decision) and the private 6-argument `ChunkTesselator.TesselateBlock` (per block skip). No allocation in the hot paths.
- When the player crosses a chunk boundary, chunks whose near/far state changed are re-tessellated (non-priority, capped at `MaxRetessPerTick` per tick).
- Stands down if the `komet` mod is loaded. On any patch failure it unpatches everything and logs `[farflora] 1.0.0 patched=FAIL <reason>`.

Success log line: `[farflora] 1.0.0 patched=ok flora=<n> far=<chunks>`.

## Config

`ModConfig/farflora.json` (created on first run):

| Key | Default | Meaning |
|---|---|---|
| Enabled | true | Master switch |
| FarDistanceBlocks | 160 | Distance beyond which flora is skipped (min 32) |
| TickMs | 250 | Tick listener interval (min 50) |
| MaxRetessPerTick | 48 | Max chunk re-tess requests per tick |
| ExcludePrefixes | ["crop-", "wildvine", "lichen"] | Code path prefixes never treated as flora |
| IncludePrefixes | [] | Code prefixes forced to count as flora |
| LogStats | false | Log counters every 10 seconds |

## Command

`.farflora` prints enabled state, flora block types, far chunk radius, recorded chunks (near/far), skipped blocks and re-tess requests.
`.farflora on` / `.farflora off` toggles at runtime and re-tessellates recorded chunks whose state changes.

## Build

`powershell -File build.ps1` builds against the Optimum install (`%LOCALAPPDATA%\Programs\Optimum`) and the patched VintagestoryLib in `%APPDATA%\VintagestoryData\.optimum\cache`, then writes `out\farflora_1.0.0.zip`. Override with `-p:VSDir=...` or `-p:OptimumCache=...` via the VSDir / OptimumCache properties.

## License

MIT, see LICENSE.
