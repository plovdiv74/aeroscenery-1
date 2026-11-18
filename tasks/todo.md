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
- `TtcWriter` - mip chain (box), BC1 (own encoder, `Bc1Encoder`; see the review), zlib, header; writes
  `<square>\<source>\<zoom>-ttc\map_LL_XXXX_YYYY.ttc`, where the existing installer picks them up.
- UI/settings: a new action "Write TTC files" next to the GeoConvert ones (which stay for comparison); the run uses
  the levels from "Generate AFS levels".
- Tiles rendered in parallel (Parallel.ForEach over branches); the process runs 64-bit.

Not in this step: elevation (tth), AFS4 install paths in DirectoryHelper, GeoConvert's coverage mask, matching
GeoConvert's brightening (open question below).

## Steps
- [x] Branch `ttc-writer` (after Karl's WIP is committed or set aside)
- [x] AfsGrid + WebMercator; check: map_12_44b0_a1d0 bounds = lat 42.61928..42.68298, lon -83.40820..-83.32031 (afsd)
- [x] TtcWriter (header, BC1, zlib); check: afsd decodes a test pattern tile to the same image (BC1 error only)
- [x] SourceTiles + TtcRenderer
- [x] Action and setting in the UI; progress reporting like the other actions
- [x] Build: Debug and Release, no new warnings

## Verify
- [x] Pontiac square map_12_44b0_a1d0 (Bing z17, already downloaded in D:\temp\aeroscenery\working), levels 12-14:
  all 21 tiles decode with afsd
- [x] Against an independent reference (Python, straight from the JPEGs): shift 0.000 px, PSNR >= 35 dB
- [x] Against GeoConvert's tiles: x shift 0, y within ~1 px (GeoConvert's linear-latitude error), brightness differs
  by GeoConvert's curve
- [x] Time per square against GeoConvert
- [ ] Sim: install into D:\afs4_extra_user_folder\scenery\images, look at Pontiac (KPTK) at every level

## Open questions
- GeoConvert brightens (source mean 64 -> 89; 42 -> 71, 81 -> 123, 148 -> 189): libpng applying the PNG's gAMA chunk,
  or IPACS's own curve? A grey ramp through GeoConvert with and without the chunk tells. Decides whether the writer
  should match it by default.

## Review (2026-11-18)
Built on branch `ttc-writer`: `AeroScenery/AFS4/` (AfsGrid, WebMercator, Bc1Encoder, TtcWriter, SourceTiles, TtcRenderer),
a "Write TTC Files (AFS4, no GeoConvert)" action (setting `WriteTtcFiles`, off in the default action set), output in
`<square>\<source>\<zoom>-ttc\`. Checked with a scratch net48 harness that compiles the AFS4 files, plus afsd and Python.
- Grid: map_12_44b0_a1d0 = lat 42.61928..42.68298, lon -83.40820..-83.32031, as afsd.
- Writer: a test pattern decodes in afsd (zlib) to the source at 46.8 dB, the right way up; every mip level matches
  the box-filtered source (BC1 error only); header as the stock us tiles.
- BC1: own encoder (principal axis + 2 least-squares refinements, 4-colour mode, rounded palette) instead of
  BCnEncoder.NET, whose net48 dependency chain (CommunityToolkit.HighPerformance, System.Memory, Bcl.Numerics, ...)
  would have needed hand-maintained packages.config entries and binding redirects. On a Pontiac tile: 34.98 dB
  against DirectXTex's 34.60 dB (afsd -t), mean bias -0.05 (truncating the palette gave -0.11 and +0.2 after decoding).
- Pontiac levels 12-14: 21 tiles in 2.7 s (GeoConvert: about 4.5 min after stitching); all decode with afsd.
  - The raw render equals an independent PIL bilinear reference rounded (GDI+ and PIL decode these JPEGs identically).
  - Level 14 after BC1 vs the reference: 34.98..36.65 dB, shift 0.000 px; levels 12-13 match the box of their children.
  - GeoConvert's tiles: x shift 0.00; GeoConvert sits 0.24..1.02 px further south, most in the middle of its stitched
    image (its linear-latitude error); it also brightens (40 -> 67, 100 -> 143, 200 -> 226), ours keeps Bing's colours.
- Not yet: the sim, and a run through the UI (Karl).

## GeoConvert brightness option (2026-11-18)
- Open question answered: GeoConvert 1.5.17 applies a fixed per-channel lookup table. Grey ramps through GeoConvert
  (raw output): identical with and without the PNG's sRGB/gAMA chunks (not libpng gamma); R, G, B ramps each follow
  the grey table, independent of the other channels (not auto levels); the same values as on the Bing tiles.
  No simple formula fits (power, 1-(1-x)^p: off by 17+), so AFS4/ToneCurves.cs keeps the measured 256 entries.
- Option "Write TTC Files: Brighten Images Like GeoConvert" (Settings, Image Processing tab; setting
  TtcGeoConvertBrightness, off by default) applies it to the source pixels as they are decoded.
- Pontiac square with the option: mean brightness within 0.1..0.5 of GeoConvert's tiles on all 21 tiles, mid-tones
  one to one (64 -> 63, 100 -> 100, 150 -> 150). Curve on the source pixels vs after sampling: 0.33 vs 0.47 from
  GeoConvert's raw output, so on the source (cheaper, applied once per source pixel).

# Install action (planned 2026-11-18)

## What exists
- Toolbar "Install Scenery" (one selected square): copies every *.ttc under the square's working folder (GeoConvert's
  and ours alike; a duplicate-name warning if both exist) into <user folder>scenery/images/<level-9 square>/, after a
  confirmation. Destination: Settings "AFS2 User Folder" (Karl: D:/afs4_extra_user_folder/), else
  Documents/Aerofly FS 2/scenery/images (AFS2, wrong for AFS4).
- Actions: "Install Scenery In AFS User Scenery Folder" checkbox, hidden from the logic (commented out); its handler
  sets DownloadImageTiles from the download checkbox (bug).

## Plan
- [x] Action "Install Scenery": after "Write TTC Files" for each square, copy the tiles that run wrote, no prompt
      (ticking the action is the consent); log the destination and count. GeoConvert's output isn't ready at that
      point (it runs in its own processes), so it stays with the toolbar button.
- [x] Installer: one copy routine for both; the level-9 folder from the tile name (AfsTile, exact) instead of the
      lat/lon lookup; files coarser than level 9 into a folder of their own name.
- [x] Destination: the user folder as now; fallback Documents/Aerofly FS 4/scenery/images; label "Aerofly User
      Folder" (setting name unchanged, so settings.xml keeps working).
- [x] Stale copies: before copying, look for the same file names elsewhere under scenery/images (e.g. a hand-copied
      map_12_44b0_a1d0 folder of GeoConvert tiles); the sim would see both. Warn and list them (not deleted).
- [x] Fix the checkbox handler; enable the checkbox in custom actions.
- [x] Verify: build (Debug, Release; no new warnings); SceneryInstaller.Install with the 21 Pontiac tiles into a
      scratch user folder: all in map_09_4480_a180, an old tile there overwritten (byte-identical after), a stale copy
      in a hand-made map_12_44b0_a1d0 folder reported and left alone. Decided with Karl: warn, don't delete; default
      actions unchanged.
- [ ] The UI (Karl).

# Elevation: write tth files straight from AeroScenery (planned 2026-11-18)

## Facts (afsd: terrain_tile.h, terrain_import.cpp, tasks/todo.md "Terrain tiles")
- tth: 0x100-byte header (magic 0x9fbf1, 0x100, level, payload size, 2113568, -500, 9000, 1028, 2), then the payload,
  LZHAM or zlib (stock has both; zlib is enough). 1028 x 1028 uint16, rows south to north; sample (u, r) at the tile's
  corner + (u - 2, r - 2) * span / 1024 (GeoConvert's resampler); height = -500 + v * 9500 / 65535 m, rounded.
- Stock tth levels: 7, 9, 10. Level 10 samples ~1.2" apart (~28 m at 42 N), i.e. a 1" DEM. Whether the sim uses
  levels 11+ is unknown (sim test below).
- afsd -th already does this from .hgt (bilinear, voids -> tile skipped); in the sim at Zurich, level 10 (2026-11-17).
- DEM sources checked 2026-11-18 (public, no login):
  - Copernicus GLO-30 (AWS open data, COG GeoTIFF float32, 1 deg cells, ~43 MB; a cell with no land is a 404 = sea):
    copernicus-dem-30m.s3.amazonaws.com/Copernicus_DSM_COG_10_N42_00_W084_00_DEM/<same>.tif
  - USGS 3DEP 1" (~51 MB/cell) and 1/3" (~421 MB/cell), US only, GeoTIFF float32, named by the NORTH-west corner:
    prd-tnm.s3.amazonaws.com/StagedProducts/Elevation/{1,13}/TIFF/current/n43w084/USGS_{1,13}_n43w084.tif
  - Viewfinder Panoramas (zips of .hgt grouped irregularly), SRTM, ALOS: manual downloads -> a local-folder source.
- GeoTIFF reading: BitMiracle.LibTiff.NET 2.4.660 (BSD, netstandard2.0, no dependencies; one packages.config
  entry). Georeferencing from the GeoTIFF tags (tie point, pixel scale, PixelIsArea/PixelIsPoint), not from names.

## Design (AeroScenery/AFS4/)
- `ElevationSources.json` (shipped defaults, copied to Documents\AeroScenery\ on first run, user-editable): per source
  a name, a URL template or a local folder, format (geotiff | hgt), cell size (1 deg), "missing cell = sea" flag,
  attribution. Template fields: {NS}{ns}{lat:00} south edge, {latN:00} north edge, {EW}{ew}{lon:000}. Defaults:
  Copernicus GLO-30 (default), USGS 3DEP 1", USGS 3DEP 1/3", Local folder. Settings: the selected source.
- `DemCells` - download cache (<working>\dem\<source>\), loads a 1-deg cell (GeoTIFF via LibTiff.Net, .hgt), bilinear
  height at lon/lat, voids/no-data handled (nearest valid within the cell; else the tile is incomplete).
- `TthWriter` - header + zlib, as TtcWriter.
- `TthRenderer` - the tiles of the selected elevation levels for a square: for a level coarser than the square, the one
  tile containing it; finer, all tiles in it. Samples at GeoConvert's positions; incomplete tiles skipped and listed.
- UI: action "Write TTH Files" (custom actions), elevation levels (default 10) on the Images tab next to the image
  levels; "Install" also copies tth into scenery\elevation\<level-9 folder>\. The half-built Elevation tab (USGS
  EarthExplorer) is left as it is.

## Verify
- [x] Against afsd -th on the same SRTM .hgt (local source): identical samples (all 1,056,784 of maph_10_4480_a1c0).
- [x] KPTK (published field elevation 298.7 m, the runways' highest point): 295.8 m SRTM, 295.1 m Copernicus at the ARP.
- [x] Stock tiles there are level 7 only: SRTM vs stock maph_07_4400_a000 (resampled) +0.06 m, RMS 2.2 m (stock is SRTM-
      based); Copernicus +4.4 m. Georeferencing, Monadnock (hilly) tile, phase correlation: the three sources agree
      within 0.1..0.7 samples (3..20 m), no half-pixel error in either GeoTIFF convention (Copernicus PixelIsPoint,
      USGS PixelIsArea with a 6-sample overlap). Copernicus (a DSM) is +11 m in NH forest, +4 m at Pontiac; USGS is
      bare earth, SRTM within 0.7 m of it. In the US, USGS 1" is the better source; Copernicus worldwide.
- [x] Missing Copernicus cells (404: N40W071, N40W072) -> .missing markers, sea level; Nantucket tile 95% sea, land
      to 25.6 m. A local .hgt source with a cell missing -> the tile is reported incomplete, not written.
- [x] Sim, level 10 at KEEN from USGS 1" (Karl, 2026-11-18): looks good, KEEN field elevation right; Mount Monadnock
      in the sim's debug output 962.73 m at 42.861520, -72.108413; the tile's highest sample 962.4 m at 42.86154,
      -72.10826 (published 965 m: 1" samples round the peak off).
- [x] Sim: levels 11/12 from USGS 1/3" (4 + 16 tiles over maph_10_4c80_a200; Karl, 2026-11-18): the sim uses them.
      Mount Monadnock 962.73 m (level 10 only) -> 964.78 m at 42.861398, -72.108238 (level 12's highest sample 964.7 m
      at 42.86140, -72.10816; published 965 m). KEEN east parking 147.01 m in the sim vs 145.4 m in the tiles (field
      elevation 148.7 m: the sim seems to blend airports toward their own elevation). Levels 11-12 relabelled in
      Settings ("best with 1/3" data"); the default stays 10.
- [x] Progress label: cell messages reached the UI through two Progress<T> hops and could arrive after the last
      one, leaving "Loading elevation cell ..." after the run; now forwarded directly, and "Finished" at the end.
- [x] Built: Debug, Release, no new warnings; the app starts without exceptions; ElevationSources.json written with the
      defaults and read back (URLs as above); Install puts ttc in images\<level-9>\ and tth flat in scenery\elevation\.
- Deviation from the plan: source and TTH levels are on a new Elevation tab in Settings (no room on the Images tab);
  the action checkbox is on the Images tab.

# Remove the GeoConvert route (planned 2026-11-18)

Karl keeps GeoConvert itself and runs it by hand for comparisons; AeroScenery no longer drives it.

- [x] Remove the stitcher (ImageProcessing/TileStitcher*, Common/StitchedImage), AID/TMC generation
      (AFS2/AFSFileGenerator*, AIDFile, TMCFile), GeoConvertManager, and the unused TOCFile and TSCFile.
- [x] Remove the actions "Stitch Image Tiles", "Generate AID / TMC Files", "Run GeoConvert" and the hidden
      "Delete Stitched Images"; close the gaps in the Actions box.
- [x] Remove the settings: AFS2 SDK folder, GeoConvert tab (wrapper, raw files, mask, smaller runs), maximum stitched
      image size, TMC shrink, delete stitched; and the old registry migration lines for them.
- [x] Image Processing tab: remove the sliders, their filters and the preview form (AForge goes with them); keep the
      tab with "Brighten Images Like GeoConvert".
- [x] Remove the unfinished Elevation side tab (USGS EarthExplorer squares, ASTER scraper, USGS settings tab,
      ElevationSettings). The "USGS (Coming Soon)" image source stub stays (imagery, not GeoConvert).
- [x] Toolbar "Install Scenery": install the square's own <zoom>-ttc and tth output instead of every *.ttc in the
      square's folder.
- [ ] Verify: Debug and Release build, no new warnings; the app starts; settings.xml with the old elements still
      loads; a default run on a square (download, TTC, TTH, install) still works (Karl).
