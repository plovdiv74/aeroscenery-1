using AeroScenery.AFS2;
using AeroScenery.AFS4;
using AeroScenery.Common;
using AeroScenery.Controls;
using AeroScenery.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AeroScenery.FileManagement
{
    /// <summary>
    /// The outcome of an install: where the tiles went, and copies of the same tiles elsewhere under scenery\images,
    /// which the sim would also load.
    /// </summary>
    public class InstallResult
    {
        public string ImagesDirectory { get; set; }
        public List<string> Installed { get; } = new List<string>();
        public List<string> OtherCopies { get; } = new List<string>();
    }

    public class SceneryInstaller
    {
        public DialogResult ConfirmSceneryInstallation(AFS2GridSquare afs2GridSquare)
        {
            var gridSquareDirectory = AeroSceneryManager.Instance.Settings.WorkingDirectory + afs2GridSquare.Name;

            DialogResult result = DialogResult.No;

            // Does this grid square exist
            if (Directory.Exists(gridSquareDirectory))
            {
                // Do we have an Aerofly folder to install into?
                string afsSceneryInstallDirectory = DirectoryHelper.FindAFSSceneryInstallDirectory(AeroSceneryManager.Instance.Settings);

                if (afsSceneryInstallDirectory != null)
                {
                    // Confirm that the user does want to install scenery
                    StringBuilder sb = new StringBuilder();

                    sb.AppendLine("Are you sure you want to install all scenery for this grid square?");
                    sb.AppendLine("Any existing files in the same destination folder will be overwritten.");
                    sb.AppendLine("");
                    sb.AppendLine(String.Format("Destination: {0}", afsSceneryInstallDirectory));

                    var messageBox = new CustomMessageBox(sb.ToString(),
                        "AeroScenery",
                        MessageBoxIcon.Question);

                    messageBox.SetButtons(
                        new string[] { "Yes", "No" },
                        new DialogResult[] { DialogResult.Yes, DialogResult.No });

                    result = messageBox.ShowDialog();
                }
                else
                {
                    ShowNoInstallDirectoryMessage();
                }
            }

            return result;
        }

        public static void ShowNoInstallDirectoryMessage()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("Could not find a location to install to.");
            sb.AppendLine("Set the Aerofly user folder in Settings, or check that your 'My Documents\\Aerofly FS 4' folder exists.");

            var messageBox = new CustomMessageBox(sb.ToString(),
                "AeroScenery",
                MessageBoxIcon.Error);

            messageBox.ShowDialog();
        }

        public static void ShowOtherCopiesMessage(IEnumerable<InstallResult> results)
        {
            var otherCopies = results.SelectMany(r => r.OtherCopies).Distinct().ToList();
            if (otherCopies.Count == 0)
            {
                return;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(String.Format("{0} of the installed tiles also exist in other folders under scenery\\images or scenery\\elevation, " +
                "and Aerofly will load those copies too. AeroScenery hasn't removed them:", otherCopies.Count));
            sb.AppendLine("");
            foreach (var folder in otherCopies.Select(Path.GetDirectoryName).Distinct().Take(10))
            {
                sb.AppendLine(String.Format("{0} ({1} files)", folder, otherCopies.Count(c => Path.GetDirectoryName(c) == folder)));
            }

            var messageBox = new CustomMessageBox(sb.ToString(), "AeroScenery", MessageBoxIcon.Warning);
            messageBox.ShowDialog();
        }

        /// <summary>
        /// The tiles Write TTC/TTH Files made for a grid square: map_*.ttc in its &lt;source&gt;\&lt;zoom&gt;-ttc folders
        /// and maph_*.tth in its elevation\&lt;source&gt; folders (not GeoConvert's output from older versions). Asks
        /// whether to go on if the same tile exists more than once (several image or elevation sources).
        /// </summary>
        public DialogResult? CheckForDuplicateTiles(AFS2GridSquare afs2GridSquare, out List<string> tileFiles)
        {
            // A null dialog result means that there are no duplicates
            DialogResult? result = null;

            var gridSquareDirectory = AeroSceneryManager.Instance.Settings.WorkingDirectory + afs2GridSquare.Name;

            tileFiles = new List<string>();
            foreach (var sourceDirectory in Directory.Exists(gridSquareDirectory) ? Directory.GetDirectories(gridSquareDirectory) : new string[0])
            {
                foreach (var ttcDirectory in Directory.GetDirectories(sourceDirectory, "*-ttc"))
                {
                    if (!Path.GetFileName(ttcDirectory).EndsWith("-geoconvert-ttc", StringComparison.OrdinalIgnoreCase))
                    {
                        tileFiles.AddRange(Directory.GetFiles(ttcDirectory, "map_*.ttc"));
                    }
                }
            }

            var elevationDirectory = Path.Combine(gridSquareDirectory, "elevation");
            if (Directory.Exists(elevationDirectory))
            {
                foreach (var sourceDirectory in Directory.GetDirectories(elevationDirectory))
                {
                    tileFiles.AddRange(Directory.GetFiles(sourceDirectory, "maph_*.tth"));
                }
            }

            var tileFileNames = tileFiles.Select(Path.GetFileName).ToList();

            // Check for duplicate tiles
            if (tileFileNames.Count != tileFileNames.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            {
                StringBuilder sbDuplicates = new StringBuilder();

                sbDuplicates.AppendLine(String.Format("Duplicate tiles were found in the folder for grid square ({0})", afs2GridSquare.Name));
                sbDuplicates.AppendLine("This may be because you have written this grid square from more than one image or elevation source,");
                sbDuplicates.AppendLine("or at more than one zoom level.");
                sbDuplicates.AppendLine("If you continue with the install you may get a mismatched set of tiles.");

                var duplicatesMessageBox = new CustomMessageBox(sbDuplicates.ToString(),
                    "AeroScenery",
                    MessageBoxIcon.Warning);

                duplicatesMessageBox.SetButtons(
                    new string[] { "Continue", "Cancel" },
                    new DialogResult[] { DialogResult.OK, DialogResult.Cancel });

                result = duplicatesMessageBox.ShowDialog();
            }

            return result;
        }

        public async Task<InstallResult> InstallSceneryAsync(List<string> ttcFiles)
        {
            var imagesDirectory = DirectoryHelper.FindAFSSceneryInstallDirectory(AeroSceneryManager.Instance.Settings);
            if (imagesDirectory == null)
            {
                return null;
            }

            return await Task.Run(() => Install(imagesDirectory, ttcFiles));
        }

        /// <summary>
        /// The folder under scenery\images a tile goes in: that of the level-9 tile containing it (as Aerofly's own
        /// scenery is laid out), or, for a tile coarser than level 9, one named after the tile.
        /// </summary>
        public static string InstallFolderName(string ttcPath)
        {
            var tile = AfsTile.Parse(ttcPath);
            if (tile.Level < 9)
            {
                return tile.Name();
            }

            const int Level9Span = 0x10000 >> 9;
            return new AfsTile(9, tile.X - tile.X % Level9Span, tile.Y - tile.Y % Level9Span).Name();
        }

        /// <summary>
        /// scenery\elevation next to scenery\images.
        /// </summary>
        public static string ElevationDirectoryFor(string imagesDirectory)
        {
            return Path.Combine(Path.GetDirectoryName(imagesDirectory.TrimEnd('\\', '/')), "elevation");
        }

        /// <summary>
        /// Copies tiles into the user's scenery folder, overwriting tiles of the same name there: ttc files into
        /// imagesDirectory\&lt;level-9 folder&gt;\, tth files flat into scenery\elevation\ (as Aerofly's own). Lists
        /// other copies of the same tiles elsewhere under scenery\images or scenery\elevation (they are not touched).
        /// </summary>
        public static InstallResult Install(string imagesDirectory, IEnumerable<string> tileFiles)
        {
            var result = new InstallResult { ImagesDirectory = imagesDirectory };
            var elevationDirectory = ElevationDirectoryFor(imagesDirectory);
            var targets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // file name -> destination

            foreach (var tileFile in tileFiles)
            {
                bool elevation = String.Equals(Path.GetExtension(tileFile), ".tth", StringComparison.OrdinalIgnoreCase);
                var folder = elevation ? elevationDirectory : Path.Combine(imagesDirectory, InstallFolderName(tileFile));
                Directory.CreateDirectory(folder);

                var destination = Path.Combine(folder, Path.GetFileName(tileFile));

                // Overwrite, so that users can install updated files again
                File.Copy(tileFile, destination, true);
                result.Installed.Add(destination);
                targets[Path.GetFileName(tileFile)] = destination;
            }

            var existingTiles = EnumerateFilesRecursive(imagesDirectory, "*.ttc")
                .Concat(Directory.Exists(elevationDirectory) ? EnumerateFilesRecursive(elevationDirectory, "*.tth") : Enumerable.Empty<string>());
            foreach (var existing in existingTiles)
            {
                string destination;
                if (targets.TryGetValue(Path.GetFileName(existing), out destination) &&
                    !String.Equals(Path.GetFullPath(existing), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                {
                    result.OtherCopies.Add(existing);
                }
            }

            return result;
        }

        /// <summary>
        /// Recursively enumerates files. Silently fails if it doesn't have access to any files.
        /// </summary>
        /// <param name="root"></param>
        /// <param name="pattern"></param>
        /// <returns></returns>
        private static IEnumerable<string> EnumerateFilesRecursive(string root, string pattern = "*")
        {
            var todo = new Queue<string>();
            todo.Enqueue(root);
            while (todo.Count > 0)
            {
                string dir = todo.Dequeue();
                string[] subdirs = new string[0];
                string[] files = new string[0];
                try
                {
                    subdirs = Directory.GetDirectories(dir);
                    files = Directory.GetFiles(dir, pattern);
                }
                catch (IOException)
                {
                }
                catch (System.UnauthorizedAccessException)
                {
                }

                foreach (string subdir in subdirs)
                {
                    todo.Enqueue(subdir);
                }
                foreach (string filename in files)
                {
                    yield return filename;
                }
            }
        }


    }
}
