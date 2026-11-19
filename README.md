## AeroScenery - aerial imagery and elevation scenery for Aerofly FS 4

AeroScenery downloads aerial imagery (Bing or Google) and elevation data for the grid squares you pick on a map, and
writes them as Aerofly FS 4 terrain tiles: `map_LL_XXXX_YYYY.ttc` (images) and `maph_LL_XXXX_YYYY.tth` (elevation).
It writes the tiles itself, so the Aerofly SDK and GeoConvert aren't needed. Coming from an earlier AeroScenery?
See "Removed from earlier versions" below.

### Quick start

1. Start AeroScenery and click **Settings**:
   - **Working Folder**: where downloads and generated tiles go.
   - **Aerofly User Folder**: the folder that holds (or will hold) `scenery\images` and `scenery\elevation`, e.g.
     your extra user folder. Without one, AeroScenery installs into `Documents\Aerofly FS 4\scenery`.
   - **Elevation** tab: pick an elevation source (see below).
2. Click a grid square on the map (the "Grid Square Selection Size" in the toolbar sets its level).
3. Pick the image source and zoom level. Zoom 17 (about 1.2 m per pixel) fills image tiles down to level 14.
4. Under "Generate AFS Levels" tick the image levels, e.g. 12, 13 and 14 for a level-12 square. Aerofly draws the
   finest level it has, so go down to the finest level of the stock scenery there (14 in much of the US).
5. Under "Actions" choose **Run Default Actions** and click **Start**.

The default actions download the image tiles, write the TTC and TTH files and install them. Restart Aerofly to see
them.

### Actions

| Action | What it does |
| --- | --- |
| Download Image Tiles | Downloads the 256-pixel image tiles covering the square into `<working folder>\<square>\<source>\<zoom>\`. |
| Write TTC Files (AFS4 Images) | Renders the selected image levels straight from the downloaded tiles into `<square>\<source>\<zoom>-ttc\`: the finest level at one tile pixel per source pixel, each coarser level averaged from the finer tiles; BC1 with mipmaps, zlib-compressed. Out at sea the imagery fades into Aerofly's sea colour (see "Sea and coastlines"); tiles of open sea only aren't written. Tiles with missing image tiles (failed downloads) are skipped and listed. |
| Write TTH Files (AFS4 Elevation) | Writes the selected elevation levels from the chosen elevation source into `<square>\elevation\<source>\`. A level coarser than the square gets the one tile containing it. |
| Install TTC/TTH Files In Aerofly User Folder | Copies the square's TTC files into `scenery\images\<level-9 tile>\` and its TTH files into `scenery\elevation\` of the Aerofly user folder, overwriting tiles of the same name. Other copies of the same tiles elsewhere in those folders (which Aerofly would also load) are listed, not deleted. |

The toolbar's "Install Scenery" button installs the selected square's TTC and TTH files the same way, without a run.

### Elevation

Elevation sources are listed in `Documents\AeroScenery\ElevationSources.json`, which AeroScenery writes with these
defaults the first time it's needed. Settings > Elevation shows them, with "Edit Sources File" and "Reload".

| Source | Coverage | Notes |
| --- | --- | --- |
| Copernicus GLO-30 | Worldwide, 1" (about 30 m) | About 45 MB per 1-degree cell. A surface model: heights include trees and buildings (4 to 11 m high in forest). Cells without land are sea level. |
| USGS 3DEP 1 arc-second | USA, Canada, Mexico | About 55 MB per cell. Bare earth: the better choice where it exists. Copernicus where it has no data. |
| USGS 3DEP 1/3 arc-second | USA, about 10 m | About 420 MB per cell. Worth it for TTH levels 11 and 12. Copernicus where it has no data. |
| Local SRTM .hgt files | Your own files | `.hgt` files you downloaded yourself (Viewfinder Panoramas, SRTM, ...). Change the folder to yours. |

Each entry has a `Name`, a `Location` (an http(s) URL or a local path, with the fields below filled in for each
1-degree cell), a `Format` (`geotiff` or `hgt`), `MissingIsSea` (treat a missing cell and no-data samples as sea
level), an optional `Fallback` (the name of another source to use where this one has no data), `Attribution` and
`Notes`. Location fields, for the cell whose south-west corner is (lat, lon):

- `{NS}` `{ns}`: N or S (n or s) of the south edge; `{lat}`: its absolute value, 2 digits
- `{NSN}` `{nsN}`: N or S of the north edge; `{latN}`: its absolute value, 2 digits
- `{EW}` `{ew}`: E or W (e or w) of the west edge; `{lon}`: its absolute value, 3 digits

For example Copernicus' `N42_00_W084_00` is `{NS}{lat}_00_{EW}{lon}_00`, and USGS's `n43w084` is
`{nsN}{latN}{ew}{lon}`. Downloads are cached in `<working folder>\elevation-data\`.

TTH levels (Settings > Elevation): the stock scenery uses levels 7, 9 and 10; level 10 (samples about 30 m apart) is
the default. Aerofly also uses levels 11 and 12, which are worth writing from 1/3" data: at Mount Monadnock (NH) the
summit rose from 962.7 m with level 10 to 964.8 m with level 12 (published: 965 m).

### Image brightness

GeoConvert brightens images with a fixed curve; AeroScenery's TTC files keep the source's colours. To match
GeoConvert's look, tick "Write TTC Files: Brighten Images Like GeoConvert" in Settings > Image Processing.

### Sea and coastlines

Bing's and Google's water is much lighter than Aerofly's own (at Provincetown about RGB 42,60,76 against 8,23,47), so
imagery that simply ends out at sea shows as a band. Aerofly's own tiles avoid this by fading their photo into a solid
sea colour, and AeroScenery does the same:

- **Near the shore** the imagery stays as it is, so marsh, tidal inlets and shallows keep it. Setting: "Keep the
  Imagery Offshore For (m)" (default 200).
- **Further out** its colour fades smoothly into the sea colour. Setting: "Then Fade It Into the Sea Colour Over (m)"
  (default 800).
- **The open sea beyond** is the solid sea colour. Setting: "Sea Colour (R,G,B)" (default 8,23,47, Aerofly's at
  Provincetown). A TTC tile that is all open sea isn't written, so Aerofly's own sea shows there, in the same colour.
- **The coastline** comes from OpenStreetMap: the land polygons from
  [osmdata.openstreetmap.de](https://osmdata.openstreetmap.de/data/land-polygons.html), about 930 MB, downloaded once
  into `<working folder>\coastline\`. Lakes and rivers aren't sea. All of this is on with "Write TTC Files: Make the Sea
  Transparent" (on by default; the name is from an earlier version).
- **No imagery:** where the image source has none (Bing answers "no-tile" over open water), the tile shows the sea
  colour, or is transparent close to land. The downloader remembers these as `.notile` files and doesn't ask again.
- **Tiles with nothing left to show** aren't written. Copies from earlier runs are removed from the output folder, and
  by Install from the Aerofly user folder.

To compare AeroScenery's tiles with Aerofly's own, the separate afsd tool's `afsd -dds` writes any TTC file as a DDS
image (with its mipmaps and transparency), which Paint.NET and Visual Studio open.

### Tested in Aerofly FS 4

**Provincetown (KPVC), Massachusetts.** Three level-12 squares (`map_12_4e00_a140`, `map_12_4e10_a140`,
`map_12_4e10_a130`) with the default actions: Bing imagery at zoom 17 (image levels 12-14) and elevation level 10 from
USGS 3DEP 1/3" with the Copernicus fallback. In the sim the imagery looks right, and the elevation is detailed enough to
show the beach at sea level against the higher ground inland. Offshore, where USGS has no data and Copernicus has no
cells, the elevation is sea level.

Without a sea treatment, the edges of Bing's imagery out at sea showed as straight bands. With the defaults above
(imagery kept 200 m offshore, faded into 8,23,47 over 800 m) the coast looks like Aerofly's own, with no visible edge
where AeroScenery's tiles meet the stock sea, and the salt marsh at Hatches Harbor keeps its imagery. Of the 21 TTC
tiles of `map_12_4e10_a130`, 8 are all open sea and aren't written. Faint horizontal stripes can show in the fade: Bing's
source images differ in brightness from row to row.

Approaches that didn't work, for the record:
- Cutting the sea where the elevation data is at or below sea level: a blocky coastline (30 m steps with Copernicus)
  and holes in salt marshes.
- A transparent sea cut at the OSM coastline: OSM maps tidal inlets and marsh as sea, so they lost their imagery, and
  the stock tiles' own near-shore photo showed through.
- Fading the imagery out with dithered transparency: a visible stipple. BC1 transparency is all or nothing per pixel,
  and BC3 (DXT5), which has smooth alpha, isn't an option: Aerofly reads a TTC's mip chain as BC1 only.

### Building

Open `AeroScenery\AeroScenery.sln` in Visual Studio 2026 (version 18) with the .NET Framework 4.8 targeting pack.
The solution has two configurations, **Debug** and **Release**, both for the **x64** platform; the output goes to
`AeroScenery\bin\Debug\` or `bin\Release\`. AeroScenery is 64-bit only: the native SQLite package doesn't support
Any CPU builds, and a USGS 1/3" elevation cell alone takes about 470 MB of memory.

The NuGet packages are `PackageReference` items in `AeroScenery.csproj` and are restored on build (from the command
line: `msbuild AeroScenery.sln -restore -p:Configuration=Release -p:Platform=x64`); binding redirects are generated.
The packages: BitMiracle.LibTiff.NET (GeoTIFF elevation); Dapper, System.Data.SQLite 2.0.4 and SQLite 3.54.1 (the
grid-square database: the managed provider, and the native `e_sqlite3.dll` it loads); GMap.NET.WinForms 2.1.7 (the
map, whose tile cache also uses SQLite); log4net; Newtonsoft.Json. System.Data.SqlClient is referenced only to lift
GMap.NET's own dependency on it (4.8.3) to a version without security advisories.

### Removed from earlier versions

AeroScenery used to build Aerofly FS 2 scenery through the SDK's GeoConvert. With the tiles now written directly,
these are gone:

- **The GeoConvert route:** the actions "Stitch Image Tiles", "Generate AID / TMC Files" and "Run GeoConvert", the
  stitched images, the AFS2 SDK folder, and the GeoConvert and tile-stitching settings. GeoConvert can still be run by
  hand on your own AID/TMC files.
- **Image adjustment sliders** (brightness, contrast, saturation, sharpness, red/green/blue) and their preview window:
  they only applied to stitched images. "Brighten Images Like GeoConvert" remains.
- **The Elevation tab of the main window** (USGS EarthExplorer squares, ASTER downloads, the USGS login): never
  finished, and replaced by Write TTH Files with the sources above.
- **Show Airports:** the airport markers came from fscloudport.com, which no longer exists.
- **The Help and "Get AeroFly FS2 SDK" buttons:** they opened the original project's page and an SDK download page
  that no longer exists.
- **The cultivation editor** remnants, and its DebugSceneryEditor build configuration.
- **The x86 solution platform:** it only mapped onto the Any CPU build.

Settings from earlier versions are kept where they still apply; the others are ignored.

### Data credits

Respect the terms of the imagery and elevation services you use. The default elevation sources ask for these
credits:

- Copernicus DEM GLO-30: (c) DLR e.V. 2010-2014 and (c) Airbus Defence and Space GmbH 2014-2018, provided under
  COPERNICUS by the European Union and ESA.
- USGS 3DEP: U.S. Geological Survey, 3D Elevation Program.

The coastline comes from OpenStreetMap: (c) OpenStreetMap contributors, available under the Open Database License
(ODbL).

AeroScenery reads GeoTIFF elevation data with [LibTiff.Net](https://github.com/BitMiracle/libtiff.net) (BSD license).
