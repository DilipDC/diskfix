# Recovery Workbench

## Recommended workflow for an old or damaged PC

1. Shut down the old PC. If possible, connect its HDD/SSD to a working PC using a suitable SATA-to-USB adapter or enclosure. Do not initialize, format, reinstall Windows to, or run a write-mode repair on the source disk.
2. If the disk clicks, disconnects, overheats, stalls, or reports hardware warnings, minimize reads. Consider creating a sector image with a recovery-oriented imager or contacting a professional lab.
3. In DriveMedic's **Recover files** tab, add files/folders or choose **Add whole drive** for a mounted volume. Select a healthy destination on a separate physical disk. Keep SHA-256 verification enabled when feasible.
4. For a RAW/unmounted volume, lost partition, or permanently deleted files, install nothing on the source. Download the official [TestDisk & PhotoRec portable package](https://www.cgsecurity.org/wiki/TestDisk_Download), extract the complete ZIP, select its folder in DriveMedic, and launch TestDisk, PhotoRec, or QPhotoRec.
5. TestDisk is intended for partition analysis/recovery and certain filesystem boot-sector operations. PhotoRec carves supported file types by signatures. Carved output may not keep original filenames/folders, and data overwritten by later writes generally cannot be recovered.
6. Use **Single file** only when useful: hashing reads the entire file and may stress a failing drive. Without a known-good reference hash, SHA-256 does not prove a file is uncorrupted.
7. Use **EFI / Windows / GRUB** for read-only diagnostics and the relevant guide. Diagnostics describe the Windows installation currently running DriveMedic, not automatically the old attached system. For an unbootable Windows PC, boot its Windows recovery media and try Startup Repair. For GRUB, use a matching Linux live USB and the distribution's current instructions.
8. Do not run bcdboot, bootrec, partition-table writes, filesystem repairs, or TestDisk write options until the data is protected and the target/system/EFI partitions are positively identified. The GUI does not run these boot-write operations automatically.

## TestDisk/PhotoRec licensing and source

DriveMedic links to, but does not bundle or modify, the upstream GPL-2.0 project: [cgsecurity/testdisk](https://github.com/cgsecurity/testdisk). Obtain binaries from [the official CGSecurity download page](https://www.cgsecurity.org/wiki/TestDisk_Download), and extract all files and subdirectories before launch.

## Limits

No product can guarantee all permanently deleted files will return. Overwritten sectors, SSD TRIM, encryption, physical failure, unsupported formats and inaccessible media can prevent recovery. DriveMedic does not bypass BitLocker/EFS or repair arbitrary file types generically. Copy recovered data to a separate physical disk and protect the original when professional recovery is still an option.
