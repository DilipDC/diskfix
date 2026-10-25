using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace DriveMedic.Core
{
    public sealed class ScanIssue
    {
        public string Path { get; set; }
        public string Kind { get; set; }
        public string Message { get; set; }
    }

    public sealed class ScanResult
    {
        public int FilesChecked { get; set; }
        public long BytesRead { get; set; }
        public bool LimitReached { get; set; }
        public List<ScanIssue> Issues { get; private set; }
        public ScanResult() { Issues = new List<ScanIssue>(); }
    }

    public sealed class ChkdskResult
    {
        public string Volume { get; set; }
        public int ExitCode { get; set; }
        public bool TimedOut { get; set; }
        public string Output { get; set; }
        public bool AccessDenied { get; set; }
    }

    public static class DiagnosticService
    {
        public static ScanResult ScanReadableFiles(string rootPath, int maxFiles)
        {
            if (string.IsNullOrWhiteSpace(rootPath)) throw new DirectoryNotFoundException("Choose a folder to scan.");
            if (!Directory.Exists(rootPath))
            {
                try
                {
                    FileAttributes attributes = File.GetAttributes(rootPath);
                    if ((attributes & FileAttributes.Directory) == 0) throw new DirectoryNotFoundException("The selected path is not a folder.");
                }
                catch (UnauthorizedAccessException) { throw; }
                catch (FileNotFoundException) { throw new DirectoryNotFoundException("The selected folder does not exist or the volume is unavailable: " + rootPath); }
                catch (DirectoryNotFoundException) { throw new DirectoryNotFoundException("The selected folder does not exist or the volume is unavailable: " + rootPath); }
            }
            if (maxFiles < 1) maxFiles = 1;

            ScanResult result = new ScanResult();
            Stack<string> pendingDirectories = new Stack<string>();
            byte[] buffer = new byte[1024 * 1024];
            pendingDirectories.Push(rootPath);
            while (pendingDirectories.Count > 0)
            {
                string directory = pendingDirectories.Pop();
                string[] files;
                try { files = Directory.GetFiles(directory); }
                catch (UnauthorizedAccessException ex) { result.Issues.Add(new ScanIssue { Path = directory, Kind = "Access denied", Message = ex.Message }); continue; }
                catch (Exception ex) { result.Issues.Add(new ScanIssue { Path = directory, Kind = "Directory read error", Message = ex.Message }); continue; }

                foreach (string file in files)
                {
                    if (result.FilesChecked >= maxFiles) { result.LimitReached = true; return result; }
                    result.FilesChecked++;
                    try
                    {
                        using (FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        {
                            int read;
                            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) result.BytesRead += read;
                        }
                    }
                    catch (UnauthorizedAccessException ex) { result.Issues.Add(new ScanIssue { Path = file, Kind = "Access denied", Message = ex.Message }); }
                    catch (IOException ex) { result.Issues.Add(new ScanIssue { Path = file, Kind = "Read error", Message = ex.Message }); }
                    catch (Exception ex) { result.Issues.Add(new ScanIssue { Path = file, Kind = "File error", Message = ex.Message }); }
                }

                string[] directories;
                try { directories = Directory.GetDirectories(directory); }
                catch (UnauthorizedAccessException ex) { result.Issues.Add(new ScanIssue { Path = directory, Kind = "Access denied", Message = ex.Message }); continue; }
                catch (Exception ex) { result.Issues.Add(new ScanIssue { Path = directory, Kind = "Directory read error", Message = ex.Message }); continue; }

                foreach (string child in directories)
                {
                    try
                    {
                        DirectoryInfo info = new DirectoryInfo(child);
                        if ((info.Attributes & FileAttributes.ReparsePoint) == 0) pendingDirectories.Push(child);
                    }
                    catch (Exception ex) { result.Issues.Add(new ScanIssue { Path = child, Kind = "Folder metadata error", Message = ex.Message }); }
                }
            }
            return result;
        }

        public static ChkdskResult RunChkdskReadOnly(string rootPath)
        {
            string drive = ValidateDriveRoot(rootPath);
            ChkdskResult result = new ChkdskResult { Volume = drive, Output = string.Empty };
            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = "chkdsk.exe";
            start.Arguments = drive;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.WorkingDirectory = Environment.SystemDirectory;

            using (Process process = new Process())
            {
                process.StartInfo = start;
                process.Start();
                Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                Task<string> stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(30 * 60 * 1000))
                {
                    result.TimedOut = true;
                    try { process.Kill(); } catch { }
                    result.Output = "Read-only CHKDSK exceeded the 30-minute safety limit and was stopped. This does not prove that the disk is damaged.";
                    return result;
                }
                Task.WaitAll(stdout, stderr);
                result.ExitCode = process.ExitCode;
                result.Output = (stdout.Result + Environment.NewLine + stderr.Result).Trim();
                result.AccessDenied = result.Output.IndexOf("access is denied", StringComparison.OrdinalIgnoreCase) >= 0 || result.Output.IndexOf("insufficient privileges", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return result;
        }

        public static string ValidateDriveRoot(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("Select a drive first.");
            string candidate = rootPath.Trim().TrimEnd('\\');
            if (candidate.Length != 2 || candidate[1] != ':' || !char.IsLetter(candidate[0]))
                throw new ArgumentException("Filesystem check only accepts a mounted drive letter such as D:. Physical disks and unmounted partitions are not supported by this action.");
            return candidate;
        }

        public static string SummarizeChkdskOutput(ChkdskResult result)
        {
            if (result.TimedOut) return "Read-only check timed out; review hardware condition and consider a sector image before retrying.";
            if (result.AccessDenied) return "Windows denied access to the volume. Retry the read-only check with administrator rights.";
            string output = result.Output ?? string.Empty;
            if (output.IndexOf("found no problems", StringComparison.OrdinalIgnoreCase) >= 0 || output.IndexOf("no further action is required", StringComparison.OrdinalIgnoreCase) >= 0)
                return "CHKDSK reports no filesystem problems in its available output. This does not validate every file or prove drive hardware is healthy.";
            if (output.IndexOf("found problems", StringComparison.OrdinalIgnoreCase) >= 0 || output.IndexOf("errors found", StringComparison.OrdinalIgnoreCase) >= 0 || output.IndexOf("corrupt", StringComparison.OrdinalIgnoreCase) >= 0)
                return "CHKDSK output may indicate filesystem problems. Review the complete output and back up data before attempting any repair.";
            if (result.ExitCode != 0) return "CHKDSK returned exit code " + result.ExitCode + ", which may indicate filesystem errors or that Windows could not complete the check. Review the full output and back up data before any repair.";
            if (string.IsNullOrWhiteSpace(output)) return "CHKDSK returned no readable output. Check permissions and Windows event logs.";
            return "CHKDSK finished with exit code " + result.ExitCode + ". Review the captured output; wording varies by Windows version and language.";
        }
    }
}