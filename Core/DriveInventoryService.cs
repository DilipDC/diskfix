using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;
using DriveMedic.Models;

namespace DriveMedic.Core
{
    public sealed class DriveInventoryService
    {
        private sealed class PhysicalDiskInfo
        {
            public int Index;
            public string DeviceId;
            public string Model;
            public string Status;
            public string PnpDeviceId;
            public long SizeBytes;
            public bool? SmartPredictFailure;
        }

        public IList<DriveRecord> GetVolumes()
        {
            List<DriveRecord> result = new List<DriveRecord>();
            Dictionary<int, PhysicalDiskInfo> physicalDisks = GetPhysicalDisks();
            Dictionary<string, int> volumeToDisk = MapVolumesToPhysicalDisks();
            string windowsRoot = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            if (!string.IsNullOrEmpty(windowsRoot)) windowsRoot = windowsRoot.TrimEnd('\\');

            foreach (DriveInfo volume in DriveInfo.GetDrives())
            {
                DriveRecord record = new DriveRecord();
                record.RootPath = volume.Name;
                record.Name = volume.Name.TrimEnd('\\');
                record.Role = "Data volume";
                record.Health = "Unknown";
                record.HealthDetails = "Hardware health could not be confirmed.";
                record.PhysicalDisk = "Not mapped";
                record.IsReady = false;

                try
                {
                    record.DriveType = volume.DriveType.ToString();
                    record.IsReady = volume.IsReady;
                    if (record.IsReady)
                    {
                        record.Label = string.Empty;
                        record.FileSystem = "Unknown";
                        try { record.Label = volume.VolumeLabel; }
                        catch (Exception ex) { record.HealthDetails = "Volume label unavailable: " + ex.Message; }
                        try { record.FileSystem = volume.DriveFormat; }
                        catch (Exception ex)
                        {
                            record.FileSystem = "RAW/Unknown";
                            record.HealthDetails = "Filesystem type could not be read. DriveMedic will not run filesystem repair on an unknown volume: " + ex.Message;
                        }
                        try { record.TotalBytes = volume.TotalSize; record.FreeBytes = volume.AvailableFreeSpace; }
                        catch (Exception ex) { record.HealthDetails = "Capacity metadata unavailable: " + ex.Message; }
                    }
                    else
                    {
                        record.Label = string.Empty;
                        record.FileSystem = "Unavailable";
                    }
                }
                catch (Exception ex)
                {
                    record.HealthDetails = "Volume metadata unavailable: " + ex.Message;
                }

                string root = record.RootPath.TrimEnd('\\');
                record.IsWindowsVolume = !string.IsNullOrEmpty(windowsRoot) && string.Equals(root, windowsRoot, StringComparison.OrdinalIgnoreCase);
                if (record.IsWindowsVolume) record.Role = "Current Windows OS volume";

                int diskIndex;
                if (volumeToDisk.TryGetValue(root, out diskIndex))
                {
                    PhysicalDiskInfo disk;
                    if (physicalDisks.TryGetValue(diskIndex, out disk))
                    {
                        record.PhysicalDiskIndex = diskIndex;
                        record.PhysicalDisk = "Disk " + disk.Index + " — " + (string.IsNullOrWhiteSpace(disk.Model) ? disk.DeviceId : disk.Model);
                        ApplyHealth(record, disk);
                    }
                }
                if (!record.IsReady && record.Health == "Unknown") record.HealthDetails = "Volume is present but not ready or is not mounted.";
                result.Add(record);
            }
            return result;
        }

        public IList<PhysicalDiskRecord> GetPhysicalDiskInventory()
        {
            Dictionary<int, PhysicalDiskInfo> disks = GetPhysicalDisks();
            Dictionary<string, int> volumeToDisk = MapVolumesToPhysicalDisks();
            List<PhysicalDiskRecord> result = new List<PhysicalDiskRecord>();
            foreach (PhysicalDiskInfo disk in disks.Values)
            {
                List<string> volumes = new List<string>();
                foreach (KeyValuePair<string, int> map in volumeToDisk)
                    if (map.Value == disk.Index) volumes.Add(map.Key);
                string status = (disk.Status ?? string.Empty).Trim();
                string health;
                string details;
                if (disk.SmartPredictFailure == true || (!string.IsNullOrEmpty(status) && status != "OK" && status != "Unknown"))
                {
                    health = "Warning";
                    details = disk.SmartPredictFailure == true ? "SMART failure prediction reported by the driver." : "Windows storage status: " + status + ".";
                }
                else if (status.Equals("OK", StringComparison.OrdinalIgnoreCase))
                {
                    health = "No warning";
                    details = disk.SmartPredictFailure == false ? "Windows status OK and available SMART predictor did not report failure." : "Windows status OK; SMART predictor unavailable or unsupported.";
                }
                else
                {
                    health = "Unknown";
                    details = "Windows did not provide a conclusive physical-drive status.";
                }
                result.Add(new PhysicalDiskRecord
                {
                    Index = disk.Index,
                    DeviceId = disk.DeviceId,
                    Model = disk.Model,
                    SizeBytes = disk.SizeBytes,
                    WindowsStatus = string.IsNullOrWhiteSpace(status) ? "Unknown" : status,
                    Health = health,
                    Volumes = volumes.Count == 0 ? "No drive letter mapped" : string.Join(", ", volumes),
                    Details = details
                });
            }
            result.Sort(delegate(PhysicalDiskRecord a, PhysicalDiskRecord b) { return a.Index.CompareTo(b.Index); });
            return result;
        }

        private static void ApplyHealth(DriveRecord record, PhysicalDiskInfo disk)
        {
            string status = (disk.Status ?? string.Empty).Trim();
            if (disk.SmartPredictFailure == true || (!string.IsNullOrEmpty(status) && status != "OK" && status != "Unknown"))
            {
                record.Health = "Warning";
                record.HealthDetails = disk.SmartPredictFailure == true
                    ? "Drive firmware reports a SMART failure prediction. Back up or image data before other actions."
                    : "Windows storage provider reports disk status: " + status + ". Hardware health needs attention.";
                return;
            }
            if (status.Equals("OK", StringComparison.OrdinalIgnoreCase) && disk.SmartPredictFailure == false)
            {
                record.Health = "No warning";
                record.HealthDetails = "Windows status is OK and the available SMART predictor reports no failure. This is not a guarantee the drive is healthy.";
            }
            else if (status.Equals("OK", StringComparison.OrdinalIgnoreCase))
            {
                record.Health = "No warning";
                record.HealthDetails = "Windows reports status OK; SMART prediction was unavailable or unsupported.";
            }
            else
            {
                record.Health = "Unknown";
                record.HealthDetails = "The disk was mapped, but its health could not be conclusively determined.";
            }
        }

        private static Dictionary<int, PhysicalDiskInfo> GetPhysicalDisks()
        {
            Dictionary<int, PhysicalDiskInfo> disks = new Dictionary<int, PhysicalDiskInfo>();
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Index, DeviceID, Model, Size, Status, PNPDeviceID FROM Win32_DiskDrive"))
                using (ManagementObjectCollection items = searcher.Get())
                {
                    foreach (ManagementObject item in items)
                    {
                        int index = Convert.ToInt32(item["Index"]);
                        PhysicalDiskInfo disk = new PhysicalDiskInfo();
                        disk.Index = index;
                        disk.DeviceId = Convert.ToString(item["DeviceID"]);
                        disk.Model = Convert.ToString(item["Model"]);
                        disk.Status = Convert.ToString(item["Status"]);
                        disk.PnpDeviceId = Convert.ToString(item["PNPDeviceID"]);
                        try { disk.SizeBytes = Convert.ToInt64(item["Size"]); } catch { disk.SizeBytes = 0; }
                        disks[index] = disk;
                    }
                }
                MapSmartStatus(disks);
            }
            catch
            {
                // WMI may be unavailable on restricted or older installations; report health as unknown.
            }
            return disks;
        }

        private static void MapSmartStatus(Dictionary<int, PhysicalDiskInfo> disks)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\WMI", "SELECT InstanceName, PredictFailure FROM MSStorageDriver_FailurePredictStatus"))
                using (ManagementObjectCollection items = searcher.Get())
                {
                    foreach (ManagementObject item in items)
                    {
                        string instanceName = Convert.ToString(item["InstanceName"]);
                        bool prediction = Convert.ToBoolean(item["PredictFailure"]);
                        foreach (PhysicalDiskInfo disk in disks.Values)
                        {
                            string pnp = disk.PnpDeviceId ?? string.Empty;
                            if (!string.IsNullOrEmpty(pnp) && (instanceName.StartsWith(pnp, StringComparison.OrdinalIgnoreCase) || pnp.StartsWith(instanceName.TrimEnd('&', '0'), StringComparison.OrdinalIgnoreCase)))
                                disk.SmartPredictFailure = prediction;
                        }
                    }
                }
            }
            catch
            {
                // Many USB bridges and some SSD/NVMe drivers do not expose this legacy SMART WMI provider.
            }
        }

        private static Dictionary<string, int> MapVolumesToPhysicalDisks()
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> partitionToDisk = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Regex partitionRegex = new Regex("Disk #(\\d+), Partition #(\\d+)", RegexOptions.IgnoreCase);
            Regex driveRegex = new Regex("DeviceID=\"([A-Za-z]:)\"", RegexOptions.IgnoreCase);
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Antecedent, Dependent FROM Win32_DiskDriveToDiskPartition"))
                using (ManagementObjectCollection items = searcher.Get())
                {
                    foreach (ManagementObject item in items)
                    {
                        Match match = partitionRegex.Match(Convert.ToString(item["Dependent"]));
                        if (!match.Success) continue;
                        int index;
                        if (int.TryParse(match.Groups[1].Value, out index)) partitionToDisk["Disk #" + match.Groups[1].Value + ", Partition #" + match.Groups[2].Value] = index;
                    }
                }
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Antecedent, Dependent FROM Win32_LogicalDiskToPartition"))
                using (ManagementObjectCollection items = searcher.Get())
                {
                    foreach (ManagementObject item in items)
                    {
                        Match partMatch = partitionRegex.Match(Convert.ToString(item["Antecedent"]));
                        Match driveMatch = driveRegex.Match(Convert.ToString(item["Dependent"]));
                        if (!partMatch.Success || !driveMatch.Success) continue;
                        string partKey = "Disk #" + partMatch.Groups[1].Value + ", Partition #" + partMatch.Groups[2].Value;
                        int index;
                        if (partitionToDisk.TryGetValue(partKey, out index)) result[driveMatch.Groups[1].Value] = index;
                    }
                }
            }
            catch
            {
                // A volume can still be listed even when WMI association mapping is blocked.
            }
            return result;
        }
    }
}