using System;

namespace DriveMedic.Models
{
    public sealed class DriveRecord
    {
        public string Name { get; set; }
        public string RootPath { get; set; }
        public string Label { get; set; }
        public string DriveType { get; set; }
        public string FileSystem { get; set; }
        public string Health { get; set; }
        public string HealthDetails { get; set; }
        public string Role { get; set; }
        public string PhysicalDisk { get; set; }
        public int? PhysicalDiskIndex { get; set; }
        public long TotalBytes { get; set; }
        public long FreeBytes { get; set; }
        public bool IsReady { get; set; }
        public bool IsWindowsVolume { get; set; }

        public string DisplayName
        {
            get { return string.IsNullOrWhiteSpace(Label) ? RootPath : RootPath + "  " + Label; }
        }

        public string CapacityText
        {
            get { return IsReady ? FormatBytes(TotalBytes - FreeBytes) + " used / " + FormatBytes(TotalBytes) : "Not ready"; }
        }

        public string FreeText { get { return IsReady ? FormatBytes(FreeBytes) : "—"; } }

        public static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = Math.Max(0, bytes);
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
            return value.ToString(value >= 100 || unit == 0 ? "0" : "0.0") + " " + units[unit];
        }

        public override string ToString() { return DisplayName; }
    }
}