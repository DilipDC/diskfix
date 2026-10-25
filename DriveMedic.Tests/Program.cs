using System;
using System.IO;
using System.Linq;
using DriveMedic.Core;

namespace DriveMedic.Tests
{
    internal static class Program
    {
        private static int _passed;
        private static int _failed;

        private static int Main()
        {
            Run("file browser all-files mode includes hidden files", TestBrowseIncludesHiddenFiles);
            Run("file browser hidden mode includes descendants of hidden folders", TestBrowseHiddenFolderContents);
            Run("file browser application filter finds installers", TestBrowseApplicationInstallers);
            Run("hardware warning always blocks generic repair", TestHardwareWarningPriority);
            Run("RAW volume is routed to recovery, not CHKDSK", TestRawVolume);
            Run("access denied recommends administrator retry", TestAccessDenied);
            Run("read error recommends copy-first recovery", TestReadError);
            Run("filesystem error recommends gated CHKDSK", TestFilesystemError);
            Run("SFC is limited to currently running Windows", TestSfcScope);
            Run("information finding does not suggest a repair", TestInformation);
            Run("copy preserves source and does not overwrite destination", TestCopyDoesNotOverwrite);
            Run("copy refuses a destination nested inside source", TestCopyPreventsRecursion);
            Run("SHA-256 verified copy preserves source and reports byte count", TestVerifiedCopy);
            Run("SHA-256 verified copy supports empty files", TestVerifiedEmptyFile);
            Run("missing source is reported without fabricating a recovered file", TestMissingSource);
            Run("copy results are isolated between independent runs", TestIndependentResults);
            Run("drive-letter validator accepts mounted volume roots", TestValidDriveRoot);
            Run("drive-letter validator refuses physical device paths", TestInvalidDriveRoot);
            Run("CHKDSK nonzero exit summary signals caution", TestChkdskNonzero);
            Run("CHKDSK access-denied summary is explicit", TestChkdskAccessDenied);
            Console.WriteLine("\r\nResults: " + _passed + " passed; " + _failed + " failed.");
            return _failed == 0 ? 0 : 1;
        }

        private static void TestHardwareWarningPriority()
        {
            Equal(RepairAction.StopAndRecoverOrImageDrive, RepairAdvisor.Recommend(FindingKind.FileSystemError, false, true, false));
        }

        private static void TestRawVolume()
        {
            Equal(RepairAction.StopAndRecoverOrImageDrive, RepairAdvisor.Recommend(FindingKind.FileSystemError, false, false, true));
            Equal(RepairAction.StopAndRecoverOrImageDrive, RepairAdvisor.Recommend(FindingKind.RawVolume, false, false, false));
        }

        private static void TestAccessDenied()
        {
            Equal(RepairAction.RetryAsAdministrator, RepairAdvisor.Recommend(FindingKind.AccessDenied, false, false, false));
        }

        private static void TestReadError()
        {
            Equal(RepairAction.CopyRecoverableData, RepairAdvisor.Recommend(FindingKind.ReadError, false, false, false));
        }

        private static void TestFilesystemError()
        {
            Equal(RepairAction.RunChkdskRepair, RepairAdvisor.Recommend(FindingKind.FileSystemError, false, false, false));
        }

        private static void TestSfcScope()
        {
            Equal(RepairAction.RunSystemFileChecker, RepairAdvisor.Recommend(FindingKind.SystemFileIssue, true, false, false));
            Equal(RepairAction.CopyRecoverableData, RepairAdvisor.Recommend(FindingKind.SystemFileIssue, false, false, false));
        }

        private static void TestInformation()
        {
            Equal(RepairAction.None, RepairAdvisor.Recommend(FindingKind.Information, false, false, false));
        }

        private static void TestCopyDoesNotOverwrite()
        {
            string root = Path.Combine(Path.GetTempPath(), "DriveMedicTest-" + Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "source");
            string destination = Path.Combine(root, "destination");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(destination);
            string sourceFile = Path.Combine(source, "sample.txt");
            File.WriteAllText(sourceFile, "source contents");
            File.WriteAllText(Path.Combine(destination, "sample.txt"), "keep existing");
            try
            {
                CopyResult result = new RecoveryService().CopyPaths(new[] { sourceFile }, destination);
                Equal(1, result.CopiedFiles);
                Equal("source contents", File.ReadAllText(sourceFile));
                Equal("keep existing", File.ReadAllText(Path.Combine(destination, "sample.txt")));
                Equal("source contents", File.ReadAllText(Path.Combine(destination, "sample (recovered 1).txt")));
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        }

        private static void TestCopyPreventsRecursion()
        {
            string root = Path.Combine(Path.GetTempPath(), "DriveMedicTest-" + Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "source");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "sample.txt"), "source");
            string destination = Path.Combine(source, "recovered");
            try
            {
                CopyResult result = new RecoveryService().CopyPaths(new[] { source }, destination);
                Equal(0, result.CopiedFiles);
                Equal(1, result.FailedFiles);
                if (Directory.Exists(destination)) throw new Exception("Copy unexpectedly created a destination inside the source tree.");
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        }

        private static void TestVerifiedCopy()
        {
            string root = Path.Combine(Path.GetTempPath(), "DriveMedicTest-" + Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "source");
            string destination = Path.Combine(root, "destination");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(destination);
            string sourceFile = Path.Combine(source, "verify.bin");
            byte[] payload = new byte[32769];
            for (int index = 0; index < payload.Length; index++) payload[index] = (byte)((index * 37) % 251);
            File.WriteAllBytes(sourceFile, payload);
            try
            {
                CopyResult result = new RecoveryService().CopyPaths(new[] { sourceFile }, destination, true);
                Equal(1, result.CopiedFiles);
                Equal(0, result.FailedFiles);
                Equal(1, result.FilesVerified);
                Equal((long)payload.Length, result.BytesCopied);
                Equal(true, AreEqual(payload, File.ReadAllBytes(sourceFile)));
                Equal(true, AreEqual(payload, File.ReadAllBytes(Path.Combine(destination, "verify.bin"))));
                Equal(0, Directory.GetFiles(destination, "*.drivemedic-partial-*.tmp").Length);
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        }

        private static void TestVerifiedEmptyFile()
        {
            string root = Path.Combine(Path.GetTempPath(), "DriveMedicTest-" + Guid.NewGuid().ToString("N"));
            string source = Path.Combine(root, "empty.dat");
            string destination = Path.Combine(root, "destination");
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(destination);
            File.WriteAllBytes(source, new byte[0]);
            try
            {
                CopyResult result = new RecoveryService().CopyPaths(new[] { source }, destination, true);
                Equal(1, result.CopiedFiles);
                Equal(1, result.FilesVerified);
                Equal(0L, result.BytesCopied);
                Equal(0L, new FileInfo(Path.Combine(destination, "empty.dat")).Length);
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        }

        private static void TestMissingSource()
        {
            string root = Path.Combine(Path.GetTempPath(), "DriveMedicTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                CopyResult result = new RecoveryService().CopyPaths(new[] { Path.Combine(root, "does-not-exist.txt") }, Path.Combine(root, "out"), true);
                Equal(0, result.CopiedFiles);
                Equal(1, result.FailedFiles);
                if (result.Messages.Count == 0 || result.Messages[0].IndexOf("no longer exists", StringComparison.OrdinalIgnoreCase) < 0)
                    throw new Exception("Missing source was not explained in the result.");
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        }

        private static void TestIndependentResults()
        {
            string root = Path.Combine(Path.GetTempPath(), "DriveMedicTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string file = Path.Combine(root, "one.txt");
            File.WriteAllText(file, "one");
            try
            {
                RecoveryService service = new RecoveryService();
                CopyResult first = service.CopyPaths(new[] { file }, Path.Combine(root, "out1"));
                CopyResult second = service.CopyPaths(new[] { Path.Combine(root, "missing.txt") }, Path.Combine(root, "out2"));
                Equal(1, first.CopiedFiles);
                Equal(0, first.FailedFiles);
                Equal(0, second.CopiedFiles);
                Equal(1, second.FailedFiles);
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        }

        private static bool AreEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++) if (left[index] != right[index]) return false;
            return true;
        }

        private static void TestBrowseIncludesHiddenFiles()
        {
            string root = Path.Combine(Path.GetTempPath(), "DriveMedicTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string visible = Path.Combine(root, "visible.txt");
            string hidden = Path.Combine(root, "hidden.txt");
            File.WriteAllText(visible, "visible");
            File.WriteAllText(hidden, "hidden");
            File.SetAttributes(hidden, File.GetAttributes(hidden) | FileAttributes.Hidden);
            try
            {
                FileBrowseScanResult result = new FileBrowseService().Scan(root, FileBrowseMode.AllFiles, System.Threading.CancellationToken.None, null);
                Equal(true, result.Files.Any(item => item.Name == "visible.txt"));
                Equal(true, result.Files.Any(item => item.Name == "hidden.txt"));
                Equal(true, result.Files.First(item => item.Name == "hidden.txt").IsHiddenOrSystem);
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        }

        private static void TestBrowseHiddenFolderContents()
        {
            string root = Path.Combine(Path.GetTempPath(), "DriveMedicTest-" + Guid.NewGuid().ToString("N"));
            string hiddenFolder = Path.Combine(root, ".hidden");
            Directory.CreateDirectory(hiddenFolder);
            string nested = Path.Combine(hiddenFolder, "inside.exe");
            File.WriteAllText(nested, "app");
            File.SetAttributes(hiddenFolder, File.GetAttributes(hiddenFolder) | FileAttributes.Hidden);
            try
            {
                FileBrowseScanResult result = new FileBrowseService().Scan(root, FileBrowseMode.HiddenAndSystem, System.Threading.CancellationToken.None, null);
                Equal(1, result.Files.Count);
                Equal("inside.exe", result.Files[0].Name);
                Equal(true, result.Files[0].IsHiddenOrSystem);
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        }

        private static void TestBrowseApplicationInstallers()
        {
            string root = Path.Combine(Path.GetTempPath(), "DriveMedicTest-" + Guid.NewGuid().ToString("N"));
            string downloads = Path.Combine(root, "Downloads");
            Directory.CreateDirectory(downloads);
            File.WriteAllText(Path.Combine(downloads, "setup.exe"), "installer");
            File.WriteAllText(Path.Combine(downloads, "manual.pdf"), "manual");
            try
            {
                FileBrowseScanResult result = new FileBrowseService().Scan(root, FileBrowseMode.ApplicationsAndInstallers, System.Threading.CancellationToken.None, null);
                Equal(1, result.Files.Count);
                Equal("setup.exe", result.Files[0].Name);
            }
            finally { try { Directory.Delete(root, true); } catch { } }
        }

        private static void TestValidDriveRoot()
        {
            Equal("D:", DiagnosticService.ValidateDriveRoot("D:\\"));
            Equal("E:", DiagnosticService.ValidateDriveRoot("E:"));
        }

        private static void TestInvalidDriveRoot()
        {
            bool thrown = false;
            try { DiagnosticService.ValidateDriveRoot("\\\\.\\PHYSICALDRIVE0"); }
            catch (ArgumentException) { thrown = true; }
            Equal(true, thrown);
        }

        private static void TestChkdskNonzero()
        {
            ChkdskResult result = new ChkdskResult { Volume = "D:", ExitCode = 2, Output = "check completed with additional detail" };
            if (DiagnosticService.SummarizeChkdskOutput(result).IndexOf("exit code 2", StringComparison.OrdinalIgnoreCase) < 0)
                throw new Exception("Nonzero exit code was not included in the summary.");
        }

        private static void TestChkdskAccessDenied()
        {
            ChkdskResult result = new ChkdskResult { Volume = "D:", ExitCode = 1, AccessDenied = true, Output = "Access is denied." };
            if (DiagnosticService.SummarizeChkdskOutput(result).IndexOf("denied", StringComparison.OrdinalIgnoreCase) < 0)
                throw new Exception("Access-denied condition was not described.");
        }

        private static void Run(string name, Action test)
        {
            try { test(); _passed++; Console.WriteLine("PASS: " + name); }
            catch (Exception ex) { _failed++; Console.WriteLine("FAIL: " + name + " — " + ex.Message); }
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!object.Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual + ".");
        }
    }
}