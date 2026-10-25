using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualBasic.FileIO;
using DriveMedic.Core;
using DriveMedic.Models;

namespace DriveMedic
{
    /// <summary>Short, guided file browser for mounted drives and recovered-output folders.</summary>
    public sealed class RecoveryBrowserControl : UserControl
    {
        public event Action RecoverDeletedFilesRequested;

        private readonly FileBrowseService _browser = new FileBrowseService();
        private readonly RecoveryService _recovery = new RecoveryService();
        private readonly ComboBox _drive = new ComboBox();
        private readonly ComboBox _mode = new ComboBox();
        private readonly TextBox _search = new TextBox();
        private readonly TextBox _destination = new TextBox();
        private readonly Label _sourceLabel = new Label();
        private readonly Label _status = new Label();
        private readonly ProgressBar _progress = new ProgressBar();
        private readonly DataGridView _grid = new DataGridView();
        private readonly Button _scanButton = new Button();
        private readonly Button _browseFolderButton = new Button();
        private readonly Button _copySelectedButton = new Button();
        private readonly Button _copyAllButton = new Button();
        private readonly Button _moveSelectedButton = new Button();
        private readonly Button _moveAllButton = new Button();
        private readonly Button _deleteSelectedButton = new Button();
        private readonly Button _selectAllButton = new Button();

        private readonly Color _blue = Color.FromArgb(35, 105, 180);
        private readonly Color _canvas = Color.FromArgb(244, 247, 251);
        private readonly Color _ink = Color.FromArgb(35, 47, 62);
        private readonly Color _muted = Color.FromArgb(103, 117, 135);

        private List<FileBrowseEntry> _allResults = new List<FileBrowseEntry>();
        private List<FileBrowseEntry> _visibleResults = new List<FileBrowseEntry>();
        private CancellationTokenSource _scanCancellation;
        private string _folderOverride;
        private string _activeRoot;
        private FileBrowseMode _lastScanMode = FileBrowseMode.AllFiles;

        public RecoveryBrowserControl()
        {
            Dock = DockStyle.Fill;
            BackColor = _canvas;
            Font = new Font("Segoe UI", 9F);
            BuildLayout();
            RefreshDrives();
        }

        private void BuildLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7,
                Padding = new Padding(10), BackColor = _canvas
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            Controls.Add(root);

            Panel intro = new Panel { Dock = DockStyle.Fill, BackColor = _canvas, Margin = new Padding(2) };
            intro.Controls.Add(new Label
            {
                Text = "Find files in 3 steps",
                Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold),
                ForeColor = _ink, AutoSize = true, Location = new Point(4, 0)
            });
            intro.Controls.Add(new Label
            {
                Text = "Select the old disk → Scan a category → Copy, move or send selected files to Recycle Bin.",
                Font = new Font("Segoe UI", 9F), ForeColor = _muted,
                AutoSize = true, Location = new Point(6, 28)
            });
            root.Controls.Add(intro, 0, 0);

            FlowLayoutPanel sourceRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 3, 0, 0)
            };
            sourceRow.Controls.Add(MakeLabel("Source disk"));
            _drive.Width = 260;
            _drive.DropDownStyle = ComboBoxStyle.DropDownList;
            _drive.SelectedIndexChanged += delegate
            {
                if (_drive.SelectedItem != null)
                {
                    _folderOverride = null;
                    _sourceLabel.Text = ((DriveOption)_drive.SelectedItem).Root;
                    ClearResults("Disk selected. Click Scan to list files.");
                }
            };
            sourceRow.Controls.Add(_drive);
            sourceRow.Controls.Add(MakeButton("Refresh disks", Color.White, _ink, RefreshDrives));
            _browseFolderButton.Text = "Choose folder…";
            StyleButton(_browseFolderButton, Color.White, _ink);
            _browseFolderButton.Click += delegate { ChooseSourceFolder(); };
            sourceRow.Controls.Add(_browseFolderButton);
            _sourceLabel.AutoSize = true;
            _sourceLabel.ForeColor = _muted;
            _sourceLabel.Padding = new Padding(4, 7, 4, 0);
            sourceRow.Controls.Add(_sourceLabel);
            _scanButton.Text = "Scan";
            StyleButton(_scanButton, _blue, Color.White);
            _scanButton.Click += async delegate { await ScanAsync(); };
            sourceRow.Controls.Add(_scanButton);
            root.Controls.Add(sourceRow, 0, 1);

            FlowLayoutPanel filterRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 3, 0, 0)
            };
            filterRow.Controls.Add(MakeLabel("Show"));
            _mode.DropDownStyle = ComboBoxStyle.DropDownList;
            _mode.Width = 260;
            _mode.Items.AddRange(new object[]
            {
                "All files (including hidden)",
                "Hidden & system files only",
                "Downloads folder",
                "Applications & installers",
                "Documents",
                "Photos & videos",
                "Changed in the last 30 days"
            });
            _mode.SelectedIndex = 0;
            _mode.SelectedIndexChanged += delegate
            {
                if (_activeRoot != null)
                    ClearResults("Category changed. Click Scan to search this category.");
            };
            filterRow.Controls.Add(_mode);
            filterRow.Controls.Add(MakeLabel("Search name/path"));
            _search.Width = 370;
            _search.Font = Font;
            _search.Margin = new Padding(3, 5, 3, 3);
            _search.TextChanged += delegate { ApplySearchFilter(); };
            filterRow.Controls.Add(_search);
            Button recoverDeleted = MakeButton("Recover deleted files (GUI)", Color.FromArgb(27, 126, 105), Color.White,
                delegate { RaiseRecoverDeleted(); });
            filterRow.Controls.Add(recoverDeleted);
            root.Controls.Add(filterRow, 0, 2);

            FlowLayoutPanel destinationRow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 3, 0, 0)
            };
            destinationRow.Controls.Add(MakeLabel("Copy/move destination"));
            _destination.Width = 520;
            _destination.Font = Font;
            _destination.Margin = new Padding(3, 5, 3, 3);
            destinationRow.Controls.Add(_destination);
            destinationRow.Controls.Add(MakeButton("Choose destination…", Color.White, _ink, ChooseDestination));
            destinationRow.Controls.Add(MakeButton("Browse recovered output…", Color.White, _ink, ChooseRecoveredOutput));
            root.Controls.Add(destinationRow, 0, 3);

            ConfigureGrid();
            root.Controls.Add(_grid, 0, 4);

            FlowLayoutPanel actions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(0, 3, 0, 0)
            };
            _selectAllButton.Text = "Select all shown";
            StyleButton(_selectAllButton, Color.White, _ink);
            _selectAllButton.Click += delegate { SetVisibleSelection(true); };
            actions.Controls.Add(_selectAllButton);
            actions.Controls.Add(MakeButton("Clear selection", Color.White, _ink, delegate { SetVisibleSelection(false); }));
            _copySelectedButton.Text = "Copy selected";
            StyleButton(_copySelectedButton, _blue, Color.White);
            _copySelectedButton.Click += async delegate { await CopyAsync(false); };
            actions.Controls.Add(_copySelectedButton);
            _copyAllButton.Text = "Copy all shown";
            StyleButton(_copyAllButton, _blue, Color.White);
            _copyAllButton.Click += async delegate { await CopyAsync(true); };
            actions.Controls.Add(_copyAllButton);
            _moveSelectedButton.Text = "Move selected";
            StyleButton(_moveSelectedButton, Color.FromArgb(27, 126, 105), Color.White);
            _moveSelectedButton.Click += async delegate { await MoveAsync(false); };
            actions.Controls.Add(_moveSelectedButton);
            _moveAllButton.Text = "Move all shown";
            StyleButton(_moveAllButton, Color.FromArgb(27, 126, 105), Color.White);
            _moveAllButton.Click += async delegate { await MoveAsync(true); };
            actions.Controls.Add(_moveAllButton);
            _deleteSelectedButton.Text = "Delete selected";
            StyleButton(_deleteSelectedButton, Color.FromArgb(170, 55, 55), Color.White);
            _deleteSelectedButton.Click += async delegate { await DeleteAsync(); };
            actions.Controls.Add(_deleteSelectedButton);
            root.Controls.Add(actions, 0, 5);

            TableLayoutPanel statusRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = _canvas };
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 76F));
            statusRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 24F));
            _status.Text = "Ready. Hidden files are included in All files; use the filter to show only hidden files or application downloads.";
            _status.ForeColor = _muted;
            _status.AutoEllipsis = true;
            _status.Dock = DockStyle.Fill;
            statusRow.Controls.Add(_status, 0, 0);
            _progress.Dock = DockStyle.Fill;
            _progress.Visible = false;
            _progress.Style = ProgressBarStyle.Marquee;
            statusRow.Controls.Add(_progress, 1, 0);
            root.Controls.Add(statusRow, 0, 6);
        }

        private void ConfigureGrid()
        {
            _grid.Dock = DockStyle.Fill;
            _grid.BackgroundColor = Color.White;
            _grid.BorderStyle = BorderStyle.FixedSingle;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.AutoGenerateColumns = false;
            _grid.RowHeadersVisible = false;
            _grid.MultiSelect = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.EditMode = DataGridViewEditMode.EditOnEnter;
            _grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                HeaderText = "✓", DataPropertyName = "Selected", Width = 36,
                Name = "Selected", ReadOnly = false, SortMode = DataGridViewColumnSortMode.NotSortable
            });
            _grid.Columns.Add(TextColumn("Name", "Name", 210));
            _grid.Columns.Add(TextColumn("Type", "TypeText", 78));
            _grid.Columns.Add(TextColumn("Size", "SizeText", 95));
            _grid.Columns.Add(TextColumn("Modified", "ModifiedText", 132));
            _grid.Columns.Add(TextColumn("Flags", "FlagsText", 105));
            _grid.Columns.Add(TextColumn("Path", "FullPath", 500));
            _grid.CellValueChanged += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == 0) UpdateSelectionStatus();
            };
            _grid.CurrentCellDirtyStateChanged += delegate
            {
                if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _grid.DataError += delegate { };
            _grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0)
                {
                    FileBrowseEntry entry = _grid.Rows[e.RowIndex].DataBoundItem as FileBrowseEntry;
                    if (entry != null) OpenContainingFolder(entry);
                }
            };
        }

        private static DataGridViewTextBoxColumn TextColumn(string header, string property, int width)
        {
            return new DataGridViewTextBoxColumn
            {
                HeaderText = header, DataPropertyName = property, Name = property,
                Width = width, ReadOnly = true, SortMode = DataGridViewColumnSortMode.Automatic
            };
        }

        private void RefreshDrives()
        {
            string previous = _drive.SelectedItem is DriveOption ? ((DriveOption)_drive.SelectedItem).Root : null;
            _drive.Items.Clear();
            foreach (DriveInfo info in DriveInfo.GetDrives())
            {
                try
                {
                    if (!info.IsReady || info.DriveType == DriveType.CDRom) continue;
                    string label = string.IsNullOrWhiteSpace(info.VolumeLabel) ? "Local volume" : info.VolumeLabel;
                    string description = info.Name + "  •  " + label + "  •  " + DriveRecord.FormatBytes(info.TotalSize);
                    _drive.Items.Add(new DriveOption(info.Name, description));
                }
                catch { }
            }

            if (_drive.Items.Count == 0)
            {
                _sourceLabel.Text = "No mounted drives detected";
                ClearResults("Connect the old HDD/SSD and click Refresh disks.");
                return;
            }

            int choice = -1;
            if (!string.IsNullOrEmpty(previous))
                for (int i = 0; i < _drive.Items.Count; i++)
                    if (string.Equals(((DriveOption)_drive.Items[i]).Root, previous, StringComparison.OrdinalIgnoreCase)) { choice = i; break; }

            if (choice < 0)
            {
                string systemRoot = Environment.GetEnvironmentVariable("SystemDrive");
                for (int i = 0; i < _drive.Items.Count; i++)
                    if (!string.Equals(((DriveOption)_drive.Items[i]).Root.TrimEnd('\\'), (systemRoot ?? "C:").TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    { choice = i; break; }
            }
            _drive.SelectedIndex = choice >= 0 ? choice : 0;
        }

        private void ChooseSourceFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog
            {
                Description = "Choose a folder on the old disk, or a folder where QPhotoRec saved recovered files.",
                ShowNewFolderButton = false
            })
            {
                if (!string.IsNullOrEmpty(_folderOverride) && Directory.Exists(_folderOverride))
                    dialog.SelectedPath = _folderOverride;
                if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    _folderOverride = Path.GetFullPath(dialog.SelectedPath);
                    _sourceLabel.Text = _folderOverride;
                    ClearResults("Folder selected. Click Scan.");
                }
            }
        }

        private void ChooseDestination()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog
            {
                Description = "Choose a destination on a different physical disk.",
                ShowNewFolderButton = true
            })
            {
                if (Directory.Exists(_destination.Text.Trim())) dialog.SelectedPath = _destination.Text.Trim();
                if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
                    _destination.Text = Path.GetFullPath(dialog.SelectedPath);
            }
        }

        private async void ChooseRecoveredOutput()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog
            {
                Description = "Select the output folder QPhotoRec recovered files into (often named recup_dir.1 or a parent folder).",
                ShowNewFolderButton = false
            })
            {
                if (Directory.Exists(_destination.Text.Trim())) dialog.SelectedPath = _destination.Text.Trim();
                if (dialog.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    _folderOverride = Path.GetFullPath(dialog.SelectedPath);
                    _sourceLabel.Text = _folderOverride;
                    _mode.SelectedIndex = 0;
                    _search.Clear();
                    ClearResults("Recovered-output folder selected. Click Scan to list recovered files.");
                    await ScanAsync();
                }
            }
        }

        private string ActiveRoot()
        {
            if (!string.IsNullOrWhiteSpace(_folderOverride) && Directory.Exists(_folderOverride))
                return Path.GetFullPath(_folderOverride);
            DriveOption selected = _drive.SelectedItem as DriveOption;
            return selected == null ? null : selected.Root;
        }

        private FileBrowseMode SelectedMode()
        {
            switch (_mode.SelectedIndex)
            {
                case 1: return FileBrowseMode.HiddenAndSystem;
                case 2: return FileBrowseMode.Downloads;
                case 3: return FileBrowseMode.ApplicationsAndInstallers;
                case 4: return FileBrowseMode.Documents;
                case 5: return FileBrowseMode.PhotosAndVideos;
                case 6: return FileBrowseMode.RecentlyModified;
                default: return FileBrowseMode.AllFiles;
            }
        }

        private async Task ScanAsync()
        {
            if (_scanCancellation != null)
            {
                _scanCancellation.Cancel();
                _status.Text = "Cancel requested. Finishing the current folder…";
                return;
            }

            string root = ActiveRoot();
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                MessageBox.Show(FindForm(), "Select a mounted source disk or choose a folder first.", "Source required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _activeRoot = Path.GetFullPath(root);
            _lastScanMode = SelectedMode();
            _scanCancellation = new CancellationTokenSource();
            CancellationToken token = _scanCancellation.Token;
            _scanButton.Text = "Cancel scan";
            _progress.Visible = true;
            _progress.Style = ProgressBarStyle.Marquee;
            _mode.Enabled = false;
            _drive.Enabled = false;
            _browseFolderButton.Enabled = false;
            _status.Text = "Scanning " + _activeRoot + "…";

            try
            {
                IProgress<FileBrowseProgress> progress = new Progress<FileBrowseProgress>(p =>
                {
                    _status.Text = "Scanned " + p.DirectoriesVisited + " folders, " +
                        p.FilesVisited + " files; matched " + p.MatchingFiles + ".";
                });
                FileBrowseScanResult result = await Task.Run(
                    () => _browser.Scan(_activeRoot, _lastScanMode, token, progress), token);
                _allResults = result.Files;
                ApplySearchFilter();
                _status.Text = "Found " + result.Files.Count + " files. Skipped " +
                    result.SkippedReparsePoints + " links; " + result.AccessErrors + " access/read errors.";
                if (result.LimitReached)
                    _status.Text += " Results capped at " + FileBrowseService.MaximumResults.ToString("N0") +
                        "; choose a narrower folder or category.";
            }
            catch (OperationCanceledException)
            {
                _status.Text = "Scan cancelled. Found " + _allResults.Count + " files before cancellation.";
            }
            catch (Exception ex)
            {
                _status.Text = "Scan failed: " + ex.Message;
                MessageBox.Show(FindForm(), ex.Message, "Scan failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                _scanCancellation.Dispose();
                _scanCancellation = null;
                _scanButton.Text = "Scan";
                _progress.Visible = false;
                _mode.Enabled = true;
                _drive.Enabled = true;
                _browseFolderButton.Enabled = true;
            }
        }

        private void ApplySearchFilter()
        {
            string query = (_search.Text ?? string.Empty).Trim();
            IEnumerable<FileBrowseEntry> matches = _allResults;
            if (query.Length > 0)
                matches = matches.Where(item =>
                    item.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    item.FullPath.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

            _visibleResults = matches.ToList();
            _grid.DataSource = null;
            _grid.DataSource = new BindingList<FileBrowseEntry>(_visibleResults);
            UpdateSelectionStatus();
        }

        private void ClearResults(string status)
        {
            _allResults.Clear();
            _visibleResults.Clear();
            _grid.DataSource = null;
            _grid.DataSource = new BindingList<FileBrowseEntry>(_visibleResults);
            _status.Text = status;
        }

        private void SetVisibleSelection(bool selected)
        {
            foreach (FileBrowseEntry entry in _visibleResults) entry.Selected = selected;
            ApplySearchFilter();
        }

        private void UpdateSelectionStatus()
        {
            int selected = _visibleResults.Count(item => item.Selected);
            if (selected > 0)
                _status.Text = selected + " file(s) checked. Double-click a row to open its containing folder.";
        }

        private List<FileBrowseEntry> GetEntries(bool allVisible)
        {
            if (allVisible) return _visibleResults.ToList();
            _grid.EndEdit();
            List<FileBrowseEntry> checkedEntries = _visibleResults.Where(item => item.Selected).ToList();
            if (checkedEntries.Count > 0) return checkedEntries;
            return _grid.SelectedRows.Cast<DataGridViewRow>()
                .Select(row => row.DataBoundItem as FileBrowseEntry)
                .Where(item => item != null)
                .Distinct()
                .ToList();
        }

        private async Task CopyAsync(bool allVisible)
        {
            List<FileBrowseEntry> entries = GetEntries(allVisible);
            if (!ValidateEntries(entries)) return;
            string destination;
            if (!ValidateDestination(out destination)) return;
            if (!ConfirmDestinationIsOtherDisk(destination, "copy recovered files")) return;

            DialogResult confirm = MessageBox.Show(
                FindForm(),
                "Copy " + entries.Count + " file(s) to:\r\n" + destination +
                "\r\n\r\nFolder paths will be preserved, existing destination files will not be overwritten, and copies will be SHA-256 verified. The source files stay in place. Continue?",
                "Confirm copy", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes) return;

            SetBusy(true, "Copying and verifying files…");
            try
            {
                Progress<int> progress = new Progress<int>(count => _status.Text = "Copied " + count + " of " + entries.Count + " files…");
                FileOperationSummary result = await Task.Run(() =>
                    CopyEntries(entries, _activeRoot, destination, CancellationToken.None, progress));
                ShowOperationSummary("Copy complete", result, "Copied", entries.Count);
            }
            catch (Exception ex) { MessageBox.Show(FindForm(), ex.Message, "Copy failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { SetBusy(false, "Ready."); }
        }

        private async Task MoveAsync(bool allVisible)
        {
            List<FileBrowseEntry> entries = GetEntries(allVisible);
            if (!ValidateEntries(entries)) return;
            string destination;
            if (!ValidateDestination(out destination)) return;
            if (!ConfirmDestinationIsOtherDisk(destination, "move files")) return;

            DialogResult confirm = MessageBox.Show(
                FindForm(),
                "Move " + entries.Count + " file(s) to:\r\n" + destination +
                "\r\n\r\nDriveMedic first creates and verifies a copy on the destination, then deletes the source file. This changes the old disk. Do this only when the source is stable and your important data is backed up. Continue?",
                "Confirm move", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes) return;

            SetBusy(true, "Moving files with copy verification…");
            try
            {
                Progress<int> progress = new Progress<int>(count => _status.Text = "Processed " + count + " of " + entries.Count + " files…");
                FileOperationSummary result = await Task.Run(() =>
                    MoveEntries(entries, _activeRoot, destination, CancellationToken.None, progress));
                ShowOperationSummary("Move complete", result, "Moved", entries.Count);
                RemoveProcessedSources(result.ProcessedPaths);
            }
            catch (Exception ex) { MessageBox.Show(FindForm(), ex.Message, "Move failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { SetBusy(false, "Ready."); }
        }

        private async Task DeleteAsync()
        {
            List<FileBrowseEntry> entries = GetEntries(false);
            if (!ValidateEntries(entries)) return;
            DialogResult confirm = MessageBox.Show(
                FindForm(),
                "Send " + entries.Count + " selected file(s) to the Windows Recycle Bin where supported?\r\n\r\nThis modifies the selected disk. Do not do this on a failing drive or before recovery is complete.",
                "Confirm delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
            if (confirm != DialogResult.Yes) return;

            SetBusy(true, "Sending files to Recycle Bin…");
            try
            {
                FileOperationSummary result = await Task.Run(() => DeleteEntries(entries));
                ShowOperationSummary("Delete complete", result, "Sent to Recycle Bin", entries.Count);
                RemoveProcessedSources(result.ProcessedPaths);
            }
            catch (Exception ex) { MessageBox.Show(FindForm(), ex.Message, "Delete failed", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            finally { SetBusy(false, "Ready."); }
        }

        private FileOperationSummary CopyEntries(
            IList<FileBrowseEntry> entries, string sourceRoot, string destination,
            CancellationToken token, IProgress<int> progress)
        {
            FileOperationSummary summary = new FileOperationSummary();
            int completed = 0;
            foreach (FileBrowseEntry entry in entries)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    string relativePath = MakeRelativePath(sourceRoot, entry.FullPath);
                    string relativeDirectory = Path.GetDirectoryName(relativePath);
                    string targetFolder = string.IsNullOrEmpty(relativeDirectory)
                        ? destination : Path.Combine(destination, relativeDirectory);
                    Directory.CreateDirectory(targetFolder);
                    CopyResult copied = _recovery.CopyPaths(new[] { entry.FullPath }, targetFolder, true);
                    summary.Completed += copied.CopiedFiles;
                    summary.Failed += copied.FailedFiles;
                    summary.Verified += copied.FilesVerified;
                    summary.Bytes += copied.BytesCopied;
                    summary.Messages.AddRange(copied.Messages.Take(50));
                    if (copied.CopiedFiles > 0) summary.ProcessedPaths.Add(entry.FullPath);
                }
                catch (Exception ex)
                {
                    summary.Failed++;
                    summary.Messages.Add(entry.FullPath + ": " + ex.Message);
                }
                completed++;
                if (progress != null) progress.Report(completed);
            }
            return summary;
        }

        private FileOperationSummary MoveEntries(
            IList<FileBrowseEntry> entries, string sourceRoot, string destination,
            CancellationToken token, IProgress<int> progress)
        {
            FileOperationSummary summary = new FileOperationSummary();
            int completed = 0;
            foreach (FileBrowseEntry entry in entries)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    string relativePath = MakeRelativePath(sourceRoot, entry.FullPath);
                    string relativeDirectory = Path.GetDirectoryName(relativePath);
                    string targetFolder = string.IsNullOrEmpty(relativeDirectory)
                        ? destination : Path.Combine(destination, relativeDirectory);
                    Directory.CreateDirectory(targetFolder);
                    string targetPath = GetUniqueTargetPath(targetFolder, Path.GetFileName(entry.FullPath));
                    MoveFileAfterVerification(entry.FullPath, targetPath);
                    summary.Completed++;
                    summary.Verified++;
                    summary.Bytes += entry.Length;
                    summary.ProcessedPaths.Add(entry.FullPath);
                }
                catch (Exception ex)
                {
                    summary.Failed++;
                    summary.Messages.Add(entry.FullPath + ": " + ex.Message);
                }
                completed++;
                if (progress != null) progress.Report(completed);
            }
            return summary;
        }

        private static void MoveFileAfterVerification(string source, string target)
        {
            string temporary = target + ".drivemedic-partial-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                long originalLength = new FileInfo(source).Length;
                byte[] sourceHash;
                byte[] buffer = new byte[1024 * 1024];
                using (SHA256 hasher = SHA256.Create())
                using (FileStream input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, read);
                        hasher.TransformBlock(buffer, 0, read, buffer, 0);
                    }
                    output.Flush();
                    hasher.TransformFinalBlock(new byte[0], 0, 0);
                    sourceHash = (byte[])hasher.Hash.Clone();
                }

                if (new FileInfo(temporary).Length != originalLength)
                    throw new IOException("Copy size verification failed; source file was kept.");

                byte[] targetHash;
                using (SHA256 hasher = SHA256.Create())
                using (FileStream verify = new FileStream(temporary, FileMode.Open, FileAccess.Read, FileShare.Read))
                    targetHash = hasher.ComputeHash(verify);
                if (!EqualHashes(sourceHash, targetHash))
                    throw new IOException("SHA-256 verification failed; source file was kept.");

                File.Move(temporary, target);
                temporary = null;
                try { File.Delete(source); }
                catch (Exception ex)
                {
                    throw new IOException("The verified destination copy was created, but the source could not be removed. Both copies were kept. " + ex.Message, ex);
                }
            }
            finally
            {
                if (!string.IsNullOrEmpty(temporary) && File.Exists(temporary))
                    try { File.Delete(temporary); } catch { }
            }
        }

        private static bool EqualHashes(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            int difference = 0;
            for (int i = 0; i < left.Length; i++) difference |= left[i] ^ right[i];
            return difference == 0;
        }

        private FileOperationSummary DeleteEntries(IList<FileBrowseEntry> entries)
        {
            FileOperationSummary summary = new FileOperationSummary();
            foreach (FileBrowseEntry entry in entries)
            {
                try
                {
                    FileSystem.DeleteFile(entry.FullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                    summary.Completed++;
                    summary.ProcessedPaths.Add(entry.FullPath);
                }
                catch (Exception ex)
                {
                    summary.Failed++;
                    summary.Messages.Add(entry.FullPath + ": " + ex.Message);
                }
            }
            return summary;
        }

        private bool ConfirmDestinationIsOtherDisk(string destination, string action)
        {
            string sourceVolume = Path.GetPathRoot(Path.GetFullPath(_activeRoot));
            string destinationVolume = Path.GetPathRoot(Path.GetFullPath(destination));
            if (string.Equals(sourceVolume, destinationVolume, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(FindForm(),
                    "Choose a destination on a different physical disk. A different folder or partition on the source disk is not a safe recovery destination.",
                    "Choose another disk", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            try
            {
                IList<DriveRecord> volumes = new DriveInventoryService().GetVolumes();
                int? sourceDisk = PhysicalDiskForRoot(sourceVolume, volumes);
                int? destinationDisk = PhysicalDiskForRoot(destinationVolume, volumes);
                if (sourceDisk.HasValue && destinationDisk.HasValue && sourceDisk.Value == destinationDisk.Value)
                {
                    MessageBox.Show(FindForm(),
                        "The source and destination map to the same physical disk. Choose a separate disk before you " + action + ".",
                        "Choose another physical disk", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }

                if (!sourceDisk.HasValue || !destinationDisk.HasValue)
                {
                    return MessageBox.Show(FindForm(),
                        "Windows could not verify the physical-disk mapping for source or destination. Confirm that the destination is physically separate and is not another partition on the source disk.",
                        "Confirm destination disk", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
                }
            }
            catch
            {
                return MessageBox.Show(FindForm(),
                    "DriveMedic could not verify physical-disk mapping. Confirm the destination is a separate physical disk.",
                    "Confirm destination disk", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
            }
            return true;
        }

        private static int? PhysicalDiskForRoot(string root, IList<DriveRecord> volumes)
        {
            DriveRecord match = volumes.FirstOrDefault(item => item != null &&
                !string.IsNullOrWhiteSpace(item.RootPath) &&
                string.Equals(item.RootPath.TrimEnd('\\'), (root ?? string.Empty).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
            return match == null ? (int?)null : match.PhysicalDiskIndex;
        }

        private bool ValidateDestination(out string destination)
        {
            destination = null;
            string value = _destination.Text.Trim();
            if (value.Length == 0)
            {
                MessageBox.Show(FindForm(), "Choose a destination on another physical disk first.", "Destination required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            try { destination = Path.GetFullPath(value); }
            catch (Exception ex)
            {
                MessageBox.Show(FindForm(), ex.Message, "Invalid destination", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (Directory.Exists(destination) && IsSameOrChild(_activeRoot, destination))
            {
                MessageBox.Show(FindForm(), "The destination cannot be inside the selected source folder.", "Unsafe destination", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (!Directory.Exists(Path.GetPathRoot(destination)))
            {
                MessageBox.Show(FindForm(), "The destination drive is not ready.", "Destination unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            return true;
        }

        private bool ValidateEntries(IList<FileBrowseEntry> entries)
        {
            if (_activeRoot == null || entries == null || entries.Count == 0)
            {
                MessageBox.Show(FindForm(), "Scan a disk first, then select one or more files.", "No files selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            return true;
        }

        private static string MakeRelativePath(string rootPath, string filePath)
        {
            string root = Path.GetFullPath(rootPath);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) &&
                !root.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                root += Path.DirectorySeparatorChar;
            Uri rootUri = new Uri(root);
            Uri fileUri = new Uri(Path.GetFullPath(filePath));
            string relative = Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString())
                .Replace('/', Path.DirectorySeparatorChar);
            if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new IOException("The selected item is outside the scanned source folder.");
            return relative;
        }

        private static string GetUniqueTargetPath(string folder, string name)
        {
            string candidate = Path.Combine(folder, name);
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
            string stem = Path.GetFileNameWithoutExtension(name);
            string extension = Path.GetExtension(name);
            for (int i = 1; i < 100000; i++)
            {
                candidate = Path.Combine(folder, stem + " (moved " + i + ")" + extension);
                if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
            }
            throw new IOException("Could not choose a safe destination name for " + name);
        }

        private static bool IsSameOrChild(string parent, string child)
        {
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(child)) return false;
            string parentPath = Path.GetFullPath(parent).TrimEnd('\\') + "\\";
            string childPath = Path.GetFullPath(child).TrimEnd('\\') + "\\";
            return childPath.StartsWith(parentPath, StringComparison.OrdinalIgnoreCase);
        }

        private void ShowOperationSummary(string title, FileOperationSummary result, string verb, int requested)
        {
            _status.Text = verb + ": " + result.Completed + " of " + requested + "; failed " + result.Failed + ".";
            string message = verb + ": " + result.Completed + "\r\nSHA-256 verified: " + result.Verified +
                "\r\nBytes processed: " + result.Bytes.ToString("N0") +
                "\r\nFailed: " + result.Failed;
            if (result.Messages.Count > 0)
                message += "\r\n\r\nDetails:\r\n" + string.Join("\r\n", result.Messages.Take(12).ToArray());
            MessageBox.Show(FindForm(), message, title, MessageBoxButtons.OK,
                result.Failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        private void RemoveProcessedSources(IEnumerable<string> paths)
        {
            HashSet<string> processed = new HashSet<string>(paths ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            _allResults.RemoveAll(item => processed.Contains(item.FullPath));
            ApplySearchFilter();
        }

        private void OpenContainingFolder(FileBrowseEntry entry)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = "/select,\"" + entry.FullPath + "\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex) { MessageBox.Show(FindForm(), ex.Message, "Open folder", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void RaiseRecoverDeleted()
        {
            if (MessageBox.Show(FindForm(),
                "Deleted files are not visible in the normal Windows folder list. DriveMedic will open QPhotoRec. Choose the old physical disk in QPhotoRec's own device list, then choose an output folder on a different physical disk. It may not restore original filenames. After the scan, use Browse recovered output to review the recovered files. Do not write to the old disk while recovering. Continue?",
                "Start deleted-file recovery", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                Action handler = RecoverDeletedFilesRequested;
                if (handler != null) handler();
            }
        }

        private void SetBusy(bool busy, string message)
        {
            _copySelectedButton.Enabled = !busy;
            _copyAllButton.Enabled = !busy;
            _moveSelectedButton.Enabled = !busy;
            _moveAllButton.Enabled = !busy;
            _deleteSelectedButton.Enabled = !busy;
            _selectAllButton.Enabled = !busy;
            _status.Text = message;
            _progress.Visible = busy;
            _progress.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
        }

        private Label MakeLabel(string text)
        {
            return new Label
            {
                Text = text, AutoSize = true, ForeColor = _muted,
                Padding = new Padding(0, 7, 4, 0), Margin = new Padding(4)
            };
        }

        private Button MakeButton(string text, Color background, Color foreground, Action action = null)
        {
            Button button = new Button { Text = text };
            StyleButton(button, background, foreground);
            if (action != null) button.Click += delegate { action(); };
            return button;
        }

        private void StyleButton(Button button, Color background, Color foreground)
        {
            button.BackColor = background;
            button.ForeColor = foreground;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Color.FromArgb(205, 216, 229);
            button.FlatAppearance.BorderSize = background == _blue ? 0 : 1;
            button.FlatAppearance.MouseOverBackColor = background == _blue
                ? Color.FromArgb(27, 88, 151) : Color.FromArgb(230, 238, 247);
            button.Font = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
            button.Height = 34;
            button.AutoSize = true;
            button.Padding = new Padding(8, 3, 8, 3);
            button.Margin = new Padding(3);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
        }

        private sealed class DriveOption
        {
            public string Root { get; private set; }
            private string Label { get; set; }

            public DriveOption(string root, string label) { Root = root; Label = label; }
            public override string ToString() { return Label; }
        }

        private sealed class FileOperationSummary
        {
            public int Completed;
            public int Failed;
            public int Verified;
            public long Bytes;
            public List<string> Messages = new List<string>();
            public List<string> ProcessedPaths = new List<string>();
        }
    }
}