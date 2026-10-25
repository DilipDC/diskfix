using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace DriveMedic.Core
{
    public sealed class CopyResult
    {
        public int CopiedFiles { get; set; }
        public int FailedFiles { get; set; }
        public int SkippedReparsePoints { get; set; }
        public int FilesVerified { get; set; }
        public long BytesCopied { get; set; }
        public List<string> Messages { get; private set; }

        public CopyResult() { Messages = new List<string>(); }
    }

    /// <summary>Copies readable data only. It never moves, deletes, overwrites, or modifies source files.</summary>
    public sealed class RecoveryService
    {
        public CopyResult CopyPaths(IEnumerable<string> sourcePaths, string destinationRoot)
        {
            return CopyPaths(sourcePaths, destinationRoot, false);
        }

        /// <summary>
        /// Copies selected files and folders without overwriting existing destinations. Optional SHA-256
        /// verification hashes source bytes during the single source read, then verifies the temporary copy.
        /// </summary>
        public CopyResult CopyPaths(IEnumerable<string> sourcePaths, string destinationRoot, bool verifySha256)
        {
            if (sourcePaths == null) throw new ArgumentNullException("sourcePaths");
            if (string.IsNullOrWhiteSpace(destinationRoot)) throw new ArgumentException("Choose a destination folder.", "destinationRoot");

            CopyResult result = new CopyResult();
            List<string> normalizedSources = new List<string>();
            foreach (string source in sourcePaths)
            {
                if (string.IsNullOrWhiteSpace(source)) continue;
                try
                {
                    string fullSource = Path.GetFullPath(source);
                    if (Directory.Exists(fullSource))
                    {
                        FileAttributes sourceAttributes = File.GetAttributes(fullSource);
                        if ((sourceAttributes & FileAttributes.ReparsePoint) != 0)
                        {
                            result.SkippedReparsePoints++;
                            result.Messages.Add("Skipped selected reparse-point directory to avoid following a junction or symbolic link: " + fullSource);
                            continue;
                        }
                        if (IsSameOrChild(fullSource, destinationRoot))
                        {
                            result.FailedFiles++;
                            result.Messages.Add("Copy cancelled because the destination is inside a selected source folder: " + fullSource);
                            return result;
                        }
                    }
                    normalizedSources.Add(fullSource);
                }
                catch (Exception ex)
                {
                    result.FailedFiles++;
                    result.Messages.Add("Invalid source path " + source + ": " + ex.Message);
                }
            }
            if (normalizedSources.Count == 0) return result;

            Directory.CreateDirectory(destinationRoot);
            foreach (string fullSource in normalizedSources)
            {
                try
                {
                    if (Directory.Exists(fullSource))
                    {
                        string name = new DirectoryInfo(fullSource.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Name;
                        string target = GetUniqueDirectoryPath(destinationRoot, name);
                        CopyDirectory(fullSource, target, result, verifySha256);
                    }
                    else if (File.Exists(fullSource))
                    {
                        CopyFile(fullSource, GetUniqueFilePath(destinationRoot, Path.GetFileName(fullSource)), result, verifySha256);
                    }
                    else
                    {
                        result.FailedFiles++;
                        result.Messages.Add("Source no longer exists: " + fullSource);
                    }
                }
                catch (Exception ex)
                {
                    result.FailedFiles++;
                    result.Messages.Add("Could not start copying " + fullSource + ": " + ex.Message);
                }
            }
            return result;
        }

        private void CopyDirectory(string source, string destination, CopyResult result, bool verifySha256)
        {
            try
            {
                FileAttributes sourceAttributes = File.GetAttributes(source);
                if ((sourceAttributes & FileAttributes.ReparsePoint) != 0)
                {
                    result.SkippedReparsePoints++;
                    result.Messages.Add("Skipped reparse-point directory: " + source);
                    return;
                }
                Directory.CreateDirectory(destination);
                string[] files = Directory.GetFiles(source);
                foreach (string file in files)
                {
                    try { CopyFile(file, GetUniqueFilePath(destination, Path.GetFileName(file)), result, verifySha256); }
                    catch (Exception ex) { result.FailedFiles++; result.Messages.Add("File failed: " + file + " — " + ex.Message); }
                }

                string[] directories = Directory.GetDirectories(source);
                foreach (string directory in directories)
                {
                    try
                    {
                        DirectoryInfo info = new DirectoryInfo(directory);
                        if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                        {
                            result.SkippedReparsePoints++;
                            result.Messages.Add("Skipped reparse-point directory: " + directory);
                            continue;
                        }
                        CopyDirectory(directory, GetUniqueDirectoryPath(destination, info.Name), result, verifySha256);
                    }
                    catch (Exception ex) { result.FailedFiles++; result.Messages.Add("Folder failed: " + directory + " — " + ex.Message); }
                }

                try { Directory.SetLastWriteTime(destination, Directory.GetLastWriteTime(source)); }
                catch (Exception ex) { result.Messages.Add("Could not preserve folder timestamp for " + source + ": " + ex.Message); }
            }
            catch (Exception ex)
            {
                result.FailedFiles++;
                result.Messages.Add("Folder failed: " + source + " — " + ex.Message);
            }
        }

        private static void CopyFile(string source, string destination, CopyResult result, bool verifySha256)
        {
            string temporaryPath = null;
            try
            {
                FileAttributes attributes = File.GetAttributes(source);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    result.SkippedReparsePoints++;
                    result.Messages.Add("Skipped reparse-point file to avoid following a symbolic link: " + source);
                    return;
                }

                // Stage beside the final destination. A failed copy is cleaned up and never appears as a complete recovery.
                temporaryPath = destination + ".drivemedic-partial-" + Guid.NewGuid().ToString("N") + ".tmp";
                long sourceLength = new FileInfo(source).Length;
                byte[] sourceHash = null;
                if (verifySha256)
                {
                    byte[] buffer = new byte[1024 * 1024];
                    using (SHA256 hasher = SHA256.Create())
                    using (FileStream input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (FileStream output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
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
                }
                else
                {
                    File.Copy(source, temporaryPath, false);
                }

                long copiedLength = new FileInfo(temporaryPath).Length;
                if (sourceLength != copiedLength)
                    throw new IOException("Source and copy sizes differ; the source may have changed while being copied.");

                if (verifySha256)
                {
                    byte[] destinationHash;
                    using (SHA256 hasher = SHA256.Create())
                    using (FileStream copyStream = new FileStream(temporaryPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        destinationHash = hasher.ComputeHash(copyStream);
                    if (!HashesEqual(sourceHash, destinationHash))
                        throw new IOException("SHA-256 verification failed. The incomplete or unverified copy was not published as a recovered file.");
                }

                try { File.SetLastWriteTime(temporaryPath, File.GetLastWriteTime(source)); }
                catch (Exception ex) { result.Messages.Add("Copied but could not preserve timestamp for " + source + ": " + ex.Message); }

                // File.Move on the same volume fails if another file already occupies the destination.
                File.Move(temporaryPath, destination);
                temporaryPath = null;
                result.CopiedFiles++;
                result.BytesCopied += copiedLength;
                if (verifySha256) result.FilesVerified++;
            }
            catch (Exception ex)
            {
                result.FailedFiles++;
                result.Messages.Add("File failed: " + source + " — " + ex.Message);
            }
            finally
            {
                if (!string.IsNullOrEmpty(temporaryPath) && File.Exists(temporaryPath))
                {
                    try { File.Delete(temporaryPath); }
                    catch (Exception ex) { result.Messages.Add("Could not remove incomplete temporary copy " + temporaryPath + ": " + ex.Message); }
                }
            }
        }

        private static bool HashesEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            int difference = 0;
            for (int index = 0; index < left.Length; index++) difference |= left[index] ^ right[index];
            return difference == 0;
        }

        private static string GetUniqueFilePath(string folder, string name)
        {
            string candidate = Path.Combine(folder, name);
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
            string stem = Path.GetFileNameWithoutExtension(name);
            string extension = Path.GetExtension(name);
            for (int index = 1; index < 100000; index++)
            {
                candidate = Path.Combine(folder, stem + " (recovered " + index + ")" + extension);
                if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
            }
            throw new IOException("Could not find a unique destination name for " + name);
        }

        private static string GetUniqueDirectoryPath(string folder, string name)
        {
            string candidate = Path.Combine(folder, name);
            if (!Directory.Exists(candidate) && !File.Exists(candidate)) return candidate;
            for (int index = 1; index < 100000; index++)
            {
                candidate = Path.Combine(folder, name + " (recovered " + index + ")");
                if (!Directory.Exists(candidate) && !File.Exists(candidate)) return candidate;
            }
            throw new IOException("Could not find a unique destination name for " + name);
        }

        private static bool IsSameOrChild(string parent, string possibleChild)
        {
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(possibleChild)) return false;
            string parentPath = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string childPath = Path.GetFullPath(possibleChild).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return childPath.StartsWith(parentPath, StringComparison.OrdinalIgnoreCase);
        }
    }
}