# AeroScenery: write AFS4 ttc files directly (no stitching, no GeoConvert)

Started 2026-11-17. Format and geometry come from afsd (D:\Repos\afsd: `terrain_tile.h`, `terrain_import.cpp`), which
also verifies the output (`afsd <tile>.ttc` -> north-up PNG).

## Facts this rests on (all verified 2026-11-17)
- A ttc is a 0x100-byte header of u32 fields (rest zero), then the payload. The header that works in the sim (stock us
  tiles, afsd -tc): magic 0x303a, payload offset 0x100, 0, payload size, 2796216 (uncompressed), 2048, 2048, 12 (mip
  levels), 0x12345672; bytes 0x24..0xff zero.
- Payload: the BC1 mip chain of a 2048x2048 image, 12 levels largest first (2796216 bytes), rows bottom to top (south
  first). The sim reads it LZHAM- **or zlib**-compressed (zlib test tiles around KBED, Karl, 2026-11-17), so .NET's
  DeflateStream is enough: `78 01` + raw deflate + Adler-32 (big-endian), as the stock tth.
- Name `map_LL_XXXX_YYYY.ttc`: XXXX_YYYY = south-west corner in level-16 grid units (hex), span 0x10000 >> LL.
  Grid: x = 65536 (0.5 + lon/360); y = 65536 (0.5 + 0.5 tan(k lat/180) / k), k = 2.3311223704144;
  lat = 180 atan(k (2y/65536 - 1)) / k.
- Pixel (i, j) of a tile (i east, j north, from 0) is centred at grid16 corner + (i + 0.5, j + 0.5) * span / 2048
  (afsd -tc matched stock tiles to 0.000 px with this).
- Bing/Google tiles are Web Mercator, 256 px; Bing zoom z pixel = AFS level z - 3 pixel in longitude.
- The sim draws the finest level it has: imagery must reach the finest stock level there (us: 14), and the selected
  square's levels down to that one.
- Mips: plain 2x2 box filter (matches stock).

## Design
New folder `AeroScenery/AFS4/`:
- `AfsGrid` - grid16 <-> lon/lat, tile names and bounds (exact, from the formulas above).
- `WebMercator` - lon/lat <-> global pixel at zoom z (the Bing/Google tile scheme).
- `SourceTiles` - loads the downloaded JPEGs by TileX/TileY from the download folder on demand (small LRU cache,
  decoded to 24-bit arrays), lists missing tiles.
- `TtcRenderer` - renders the tiles of the selected square:
  - Finest level F = zoom - 3 (one output pixel per source pixel). Each pixel centre -> lon/lat -> Mercator pixel ->
    bilinear from SourceTiles. No stitched image, so no Mercator-as-linear-latitude error.
  - Levels coarser than F: 2x2 box of the four child tiles, depth-first over the quadtree, so only one branch is in
    memory at a time. Levels finer than F (if selected): rendered directly (upsampled).
  - Only selected levels are written; tiles whose sources are incomplete are skipped and listed.
  - Optional: AeroScenery's existing image-processing filters (brightness, contrast, ...) applied per tile.
- `TtcWriter` - mip chain (box), BC1 (BCnEncoder.NET 2.3.0, MIT, netstandard2.0), zlib, header; writes
  `<square>\<source>\<zoom>-ttc\map_LL_XXXX_YYYY.ttc`, where the existing installer picks them up.
- UI/settings: a new action "Write TTC files" next to the GeoConvert ones (which stay for comparison); the run uses
  the levels from "Generate AFS levels".
- Tiles rendered in parallel (Parallel.ForEach over branches); the process runs 64-bit.

Not in this step: elevation (tth), AFS4 install paths in DirectoryHelper, GeoConvert's coverage mask, matching
GeoConvert's brightening (open question below).

## Steps
- [ ] Branch `ttc-writer` (after Karl's WIP is committed or set aside)
- [ ] AfsGrid + WebMercator; check: map_12_44b0_a1d0 bounds = lat 42.61928..42.68298, lon -83.40820..-83.32031 (afsd)
- [ ] TtcWriter (header, BC1, zlib); check: afsd decodes a test pattern tile to the same image (BC1 error only)
- [ ] SourceTiles + TtcRenderer
- [ ] Action and setting in the UI; progress reporting like the other actions
- [ ] Build: Debug and Release, no new warnings

## Verify
- [ ] Pontiac square map_12_44b0_a1d0 (Bing z17, already downloaded in D:\temp\aeroscenery\working), levels 12-14:
  all 21 tiles decode with afsd
- [ ] Against an independent reference (Python, straight from the JPEGs): shift 0.000 px, PSNR >= 35 dB
- [ ] Against GeoConvert's tiles: x shift 0, y within ~1 px (GeoConvert's linear-latitude error), brightness differs
  by GeoConvert's curve
- [ ] Time per square against GeoConvert
- [ ] Sim: install into D:\afs4_extra_user_folder\scenery\images, look at Pontiac (KPTK) at every level

## Open questions
- GeoConvert brightens (source mean 64 -> 89; 42 -> 71, 81 -> 123, 148 -> 189): libpng applying the PNG's gAMA chunk,
  or IPACS's own curve? A grey ramp through GeoConvert with and without the chunk tells. Decides whether the writer
  should match it by default.
