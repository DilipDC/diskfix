using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace DriveMedic.Core
{
    public enum FileBrowseMode
    {
        AllFiles,
        HiddenAndSystem,
        Downloads,
        ApplicationsAndInstallers,
        Documents,
        PhotosAndVideos,
        RecentlyModified
    }

    public sealed class FileBrowseEntry
    {
        public bool Selected { get; set; }
        public string FullPath { get; set; }
        public string Name { get; set; }
        public string Extension { get; set; }
        public long Length { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }
        public FileAttributes Attributes { get; set; }
        public bool IsHiddenOrSystem { get; set; }
        public bool IsSystem { get; set; }

        public string TypeText
        {
            get { return string.IsNullOrEmpty(Extension) ? "File" : Extension.TrimStart('.').ToUpperInvariant(); }
        }

        public string SizeText
        {
            get { return FormatSize(Length); }
        }

        public string ModifiedText
        {
            get { return LastWriteTimeUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"); }
        }

        public string FlagsText
        {
            get
            {
                List<string> flags = new List<string>();
                if (IsHiddenOrSystem) flags.Add("Hidden");
                if (IsSystem) flags.Add("System");
                if ((Attributes & FileAttributes.ReadOnly) != 0) flags.Add("Read-only");
                return flags.Count == 0 ? "—" : string.Join(", ", flags);
            }
        }

        private static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = Math.Max(0, bytes);
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
            return value.ToString(value >= 100 || unit == 0 ? "0" : "0.0") + " " + units[unit];
        }
    }

    public sealed class FileBrowseProgress
    {
        public int DirectoriesVisited { get; set; }
        public int FilesVisited { get; set; }
        public int MatchingFiles { get; set; }
        public string CurrentDirectory { get; set; }
    }

    public sealed class FileBrowseScanResult
    {
        public List<FileBrowseEntry> Files { get; private set; }
        public int DirectoriesVisited { get; set; }
        public int FilesVisited { get; set; }
        public int AccessErrors { get; set; }
        public int SkippedReparsePoints { get; set; }
        public bool LimitReached { get; set; }

        public FileBrowseScanResult()
        {
            Files = new List<FileBrowseEntry>();
        }
    }

    /// <summary>
    /// Read-only file browser for mounted volumes. Deleted sectors are not visible through the
    /// normal Windows filesystem and must be scanned with PhotoRec/TestDisk.
    /// </summary>
    public sealed class FileBrowseService
    {
        public const int MaximumResults = 50000;

        private static readonly HashSet<string> ApplicationExtensions = Set(
            ".exe", ".msi", ".msix", ".appx", ".appxbundle", ".zip", ".7z", ".rar",
            ".iso", ".cab", ".msu", ".jar", ".apk", ".dmg");

        private static readonly HashSet<string> DocumentExtensions = Set(
            ".pdf", ".doc", ".docx", ".odt", ".rtf", ".txt", ".md", ".csv",
            ".xls", ".xlsx", ".ods", ".ppt", ".pptx", ".odp");

        private static readonly HashSet<string> MediaExtensions = Set(
            ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".heic", ".tif", ".tiff",
            ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".mp3", ".wav", ".flac", ".m4a", ".aac");

        public FileBrowseScanResult Scan(
            string rootPath,
            FileBrowseMode mode,
            CancellationToken cancellationToken,
            IProgress<FileBrowseProgress> progress)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
                throw new ArgumentException("Select a disk or folder to scan.", "rootPath");

            string root = Path.GetFullPath(rootPath);
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException("The selected disk or folder is not available: " + root);

            FileBrowseScanResult result = new FileBrowseScanResult();
            Stack<DirectoryWorkItem> pending = new Stack<DirectoryWorkItem>();
            FileAttributes rootAttributes = GetAttributes(root, ref result);
            pending.Push(new DirectoryWorkItem(
                root,
                (rootAttributes & FileAttributes.Hidden) != 0 || (rootAttributes & FileAttributes.System) != 0,
                (rootAttributes & FileAttributes.System) != 0));

            DateTime recentCutoff = DateTime.UtcNow.AddDays(-30);

            while (pending.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DirectoryWorkItem current = pending.Pop();
                result.DirectoriesVisited++;

                if (progress != null && (result.DirectoriesVisited == 1 || result.DirectoriesVisited % 20 == 0))
                    progress.Report(MakeProgress(result, current.Path));

                string[] files;
                try { files = Directory.GetFiles(current.Path, "*", SearchOption.TopDirectoryOnly); }
                catch
                {
                    result.AccessErrors++;
                    files = new string[0];
                }

                foreach (string filePath in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    result.FilesVisited++;
                    try
                    {
                        FileAttributes attributes = File.GetAttributes(filePath);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            result.SkippedReparsePoints++;
                            continue;
                        }

                        FileInfo info = new FileInfo(filePath);
                        bool isSystem = current.ParentSystem || (attributes & FileAttributes.System) != 0;
                        bool isHidden = current.ParentHidden ||
                            (attributes & FileAttributes.Hidden) != 0 ||
                            (attributes & FileAttributes.System) != 0;

                        if (!Matches(filePath, info.Extension, info.LastWriteTimeUtc, isHidden, mode, recentCutoff))
                            continue;

                        result.Files.Add(new FileBrowseEntry
                        {
                            FullPath = info.FullName,
                            Name = info.Name,
                            Extension = info.Extension,
                            Length = info.Length,
                            LastWriteTimeUtc = info.LastWriteTimeUtc,
                            Attributes = attributes,
                            IsHiddenOrSystem = isHidden,
                            IsSystem = isSystem
                        });

                        if (result.Files.Count >= MaximumResults)
                        {
                            result.LimitReached = true;
                            break;
                        }
                    }
                    catch { result.AccessErrors++; }
                }

                if (result.LimitReached) break;

                string[] directories;
                try { directories = Directory.GetDirectories(current.Path, "*", SearchOption.TopDirectoryOnly); }
                catch
                {
                    result.AccessErrors++;
                    directories = new string[0];
                }

                // Reverse push order so the scan proceeds alphabetically and reports stable results.
                Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
                for (int index = directories.Length - 1; index >= 0; index--)
                {
                    string child = directories[index];
                    string leaf = Path.GetFileName(child.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    if (string.Equals(leaf, "System Volume Information", StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        FileAttributes attributes = File.GetAttributes(child);
                        if ((attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            result.SkippedReparsePoints++;
                            continue;
                        }

                        pending.Push(new DirectoryWorkItem(
                            child,
                            current.ParentHidden || (attributes & FileAttributes.Hidden) != 0 ||
                                (attributes & FileAttributes.System) != 0,
                            current.ParentSystem || (attributes & FileAttributes.System) != 0));
                    }
                    catch { result.AccessErrors++; }
                }
            }

            if (progress != null)
                progress.Report(MakeProgress(result, root));
            return result;
        }

        public static bool IsApplicationFile(string path)
        {
            return !string.IsNullOrEmpty(path) && ApplicationExtensions.Contains(Path.GetExtension(path));
        }

        private static bool Matches(
            string path,
            string extension,
            DateTime lastWriteTimeUtc,
            bool hiddenOrSystem,
            FileBrowseMode mode,
            DateTime recentCutoff)
        {
            switch (mode)
            {
                case FileBrowseMode.HiddenAndSystem:
                    return hiddenOrSystem;
                case FileBrowseMode.Downloads:
                    return IsInsideDownloadsFolder(path);
                case FileBrowseMode.ApplicationsAndInstallers:
                    return ApplicationExtensions.Contains(extension);
                case FileBrowseMode.Documents:
                    return DocumentExtensions.Contains(extension);
                case FileBrowseMode.PhotosAndVideos:
                    return MediaExtensions.Contains(extension);
                case FileBrowseMode.RecentlyModified:
                    return lastWriteTimeUtc >= recentCutoff;
                default:
                    return true;
            }
        }

        private static bool IsInsideDownloadsFolder(string path)
        {
            string normalized = path.Replace('/', '\\');
            string[] segments = normalized.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            return segments.Any(segment => string.Equals(segment, "Downloads", StringComparison.OrdinalIgnoreCase));
        }

        private static HashSet<string> Set(params string[] values)
        {
            return new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
        }

        private static FileAttributes GetAttributes(string path, ref FileBrowseScanResult result)
        {
            try { return File.GetAttributes(path); }
            catch { result.AccessErrors++; return FileAttributes.Directory; }
        }

        private static FileBrowseProgress MakeProgress(FileBrowseScanResult result, string path)
        {
            return new FileBrowseProgress
            {
                DirectoriesVisited = result.DirectoriesVisited,
                FilesVisited = result.FilesVisited,
                MatchingFiles = result.Files.Count,
                CurrentDirectory = path
            };
        }

        private sealed class DirectoryWorkItem
        {
            public string Path { get; private set; }
            public bool ParentHidden { get; private set; }
            public bool ParentSystem { get; private set; }

            public DirectoryWorkItem(string path, bool parentHidden, bool parentSystem)
            {
                Path = path;
                ParentHidden = parentHidden;
                ParentSystem = parentSystem;
            }
        }
    }
}