using System;
using DriveMedic.Core;

namespace DriveMedic.Models
{
    public sealed class DiagnosticFinding
    {
        public DateTime Time { get; set; }
        public string Severity { get; set; }
        public string Category { get; set; }
        public string Location { get; set; }
        public string Summary { get; set; }
        public string Details { get; set; }
        public RepairAction RecommendedAction { get; set; }

        public DiagnosticFinding()
        {
            Time = DateTime.Now;
            Severity = "Info";
            Category = "General";
            Location = string.Empty;
            Summary = string.Empty;
            Details = string.Empty;
            RecommendedAction = RepairAction.None;
        }
    }
}