using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DriveMedic.Core;
using DriveMedic.Models;

namespace DriveMedic
{
    /// <summary>Recovery-first home screen. Existing inventory and diagnostics stay available in a tab.</summary>
    public sealed class RecoveryHubForm : Form
    {
        private readonly RecoveryService _recovery = new RecoveryService();
        private TabControl _tabs;
        private ListBox _sources;
        private ComboBox _drive;
        private TextBox _destination;
        private CheckBox _verify;
        private TextBox _copyLog;
        private TextBox _filePath;
        private TextBox _fileReport;
        private CheckBox _confirmHash;
        private TextBox _toolsFolder;
        private ComboBox _bootChoice;
        private TextBox _bootGuide;
        private TextBox _bootReport;
        private Button _copyButton;

        private static readonly Color Navy = Color.FromArgb(20, 40, 70);
        private static readonly Color Blue = Color.FromArgb(35, 105, 180);
        private static readonly Color Canvas = Color.FromArgb(244, 247, 251);
        private static readonly Color Dark = Color.FromArgb(35, 47, 62);
        private static readonly Color Muted = Color.FromArgb(103, 117, 135);

        public RecoveryHubForm()
        {
            Text = "DriveMedic — Simple Recovery";
            MinimumSize = new Size(1020, 680);
            Size = new Size(1280, 820);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Canvas;
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("Segoe UI", 9.5F);
            BuildShell();
        }

        private void BuildShell()
        {
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Canvas, Margin = new Padding(0) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            Panel header = new Panel { Dock = DockStyle.Fill, BackColor = Navy, Padding = new Padding(20, 8, 18, 8) };
            Label mark = new Label
            {
                Text = "DM", ForeColor = Color.White, BackColor = Blue,
                Font = new Font("Segoe UI Semibold", 14F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter, Size = new Size(46, 46), Location = new Point(20, 18)
            };
            header.Controls.Add(mark);
            header.Controls.Add(new Label
            {
                Text = "DriveMedic", ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 19F, FontStyle.Bold),
                AutoSize = true, Location = new Point(80, 10)
            });
            header.Controls.Add(new Label
            {
                Text = "File recovery and Windows startup help",
                ForeColor = Color.FromArgb(190, 209, 232),
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                AutoSize = true, Location = new Point(82, 45)
            });
            Label badge = new Label
            {
                Text = "COPY FIRST  •  REPAIR LATER",
                ForeColor = Color.White, BackColor = Color.FromArgb(37, 57, 82),
                Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold),
                Padding = new Padding(10, 7, 10, 7), AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(Width - 255, 25)
            };
            header.Controls.Add(badge);
            header.Resize += delegate { badge.Location = new Point(Math.Max(680, header.ClientSize.Width - badge.Width - 20), 25); };
            root.Controls.Add(header, 0, 0);

            _tabs = new TabControl
            {
                Dock = DockStyle.Fill, Padding = new Point(16, 8),
                Font = new Font("Segoe UI", 9.5F), HotTrack = true
            };
            _tabs.TabPages.Add(BuildHomeTab());
            _tabs.TabPages.Add(BuildCopyTab());
            _tabs.TabPages.Add(BuildFileTab());
            _tabs.TabPages.Add(BuildToolsTab());
            _tabs.TabPages.Add(BuildBootTab());
            _tabs.TabPages.Add(BuildExistingTab());
            _tabs.SelectedIndex = 0;
            root.Controls.Add(_tabs, 0, 1);
        }

        private TabPage BuildHomeTab()
        {
            TabPage page = MakeTab("Home");
            TableLayoutPanel layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4,
                Padding = new Padding(12), BackColor = Canvas
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
            page.Controls.Add(layout);

            Panel intro = new Panel { Dock = DockStyle.Fill, BackColor = Canvas, Margin = new Padding(4) };
            intro.Controls.Add(new Label
            {
                Text = "What would you like to do?",
                Font = new Font("Segoe UI Semibold", 17F, FontStyle.Bold),
                ForeColor = Dark, AutoSize = true, Location = new Point(5, 3)
            });
            intro.Controls.Add(new Label
            {
                Text = "Choose a task below. DriveMedic guides you through the next steps.",
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = Muted, AutoSize = true, Location = new Point(7, 38)
            });
            layout.Controls.Add(intro, 0, 0);
            layout.SetColumnSpan(intro, 2);

            layout.Controls.Add(MakeTaskCard(
                "Recover files from an old disk",
                "Copy documents, photos, downloads, and folders from a connected HDD or SSD to another drive.",
                "Start file recovery", Blue, delegate { _tabs.SelectedIndex = 1; }), 0, 1);

            layout.Controls.Add(MakeTaskCard(
                "Recover deleted files",
                "Try advanced tools for deleted files, lost partitions, or a drive Windows cannot open.",
                "Open recovery tools", Color.FromArgb(27, 126, 105), delegate { _tabs.SelectedIndex = 3; }), 1, 1);

            layout.Controls.Add(MakeTaskCard(
                "Fix startup, EFI, or GRUB",
                "Get guided steps for Windows boot problems, EFI/BCD, legacy boot, Linux GRUB, and RAW disks.",
                "Open boot help", Color.FromArgb(112, 82, 170), delegate { _tabs.SelectedIndex = 4; }), 0, 2);

            layout.Controls.Add(MakeTaskCard(
                "Check disk health",
                "Review detected drives, capacity, file system, health warnings, and diagnostics.",
                "View disk information", Color.FromArgb(190, 112, 35), delegate { _tabs.SelectedIndex = 5; }), 1, 2);

            Panel safety = new Panel
            {
                Dock = DockStyle.Fill, BackColor = Color.FromArgb(255, 248, 230),
                BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(5), Padding = new Padding(12)
            };
            safety.Controls.Add(new Label
            {
                Text = "Important: save recovered data to a different physical disk. Do not format or repair the old disk before copying important files. If the disk clicks, disconnects, or is failing, stop and consider professional recovery.",
                Dock = DockStyle.Fill, ForeColor = Color.FromArgb(112, 75, 20),
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft
            });
            layout.Controls.Add(safety, 0, 3);
            layout.SetColumnSpan(safety, 2);
            return page;
        }

        private Panel MakeTaskCard(string title, string description, string actionText, Color accent, Action action)
        {
            Panel card = new Panel
            {
                Dock = DockStyle.Fill, BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(5), Padding = new Padding(16)
            };
            card.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 5, BackColor = accent });
            Label titleLabel = new Label
            {
                Text = title, Font = new Font("Segoe UI Semibold", 12F, FontStyle.Bold),
                ForeColor = Dark, AutoSize = true, Location = new Point(22, 13)
            };
            Label descriptionLabel = new Label
            {
                Text = description, Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
                ForeColor = Muted, Location = new Point(22, 43), Size = new Size(420, 58),
                AutoEllipsis = true
            };
            Button go = Button(actionText, accent, Color.White, action);
            go.AutoSize = false;
            go.Size = new Size(205, 36);
            go.Location = new Point(22, 112);
            card.Controls.Add(titleLabel);
            card.Controls.Add(descriptionLabel);
            card.Controls.Add(go);
            card.Resize += delegate
            {
                descriptionLabel.Width = Math.Max(120, card.ClientSize.Width - 44);
                go.Location = new Point(22, Math.Max(100, card.ClientSize.Height - 49));
            };
            return card;
        }

        private TabPage BuildCopyTab()
        {
            TabPage page = MakeTab("Recover files");
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(10), BackColor = Canvas };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 61));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
            page.Controls.Add(layout);
            layout.Controls.Add(new Label
            {
                Text = "Step 1: choose your old drive and add files or folders. Step 2: choose a destination on a different physical disk. Step 3: start the copy. DriveMedic does not intentionally change source files.",
                Dock = DockStyle.Fill, ForeColor = Dark, Padding = new Padding(4, 5, 4, 4), AutoEllipsis = true
            }, 0, 0);

            FlowLayoutPanel add = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(0, 3, 0, 0) };
            _drive = new ComboBox { Width = 155, DropDownStyle = ComboBoxStyle.DropDownList, Font = Font };
            add.Controls.Add(new Label { Text = "Drive", AutoSize = true, ForeColor = Muted, Padding = new Padding(0, 8, 0, 0) });
            add.Controls.Add(_drive);
            add.Controls.Add(Button("Refresh", Color.White, Dark, RefreshDrives));
            add.Controls.Add(Button("Add drive contents", Blue, Color.White, AddWholeDrive));
            add.Controls.Add(Button("Add folder", Color.White, Dark, AddFolder));
            add.Controls.Add(Button("Add files…", Color.White, Dark, AddFiles));
            add.Controls.Add(Button("Remove selected", Color.White, Dark, RemoveSelected));
            add.Controls.Add(Button("Clear", Color.White, Dark, delegate { _sources.Items.Clear(); }));
            layout.Controls.Add(add, 0, 1);

            _sources = new ListBox { Dock = DockStyle.Fill, SelectionMode = SelectionMode.MultiExtended, HorizontalScrollbar = true, Font = new Font("Consolas", 9F), BackColor = Color.White, IntegralHeight = false };
            layout.Controls.Add(_sources, 0, 2);
            FlowLayoutPanel dest = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 3, 0, 0) };
            dest.Controls.Add(new Label { Text = "Destination", AutoSize = true, ForeColor = Muted, Padding = new Padding(0, 8, 0, 0) });
            _destination = new TextBox { Width = 540, Font = Font, Margin = new Padding(3, 6, 3, 3) };
            dest.Controls.Add(_destination);
            dest.Controls.Add(Button("Browse…", Color.White, Dark, ChooseDestination));
            layout.Controls.Add(dest, 0, 3);

            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(0, 3, 0, 0) };
            _verify = new CheckBox { Text = "Verify copies with SHA-256", Checked = true, AutoSize = true, ForeColor = Dark, Padding = new Padding(0, 6, 8, 0) };
            actions.Controls.Add(_verify);
            _copyButton = Button("Start safe copy", Blue, Color.White, async delegate { await CopyAsync(); });
            _copyButton.Width = 185;
            actions.Controls.Add(_copyButton);
            actions.Controls.Add(Button("Save log…", Color.White, Dark, delegate { SaveText("DriveMedic-copy-report.txt", _copyLog.Text); }));
            layout.Controls.Add(actions, 0, 4);

            _copyLog = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9F), BackColor = Color.White, Text = "Ready. If a drive clicks, disconnects, overheats, or reports hardware warnings, stop repeated scans and prioritize imaging or professional recovery." };
            layout.Controls.Add(_copyLog, 0, 5);
            RefreshDrives();
            return page;
        }

        private TabPage BuildFileTab()
        {
            TabPage page = MakeTab("Single file");
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12), BackColor = Canvas };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            page.Controls.Add(layout);
            layout.Controls.Add(new Label
            {
                Text = "Choose a file from the old disk to inspect it and queue a safe copy. A SHA-256 hash identifies bytes but cannot prove a file is valid without a trusted reference hash or a format-specific validator. Universal one-click repair of arbitrary file formats is not possible.",
                Dock = DockStyle.Fill, ForeColor = Dark, Padding = new Padding(4, 4, 4, 4), AutoEllipsis = true
            }, 0, 0);
            FlowLayoutPanel path = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 4, 0, 0) };
            _filePath = new TextBox { Width = 800, Font = Font, Margin = new Padding(3, 6, 3, 3) };
            path.Controls.Add(_filePath);
            path.Controls.Add(Button("Select file…", Color.White, Dark, ChooseFile));
            layout.Controls.Add(path, 0, 1);
            FlowLayoutPanel checks = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 0) };
            _confirmHash = new CheckBox { Text = "I understand hashing reads the entire file and may stress a failing disk.", AutoSize = true, ForeColor = Dark, Padding = new Padding(0, 5, 8, 0) };
            checks.Controls.Add(_confirmHash);
            checks.Controls.Add(Button("Inspect & hash", Blue, Color.White, async delegate { await InspectFileAsync(); }));
            layout.Controls.Add(checks, 0, 2);
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 0) };
            actions.Controls.Add(Button("Queue for safe recovery copy", Blue, Color.White, QueueFile));
            actions.Controls.Add(Button("Deleted-file tools", Color.White, Dark, delegate { _tabs.SelectedIndex = 3; }));
            actions.Controls.Add(Button("Save report…", Color.White, Dark, delegate { SaveText("DriveMedic-file-report.txt", _fileReport.Text); }));
            layout.Controls.Add(actions, 0, 3);
            _fileReport = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9F), BackColor = Color.White, Text = "No file inspected. For a failing disk, recover critical files before doing full-file scans." };
            layout.Controls.Add(_fileReport, 0, 4);
            return page;
        }

        private TabPage BuildToolsTab()
        {
            TabPage page = MakeTab("Deleted-file tools");
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12), BackColor = Canvas };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            page.Controls.Add(layout);
            layout.Controls.Add(new Label
            {
                Text = "Optional open-source integration: TestDisk analyzes lost partitions and certain boot sectors; PhotoRec/QPhotoRec carves many deleted files. They are separate downloads and are not bundled into DriveMedic. No tool can guarantee recovery of overwritten data. Save recovered output to a different physical disk.",
                Dock = DockStyle.Fill, ForeColor = Dark, Padding = new Padding(4, 4, 4, 4), AutoEllipsis = true
            }, 0, 0);
            FlowLayoutPanel folder = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 3, 0, 0) };
            folder.Controls.Add(new Label { Text = "Extracted tool folder", AutoSize = true, ForeColor = Muted, Padding = new Padding(0, 8, 0, 0) });
            _toolsFolder = new TextBox { Width = 760, Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "testdisk"), Font = Font, Margin = new Padding(3, 6, 3, 3) };
            folder.Controls.Add(_toolsFolder);
            folder.Controls.Add(Button("Browse…", Color.White, Dark, ChooseToolsFolder));
            layout.Controls.Add(folder, 0, 1);
            FlowLayoutPanel links = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(0, 2, 0, 0) };
            links.Controls.Add(Button("Official download", Blue, Color.White, delegate { OpenUrl("https://www.cgsecurity.org/wiki/TestDisk_Download"); }));
            links.Controls.Add(Button("GitHub source (GPL-2.0)", Color.White, Dark, delegate { OpenUrl("https://github.com/cgsecurity/testdisk"); }));
            links.Controls.Add(Button("Documentation", Color.White, Dark, delegate { OpenUrl("https://www.cgsecurity.org/testdisk_doc/"); }));
            layout.Controls.Add(links, 0, 2);
            FlowLayoutPanel launch = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(0, 3, 0, 0) };
            launch.Controls.Add(Button("Launch TestDisk", Blue, Color.White, delegate { LaunchTool(new[] { "testdisk_win.exe", "testdisk.exe" }, "TestDisk"); }));
            launch.Controls.Add(Button("Launch PhotoRec", Blue, Color.White, delegate { LaunchTool(new[] { "photorec_win.exe", "photorec.exe" }, "PhotoRec"); }));
            launch.Controls.Add(Button("Launch QPhotoRec", Blue, Color.White, delegate { LaunchTool(new[] { "qphotorec_win.exe", "qphotorec.exe" }, "QPhotoRec"); }));
            layout.Controls.Add(launch, 0, 3);
            layout.Controls.Add(new Label
            {
                Text = "For permanently deleted files: stop using the source disk immediately. TestDisk may undelete files when filesystem metadata remains; PhotoRec can scan for supported file signatures but may not preserve names/folders. New writes can overwrite deleted data; SSD TRIM, encryption, physical damage and unsupported formats reduce recovery chances. In TestDisk, analyze/list/copy before considering any Write action. Do not select a disk by guesswork.",
                Dock = DockStyle.Fill, ForeColor = Dark, Padding = new Padding(8, 12, 8, 8), AutoEllipsis = true
            }, 0, 4);
            return page;
        }

        private TabPage BuildBootTab()
        {
            TabPage page = MakeTab("Boot & EFI help");
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(10), BackColor = Canvas };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
            page.Controls.Add(layout);
            layout.Controls.Add(new Label
            {
                Text = "Boot diagnostics are read-only and describe the Windows installation running this app. They do not automatically repair an attached offline PC. For an unbootable Windows PC, try WinRE Startup Repair; for GRUB, use a matching Linux live USB and distribution-specific guidance.",
                Dock = DockStyle.Fill, ForeColor = Dark, Padding = new Padding(4, 4, 4, 4), AutoEllipsis = true
            }, 0, 0);
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(0, 3, 0, 0) };
            _bootChoice = new ComboBox { Width = 295, DropDownStyle = ComboBoxStyle.DropDownList, Font = Font };
            _bootChoice.Items.AddRange(new object[] { "Windows UEFI / EFI", "Windows legacy MBR", "Linux GRUB / dual boot", "RAW / damaged filesystem", "Offline Windows system files" });
            _bootChoice.SelectedIndex = 0;
            _bootChoice.SelectedIndexChanged += delegate { UpdateBootGuide(); };
            actions.Controls.Add(_bootChoice);
            actions.Controls.Add(Button("Run read-only diagnostics", Blue, Color.White, async delegate { await RunBootDiagnosticsAsync(); }));
            actions.Controls.Add(Button("Copy guide text", Color.White, Dark, delegate { try { Clipboard.SetText(_bootGuide.Text); } catch (Exception ex) { ShowError("Clipboard", ex); } }));
            layout.Controls.Add(actions, 0, 1);
            _bootGuide = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, WordWrap = true, Font = new Font("Segoe UI", 9F), BackColor = Color.White };
            layout.Controls.Add(_bootGuide, 0, 2);
            FlowLayoutPanel links = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(0, 3, 0, 0) };
            links.Controls.Add(Button("Windows recovery guide", Color.White, Dark, delegate { OpenUrl("https://support.microsoft.com/en-us/windows/experience/backup-recovery/windows-recovery-environment"); }));
            links.Controls.Add(Button("TestDisk boot guide", Color.White, Dark, delegate { OpenUrl("https://www.cgsecurity.org/testdisk_doc/repairing_filesystem.html"); }));
            links.Controls.Add(Button("Ubuntu GRUB guide", Color.White, Dark, delegate { OpenUrl("https://help.ubuntu.com/community/Boot-Repair"); }));
            links.Controls.Add(Button("Export report…", Color.White, Dark, delegate { SaveText("DriveMedic-boot-report.txt", _bootGuide.Text + Environment.NewLine + Environment.NewLine + _bootReport.Text); }));
            layout.Controls.Add(links, 0, 3);
            _bootReport = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9F), BackColor = Color.White, Text = "Run diagnostics to collect BCD, mounted-volume, recovery-environment and partition information from this Windows system." };
            layout.Controls.Add(_bootReport, 0, 4);
            UpdateBootGuide();
            return page;
        }

        private TabPage BuildExistingTab()
        {
            TabPage page = MakeTab("Disk health");
            MainForm legacy = new MainForm();
            legacy.TopLevel = false;
            legacy.FormBorderStyle = FormBorderStyle.None;
            legacy.MinimumSize = Size.Empty;
            legacy.Dock = DockStyle.Fill;
            page.Controls.Add(legacy);
            legacy.Show();
            return page;
        }

        private void AddWholeDrive()
        {
            string root = _drive.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                MessageBox.Show(this, "Select a mounted drive. For RAW/unmounted partitions use TestDisk or PhotoRec in the advanced tools tab.", "Drive selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, "Queue all visible top-level files and folders from " + root + "? This can take time and may hit protected folders. Continue only if the source disk is stable.", "Add whole drive", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            int before = _sources.Items.Count;
            try
            {
                foreach (string p in Directory.GetFiles(root)) AddSource(p);
                foreach (string p in Directory.GetDirectories(root)) AddSource(p);
                Log("Queued " + (_sources.Items.Count - before) + " top-level items from " + root + ".");
            }
            catch (Exception ex) { Log("Cannot enumerate all of " + root + ": " + ex.Message); MessageBox.Show(this, ex.Message, "Drive enumeration", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void AddFolder()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog { Description = "Choose a folder on the old disk.", ShowNewFolderButton = false })
                if (d.ShowDialog(this) == DialogResult.OK) AddSource(d.SelectedPath);
        }

        private void AddFiles()
        {
            using (OpenFileDialog d = new OpenFileDialog { Title = "Choose files to recover", Multiselect = true, CheckFileExists = true })
                if (d.ShowDialog(this) == DialogResult.OK) foreach (string p in d.FileNames) AddSource(p);
        }

        private void AddSource(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try { path = Path.GetFullPath(path); } catch { return; }
            if (!File.Exists(path) && !Directory.Exists(path)) { Log("Skipped missing path: " + path); return; }
            foreach (object item in _sources.Items)
                if (string.Equals(Convert.ToString(item), path, StringComparison.OrdinalIgnoreCase)) return;
            _sources.Items.Add(path);
        }

        private void RemoveSelected()
        {
            List<object> chosen = _sources.SelectedItems.Cast<object>().ToList();
            foreach (object item in chosen) _sources.Items.Remove(item);
        }

        private void RefreshDrives()
        {
            if (_drive == null) return;
            string old = _drive.SelectedItem as string;
            _drive.Items.Clear();
            foreach (DriveInfo d in DriveInfo.GetDrives())
                try { if (d.IsReady && d.DriveType != DriveType.CDRom) _drive.Items.Add(d.Name); } catch { }
            if (!string.IsNullOrEmpty(old) && _drive.Items.Contains(old)) _drive.SelectedItem = old;
            else if (_drive.Items.Count > 0) _drive.SelectedIndex = 0;
        }

        private void ChooseDestination()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog { Description = "Choose a folder on a different physical disk.", ShowNewFolderButton = true })
            {
                if (Directory.Exists(_destination.Text)) d.SelectedPath = _destination.Text;
                if (d.ShowDialog(this) == DialogResult.OK) _destination.Text = d.SelectedPath;
            }
        }

        private async Task CopyAsync()
        {
            List<string> paths = _sources.Items.Cast<object>().Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            string destination = _destination.Text.Trim();
            if (paths.Count == 0) { MessageBox.Show(this, "Add files or folders first.", "Recovery copy", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (string.IsNullOrWhiteSpace(destination)) { MessageBox.Show(this, "Choose a destination on another disk.", "Recovery copy", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            try { destination = Path.GetFullPath(destination); } catch (Exception ex) { ShowError("Destination", ex); return; }

            IList<DriveRecord> inventory = new List<DriveRecord>();
            try { inventory = new DriveInventoryService().GetVolumes(); } catch (Exception ex) { Log("Physical-disk mapping unavailable: " + ex.Message); }
            int? destDisk = DiskForPath(Path.GetPathRoot(destination), inventory);
            int samePhysical = paths.Count(p => destDisk.HasValue && DiskForPath(Path.GetPathRoot(p), inventory) == destDisk);
            int sameVolume = paths.Count(p => string.Equals(Path.GetPathRoot(p), Path.GetPathRoot(destination), StringComparison.OrdinalIgnoreCase));
            if (samePhysical > 0 && MessageBox.Show(this, samePhysical + " source item(s) map to the same physical disk as the destination. A different partition on that disk is not a safe backup. Continue anyway?", "Same physical disk", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            else if (sameVolume > 0 && MessageBox.Show(this, "The destination is on the same volume as one or more sources. This is not a safe backup. Continue anyway?", "Same volume", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            else if (!destDisk.HasValue) Log("Warning: destination physical disk could not be confirmed. Please verify it manually.");

            if (MessageBox.Show(this, "Copy " + paths.Count + " item(s) to " + destination + "? The source will not be intentionally changed. Existing destination names will be preserved; copies use a recovered suffix. SHA-256 verification: " + (_verify.Checked ? "on" : "off") + ".", "Confirm recovery copy", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            _copyButton.Enabled = false;
            Cursor = Cursors.WaitCursor;
            try
            {
                bool verify = _verify.Checked;
                CopyResult result = await Task.Run(delegate { return _recovery.CopyPaths(paths, destination, verify); });
                Log("Finished. Files copied: " + result.CopiedFiles + "; SHA-256 verified: " + result.FilesVerified + "; bytes: " + result.BytesCopied + "; errors: " + result.FailedFiles + "; skipped links: " + result.SkippedReparsePoints + ".");
                foreach (string message in result.Messages.Take(1000)) Log(message);
                MessageBox.Show(this, "Files copied: " + result.CopiedFiles + "\r\nVerified: " + result.FilesVerified + "\r\nBytes: " + result.BytesCopied + "\r\nErrors: " + result.FailedFiles + "\r\nSkipped links: " + result.SkippedReparsePoints + "\r\n\r\nA verified copy confirms the destination matches the bytes read from the source; it cannot prove the source content was healthy.", "Recovery finished", MessageBoxButtons.OK, result.FailedFiles > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            catch (Exception ex) { Log("Copy failed: " + ex.Message); ShowError("Copy failed", ex); }
            finally { Cursor = Cursors.Default; _copyButton.Enabled = true; }
        }

        private static int? DiskForPath(string path, IList<DriveRecord> inventory)
        {
            if (string.IsNullOrWhiteSpace(path) || inventory == null) return null;
            string normalized = path.TrimEnd('\\');
            DriveRecord r = inventory.FirstOrDefault(x => x != null && !string.IsNullOrWhiteSpace(x.RootPath) && string.Equals(x.RootPath.TrimEnd('\\'), normalized, StringComparison.OrdinalIgnoreCase));
            return r == null ? (int?)null : r.PhysicalDiskIndex;
        }

        private void ChooseFile()
        {
            using (OpenFileDialog d = new OpenFileDialog { Title = "Select a file on the source disk", CheckFileExists = true })
                if (d.ShowDialog(this) == DialogResult.OK) _filePath.Text = d.FileName;
        }

        private async Task InspectFileAsync()
        {
            string path = _filePath.Text.Trim();
            if (!File.Exists(path)) { MessageBox.Show(this, "Choose an existing file.", "Inspect file", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (!_confirmHash.Checked) { MessageBox.Show(this, "A full-file hash reads every byte and may stress a failing disk. Check the acknowledgement or recover critical files first.", "Full-read acknowledgement", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            Cursor = Cursors.WaitCursor;
            _fileReport.Text = "Reading file and calculating SHA-256…";
            try
            {
                _fileReport.Text = await Task.Run(delegate
                {
                    FileInfo info = new FileInfo(path);
                    byte[] hash;
                    using (SHA256 sha = SHA256.Create())
                    using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1024 * 1024, FileOptions.SequentialScan)) hash = sha.ComputeHash(stream);
                    return "READ COMPLETED\r\nPath: " + info.FullName + "\r\nSize: " + info.Length + " bytes\r\nLast modified (UTC): " + info.LastWriteTimeUtc.ToString("u") + "\r\nSHA-256: " + BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant() + "\r\n\r\nThis confirms the bytes were readable during this scan. Without a trusted reference hash or file-format validator, it does not prove the content is correct.";
                });
            }
            catch (Exception ex) { _fileReport.Text = "READ FAILED\r\n" + path + "\r\n" + ex.Message + "\r\n\r\nAvoid repeated scans on failing media. Use image-first or specialist recovery if data is valuable."; }
            finally { Cursor = Cursors.Default; }
        }

        private void QueueFile()
        {
            string path = _filePath.Text.Trim();
            if (!File.Exists(path)) { MessageBox.Show(this, "Choose an existing file.", "Queue file", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            AddSource(path);
            _tabs.SelectedIndex = 0;
            MessageBox.Show(this, "The file is queued. Select a different physical disk as the destination and start the copy.", "File queued", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ChooseToolsFolder()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog { Description = "Choose the folder where the official TestDisk/PhotoRec ZIP was fully extracted.", ShowNewFolderButton = false })
            {
                if (Directory.Exists(_toolsFolder.Text)) d.SelectedPath = _toolsFolder.Text;
                if (d.ShowDialog(this) == DialogResult.OK) _toolsFolder.Text = d.SelectedPath;
            }
        }

        private void LaunchTool(string[] names, string display)
        {
            string exe = null;
            string folder = _toolsFolder.Text.Trim();
            if (Directory.Exists(folder))
                foreach (string name in names)
                {
                    string candidate = Path.Combine(folder, name);
                    if (File.Exists(candidate)) { exe = candidate; break; }
                }
            if (exe == null)
            {
                if (MessageBox.Show(this, display + " was not found. Download and fully extract the official TestDisk & PhotoRec portable ZIP, then select its folder. Open the official download page?", "Tool not found", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    OpenUrl("https://www.cgsecurity.org/wiki/TestDisk_Download");
                return;
            }
            string caution = display + " can access raw disks and some options can change disk data. Identify the source by model and size, recover output to another physical disk, and inspect before choosing any write/repair option. Launch elevated for low-level disk access?";
            if (MessageBox.Show(this, caution, "Confirm advanced recovery", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;
            try { Process.Start(new ProcessStartInfo { FileName = exe, WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = true, Verb = "runas" }); }
            catch (Exception ex) { ShowError("Could not launch " + display, ex); }
        }

        private void UpdateBootGuide()
        {
            if (_bootGuide == null || _bootChoice == null || _bootChoice.SelectedItem == null) return;
            string choice = Convert.ToString(_bootChoice.SelectedItem);
            if (choice == "Windows UEFI / EFI")
                _bootGuide.Text = "WINDOWS UEFI / EFI — GUIDED TEMPLATE, NOT RUN AUTOMATICALLY\r\n\r\n1. Boot the affected PC from Windows recovery/installation media and choose Repair your computer > Troubleshoot > Advanced options > Startup Repair.\r\n2. If manual repair is required, open WinRE Command Prompt. Confirm the Windows volume by testing dir C:\\Windows, dir D:\\Windows, etc.; letters may change in WinRE.\r\n3. Use diskpart and list vol to identify the correct EFI System Partition. It is commonly a small FAT32 volume. Confirm the disk before assigning a temporary letter.\r\n4. Only after identifying the correct Windows and EFI volumes, the common template is: bcdboot <WindowsLetter>:\\Windows /s <EfiLetter>: /f UEFI. Replace placeholders with verified letters. This writes boot files to the selected EFI partition.\r\n5. Have the BitLocker recovery key available. Do not format EFI or run commands against an unknown disk. Multi-boot setups may require distribution-specific steps.\r\n\r\nOfficial guide: https://support.microsoft.com/en-us/windows/experience/backup-recovery/windows-recovery-environment";
            else if (choice == "Windows legacy MBR")
                _bootGuide.Text = "WINDOWS LEGACY BIOS / MBR\r\n\r\nTry WinRE Startup Repair first. Confirm the installation uses legacy BIOS/MBR, not UEFI/GPT. Commands such as bootrec /fixmbr write boot code and bootrec /rebuildbcd changes the boot configuration; these are not universal fixes and can be inappropriate for UEFI or Linux dual-boot systems. Back up data and positively identify the target disk before any write operation.\r\n\r\nGuide: https://support.microsoft.com/en-us/windows/experience/backup-recovery/windows-recovery-environment";
            else if (choice == "Linux GRUB / dual boot")
                _bootGuide.Text = "LINUX GRUB / DUAL BOOT\r\n\r\nDo not run Windows bootrec/bcdboot expecting them to repair GRUB. Boot a live USB for the same Linux distribution, identify the root, /boot (if separate), and EFI partitions by UUID/size, then follow that distribution's current GRUB reinstall steps. If Windows and Linux share an EFI partition, back it up before changes. Commands vary for encrypted, LVM, RAID, Secure Boot, and different distributions.\r\n\r\nUbuntu guide: https://help.ubuntu.com/community/Boot-Repair\r\nTestDisk docs: https://www.cgsecurity.org/testdisk_doc/";
            else if (choice == "RAW / damaged filesystem")
                _bootGuide.Text = "RAW / DAMAGED FILESYSTEM\r\n\r\nDo not initialize, format, or run write-mode CHKDSK on a source disk when data recovery is the priority. Hardware warnings, clicks, disconnections or stalls are reasons to minimize reads and consider imaging or professional recovery. TestDisk can analyze partitions/filesystems; PhotoRec can carve supported file types. Always save recovered files to a different physical disk. Overwritten or physically unreadable sectors cannot be recreated by software.\r\n\r\nTestDisk docs: https://www.cgsecurity.org/testdisk_doc/";
            else
                _bootGuide.Text = "OFFLINE WINDOWS SYSTEM FILES\r\n\r\nSFC launched in the running Windows normally targets that installation, not an old Windows folder on an attached disk. For offline repair, boot WinRE/installation media, identify the correct Windows directory, and follow the supported DISM/SFC offline steps for that Windows version. Do not download random DLLs or replace files with binaries from another build. Generic one-click repair of arbitrary file types is not possible.";
        }

        private async Task RunBootDiagnosticsAsync()
        {
            _bootReport.Text = "Collecting read-only diagnostics for the currently running Windows…";
            Cursor = Cursors.WaitCursor;
            try
            {
                _bootReport.Text = await Task.Run(delegate
                {
                    StringBuilder b = new StringBuilder();
                    b.AppendLine("DriveMedic read-only boot report");
                    b.AppendLine("Created: " + DateTime.Now.ToString("u"));
                    b.AppendLine("Computer: " + Environment.MachineName);
                    b.AppendLine("Windows directory: " + Environment.GetFolderPath(Environment.SpecialFolder.Windows));
                    b.AppendLine("NOTE: This describes the running Windows installation. It does not repair an attached old disk.");
                    b.AppendLine();
                    RunCommand(b, "bcdedit /enum all");
                    RunCommand(b, "mountvol");
                    RunCommand(b, "reagentc /info");
                    RunCommand(b, "wmic partition get DiskIndex,Index,Size,Type,BootPartition /format:list");
                    return b.ToString();
                });
            }
            catch (Exception ex) { _bootReport.Text = "Diagnostics failed: " + ex.Message; }
            finally { Cursor = Cursors.Default; }
        }

        private static void RunCommand(StringBuilder b, string command)
        {
            b.AppendLine("===== " + command + " =====");
            try
            {
                ProcessStartInfo info = new ProcessStartInfo { FileName = "cmd.exe", Arguments = "/d /s /c \"" + command + " 2>&1\"", UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
                using (Process p = Process.Start(info))
                {
                    if (p == null) { b.AppendLine("Could not start command."); return; }
                    string output = p.StandardOutput.ReadToEnd();
                    if (!p.WaitForExit(20000)) { try { p.Kill(); } catch { } b.AppendLine("Timed out after 20 seconds."); }
                    else b.AppendLine(output);
                }
            }
            catch (Exception ex) { b.AppendLine("Command unavailable: " + ex.Message); }
            b.AppendLine();
        }

        private void SaveText(string filename, string content)
        {
            using (SaveFileDialog d = new SaveFileDialog { FileName = filename, Filter = "Text report (*.txt)|*.txt|All files (*.*)|*.*" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { File.WriteAllText(d.FileName, content ?? string.Empty, new UTF8Encoding(false)); }
                catch (Exception ex) { ShowError("Save report", ex); }
            }
        }

        private void Log(string text)
        {
            if (_copyLog != null) _copyLog.AppendText(Environment.NewLine + "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text);
        }

        private void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
            catch (Exception ex) { ShowError("Open link", ex); }
        }

        private void ShowError(string title, Exception ex)
        {
            MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static TabPage MakeTab(string text)
        {
            return new TabPage(text) { BackColor = Canvas, ForeColor = Dark };
        }

        private static Button Button(string title, Color back, Color fore, Action action)
        {
            Button button = new Button
            {
                Text = title, BackColor = back, ForeColor = fore, FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold), Height = 36,
                AutoSize = true, Padding = new Padding(10, 4, 10, 4), Margin = new Padding(4), Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(205, 216, 229);
            button.FlatAppearance.BorderSize = back == Blue ? 0 : 1;
            button.FlatAppearance.MouseOverBackColor = back == Blue ? Color.FromArgb(27, 88, 151) : Color.FromArgb(230, 238, 247);
            button.FlatAppearance.MouseDownBackColor = back == Blue ? Color.FromArgb(19, 67, 117) : Color.FromArgb(216, 228, 241);
            button.Click += delegate { if (action != null) action(); };
            return button;
        }
    }
}