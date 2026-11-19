using AeroScenery.OrthoPhotoSources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AeroScenery.Common
{
    public enum ActionSet
    {
        Default,
        Custom
    }

    public class Settings
    {
        public Settings()
        {
            this.OrthophotoSourceSettings = new OrthophotoSourceSettings();
        }

        public string AFS2Directory { get; set; }

        public string WorkingDirectory { get; set; }

        public string AeroSceneryDBDirectory { get; set; }

        public OrthophotoSource? OrthophotoSource { get; set; }

        public int? ZoomLevel { get; set; }

        public bool? DownloadImageTiles { get; set; }

        public bool? WriteTtcFiles { get; set; }

        public bool? InstallScenery { get; set; }

        public ActionSet? ActionSet { get; set; }

        public List<int> AFSLevelsToGenerate { get; set; }

        public string UserAgent { get; set; }


        public int? DownloadWaitMs { get; set; }

        public int? DownloadWaitRandomMs { get; set; }

        public int? SimultaneousDownloads { get; set; }


        public int? MapControlLastZoomLevel { get; set;}
        public double? MapControlLastX { get; set; }
        public double? MapControlLastY { get; set; }
        public string MapControlLastMapType { get; set; }
        public string AFS2UserDirectory { get; set; }

        // Write TTC Files: apply GeoConvert's brightening (AFS4\ToneCurves.cs)
        public bool? TtcGeoConvertBrightness { get; set; }
        // Write TTC Files: make the sea transparent (Aerofly's own water), from the OSM coastline (AFS4\Coastline.cs)
        public bool? TtcSeaTransparent { get; set; }
        // Write TTC Files, with the sea transparent: metres of sea next to land where the imagery stays as it is, then
        // metres over which it fades into the sea colour ("R,G,B"); the open sea beyond is transparent (AFS4\Coastline.cs)
        public int? TtcSeaKeepMetres { get; set; }
        public int? TtcSeaFadeMetres { get; set; }
        public string TtcSeaColour { get; set; }

        // Write TTH Files: elevation tiles from a source in ElevationSources.json (AFS4\ElevationSources.cs)
        public bool? WriteTthFiles { get; set; }
        public string ElevationSource { get; set; }
        public List<int> TthLevelsToGenerate { get; set; }


        public bool? GridSquareNamesFixed { get; set; }

        public OrthophotoSourceSettings OrthophotoSourceSettings { get; set; }

    }
}
