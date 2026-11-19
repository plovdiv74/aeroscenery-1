using AeroScenery.Common;
using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Globalization;

namespace AeroScenery.UI
{
    public partial class SettingsForm : Form
    {
        private readonly ILog log = LogManager.GetLogger("AeroScenery");

        public SettingsForm()
        {
            InitializeComponent();
        }

        // An entry of the TTH levels list.
        private class TthLevelItem
        {
            public int Level;
            public string Text;

            public override string ToString()
            {
                return this.Text;
            }
        }

        private void LoadElevationTab(Settings settings)
        {
            this.tthLevelsCheckedListBox.Items.Clear();
            foreach (var item in new[]
            {
                new TthLevelItem { Level = 7, Text = "Level 7" },
                new TthLevelItem { Level = 9, Text = "Level 9" },
                new TthLevelItem { Level = 10, Text = "Level 10 (finest stock level)" },
                // Not in the stock scenery, but the sim uses them (Mount Monadnock at KEEN, 2026-11-18).
                new TthLevelItem { Level = 11, Text = "Level 11 (best with 1/3\" data)" },
                new TthLevelItem { Level = 12, Text = "Level 12 (best with 1/3\" data)" },
            })
            {
                this.tthLevelsCheckedListBox.Items.Add(item, settings.TthLevelsToGenerate.Contains(item.Level));
            }

            this.LoadElevationSources(settings.ElevationSource);
        }

        private void LoadElevationSources(string selectedName)
        {
            this.elevationSourcesPathLabel.Text = DirectoryHelper.ElevationSourcesPath;
            this.elevationSourceComboBox.Items.Clear();
            try
            {
                var sources = AFS4.ElevationSources.Load(DirectoryHelper.ElevationSourcesPath);
                foreach (var source in sources)
                {
                    this.elevationSourceComboBox.Items.Add(source);
                }

                this.elevationSourceComboBox.DisplayMember = "Name";
                var selected = sources.FirstOrDefault(s => s.Name == selectedName) ?? sources.FirstOrDefault();
                this.elevationSourceComboBox.SelectedItem = selected;
            }
            catch (Exception ex)
            {
                this.elevationSourceNotesLabel.Text = "Could not read the elevation sources: " + ex.Message;
            }
        }

        private void elevationSourceComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            var source = this.elevationSourceComboBox.SelectedItem as AFS4.ElevationSource;
            if (source == null)
            {
                return;
            }

            var text = source.Notes ?? "";
            if (!String.IsNullOrEmpty(source.Fallback))
            {
                text += String.Format("\nWhere it has no data: {0}.", source.Fallback);
            }

            if (!String.IsNullOrEmpty(source.Attribution))
            {
                text += "\n" + source.Attribution;
            }

            this.elevationSourceNotesLabel.Text = text;
        }

        private void openElevationSourcesButton_Click(object sender, EventArgs e)
        {
            // Make sure the file exists (written with the defaults), then open it in the default editor for .json.
            this.LoadElevationSources((this.elevationSourceComboBox.SelectedItem as AFS4.ElevationSource)?.Name);
            if (File.Exists(DirectoryHelper.ElevationSourcesPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", "\"" + DirectoryHelper.ElevationSourcesPath + "\""));
            }
        }

        private void reloadElevationSourcesButton_Click(object sender, EventArgs e)
        {
            this.LoadElevationSources((this.elevationSourceComboBox.SelectedItem as AFS4.ElevationSource)?.Name);
        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            var settings = AeroSceneryManager.Instance.Settings;

            settings.WorkingDirectory = pathWithTrailingDirectorySeparatorChar(this.workingFolderTextBox.Text);
            settings.AeroSceneryDBDirectory = pathWithTrailingDirectorySeparatorChar(this.aeroSceneryDatabaseFolderTextBox.Text);
            settings.AFS2UserDirectory = pathWithTrailingDirectorySeparatorChar(this.afsUserFolderTextBox.Text);


            settings.UserAgent = this.userAgentTextBox.Text;

            // Elevation
            var elevationSource = this.elevationSourceComboBox.SelectedItem as AFS4.ElevationSource;
            if (elevationSource != null)
            {
                settings.ElevationSource = elevationSource.Name;
            }

            var tthLevels = this.tthLevelsCheckedListBox.CheckedItems.Cast<TthLevelItem>().Select(l => l.Level).ToList();
            settings.TthLevelsToGenerate = tthLevels.Count > 0 ? tthLevels : new List<int> { 10 };

            if (!String.IsNullOrEmpty(this.downloadWaitTextBox.Text))
            {
                settings.DownloadWaitMs = int.Parse(this.downloadWaitTextBox.Text);
            }

            if (!String.IsNullOrEmpty(this.downloadWaitRandomTextBox.Text))
            {
                settings.DownloadWaitRandomMs = int.Parse(this.downloadWaitRandomTextBox.Text);
            }

            switch (this.simultaneousDownloadsComboBox.SelectedIndex)
            {
                case 0:
                    settings.SimultaneousDownloads = 4;
                    break;
                case 1:
                    settings.SimultaneousDownloads = 6;
                    break;
                case 2:
                    settings.SimultaneousDownloads = 8;
                    break;
            }

            // Image processing
            settings.TtcGeoConvertBrightness = this.ttcGeoConvertBrightnessCheckBox.Checked;
            settings.TtcSeaTransparent = this.ttcSeaTransparentCheckBox.Checked;
            settings.TtcWaterStripMetres = String.IsNullOrEmpty(this.ttcWaterStripTextBox.Text) ? 0 : int.Parse(this.ttcWaterStripTextBox.Text);

            AeroSceneryManager.Instance.SaveSettings();
            this.Hide();
            log.Info("Settings saved");
        }

        private void SettingsForm_Shown(object sender, EventArgs e)
        {
            var settings = AeroSceneryManager.Instance.Settings;

            this.workingFolderTextBox.Text = settings.WorkingDirectory;
            this.aeroSceneryDatabaseFolderTextBox.Text = settings.AeroSceneryDBDirectory;
            this.afsUserFolderTextBox.Text = settings.AFS2UserDirectory;

            this.userAgentTextBox.Text = settings.UserAgent;

            this.LoadElevationTab(settings);
            this.downloadWaitTextBox.Text = settings.DownloadWaitMs.ToString();
            this.downloadWaitRandomTextBox.Text = settings.DownloadWaitRandomMs.ToString();

            switch (settings.SimultaneousDownloads)
            {
                case 4:
                    this.simultaneousDownloadsComboBox.SelectedIndex = 0;
                    break;
                case 6:
                    this.simultaneousDownloadsComboBox.SelectedIndex = 1;
                    break;
                case 8:
                    this.simultaneousDownloadsComboBox.SelectedIndex = 2;
                    break;
            }

            // Image processing
            this.ttcGeoConvertBrightnessCheckBox.Checked = settings.TtcGeoConvertBrightness.Value;
            this.ttcSeaTransparentCheckBox.Checked = settings.TtcSeaTransparent.Value;
            this.ttcWaterStripTextBox.Text = settings.TtcWaterStripMetres.Value.ToString();
        }

        private void folderBrowserDialog1_HelpRequest(object sender, EventArgs e)
        {

        }

        private void workingFolderButton_Click(object sender, EventArgs e)
        {
            var settings = AeroSceneryManager.Instance.Settings;
            DialogResult result = this.folderBrowserDialog1.ShowDialog();

            if (result == DialogResult.OK)
            {
                this.workingFolderTextBox.Text = folderBrowserDialog1.SelectedPath;
                settings.WorkingDirectory = this.workingFolderTextBox.Text;
            }
        }

        private void aerosceneryDatabaseFolderButton_Click(object sender, EventArgs e)
        {
            var settings = AeroSceneryManager.Instance.Settings;
            DialogResult result = this.folderBrowserDialog1.ShowDialog();

            if (result == DialogResult.OK)
            {
                this.aeroSceneryDatabaseFolderTextBox.Text = folderBrowserDialog1.SelectedPath;
                settings.AeroSceneryDBDirectory = this.aeroSceneryDatabaseFolderTextBox.Text;
            }
        }

        private void afsUserFolderButton_Click(object sender, EventArgs e)
        {
            var settings = AeroSceneryManager.Instance.Settings;
            DialogResult result = this.folderBrowserDialog1.ShowDialog();

            if (result == DialogResult.OK)
            {
                this.afsUserFolderTextBox.Text = folderBrowserDialog1.SelectedPath;
                settings.AFS2UserDirectory = this.afsUserFolderTextBox.Text;
            }
        }

        private string pathWithTrailingDirectorySeparatorChar(string path)
        {
            if (!String.IsNullOrEmpty(path))
            {
                // They're always one character but EndsWith is shorter than
                // array style access to last path character. Change this
                // if performance are a (measured) issue.
                string separator1 = Path.DirectorySeparatorChar.ToString();
                string separator2 = Path.AltDirectorySeparatorChar.ToString();

                // Trailing white spaces are always ignored but folders may have
                // leading spaces. It's unusual but it may happen. If it's an issue
                // then just replace TrimEnd() with Trim(). Tnx Paul Groke to point this out.
                path = path.TrimEnd();

                // Argument is always a directory name then if there is one
                // of allowed separators then I have nothing to do.
                if (path.EndsWith(separator1) || path.EndsWith(separator2))
                    return path;

                // If there is the "alt" separator then I add a trailing one.
                // Note that URI format (file://drive:\path\filename.ext) is
                // not supported in most .NET I/O functions then we don't support it
                // here too. If you have to then simply revert this check:
                // if (path.Contains(separator1))
                //     return path + separator1;
                //
                // return path + separator2;
                if (path.Contains(separator2))
                    return path + separator2;

                // If there is not an "alt" separator I add a "normal" one.
                // It means path may be with normal one or it has not any separator
                // (for example if it's just a directory name). In this case I
                // default to normal as users expect.
                return path + separator1;
            }

            return path;

        }

        private string GetInteger(string input)
        {
            return new string(input.Where(c => char.IsDigit(c)).ToArray());
        }

        private void ttcWaterStripTextBox_TextChanged(object sender, EventArgs e)
        {
            var numbersOnly = this.GetInteger(this.ttcWaterStripTextBox.Text);
            if (this.ttcWaterStripTextBox.Text != numbersOnly)
            {
                this.ttcWaterStripTextBox.Text = numbersOnly;
            }
        }

        private void downloadWaitTextBox_TextChanged(object sender, EventArgs e)
        {
            var numbersOnly = this.GetInteger(this.downloadWaitTextBox.Text);

            if (this.downloadWaitTextBox.Text != numbersOnly)
            {
                this.downloadWaitTextBox.Text = numbersOnly;
            }
        }

        private void downloadWaitRandomTextBox_TextChanged(object sender, EventArgs e)
        {
            var numbersOnly = this.GetInteger(this.downloadWaitRandomTextBox.Text);

            if (this.downloadWaitRandomTextBox.Text != numbersOnly)
            {
                this.downloadWaitRandomTextBox.Text = numbersOnly;
            }
        }

        private void AddUserFolderToConfigButton_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {

        }



    }
}
