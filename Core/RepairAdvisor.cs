namespace DriveMedic.Core
{
    public enum FindingKind
    {
        Information,
        AccessDenied,
        ReadError,
        FileSystemError,
        RawVolume,
        HardwareWarning,
        SystemFileIssue,
        ScanLimitReached
    }

    public enum RepairAction
    {
        None,
        RetryAsAdministrator,
        CopyRecoverableData,
        RunChkdskRepair,
        RunSystemFileChecker,
        StopAndRecoverOrImageDrive
    }

    /// <summary>Centralized, conservative mapping from a finding to its next safe action.</summary>
    public static class RepairAdvisor
    {
        public static RepairAction Recommend(FindingKind kind, bool isCurrentWindowsVolume, bool hardwareWarning, bool rawVolume)
        {
            // Hardware and RAW conditions always override software repair suggestions.
            if (hardwareWarning || kind == FindingKind.HardwareWarning) return RepairAction.StopAndRecoverOrImageDrive;
            if (rawVolume || kind == FindingKind.RawVolume) return RepairAction.StopAndRecoverOrImageDrive;

            switch (kind)
            {
                case FindingKind.AccessDenied:
                    return RepairAction.RetryAsAdministrator;
                case FindingKind.ReadError:
                    return RepairAction.CopyRecoverableData;
                case FindingKind.FileSystemError:
                    return RepairAction.RunChkdskRepair;
                case FindingKind.SystemFileIssue:
                    return isCurrentWindowsVolume ? RepairAction.RunSystemFileChecker : RepairAction.CopyRecoverableData;
                default:
                    return RepairAction.None;
            }
        }

        public static string Explain(RepairAction action)
        {
            switch (action)
            {
                case RepairAction.RetryAsAdministrator:
                    return "Retry with administrator rights. This does not bypass BitLocker or EFS encryption.";
                case RepairAction.CopyRecoverableData:
                    return "Copy readable files to a different physical drive before attempting repairs.";
                case RepairAction.RunChkdskRepair:
                    return "Back up or image the drive first, then run CHKDSK /f only if hardware failure is not suspected.";
                case RepairAction.RunSystemFileChecker:
                    return "Run SFC on the currently running Windows installation only.";
                case RepairAction.StopAndRecoverOrImageDrive:
                    return "Do not run filesystem repairs yet. Minimize writes; recover or image the drive, then assess hardware.";
                default:
                    return "No repair action is recommended for this finding.";
            }
        }
    }
}