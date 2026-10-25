namespace DriveMedic.Models
{
    public sealed class PhysicalDiskRecord
    {
        public int Index { get; set; }
        public string DeviceId { get; set; }
        public string Model { get; set; }
        public long SizeBytes { get; set; }
        public string WindowsStatus { get; set; }
        public string Health { get; set; }
        public string Volumes { get; set; }
        public string Details { get; set; }
        public string SizeText { get { return SizeBytes > 0 ? DriveRecord.FormatBytes(SizeBytes) : "Unknown"; } }
    }
}