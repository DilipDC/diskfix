using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DriveMedic.Core;
using DriveMedic.Models;

namespace DriveMedic
{
    public sealed class MainForm : Form
    {
        private readonly DriveInventoryService _inventory = new DriveInventoryService();
        private readonly RecoveryService _recovery = new RecoveryService();
        private readonly BindingList<DriveRecord> _drives = new BindingList<DriveRecord>();
        private readonly BindingList<PhysicalDiskRecord> _physicalDisks = new BindingList<PhysicalDiskRecord>();
        private readonly List<DiagnosticFinding> _findings = new List<DiagnosticFinding>();
        private readonly List<string> _activity = new List<string>();

        private TabControl _tabs;
        private DataGridView _driveGrid;
        private DataGridView _physicalGrid;
        private ComboBox _browseDrive;
        private ComboBox _diagnosticDrive;
        private ComboBox _commonFolder;
        private TextBox _pathBox;
        private ListView _fileList;
        private ListView _findingList;
        private TextBox _activityBox;
        private Label _volumeSummary;
        private Label _healthSummary;
        private Label _osSummary;
        private Label _adminStatus;
        private Button _refreshButton;
        private Button _scanButton;
        private Button _copyButton;
        private Button _fixButton;
        private Button _checkButton;
        private Button _repairButton;
        private bool _refreshing;

        private static readonly Color Navy = Color.FromArgb(20, 40, 70);
        private static readonly Color Blue = Color.FromArgb(35, 105, 180);
        private static readonly Color Canvas = Color.FromArgb(244, 247, 251);
        private static readonly Color Border = Color.FromArgb(218, 226, 236);
        private static readonly Color TextDark = Color.FromArgb(35, 47, 62);
        private static readonly Color Muted = Color.FromArgb(103, 117, 135);
        private static readonly Font StrongFont = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);

        public MainForm()
        {
            Text = "DriveMedic — Drive Diagnostics & Recovery";
            MinimumSize = new Size(1040, 680);
            Size = new Size(1280, 820);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Canvas;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BuildInterface();
            Load += async delegate { await RefreshAllAsync(); };
        }

        private void BuildInterface()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.RowCount = 2;
            root.ColumnCount = 1;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.BackColor = Canvas;
            Controls.Add(root);

            Panel header = new Panel { Dock = DockStyle.Fill, BackColor = Navy, Padding = new Padding(22, 10, 18, 8) };
            root.Controls.Add(header, 0, 0);
            Label title = new Label { Text = "DriveMedic", ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 19F, FontStyle.Bold), AutoSize = true, Location = new Point(20, 8) };
            Label subtitle = new Label { Text = "Inspect first. Recover safely.", ForeColor = Color.FromArgb(190, 209, 232), AutoSize = true, Location = new Point(22, 43) };
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            bool elevated = IsAdministrator();
            _adminStatus = new Label { Text = elevated ? "ADMINISTRATOR" : "STANDARD USER", ForeColor = Color.White, BackColor = elevated ? Color.FromArgb(28, 125, 93) : Color.FromArgb(91, 105, 122), Padding = new Padding(9, 5, 9, 5), AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(820, 12) };
            header.Controls.Add(_adminStatus);
            _refreshButton = MakeButton("Refresh drives", Blue, Color.White);
            _refreshButton.Size = new Size(130, 35);
            _refreshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _refreshButton.Location = new Point(Width - 310, 11);
            _refreshButton.Click += async delegate { await RefreshAllAsync(); };
            header.Controls.Add(_refreshButton);
            Button adminButton = MakeButton(elevated ? "Already elevated" : "Restart as admin", Color.FromArgb(61, 77, 98), Color.White);
            adminButton.Enabled = !elevated;
            adminButton.Size = new Size(140, 35);
            adminButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            adminButton.Location = new Point(Width - 165, 11);
            adminButton.Click += delegate { RestartAsAdministrator(); };
            header.Controls.Add(adminButton);
            header.Resize += delegate
            {
                adminButton.Location = new Point(header.ClientSize.Width - adminButton.Width - 2, 11);
                _refreshButton.Location = new Point(adminButton.Left - _refreshButton.Width - 8, 11);
                _adminStatus.Location = new Point(Math.Max(500, _refreshButton.Left - _adminStatus.Width - 12), 15);
            };

            _tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(16, 7), Font = new Font("Segoe UI", 9.5F) };
            _tabs.TabPages.Add(BuildDashboardTab());
            _tabs.TabPages.Add(BuildPhysicalDevicesTab());
            _tabs.TabPages.Add(BuildBrowseTab());
            _tabs.TabPages.Add(BuildDiagnosticsTab());
            _tabs.TabPages.Add(BuildActivityTab());
            root.Controls.Add(_tabs, 0, 1);
        }

        private TabPage BuildDashboardTab()
        {
            TabPage page = MakeTab("Overview");
            FlowLayoutPanel cards = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 112, Padding = new Padding(2, 10, 2, 8), WrapContents = false, BackColor = Canvas };
            _volumeSummary = new Label();
            _healthSummary = new Label();
            _osSummary = new Label();
            cards.Controls.Add(MakeSummaryCard("Detected volumes", _volumeSummary, 230));
            cards.Controls.Add(MakeSummaryCard("Hardware warnings", _healthSummary, 230));
            cards.Controls.Add(MakeSummaryCard("Current Windows", _osSummary, 330));
            page.Controls.Add(cards);

            Label label = new Label { Text = "Storage inventory", ForeColor = TextDark, Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold), Dock = DockStyle.Top, Height = 32, Padding = new Padding(3, 3, 0, 0) };
            page.Controls.Add(label);
            _driveGrid = CreateGrid();
            _driveGrid.Columns.Add(CreateTextColumn("RootPath", "Volume", 75));
            _driveGrid.Columns.Add(CreateTextColumn("Label", "Label", 120));
            _driveGrid.Columns.Add(CreateTextColumn("Role", "Role", 150));
            _driveGrid.Columns.Add(CreateTextColumn("DriveType", "Type", 75));
            _driveGrid.Columns.Add(CreateTextColumn("FileSystem", "File system", 85));
            _driveGrid.Columns.Add(CreateTextColumn("Health", "Health", 85));
            _driveGrid.Columns.Add(CreateTextColumn("CapacityText", "Capacity", 160));
            _driveGrid.Columns.Add(CreateTextColumn("PhysicalDisk", "Physical device", 220));
            _driveGrid.Columns.Add(CreateTextColumn("HealthDetails", "Health details", 330));
            _driveGrid.Dock = DockStyle.Fill;
            _driveGrid.CellFormatting += delegate(object sender, DataGridViewCellFormattingEventArgs e)
            {
                if (e.RowIndex >= 0 && e.RowIndex < _driveGrid.Rows.Count)
                {
                    DriveRecord record = _driveGrid.Rows[e.RowIndex].DataBoundItem as DriveRecord;
                    if (record != null && record.IsWindowsVolume)
                    {
                        e.CellStyle.BackColor = Color.FromArgb(226, 239, 253);
                        e.CellStyle.Font = StrongFont;
                    }
                    else if (record != null && record.Health == "Warning") e.CellStyle.ForeColor = Color.FromArgb(164, 55, 34);
                }
            };
            page.Controls.Add(_driveGrid);

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(2, 8, 0, 0), WrapContents = false, BackColor = Canvas };
            Button browse = MakeButton("Browse selected volume", Blue, Color.White);
            browse.Click += delegate { BrowseSelectedDrive(); };
            Button refresh = MakeButton("Refresh inventory", Color.White, TextDark);
            refresh.Click += async delegate { await RefreshAllAsync(); };
            actions.Controls.Add(browse);
            actions.Controls.Add(refresh);
            page.Controls.Add(actions);
            return page;
        }

        private TabPage BuildPhysicalDevicesTab()
        {
            TabPage page = MakeTab("Physical devices");
            Label intro = new Label { Text = "Physical-drive inventory — includes disks without a mounted drive letter when Windows exposes them. SMART data is driver-dependent; ‘No warning’ is not a guarantee of health.", Dock = DockStyle.Top, Height = 48, ForeColor = Muted, Padding = new Padding(8, 8, 8, 4) };
            page.Controls.Add(intro);
            _physicalGrid = CreateGrid();
            _physicalGrid.Columns.Add(CreateTextColumn("Index", "Disk #", 55));
            _physicalGrid.Columns.Add(CreateTextColumn("Model", "Model", 210));
            _physicalGrid.Columns.Add(CreateTextColumn("DeviceId", "Windows device ID", 190));
            _physicalGrid.Columns.Add(CreateTextColumn("SizeText", "Capacity", 100));
            _physicalGrid.Columns.Add(CreateTextColumn("WindowsStatus", "Windows status", 100));
            _physicalGrid.Columns.Add(CreateTextColumn("Health", "Health", 90));
            _physicalGrid.Columns.Add(CreateTextColumn("Volumes", "Mapped volumes", 145));
            _physicalGrid.Columns.Add(CreateTextColumn("Details", "Details", 340));
            _physicalGrid.Dock = DockStyle.Fill;
            page.Controls.Add(_physicalGrid);
            Button refresh = MakeButton("Refresh physical inventory", Blue, Color.White);
            refresh.Dock = DockStyle.Bottom;
            refresh.Height = 40;
            refresh.Click += async delegate { await RefreshAllAsync(); };
            page.Controls.Add(refresh);
            return page;
        }

        private TabPage BuildBrowseTab()
        {
            TabPage page = MakeTab("Browse & Recover");
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Canvas, Padding = new Padding(8) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            page.Controls.Add(layout);

            FlowLayoutPanel top = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
            top.Controls.Add(new Label { Text = "Drive", AutoSize = true, ForeColor = Muted, Padding = new Padding(0, 8, 2, 0) });
            _browseDrive = new ComboBox { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList, Font = Font };
            top.Controls.Add(_browseDrive);
            Button openRoot = MakeButton("Open volume", Blue, Color.White);
            openRoot.Click += delegate { DriveRecord drive = SelectedDrive(_browseDrive); if (drive != null) NavigateTo(drive.RootPath); };
            top.Controls.Add(openRoot);
            Button findUsers = MakeButton("Find user profiles", Color.White, TextDark);
            findUsers.Click += delegate { FindUserProfiles(); };
            top.Controls.Add(findUsers);
            layout.Controls.Add(top, 0, 0);

            FlowLayoutPanel pathTools = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 2, 0, 0) };
            Button up = MakeButton("Up", Color.White, TextDark);
            up.Width = 58;
            up.Click += delegate { NavigateUp(); };
            pathTools.Controls.Add(up);
            _pathBox = new TextBox { Width = 320, Font = Font, Margin = new Padding(4, 7, 4, 3) };
            pathTools.Controls.Add(_pathBox);
            Button openPath = MakeButton("Open path", Color.White, TextDark);
            openPath.Click += delegate { NavigateTo(_pathBox.Text.Trim()); };
            pathTools.Controls.Add(openPath);
            _commonFolder = new ComboBox { Width = 125, DropDownStyle = ComboBoxStyle.DropDownList, Font = Font, Margin = new Padding(4, 6, 4, 3) };
            _commonFolder.Items.AddRange(new object[] { "Desktop", "Documents", "Downloads", "Pictures", "Music", "Videos" });
            _commonFolder.SelectedIndex = 0;
            pathTools.Controls.Add(_commonFolder);
            Button commonFolderButton = MakeButton("Open common folder", Color.White, TextDark);
            commonFolderButton.Width = 132;
            commonFolderButton.Click += delegate { OpenCommonFolder(); };
            pathTools.Controls.Add(commonFolderButton);
            _copyButton = MakeButton("Copy selected to…", Blue, Color.White);
            _copyButton.Click += async delegate { await CopySelectedAsync(); };
            pathTools.Controls.Add(_copyButton);
            layout.Controls.Add(pathTools, 0, 1);

            _fileList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, HideSelection = false, BackColor = Color.White, ForeColor = TextDark, BorderStyle = BorderStyle.FixedSingle };
            _fileList.Columns.Add("Name", 360);
            _fileList.Columns.Add("Type", 105);
            _fileList.Columns.Add("Size", 105);
            _fileList.Columns.Add("Last modified", 160);
            _fileList.Columns.Add("Access", 140);
            _fileList.DoubleClick += delegate
            {
                if (_fileList.SelectedItems.Count == 0) return;
                FileEntry entry = _fileList.SelectedItems[0].Tag as FileEntry;
                if (entry != null && entry.IsDirectory) NavigateTo(entry.FullPath);
            };
            layout.Controls.Add(_fileList, 0, 2);
            Label note = new Label { Text = "Recovery mode is copy-only: source files are never moved or overwritten. Encrypted EFS/BitLocker data still requires its key.", Dock = DockStyle.Bottom, Height = 26, ForeColor = Muted, Padding = new Padding(3, 6, 0, 0) };
            page.Controls.Add(note);
            return page;
        }

        private TabPage BuildDiagnosticsTab()
        {
            TabPage page = MakeTab("Diagnostics & Fixes");
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Canvas, Padding = new Padding(8) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            page.Controls.Add(layout);

            FlowLayoutPanel commands = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(0, 4, 0, 0) };
            commands.Controls.Add(new Label { Text = "Target volume", AutoSize = true, ForeColor = Muted, Padding = new Padding(0, 7, 1, 0) });
            _diagnosticDrive = new ComboBox { Width = 260, DropDownStyle = ComboBoxStyle.DropDownList, Font = Font };
            commands.Controls.Add(_diagnosticDrive);
            _scanButton = MakeButton("Scan readable files", Blue, Color.White);
            _scanButton.Click += async delegate { await RunReadabilityScanAsync(); };
            commands.Controls.Add(_scanButton);
            _checkButton = MakeButton("Check filesystem (read-only)", Color.White, TextDark);
            _checkButton.Click += async delegate { await RunChkdskReadOnlyAsync(); };
            commands.Controls.Add(_checkButton);
            Button verify = MakeButton("Verify current Windows files", Color.White, TextDark);
            verify.Click += delegate { RunSfc(false); };
            commands.Controls.Add(verify);
            Button repairWindows = MakeButton("Repair current Windows files", Color.White, TextDark);
            repairWindows.Click += delegate { RunSfc(true); };
            commands.Controls.Add(repairWindows);
            layout.Controls.Add(commands, 0, 0);

            _findingList = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, HideSelection = false, BackColor = Color.White, ForeColor = TextDark };
            _findingList.Columns.Add("Time", 135);
            _findingList.Columns.Add("Severity", 85);
            _findingList.Columns.Add("Category", 125);
            _findingList.Columns.Add("Location", 240);
            _findingList.Columns.Add("Summary", 430);
            _findingList.Columns.Add("Recommended next step", 390);
            layout.Controls.Add(_findingList, 0, 1);

            FlowLayoutPanel bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 7, 0, 0) };
            _fixButton = MakeButton("Fix selected finding", Blue, Color.White);
            _fixButton.Click += async delegate { await FixSelectedFindingAsync(); };
            _repairButton = MakeButton("Repair filesystem…", Color.FromArgb(165, 75, 38), Color.White);
            _repairButton.Click += delegate { RunChkdskRepair(); };
            Button clear = MakeButton("Clear findings", Color.White, TextDark);
            clear.Click += delegate { _findings.Clear(); RefreshFindings(); Log("Findings cleared by user."); };
            bottom.Controls.Add(_fixButton);
            bottom.Controls.Add(_repairButton);
            bottom.Controls.Add(clear);
            layout.Controls.Add(bottom, 0, 2);
            return page;
        }

        private TabPage BuildActivityTab()
        {
            TabPage page = MakeTab("Activity & Report");
            Panel tools = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Canvas };
            Button export = MakeButton("Export diagnostic report…", Blue, Color.White);
            export.Location = new Point(0, 7);
            export.Click += delegate { ExportReport(); };
            tools.Controls.Add(export);
            page.Controls.Add(tools);
            _activityBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9F), BackColor = Color.White, ForeColor = TextDark, BorderStyle = BorderStyle.FixedSingle };
            page.Controls.Add(_activityBox);
            return page;
        }

        private Panel MakeSummaryCard(string caption, Label value, int width)
        {
            Panel panel = new Panel { Width = width, Height = 82, BackColor = Color.White, Margin = new Padding(0, 0, 12, 0), Padding = new Padding(14, 8, 8, 8) };
            panel.Paint += delegate(object sender, PaintEventArgs e) { using (Pen pen = new Pen(Border)) e.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1); };
            Label heading = new Label { Text = caption, ForeColor = Muted, AutoSize = true, Location = new Point(12, 8) };
            value.Text = "—";
            value.ForeColor = TextDark;
            value.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold);
            value.AutoSize = true;
            value.Location = new Point(12, 31);
            panel.Controls.Add(heading);
            panel.Controls.Add(value);
            return panel;
        }

        private static TabPage MakeTab(string name) { return new TabPage(name) { BackColor = Canvas, Padding = new Padding(4) }; }

        private Button MakeButton(string text, Color background, Color foreground)
        {
            Button button = new Button { Text = text, AutoSize = true, Height = 32, Padding = new Padding(10, 2, 10, 2), Margin = new Padding(4, 2, 4, 2), BackColor = background, ForeColor = foreground, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F, FontStyle.Regular) };
            button.FlatAppearance.BorderColor = background == Color.White ? Border : background;
            button.Cursor = Cursors.Hand;
            return button;
        }

        private static DataGridView CreateGrid()
        {
            return new DataGridView { ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, AutoGenerateColumns = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, RowHeadersVisible = false, BackgroundColor = Color.White, BorderStyle = BorderStyle.FixedSingle, GridColor = Border, EnableHeadersVisualStyles = false, ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(230, 236, 244), ForeColor = TextDark, Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold), Padding = new Padding(4) }, DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.White, ForeColor = TextDark, SelectionBackColor = Color.FromArgb(218, 233, 250), SelectionForeColor = TextDark, Padding = new Padding(4) }, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
        }

        private static DataGridViewTextBoxColumn CreateTextColumn(string property, string header, int minWidth)
        {
            return new DataGridViewTextBoxColumn { DataPropertyName = property, HeaderText = header, MinimumWidth = Math.Min(minWidth, 45), SortMode = DataGridViewColumnSortMode.Automatic, FillWeight = Math.Max(35, minWidth) };
        }

        private async Task RefreshAllAsync()
        {
            if (_refreshing) return;
            _refreshing = true;
            _refreshButton.Enabled = false;
            _refreshButton.Text = "Scanning…";
            Log("Started drive inventory refresh.");
            try
            {
                Tuple<IList<DriveRecord>, IList<PhysicalDiskRecord>> snapshot = await Task.Run(delegate
                {
                    IList<DriveRecord> volumes = _inventory.GetVolumes();
                    IList<PhysicalDiskRecord> physical = _inventory.GetPhysicalDiskInventory();
                    return Tuple.Create(volumes, physical);
                });
                IList<DriveRecord> found = snapshot.Item1;
                string oldBrowse = SelectedDrive(_browseDrive) == null ? null : SelectedDrive(_browseDrive).RootPath;
                string oldDiag = SelectedDrive(_diagnosticDrive) == null ? null : SelectedDrive(_diagnosticDrive).RootPath;
                _drives.Clear();
                foreach (DriveRecord drive in found) _drives.Add(drive);
                _driveGrid.DataSource = null;
                _driveGrid.DataSource = _drives;
                _physicalDisks.Clear();
                foreach (PhysicalDiskRecord disk in snapshot.Item2) _physicalDisks.Add(disk);
                _physicalGrid.DataSource = null;
                _physicalGrid.DataSource = _physicalDisks;
                PopulateDriveCombo(_browseDrive, oldBrowse);
                PopulateDriveCombo(_diagnosticDrive, oldDiag);

                int warnings = _physicalDisks.Count(x => x.Health == "Warning") + _drives.Count(x => x.Health == "Warning" && x.PhysicalDisk == "Not mapped");
                _volumeSummary.Text = _drives.Count.ToString();
                _healthSummary.Text = warnings.ToString();
                _healthSummary.ForeColor = warnings > 0 ? Color.FromArgb(173, 63, 34) : TextDark;
                DriveRecord os = _drives.FirstOrDefault(x => x.IsWindowsVolume);
                _osSummary.Text = os == null ? "Not identified" : os.RootPath;
                Log("Inventory complete: " + _drives.Count + " logical volumes; " + _physicalDisks.Count + " physical disks; " + warnings + " hardware warning(s). A missing warning is not proof of healthy hardware.");
                foreach (DriveRecord drive in _drives.Where(x => x.Health == "Warning"))
                    AddFindingOnce(CreateFinding(FindingKind.HardwareWarning, "Warning", "Drive health", drive.RootPath, drive.HealthDetails, drive));
                foreach (PhysicalDiskRecord disk in _physicalDisks.Where(x => x.Health == "Warning"))
                    AddFindingOnce(CreateFinding(FindingKind.HardwareWarning, "Warning", "Physical disk health", disk.DeviceId, disk.Details, null));
            }
            catch (Exception ex)
            {
                Log("Drive inventory failed: " + ex.Message);
                MessageBox.Show(this, "Drive inventory could not be completed.\r\n\r\n" + ex.Message, "DriveMedic", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _refreshButton.Enabled = true;
                _refreshButton.Text = "Refresh drives";
                _refreshing = false;
            }
        }

        private void PopulateDriveCombo(ComboBox combo, string preferredRoot)
        {
            if (combo == null) return;
            combo.SelectedIndexChanged -= DriveComboChanged;
            combo.DataSource = null;
            combo.DisplayMember = "DisplayName";
            combo.DataSource = _drives.ToList();
            int selected = -1;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                DriveRecord drive = combo.Items[i] as DriveRecord;
                if (drive != null && string.Equals(drive.RootPath, preferredRoot, StringComparison.OrdinalIgnoreCase)) { selected = i; break; }
            }
            if (selected < 0)
            {
                DriveRecord os = _drives.FirstOrDefault(x => x.IsWindowsVolume);
                selected = os == null ? (_drives.Count > 0 ? 0 : -1) : _drives.IndexOf(os);
            }
            if (selected >= 0 && selected < combo.Items.Count) combo.SelectedIndex = selected;
            combo.SelectedIndexChanged += DriveComboChanged;
        }

        private void DriveComboChanged(object sender, EventArgs e) { }

        private static DriveRecord SelectedDrive(ComboBox combo) { return combo == null ? null : combo.SelectedItem as DriveRecord; }

        private DriveRecord SelectedGridDrive()
        {
            return _driveGrid.CurrentRow == null ? null : _driveGrid.CurrentRow.DataBoundItem as DriveRecord;
        }

        private void BrowseSelectedDrive()
        {
            DriveRecord selected = SelectedGridDrive();
            if (selected == null) { MessageBox.Show(this, "Select a volume first.", "DriveMedic", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            SelectComboRoot(_browseDrive, selected.RootPath);
            _tabs.SelectedIndex = 2;
            NavigateTo(selected.RootPath);
        }

        private static void SelectComboRoot(ComboBox combo, string root)
        {
            if (combo == null) return;
            for (int i = 0; i < combo.Items.Count; i++)
            {
                DriveRecord drive = combo.Items[i] as DriveRecord;
                if (drive != null && string.Equals(drive.RootPath, root, StringComparison.OrdinalIgnoreCase)) { combo.SelectedIndex = i; return; }
            }
        }

        private async void NavigateTo(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            string resolved;
            try { resolved = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'))); }
            catch (Exception ex) { MessageBox.Show(this, "Invalid path: " + ex.Message, "Browse", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            _pathBox.Text = resolved;
            _fileList.Items.Clear();
            _fileList.Enabled = false;
            try
            {
                DirectorySnapshot snapshot = await Task.Run(delegate { return ReadDirectorySnapshot(resolved); });
                foreach (FileEntry entry in snapshot.Entries)
                {
                    ListViewItem row = new ListViewItem((entry.IsDirectory ? "[DIR] " : "      ") + entry.Name);
                    row.SubItems.Add(entry.IsDirectory ? "Folder" : "File");
                    row.SubItems.Add(entry.IsDirectory ? "—" : DriveRecord.FormatBytes(entry.Size));
                    row.SubItems.Add(entry.LastModified.HasValue ? entry.LastModified.Value.ToString("yyyy-MM-dd HH:mm") : "—");
                    row.SubItems.Add(entry.AccessStatus);
                    row.Tag = entry;
                    _fileList.Items.Add(row);
                }
                Log("Opened folder: " + resolved + " (" + snapshot.Entries.Count + " entries listed).");
                if (!string.IsNullOrWhiteSpace(snapshot.Error))
                {
                    Log("Folder access warning: " + snapshot.Error);
                    DriveRecord affectedDrive = FindDriveForPath(resolved) ?? SelectedDrive(_browseDrive);
                    FindingKind issueKind = snapshot.Error.IndexOf("Access is denied", StringComparison.OrdinalIgnoreCase) >= 0
                        || snapshot.Error.IndexOf("access denied", StringComparison.OrdinalIgnoreCase) >= 0
                        ? FindingKind.AccessDenied : FindingKind.ReadError;
                    AddFinding(CreateFinding(issueKind, "Warning", "Folder access", resolved, snapshot.Error, affectedDrive));
                }
            }
            catch (Exception ex)
            {
                Log("Folder open failed: " + resolved + " — " + ex.Message);
                DriveRecord affectedDrive = FindDriveForPath(resolved) ?? SelectedDrive(_browseDrive);
                AddFinding(CreateFinding(ex is UnauthorizedAccessException ? FindingKind.AccessDenied : FindingKind.ReadError, "Warning", "Folder access", resolved, ex.Message, affectedDrive));
                MessageBox.Show(this, "This folder could not be opened. The application will not change ownership or permissions automatically.\r\n\r\n" + ex.Message, "Folder access", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { _fileList.Enabled = true; }
        }

        private static DirectorySnapshot ReadDirectorySnapshot(string path)
        {
            DirectorySnapshot snapshot = new DirectorySnapshot();
            if (!Directory.Exists(path))
            {
                try
                {
                    FileAttributes attributes = File.GetAttributes(path);
                    if ((attributes & FileAttributes.Directory) == 0) throw new DirectoryNotFoundException("The selected path is not a folder: " + path);
                }
                catch (UnauthorizedAccessException) { throw; }
                catch (FileNotFoundException) { throw new DirectoryNotFoundException("The folder does not exist or the volume is unavailable: " + path); }
                catch (DirectoryNotFoundException) { throw new DirectoryNotFoundException("The folder does not exist or the volume is unavailable: " + path); }
            }
            try
            {
                foreach (string directory in Directory.GetDirectories(path))
                {
                    try
                    {
                        DirectoryInfo info = new DirectoryInfo(directory);
                        snapshot.Entries.Add(new FileEntry { FullPath = directory, Name = info.Name, IsDirectory = true, LastModified = info.LastWriteTime, AccessStatus = "Not tested" });
                    }
                    catch (Exception ex) { snapshot.Error = AddError(snapshot.Error, directory + ": " + ex.Message); }
                }
            }
            catch (Exception ex) { snapshot.Error = AddError(snapshot.Error, "Folder enumeration: " + ex.Message); }
            try
            {
                foreach (string file in Directory.GetFiles(path))
                {
                    try
                    {
                        FileInfo info = new FileInfo(file);
                        snapshot.Entries.Add(new FileEntry { FullPath = file, Name = info.Name, IsDirectory = false, Size = info.Length, LastModified = info.LastWriteTime, AccessStatus = "Not tested" });
                    }
                    catch (Exception ex) { snapshot.Error = AddError(snapshot.Error, file + ": " + ex.Message); }
                }
            }
            catch (Exception ex) { snapshot.Error = AddError(snapshot.Error, "File enumeration: " + ex.Message); }
            snapshot.Entries.Sort(delegate(FileEntry a, FileEntry b)
            {
                if (a.IsDirectory != b.IsDirectory) return a.IsDirectory ? -1 : 1;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            if (snapshot.Entries.Count == 0 && string.IsNullOrEmpty(snapshot.Error)) snapshot.Error = "No entries found. This may be an empty folder.";
            return snapshot;
        }

        private static string AddError(string existing, string item)
        {
            if (string.IsNullOrEmpty(existing)) return item;
            return existing + Environment.NewLine + item;
        }

        private void NavigateUp()
        {
            string current = _pathBox.Text;
            try
            {
                DirectoryInfo parent = Directory.GetParent(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (parent != null) NavigateTo(parent.FullName);
            }
            catch (Exception ex) { Log("Could not navigate to parent folder: " + ex.Message); }
        }

        private void FindUserProfiles()
        {
            DriveRecord drive = SelectedDrive(_browseDrive);
            if (drive == null) { MessageBox.Show(this, "Select a source drive first.", "Find profiles", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string users = Path.Combine(drive.RootPath, "Users");
            if (!CanSeeDirectory(users)) users = Path.Combine(drive.RootPath, "Documents and Settings");
            if (!CanSeeDirectory(users))
            {
                AddFinding(CreateFinding(FindingKind.ReadError, "Info", "Profile search", drive.RootPath, "No accessible Users or Documents and Settings folder was found at the drive root.", drive));
                MessageBox.Show(this, "No accessible user-profile folder was found at this drive root. Browse the drive manually or use Restart as admin if access is denied. Windows permissions, BitLocker, and EFS can still limit access.", "Find profiles", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            NavigateTo(users);
            Log("Searched common Windows profile location: " + users + ". Open a profile, then use Open common folder for Desktop, Documents, Downloads, Pictures, Music, or Videos.");
        }

        private static bool CanSeeDirectory(string path)
        {
            try { return (File.GetAttributes(path) & FileAttributes.Directory) != 0; }
            catch { return false; }
        }

        private void OpenCommonFolder()
        {
            string current = _pathBox == null ? string.Empty : _pathBox.Text.Trim();
            string folder = _commonFolder == null ? null : _commonFolder.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(folder)) return;
            string candidate = Path.Combine(current, folder);
            if (!CanSeeDirectory(candidate))
            {
                MessageBox.Show(this, "The folder was not found at the current path or cannot be inspected. Navigate into a specific old user-profile folder first. If access is denied, try Restart as admin; encryption still requires its key." + Environment.NewLine + Environment.NewLine + candidate, "Common folder", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            NavigateTo(candidate);
        }

        private async Task CopySelectedAsync()
        {
            if (_fileList.SelectedItems.Count == 0)
            {
                MessageBox.Show(this, "Select one or more files or folders first. The source will be copied, not moved.", "Recover files", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            List<string> sourcePaths = _fileList.SelectedItems.Cast<ListViewItem>().Select(x => ((FileEntry)x.Tag).FullPath).ToList();
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose a destination on a different physical drive when possible.";
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath)) return;
                string destinationVolume = string.Empty;
                try { destinationVolume = Path.GetPathRoot(Path.GetFullPath(dialog.SelectedPath)); } catch { }
                List<string> sameVolumeSources = sourcePaths.Where(delegate(string source)
                {
                    try { return !string.IsNullOrEmpty(destinationVolume) && string.Equals(Path.GetPathRoot(Path.GetFullPath(source)), destinationVolume, StringComparison.OrdinalIgnoreCase); }
                    catch { return false; }
                }).ToList();
                if (sameVolumeSources.Count > 0 && MessageBox.Show(this,
                    "The destination appears to be on the same volume as " + sameVolumeSources.Count + " selected source item(s). This is not a safe backup if that disk fails. Continue anyway?\r\n\r\nFor recovery, prefer a different physical disk.",
                    "Destination is on the source volume", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
                DialogResult copyChoice = MessageBox.Show(this,
                    "Copy " + sourcePaths.Count + " selected item(s) to:\r\n" + dialog.SelectedPath +
                    "\r\n\r\nSource files will not be changed. Existing destination files will not be overwritten.\r\n\r\n" +
                    "YES = copy and verify each file with SHA-256 (recommended).\r\nNO = copy without SHA-256 verification.\r\nCANCEL = do not copy.",
                    "Confirm recovery copy", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1);
                if (copyChoice == DialogResult.Cancel) return;
                bool verifySha256 = copyChoice == DialogResult.Yes;
                _copyButton.Enabled = false;
                _copyButton.Text = "Copying…";
                Log("Started copy-only recovery to " + dialog.SelectedPath + (verifySha256 ? " with SHA-256 verification." : " without SHA-256 verification."));
                try
                {
                    CopyResult result = await Task.Run(delegate { return _recovery.CopyPaths(sourcePaths, dialog.SelectedPath, verifySha256); });
                    string summary = "Files copied: " + result.CopiedFiles + "; SHA-256 verified: " + result.FilesVerified +
                        "; bytes copied: " + DriveRecord.FormatBytes(result.BytesCopied) + "; errors: " + result.FailedFiles +
                        "; skipped reparse links: " + result.SkippedReparsePoints + ".";
                    Log(summary);
                    foreach (string message in result.Messages.Take(500)) Log("  " + message);
                    AddFinding(new DiagnosticFinding { Severity = result.FailedFiles > 0 ? "Warning" : "Info", Category = "Recovery copy", Location = dialog.SelectedPath, Summary = summary, Details = string.Join(Environment.NewLine, result.Messages.Take(200)), RecommendedAction = result.FailedFiles > 0 ? RepairAction.CopyRecoverableData : RepairAction.None });
                    MessageBox.Show(this, summary + "\r\n\r\nReview Activity & Report for file-level errors.", "Recovery copy finished", MessageBoxButtons.OK, result.FailedFiles > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    Log("Recovery copy failed: " + ex.Message);
                    MessageBox.Show(this, ex.Message, "Recovery copy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally { _copyButton.Enabled = true; _copyButton.Text = "Copy selected to…"; }
            }
        }

        private async Task RunReadabilityScanAsync()
        {
            string path = _pathBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                DriveRecord drive = SelectedDrive(_diagnosticDrive);
                if (drive != null && drive.IsReady) path = drive.RootPath;
                else { MessageBox.Show(this, "Open a folder in Browse & Recover or choose a ready volume.", "Readability scan", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            }
            if (MessageBox.Show(this, "A readability scan reads file contents but does not modify them. It may take a long time and place load on a failing drive. If the data is irreplaceable or the drive is making unusual noises, stop and consider professional recovery first.\r\n\r\nScan up to 10,000 files under:\r\n" + path, "Confirm read-only scan", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            _scanButton.Enabled = false;
            _scanButton.Text = "Scanning…";
            Log("Started read-only readability scan under: " + path);
            try
            {
                ScanResult result = await Task.Run(delegate { return DiagnosticService.ScanReadableFiles(path, 10000); });
                DriveRecord drive = FindDriveForPath(path) ?? SelectedDrive(_diagnosticDrive);
                if (result.Issues.Count == 0)
                {
                    AddFinding(CreateFinding(FindingKind.Information, "Info", "Readability scan", path, "No read errors or access denials were observed in " + result.FilesChecked + " file(s). This is not a proof of file-format integrity or hardware health.", drive));
                }
                foreach (ScanIssue issue in result.Issues.Take(200))
                {
                    FindingKind kind = issue.Kind.IndexOf("Access denied", StringComparison.OrdinalIgnoreCase) >= 0 ? FindingKind.AccessDenied : FindingKind.ReadError;
                    AddFinding(CreateFinding(kind, "Warning", issue.Kind, issue.Path, issue.Message, drive));
                }
                if (result.Issues.Count > 200) AddFinding(CreateFinding(FindingKind.ScanLimitReached, "Info", "Findings capped", path, (result.Issues.Count - 200) + " additional scan issue(s) were not added individually.", drive));
                if (result.LimitReached) AddFinding(CreateFinding(FindingKind.ScanLimitReached, "Info", "Scan limit", path, "Stopped after the configured limit of 10,000 files. Run on a smaller folder to continue.", drive));
                string summary = "Readability scan complete. Files read: " + result.FilesChecked + "; bytes read: " + DriveRecord.FormatBytes(result.BytesRead) + "; issues: " + result.Issues.Count + (result.LimitReached ? "; file limit reached." : ".");
                Log(summary);
                MessageBox.Show(this, summary + "\r\n\r\nAn I/O error is evidence of a read problem, but not enough on its own to identify the exact hardware or filesystem cause.", "Readability scan", MessageBoxButtons.OK, result.Issues.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log("Readability scan failed: " + ex.Message);
                AddFinding(CreateFinding(ex is UnauthorizedAccessException ? FindingKind.AccessDenied : FindingKind.ReadError, "Warning", "Readability scan", path, ex.Message, SelectedDrive(_diagnosticDrive)));
            }
            finally { _scanButton.Enabled = true; _scanButton.Text = "Scan readable files"; }
        }

        private async Task RunChkdskReadOnlyAsync()
        {
            DriveRecord drive = SelectedDrive(_diagnosticDrive);
            if (drive == null || !drive.IsReady) { MessageBox.Show(this, "Choose a ready, mounted drive letter.", "Filesystem check", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (IsUnsafeFilesystemForChkdsk(drive))
            {
                AddFinding(CreateFinding(FindingKind.RawVolume, "Warning", "RAW/unknown volume", drive.RootPath, "Windows does not provide a confirmed filesystem type for this volume. Do not format it or run generic repair commands; recover or image it first.", drive));
                return;
            }
            if (drive.Health == "Warning" && MessageBox.Show(this, "A hardware warning is reported for this volume. A full filesystem scan may add stress to a failing drive. Recover or image data first. Continue anyway?", "Hardware warning", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            _checkButton.Enabled = false;
            _checkButton.Text = "Checking…";
            Log("Started read-only CHKDSK for " + drive.RootPath + ".");
            try
            {
                ChkdskResult result = await Task.Run(delegate { return DiagnosticService.RunChkdskReadOnly(drive.RootPath); });
                string summary = DiagnosticService.SummarizeChkdskOutput(result);
                Log("CHKDSK summary: " + summary);
                Log("CHKDSK output follows:\r\n" + result.Output);
                FindingKind kind = result.AccessDenied ? FindingKind.AccessDenied
                    : (result.TimedOut ? FindingKind.ReadError
                    : (result.ExitCode != 0 || summary.IndexOf("may indicate filesystem problems", StringComparison.OrdinalIgnoreCase) >= 0 ? FindingKind.FileSystemError : FindingKind.Information));
                AddFinding(CreateFinding(kind, kind == FindingKind.Information ? "Info" : "Warning", "CHKDSK read-only", drive.RootPath, summary, drive, result.Output));
                MessageBox.Show(this, summary + "\r\n\r\nFull output is available in Activity & Report.", "Filesystem check complete", MessageBoxButtons.OK, kind == FindingKind.Information ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                Log("Read-only CHKDSK failed: " + ex.Message);
                AddFinding(CreateFinding(FindingKind.AccessDenied, "Warning", "CHKDSK failed", drive.RootPath, ex.Message, drive));
                MessageBox.Show(this, ex.Message, "Filesystem check", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { _checkButton.Enabled = true; _checkButton.Text = "Check filesystem (read-only)"; }
        }

        private static bool IsUnsafeFilesystemForChkdsk(DriveRecord drive)
        {
            if (drive == null || string.IsNullOrWhiteSpace(drive.FileSystem)) return true;
            return drive.FileSystem.Equals("RAW", StringComparison.OrdinalIgnoreCase)
                || drive.FileSystem.Equals("RAW/Unknown", StringComparison.OrdinalIgnoreCase)
                || drive.FileSystem.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
                || drive.FileSystem.Equals("Unavailable", StringComparison.OrdinalIgnoreCase);
        }

        private void RunChkdskRepair()
        {
            DriveRecord drive = SelectedDrive(_diagnosticDrive);
            if (drive == null || !drive.IsReady) { MessageBox.Show(this, "Choose a ready, mounted drive letter.", "Repair filesystem", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (DriveHasHardwareWarning(drive))
            {
                AddFinding(CreateFinding(FindingKind.HardwareWarning, "Warning", "Repair blocked", drive.RootPath, "In-place filesystem repair is blocked because the selected or an un-mapped physical drive reports a hardware warning. Image/recover data first.", drive));
                MessageBox.Show(this, "DriveMedic will not launch CHKDSK /f while the selected physical drive reports a hardware warning. If Windows could not map the volume to a physical disk, any reported disk warning also blocks this repair. Recover or image the source first.", "Repair blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (IsUnsafeFilesystemForChkdsk(drive))
            {
                AddFinding(CreateFinding(FindingKind.RawVolume, "Warning", "Repair blocked", drive.RootPath, "RAW or unknown filesystems are not passed to CHKDSK /f by DriveMedic.", drive));
                MessageBox.Show(this, "The filesystem is RAW or unknown. Do not format it. Create a sector image or use a reputable recovery workflow first.", "Repair blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string chkdskDrive;
            try { chkdskDrive = DiagnosticService.ValidateDriveRoot(drive.RootPath); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Repair unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string message = "CHKDSK /f attempts to modify filesystem metadata. It is not a data-recovery tool and can make recovery harder on failing media.\r\n\r\nFirst copy your important files or create a sector-level image. Continue only if you understand the risk.\r\n\r\nTarget: " + chkdskDrive + " (" + drive.FileSystem + ")";
            if (MessageBox.Show(this, message, "Confirm filesystem repair", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            try
            {
                ProcessStartInfo start = new ProcessStartInfo("cmd.exe", "/k chkdsk " + chkdskDrive + " /f");
                start.UseShellExecute = true;
                start.Verb = "runas";
                start.WorkingDirectory = Environment.SystemDirectory;
                Process.Start(start);
                Log("Launched interactive elevated CHKDSK /f for " + drive.RootPath + ". Review its output and any schedule/unmount prompt in the command window.");
                AddFinding(CreateFinding(FindingKind.FileSystemError, "Action", "Repair launched", drive.RootPath, "Interactive CHKDSK /f was launched. DriveMedic does not claim repair success; verify the command output and recheck afterward.", drive));
            }
            catch (Exception ex)
            {
                Log("Could not launch CHKDSK /f: " + ex.Message);
                MessageBox.Show(this, "Could not launch the repair command. " + ex.Message, "Repair filesystem", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RunSfc(bool repair)
        {
            DriveRecord os = _drives.FirstOrDefault(x => x.IsWindowsVolume);
            DriveRecord target = SelectedDrive(_diagnosticDrive);
            if (target != null && !target.IsWindowsVolume)
            {
                MessageBox.Show(this, "SFC acts on the currently running Windows installation, not the selected offline Windows folder. Select the current Windows OS volume or use Windows Recovery Environment for offline repair.", "Windows file verification", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (os == null) { MessageBox.Show(this, "Current Windows OS volume could not be identified.", "Windows file verification", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            string action = repair ? "sfc /scannow" : "sfc /verifyonly";
            string warning = repair ? "This scans and attempts to repair protected files in the currently running Windows installation. Back up important data first." : "This verifies protected Windows system files without attempting repairs.";
            if (MessageBox.Show(this, warning + "\r\n\r\nLaunch " + action + " in an elevated command window?", "Windows file verification", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try
            {
                ProcessStartInfo start = new ProcessStartInfo("cmd.exe", "/k " + action);
                start.UseShellExecute = true;
                start.Verb = "runas";
                start.WorkingDirectory = Environment.SystemDirectory;
                Process.Start(start);
                Log("Launched " + action + " for the currently running Windows installation. Review the command window for results.");
                AddFinding(new DiagnosticFinding { Severity = "Action", Category = "Windows files", Location = os.RootPath, Summary = action + " launched. DriveMedic cannot verify completion of the separate console operation.", Details = warning, RecommendedAction = repair ? RepairAction.RunSystemFileChecker : RepairAction.None });
            }
            catch (Exception ex) { Log("Could not launch SFC: " + ex.Message); MessageBox.Show(this, ex.Message, "Windows file verification", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private async Task FixSelectedFindingAsync()
        {
            if (_findingList.SelectedItems.Count == 0) { MessageBox.Show(this, "Select a finding first.", "Fix finding", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            DiagnosticFinding finding = _findingList.SelectedItems[0].Tag as DiagnosticFinding;
            if (finding == null) return;
            DriveRecord drive = _drives.FirstOrDefault(x => string.Equals(x.RootPath, finding.Location, StringComparison.OrdinalIgnoreCase));
            if (drive == null) drive = FindDriveForPath(finding.Location);
            if (drive == null) drive = SelectedDrive(_diagnosticDrive);
            switch (finding.RecommendedAction)
            {
                case RepairAction.RetryAsAdministrator:
                    if (MessageBox.Show(this, "Restart DriveMedic as administrator and retry the operation? This does not bypass encryption or grant permission to data you are not authorized to access.", "Retry with administrator rights", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes) RestartAsAdministrator();
                    break;
                case RepairAction.CopyRecoverableData:
                    _tabs.SelectedIndex = 2;
                    if (!string.IsNullOrWhiteSpace(finding.Location) && (File.Exists(finding.Location) || Directory.Exists(finding.Location)))
                    {
                        string folder = Directory.Exists(finding.Location) ? finding.Location : Path.GetDirectoryName(finding.Location);
                        NavigateTo(folder);
                        MessageBox.Show(this, "Select the affected file or folder in the browser, then choose Copy selected to… and save it to another drive.", "Copy recoverable data", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else if (drive != null) NavigateTo(drive.RootPath);
                    break;
                case RepairAction.RunChkdskRepair:
                    if (drive != null) SelectComboRoot(_diagnosticDrive, drive.RootPath);
                    RunChkdskRepair();
                    break;
                case RepairAction.RunSystemFileChecker:
                    RunSfc(true);
                    break;
                case RepairAction.StopAndRecoverOrImageDrive:
                    MessageBox.Show(this, finding.Summary + "\r\n\r\n" + RepairAdvisor.Explain(RepairAction.StopAndRecoverOrImageDrive), "No in-place fix recommended", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    break;
                default:
                    await Task.Delay(0);
                    MessageBox.Show(this, "No automated fix is available for this finding. Review its details and the exported report.", "Fix finding", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
            }
        }

        private DriveRecord FindDriveForPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            string fullPath;
            try { fullPath = Path.GetFullPath(path); }
            catch { fullPath = path; }
            return _drives.Where(x => !string.IsNullOrWhiteSpace(x.RootPath))
                .OrderByDescending(x => x.RootPath.Length)
                .FirstOrDefault(x => fullPath.StartsWith(x.RootPath, StringComparison.OrdinalIgnoreCase));
        }

        private DiagnosticFinding CreateFinding(FindingKind kind, string severity, string category, string location, string summary, DriveRecord drive, string details = null)
        {
            bool currentSystem = drive != null && drive.IsWindowsVolume;
            bool hardwareWarning = drive != null && DriveHasHardwareWarning(drive);
            bool raw = drive != null && IsUnsafeFilesystemForChkdsk(drive);
            RepairAction action = RepairAdvisor.Recommend(kind, currentSystem, hardwareWarning, raw);
            string recommendation = RepairAdvisor.Explain(action);
            string combined = string.IsNullOrWhiteSpace(details) ? recommendation : recommendation + Environment.NewLine + Environment.NewLine + details;
            return new DiagnosticFinding { Time = DateTime.Now, Severity = severity, Category = category, Location = location ?? string.Empty, Summary = summary ?? string.Empty, Details = combined, RecommendedAction = action };
        }

        private bool DriveHasHardwareWarning(DriveRecord drive)
        {
            if (drive == null) return false;
            if (drive.Health == "Warning") return true;
            if (drive.PhysicalDiskIndex.HasValue)
                return _physicalDisks.Any(disk => disk.Index == drive.PhysicalDiskIndex.Value && disk.Health == "Warning");

            // If mapping failed, there is no safe way to prove a warning belongs to another disk.
            // Prefer blocking a write-mode repair over risking the wrong physical source.
            return _physicalDisks.Any(disk => disk.Health == "Warning");
        }

        private void AddFinding(DiagnosticFinding finding)
        {
            _findings.Add(finding);
            RefreshFindings();
        }

        private void AddFindingOnce(DiagnosticFinding finding)
        {
            if (_findings.Any(existing => existing.Category == finding.Category
                && string.Equals(existing.Location, finding.Location, StringComparison.OrdinalIgnoreCase)
                && string.Equals(existing.Summary, finding.Summary, StringComparison.OrdinalIgnoreCase))) return;
            AddFinding(finding);
        }

        private void RefreshFindings()
        {
            if (_findingList == null) return;
            _findingList.BeginUpdate();
            _findingList.Items.Clear();
            foreach (DiagnosticFinding finding in _findings)
            {
                ListViewItem item = new ListViewItem(finding.Time.ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add(finding.Severity);
                item.SubItems.Add(finding.Category);
                item.SubItems.Add(finding.Location);
                item.SubItems.Add(finding.Summary);
                item.SubItems.Add(RepairAdvisor.Explain(finding.RecommendedAction));
                item.Tag = finding;
                if (finding.Severity == "Warning") item.ForeColor = Color.FromArgb(155, 61, 38);
                _findingList.Items.Add(item);
            }
            _findingList.EndUpdate();
        }

        private void Log(string message)
        {
            string line = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + message;
            _activity.Add(line);
            if (_activity.Count > 5000) _activity.RemoveRange(0, _activity.Count - 5000);
            if (_activityBox != null)
            {
                _activityBox.AppendText(line + Environment.NewLine);
                _activityBox.SelectionStart = _activityBox.TextLength;
                _activityBox.ScrollToCaret();
            }
        }

        private void ExportReport()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "Export DriveMedic diagnostic report";
                dialog.Filter = "Text report (*.txt)|*.txt|CSV findings (*.csv)|*.csv";
                dialog.FileName = "DriveMedic-report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    if (string.Equals(Path.GetExtension(dialog.FileName), ".csv", StringComparison.OrdinalIgnoreCase))
                    {
                        StringBuilder csv = new StringBuilder("Time,Severity,Category,Location,Summary,RecommendedAction\r\n");
                        foreach (DiagnosticFinding finding in _findings)
                            csv.Append(Csv(finding.Time.ToString("s"))).Append(',').Append(Csv(finding.Severity)).Append(',').Append(Csv(finding.Category)).Append(',').Append(Csv(finding.Location)).Append(',').Append(Csv(finding.Summary)).Append(',').Append(Csv(finding.RecommendedAction.ToString())).Append("\r\n");
                        File.WriteAllText(dialog.FileName, csv.ToString(), Encoding.UTF8);
                    }
                    else
                    {
                        StringBuilder text = new StringBuilder();
                        text.AppendLine("DriveMedic Diagnostic Report");
                        text.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                        text.AppendLine("Computer: " + Environment.MachineName);
                        text.AppendLine("OS: " + Environment.OSVersion);
                        text.AppendLine("Application elevation: " + (IsAdministrator() ? "Administrator" : "Standard user"));
                        text.AppendLine();
                        text.AppendLine("DRIVE INVENTORY");
                        foreach (DriveRecord drive in _drives)
                            text.AppendLine(string.Join(" | ", drive.RootPath, drive.Label, drive.Role, drive.DriveType, drive.FileSystem, drive.Health, drive.CapacityText, drive.PhysicalDisk, drive.HealthDetails));
                        text.AppendLine();
                        text.AppendLine("FINDINGS");
                        foreach (DiagnosticFinding finding in _findings)
                        {
                            text.AppendLine("[" + finding.Time.ToString("s") + "] [" + finding.Severity + "] " + finding.Category + " — " + finding.Location);
                            text.AppendLine(finding.Summary);
                            text.AppendLine("Action: " + RepairAdvisor.Explain(finding.RecommendedAction));
                            text.AppendLine(finding.Details);
                            text.AppendLine();
                        }
                        text.AppendLine("ACTIVITY LOG");
                        foreach (string line in _activity) text.AppendLine(line);
                        text.AppendLine();
                        text.AppendLine("Privacy: this report is saved locally. DriveMedic does not upload data.");
                        text.AppendLine("Caution: absence of warnings does not prove the hardware is healthy; unreadable files do not identify the exact root cause.");
                        File.WriteAllText(dialog.FileName, text.ToString(), Encoding.UTF8);
                    }
                    Log("Exported diagnostic report to " + dialog.FileName);
                    MessageBox.Show(this, "Report saved locally:\r\n" + dialog.FileName, "Export report", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show(this, "Could not save report: " + ex.Message, "Export report", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private static string Csv(string value) { return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\""; }

        private static bool IsAdministrator()
        {
            try
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        private void RestartAsAdministrator()
        {
            try
            {
                ProcessStartInfo start = new ProcessStartInfo(Application.ExecutablePath);
                start.UseShellExecute = true;
                start.Verb = "runas";
                Process.Start(start);
                Log("Requested restart with administrator privileges.");            }
            catch (Exception ex)
            {
                Log("Administrator restart failed or was cancelled: " + ex.Message);
                MessageBox.Show(this, "DriveMedic could not restart as administrator. " + ex.Message, "Administrator access", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private sealed class FileEntry
        {
            public string FullPath;
            public string Name;
            public bool IsDirectory;
            public long Size;
            public DateTime? LastModified;
            public string AccessStatus;
        }

        private sealed class DirectorySnapshot
        {
            public readonly List<FileEntry> Entries = new List<FileEntry>();
            public string Error;
        }
    }
}