# DriveMedic: technician workflow

Use this checklist when a Windows SSD no longer boots but another Windows installation can see it as a secondary drive.

1. **Protect the source first.** Do not format, initialize, reinstall Windows to, or run a write-mode repair on the source SSD. If it clicks, disconnects, reports a hardware/SMART warning, or contains irreplaceable data, stop broad scans and use a professional imaging/recovery workflow.
2. **Inventory the drives.** Boot DriveMedic from a known-good Windows installation on a different disk. Refresh the inventory and confirm which volume contains the currently running Windows. Compare capacity, model, volume labels, and physical-disk mapping before selecting a source.
3. **Treat health status conservatively.** `Warning` means stop ordinary repair attempts and recover/image first. `Unknown` means Windows could not provide conclusive health information. `No warning` is not a guarantee of health, especially for NVMe, RAID, USB bridges, and unsupported SMART providers.
4. **Find the old profile.** In **Browse & Recover**, choose the source volume, select **Find user profiles**, then open the old user profile and locate Desktop, Documents, Downloads, Pictures, Music, and Videos. Hidden and user-created folders are included when Windows permits enumeration.
5. **Copy to another physical disk.** Select the files/folders and choose **Copy selected to…**. Prefer a healthy external or secondary disk. Choose **Yes** to verify each copied file using SHA-256, or **No** for a faster copy without hash verification. DriveMedic skips symbolic links/junctions to avoid following paths outside the selected tree and never overwrites an existing destination file.
6. **Review the result.** Inspect copied-file, byte, SHA-256 verification, skipped-link, and error counts. Read the Activity & Report tab and export a local TXT/CSV report for the service ticket. Do not put customer files or passwords in the report.
7. **Diagnose only after recovery is protected.** A read-only CHKDSK check and readability scan still read the source and can put load on weak media. A readability error proves only that a read failed; it does not identify the exact cause or guarantee repairability.
8. **Repair only with informed approval.** `CHKDSK X: /f` modifies filesystem metadata and is blocked by DriveMedic when a hardware warning or RAW/unknown filesystem is identified. Back up or image first. SFC repairs only the currently running Windows installation; it is not an offline repair utility for an old Windows installation on a secondary SSD.
9. **Verify before closing the job.** Reopen representative recovered files from the destination, ensure the source was not selected as the destination, save the report, and record any unresolved errors or encryption limitations.

## Recovery Workbench additions

- The **Recover files** tab can queue individual files/folders or the visible top-level contents of a mounted drive. Save output to a different physical disk and leave SHA-256 verification enabled when practical.
- The **Single file** tab reads every byte only after acknowledgement and reports a SHA-256 hash. This is a readability/fingerprint check, not a universal corruption detector or file repair mechanism.
- For deleted files or lost partitions, download the official [TestDisk & PhotoRec portable ZIP](https://www.cgsecurity.org/wiki/TestDisk_Download), extract all files/subfolders, select that folder in the **Deleted files & partitions** tab, then launch TestDisk, PhotoRec, or QPhotoRec. Use a separate destination disk.
- The **EFI / Windows / GRUB** tab collects read-only diagnostics for the currently running Windows instance and provides guided notes. It does not automatically repair an attached offline PC.
- Deleted-file recovery is not guaranteed. Stop writing to the source; overwritten sectors cannot be recreated. Generic one-click repair of arbitrary file formats is not possible without file-specific validation or trusted replacement data.

## Important boundaries

- DriveMedic does not bypass BitLocker, EFS, access controls, firmware locks, or customer authorization.
- It does not perform sector imaging, RAID reconstruction, deleted-file carving, NTFS metadata reconstruction, or generic content-level validation for every file format.
- SHA-256 verification checks that the bytes written to the staged destination match the bytes read from the source during that copy. It does not prove the source file was uncorrupted before copying.
- Never promise recovery of files the operating system cannot read. Keep the original drive unchanged when professional recovery is still an option.