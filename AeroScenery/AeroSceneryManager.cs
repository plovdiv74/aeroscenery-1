using AeroScenery.AFS2;
using AeroScenery.AFS4;
using AeroScenery.Common;
using AeroScenery.Controls;
using AeroScenery.Data;
using AeroScenery.Data.Mappers;
using AeroScenery.Data.Models;
using AeroScenery.Download;
using AeroScenery.FileManagement;
using AeroScenery.OrthophotoSources;
using AeroScenery.OrthoPhotoSources;
using AeroScenery.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AeroScenery
{
    public class AeroSceneryManager
    {
        private MainForm mainForm;

        private BingOrthophotoSource bingOrthophotoSource;
        private GoogleOrthophotoSource googleOrthophotoSource;
        private USGSOrthophotoSource usgsOrthophotoSource;

        private DownloadManager downloadManager;

        private static AeroSceneryManager aeroSceneryManager;

        private ImageTileService imageTileService;

        private Common.Settings settings;

        private SettingsService settingsService;

        private IDataRepository dataRepository;

        private GridSquareMapper gridSquareMapper;

        private CancellationTokenSource ttcCancellation;

        private List<ImageTile> imageTiles;
        private readonly ILog log = LogManager.GetLogger("AeroScenery");
        private string version;
        private int incrementalVersion;

        public AeroSceneryManager()
        {
            downloadManager = new DownloadManager();
            imageTileService = new ImageTileService();
            settingsService = new SettingsService();
            gridSquareMapper = new GridSquareMapper();
            dataRepository = new SqlLiteDataRepository();

            imageTiles = null;
            version = "2.0.0.1";
            incrementalVersion = 9;
        }

        public Settings Settings
        {
            get
            {
                return this.settings;
            }
        }

        public string Version
        {
            get
            {
                return this.version;
            }
        }

        public int IncrementalVersion
        {
            get
            {
                return this.incrementalVersion;
            }
        }

        public static AeroSceneryManager Instance
        {
            get
            {
                if (AeroSceneryManager.aeroSceneryManager == null)
                {
                    aeroSceneryManager = new AeroSceneryManager();
                }

                return aeroSceneryManager;
            }
        }

        public void Initialize()
        {
            // Create settings if required and read them
            this.settings = settingsService.GetSettings();
            settingsService.LogSettings(this.settings);
            settingsService.CheckConfiguredDirectories(this.settings);

            this.dataRepository.Settings = settings;
            this.dataRepository.UpgradeDatabase();

            var gridSquareNameFixer = new GridSquareNameFixer(settings, this.dataRepository, this.settingsService);
            gridSquareNameFixer.FixGridSquareNames();

            bingOrthophotoSource = new BingOrthophotoSource(settings.OrthophotoSourceSettings.BN_OrthophotoSourceUrlTemplate);
            googleOrthophotoSource = new GoogleOrthophotoSource(settings.OrthophotoSourceSettings.GM_OrthophotoSourceUrlTemplate);
            usgsOrthophotoSource = new USGSOrthophotoSource();


            this.mainForm = new MainForm();
            this.mainForm.StartStopClicked += async (sender, eventArgs) =>
            {
                if (this.mainForm.ActionsRunning)
                {
                    await StartSceneryGenerationProcessAsync(sender, eventArgs);
                }
                else
                {
                    StopSceneryGenerationProcess(sender, eventArgs);
                }
            };

            this.mainForm.ResetGridSquare += (sender, name) =>
            {
                this.ResetGridSquare(name);
            };

            this.mainForm.Initialize();
            Application.Run(this.mainForm);

        }


        private string GetTileDownloadDirectory(string afsGridSquareDirectory)
        {
            var tileDownloadDirectory = afsGridSquareDirectory;

            switch (this.settings.OrthophotoSource)
            {
                case OrthophotoSource.Bing:
                    tileDownloadDirectory += @"\b\";
                    break;
                case OrthophotoSource.Google:
                    tileDownloadDirectory += @"\g\";
                    break;
            }

            return tileDownloadDirectory;
        }

        private string GetOrthophotoSourcePrefix()
        {
            string prefix = "";

            switch (this.settings.OrthophotoSource)
            {
                case OrthophotoSource.Bing:
                    prefix = "b";
                    break;
                case OrthophotoSource.Google:
                    prefix = "g";
                    break;
            }

            return prefix;
        }

        public void StopSceneryGenerationProcess(object sender, EventArgs e)
        {
            downloadManager.StopDownloads();
            this.ttcCancellation?.Cancel();

            if (this.imageTiles != null)
            {
                this.imageTiles.Clear();
                this.imageTiles = null;
                System.GC.Collect();
            }

        }

        private void ActionsComplete()
        {
            this.mainForm.ActionsComplete();

            if (this.imageTiles != null)
            {
                this.imageTiles.Clear();
                this.imageTiles = null;
                System.GC.Collect();
            }

        }


        public async Task StartSceneryGenerationProcessAsync(object sender, EventArgs e)
        {
            try
            {
                int i = 0;
                var installResults = new List<InstallResult>();
                List<ElevationSource> elevationSources = null;
                ElevationSource elevationSource = null;
                foreach (AFS2GridSquare afs2GridSquare in this.mainForm.SelectedAFS2GridSquares.Values.Select(x => x.AFS2GridSquare))
                {
                    var currentGrideSquareMessage = String.Format("Working on AFS Grid Square {0} of {1}", i + 1, this.mainForm.SelectedAFS2GridSquares.Count());
                    this.mainForm.UpdateParentTaskLabel(currentGrideSquareMessage);
                    log.Info(currentGrideSquareMessage);

                    // Do we have a directory for this afs grid square in our working directory?
                    var afsGridSquareDirectory = this.settings.WorkingDirectory + afs2GridSquare.Name;

                    if (!Directory.Exists(this.settings.WorkingDirectory + afs2GridSquare.Name))
                    {
                        Directory.CreateDirectory(this.settings.WorkingDirectory + afs2GridSquare.Name);
                    }

                    var tileDownloadDirectory = GetTileDownloadDirectory(afsGridSquareDirectory) + this.settings.ZoomLevel + @"\";
                    var ttcDirectory = GetTileDownloadDirectory(afsGridSquareDirectory) + this.settings.ZoomLevel + @"-ttc\";

                    // Do we have a directory for the afs grid square and this orthophoto source
                    if (!Directory.Exists(tileDownloadDirectory))
                    {
                        Directory.CreateDirectory(tileDownloadDirectory);
                    }

                    // Download Imamge Tiles
                    if (this.Settings.DownloadImageTiles.Value && this.mainForm.ActionsRunning)
                    {
                        this.mainForm.UpdateChildTaskLabel("Calculating Image Tiles To Download");
                        log.Info("Calculating Image Tiles To Download");


                        var imageTilesTask = Task.Run(() => {

                            // Get a list of all the image tiles we need to download
                            switch (settings.OrthophotoSource)
                            {
                                case OrthophotoSource.Bing:
                                    imageTiles = bingOrthophotoSource.ImageTilesForGridSquares(afs2GridSquare, settings.ZoomLevel.Value);
                                    break;
                                case OrthophotoSource.Google:
                                    imageTiles = googleOrthophotoSource.ImageTilesForGridSquares(afs2GridSquare, settings.ZoomLevel.Value);
                                    break;
                                case OrthophotoSource.USGS:
                                    imageTiles = usgsOrthophotoSource.ImageTilesForGridSquares(afs2GridSquare, settings.ZoomLevel.Value);
                                    break;
                            }
                        });

                        await imageTilesTask;

                        this.mainForm.UpdateChildTaskLabel("Downloading Image Tiles");
                        log.Info("Downloading Image Tiles");

                        // Capture the progress of each thread
                        var downloadThreadProgress = new Progress<DownloadThreadProgress>();
                        downloadThreadProgress.ProgressChanged += DownloadThreadProgress_ProgressChanged;

                        // Send the image tiles to the download manager
                        await downloadManager.DownloadImageTiles(settings.OrthophotoSource.Value, imageTiles, downloadThreadProgress, tileDownloadDirectory);

                        // Only finalise if we weren't cancelled
                        if (this.mainForm.ActionsRunning)
                        {
                            var existingGridSquare = this.dataRepository.FindGridSquare(afs2GridSquare.Name);

                            if (existingGridSquare == null)
                            {
                                this.dataRepository.CreateGridSquare(this.gridSquareMapper.ToModel(afs2GridSquare));
                                this.mainForm.AddDownloadedGridSquare(afs2GridSquare);
                            }
                        }


                    }

                    // Write TTC files from the downloaded image tiles
                    if (this.Settings.WriteTtcFiles.Value && this.mainForm.ActionsRunning)
                    {
                        this.mainForm.UpdateChildTaskLabel("Writing TTC Files");
                        log.Info("Writing TTC Files");

                        await WriteTtcFilesAsync(afs2GridSquare, tileDownloadDirectory, ttcDirectory);
                    }

                    // Write TTH (elevation) files from the selected elevation source
                    string tthDirectory = null;
                    if ((this.Settings.WriteTthFiles.Value || this.Settings.InstallScenery.Value) && this.mainForm.ActionsRunning)
                    {
                        if (elevationSources == null)
                        {
                            elevationSources = LoadElevationSources();
                            elevationSource = elevationSources == null ? null : SelectedElevationSource(elevationSources);
                        }

                        if (elevationSource != null)
                        {
                            tthDirectory = Path.Combine(afsGridSquareDirectory, "elevation", SafeFolderName(elevationSource.Name)) + @"\";
                        }
                    }

                    if (this.Settings.WriteTthFiles.Value && this.mainForm.ActionsRunning && elevationSource != null)
                    {
                        this.mainForm.UpdateChildTaskLabel("Writing TTH Files");
                        log.Info(String.Format("Writing TTH Files from {0}", elevationSource.Name));

                        await WriteTthFilesAsync(afs2GridSquare, elevationSources, elevationSource, tthDirectory);
                    }

                    // Install the square's TTC and TTH files (from Write TTC/TTH Files) into the Aerofly user folder
                    if (this.Settings.InstallScenery.Value && this.mainForm.ActionsRunning)
                    {
                        this.mainForm.UpdateChildTaskLabel("Installing Scenery");
                        var installResult = await InstallTilesAsync(afs2GridSquare, ttcDirectory, tthDirectory);
                        if (installResult == null)
                        {
                            break;
                        }

                        installResults.Add(installResult);
                    }

                    i++;

                }

                // Installed tiles that also exist elsewhere under scenery\images: the sim loads both
                SceneryInstaller.ShowOtherCopiesMessage(installResults);

                this.ActionsComplete();

            }
            finally
            {
                if (this.imageTiles != null)
                {
                    this.imageTiles.Clear();
                    this.imageTiles = null;
                    System.GC.Collect();
                }
            }


        }

        /// <summary>
        /// Renders the selected AFS levels of a grid square from its downloaded image tiles into AFS4 ttc files
        /// (AFS4\TtcRenderer.cs).
        /// </summary>
        private async Task WriteTtcFilesAsync(AFS2GridSquare afs2GridSquare, string tileDownloadDirectory, string ttcDirectory)
        {
            var prefix = GetOrthophotoSourcePrefix();
            string problem = null;
            if (String.IsNullOrEmpty(prefix))
            {
                problem = "Writing TTC files works with Bing and Google imagery only";
            }
            else if (this.settings.AFSLevelsToGenerate.Count == 0)
            {
                problem = "Please choose one or more AFS levels to generate before writing TTC files";
            }

            if (problem != null)
            {
                var messageBox = new CustomMessageBox(problem, "AeroScenery", MessageBoxIcon.Warning);
                messageBox.ShowDialog();
                return;
            }

            var brighten = this.settings.TtcGeoConvertBrightness.Value;
            var source = new SourceTiles(tileDownloadDirectory, prefix, this.settings.ZoomLevel.Value,
                brighten ? ToneCurves.GeoConvert : null);
            var renderer = new TtcRenderer(source, ttcDirectory);
            var square = AfsTile.Parse(afs2GridSquare.Name);
            var levels = this.settings.AFSLevelsToGenerate.ToList();
            var progress = new Progress<TtcRendererProgress>(p =>
            {
                if (this.mainForm.ActionsRunning)
                {
                    this.mainForm.CurrentActionProgressPercentage = (int)(100L * p.BaseTilesDone / p.BaseTilesTotal);
                }
            });

            var cancellation = new CancellationTokenSource();
            this.ttcCancellation = cancellation;
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await Task.Run(() => renderer.Render(square, levels, progress, cancellation.Token));
                log.Info(String.Format("Wrote {0} TTC files for {1} to {2} in {3:F1} s (base level {4}{5})",
                    result.Written.Count, square, ttcDirectory, stopwatch.Elapsed.TotalSeconds, result.BaseLevel,
                    brighten ? ", brightened like GeoConvert" : ""));

                if (result.SkippedLevels.Count > 0)
                {
                    log.Info(String.Format("Levels {0} are coarser than the grid square {1}: not written",
                        String.Join(", ", result.SkippedLevels), square));
                }

                if (result.Incomplete.Count > 0)
                {
                    log.Warn(String.Format("TTC files not written for lack of image tiles: {0}; missing image tiles: {1}",
                        String.Join(" ", result.Incomplete), String.Join(" ", result.MissingSourceTiles)));

                    var messageBox = new CustomMessageBox(
                        String.Format("{0} TTC files of grid square {1} were not written because {2} image tiles are missing " +
                            "(the log lists them). Downloading the image tiles again fills the gaps.",
                            result.Incomplete.Count, square, result.MissingSourceTiles.Count),
                        "AeroScenery",
                        MessageBoxIcon.Warning);
                    messageBox.ShowDialog();
                }
            }
            catch (OperationCanceledException)
            {
                log.Info("Writing TTC files cancelled");
            }
            finally
            {
                if (this.ttcCancellation == cancellation)
                {
                    this.ttcCancellation = null;
                }
            }
        }

        /// <summary>
        /// Copies the TTC and TTH files Write TTC/TTH Files made for a grid square into the Aerofly user folder
        /// (scenery\images\&lt;level-9 square&gt;\, scenery\elevation\). Null if there is nowhere to install to (the
        /// user has been told).
        /// </summary>
        private async Task<InstallResult> InstallTilesAsync(AFS2GridSquare afs2GridSquare, string ttcDirectory, string tthDirectory)
        {
            var imagesDirectory = DirectoryHelper.FindAFSSceneryInstallDirectory(this.settings);
            if (imagesDirectory == null)
            {
                SceneryInstaller.ShowNoInstallDirectoryMessage();
                return null;
            }

            var tiles = new List<string>();
            if (Directory.Exists(ttcDirectory))
            {
                tiles.AddRange(Directory.GetFiles(ttcDirectory, "map_*.ttc"));
            }

            if (tthDirectory != null && Directory.Exists(tthDirectory))
            {
                tiles.AddRange(Directory.GetFiles(tthDirectory, "maph_*.tth"));
            }

            if (tiles.Count == 0)
            {
                log.Info(String.Format("No TTC or TTH files to install for {0}", afs2GridSquare.Name));
                return new InstallResult { ImagesDirectory = imagesDirectory };
            }

            var result = await Task.Run(() => SceneryInstaller.Install(imagesDirectory, tiles));
            log.Info(String.Format("Installed {0} TTC and {1} TTH files for {2} into {3}",
                result.Installed.Count(p => p.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase)),
                result.Installed.Count(p => p.EndsWith(".tth", StringComparison.OrdinalIgnoreCase)),
                afs2GridSquare.Name, Path.GetDirectoryName(imagesDirectory.TrimEnd('\\'))));
            foreach (var copy in result.OtherCopies)
            {
                log.Warn(String.Format("Another copy of an installed tile, which Aerofly also loads: {0}", copy));
            }

            return result;
        }

        /// <summary>
        /// The elevation sources from ElevationSources.json (written with the defaults if missing); null, with a
        /// message, if the file can't be read.
        /// </summary>
        private List<ElevationSource> LoadElevationSources()
        {
            try
            {
                return ElevationSources.Load(DirectoryHelper.ElevationSourcesPath);
            }
            catch (Exception ex)
            {
                log.Error("Error reading the elevation sources", ex);
                var messageBox = new CustomMessageBox(String.Format("Could not read the elevation sources ({0}):\n{1}",
                    DirectoryHelper.ElevationSourcesPath, ex.Message), "AeroScenery", MessageBoxIcon.Error);
                messageBox.ShowDialog();
                return null;
            }
        }

        // The source chosen in Settings, or the first in the file.
        private ElevationSource SelectedElevationSource(List<ElevationSource> sources)
        {
            return sources.FirstOrDefault(s => s.Name == this.settings.ElevationSource) ?? sources.FirstOrDefault();
        }

        private static string SafeFolderName(string name)
        {
            return String.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        }

        /// <summary>
        /// Writes the TTH (elevation) tiles of the selected TTH levels for a grid square (AFS4\TthRenderer.cs);
        /// elevation data is downloaded into &lt;working folder&gt;\elevation-data\.
        /// </summary>
        private async Task WriteTthFilesAsync(AFS2GridSquare afs2GridSquare, List<ElevationSource> sources, ElevationSource source, string tthDirectory)
        {
            var square = AfsTile.Parse(afs2GridSquare.Name);
            var levels = this.settings.TthLevelsToGenerate.ToList();
            var dem = new DemSet(sources, source, Path.Combine(this.settings.WorkingDirectory, "elevation-data"), this.settings.UserAgent);
            var renderer = new TthRenderer(dem, tthDirectory);
            var progress = new Progress<TthRendererProgress>(p =>
            {
                if (this.mainForm.ActionsRunning)
                {
                    if (p.Message != null)
                    {
                        this.mainForm.UpdateChildTaskLabel(p.Message);
                    }

                    this.mainForm.CurrentActionProgressPercentage = p.TilesTotal == 0 ? 0 : 100 * p.TilesDone / p.TilesTotal;
                }
            });

            var cancellation = new CancellationTokenSource();
            this.ttcCancellation = cancellation;
            try
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await renderer.RenderAsync(square, levels, progress, cancellation.Token);
                log.Info(String.Format("Wrote {0} TTH files for {1} (levels {2}, {3}) to {4} in {5:F1} s",
                    result.Written.Count, square, String.Join(",", levels), source.Name, tthDirectory, stopwatch.Elapsed.TotalSeconds));

                if (result.Incomplete.Count > 0)
                {
                    log.Warn(String.Format("TTH files not written for lack of elevation data: {0}", String.Join(" ", result.Incomplete)));
                    var messageBox = new CustomMessageBox(
                        String.Format("{0} TTH files of grid square {1} were not written because {2} has no elevation data for part of them " +
                            "(the log lists them). A source with a fallback, or one that covers the area, fills the gaps.",
                            result.Incomplete.Count, square, source.Name),
                        "AeroScenery",
                        MessageBoxIcon.Warning);
                    messageBox.ShowDialog();
                }
            }
            catch (OperationCanceledException)
            {
                log.Info("Writing TTH files cancelled");
            }
            catch (Exception ex) when (ex is System.Net.Http.HttpRequestException || ex is IOException || ex is InvalidDataException)
            {
                log.Error("Writing TTH files failed", ex);
                var messageBox = new CustomMessageBox(String.Format("Writing TTH files for {0} failed:\n{1}", square, ex.Message),
                    "AeroScenery", MessageBoxIcon.Error);
                messageBox.ShowDialog();
            }
            finally
            {
                if (this.ttcCancellation == cancellation)
                {
                    this.ttcCancellation = null;
                }
            }
        }

        private void ResetGridSquare(string gridSquareName)
        {
            var existingGridSquare = this.dataRepository.FindGridSquare(gridSquareName);

            if (existingGridSquare != null)
            {
                this.dataRepository.DeleteGridSquare(gridSquareName);
            }
        }


        private void DownloadThreadProgress_ProgressChanged(object sender, DownloadThreadProgress progress)
        {
            if (this.mainForm.ActionsRunning)
            {
                var progressControl = this.mainForm.GetDownloadThreadProgressControl(progress.DownloadThreadNumber);
                var percentageProgress = (int)Math.Floor(((double)progress.FilesDownloaded / (double)progress.TotalFiles) * 100);

                progressControl.SetProgressPercentage(percentageProgress);

                progressControl.SetImageTileCount(progress.FilesDownloaded, progress.TotalFiles);

                var downloadActionProgressPercentage = this.mainForm.CurrentActionProgressPercentage;

                if (percentageProgress > downloadActionProgressPercentage)
                {
                    this.mainForm.CurrentActionProgressPercentage = percentageProgress;
                }
            }

        }


        public bool AllImageTilesDownloaded(List<ImageTile> imageTiles)
        {
            return true;
        }

        public void SaveSettings()
        {
            this.settingsService.SaveSettings(this.settings);
        }

        public string ApplicationPath
        {
            get
            {
                var applicationUri = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
                var applicationLocalPath = new Uri(Path.GetDirectoryName(applicationUri)).LocalPath;
                return applicationLocalPath;

            }
        }


    }
}
