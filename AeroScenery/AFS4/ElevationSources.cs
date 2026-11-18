using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AeroScenery.AFS4
{
    /// <summary>
    /// A source of elevation data in 1x1 degree cells: a download URL or a local path, made from a template.
    ///
    /// Template fields, for the cell whose south-west corner is (lat, lon):
    ///   {NS} {ns}  N or S (n or s) of the south edge     {lat}  |south edge|, 2 digits
    ///   {NSN} {nsN} N or S of the north edge              {latN} |north edge|, 2 digits
    ///   {EW} {ew}  E or W (e or w) of the west edge      {lon}  |west edge|, 3 digits
    /// e.g. Copernicus N42_00_W084_00 = {NS}{lat}_00_{EW}{lon}_00; USGS n43w084 = {nsN}{latN}{ew}{lon}.
    /// </summary>
    public class ElevationSource
    {
        /// <summary>The name shown in Settings; also the name of the download cache folder.</summary>
        public string Name { get; set; }

        /// <summary>An http(s) URL template (downloaded and cached) or a local file path template.</summary>
        public string Location { get; set; }

        /// <summary>"geotiff" (georeferenced by its own tags) or "hgt" (SRTM: big-endian int16, corner from the name).</summary>
        public string Format { get; set; }

        /// <summary>
        /// Treat a cell that doesn't exist (404, no file) and no-data samples as sea level. True for a global source
        /// that leaves out cells without land (Copernicus); false where a missing cell means no coverage.
        /// </summary>
        public bool MissingIsSea { get; set; }

        /// <summary>The name of a source to take a sample from where this one has none (optional).</summary>
        public string Fallback { get; set; }

        public string Attribution { get; set; }

        public string Notes { get; set; }

        [JsonIgnore]
        public bool IsDownload
        {
            get { return this.Location != null && (this.Location.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || this.Location.StartsWith("https://", StringComparison.OrdinalIgnoreCase)); }
        }

        /// <summary>The URL or path of the cell whose south-west corner is (lat, lon), whole degrees.</summary>
        public string CellLocation(int lat, int lon)
        {
            int latN = lat + 1;
            return this.Location
                .Replace("{NSN}", latN >= 0 ? "N" : "S")
                .Replace("{nsN}", latN >= 0 ? "n" : "s")
                .Replace("{latN}", Math.Abs(latN).ToString("00", CultureInfo.InvariantCulture))
                .Replace("{NS}", lat >= 0 ? "N" : "S")
                .Replace("{ns}", lat >= 0 ? "n" : "s")
                .Replace("{lat}", Math.Abs(lat).ToString("00", CultureInfo.InvariantCulture))
                .Replace("{EW}", lon >= 0 ? "E" : "W")
                .Replace("{ew}", lon >= 0 ? "e" : "w")
                .Replace("{lon}", Math.Abs(lon).ToString("000", CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// The list of elevation sources, kept in ElevationSources.json in the AeroScenery documents folder so users can
    /// add their own. Written with the defaults below if it doesn't exist.
    /// </summary>
    public static class ElevationSources
    {
        public const string FileName = "ElevationSources.json";

        public static List<ElevationSource> Defaults()
        {
            return new List<ElevationSource>
            {
                new ElevationSource
                {
                    Name = "Copernicus GLO-30",
                    Location = "https://copernicus-dem-30m.s3.amazonaws.com/Copernicus_DSM_COG_10_{NS}{lat}_00_{EW}{lon}_00_DEM/Copernicus_DSM_COG_10_{NS}{lat}_00_{EW}{lon}_00_DEM.tif",
                    Format = "geotiff",
                    MissingIsSea = true,
                    Attribution = "Copernicus DEM GLO-30, (c) DLR e.V. 2010-2014 and (c) Airbus Defence and Space GmbH 2014-2018, provided under COPERNICUS by the European Union and ESA",
                    Notes = "Worldwide, 1 arc-second (about 30 m), about 45 MB per cell. A surface model: heights include trees and buildings. Cells without land don't exist (sea level).",
                },
                new ElevationSource
                {
                    Name = "USGS 3DEP 1 arc-second",
                    Location = "https://prd-tnm.s3.amazonaws.com/StagedProducts/Elevation/1/TIFF/current/{nsN}{latN}{ew}{lon}/USGS_1_{nsN}{latN}{ew}{lon}.tif",
                    Format = "geotiff",
                    MissingIsSea = false,
                    Fallback = "Copernicus GLO-30",
                    Attribution = "U.S. Geological Survey, 3D Elevation Program",
                    Notes = "USA, Canada, Mexico; bare earth, about 30 m, about 55 MB per cell. Copernicus where it has no data (sea, outside coverage).",
                },
                new ElevationSource
                {
                    Name = "USGS 3DEP 1/3 arc-second",
                    Location = "https://prd-tnm.s3.amazonaws.com/StagedProducts/Elevation/13/TIFF/current/{nsN}{latN}{ew}{lon}/USGS_13_{nsN}{latN}{ew}{lon}.tif",
                    Format = "geotiff",
                    MissingIsSea = false,
                    Fallback = "Copernicus GLO-30",
                    Attribution = "U.S. Geological Survey, 3D Elevation Program",
                    Notes = "USA; bare earth, about 10 m, about 420 MB per cell. Worth it for TTH levels 11-12 (level 10 shows about 30 m). Copernicus where it has no data.",
                },
                new ElevationSource
                {
                    Name = "Local SRTM .hgt files",
                    Location = @"C:\DEM\hgt\{NS}{lat}{EW}{lon}.hgt",
                    Format = "hgt",
                    MissingIsSea = false,
                    Notes = "SRTM-style .hgt files you downloaded yourself (Viewfinder Panoramas, SRTM, ...), named like N47E008.hgt. Change the folder to yours.",
                },
            };
        }

        public static string PathIn(string documentsDirectory)
        {
            return Path.Combine(documentsDirectory, FileName);
        }

        /// <summary>
        /// Reads the sources from the file, writing the defaults first if there is no file.
        /// </summary>
        public static List<ElevationSource> Load(string path)
        {
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonConvert.SerializeObject(Defaults(), Formatting.Indented));
            }

            var sources = JsonConvert.DeserializeObject<List<ElevationSource>>(File.ReadAllText(path)) ?? new List<ElevationSource>();
            var problems = sources
                .Where(s => String.IsNullOrWhiteSpace(s.Name) || String.IsNullOrWhiteSpace(s.Location) || (s.Format != "geotiff" && s.Format != "hgt"))
                .Select(s => s.Name ?? "(no name)")
                .ToList();
            if (problems.Count > 0)
            {
                throw new InvalidDataException(String.Format("{0}: these sources need a Name, a Location and a Format of geotiff or hgt: {1}",
                    path, String.Join(", ", problems)));
            }

            return sources;
        }
    }
}
