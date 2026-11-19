namespace AeroScenery.UI
{
    partial class SettingsForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SettingsForm));
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.afsUserFolderButton = new System.Windows.Forms.Button();
            this.afsUserFolderTextBox = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.aerosceneryDatabaseFolderButton = new System.Windows.Forms.Button();
            this.workingFolderButton = new System.Windows.Forms.Button();
            this.aeroSceneryDatabaseFolderTextBox = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.workingFolderTextBox = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label10 = new System.Windows.Forms.Label();
            this.label8 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.downloadWaitRandomTextBox = new System.Windows.Forms.TextBox();
            this.downloadWaitTextBox = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.simultaneousDownloadsComboBox = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.userAgentTextBox = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.closeButton = new System.Windows.Forms.Button();
            this.okButton = new System.Windows.Forms.Button();
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.tabPage5 = new System.Windows.Forms.TabPage();
            this.elevationSourceLabel = new System.Windows.Forms.Label();
            this.elevationSourceComboBox = new System.Windows.Forms.ComboBox();
            this.elevationSourceNotesLabel = new System.Windows.Forms.Label();
            this.tthLevelsLabel = new System.Windows.Forms.Label();
            this.tthLevelsCheckedListBox = new System.Windows.Forms.CheckedListBox();
            this.openElevationSourcesButton = new System.Windows.Forms.Button();
            this.reloadElevationSourcesButton = new System.Windows.Forms.Button();
            this.elevationSourcesPathLabel = new System.Windows.Forms.Label();
            this.ttcGeoConvertBrightnessCheckBox = new System.Windows.Forms.CheckBox();
            this.ttcSeaTransparentCheckBox = new System.Windows.Forms.CheckBox();
            this.ttcWaterStripLabel = new System.Windows.Forms.Label();
            this.ttcWaterStripTextBox = new System.Windows.Forms.TextBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage4.SuspendLayout();
            this.tabPage5.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.afsUserFolderButton);
            this.groupBox1.Controls.Add(this.afsUserFolderTextBox);
            this.groupBox1.Controls.Add(this.label9);
            this.groupBox1.Controls.Add(this.aerosceneryDatabaseFolderButton);
            this.groupBox1.Controls.Add(this.workingFolderButton);
            this.groupBox1.Controls.Add(this.aeroSceneryDatabaseFolderTextBox);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.workingFolderTextBox);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox1.Location = new System.Drawing.Point(14, 9);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(654, 135);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Folders";
            // 
            // afsUserFolderButton
            // 
            this.afsUserFolderButton.Location = new System.Drawing.Point(603, 91);
            this.afsUserFolderButton.Name = "afsUserFolderButton";
            this.afsUserFolderButton.Size = new System.Drawing.Size(33, 25);
            this.afsUserFolderButton.TabIndex = 11;
            this.afsUserFolderButton.Text = "...";
            this.afsUserFolderButton.UseVisualStyleBackColor = true;
            this.afsUserFolderButton.Click += new System.EventHandler(this.afsUserFolderButton_Click);
            // 
            // afsUserFolderTextBox
            // 
            this.afsUserFolderTextBox.Location = new System.Drawing.Point(216, 91);
            this.afsUserFolderTextBox.Name = "afsUserFolderTextBox";
            this.afsUserFolderTextBox.Size = new System.Drawing.Size(381, 25);
            this.afsUserFolderTextBox.TabIndex = 10;
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label9.Location = new System.Drawing.Point(20, 94);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(127, 17);
            this.label9.TabIndex = 9;
            this.label9.Text = "Aerofly User Folder";
            // 
            // aerosceneryDatabaseFolderButton
            // 
            this.aerosceneryDatabaseFolderButton.Location = new System.Drawing.Point(603, 60);
            this.aerosceneryDatabaseFolderButton.Name = "aerosceneryDatabaseFolderButton";
            this.aerosceneryDatabaseFolderButton.Size = new System.Drawing.Size(33, 25);
            this.aerosceneryDatabaseFolderButton.TabIndex = 7;
            this.aerosceneryDatabaseFolderButton.Text = "...";
            this.aerosceneryDatabaseFolderButton.UseVisualStyleBackColor = true;
            this.aerosceneryDatabaseFolderButton.Click += new System.EventHandler(this.aerosceneryDatabaseFolderButton_Click);
            // 
            // workingFolderButton
            // 
            this.workingFolderButton.Location = new System.Drawing.Point(603, 29);
            this.workingFolderButton.Name = "workingFolderButton";
            this.workingFolderButton.Size = new System.Drawing.Size(33, 25);
            this.workingFolderButton.TabIndex = 6;
            this.workingFolderButton.Text = "...";
            this.workingFolderButton.UseVisualStyleBackColor = true;
            this.workingFolderButton.Click += new System.EventHandler(this.workingFolderButton_Click);
            // 
            // aeroSceneryDatabaseFolderTextBox
            // 
            this.aeroSceneryDatabaseFolderTextBox.Location = new System.Drawing.Point(216, 60);
            this.aeroSceneryDatabaseFolderTextBox.Name = "aeroSceneryDatabaseFolderTextBox";
            this.aeroSceneryDatabaseFolderTextBox.Size = new System.Drawing.Size(381, 25);
            this.aeroSceneryDatabaseFolderTextBox.TabIndex = 3;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(20, 63);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(181, 17);
            this.label2.TabIndex = 2;
            this.label2.Text = "AeroScenery Database Folder";
            // 
            // workingFolderTextBox
            // 
            this.workingFolderTextBox.Location = new System.Drawing.Point(216, 29);
            this.workingFolderTextBox.Name = "workingFolderTextBox";
            this.workingFolderTextBox.Size = new System.Drawing.Size(381, 25);
            this.workingFolderTextBox.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(20, 32);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(97, 17);
            this.label1.TabIndex = 0;
            this.label1.Text = "Working Folder";
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Controls.Add(this.label10);
            this.groupBox2.Controls.Add(this.label8);
            this.groupBox2.Controls.Add(this.label7);
            this.groupBox2.Controls.Add(this.downloadWaitRandomTextBox);
            this.groupBox2.Controls.Add(this.downloadWaitTextBox);
            this.groupBox2.Controls.Add(this.label5);
            this.groupBox2.Controls.Add(this.simultaneousDownloadsComboBox);
            this.groupBox2.Controls.Add(this.label4);
            this.groupBox2.Controls.Add(this.userAgentTextBox);
            this.groupBox2.Controls.Add(this.label6);
            this.groupBox2.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox2.Location = new System.Drawing.Point(14, 154);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(654, 145);
            this.groupBox2.TabIndex = 9;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Downloads";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label10.Location = new System.Drawing.Point(350, 101);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(127, 17);
            this.label10.TabIndex = 9;
            this.label10.Text = "randomize by + or -";
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label8.Location = new System.Drawing.Point(574, 101);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(25, 17);
            this.label8.TabIndex = 8;
            this.label8.Text = "ms";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label7.Location = new System.Drawing.Point(301, 101);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(25, 17);
            this.label7.TabIndex = 7;
            this.label7.Text = "ms";
            // 
            // downloadWaitRandomTextBox
            // 
            this.downloadWaitRandomTextBox.Location = new System.Drawing.Point(489, 98);
            this.downloadWaitRandomTextBox.Name = "downloadWaitRandomTextBox";
            this.downloadWaitRandomTextBox.Size = new System.Drawing.Size(79, 25);
            this.downloadWaitRandomTextBox.TabIndex = 6;
            this.downloadWaitRandomTextBox.TextChanged += new System.EventHandler(this.downloadWaitRandomTextBox_TextChanged);
            // 
            // downloadWaitTextBox
            // 
            this.downloadWaitTextBox.Location = new System.Drawing.Point(216, 98);
            this.downloadWaitTextBox.Name = "downloadWaitTextBox";
            this.downloadWaitTextBox.Size = new System.Drawing.Size(79, 25);
            this.downloadWaitTextBox.TabIndex = 5;
            this.downloadWaitTextBox.TextChanged += new System.EventHandler(this.downloadWaitTextBox_TextChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(20, 101);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(154, 17);
            this.label5.TabIndex = 4;
            this.label5.Text = "Wait Between Downloads";
            // 
            // simultaneousDownloadsComboBox
            // 
            this.simultaneousDownloadsComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.simultaneousDownloadsComboBox.Enabled = false;
            this.simultaneousDownloadsComboBox.FormattingEnabled = true;
            this.simultaneousDownloadsComboBox.Items.AddRange(new object[] {
            "4",
            "6",
            "8"});
            this.simultaneousDownloadsComboBox.Location = new System.Drawing.Point(216, 63);
            this.simultaneousDownloadsComboBox.Name = "simultaneousDownloadsComboBox";
            this.simultaneousDownloadsComboBox.Size = new System.Drawing.Size(420, 25);
            this.simultaneousDownloadsComboBox.TabIndex = 3;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(20, 66);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(154, 17);
            this.label4.TabIndex = 2;
            this.label4.Text = "Simultaneous Downloads";
            // 
            // userAgentTextBox
            // 
            this.userAgentTextBox.Location = new System.Drawing.Point(216, 29);
            this.userAgentTextBox.Name = "userAgentTextBox";
            this.userAgentTextBox.Size = new System.Drawing.Size(420, 25);
            this.userAgentTextBox.TabIndex = 1;
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(20, 32);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(73, 17);
            this.label6.TabIndex = 0;
            this.label6.Text = "User Agent";
            // 
            // closeButton
            // 
            this.closeButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.closeButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.closeButton.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.closeButton.Location = new System.Drawing.Point(606, 489);
            this.closeButton.Name = "closeButton";
            this.closeButton.Size = new System.Drawing.Size(96, 32);
            this.closeButton.TabIndex = 10;
            this.closeButton.Text = "Cancel";
            this.closeButton.UseVisualStyleBackColor = true;
            this.closeButton.Click += new System.EventHandler(this.closeButton_Click);
            // 
            // okButton
            // 
            this.okButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.okButton.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.okButton.Location = new System.Drawing.Point(509, 489);
            this.okButton.Name = "okButton";
            this.okButton.Size = new System.Drawing.Size(91, 32);
            this.okButton.TabIndex = 11;
            this.okButton.Text = "OK";
            this.okButton.UseVisualStyleBackColor = true;
            this.okButton.Click += new System.EventHandler(this.okButton_Click);
            // 
            // folderBrowserDialog1
            // 
            this.folderBrowserDialog1.HelpRequest += new System.EventHandler(this.folderBrowserDialog1_HelpRequest);
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.Controls.Add(this.tabPage5);
            this.tabControl1.Font = new System.Drawing.Font("Segoe UI", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.tabControl1.Location = new System.Drawing.Point(12, 12);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(690, 464);
            this.tabControl1.TabIndex = 15;
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.groupBox1);
            this.tabPage1.Controls.Add(this.groupBox2);
            this.tabPage1.Location = new System.Drawing.Point(4, 26);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(682, 434);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "AeroScenery";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // tabPage4
            // 
            this.tabPage4.Controls.Add(this.ttcGeoConvertBrightnessCheckBox);
            this.tabPage4.Controls.Add(this.ttcSeaTransparentCheckBox);
            this.tabPage4.Controls.Add(this.ttcWaterStripLabel);
            this.tabPage4.Controls.Add(this.ttcWaterStripTextBox);
            this.tabPage4.Location = new System.Drawing.Point(4, 26);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage4.Size = new System.Drawing.Size(682, 434);
            this.tabPage4.TabIndex = 3;
            this.tabPage4.Text = "Image Processing";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // tabPage5
            // 
            this.tabPage5.Controls.Add(this.elevationSourcesPathLabel);
            this.tabPage5.Controls.Add(this.reloadElevationSourcesButton);
            this.tabPage5.Controls.Add(this.openElevationSourcesButton);
            this.tabPage5.Controls.Add(this.tthLevelsCheckedListBox);
            this.tabPage5.Controls.Add(this.tthLevelsLabel);
            this.tabPage5.Controls.Add(this.elevationSourceNotesLabel);
            this.tabPage5.Controls.Add(this.elevationSourceComboBox);
            this.tabPage5.Controls.Add(this.elevationSourceLabel);
            this.tabPage5.Location = new System.Drawing.Point(4, 26);
            this.tabPage5.Name = "tabPage5";
            this.tabPage5.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage5.Size = new System.Drawing.Size(682, 434);
            this.tabPage5.TabIndex = 4;
            this.tabPage5.Text = "Elevation";
            this.tabPage5.UseVisualStyleBackColor = true;
            // 
            // elevationSourceLabel
            // 
            this.elevationSourceLabel.AutoSize = true;
            this.elevationSourceLabel.Location = new System.Drawing.Point(13, 20);
            this.elevationSourceLabel.Name = "elevationSourceLabel";
            this.elevationSourceLabel.Size = new System.Drawing.Size(105, 17);
            this.elevationSourceLabel.TabIndex = 0;
            this.elevationSourceLabel.Text = "Elevation Source";
            // 
            // elevationSourceComboBox
            // 
            this.elevationSourceComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.elevationSourceComboBox.FormattingEnabled = true;
            this.elevationSourceComboBox.Location = new System.Drawing.Point(150, 17);
            this.elevationSourceComboBox.Name = "elevationSourceComboBox";
            this.elevationSourceComboBox.Size = new System.Drawing.Size(400, 25);
            this.elevationSourceComboBox.TabIndex = 1;
            this.elevationSourceComboBox.SelectedIndexChanged += new System.EventHandler(this.elevationSourceComboBox_SelectedIndexChanged);
            // 
            // elevationSourceNotesLabel
            // 
            this.elevationSourceNotesLabel.Location = new System.Drawing.Point(147, 50);
            this.elevationSourceNotesLabel.Name = "elevationSourceNotesLabel";
            this.elevationSourceNotesLabel.Size = new System.Drawing.Size(520, 90);
            this.elevationSourceNotesLabel.TabIndex = 2;
            // 
            // tthLevelsLabel
            // 
            this.tthLevelsLabel.AutoSize = true;
            this.tthLevelsLabel.Location = new System.Drawing.Point(13, 150);
            this.tthLevelsLabel.Name = "tthLevelsLabel";
            this.tthLevelsLabel.Size = new System.Drawing.Size(67, 17);
            this.tthLevelsLabel.TabIndex = 3;
            this.tthLevelsLabel.Text = "TTH Levels";
            // 
            // tthLevelsCheckedListBox
            // 
            this.tthLevelsCheckedListBox.CheckOnClick = true;
            this.tthLevelsCheckedListBox.FormattingEnabled = true;
            this.tthLevelsCheckedListBox.Location = new System.Drawing.Point(150, 150);
            this.tthLevelsCheckedListBox.Name = "tthLevelsCheckedListBox";
            this.tthLevelsCheckedListBox.Size = new System.Drawing.Size(260, 124);
            this.tthLevelsCheckedListBox.TabIndex = 4;
            // 
            // openElevationSourcesButton
            // 
            this.openElevationSourcesButton.Location = new System.Drawing.Point(150, 295);
            this.openElevationSourcesButton.Name = "openElevationSourcesButton";
            this.openElevationSourcesButton.Size = new System.Drawing.Size(160, 29);
            this.openElevationSourcesButton.TabIndex = 5;
            this.openElevationSourcesButton.Text = "Edit Sources File";
            this.openElevationSourcesButton.UseVisualStyleBackColor = true;
            this.openElevationSourcesButton.Click += new System.EventHandler(this.openElevationSourcesButton_Click);
            // 
            // reloadElevationSourcesButton
            // 
            this.reloadElevationSourcesButton.Location = new System.Drawing.Point(320, 295);
            this.reloadElevationSourcesButton.Name = "reloadElevationSourcesButton";
            this.reloadElevationSourcesButton.Size = new System.Drawing.Size(90, 29);
            this.reloadElevationSourcesButton.TabIndex = 6;
            this.reloadElevationSourcesButton.Text = "Reload";
            this.reloadElevationSourcesButton.UseVisualStyleBackColor = true;
            this.reloadElevationSourcesButton.Click += new System.EventHandler(this.reloadElevationSourcesButton_Click);
            // 
            // elevationSourcesPathLabel
            // 
            this.elevationSourcesPathLabel.AutoSize = true;
            this.elevationSourcesPathLabel.Location = new System.Drawing.Point(147, 330);
            this.elevationSourcesPathLabel.Name = "elevationSourcesPathLabel";
            this.elevationSourcesPathLabel.Size = new System.Drawing.Size(0, 17);
            this.elevationSourcesPathLabel.TabIndex = 7;
            //
            // ttcGeoConvertBrightnessCheckBox
            //
            this.ttcGeoConvertBrightnessCheckBox.AutoSize = true;
            this.ttcGeoConvertBrightnessCheckBox.Location = new System.Drawing.Point(13, 20);
            this.ttcGeoConvertBrightnessCheckBox.Name = "ttcGeoConvertBrightnessCheckBox";
            this.ttcGeoConvertBrightnessCheckBox.Size = new System.Drawing.Size(330, 21);
            this.ttcGeoConvertBrightnessCheckBox.TabIndex = 24;
            this.ttcGeoConvertBrightnessCheckBox.Text = "Write TTC Files: Brighten Images Like GeoConvert";
            this.ttcGeoConvertBrightnessCheckBox.UseVisualStyleBackColor = true;
            // 
            // ttcSeaTransparentCheckBox
            // 
            this.ttcSeaTransparentCheckBox.AutoSize = true;
            this.ttcSeaTransparentCheckBox.Location = new System.Drawing.Point(13, 47);
            this.ttcSeaTransparentCheckBox.Name = "ttcSeaTransparentCheckBox";
            this.ttcSeaTransparentCheckBox.Size = new System.Drawing.Size(560, 21);
            this.ttcSeaTransparentCheckBox.TabIndex = 25;
            this.ttcSeaTransparentCheckBox.Text = "Write TTC Files: Make the Sea Transparent (OSM Coastline; Aerofly's Own Water Shows)";
            this.ttcSeaTransparentCheckBox.UseVisualStyleBackColor = true;
            // 
            // ttcWaterStripLabel
            // 
            this.ttcWaterStripLabel.AutoSize = true;
            this.ttcWaterStripLabel.Location = new System.Drawing.Point(31, 77);
            this.ttcWaterStripLabel.Name = "ttcWaterStripLabel";
            this.ttcWaterStripLabel.Size = new System.Drawing.Size(330, 17);
            this.ttcWaterStripLabel.TabIndex = 26;
            this.ttcWaterStripLabel.Text = "Blend Into the Stock Water Over (m; Needs afsd -ts):";
            // 
            // ttcWaterStripTextBox
            // 
            this.ttcWaterStripTextBox.Location = new System.Drawing.Point(400, 74);
            this.ttcWaterStripTextBox.Name = "ttcWaterStripTextBox";
            this.ttcWaterStripTextBox.Size = new System.Drawing.Size(60, 22);
            this.ttcWaterStripTextBox.TabIndex = 27;
            this.ttcWaterStripTextBox.TextChanged += new System.EventHandler(this.ttcWaterStripTextBox_TextChanged);
            // 
            // SettingsForm
            // 
            this.AcceptButton = this.okButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.closeButton;
            this.ClientSize = new System.Drawing.Size(714, 533);
            this.Controls.Add(this.tabControl1);
            this.Controls.Add(this.okButton);
            this.Controls.Add(this.closeButton);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "SettingsForm";
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Settings";
            this.TopMost = true;
            this.Shown += new System.EventHandler(this.SettingsForm_Shown);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage4.ResumeLayout(false);
            this.tabPage4.PerformLayout();
            this.tabPage5.ResumeLayout(false);
            this.tabPage5.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox workingFolderTextBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button aerosceneryDatabaseFolderButton;
        private System.Windows.Forms.Button workingFolderButton;
        private System.Windows.Forms.TextBox aeroSceneryDatabaseFolderTextBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox downloadWaitRandomTextBox;
        private System.Windows.Forms.TextBox downloadWaitTextBox;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox simultaneousDownloadsComboBox;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox userAgentTextBox;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button closeButton;
        private System.Windows.Forms.Button okButton;
        private System.Windows.Forms.Button afsUserFolderButton;
        private System.Windows.Forms.TextBox afsUserFolderTextBox;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage4;
        private System.Windows.Forms.TabPage tabPage5;
        private System.Windows.Forms.Label elevationSourceLabel;
        private System.Windows.Forms.ComboBox elevationSourceComboBox;
        private System.Windows.Forms.Label elevationSourceNotesLabel;
        private System.Windows.Forms.Label tthLevelsLabel;
        private System.Windows.Forms.CheckedListBox tthLevelsCheckedListBox;
        private System.Windows.Forms.Button openElevationSourcesButton;
        private System.Windows.Forms.Button reloadElevationSourcesButton;
        private System.Windows.Forms.Label elevationSourcesPathLabel;
        private System.Windows.Forms.CheckBox ttcGeoConvertBrightnessCheckBox;
        private System.Windows.Forms.CheckBox ttcSeaTransparentCheckBox;
        private System.Windows.Forms.Label ttcWaterStripLabel;
        private System.Windows.Forms.TextBox ttcWaterStripTextBox;
    }
}